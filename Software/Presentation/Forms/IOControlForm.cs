using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConfocalMeter
{
    /// <summary>
    /// IO 控制窗口：用于控制 CM35 控制器输入并监测输出状态。
    /// 串口连接由主界面管理，本窗口仅负责 IO 显示与操作。
    /// </summary>
    public partial class IOControlForm : Form
    {
        private GroupBox grpInputs;
        private CheckBox[] chkInputs = new CheckBox[8];

        private GroupBox grpOutputs;
        private Panel[] pnlOutputs = new Panel[8];
        private Label[] lblOutputs = new Label[8];

        private GroupBox grpRawData;
        private RichTextBox rtbRawData;

        private Label lblConnectionStatus;
        private Timer tmrRefresh;
        private bool _isSyncingInputs;

        public IOControlForm()
        {
            InitializeCustomUI();
            BindEvents();
        }

        private void InitializeCustomUI()
        {
            this.Text = "IO控制 - CM35电机控制器";
            this.Size = new Size(750, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("微软雅黑", 9F);

            lblConnectionStatus = new Label()
            {
                Text = "请先在主界面打开 MCU 串口",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("微软雅黑", 11F, FontStyle.Bold),
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblConnectionStatus);

            grpInputs = new GroupBox()
            {
                Text = "输入控制 (PC -> MCU -> CM35 Input)",
                Location = new Point(20, 50),
                Size = new Size(340, 200)
            };

            for (int i = 0; i < 8; i++)
            {
                int col = i % 2;
                int row = i / 2;
                chkInputs[i] = new CheckBox()
                {
                    Text = $"输入 IN{11 + i}",
                    Location = new Point(30 + col * 160, 35 + row * 40),
                    AutoSize = true,
                    Font = new Font("微软雅黑", 10F)
                };

                int index = i;
                chkInputs[i].CheckedChanged += (s, e) =>
                {
                    if (_isSyncingInputs) return;
                    McuSerialManager.Instance.SetInputBit(index, chkInputs[index].Checked);
                };

                grpInputs.Controls.Add(chkInputs[i]);
            }
            this.Controls.Add(grpInputs);

            grpOutputs = new GroupBox()
            {
                Text = "输出监测 (MCU -> CM35 Output)",
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

            grpRawData = new GroupBox()
            {
                Text = "原始数据监测 (Hex)",
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

            tmrRefresh = new Timer() { Interval = 100 };
        }

        private void BindEvents()
        {
            this.Load += IOControlForm_Load;
            this.FormClosing += IOControlForm_FormClosing;
            tmrRefresh.Tick += TmrRefresh_Tick;
        }

        private void IOControlForm_Load(object sender, EventArgs e)
        {
            McuSerialManager.Instance.OnLog += AppendLog;
            McuSerialManager.Instance.OnOutputStateChanged += UpdateOutputUI;
            McuSerialManager.Instance.OnConnectionChanged += UpdateConnectionStatus;

            UpdateConnectionStatus(McuSerialManager.Instance.IsOpen);
            SyncInputUI(McuSerialManager.Instance.InputMap);
            UpdateOutputUI(McuSerialManager.Instance.OutputState);

            tmrRefresh.Start();
        }

        private void IOControlForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            tmrRefresh.Stop();

            McuSerialManager.Instance.OnLog -= AppendLog;
            McuSerialManager.Instance.OnOutputStateChanged -= UpdateOutputUI;
            McuSerialManager.Instance.OnConnectionChanged -= UpdateConnectionStatus;
        }

        private void TmrRefresh_Tick(object sender, EventArgs e)
        {
            bool isConnected = McuSerialManager.Instance.IsConnected;
            SyncInputUI(McuSerialManager.Instance.InputMap);

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
                lblConnectionStatus.Text = "请先在主界面打开 MCU 串口";
                lblConnectionStatus.ForeColor = Color.Gray;
                SyncInputUI(0x00);
            }
        }

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
                lblConnectionStatus.Text = "请先在主界面打开 MCU 串口";
                lblConnectionStatus.ForeColor = Color.Gray;
                ResetOutputs();
                SyncInputUI(0x00);
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

        private void SyncInputUI(byte inputMap)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<byte>(SyncInputUI), inputMap);
                return;
            }

            _isSyncingInputs = true;
            try
            {
                for (int i = 0; i < chkInputs.Length; i++)
                {
                    bool shouldChecked = (inputMap & (1 << i)) != 0;
                    if (chkInputs[i].Checked != shouldChecked)
                    {
                        chkInputs[i].Checked = shouldChecked;
                    }
                }
            }
            finally
            {
                _isSyncingInputs = false;
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

        private void IOControlForm_Load_1(object sender, EventArgs e)
        {
        }
    }
}
