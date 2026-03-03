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
    /// 历史记录管理窗体 - 查询、查看和删除测量记录
    /// 采用三段式布局：筛选区、列表区、详情/回放区
    /// </summary>
    public partial class HistoryForm : Form
    {
        private MeasurementDataService _dataService;
        private List<MeasurementRecordDTO> _currentRecords;

        // --- UI 控件 ---
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

        public HistoryForm(MeasurementDataService dataService)
        {
            _dataService = dataService;
            InitializeUI();
        }

        private void InitializeUI()
        {
            // 窗体基本设置
            this.Text = "历史记录管理";
            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(900, 500);

            // 顶部筛选区
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
                Value = DateTime.Today.AddDays(-30) // 默认查询最近 30 天
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

            // 主分割容器
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                // 先不设置 SplitterDistance，防止初始化时因宽度不足报错
                Panel1MinSize = 100, // 减小最小限制
                Panel2MinSize = 100
            };

            // 左侧：数据列表
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

            // 设置列
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

            // 右侧：详情 Tab
            tabDetail = new TabControl { Dock = DockStyle.Fill };

            // Tab1: 参数详情
            var tabParams = new TabPage("参数详情");
            propGridDetail = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                PropertySort = PropertySort.NoSort
            };
            tabParams.Controls.Add(propGridDetail);

            // Tab2: 波形回放
            var tabChart = new TabPage("波形回放");
            chartReplay = new Chart { Dock = DockStyle.Fill };
            var area = new ChartArea("main");
            area.AxisX.Title = "位置 (mm)";
            area.AxisY.Title = "高度 (mm)";
            area.AxisY.IsStartedFromZero = false;
            chartReplay.ChartAreas.Add(area);
            chartReplay.Series.Add(new Series("原始轮廓") { ChartType = SeriesChartType.Line, Color = Color.Blue, BorderWidth = 2 });
            tabChart.Controls.Add(chartReplay);

            tabDetail.TabPages.Add(tabParams);
            tabDetail.TabPages.Add(tabChart);

            splitMain.Panel2.Controls.Add(tabDetail);

            // 添加到窗体
            this.Controls.Add(splitMain);
            this.Controls.Add(panelFilter);

            // 安全设置分割位置 (在控件有尺寸后)
            splitMain.SplitterDistance = 500;

            // 初始加载
            LoadRecords();
        }

        /// <summary>
        /// 加载记录列表
        /// </summary>
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
                    LoadRecords(); // 刷新列表
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
                            // 写入表头
                            writer.WriteLine("ID,测量时间,评定长度(mm),间隔(mm),速度(mm/min),Ra(μm),Rz(μm),Mr1(%),Mr2(%),Rpk(μm),Rvk(μm),Rk(μm)");

                            // 写入数据
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
                    // 更新 PropertyGrid
                    propGridDetail.SelectedObject = selectedRecord;

                    // 加载波形数据并绘制
                    LoadAndPlotRawData(id);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"选择记录时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载原始波形并绘制图表
        /// </summary>
        private void LoadAndPlotRawData(long id)
        {
            try
            {
                double[] rawData = _dataService.LoadRawData(id);
                if (rawData == null || rawData.Length == 0)
                {
                    chartReplay.Series["原始轮廓"].Points.Clear();
                    return;
                }

                var selectedRecord = _currentRecords?.Find(r => r.Id == id);
                double dx = selectedRecord?.Interval ?? 0.001;

                // 清除旧数据
                chartReplay.Series["原始轮廓"].Points.Clear();

                // 绘制新数据
                for (int i = 0; i < rawData.Length; i++)
                {
                    double x = i * dx;
                    chartReplay.Series["原始轮廓"].Points.AddXY(x, rawData[i]);
                }

                chartReplay.ChartAreas[0].RecalculateAxesScale();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载波形数据失败: {ex.Message}");
            }
        }
    }
}
