using System;
using System.Threading;
using System.Runtime.InteropServices;
using tscmccs;
using InstanceHandle = System.UInt64;

namespace ConfocalMeter
{
    public class SensorManager
    {
        private InstanceHandle _instance;
        private int _controllerIdx = 0; // 控制器索引通常是 0

        // 【核心修正】通道索引必须是 1 (CH1)，绝对不能是 0 (CONTROLLER)
        // 0 代表控制器本身(只有时间戳等数据)，1 代表第一个探头
        private int _sensorIdx = 1;

        public bool IsConnected { get; private set; } = false;

        public event Action<string> OnLog;
        public event Action<bool> OnConnectionChanged;

        public SensorManager()
        {
            _instance = TSCMCAPICS.CreateInstance();
        }

        public bool Connect(string ipStr, int remotePort, int localPort)
        {
            try
            {
                IPAddr addr = ParseIpToStruct(ipStr);

                var err = TSCMCAPICS.OpenConnectionEthernet(_instance, addr, localPort);
                if (err != ERRCODE.OK) { Log($"打开监听失败: {err}"); return false; }

                err = TSCMCAPICS.SetConnectionOn(_instance, _controllerIdx);
                if (err != ERRCODE.OK)
                {
                    Log($"握手失败: {err}");
                    TSCMCAPICS.CloseConnectionPort(_instance);
                    return false;
                }

                // 1. 针对单通道设备，不需要 SetContorllerChannelEnable
                // 但我们必须确保 _sensorIdx 是正确的。
                // 我们可以动态获取一下最大通道数，如果是单通道，通常 MaxSensorChannels 返回 1
                int maxCh = TSCMCAPICS.MaxSensorChannels(_instance);
                Log($"控制器支持通道数: {maxCh}");
                _sensorIdx = 1; // 强制指定为通道1

                // 2. 配置输出信号 (激活通道)
                // 告诉控制器：通道1 (Index=1) 准备输出距离1
                int[] dataSel = new int[] { (int)SENSOR_OUTPUT_DATA.DIST1 };
                var connType = TSCMCAPICS.GetConnectionType(_instance);

                var cfgErr = TSCMCAPICS.SetConfigOutputSignals(_instance, _controllerIdx, _sensorIdx, connType, ref dataSel[0], 1);
                if (cfgErr != ERRCODE.OK) Log($"输出配置警告: {cfgErr}");

                // 3. 开启光源 (针对通道1)
                TSCMCAPICS.SetConfigLightSource(_instance, _controllerIdx, _sensorIdx, STATE.ON);

                IsConnected = true;
                OnConnectionChanged?.Invoke(true);

                ForceStopMeasurement(); // 清空状态

                Log("设备连接成功 (通道1已激活)");
                return true;
            }
            catch (Exception ex)
            {
                Log($"连接异常: {ex.Message}");
                return false;
            }
        }

        // 在 SensorManager 类中添加以下方法
        public bool IsDeviceAcquiring()
        {
            if (!IsConnected) return false;
            return TSCMCAPICS.isAcquireData(_instance);
        }

        // --- 在 SensorManager 类中添加 ---

        /// <summary>
        /// 读取当前设备的采样间隔
        /// </summary>
        public string GetCurrentSamplingInterval()
        {
            if (!IsConnected) return "未连接";

            SAMPLING_INTERVAL interval = SAMPLING_INTERVAL._1MS; // 默认

            // 调用 SDK 读取
            var err = TSCMCAPICS.GetConfigSamplingInterval(_instance, _controllerIdx, ref interval);

            if (err == ERRCODE.OK)
            {
                return interval.ToString(); // 返回枚举的字符串，如 "_500US"
            }
            else
            {
                return $"读取失败({err})";
            }
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                ForceStopMeasurement();
                TSCMCAPICS.SetConnectionOff(_instance, _controllerIdx);
                TSCMCAPICS.CloseConnectionPort(_instance);
                IsConnected = false;
                OnConnectionChanged?.Invoke(false);
                Log("已断开连接");
            }
        }

        public void ForceStopMeasurement()
        {
            if (!IsConnected) return;
            TSCMCAPICS.SetDataOutputOff(_instance, _controllerIdx);
            TSCMCAPICS.ClearRingBuffer(_instance);
            Thread.Sleep(50);
        }

        // --- 图像获取 (Index 已修正为 1) ---
        public (double[], ERRCODE) GetRawImageDebug()
        {
            if (!IsConnected) return (null, ERRCODE.DEVICE_NOT_CONNECTED);

            int pixelNum = ConstDef.CMOS_PIXEL_NUM;
            double[] rawData = new double[pixelNum];
            int realSize = 0;

            // 这里传入 _sensorIdx=1，获取通道1的图像
            var err = TSCMCAPICS.GetDataFrameSingle(_instance, _controllerIdx, _sensorIdx,
                                                    ref rawData[0], ref realSize, pixelNum);

            if (err == ERRCODE.CMD_FAILED || err == ERRCODE.IS_ACQUIRE_DATA)
            {
                // 如果失败，尝试再次停止后重试
                ForceStopMeasurement();
                Thread.Sleep(20);
                err = TSCMCAPICS.GetDataFrameSingle(_instance, _controllerIdx, _sensorIdx,
                                                    ref rawData[0], ref realSize, pixelNum);
            }

            if (err == ERRCODE.OK && realSize > 0)
            {
                return (rawData, ERRCODE.OK);
            }
            return (null, err);
        }

