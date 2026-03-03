using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using ConfocalMeter.Domain.Interfaces;

namespace ConfocalMeter
{
    /// <summary>
    /// MCU串口通信管理器 - 用于与STM32单片机通信，控制CM35电机控制器的输入输出
    /// 采用单例模式，供主界面和IO控制子窗体共享
    /// </summary>
    public class McuSerialManager : IMcuDriver
    {
        #region 单例模式

        private static McuSerialManager _instance;
        private static readonly object _lock = new object();

        public static McuSerialManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
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

        #endregion

        #region 协议定义

        private const byte HEAD = 0xAA;
        private const byte CMD_CTRL_QUERY = 0x01;
        private const byte TAIL = 0x55;

        #endregion

        #region 私有成员

        private SerialPort _serialPort;
        private List<byte> _buffer = new List<byte>();
        private DateTime _lastPacketTime = DateTime.MinValue;
        private Thread _receiveThread;
        private volatile bool _isRunning = false;
        private byte _currentInputMap = 0; // 当前输入控制位图
        private byte _currentOutputState = 0; // 当前输出状态

        #endregion

        #region 公共属性

        /// <summary>
        /// 串口是否已打开
        /// </summary>
        public bool IsOpen => _serialPort != null && _serialPort.IsOpen;

        /// <summary>
        /// 当前端口名
        /// </summary>
        public string PortName => _serialPort?.PortName ?? "";

        /// <summary>
        /// 通信是否正常 (500ms内有响应)
        /// </summary>
        public bool IsConnected => IsOpen && (DateTime.Now - _lastPacketTime).TotalMilliseconds < 500;

        /// <summary>
        /// 当前输出状态
        /// </summary>
        public byte OutputState => _currentOutputState;

        /// <summary>
        /// 当前输入控制位图
        /// </summary>
        public byte InputMap => _currentInputMap;

        #endregion

        #region 事件

        /// <summary>
        /// 日志事件
        /// </summary>
        public event Action<string> OnLog;

        /// <summary>
        /// 输出状态更新事件
        /// </summary>
        public event Action<byte> OnOutputStateChanged;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event Action<bool> OnConnectionChanged;

        #endregion

        #region 公共方法

        /// <summary>
        /// 打开串口
        /// </summary>
        public bool Open(string portName, int baudRate = 115200)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    Close();
                }

                _serialPort = new SerialPort()
                {
                    PortName = portName,
                    BaudRate = baudRate,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Parity = Parity.None,
                    ReadTimeout = 100,
                    WriteTimeout = 100
                };

                _serialPort.Open();
                _buffer.Clear();
                _lastPacketTime = DateTime.MinValue;
                _isRunning = true;

                // 启动独立的接收和发送线程
                _receiveThread = new Thread(CommunicationLoop)
                {
                    IsBackground = true,
                    Name = "MCU_Serial_Thread"
                };
                _receiveThread.Start();

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

        /// <summary>
        /// 关闭串口
        /// </summary>
        public void Close()
        {
            _isRunning = false;

            // 等待线程结束
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
                catch { }
                _serialPort = null;
            }

            _currentOutputState = 0;
            Log("串口已关闭");
            OnConnectionChanged?.Invoke(false);
        }

        /// <summary>
        /// 设置输入控制位图
        /// </summary>
        /// <param name="inputMap">8位输入控制位图</param>
        public void SetInputMap(byte inputMap)
        {
            _currentInputMap = inputMap;
        }

        /// <summary>
        /// 设置单个输入位
        /// </summary>
        public void SetInputBit(int index, bool value)
        {
            if (index < 0 || index > 7) return;

            if (value)
                _currentInputMap |= (byte)(1 << index);
            else
                _currentInputMap &= (byte)~(1 << index);
        }

        /// <summary>
        /// 获取输出位状态
        /// </summary>
        public bool GetOutputBit(int index)
        {
            if (index < 0 || index > 7) return false;
            return (_currentOutputState & (1 << index)) != 0;
        }

        #endregion

        #region 通信线程

        /// <summary>
        /// 通信循环 - 在独立线程中运行，避免阻塞UI
        /// </summary>
        private void CommunicationLoop()
        {
            int sendCounter = 0;

            while (_isRunning && _serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    // 每200ms发送一次控制帧
                    if (sendCounter >= 200)
                    {
                        SendFrame();
                        sendCounter = 0;
                    }

                    // 接收数据
                    ReceiveData();

                    Thread.Sleep(10);
                    sendCounter += 10;
                }
                catch (Exception ex)
                {
                    if (_isRunning) // 只有在运行时才记录异常
                    {
                        Log($"通信异常: {ex.Message}");
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 发送控制帧
        /// </summary>
        private void SendFrame()
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                byte[] frame = new byte[6];
                frame[0] = HEAD;
                frame[1] = CMD_CTRL_QUERY;
                frame[2] = _currentInputMap;
                frame[3] = 0x00;

                long sum = frame[0] + frame[1] + frame[2] + frame[3];
                frame[4] = (byte)(sum & 0xFF);
                frame[5] = TAIL;

                _serialPort.Write(frame, 0, frame.Length);
            }
            catch { }
        }

        /// <summary>
        /// 接收并处理数据
        /// </summary>
        private void ReceiveData()
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead > 0)
                {
                    byte[] temp = new byte[bytesToRead];
                    _serialPort.Read(temp, 0, bytesToRead);

                    lock (_buffer)
                    {
                        _buffer.AddRange(temp);
                    }

                    ProcessBuffer();
                }
            }
            catch { }
        }

        /// <summary>
        /// 处理接收缓冲区
        /// </summary>
        private void ProcessBuffer()
        {
            lock (_buffer)
            {
                while (_buffer.Count >= 6)
                {
                    if (_buffer[0] != HEAD)
                    {
                        _buffer.RemoveAt(0);
                        continue;
                    }

                    if (_buffer[5] != TAIL)
                    {
                        _buffer.RemoveAt(0);
                        continue;
                    }

                    long calcSum = _buffer[0] + _buffer[1] + _buffer[2] + _buffer[3];
                    byte calcChecksum = (byte)(calcSum & 0xFF);
                    byte recvChecksum = _buffer[4];

                    if (calcChecksum == recvChecksum)
                    {
                        byte cmd = _buffer[1];
                        byte outputState = _buffer[3];

                        if (cmd == CMD_CTRL_QUERY)
                        {
                            if (_currentOutputState != outputState)
                            {
                                _currentOutputState = outputState;
                                OnOutputStateChanged?.Invoke(outputState);
                            }
                            else
                            {
                                _currentOutputState = outputState;
                            }
                        }

                        byte[] validFrame = new byte[6];
                        _buffer.CopyTo(0, validFrame, 0, 6);
                        Log($"RX: {BitConverter.ToString(validFrame)}");

                        _buffer.RemoveRange(0, 6);
                        _lastPacketTime = DateTime.Now;
                    }
                    else
                    {
                        Log($"校验失败: 收到{recvChecksum:X2} 计算{calcChecksum:X2}");
                        _buffer.RemoveAt(0);
                    }
                }
            }
        }

        #endregion

        #region 辅助方法

        private void Log(string msg)
        {
            OnLog?.Invoke(msg);
        }

        #endregion
    }
}
