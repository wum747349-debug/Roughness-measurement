using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using tscmccs;

namespace ConfocalMeter
{
    public partial class RawImageForm : Form
    {
        private Chart chartRaw;
        private Panel pnlControls;
        private Button btnToggleRefresh, btnDarkCalib;
        private Label lblPeakValue, lblPeakPos, lblStatus, lblRangeInfo;

        private bool _isRefreshing = false;
        private Thread _refreshThread;
        private int _rangeStart = 20, _rangeEnd = 1000;

        public RawImageForm()
        {
            InitializeCustomUI();
            this.Shown += RawImageForm_Shown;
        }

        private void RawImageForm_Shown(object sender, EventArgs e)
        {
            if (!MainForm.Sensor.IsConnected) return;

            lblStatus.Text = "状态: 初始化...";
            System.Windows.Forms.Application.DoEvents();

            // 1. 停止采集
            MainForm.Sensor.ForceStopMeasurement();

            // 2. 配置参数
            MainForm.Sensor.SetupForDistanceMeasurement();

            // 3. 尝试获取量程 (失败也没关系，用默认值)
            bool ok = MainForm.Sensor.GetRangePixelLimits(out _rangeStart, out _rangeEnd);

            if (ok)
            {
                lblRangeInfo.Text = $"有效量程: {_rangeStart} ~ {_rangeEnd}";
                lblRangeInfo.ForeColor = Color.DarkGreen;
            }
            else
            {
                lblRangeInfo.Text = $"有效量程: 默认 ({_rangeStart}-{_rangeEnd})";
                lblRangeInfo.ForeColor = Color.Orange; // 橙色警告，但不报错
            }

            // 4. 绘制背景
            DrawChartBackground();

            lblStatus.Text = "状态: 就绪";
        }

        private void DrawChartBackground()
        {
            ChartArea area = chartRaw.ChartAreas[0];
            area.AxisX.StripLines.Clear();

            // 灰色背景通过设置 ChartArea BackColor 实现
            // 这里我们用 StripLine 画出中间的“白色有效区”
            StripLine validZone = new StripLine();
            validZone.Interval = 0;
            validZone.IntervalOffset = _rangeStart;
            validZone.StripWidth = _rangeEnd - _rangeStart;
            validZone.BackColor = Color.White;
            area.AxisX.StripLines.Add(validZone);

            // 画绿线
            AddLine(area, _rangeStart, "下限");
            AddLine(area, _rangeEnd, "上限");
        }

        private void AddLine(ChartArea area, int x, string txt)
        {
            StripLine line = new StripLine();
            line.Interval = 0;
            line.IntervalOffset = x;
            line.StripWidth = 0;
            line.BorderColor = Color.Green;
            line.BorderDashStyle = ChartDashStyle.Dash;
            line.BorderWidth = 2;
            line.Text = txt;
            line.ForeColor = Color.Green;
            area.AxisX.StripLines.Add(line);
        }

        private void BtnToggleRefresh_Click(object sender, EventArgs e)
        {
            if (!_isRefreshing)
            {
                _isRefreshing = true;
                btnToggleRefresh.Text = "停止刷新";
                btnToggleRefresh.BackColor = Color.LightCoral;
                btnDarkCalib.Enabled = false;
                _refreshThread = new Thread(RefreshLoop) { IsBackground = true };
                _refreshThread.Start();
            }
            else
            {
                _isRefreshing = false;
                btnToggleRefresh.Text = "刷新图像";
                btnToggleRefresh.BackColor = Color.LightGray;
                btnDarkCalib.Enabled = true;
            }
        }

