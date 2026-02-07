using System;

namespace ConfocalMeter.Presentation.Views
{
    /// <summary>
    /// 主窗体视图接口 - 定义MainForm与MainPresenter之间的通信契约
    /// 实现MVP模式，将UI操作与业务逻辑分离
    /// </summary>
    public interface IMainView
    {
        #region 属性 - UI状态

        /// <summary>
        /// 扫描时长（毫秒）
        /// </summary>
        int ScanDuration { get; }

        /// <summary>
        /// 传感器IP地址
        /// </summary>
        string SensorIp { get; }

        /// <summary>
        /// MCU串口名
        /// </summary>
        string McuPortName { get; }

        /// <summary>
        /// MCU波特率
        /// </summary>
        int McuBaudRate { get; }

        #endregion

        #region 方法 - UI更新

        /// <summary>
        /// 更新连接状态显示
        /// </summary>
        void UpdateConnectionStatus(bool connected);

        /// <summary>
        /// 更新扫描状态显示
        /// </summary>
        void UpdateScanStatus(string status);

        /// <summary>
        /// 启用/禁用扫描按钮
        /// </summary>
        void SetScanButtonEnabled(bool enabled);

        /// <summary>
        /// 更新扫描进度
        /// </summary>
        void UpdateProgress(int current, int total);

        /// <summary>
        /// 更新粗糙度结果显示（完整 7 参数）
        /// </summary>
        void UpdateRoughnessResults(double ra, double rz, double mr1, double mr2, double rpk, double rvk, double rk);

        /// <summary>
        /// 更新曲线图表（原始、去趋势、粗糙度）
        /// </summary>
        void UpdateProfileCharts(double[] original, double[] detrended, double[] roughness);

        /// <summary>
        /// 更新 Abbott-Firestone 曲线图表
        /// </summary>
        void UpdateAbbottFirestoneChart(double[] tp, double[] height, double coreA, double coreB, double coreStart, double coreEnd);

        /// <summary>
        /// 更新图表数据（兼容旧接口）
        /// </summary>
        void UpdateChart(double[] rawData, double[] filteredData);

        /// <summary>
        /// 添加日志
        /// </summary>
        void AppendLog(string message);

        /// <summary>
        /// 更新MCU连接状态
        /// </summary>
        void UpdateMcuConnectionStatus(bool connected);

        /// <summary>
        /// 显示错误消息
        /// </summary>
        void ShowError(string message);

        /// <summary>
        /// 显示信息消息
        /// </summary>
        void ShowInfo(string message);

        #endregion

        #region 事件 - 用户操作

        /// <summary>
        /// 请求连接传感器
        /// </summary>
        event Action OnConnectRequested;

        /// <summary>
        /// 请求断开传感器
        /// </summary>
        event Action OnDisconnectRequested;

        /// <summary>
        /// 请求开始扫描
        /// </summary>
        event Action OnStartScanRequested;

        /// <summary>
        /// 请求打开MCU串口
        /// </summary>
        event Action OnMcuOpenRequested;

        /// <summary>
        /// 请求关闭MCU串口
        /// </summary>
        event Action OnMcuCloseRequested;

        /// <summary>
        /// 请求打开粗糙度设置
        /// </summary>
        event Action OnRoughnessSettingsRequested;

        #endregion
    }
}
