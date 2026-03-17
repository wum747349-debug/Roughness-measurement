using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using ConfocalMeter.Models;
using ConfocalMeter.Infrastructure.Data;

namespace ConfocalMeter.Presentation.Forms
{
    /// <summary>
    /// 历史记录管理窗体 - 查询、查看和删除测量记录。
    /// </summary>
    public partial class HistoryForm : Form
    {
        private readonly MeasurementDataService _dataService;
        private List<MeasurementRecordDTO> _currentRecords;

        // 顶部筛选区
        private Panel panelFilter;
        private DateTimePicker dtpStart, dtpEnd;
        private Button btnQuery, btnDelete, btnExport;
        private Label lblRecordCount;

        // 左侧列表区
        private DataGridView dgvRecords;

        // 右侧详情区
        private SplitContainer splitMain;
        private TabControl tabDetail;
        private PropertyGrid propGridDetail;
        private Chart chartReplay;

        // 历史回放显示缓冲（仅 UI 线程访问）
        private const int REPLAY_RING_CAPACITY = 400000;
        private readonly double[] _replayRing = new double[REPLAY_RING_CAPACITY];
        private int _replayHead = 0;
        private int _replayCount = 0;
        private double _replayDx = 0.001;

        public HistoryForm(MeasurementDataService dataService)
        {
            _dataService = dataService;
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = "历史记录管理";
            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(900, 500);

            panelFilter = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(10, 10, 10, 5)
            };

            var lblStart = new Label { Text = "开始日期:", AutoSize = true, Location = new Point(10, 15) };
            dtpStart = new DateTimePicker
            {
                Location = new Point(75, 12),
                Width = 120,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddDays(-30)
            };

            var lblEnd = new Label { Text = "结束日期:", AutoSize = true, Location = new Point(210, 15) };
            dtpEnd = new DateTimePicker
            {
                Location = new Point(275, 12),
                Width = 120,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };

            btnQuery = new Button
            {
                Text = "查询",
                Location = new Point(420, 10),
                Size = new Size(80, 28),
                BackColor = Color.LightBlue
            };
            btnQuery.Click += BtnQuery_Click;

            btnDelete = new Button
            {
                Text = "批量删除",
                Location = new Point(520, 10),
                Size = new Size(80, 28),
                BackColor = Color.LightCoral,
                ForeColor = Color.White
            };
            btnDelete.Click += BtnDelete_Click;

            btnExport = new Button
            {
                Text = "导出CSV",
                Location = new Point(620, 10),
                Size = new Size(80, 28),
                BackColor = Color.LightGreen
            };
            btnExport.Click += BtnExport_Click;

            lblRecordCount = new Label
            {
                Text = "共 0 条记录",
                AutoSize = true,
                Location = new Point(720, 15)
            };

            panelFilter.Controls.AddRange(new Control[] {
                lblStart, dtpStart, lblEnd, dtpEnd,
                btnQuery, btnDelete, btnExport, lblRecordCount
            });

            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Panel1MinSize = 100,
                Panel2MinSize = 100
            };

