using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections.Concurrent;
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

        // 采集队列（线程间）
        private readonly ConcurrentQueue<DataPointObj> _dataQueue = new ConcurrentQueue<DataPointObj>();

        // 显示缓冲（仅 UI 线程访问）
        private const int DISPLAY_RING_CAPACITY = 120000;
        private readonly DataPointObj[] _displayRing = new DataPointObj[DISPLAY_RING_CAPACITY];
        private int _ringHead = 0;   // 下一个写入位置
        private int _ringCount = 0;  // 当前有效点数

        // 状态变量
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private double _zeroOffset = 0.0;
        private const double VIEW_WINDOW = 10.0; // 滚动窗口 10 秒

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

            chartDist = new Chart();
            chartDist.Dock = DockStyle.Fill;
            ChartArea area = new ChartArea("ScopeArea");
            area.BackColor = Color.Black;

            area.AxisX.Title = "时间 (s)";
            area.AxisX.TitleForeColor = Color.LightGray;
            area.AxisX.LabelStyle.ForeColor = Color.White;
            area.AxisX.LineColor = Color.White;
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(60, 60, 60);
            area.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            area.AxisX.LabelStyle.Format = "F1";

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

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_isMeasuring) return;
            _isMeasuring = true;
            btnStart.Enabled = false;
            btnStop.Enabled = true;

            while (_dataQueue.TryDequeue(out _)) { }
            ClearDisplayRing();
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
            btnStart.Enabled = true;
            btnStop.Enabled = false;
        }

        private void BtnTare_Click(object sender, EventArgs e)
        {
            if (TryGetLatestPoint(out DataPointObj latest))
            {
                double currentAbs = latest.Value + _zeroOffset;
                _zeroOffset = currentAbs;
                ClearDisplayRing();
                chartDist.Series[0].Points.Clear();
                lblCurrentVal.Text = "0.0000 mm";
                lblStats.Text = "P-P(波动): --";
            }
        }

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
                        if (buffer[i].cfg.channel == 1 && buffer[i].cfg.type == (int)SENSOR_OUTPUT_DATA.DIST1)
                        {
                            double val = buffer[i].data;
                            if (val > -1000 && val < 1000)
                            {
                                double t = _stopwatch.Elapsed.TotalSeconds;
                                _dataQueue.Enqueue(new DataPointObj { Time = t, Value = val });
                            }
                        }
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            int limit = 3000;
            int processed = 0;

            while (processed < limit && _dataQueue.TryDequeue(out DataPointObj pt))
            {
                pt.Value -= _zeroOffset;
                AddToDisplayRing(pt);
                processed++;
            }

            if (!TryGetLatestPoint(out DataPointObj latest))
            {
                return;
            }

            RenderWave(latest.Time);
            lblCurrentVal.Text = $"{latest.Value:F4} mm";
        }

        private void RenderWave(double latestTime)
        {
            double windowStart = Math.Max(0, latestTime - VIEW_WINDOW);
            int bucketCount = Math.Max(200, chartDist.ClientSize.Width - 80);

            bool[] hasBucket = new bool[bucketCount];
            double[] minVal = new double[bucketCount];
            double[] maxVal = new double[bucketCount];
            double[] minTime = new double[bucketCount];
            double[] maxTime = new double[bucketCount];

            double visibleMin = double.MaxValue;
            double visibleMax = double.MinValue;
            int visiblePoints = 0;

            int oldest = GetOldestIndex();
            for (int i = 0; i < _ringCount; i++)
            {
                int idx = (oldest + i) % DISPLAY_RING_CAPACITY;
                var pt = _displayRing[idx];

                if (pt.Time < windowStart || pt.Time > latestTime)
                {
                    continue;
                }

                visiblePoints++;
                if (pt.Value < visibleMin) visibleMin = pt.Value;
                if (pt.Value > visibleMax) visibleMax = pt.Value;

                int bucket = (int)((pt.Time - windowStart) / VIEW_WINDOW * (bucketCount - 1));
                if (bucket < 0) bucket = 0;
                if (bucket >= bucketCount) bucket = bucketCount - 1;

                if (!hasBucket[bucket])
                {
                    hasBucket[bucket] = true;
                    minVal[bucket] = maxVal[bucket] = pt.Value;
                    minTime[bucket] = maxTime[bucket] = pt.Time;
                }
                else
                {
                    if (pt.Value < minVal[bucket])
                    {
                        minVal[bucket] = pt.Value;
                        minTime[bucket] = pt.Time;
                    }
                    if (pt.Value > maxVal[bucket])
                    {
                        maxVal[bucket] = pt.Value;
                        maxTime[bucket] = pt.Time;
                    }
                }
            }

            var points = chartDist.Series[0].Points;
            points.SuspendUpdates();
            points.Clear();

            if (visiblePoints > 0)
            {
                for (int b = 0; b < bucketCount; b++)
                {
                    if (!hasBucket[b]) continue;

                    if (minTime[b] <= maxTime[b])
                    {
                        points.AddXY(minTime[b], minVal[b]);
                        if (maxTime[b] != minTime[b] || maxVal[b] != minVal[b])
                            points.AddXY(maxTime[b], maxVal[b]);
                    }
                    else
                    {
                        points.AddXY(maxTime[b], maxVal[b]);
                        if (maxTime[b] != minTime[b] || maxVal[b] != minVal[b])
                            points.AddXY(minTime[b], minVal[b]);
                    }
                }
            }

            points.ResumeUpdates();

            if (latestTime > VIEW_WINDOW)
            {
                chartDist.ChartAreas[0].AxisX.Minimum = latestTime - VIEW_WINDOW;
                chartDist.ChartAreas[0].AxisX.Maximum = latestTime;
            }
            else
            {
                chartDist.ChartAreas[0].AxisX.Minimum = 0;
                chartDist.ChartAreas[0].AxisX.Maximum = VIEW_WINDOW;
            }

            if (chkAutoY.Checked && visiblePoints > 0)
            {
                UpdateYAxisHighSensitivity(visibleMin, visibleMax);
            }
            else if (visiblePoints > 0)
            {
                lblStats.Text = $"P-P(波动): {(visibleMax - visibleMin):F4} mm";
            }
        }

        private void UpdateYAxisHighSensitivity(double min, double max)
        {
            double range = max - min;
            if (range < 0.001) range = 0.001;

            double center = (max + min) / 2.0;
            double span = range * 1.1;

            chartDist.ChartAreas[0].AxisY.Maximum = center + span / 2.0;
            chartDist.ChartAreas[0].AxisY.Minimum = center - span / 2.0;

            lblStats.Text = $"P-P(波动): {(max - min):F4} mm";
        }

        private void AddToDisplayRing(DataPointObj pt)
        {
            _displayRing[_ringHead] = pt;
            _ringHead = (_ringHead + 1) % DISPLAY_RING_CAPACITY;
            if (_ringCount < DISPLAY_RING_CAPACITY)
            {
                _ringCount++;
            }
        }

        private bool TryGetLatestPoint(out DataPointObj pt)
        {
            if (_ringCount == 0)
            {
                pt = default(DataPointObj);
                return false;
            }

            int idx = (_ringHead - 1 + DISPLAY_RING_CAPACITY) % DISPLAY_RING_CAPACITY;
            pt = _displayRing[idx];
            return true;
        }

        private int GetOldestIndex()
        {
            return (_ringHead - _ringCount + DISPLAY_RING_CAPACITY) % DISPLAY_RING_CAPACITY;
        }

        private void ClearDisplayRing()
        {
            _ringHead = 0;
            _ringCount = 0;
        }
    }
}
