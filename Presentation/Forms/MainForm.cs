using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Diagnostics;

// 引用 SDK 命名空间
using tscmccs;

// 引用粗糙度算法相关命名空间
using ConfocalMeter.Models;
using ConfocalMeter.Domain;
using ConfocalMeter.Interfaces;
using ConfocalMeter.Application;
using ConfocalMeter.Infrastructure.Data;

namespace ConfocalMeter
{
    public partial class MainForm : Form
    {
        // 全局单例传感器管理器
        public static SensorManager Sensor = new SensorManager();

        // 子窗口引用
        private RawImageForm _rawForm = null;
        private MeasurementForm _measureForm = null;
        private IOControlForm _ioForm = null;

        // --- 粗糙度计算服务 ---
        private DetrendAppService _roughService;
        private DetrendOptions _roughOptions;
        
        // --- 数据存储服务 ---
        private MeasurementDataService _dataService;

        // --- UI 控件定义 ---

        // 左侧控制区
        private GroupBox grpComm, grpStatus, grpFunctions;
        private TextBox txtIP, txtRemotePort, txtLocalPort;
        private Button btnConnect, btnOpenRawImage, btnOpenMeasurement, btnOpenIOControl;
        private Label lblConnectStatus, lblDataStatus;
        private ListBox listBoxLog;
        private System.Windows.Forms.Timer timerStatus;

        // MCU串口控制区
        private GroupBox grpMcuSerial;
        private ComboBox cmbMcuPort, cmbMcuBaud;
        private Button btnMcuOpenClose;
        private Label lblMcuStatus;

        // 右侧测量与结果区
        private GroupBox grpRoughnessConfig, grpResults;
        private NumericUpDown numEvalLength, numMotorSpeed;
        private ComboBox cmbRoughnessSampling;
        private Button btnStartScan, btnRoughSettings, btnOpenHistory;
        private ProgressBar progressBarScan;
        private Label lblScanStatus;

        // 图表
        private Chart chartProfile; // 轮廓图
        private Chart chartAF;      // Abbott-Firestone 曲线图
        
        // 曲线显示选择复选框
        private CheckBox chkShowRaw, chkShowDetrend, chkShowRough;

        // 结果数值标签 (7参数)
        private Label lblRa, lblRz, lblRk, lblMr1, lblMr2, lblRpk, lblRvk;

        // --- 逻辑变量 ---
        private bool _isScanning = false;
        private Thread _scanThread;
        private List<double> _rawProfileData = new List<double>(); // 原始采集数据

        public MainForm()
        {
            // 1. 初始化 UI 布局 (最先执行，创建 ListBox 等控件)
            InitializeCustomUI();

            // 2. 初始化算法服务
            InitRoughnessService();

            // 3. 初始化数据存储服务
            InitDataService();

            // 4. 绑定逻辑事件
            BindLogic();
        }

        // ==========================================
        // 1. 初始化算法服务
        // ==========================================
        private void InitRoughnessService()
        {
            try
            {
                // 依赖注入组装 (Dependency Injection)
                var afCalc = new AbbottFirestoneCalculator();
                var roughCalc = new RoughnessCalculator(afCalc);
                var detrender = new RobustLoessDetrender();

                _roughService = new DetrendAppService(detrender, roughCalc);
                
                // 设置日志回调，将滤波过程提示输出到日志区
                _roughService.LogCallback = msg => AppendLog($"[算法] {msg}");

                // 默认参数配置
                _roughOptions = new DetrendOptions
                {
                    LambdaC = 0.8,                         // 默认截止波长 0.8mm
                    Mode = RoughnessMode.HighpassByLambdaC, // 默认高斯高通
                    UseRobustLoess = true,                 // 启用去趋势
                    WeakLoess = true,                      // 弱化 LOESS (0.7×λc)
                    AutoRelaxLambda = true,                // PSD 失能量时自动放宽 λc
                    SelfCheckDropPct = 50.0                // PSD 自检阈值
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"算法服务初始化失败: {ex.Message}");
            }
        }

        // ==========================================
        // 1.5 初始化数据存储服务
        // ==========================================
        private void InitDataService()
        {
            try
            {
                // 数据库文件路径 (V2上位机根目录)
                string dbPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "Roughnessdata.db"
                );
                
                _dataService = new MeasurementDataService(dbPath);
                _dataService.Initialize(); // 创建表和索引
                
                AppendLog($"数据库已初始化: {dbPath}");
            }
            catch (Exception ex)
            {
                AppendLog($"数据库初始化失败: {ex.Message}");
            }
        }

