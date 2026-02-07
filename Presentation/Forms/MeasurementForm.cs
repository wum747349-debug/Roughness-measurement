using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using tscmccs;

namespace ConfocalMeter
{
    public partial class MeasurementForm : Form
    {
        // UI 控件
        private Chart chartDist;
        private Button btnStart, btnStop, btnTare;
        private CheckBox chkAutoY;
        private Label lblCurrentVal, lblStats;

        // 逻辑变量
        private bool _isMeasuring = false;
        private Thread _daqThread;
        private System.Windows.Forms.Timer _uiTimer;

        // 数据队列
        private ConcurrentQueue<DataPointObj> _dataQueue = new ConcurrentQueue<DataPointObj>();
        private List<DataPointObj> _displayBuffer = new List<DataPointObj>(); // 本地缓存用于绘图

        // 状态变量
        private Stopwatch _stopwatch = new Stopwatch();
        private double _zeroOffset = 0.0;
        private const double VIEW_WINDOW = 10.0; // 滚动窗口 10秒

        private struct DataPointObj
        {
            public double Time;
            public double Value;
        }

        public MeasurementForm()
        {
            InitializeCustomUI();
            _uiTimer = new System.Windows.Forms.Timer { Interval = 40 }; // 25fps
            _uiTimer.Tick += UiTimer_Tick;
        }

        private void InitializeCustomUI()
        {
            this.Text = "实时数据采集 (微米级示波器)";
            this.Size = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            Panel topPanel = new Panel() { Dock = DockStyle.Top, Height = 80, BackColor = Color.FromArgb(240, 240, 240) };

            btnStart = CreateButton("开始测量", 20, 20, Color.LimeGreen);
            btnStart.Click += BtnStart_Click;

            btnStop = CreateButton("停止", 130, 20, Color.LightSalmon);
            btnStop.Enabled = false;
            btnStop.Click += BtnStop_Click;

            btnTare = CreateButton("归零 (Tare)", 240, 20, Color.LightBlue);
            btnTare.Click += BtnTare_Click;

            chkAutoY = new CheckBox() { Text = "Y轴自动放大", Location = new Point(360, 30), AutoSize = true, Checked = true, Font = new Font("微软雅黑", 10) };

            lblCurrentVal = new Label() { Text = "0.0000 mm", Location = new Point(500, 20), AutoSize = true, Font = new Font("Consolas", 20, FontStyle.Bold), ForeColor = Color.Blue };
            lblStats = new Label() { Text = "波动: --", Location = new Point(780, 30), AutoSize = true, Font = new Font("Consolas", 12), ForeColor = Color.Gray };

            topPanel.Controls.AddRange(new Control[] { btnStart, btnStop, btnTare, chkAutoY, lblCurrentVal, lblStats });

            // --- 图表配置 ---
            chartDist = new Chart();
            chartDist.Dock = DockStyle.Fill;
            ChartArea area = new ChartArea("ScopeArea");
            area.BackColor = Color.Black; // 深色背景

            // --- X轴设置 ---
            area.AxisX.Title = "时间 (s)";
            area.AxisX.TitleForeColor = Color.LightGray;
            area.AxisX.LabelStyle.ForeColor = Color.White;
            area.AxisX.LineColor = Color.White;
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(60, 60, 60);
            area.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            area.AxisX.LabelStyle.Format = "F1";

            // --- Y轴设置 ---
            area.AxisY.Title = "距离 (mm)";
            area.AxisY.TitleForeColor = Color.Cyan;
            area.AxisY.LabelStyle.ForeColor = Color.Cyan;
            area.AxisY.LineColor = Color.White;
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(60, 60, 60);
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            area.AxisY.LabelStyle.Format = "F4";
            area.AxisY.IsStartedFromZero = false;

            chartDist.ChartAreas.Add(area);

            Series series = new Series("Wave");
            series.ChartType = SeriesChartType.FastLine;
            series.Color = Color.Cyan;
            series.BorderWidth = 1;
            chartDist.Series.Add(series);

            this.Controls.Add(chartDist);
            this.Controls.Add(topPanel);

            this.FormClosing += (s, e) => BtnStop_Click(null, null);
        }

        private Button CreateButton(string text, int x, int y, Color bg)
        {
            return new Button() { Text = text, Location = new Point(x, y), Width = 100, Height = 40, BackColor = bg, FlatStyle = FlatStyle.Flat, Font = new Font("微软雅黑", 9, FontStyle.Bold) };
        }

        // --- 逻辑控制 ---

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_isMeasuring) return;
            _isMeasuring = true;
            btnStart.Enabled = false; btnStop.Enabled = true;

            while (_dataQueue.TryDequeue(out _)) ;
            _displayBuffer.Clear();
            chartDist.Series[0].Points.Clear();