        private void RefreshLoop()
        {
            while (_isRefreshing)
            {
                if (this.IsDisposed || !this.IsHandleCreated) { _isRefreshing = false; break; }

                var result = MainForm.Sensor.GetRawImageDebug();
                double[] data = result.Item1;
                ERRCODE err = result.Item2;

                this.BeginInvoke((MethodInvoker)delegate {
                    if (this.IsDisposed) return;

                    if (err == ERRCODE.OK && data != null)
                    {
                        lblStatus.Text = "状态: ● 刷新中";
                        lblStatus.ForeColor = Color.Green;

                        chartRaw.Series[0].Points.Clear();
                        double maxVal = 0; int maxPos = 0;

                        for (int i = 0; i < data.Length; i++)
                        {
                            chartRaw.Series[0].Points.AddY(data[i]);
                            if (data[i] > maxVal) { maxVal = data[i]; maxPos = i; }
                        }

                        lblPeakValue.Text = $"最大峰值: {maxVal:F0}";
                        lblPeakPos.Text = $"最大峰位置: {maxPos}";

                        chartRaw.ChartAreas[0].AxisY.Maximum = Double.NaN;
                        if (maxVal < 200) chartRaw.ChartAreas[0].AxisY.Maximum = 200;
                    }
                    else
                    {
                        lblStatus.Text = $"错误: {err}";
                        lblStatus.ForeColor = Color.Red;
                    }
                });
                Thread.Sleep(50);
            }
        }

        private void BtnDarkCalib_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("请遮挡探头光路。\n点击【确定】开始...", "提示", MessageBoxButtons.OKCancel) != DialogResult.OK) return;

            btnDarkCalib.Enabled = false;
            btnToggleRefresh.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            Thread t = new Thread(() => {
                MainForm.Sensor.ForceStopMeasurement();
                ERRCODE err = MainForm.Sensor.PerformDarkCalibration();
                Thread.Sleep(1000);
                this.Invoke((MethodInvoker)delegate {
                    btnDarkCalib.Enabled = true;
                    btnToggleRefresh.Enabled = true;
                    this.Cursor = Cursors.Default;
                    if (err == ERRCODE.OK) MessageBox.Show("暗校准成功！", "成功");
                    else MessageBox.Show($"失败: {err}", "错误");
                });
            });
            t.Start();
        }

        private void InitializeCustomUI()
        {
            this.Text = "原始图像";
            this.Size = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            pnlControls = new Panel() { Dock = DockStyle.Top, Height = 70, BackColor = Color.WhiteSmoke };
            btnToggleRefresh = new Button() { Text = "刷新图像", Location = new Point(20, 20), Width = 100, Height = 30, BackColor = Color.LightGray };
            btnToggleRefresh.Click += BtnToggleRefresh_Click;
            btnDarkCalib = new Button() { Text = "暗校准", Location = new Point(140, 20), Width = 100, Height = 30 };
            btnDarkCalib.Click += BtnDarkCalib_Click;

            lblStatus = new Label() { Text = "状态: 待机", Location = new Point(260, 25), AutoSize = true, Font = new Font("微软雅黑", 10) };
            lblPeakValue = new Label() { Text = "最大峰值: 0", Location = new Point(450, 25), AutoSize = true, Font = new Font("微软雅黑", 10, FontStyle.Bold) };
            lblPeakPos = new Label() { Text = "最大峰位置: 0", Location = new Point(600, 25), AutoSize = true, Font = new Font("微软雅黑", 10, FontStyle.Bold) };
            lblRangeInfo = new Label() { Text = "有效量程: --", Location = new Point(750, 25), AutoSize = true, ForeColor = Color.DarkGreen, Font = new Font("微软雅黑", 10, FontStyle.Bold) };

            pnlControls.Controls.AddRange(new Control[] { btnToggleRefresh, btnDarkCalib, lblStatus, lblPeakValue, lblPeakPos, lblRangeInfo });

            chartRaw = new Chart();
            chartRaw.Dock = DockStyle.Fill;
            ChartArea area = new ChartArea("MainArea");
            area.BackColor = Color.FromArgb(220, 220, 220); // 默认灰色
            area.AxisX.Minimum = 0; area.AxisX.Maximum = 1024;
            area.AxisX.Title = "像素";
            area.AxisY.Title = "光强";
            area.AxisX.MajorGrid.LineColor = Color.White;
            area.AxisY.MajorGrid.LineColor = Color.White;
            area.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            chartRaw.ChartAreas.Add(area);

            Series s = new Series("Data") { ChartType = SeriesChartType.FastLine, Color = Color.Blue, BorderWidth = 1 };
            chartRaw.Series.Add(s);

            this.Controls.Add(chartRaw);
            this.Controls.Add(pnlControls);
            this.FormClosing += (Rs, e) => _isRefreshing = false;
        }
    }
}