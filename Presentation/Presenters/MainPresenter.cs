using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using tscmccs;
using ConfocalMeter.Models;
using ConfocalMeter.Domain;
using ConfocalMeter.Interfaces;
using ConfocalMeter.Application;
using ConfocalMeter.Presentation.Views;
using ConfocalMeter.Domain.Interfaces;

namespace ConfocalMeter.Presentation.Presenters
{
    /// <summary>
    /// 主窗体表示器 - 处理MainForm的业务逻辑
    /// 实现MVP模式，将UI操作与业务逻辑分离
    /// 
    /// NOTE: 这是一个架构示例类，展示如何将MainForm的逻辑分离
    /// 完整迁移需要逐步将MainForm的方法迁移到此处
    /// </summary>
    public class MainPresenter
    {
        #region 私有成员

        private readonly IMainView _view;
        private readonly SensorManager _sensor;
        private readonly IMcuDriver _mcuDriver;
        
        // 粗糙度计算服务 (使用项目现有类型)
        private DetrendAppService _roughService;
        private DetrendOptions _roughOptions;
        
        // 扫描控制
        private bool _isScanning = false;
        private CancellationTokenSource _scanCts;
        private List<double> _rawProfileData = new List<double>();

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建MainPresenter实例
        /// </summary>
        /// <param name="view">视图接口</param>
        /// <param name="sensor">传感器管理器</param>
        /// <param name="mcuDriver">MCU驱动</param>
        public MainPresenter(IMainView view, SensorManager sensor, IMcuDriver mcuDriver)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
            _mcuDriver = mcuDriver ?? throw new ArgumentNullException(nameof(mcuDriver));

            // 初始化算法服务
            InitRoughnessService();

            // 绑定视图事件
            BindViewEvents();

