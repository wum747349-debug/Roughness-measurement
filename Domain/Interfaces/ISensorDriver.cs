using System;

namespace ConfocalMeter.Domain.Interfaces
{
    /// <summary>
    /// 传感器驱动接口 - 定义与共聚焦传感器通信的标准方法
    /// </summary>
    public interface ISensorDriver
    {
        #region 属性
        
        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }
        
        #endregion
        
        #region 连接管理
        
        /// <summary>
        /// 连接到传感器设备
        /// </summary>
        /// <param name="ipAddress">设备IP地址</param>
        /// <returns>连接成功返回true</returns>
        bool Connect(string ipAddress);
        
        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();
        
        #endregion
        
        #region 设备状态
        
        /// <summary>
        /// 检查设备是否正在采集
        /// </summary>
        bool IsDeviceAcquiring();
        
        /// <summary>
        /// 强制停止测量
        /// </summary>
        void ForceStopMeasurement();
        
        /// <summary>
        /// 获取当前采样间隔
        /// </summary>
        int GetCurrentSamplingInterval();
        
        #endregion
        
        #region 测量设置
        
        /// <summary>
        /// 设置距离测量模式
        /// </summary>
        bool SetupForDistanceMeasurement();
        
        /// <summary>
        /// 执行暗校准
        /// </summary>
        bool PerformDarkCalibration();
        
        /// <summary>
        /// 设置数据传输
        /// </summary>
        void SetDataTransfer(uint holdCount, int timeout);
        
        /// <summary>
        /// 设置采样间隔
        /// </summary>
        void SetSamplingInterval(int interval);
        
        /// <summary>
        /// 设置保持点数
        /// </summary>
        void SetHoldPoints(uint points);
        
        #endregion
        
        #region 数据读取
        
        /// <summary>
        /// 读取数据缓冲区
        /// </summary>
        double[] ReadBuffer(uint count);
        
        /// <summary>
        /// 获取原始图像数据
        /// </summary>
        ushort[] GetRawImageDebug(int sensorIndex, int windowWidth, int windowHeight);
        
        /// <summary>
        /// 获取量程像素限制
        /// </summary>
        (double min, double max) GetRangePixelLimits(int sensorIndex);
        
        /// <summary>
        /// 检查数据状态
        /// </summary>
        uint CheckDataStatus();
        
        #endregion
        
        #region 事件
        
        /// <summary>
        /// 日志事件
        /// </summary>
        event Action<string> OnLog;
        
        #endregion
    }
}