            dgvRecords = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White
            };
            dgvRecords.SelectionChanged += DgvRecords_SelectionChanged;

            dgvRecords.Columns.Add("Id", "ID");
            dgvRecords.Columns.Add("MeasurementTime", "测量时间");
            dgvRecords.Columns.Add("Ra", "Ra (μm)");
            dgvRecords.Columns.Add("Rz", "Rz (μm)");
            dgvRecords.Columns.Add("Speed", "速度 (mm/min)");
            dgvRecords.Columns.Add("EvalLength", "评定长度 (mm)");

            dgvRecords.Columns["Id"].Width = 50;
            dgvRecords.Columns["MeasurementTime"].Width = 130;
            dgvRecords.Columns["Ra"].Width = 80;
            dgvRecords.Columns["Rz"].Width = 80;

            splitMain.Panel1.Controls.Add(dgvRecords);

            tabDetail = new TabControl { Dock = DockStyle.Fill };

            var tabParams = new TabPage("参数详情");
            propGridDetail = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                PropertySort = PropertySort.NoSort
            };
            tabParams.Controls.Add(propGridDetail);

            var tabChart = new TabPage("波形回放");
            chartReplay = new Chart { Dock = DockStyle.Fill };
            var area = new ChartArea("main");
            area.AxisX.Title = "位置 (mm)";
            area.AxisY.Title = "高度 (mm)";
            area.AxisY.IsStartedFromZero = false;
            chartReplay.ChartAreas.Add(area);
            chartReplay.Series.Add(new Series("原始轮廓")
            {
                ChartType = SeriesChartType.FastLine,
                Color = Color.Blue,
                BorderWidth = 2
            });
            chartReplay.Resize += (s, e) => PlotReplayFromRing();
            tabChart.Controls.Add(chartReplay);

            tabDetail.TabPages.Add(tabParams);
            tabDetail.TabPages.Add(tabChart);

            splitMain.Panel2.Controls.Add(tabDetail);

            this.Controls.Add(splitMain);
            this.Controls.Add(panelFilter);
            splitMain.SplitterDistance = 500;

            LoadRecords();
        }

        private void LoadRecords()
        {
            try
            {
                _currentRecords = _dataService.QueryByDateRange(dtpStart.Value, dtpEnd.Value);
                dgvRecords.Rows.Clear();

                foreach (var rec in _currentRecords)
                {
                    dgvRecords.Rows.Add(
                        rec.Id,
                        rec.MeasurementTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        rec.Ra.ToString("F3"),
                        rec.Rz.ToString("F3"),
                        rec.Speed.ToString("F1"),
                        rec.EvalLength.ToString("F1")
                    );
                }

                lblRecordCount.Text = $"共 {_currentRecords.Count} 条记录";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnQuery_Click(object sender, EventArgs e)
        {
            LoadRecords();
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_currentRecords == null || _currentRecords.Count == 0)
            {
                MessageBox.Show("当前没有可删除的记录。", "提示");
                return;
            }

            string msg = $"确定要删除 {dtpStart.Value:yyyy-MM-dd} 到 {dtpEnd.Value:yyyy-MM-dd} 期间的 {_currentRecords.Count} 条记录吗？\n\n此操作不可恢复！";
            var result = MessageBox.Show(msg, "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    int deleted = _dataService.DeleteByDateRange(dtpStart.Value, dtpEnd.Value);
                    MessageBox.Show($"成功删除 {deleted} 条记录。", "删除完成");
                    LoadRecords();
                    ClearReplayRing();
                    chartReplay.Series["原始轮廓"].Points.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (_currentRecords == null || _currentRecords.Count == 0)
            {
                MessageBox.Show("没有数据可导出。", "提示");
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV 文件|*.csv";
                sfd.FileName = $"Roughness_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var writer = new System.IO.StreamWriter(sfd.FileName, false, System.Text.Encoding.UTF8))
                        {
                            writer.WriteLine("ID,测量时间,评定长度(mm),间隔(mm),速度(mm/min),Ra(μm),Rz(μm),Mr1(%),Mr2(%),Rpk(μm),Rvk(μm),Rk(μm)");

                            foreach (var rec in _currentRecords)
                            {
                                writer.WriteLine($"{rec.Id},{rec.MeasurementTime:yyyy-MM-dd HH:mm:ss},{rec.EvalLength:F3},{rec.Interval:F6},{rec.Speed:F1},{rec.Ra:F3},{rec.Rz:F3},{rec.Mr1:F2},{rec.Mr2:F2},{rec.Rpk:F3},{rec.Rvk:F3},{rec.Rk:F3}");
                            }
                        }

                        MessageBox.Show($"成功导出 {_currentRecords.Count} 条记录到:\n{sfd.FileName}", "导出成功");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void DgvRecords_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvRecords.SelectedRows.Count == 0) return;

            try
            {
                long id = Convert.ToInt64(dgvRecords.SelectedRows[0].Cells["Id"].Value);
                var selectedRecord = _currentRecords?.Find(r => r.Id == id);

                if (selectedRecord != null)
                {
                    propGridDetail.SelectedObject = selectedRecord;
                    LoadAndPlotRawData(id);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"选择记录时出错: {ex.Message}");
            }
        }

        private void LoadAndPlotRawData(long id)
        {
            try
            {
                double[] rawData = _dataService.LoadRawData(id);
                if (rawData == null || rawData.Length == 0)
                {
                    ClearReplayRing();
                    chartReplay.Series["原始轮廓"].Points.Clear();
                    return;
                }

                var selectedRecord = _currentRecords?.Find(r => r.Id == id);
                _replayDx = selectedRecord?.Interval ?? 0.001;

                ClearReplayRing();
                for (int i = 0; i < rawData.Length; i++)
                {
                    AddReplaySample(rawData[i]);
                }

                PlotReplayFromRing();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载波形数据失败: {ex.Message}");
            }
        }

        private void PlotReplayFromRing()
        {
            var series = chartReplay.Series["原始轮廓"];
            if (_replayCount == 0)
            {
                series.Points.Clear();
                return;
            }

            int pixelBuckets = Math.Max(300, chartReplay.ClientSize.Width - 80);
            int samplesPerBucket = Math.Max(1, (int)Math.Ceiling(_replayCount / (double)pixelBuckets));
            int bucketCount = (int)Math.Ceiling(_replayCount / (double)samplesPerBucket);

            series.Points.SuspendUpdates();
            series.Points.Clear();

            int oldest = GetReplayOldestIndex();
            for (int b = 0; b < bucketCount; b++)
            {
                int start = b * samplesPerBucket;
                int end = Math.Min(_replayCount, start + samplesPerBucket);
                if (start >= end) break;

                double minV = double.MaxValue;
                double maxV = double.MinValue;
                int minI = start;
                int maxI = start;

                for (int i = start; i < end; i++)
                {
                    int ringIndex = (oldest + i) % REPLAY_RING_CAPACITY;
                    double v = _replayRing[ringIndex];
                    if (v < minV)
                    {
                        minV = v;
                        minI = i;
                    }
                    if (v > maxV)
                    {
                        maxV = v;
                        maxI = i;
                    }
                }

                if (minI <= maxI)
                {
                    series.Points.AddXY(minI * _replayDx, minV);
                    if (minI != maxI || Math.Abs(maxV - minV) > double.Epsilon)
                        series.Points.AddXY(maxI * _replayDx, maxV);
                }
                else
                {
                    series.Points.AddXY(maxI * _replayDx, maxV);
                    if (minI != maxI || Math.Abs(maxV - minV) > double.Epsilon)
                        series.Points.AddXY(minI * _replayDx, minV);
                }
            }

            series.Points.ResumeUpdates();
            chartReplay.ChartAreas[0].RecalculateAxesScale();
        }

        private void AddReplaySample(double value)
        {
            _replayRing[_replayHead] = value;
            _replayHead = (_replayHead + 1) % REPLAY_RING_CAPACITY;
            if (_replayCount < REPLAY_RING_CAPACITY)
            {
                _replayCount++;
            }
        }

        private int GetReplayOldestIndex()
        {
            return (_replayHead - _replayCount + REPLAY_RING_CAPACITY) % REPLAY_RING_CAPACITY;
        }

        private void ClearReplayRing()
        {
            _replayHead = 0;
            _replayCount = 0;
        }
    }
}