            // 绑定设备事件
            BindDeviceEvents();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化粗糙度计算服务
        /// </summary>
        private void InitRoughnessService()
        {
            try
            {
                // 依赖注入组装 - 使用项目现有类型
                var afCalc = new AbbottFirestoneCalculator();
                var roughCalc = new RoughnessCalculator(afCalc);
                var detrender = new RobustLoessDetrender();

                _roughService = new DetrendAppService(detrender, roughCalc);
                
                // 设置日志回调，将滤波过程提示输出到UI日志区
                _roughService.LogCallback = msg => _view.AppendLog($"[算法] {msg}");

                // 默认参数配置
                _roughOptions = new DetrendOptions
                {
                    LambdaC = 0.8,
                    Mode = RoughnessMode.HighpassByLambdaC,
                    UseRobustLoess = true,
                    WeakLoess = true,
                    AutoRelaxLambda = true,
                    SelfCheckDropPct = 50.0
                };

                _view.AppendLog("粗糙度服务初始化完成");
            }
            catch (Exception ex)
            {
                _view.ShowError($"算法服务初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 绑定视图事件
        /// </summary>
        private void BindViewEvents()
        {
            _view.OnConnectRequested += HandleConnect;
            _view.OnDisconnectRequested += HandleDisconnect;
            _view.OnStartScanRequested += HandleStartScan;
            _view.OnMcuOpenRequested += HandleMcuOpen;
            _view.OnMcuCloseRequested += HandleMcuClose;
            _view.OnRoughnessSettingsRequested += HandleRoughnessSettings;
        }

        /// <summary>
        /// 绑定设备事件
        /// </summary>
        private void BindDeviceEvents()
        {
            _sensor.OnLog += msg => _view.AppendLog(msg);
            _sensor.OnConnectionChanged += connected => _view.UpdateConnectionStatus(connected);

            _mcuDriver.OnLog += msg => _view.AppendLog($"[MCU] {msg}");
            _mcuDriver.OnConnectionChanged += connected => _view.UpdateMcuConnectionStatus(connected);
        }

        #endregion

        #region 事件处理 - 传感器连接

        /// <summary>
        /// 处理连接请求
        /// </summary>
        private void HandleConnect()
        {
            string ip = _view.SensorIp;
            _view.AppendLog($"正在连接传感器: {ip}");

            bool success = _sensor.Connect(ip, 50000, 50001);
            if (success)
            {
                _view.UpdateConnectionStatus(true);
                _view.SetScanButtonEnabled(true);
                _view.AppendLog("传感器连接成功");
            }
            else
            {
                _view.ShowError("连接传感器失败，请检查IP地址和网络");
            }
        }

        /// <summary>
        /// 处理断开连接请求
        /// </summary>
        private void HandleDisconnect()
        {
            _sensor.Disconnect();
            _view.UpdateConnectionStatus(false);
            _view.SetScanButtonEnabled(false);
            _view.AppendLog("已断开传感器连接");
        }

        #endregion

        #region 事件处理 - 扫描

        /// <summary>
        /// 处理开始扫描请求
        /// </summary>
        private void HandleStartScan()
        {
            if (_isScanning) return;
            if (!_sensor.IsConnected)
            {
                _view.ShowError("请先连接传感器");
                return;
            }

            _isScanning = true;
            _scanCts = new CancellationTokenSource();
            _rawProfileData.Clear();
            _view.SetScanButtonEnabled(false);
            _view.UpdateScanStatus("扫描中...");

            // NOTE: 实际扫描逻辑较复杂，涉及线程和SDK调用
            // 这里仅作为架构示例，完整实现需要将MainForm.ScanTask迁移过来
            _view.AppendLog("扫描开始 (架构示例)");
        }

        #endregion

        #region 事件处理 - MCU串口

        /// <summary>
        /// 处理MCU串口打开请求
        /// </summary>
        private void HandleMcuOpen()
        {
            string portName = _view.McuPortName;
            int baudRate = _view.McuBaudRate;

            if (_mcuDriver.Open(portName, baudRate))
            {
                _view.UpdateMcuConnectionStatus(true);
                _view.AppendLog($"MCU串口已打开: {portName}");
            }
            else
            {
                _view.ShowError($"无法打开MCU串口 {portName}，请检查端口是否被占用");
            }
        }

        /// <summary>
        /// 处理MCU串口关闭请求
        /// </summary>
        private void HandleMcuClose()
        {
            _mcuDriver.Close();
            _view.UpdateMcuConnectionStatus(false);
            _view.AppendLog("MCU串口已关闭");
        }

        #endregion

        #region 事件处理 - 设置

        /// <summary>
        /// 处理粗糙度设置请求
        /// </summary>
        private void HandleRoughnessSettings()
        {
            // 由View层处理窗体显示，Presenter只处理参数逻辑
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 更新粗糙度参数
        /// </summary>
        public void UpdateRoughnessOptions(DetrendOptions newOptions)
        {
            _roughOptions = newOptions ?? _roughOptions;
            _view.AppendLog($"粗糙度参数已更新: λc={_roughOptions.LambdaC}mm");
        }

        /// <summary>
        /// 获取当前粗糙度参数
        /// </summary>
        public DetrendOptions GetRoughnessOptions() => _roughOptions;

        /// <summary>
        /// 取消当前扫描
        /// </summary>
        public void CancelScan()
        {
            _scanCts?.Cancel();
            _isScanning = false;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            CancelScan();
            _sensor?.Disconnect();
            _mcuDriver?.Close();
        }

        /// <summary>
        /// 执行完整粗糙度计算并更新视图
        /// </summary>
        /// <param name="profileData">原始高度数据 (mm)</param>
        /// <param name="dx">采样间距 (mm) = 移动速度 * 采样间隔</param>
        /// <param name="scale">输出单位缩放 (默认 1000 转 μm)</param>
        public void ComputeRoughness(double[] profileData, double dx, double scale = 1000.0)
        {
            if (profileData == null || profileData.Length < 10)
            {
                _view.ShowError("数据点数不足，无法计算粗糙度");
                return;
            }

            try
            {
                _view.AppendLog($"开始粗糙度计算: {profileData.Length}点, dx={dx:F6}mm");

                var result = _roughService.ComputeAllFromMemory(profileData, dx, _roughOptions, scale);

                if (result == null)
                {
                    _view.ShowError("粗糙度计算失败");
                    return;
                }

                // 更新参数显示
                _view.UpdateRoughnessResults(
                    result.Ra, result.Rz,
                    result.Mr1, result.Mr2,
                    result.Rpk, result.Rvk, result.Rk);

                // 更新三条曲线图
                _view.UpdateProfileCharts(result.Height, result.Residual, result.Roughness);

                // 更新 AF 曲线
                _view.UpdateAbbottFirestoneChart(
                    result.AbbottTp, result.AbbottHeight,
                    result.CoreA, result.CoreB,
                    result.CoreStart, result.CoreEnd);

                _view.AppendLog($"计算完成: Ra={result.Ra:F4}μm, Rz={result.Rz:F4}μm");
            }
            catch (Exception ex)
            {
                _view.ShowError($"粗糙度计算异常: {ex.Message}");
                _view.AppendLog($"[错误] {ex.Message}");
            }
        }

        #endregion
    }
}