            _zeroOffset = 0;
            _stopwatch.Restart();

            MainForm.Sensor.SetDataTransfer(true);

            _daqThread = new Thread(DaqLoop) { IsBackground = true };
            _daqThread.Start();
            _uiTimer.Start();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (!_isMeasuring) return;
            _isMeasuring = false;
            _uiTimer.Stop();
            _stopwatch.Stop();
            MainForm.Sensor.SetDataTransfer(false);
            btnStart.Enabled = true; btnStop.Enabled = false;
        }

        private void BtnTare_Click(object sender, EventArgs e)
        {
            if (_displayBuffer.Count > 0)
            {
                double currentAbs = _displayBuffer.Last().Value + _zeroOffset;
                _zeroOffset = currentAbs;
                _displayBuffer.Clear();
                chartDist.Series[0].Points.Clear();
            }
        }

        // --- 采集线程 (关键修复：加入通道过滤) ---
        private void DaqLoop()
        {
            int bufSize = 1000;
            DataNode[] buffer = new DataNode[bufSize];

            while (_isMeasuring)
            {
                int count = MainForm.Sensor.ReadBuffer(buffer, bufSize);
                if (count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        // ========================================================
                        // 【核心修复】过滤 Channel 0 (时间戳)
                        // 只保留 Channel 1 (DIST1)
                        // ========================================================
                        if (buffer[i].cfg.channel == 1 && buffer[i].cfg.type == (int)SENSOR_OUTPUT_DATA.DIST1)
                        {
                            double val = buffer[i].data;

                            // 过滤无效值
                            if (val > -1000 && val < 1000)
                            {
                                double t = _stopwatch.Elapsed.TotalSeconds;
                                _dataQueue.Enqueue(new DataPointObj { Time = t, Value = val });
                            }
                        }
                    }
                }
                else Thread.Sleep(1);
            }
        }

        // --- UI 刷新 ---
        private void UiTimer_Tick(object sender, EventArgs e)
        {
            int count = _dataQueue.Count;
            if (count == 0) return;

            int limit = 3000;
            int processed = 0;

            while (processed < limit && _dataQueue.TryDequeue(out DataPointObj pt))
            {
                pt.Value -= _zeroOffset;
                _displayBuffer.Add(pt);
                processed++;
            }

            // 1. 移除过期数据 (实现向左滚动)
            if (_displayBuffer.Count > 0)
            {
                double lastTime = _displayBuffer.Last().Time;

                int removeCount = 0;
                while (removeCount < 500 && _displayBuffer.Count > 0 && _displayBuffer[0].Time < (lastTime - VIEW_WINDOW))
                {
                    _displayBuffer.RemoveAt(0);
                    removeCount++;
                }

                // 2. 绘制
                chartDist.Series[0].Points.SuspendUpdates();
                chartDist.Series[0].Points.Clear();
                for (int i = 0; i < _displayBuffer.Count; i++)
                {
                    chartDist.Series[0].Points.AddXY(_displayBuffer[i].Time, _displayBuffer[i].Value);
                }
                chartDist.Series[0].Points.ResumeUpdates();

                // 3. 更新数值
                lblCurrentVal.Text = $"{_displayBuffer.Last().Value:F4} mm";

                // 4. X轴滚动
                if (lastTime > VIEW_WINDOW)
                {
                    chartDist.ChartAreas[0].AxisX.Minimum = lastTime - VIEW_WINDOW;
                    chartDist.ChartAreas[0].AxisX.Maximum = lastTime;
                }
                else
                {
                    chartDist.ChartAreas[0].AxisX.Minimum = 0;
                    chartDist.ChartAreas[0].AxisX.Maximum = VIEW_WINDOW;
                }

                // 5. Y轴自适应
                if (chkAutoY.Checked)
                {
                    UpdateYAxisHighSensitivity(_displayBuffer);
                }
            }
        }

        private void UpdateYAxisHighSensitivity(List<DataPointObj> data)
        {
            if (data.Count == 0) return;
            double max = double.MinValue;
            double min = double.MaxValue;

            foreach (var pt in data)
            {
                if (pt.Value > max) max = pt.Value;
                if (pt.Value < min) min = pt.Value;
            }

            double range = max - min;
            // 最小显示 1微米，防止完全直线时显示异常
            if (range < 0.001) range = 0.001;

            double center = (max + min) / 2.0;
            double span = range * 1.1;

            chartDist.ChartAreas[0].AxisY.Maximum = center + span / 2.0;
            chartDist.ChartAreas[0].AxisY.Minimum = center - span / 2.0;

            lblStats.Text = $"P-P(波动): {(max - min):F4} mm";
        }
    }
}