        // ==========================================
        // 2. 纯代码 UI 布局 (无需设计器)
        // ==========================================
        private void InitializeCustomUI()
        {
            this.Text = "光谱共焦粗糙度测量系统 (Integrated)";
            this.Size = new Size(1350, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            int leftX = 20;
            int rightX = 320;

            // --- 左侧：通信与调试 ---

            // 1. 通信设置
            grpComm = new GroupBox() { Text = "通信设置", Location = new Point(leftX, 20), Size = new Size(280, 160) };
            AddLabel(grpComm, "IP:", 20, 30);
            txtIP = AddTextBox(grpComm, "192.168.0.10", 80, 27);
            AddLabel(grpComm, "Dev Port:", 20, 65);
            txtRemotePort = AddTextBox(grpComm, "8000", 80, 62);
            AddLabel(grpComm, "PC Port:", 20, 100);
            txtLocalPort = AddTextBox(grpComm, "8001", 80, 97);
            btnConnect = new Button() { Text = "连接设备", Location = new Point(80, 130), Width = 100, Height = 28, BackColor = Color.LightGray };
            btnConnect.Click += BtnConnect_Click;
            grpComm.Controls.Add(btnConnect);

            // 2. 系统状态
            grpStatus = new GroupBox() { Text = "系统状态", Location = new Point(leftX, 190), Size = new Size(280, 110) };
            AddLabel(grpStatus, "连接:", 20, 30);
            lblConnectStatus = CreateStatusLabel("未连接", Color.Gray, 25, grpStatus);
            AddLabel(grpStatus, "数据:", 20, 70);
            lblDataStatus = CreateStatusLabel("停止", Color.Gray, 65, grpStatus);

            // 3. 调试工具
            grpFunctions = new GroupBox() { Text = "调试工具", Location = new Point(leftX, 310), Size = new Size(280, 110) };
            btnOpenRawImage = new Button() { Text = "原始图像", Location = new Point(20, 25), Width = 110, Height = 35 };
            btnOpenRawImage.Click += BtnOpenRawImage_Click;
            btnOpenMeasurement = new Button() { Text = "实时示波器", Location = new Point(140, 25), Width = 110, Height = 35 };
            btnOpenMeasurement.Click += BtnOpenMeasurement_Click;
            btnOpenIOControl = new Button() { Text = "IO控制", Location = new Point(20, 65), Width = 110, Height = 35, BackColor = System.Drawing.Color.LightBlue };
            btnOpenIOControl.Click += BtnOpenIOControl_Click;
            btnOpenHistory = new Button() { Text = "历史记录", Location = new Point(140, 65), Width = 110, Height = 35, BackColor = System.Drawing.Color.LightGreen };
            btnOpenHistory.Click += BtnOpenHistory_Click;
            grpFunctions.Controls.Add(btnOpenRawImage);
            grpFunctions.Controls.Add(btnOpenMeasurement);
            grpFunctions.Controls.Add(btnOpenIOControl);
            grpFunctions.Controls.Add(btnOpenHistory);

            // 4. MCU串口设置区 (用于IO控制与STM32通信)
            grpMcuSerial = new GroupBox() { Text = "MCU串口 (IO控制)", Location = new Point(leftX, 430), Size = new Size(280, 110) };
            AddLabel(grpMcuSerial, "端口:", 15, 30);
            cmbMcuPort = new ComboBox() { Location = new Point(55, 27), Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
            // 预设COM1-COM10，默认COM3
            for (int i = 1; i <= 10; i++) cmbMcuPort.Items.Add($"COM{i}");
            cmbMcuPort.SelectedIndex = 2; // COM3
            grpMcuSerial.Controls.Add(cmbMcuPort);

            AddLabel(grpMcuSerial, "波特率:", 140, 30);
            cmbMcuBaud = new ComboBox() { Location = new Point(195, 27), Width = 75, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMcuBaud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
            cmbMcuBaud.SelectedIndex = 4; // 115200
            grpMcuSerial.Controls.Add(cmbMcuBaud);

            btnMcuOpenClose = new Button() { Text = "打开串口", Location = new Point(15, 65), Width = 100, Height = 35, BackColor = Color.LightGray };
            btnMcuOpenClose.Click += BtnMcuOpenClose_Click;
            grpMcuSerial.Controls.Add(btnMcuOpenClose);

            lblMcuStatus = new Label() { Text = "未连接", Location = new Point(125, 72), AutoSize = true, ForeColor = Color.Gray, Font = new Font("微软雅黑", 9F, FontStyle.Bold) };
            grpMcuSerial.Controls.Add(lblMcuStatus);

            // 5. 日志（支持横向滚动）
            Label lblLog = new Label() { Text = "运行日志:", Location = new Point(leftX, 550), AutoSize = true };
            listBoxLog = new ListBox() 
            { 
                Location = new Point(leftX, 570), 
                Width = 280, 
                Height = 300,
                HorizontalScrollbar = true  // 启用横向滚动条
            };

            // --- 右侧：测量与结果 ---

            // 5. 测量参数设置
            grpRoughnessConfig = new GroupBox() { Text = "扫描参数与控制", Location = new Point(rightX, 20), Size = new Size(1000, 80) };

            AddLabel(grpRoughnessConfig, "评定长度(mm):", 20, 30);
            numEvalLength = new NumericUpDown() { Location = new Point(110, 27), Width = 70, DecimalPlaces = 1, Value = 4.0M, Maximum = 100 };
            grpRoughnessConfig.Controls.Add(numEvalLength);

            AddLabel(grpRoughnessConfig, "速度(mm/min):", 190, 30);
            numMotorSpeed = new NumericUpDown() { Location = new Point(290, 27), Width = 70, DecimalPlaces = 1, Value = 60.0M, Maximum = 10000 };
            grpRoughnessConfig.Controls.Add(numMotorSpeed);

            AddLabel(grpRoughnessConfig, "采样间隔:", 370, 30);
            cmbRoughnessSampling = new ComboBox() { Location = new Point(440, 27), Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRoughnessSampling.Items.AddRange(new string[] { "250us", "500us", "1ms", "2ms", "5ms", "10ms", "100us", "125us", "160us", "200us" });
            cmbRoughnessSampling.SelectedIndex = 2; // 1ms
            grpRoughnessConfig.Controls.Add(cmbRoughnessSampling);

            btnRoughSettings = new Button() { Text = "算法参数设置", Location = new Point(530, 25), Width = 120, Height = 30 };
            btnRoughSettings.Click += BtnRoughSettings_Click;
            grpRoughnessConfig.Controls.Add(btnRoughSettings);

            btnStartScan = new Button() { Text = "开始扫描", Location = new Point(850, 20), Width = 130, Height = 45, BackColor = Color.LightGreen, Font = new Font("微软雅黑", 12, FontStyle.Bold) };
            btnStartScan.Click += BtnStartScan_Click;
            grpRoughnessConfig.Controls.Add(btnStartScan);

            // 6. 轮廓图表 (双Y轴叠加显示)
            chartProfile = new Chart();
            chartProfile.Location = new Point(rightX, 110);
            chartProfile.Size = new Size(1000, 420);
            chartProfile.BorderlineColor = Color.Black; chartProfile.BorderlineDashStyle = ChartDashStyle.Solid;

            // 单一绘图区域，但启用双Y轴
            ChartArea areaProfile = new ChartArea("Main");
            areaProfile.BackColor = Color.White;
            
            // X轴
            areaProfile.AxisX.Title = "位置 X (mm)"; 
            areaProfile.AxisX.LabelStyle.Format = "F2";
            
            // 主Y轴 (左): 显示原始轮廓和去趋势轮廓 (mm)
            areaProfile.AxisY.Title = "轮廓高度 (mm)"; 
            areaProfile.AxisY.LabelStyle.Format = "F3";
            areaProfile.AxisY.IsStartedFromZero = false;
            
            // 副Y轴 (右): 显示粗糙度 (μm)
            areaProfile.AxisY2.Enabled = AxisEnabled.True;
            areaProfile.AxisY2.Title = "粗糙度 (μm)";
            areaProfile.AxisY2.LabelStyle.Format = "F2";
            areaProfile.AxisY2.IsStartedFromZero = false;
            areaProfile.AxisY2.MajorGrid.Enabled = false; // 去掉右轴网格，避免混乱

            areaProfile.Position.Auto = false;
            areaProfile.Position.X = 2; areaProfile.Position.Y = 8; areaProfile.Position.Width = 94; areaProfile.Position.Height = 88;
            chartProfile.ChartAreas.Add(areaProfile);

            // Series 绑定 (全部绑定主轴，通过数值偏移实现分离)
            var sRaw = new Series("原始轮廓") { ChartType = SeriesChartType.FastLine, Color = Color.Blue, BorderWidth = 2, YAxisType = AxisType.Primary };
            var sDetrend = new Series("去趋势轮廓") { ChartType = SeriesChartType.FastLine, Color = Color.Orange, BorderWidth = 2, YAxisType = AxisType.Primary };
            var sRough = new Series("粗糙度") { ChartType = SeriesChartType.FastLine, Color = Color.Green, BorderWidth = 2, YAxisType = AxisType.Primary };

            chartProfile.Series.Add(sRaw);
            chartProfile.Series.Add(sDetrend);
            chartProfile.Series.Add(sRough);

            // 图例 (顶部)
            Legend legend = new Legend("Legend");
            legend.Docking = Docking.Top;
            legend.Alignment = StringAlignment.Center;
            chartProfile.Legends.Add(legend);

            // 复选框添加到顶部配置区 (在“算法参数设置”按钮之后，距离“开始扫描”趋近一些)
            chkShowRaw = new CheckBox() { Text = "原始", Location = new Point(660, 30), AutoSize = true, Checked = true, ForeColor = Color.Blue };
            chkShowDetrend = new CheckBox() { Text = "去趋势", Location = new Point(710, 30), AutoSize = true, Checked = true, ForeColor = Color.Orange };
            chkShowRough = new CheckBox() { Text = "粗糙度", Location = new Point(770, 30), AutoSize = true, Checked = true, ForeColor = Color.Green };
            
            grpRoughnessConfig.Controls.Add(chkShowRaw);
            grpRoughnessConfig.Controls.Add(chkShowDetrend);
            grpRoughnessConfig.Controls.Add(chkShowRough);

            // 复选框事件
            chkShowRaw.CheckedChanged += (s, e) => { chartProfile.Series["原始轮廓"].Enabled = chkShowRaw.Checked; chartProfile.ChartAreas[0].RecalculateAxesScale(); };
            chkShowDetrend.CheckedChanged += (s, e) => { chartProfile.Series["去趋势轮廓"].Enabled = chkShowDetrend.Checked; chartProfile.ChartAreas[0].RecalculateAxesScale(); };
            chkShowRough.CheckedChanged += (s, e) => { chartProfile.Series["粗糙度"].Enabled = chkShowRough.Checked; chartProfile.ChartAreas[0].RecalculateAxesScale(); };

            // 7. Abbott-Firestone 曲线 (左下)
            chartAF = new Chart();
            chartAF.Location = new Point(rightX, 565);  // 下移给进度条腐出空间
            chartAF.Size = new Size(450, 280);
            chartAF.BorderlineColor = Color.Gray; chartAF.BorderlineDashStyle = ChartDashStyle.Solid;

            ChartArea areaAF = new ChartArea("AF");
            areaAF.AxisX.Title = "材料率 Tp (%)"; areaAF.AxisX.Minimum = 0; areaAF.AxisX.Maximum = 100;
            areaAF.AxisY.Title = "深度 (μm)";
            chartAF.ChartAreas.Add(areaAF);

            var sAF = new Series("AF曲线") { ChartType = SeriesChartType.Line, Color = Color.Purple, BorderWidth = 2 };
            var sCoreLine = new Series("核心线") { ChartType = SeriesChartType.Line, Color = Color.Red, BorderWidth = 2, BorderDashStyle = ChartDashStyle.Dash };
            chartAF.Series.Add(sAF);
            chartAF.Series.Add(sCoreLine);

            // 8. 结果数值区 (右下)
            grpResults = new GroupBox() { Text = "评定结果 (ISO 4287 / ISO 13565)", Location = new Point(rightX + 460, 565), Size = new Size(540, 280) };
            grpResults.Font = new Font("微软雅黑", 10);

            lblRa = CreateResLabel("Ra:", 30, 40);
            lblRz = CreateResLabel("Rz:", 30, 80);
            lblRk = CreateResLabel("Rk:", 30, 120);
            lblRpk = CreateResLabel("Rpk:", 30, 160);
            lblRvk = CreateResLabel("Rvk:", 30, 200);
            lblMr1 = CreateResLabel("Mr1:", 280, 40);
            lblMr2 = CreateResLabel("Mr2:", 280, 80);

            grpResults.Controls.AddRange(new Control[] { lblRa, lblRz, lblRk, lblRpk, lblRvk, lblMr1, lblMr2 });

            // 9. 进度条（位于轮廓图下方）
            progressBarScan = new ProgressBar() { Location = new Point(rightX, 535), Size = new Size(700, 18), Style = ProgressBarStyle.Continuous, Visible = false };
            lblScanStatus = new Label() { Text = "准备就绪", Location = new Point(rightX + 710, 535), AutoSize = true, Font = new Font("微软雅黑", 9) };

            // 添加所有控件
            this.Controls.AddRange(new Control[] {
                grpComm, grpStatus, grpFunctions, grpMcuSerial, lblLog, listBoxLog,
                grpRoughnessConfig, chartProfile, chartAF, grpResults, progressBarScan, lblScanStatus
                // 注意：chkShowRaw, chkShowDetrend, chkShowRough 已经添加到 grpRoughnessConfig，不要重复添加
            });
        }

        // --- UI 辅助方法 ---
        private void AddLabel(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label() { Text = text, Location = new Point(x, y + 5), AutoSize = true });
        }
        private TextBox AddTextBox(Control parent, string text, int x, int y)
        {
            TextBox tb = new TextBox() { Text = text, Location = new Point(x, y), Width = 100 };
            parent.Controls.Add(tb);
            return tb;
        }
        private Label CreateStatusLabel(string text, Color bg, int y, Control parent)
        {
            Label lbl = new Label() { Text = text, Location = new Point(100, y), Size = new Size(80, 25), TextAlign = ContentAlignment.MiddleCenter, BackColor = bg, ForeColor = Color.White, Font = new Font("微软雅黑", 9, FontStyle.Bold) };
            parent.Controls.Add(lbl);
            return lbl;
        }
        private Label CreateResLabel(string title, int x, int y)
        {
            Label lbl = new Label() { Text = title + " --", Location = new Point(x, y), AutoSize = true, Font = new Font("Consolas", 14, FontStyle.Bold) };
            return lbl;
        }

        private void BindLogic()
        {
            Sensor.OnLog += AppendLog;
            Sensor.OnConnectionChanged += UpdateUIState;
            timerStatus = new System.Windows.Forms.Timer { Interval = 500 };
            timerStatus.Tick += TimerStatus_Tick;
        }

        // ==========================================
        // 3. 扫描控制逻辑
        // ==========================================
        private void BtnStartScan_Click(object sender, EventArgs e)
        {
            if (!Sensor.IsConnected) { MessageBox.Show("请先连接设备！"); return; }
            if (_isScanning) return;

            // 获取参数
            double lengthMm = (double)numEvalLength.Value;
            double speedMmMin = (double)numMotorSpeed.Value;
            if (speedMmMin <= 0) return;
            double speedMmSec = speedMmMin / 60.0;
            double durationSec = lengthMm / speedMmSec;

            // 下发采样
            SAMPLING_INTERVAL intervalEnum = (SAMPLING_INTERVAL)cmbRoughnessSampling.SelectedIndex;
            Sensor.SetSamplingInterval(intervalEnum);

            // 准备
            _isScanning = true;
            _rawProfileData.Clear();
            chartProfile.Series["原始轮廓"].Points.Clear();
            chartProfile.Series["去趋势轮廓"].Points.Clear();
            chartProfile.Series["粗糙度"].Points.Clear();
            chartAF.Series["AF曲线"].Points.Clear();
            chartAF.Series["核心线"].Points.Clear();

            // UI 更新
            btnStartScan.Enabled = false; btnStartScan.Text = "扫描中..."; btnStartScan.BackColor = Color.LightSalmon;
            progressBarScan.Visible = true; progressBarScan.Value = 0;
            lblScanStatus.Text = $"扫描中... (预计 {durationSec:F1}s)";

            // 启动线程
            _scanThread = new Thread(() => ScanTask(durationSec, speedMmMin));
            _scanThread.IsBackground = true;
            _scanThread.Start();
        }

        private void ScanTask(double durationSeconds, double speedMmMin)
        {
            Sensor.ForceStopMeasurement();
            Sensor.SetDataTransfer(true);

            Stopwatch sw = new Stopwatch();
            sw.Start();

            int bufSize = 4000;
            DataNode[] buffer = new DataNode[bufSize];

            while (sw.Elapsed.TotalSeconds < durationSeconds && _isScanning)
            {
                int count = Sensor.ReadBuffer(buffer, bufSize);
                if (count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        // 过滤通道1和DIST1类型
                        if (buffer[i].cfg.channel == 1 && buffer[i].cfg.type == (int)SENSOR_OUTPUT_DATA.DIST1)
                        {
                            double val = buffer[i].data;
                            // 基础过滤：剔除绝对错误码
                            if (val > -1000 && val < 1000)
                            {
                                _rawProfileData.Add(val);
                            }
                        }
                    }
                }
                else Thread.Sleep(1);

                // 更新进度
                if (sw.ElapsedMilliseconds % 100 < 10)
                {
                    int progress = (int)((sw.ElapsedMilliseconds * 100) / (durationSeconds * 1000));
                    if (progress > 100) progress = 100;
                    this.BeginInvoke((MethodInvoker)delegate {
                        progressBarScan.Value = progress;
                        lblScanStatus.Text = $"采集点数: {_rawProfileData.Count}";
                    });
                }
            }

            double actualTime = sw.Elapsed.TotalSeconds;
            sw.Stop();
            Sensor.SetDataTransfer(false);
            Sensor.ForceStopMeasurement();

            this.BeginInvoke((MethodInvoker)delegate {
                FinishScan(actualTime, speedMmMin);
            });
        }

        // ==========================================
        // 4. 数据处理与结果显示 (核心集成点)
        // ==========================================
        private void FinishScan(double actualTime, double speedMmMin)
        {
            _isScanning = false;
            btnStartScan.Enabled = true; btnStartScan.Text = "开始扫描"; btnStartScan.BackColor = Color.LightGreen;
            progressBarScan.Visible = false;

            if (_rawProfileData.Count < 100)
            {
                MessageBox.Show("采集数据过少，无法计算！", "警告");
                return;
            }

            // 1. 数据清洗 (中值滤波去噪)
            // 先去除极端的“蓝色矩形”噪点
            var sorted = _rawProfileData.OrderBy(x => x).ToList();
            double median = sorted[sorted.Count / 2];
            List<double> validData = new List<double>();
            foreach (var val in _rawProfileData)
            {
                if (Math.Abs(val - median) < 2.0) validData.Add(val);
            }

            if (validData.Count < 100) { MessageBox.Show("数据噪声过大，过滤后无效！"); return; }

            // 2. 准备计算参数
            double[] yData = validData.ToArray();
            // 计算实际物理步长 dx
            double speedMmSec = speedMmMin / 60.0;
            double totalDist = speedMmSec * actualTime;
            double dx = totalDist / yData.Length; // 强制拟合到实际走过的距离

            lblScanStatus.Text = $"完成. 点数:{yData.Length} dx:{dx:F6}mm";
            AppendLog($"开始计算粗糙度... dx={dx:F6}mm");

            // 3. 调用算法服务
            try
            {
                double scale = 1000.0; // mm 转 um
                DetrendResult result = _roughService.ComputeAllFromMemory(yData, dx, _roughOptions, scale);

                if (result == null) { MessageBox.Show("计算返回空结果"); return; }

                // 4. 更新界面
                UpdateResults(result, dx, scale);
                AppendLog("计算完成。");

                // 5. 保存数据到数据库 (异步执行，避免阻塞 UI)
                SaveMeasurementRecord(result, yData, dx, speedMmMin);
            }
            catch (Exception ex)
            {
                AppendLog($"计算异常: {ex.Message}");
                MessageBox.Show($"算法错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存测量记录到 SQLite 数据库
        /// </summary>
        private void SaveMeasurementRecord(DetrendResult result, double[] rawData, double dx, double speed)
        {
            if (_dataService == null) return;

            try
            {
                var record = new MeasurementRecord
                {
                    MeasurementTime = DateTime.Now,
                    EvalLength = (double)numEvalLength.Value,
                    Interval = dx,
                    Speed = speed,
                    Ra = result.Ra,
                    Rz = result.Rz,
                    Mr1 = result.Mr1,
                    Mr2 = result.Mr2,
                    Rpk = result.Rpk,
                    Rvk = result.Rvk,
                    Rk = result.Rk,
                    RawData = rawData
                };

                // 使用线程池异步保存，避免阻塞 UI
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        long id = _dataService.SaveRecord(record);
                        this.BeginInvoke((Action)(() =>
                        {
                            AppendLog($"数据已保存 (ID: {id})");
                        }));
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke((Action)(() =>
                        {
                            AppendLog($"数据保存失败: {ex.Message}");
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"准备保存数据时出错: {ex.Message}");
            }
        }

        private void UpdateResults(DetrendResult res, double dx, double scale)
        {
            // A. 数值结果 (7 参数)
            lblRa.Text = $"Ra: {res.Ra:F3} μm";
            lblRz.Text = $"Rz: {res.Rz:F3} μm";
            lblRk.Text = $"Rk: {res.Rk:F3} μm";
            lblRpk.Text = $"Rpk: {res.Rpk:F3} μm";
            lblRvk.Text = $"Rvk: {res.Rvk:F3} μm";
            lblMr1.Text = $"Mr1: {res.Mr1:F2} %";
            lblMr2.Text = $"Mr2: {res.Mr2:F2} %";

            // B. 轮廓图 (单轴偏移显示)
            var sRaw = chartProfile.Series["原始轮廓"];
            var sDetrend = chartProfile.Series["去趋势轮廓"];
            var sRough = chartProfile.Series["粗糙度"];

            sRaw.Points.Clear();
            sDetrend.Points.Clear();
            sRough.Points.Clear();

            // 计算原始轮廓的平均高度，作为参考基准
            double avgHeight = 0;
            if (res.Height.Length > 0) avgHeight = res.Height.Average();
            
            // 计算去趋势和粗糙度的振幅，用于确定合适的偏移量
            double maxDetrend = 0, maxRough = 0;
            if (res.Residual != null && res.Residual.Length > 0)
                maxDetrend = res.Residual.Max(v => Math.Abs(v));
            if (res.Roughness != null && res.Roughness.Length > 0)
                maxRough = res.Roughness.Max(v => Math.Abs(v));
            
            // 动态偏移量 = 振幅的 1.5 倍 (紧凑且不重叠)
            // 去趋势在原始轮廓上方，粗糙度在去趋势上方
            double gap = Math.Max(maxDetrend, maxRough) * 1.5; // 1.5 倍振幅间隔 - 曲线更紧凑
            if (gap < 0.002) gap = 0.002; // 最小 2μm 间隔
            
            double offsetDetrend = avgHeight + gap;      // 去趋势在原始上方
            double offsetRough = avgHeight + gap * 2;    // 粗糙度在去趋势上方

            for (int i = 0; i < res.Height.Length; i++)
            {
                double x = i * dx;
                double h = res.Height[i];
                double r = (res.Roughness != null && i < res.Roughness.Length) ? res.Roughness[i] : 0;
                double d = (res.Residual != null && i < res.Residual.Length) ? res.Residual[i] : 0;

                // 原始轮廓保持原位
                sRaw.Points.AddXY(x, h);
                
                // 去趋势和粗糙度加上偏移量
                sDetrend.Points.AddXY(x, d + offsetDetrend);
                sRough.Points.AddXY(x, r + offsetRough);
            }

            // 自动调整坐标轴 - 紧密围绕数据范围
            var area = chartProfile.ChartAreas[0];
            area.AxisY.IsStartedFromZero = false;
            area.AxisY2.Enabled = AxisEnabled.False;
            
            // 强制刷新
            area.RecalculateAxesScale();

            // C. AF 曲线 + 核心线
            chartAF.Series["AF曲线"].Points.Clear();
            chartAF.Series["核心线"].Points.Clear();

            if (res.AbbottTp != null && res.AbbottHeight != null)
            {
                for (int i = 0; i < res.AbbottTp.Length; i++)
                {
                    chartAF.Series["AF曲线"].Points.AddXY(res.AbbottTp[i], res.AbbottHeight[i] * scale);
                }

                // 绘制核心线 (从 CoreStart 到 CoreEnd)
                double y1 = (res.CoreA * res.CoreStart + res.CoreB) * scale;
                double y2 = (res.CoreA * res.CoreEnd + res.CoreB) * scale;
                chartAF.Series["核心线"].Points.AddXY(res.CoreStart, y1);
                chartAF.Series["核心线"].Points.AddXY(res.CoreEnd, y2);
            }
            chartAF.ChartAreas[0].RecalculateAxesScale();
        }

        private void BtnRoughSettings_Click(object sender, EventArgs e)
        {
            using (var form = new RoughnessSettingsForm(_roughOptions))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _roughOptions = form.Options;
                    AppendLog($"算法参数更新: λc={_roughOptions.LambdaC}mm");
                }
            }
        }

        // --- 辅助逻辑 (保持不变) ---
        private void TimerStatus_Tick(object sender, EventArgs e)
        {
            if (_isScanning) return;
            bool isAcq = Sensor.IsDeviceAcquiring();
            if (isAcq) { lblDataStatus.Text = "采集中"; lblDataStatus.BackColor = Color.LimeGreen; }
            else { lblDataStatus.Text = "停止"; lblDataStatus.BackColor = Color.Gray; }
        }
        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (!Sensor.IsConnected)
            {
                int.TryParse(txtLocalPort.Text, out int lp); int.TryParse(txtRemotePort.Text, out int rp);
                Sensor.Connect(txtIP.Text, rp, lp); timerStatus.Start();
            }
            else
            {
                Sensor.Disconnect(); timerStatus.Stop(); UpdateUIState(false);
            }
        }
        private void UpdateUIState(bool isConnected)
        {
            if (this.InvokeRequired) { this.Invoke(new Action<bool>(UpdateUIState), isConnected); return; }
            if (isConnected)
            {
                btnConnect.Text = "断开连接"; btnConnect.BackColor = Color.LightGreen;
                lblConnectStatus.BackColor = Color.LimeGreen; lblConnectStatus.Text = "已连接"; txtIP.Enabled = false;
            }
            else
            {
                btnConnect.Text = "连接设备"; btnConnect.BackColor = Color.LightGray;
                lblConnectStatus.BackColor = Color.Red; lblConnectStatus.Text = "未连接"; txtIP.Enabled = true;
            }
        }
        private void AppendLog(string msg)
        {
            if (this.InvokeRequired) { this.Invoke(new Action<string>(AppendLog), msg); return; }
            listBoxLog.Items.Add(msg); listBoxLog.TopIndex = listBoxLog.Items.Count - 1;
        }
        private void BtnOpenRawImage_Click(object sender, EventArgs e)
        {
            if (!Sensor.IsConnected) { MessageBox.Show("请先连接"); return; }
            if (_rawForm == null || _rawForm.IsDisposed) { _rawForm = new RawImageForm(); _rawForm.Show(); } else _rawForm.BringToFront();
        }
        private void BtnOpenMeasurement_Click(object sender, EventArgs e)
        {
            if (!Sensor.IsConnected) { MessageBox.Show("请先连接"); return; }
            if (_measureForm == null || _measureForm.IsDisposed) { _measureForm = new MeasurementForm(); _measureForm.Show(); } else _measureForm.BringToFront();
        }

        /// <summary>
        /// 打开IO控制窗体 - 用于与STM32单片机通信，间接控制CM35电机控制器
        /// </summary>
        private void BtnOpenIOControl_Click(object sender, EventArgs e)
        {
            if (_ioForm == null || _ioForm.IsDisposed)
            {
                _ioForm = new IOControlForm();
                _ioForm.Show();
            }
            else
            {
                _ioForm.BringToFront();
            }
        }

        /// <summary>
        /// 打开历史记录窗体 - 查询和管理测量记录
        /// </summary>
        private void BtnOpenHistory_Click(object sender, EventArgs e)
        {
            if (_dataService == null)
            {
                MessageBox.Show("数据库服务未初始化！", "错误");
                return;
            }

            using (var historyForm = new ConfocalMeter.Presentation.Forms.HistoryForm(_dataService))
            {
                historyForm.ShowDialog(this);
            }
        }

        /// <summary>
        /// MCU串口打开/关闭按钮点击事件
        /// </summary>
        private void BtnMcuOpenClose_Click(object sender, EventArgs e)
        {
            if (McuSerialManager.Instance.IsOpen)
            {
                // 关闭串口
                McuSerialManager.Instance.Close();
                btnMcuOpenClose.Text = "打开串口";
                btnMcuOpenClose.BackColor = Color.LightGray;
                lblMcuStatus.Text = "未连接";
                lblMcuStatus.ForeColor = Color.Gray;
                AppendLog("MCU串口已关闭");
            }
            else
            {
                // 打开串口
                string portName = cmbMcuPort.Text;
                int baudRate = int.Parse(cmbMcuBaud.Text);

                if (McuSerialManager.Instance.Open(portName, baudRate))
                {
                    btnMcuOpenClose.Text = "关闭串口";
                    btnMcuOpenClose.BackColor = Color.LightGreen;
                    lblMcuStatus.Text = "已连接";
                    lblMcuStatus.ForeColor = Color.Green;
                    AppendLog($"MCU串口已打开: {portName}");
                }
                else
                {
                    MessageBox.Show($"无法打开串口 {portName}，请检查端口是否被占用！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}