        // --- 量程获取 (Index 已修正为 1) ---
        public bool GetRangePixelLimits(out int startPixel, out int endPixel)
        {
            startPixel = 20;
            endPixel = 1000;

            if (!IsConnected) return false;

            // 这里传入 _sensorIdx=1，获取通道1的量程
            var err = TSCMCAPICS.GetConfigRangeEdgePixel(_instance, _controllerIdx, _sensorIdx, ref startPixel, ref endPixel);

            if (err == ERRCODE.OK) return true;

            Log($"获取量程失败 (Err: {err})");
            return false;
        }

        public void SetupForDistanceMeasurement()
        {
            if (!IsConnected) return;

            // 所有配置都针对 _sensorIdx (1)
            TSCMCAPICS.SetConfigFrameDataSource(_instance, _controllerIdx, FRAME_DATA_SRC.CALIB);
            TSCMCAPICS.SetConfigLightSource(_instance, _controllerIdx, _sensorIdx, STATE.ON);
            TSCMCAPICS.SetConfigLightIntensity(_instance, _controllerIdx, _sensorIdx, 100);

            ExposureConfig exp = new ExposureConfig { auto_control = STATE.ON, exposure_time = 5000 };
            TSCMCAPICS.SetConfigExposure(_instance, _controllerIdx, _sensorIdx, exp);
            TSCMCAPICS.SetConfigAutoExposureTarget(_instance, _controllerIdx, _sensorIdx, 4000);

            try
            {
                PeakSelection ps = new PeakSelection();
                // 先读取，再修改，再写入
                TSCMCAPICS.GetConfigPeakSelection(_instance, _controllerIdx, _sensorIdx, ref ps);
                ps.mode = PEAK_SELECTION_MODE.MAX;
                TSCMCAPICS.SetConfigPeakSelection(_instance, _controllerIdx, _sensorIdx, ps);
            }
            catch { /* 忽略 */ }

            Log("测距参数已配置");
        }

        public ERRCODE PerformDarkCalibration()
        {
            if (!IsConnected) return ERRCODE.DEVICE_NOT_CONNECTED;
            DarkReferenceTable table = new DarkReferenceTable();
            table.refr.data = new short[ConstDef.CMOS_PIXEL_NUM];
            table.coeff.data = new ushort[ConstDef.CMOS_PIXEL_NUM];

            // 针对 _sensorIdx (1) 进行暗校准
            return TSCMCAPICS.DarkCalibration(_instance, _controllerIdx, _sensorIdx, ref table);
        }

        public void SetDataTransfer(bool enable)
        {
            if (!IsConnected) return;
            if (enable)
            {
                TSCMCAPICS.SetDataOutputOn(_instance, _controllerIdx);
                TSCMCAPICS.ClearRingBuffer(_instance);
            }
            else
            {
                TSCMCAPICS.SetDataOutputOff(_instance, _controllerIdx);
            }
        }

        public int ReadBuffer(DataNode[] buffer, int maxCount)
        {
            if (!IsConnected) return 0;
            int nRead = 0;
            var err = TSCMCAPICS.TransferDataNode(_instance, ref buffer[0], ref nRead, maxCount);
            return (err == ERRCODE.OK) ? nRead : 0;
        }

        public void SetSamplingInterval(SAMPLING_INTERVAL interval) => TSCMCAPICS.SetConfigSamplingInterval(_instance, _controllerIdx, interval);
        public void SetHoldPoints(int points) => TSCMCAPICS.SetWarningHoldPoints(_instance, _controllerIdx, points);

        public bool CheckDataStatus()
        {
            if (!IsConnected) return false;
            DataNode[] node = new DataNode[1];
            int nread = 0;
            var err = TSCMCAPICS.GetSingleDataNode(_instance, _controllerIdx, ref node[0], ref nread, 1);
            return (err == ERRCODE.OK && nread > 0);
        }

        private IPAddr ParseIpToStruct(string ipStr)
        {
            IPAddr ipAddr = new IPAddr();
            try { string[] parts = ipStr.Split('.'); ipAddr.c1 = byte.Parse(parts[0]); ipAddr.c2 = byte.Parse(parts[1]); ipAddr.c3 = byte.Parse(parts[2]); ipAddr.c4 = byte.Parse(parts[3]); }
            catch { ipAddr.c1 = 192; ipAddr.c2 = 168; ipAddr.c3 = 0; ipAddr.c4 = 10; }
            return ipAddr;
        }
        private void Log(string msg) => OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }
}