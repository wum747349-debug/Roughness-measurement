using System;

namespace ConfocalMeter.Domain.Interfaces
{
    /// <summary>
    /// MCU驱动接口 - 定义与STM32/CM35电机控制器通信的标准方法
    /// </summary>
    public interface IMcuDriver
    {
        #region 属性
        
        /// <summary>
        /// 串口是否已打开
        /// </summary>
        bool IsOpen { get; }
        
        /// <summary>
        /// 设备是否已连接（有通信响应）
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// 当前输出状态位图
        /// </summary>
        byte OutputState { get; }
        
        /// <summary>
        /// 当前输入控制位图
        /// </summary>
        byte InputMap { get; }
        
        #endregion
        
        #region 连接管理
        
        /// <summary>
        /// 打开串口连接
        /// </summary>
        /// <param name="portName">串口名称 (如 COM3)</param>
        /// <param name="baudRate">波特率</param>
        /// <returns>成功返回true</returns>
        bool Open(string portName, int baudRate = 115200);
        
        /// <summary>
        /// 关闭串口连接
        /// </summary>
        void Close();
        
        #endregion
        
        #region IO控制
        
        /// <summary>
        /// 设置输入控制位图
        /// </summary>
        /// <param name="inputMap">8位输入控制位图</param>
        void SetInputMap(byte inputMap);
        
        /// <summary>
        /// 设置单个输入位
        /// </summary>
        /// <param name="bitIndex">位索引 (0-7)</param>
        /// <param name="value">位值</param>
        void SetInputBit(int bitIndex, bool value);
        
        /// <summary>
        /// 获取输出位状态
        /// </summary>
        /// <param name="bitIndex">位索引 (0-7)</param>
        /// <returns>位状态</returns>
        bool GetOutputBit(int bitIndex);
        
        #endregion
        
        #region 事件
        
        /// <summary>
        /// 日志事件
        /// </summary>
        event Action<string> OnLog;
        
        /// <summary>
        /// 输出状态变化事件
        /// </summary>
        event Action<byte> OnOutputStateChanged;
        
        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        event Action<bool> OnConnectionChanged;
        
        #endregion
    }
}
