using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using ConfocalMeter.Domain.Interfaces;

namespace ConfocalMeter
{
    /// <summary>
    /// MCU串口通信管理器。
    /// </summary>
    public class McuSerialManager : IMcuDriver
    {
        private static McuSerialManager _instance;
        private static readonly object _instanceLock = new object();

        private const byte HEAD = 0xAA;
        private const byte CMD_CTRL_QUERY = 0x01;
        private const byte TAIL = 0x55;
        private const int FRAME_LEN = 6;
        private const int MAX_RX_BUFFER = 4096;
        private const int RX_LOG_INTERVAL = 20;
        private const int SEND_INTERVAL_MS = 50;

        private SerialPort _serialPort;
        private readonly List<byte> _buffer = new List<byte>();
        private readonly object _stateLock = new object();
        private readonly object _txLock = new object();
        private DateTime _lastPacketTime = DateTime.MinValue;
        private Thread _receiveThread;
        private volatile bool _isRunning;
        private byte _currentInputMap;
        private byte _currentOutputState;
        private int _rxFrameCounter;

        public static McuSerialManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new McuSerialManager();
                        }
                    }
                }

                return _instance;
            }
        }

        private McuSerialManager() { }

        public bool IsOpen => _serialPort != null && _serialPort.IsOpen;

        public string PortName => _serialPort?.PortName ?? string.Empty;

        public bool IsConnected => IsOpen && (DateTime.Now - _lastPacketTime).TotalMilliseconds < 500;

        public byte OutputState => _currentOutputState;

        public byte InputMap => _currentInputMap;

        public event Action<string> OnLog;

        public event Action<byte> OnOutputStateChanged;

        public event Action<bool> OnConnectionChanged;

        public bool Open(string portName, int baudRate = 115200)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    Close();
                }

                _serialPort = new SerialPort
                {
                    PortName = portName,
                    BaudRate = baudRate,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Parity = Parity.None,
                    ReadTimeout = 100,
                    WriteTimeout = 100,
                    DtrEnable = true,
                    RtsEnable = true
                };

                _serialPort.Open();
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                lock (_buffer)
                {
                    _buffer.Clear();
                }

                // 重连后默认进入安全态：所有输入请求清零
                lock (_stateLock)
                {
                    _currentInputMap = 0;
                }
                _currentOutputState = 0;
                _rxFrameCounter = 0;
                _lastPacketTime = DateTime.MinValue;
                _isRunning = true;

                _receiveThread = new Thread(CommunicationLoop)
                {
                    IsBackground = true,
                    Name = "MCU_Serial_Thread"
                };
                _receiveThread.Start();

                // 打开后立即发送一帧，避免首次IO切换等待周期发送
                SendFrame();
                Log($"串口已打开: {portName}");
                OnConnectionChanged?.Invoke(true);
                return true;
            }
            catch (Exception ex)
            {
                Log($"打开串口失败: {ex.Message}");
                return false;
            }
        }

        public void Close()
        {
            // 关闭前主动下发一次安全态（InputMap=0），形成上下位机双保险
            if (_serialPort != null && _serialPort.IsOpen)
            {
                lock (_stateLock)
                {
                    _currentInputMap = 0;
                }
                SendFrame();
                Thread.Sleep(20);
                SendFrame();
            }

            _isRunning = false;

            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join(500);
            }

            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }

                    _serialPort.Dispose();
                }
                catch (Exception ex)
                {
                    Log($"关闭串口异常: {ex.Message}");
                }
                finally
                {
                    _serialPort = null;
                }
            }

            _currentOutputState = 0;
            lock (_stateLock)
            {
                _currentInputMap = 0;
            }
            Log("串口已关闭");
            OnConnectionChanged?.Invoke(false);
        }

        public void SetInputMap(byte inputMap)
        {
            lock (_stateLock)
            {
                _currentInputMap = inputMap;
            }

            // 映射变化后立即下发，提升IO控制实时性
            SendFrame();
        }

        public void SetInputBit(int bitIndex, bool value)
        {
            if (bitIndex < 0 || bitIndex > 7)
            {
                return;
            }

            bool changed = false;

            lock (_stateLock)
            {
                byte oldMap = _currentInputMap;
                if (value)
                {
                    _currentInputMap |= (byte)(1 << bitIndex);
                }
                else
                {
                    _currentInputMap &= (byte)~(1 << bitIndex);
                }

                changed = oldMap != _currentInputMap;
            }

            // 用户点击后立即下发，避免“勾选后无响应”的体感延迟
            if (changed)
            {
                SendFrame();
            }
        }

        public bool GetOutputBit(int bitIndex)
        {
            if (bitIndex < 0 || bitIndex > 7)
            {
                return false;
            }

            return (_currentOutputState & (1 << bitIndex)) != 0;
        }

        private void CommunicationLoop()
        {
            var loopSw = System.Diagnostics.Stopwatch.StartNew();
            long nextSendMs = 0;

            while (_isRunning && _serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    if (loopSw.ElapsedMilliseconds >= nextSendMs)
                    {
                        SendFrame();
                        nextSendMs = loopSw.ElapsedMilliseconds + SEND_INTERVAL_MS;
                    }

                    ReceiveData();

                    Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Log($"通信异常: {ex.Message}");
                        OnConnectionChanged?.Invoke(false);
                    }

                    break;
                }
            }
        }

        private void SendFrame()
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                return;
            }

            try
            {
                byte inputMapSnapshot;
                lock (_stateLock)
                {
                    inputMapSnapshot = _currentInputMap;
                }

                byte[] frame = new byte[FRAME_LEN];
                frame[0] = HEAD;
                frame[1] = CMD_CTRL_QUERY;
                frame[2] = inputMapSnapshot;
                frame[3] = 0x00;

                long sum = frame[0] + frame[1] + frame[2] + frame[3];
                frame[4] = (byte)(sum & 0xFF);
                frame[5] = TAIL;

                lock (_txLock)
                {
                    _serialPort.Write(frame, 0, frame.Length);
                }
            }
            catch (Exception ex)
            {
                Log($"发送失败: {ex.Message}");
            }
        }

        private void ReceiveData()
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                return;
            }

            try
            {
                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead <= 0)
                {
                    return;
                }

                byte[] temp = new byte[bytesToRead];
                _serialPort.Read(temp, 0, bytesToRead);

                lock (_buffer)
                {
                    _buffer.AddRange(temp);
                    if (_buffer.Count > MAX_RX_BUFFER)
                    {
                        int drop = _buffer.Count - MAX_RX_BUFFER;
                        _buffer.RemoveRange(0, drop);
                        Log($"接收缓冲区溢出，丢弃 {drop} 字节旧数据");
                    }
                }

                ProcessBuffer();
            }
            catch (Exception ex)
            {
                Log($"接收失败: {ex.Message}");
            }
        }

        private void ProcessBuffer()
        {
            byte? changedOutput = null;
            byte[] latestFrame = null;
            string checksumError = null;

            lock (_buffer)
            {
                int idx = 0;

                while (_buffer.Count - idx >= FRAME_LEN)
                {
                    if (_buffer[idx] != HEAD)
                    {
                        idx++;
                        continue;
                    }

                    if (_buffer[idx + FRAME_LEN - 1] != TAIL)
                    {
                        idx++;
                        continue;
                    }

                    long calcSum = _buffer[idx] + _buffer[idx + 1] + _buffer[idx + 2] + _buffer[idx + 3];
                    byte calcChecksum = (byte)(calcSum & 0xFF);
                    byte recvChecksum = _buffer[idx + 4];

                    if (calcChecksum == recvChecksum)
                    {
                        byte cmd = _buffer[idx + 1];
                        byte outputState = _buffer[idx + 3];

                        if (cmd == CMD_CTRL_QUERY && _currentOutputState != outputState)
                        {
                            _currentOutputState = outputState;
                            changedOutput = outputState;
                        }
                        else
                        {
                            _currentOutputState = outputState;
                        }

                        latestFrame = new byte[FRAME_LEN];
                        _buffer.CopyTo(idx, latestFrame, 0, FRAME_LEN);
                        idx += FRAME_LEN;
                        _lastPacketTime = DateTime.Now;
                    }
                    else
                    {
                        checksumError = $"校验失败: 收到{recvChecksum:X2} 计算{calcChecksum:X2}";
                        idx++;
                    }
                }

                if (idx > 0)
                {
                    _buffer.RemoveRange(0, idx);
                }
            }

            if (changedOutput.HasValue)
            {
                OnOutputStateChanged?.Invoke(changedOutput.Value);
            }

            if (!string.IsNullOrEmpty(checksumError))
            {
                Log(checksumError);
            }

            if (latestFrame != null)
            {
                _rxFrameCounter++;
                if (_rxFrameCounter >= RX_LOG_INTERVAL)
                {
                    _rxFrameCounter = 0;
                    Log($"RX: {BitConverter.ToString(latestFrame)}");
                }
            }
        }

        private void Log(string msg)
        {
            OnLog?.Invoke(msg);
        }
    }
}
