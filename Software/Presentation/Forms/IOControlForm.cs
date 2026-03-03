using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfocalMeter
{
    /// <summary>
    /// IO控制窗体 - 用于控制CM35电机控制器的输入输出
    /// 串口通信由主界面管理，本窗体仅负责IO控制和状态显示
    /// </summary>
    public partial class IOControlForm : Form
    {
        #region UI控件

        // 输入控制区
        private GroupBox grpInputs;
        private CheckBox[] chkInputs = new CheckBox[8];

        // 输出监测区
        private GroupBox grpOutputs;
        private Panel[] pnlOutputs = new Panel[8];
        private Label[] lblOutputs = new Label[8];

        // 原始数据监控区
        private GroupBox grpRawData;
        private RichTextBox rtbRawData;

        // 状态显示
        private Label lblConnectionStatus;

        // 定时器 - 用于刷新UI状态
        private Timer tmrRefresh;

        #endregion

        public IOControlForm()
        {
            InitializeCustomUI();
            BindEvents();
        }

        #region UI初始化

        private void InitializeCustomUI()
        {
            this.Text = "IO控制 - CM35电机控制器";
            this.Size = new Size(750, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("微软雅黑", 9F);

            // ========== 连接状态提示 ==========
            lblConnectionStatus = new Label()
            {
                Text = "请先在主界面打开MCU串口",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("微软雅黑", 11F, FontStyle.Bold),
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblConnectionStatus);

            // ========== 输入控制区 ==========
            grpInputs = new GroupBox()
            {
                Text = "输入控制 (PC → MCU → CM35 Input)",
                Location = new Point(20, 50),
                Size = new Size(340, 200)
            };

            for (int i = 0; i < 8; i++)
            {
                int col = i % 2;
                int row = i / 2;
                chkInputs[i] = new CheckBox()
                {
                    Text = $"输入 IN{i + 1}",
                    Location = new Point(30 + col * 160, 35 + row * 40),
                    AutoSize = true,
                    Font = new Font("微软雅黑", 10F)
                };
                int index = i; // 闭包变量
                chkInputs[i].CheckedChanged += (s, e) =>
                {
                    McuSerialManager.Instance.SetInputBit(index, chkInputs[index].Checked);
                };
                grpInputs.Controls.Add(chkInputs[i]);
            }
            this.Controls.Add(grpInputs);

            // ========== 输出监测区 ==========
            grpOutputs = new GroupBox()
            {
                Text = "输出监测 (MCU ← CM35 Output)",
                Location = new Point(380, 50),
                Size = new Size(350, 200)
            };

            for (int i = 0; i < 8; i++)
            {
                int col = i % 4;
                int row = i / 4;
                int baseX = 25 + col * 80;
                int baseY = 35 + row * 85;

                lblOutputs[i] = new Label()
                {
                    Text = $"OUT{i + 1}",
                    Location = new Point(baseX, baseY),
                    AutoSize = true,
                    Font = new Font("微软雅黑", 9F)
                };
                grpOutputs.Controls.Add(lblOutputs[i]);

                pnlOutputs[i] = new Panel()
                {
                    Location = new Point(baseX, baseY + 22),
                    Size = new Size(40, 40),
                    BackColor = Color.Gray,
                    BorderStyle = BorderStyle.FixedSingle
                };
                grpOutputs.Controls.Add(pnlOutputs[i]);
            }
            this.Controls.Add(grpOutputs);

            // ========== 原始数据监控区 ==========
            grpRawData = new GroupBox()
            {
                Text = "原始数据监控 (Hex)",
                Location = new Point(20, 265),
                Size = new Size(710, 280)
            };

            rtbRawData = new RichTextBox()
            {
                Location = new Point(10, 25),
                Size = new Size(690, 240),
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 10F),
                ReadOnly = true
            };
            grpRawData.Controls.Add(rtbRawData);
            this.Controls.Add(grpRawData);

            // ========== 定时器 ==========
            tmrRefresh = new Timer() { Interval = 100 };
        }

        #endregion

        #region 事件绑定

        private void BindEvents()
        {
            this.Load += IOControlForm_Load;
            this.FormClosing += IOControlForm_FormClosing;
            tmrRefresh.Tick += TmrRefresh_Tick;
        }

        private void IOControlForm_Load(object sender, EventArgs e)
        {
            // 订阅Manager事件
            McuSerialManager.Instance.OnLog += AppendLog;
            McuSerialManager.Instance.OnOutputStateChanged += UpdateOutputUI;
            McuSerialManager.Instance.OnConnectionChanged += UpdateConnectionStatus;

            // 初始化UI状态
            UpdateConnectionStatus(McuSerialManager.Instance.IsOpen);
            UpdateOutputUI(McuSerialManager.Instance.OutputState);

            tmrRefresh.Start();
        }

        private void IOControlForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            tmrRefresh.Stop();

            // 取消订阅事件
            McuSerialManager.Instance.OnLog -= AppendLog;
            McuSerialManager.Instance.OnOutputStateChanged -= UpdateOutputUI;
            McuSerialManager.Instance.OnConnectionChanged -= UpdateConnectionStatus;
        }

        private void TmrRefresh_Tick(object sender, EventArgs e)
        {
            // 定期刷新连接状态
            bool isConnected = McuSerialManager.Instance.IsConnected;
            if (isConnected)
            {
                lblConnectionStatus.Text = $"通信正常 - {McuSerialManager.Instance.PortName}";
                lblConnectionStatus.ForeColor = Color.Green;
            }
            else if (McuSerialManager.Instance.IsOpen)
            {
                lblConnectionStatus.Text = $"通信超时 - {McuSerialManager.Instance.PortName}";
                lblConnectionStatus.ForeColor = Color.Red;
            }
            else
            {
                lblConnectionStatus.Text = "请先在主界面打开MCU串口";
                lblConnectionStatus.ForeColor = Color.Gray;
            }
        }

        #endregion

        #region UI更新

        private void UpdateConnectionStatus(bool isOpen)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<bool>(UpdateConnectionStatus), isOpen);
                return;
            }

            if (isOpen)
            {
                lblConnectionStatus.Text = $"已连接 - {McuSerialManager.Instance.PortName}";
                lblConnectionStatus.ForeColor = Color.Green;
            }
            else
            {
                lblConnectionStatus.Text = "请先在主界面打开MCU串口";
                lblConnectionStatus.ForeColor = Color.Gray;
                ResetOutputs();
            }
        }

        private void UpdateOutputUI(byte outputByte)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<byte>(UpdateOutputUI), outputByte);
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                bool isActive = (outputByte & (1 << i)) != 0;
                pnlOutputs[i].BackColor = isActive ? Color.Lime : Color.Gray;
            }
        }

        private void ResetOutputs()
        {
            foreach (var pnl in pnlOutputs)
            {
                pnl.BackColor = Color.Gray;
            }
        }

        private void AppendLog(string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(AppendLog), msg);
                return;
            }

            if (rtbRawData.IsDisposed) return;

            if (rtbRawData.TextLength > 10000)
            {
                rtbRawData.Clear();
            }

            rtbRawData.AppendText($"{DateTime.Now:HH:mm:ss.fff} {msg}\r\n");
            rtbRawData.ScrollToCaret();
        }

        #endregion

        private void IOControlForm_Load_1(object sender, EventArgs e)
        {

        }
    }
}
