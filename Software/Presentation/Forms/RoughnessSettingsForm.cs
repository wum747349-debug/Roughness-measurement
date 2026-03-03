using System;
using System.Drawing;
using System.Windows.Forms;
using ConfocalMeter.Models;

namespace ConfocalMeter
{
    public partial class RoughnessSettingsForm : Form
    {
        // 对外公开的属性，供主界面读取
        public DetrendOptions Options { get; private set; }

        // 类成员变量（控件）
        private NumericUpDown nudLambdaC;
        private ComboBox cmbFilterMode;
        private CheckBox chkUseLoess;
        private NumericUpDown nudPsdThreshold;
        private Button btnOK;
        private Button btnCancel;
        private Button btnAdvice;

        public RoughnessSettingsForm(DetrendOptions currentOptions)
        {
            // 初始化 UI
            InitializeCustomUI(currentOptions);
        }

        private void InitializeCustomUI(DetrendOptions opts)
        {
            this.Text = "粗糙度算法参数设置";
            this.Size = new Size(500, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int x = 30;
            int y = 30;
            int gap = 50;

            // 1. 截止波长 Lambda C
            Label lblL = new Label { Text = "截止波长 λc (mm):", Location = new Point(x, y), AutoSize = true };
            nudLambdaC = new NumericUpDown
            {
                Location = new Point(x + 200, y - 3),
                DecimalPlaces = 3,
                Increment = 0.1M,
                Maximum = 100,
                Value = (decimal)opts.LambdaC,
                Width = 120
            };
            this.Controls.Add(lblL);
            this.Controls.Add(nudLambdaC);

            y += gap;

            // 2. 滤波模式
            Label lblM = new Label { Text = "滤波模式:", Location = new Point(x, y), AutoSize = true };
            cmbFilterMode = new ComboBox
            {
                Location = new Point(x + 200, y - 3),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFilterMode.Items.AddRange(new object[] { "严格高通 (ISO 16610-21)", "轻度去噪 (保真优先)", "稳健高斯回归 (抗异常)" });
            cmbFilterMode.SelectedIndex = (int)opts.Mode;
            this.Controls.Add(lblM);
            this.Controls.Add(cmbFilterMode);

            y += gap;

            // 3. LOESS 去趋势
            chkUseLoess = new CheckBox
            {
                Text = "启用 LOESS 去趋势 (消除波浪度)",
                Location = new Point(x, y),
                AutoSize = true,
                Checked = opts.UseRobustLoess
            };
            this.Controls.Add(chkUseLoess);

            y += gap;

            // 4. PSD 自检阈值
            Label lblP = new Label { Text = "PSD 自检阈值 (%):", Location = new Point(x, y), AutoSize = true };
            nudPsdThreshold = new NumericUpDown
            {
                Location = new Point(x + 200, y - 3),
                Value = (decimal)opts.SelfCheckDropPct,
                Width = 80
            };
            this.Controls.Add(lblP);
            this.Controls.Add(nudPsdThreshold);

            y += gap;

            // 5. 建议按钮
            btnAdvice = new Button { Text = "查看滤波建议", Location = new Point(x, y), Width = 150, Height = 30 };
            btnAdvice.Click += (s, e) => MessageBox.Show(GetAdviceText(), "专家建议", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Controls.Add(btnAdvice);

            // 底部按钮
            y += 80;
            btnOK = new Button { Text = "确定", Location = new Point(100, y), DialogResult = DialogResult.OK, Width = 100, Height = 35, BackColor = Color.LightGreen };
            btnOK.Click += (s, e) => SaveOptions(); // 绑定保存事件

            btnCancel = new Button { Text = "取消", Location = new Point(250, y), DialogResult = DialogResult.Cancel, Width = 100, Height = 35 };

            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
        }

        private void SaveOptions()
        {
            // 将界面控件的值保存到 Options 对象中
            Options = new DetrendOptions
            {
                LambdaC = (double)nudLambdaC.Value,
                Mode = (RoughnessMode)cmbFilterMode.SelectedIndex,
                UseRobustLoess = chkUseLoess.Checked,
                SelfCheckDropPct = (double)nudPsdThreshold.Value,

                // 其他参数使用默认值
                WeakLoess = true,
                AutoRelaxLambda = true,
                LimitLambdaC = true,
                Degree = 1
            };
        }

        private string GetAdviceText()
        {
            return "1) 标准模式（默认）：\n" +
                   "   适用于大多数平坦表面，符合 ISO 标准流程。\n\n" +
                   "2) 短记录模式：\n" +
                   "   当测量长度 < 3倍 λc 时使用，避免过度过滤。\n\n" +
                   "3) 稳健模式：\n" +
                   "   当表面有深槽、台阶或严重划痕时使用。\n\n" +
                   "LOESS去趋势：\n" +
                   "   建议始终开启，用于去除由于工件倾斜或表面波浪度引起的低频误差。";
        }
    }
}