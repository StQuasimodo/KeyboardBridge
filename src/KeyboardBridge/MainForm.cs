using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KeyboardBridge
{
    public class MainForm : Form
    {
        private HdcClient _hdc;
        private Injector _inj;
        private string _target = "";
        private bool _busy;

        private Label _status;
        private Label _deviceInfo;
        private Label _lblBundle;
        private Label _counter;
        private TextBox _txtBundle;
        private TextBox _editor;
        private TextBox _log;
        private Button _btnConnect;
        private Button _btnDisconnect;
        private Button _btnTest;
        private Button _btnSend;
        private CheckBox _chkVerify;
        private CheckBox _chkClear;
        private CheckBox _chkQuotes;

        public MainForm()
        {
            Text = "KeyboardBridge — Windows 键盘输入助手";
            ClientSize = new Size(944, 700);
            MinimumSize = new Size(780, 580);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);

            _status = new Label();
            _status.Location = new Point(14, 12);
            _status.AutoSize = true;
            _status.Text = "● 未连接";
            _status.ForeColor = Color.Firebrick;
            _status.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);

            _deviceInfo = new Label();
            _deviceInfo.Location = new Point(14, 42);
            _deviceInfo.AutoSize = true;
            _deviceInfo.Text = "设备：—";

            _lblBundle = new Label();
            _lblBundle.Location = new Point(14, 76);
            _lblBundle.AutoSize = true;
            _lblBundle.Text = "目标应用包名（留空则不校验）：";

            _txtBundle = new TextBox();
            _txtBundle.Location = new Point(226, 72);
            _txtBundle.Size = new Size(320, 24);
            _txtBundle.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            _btnConnect = new Button();
            _btnConnect.Text = "连接 / 刷新";
            _btnConnect.Location = new Point(668, 10);
            _btnConnect.Size = new Size(118, 30);
            _btnConnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnConnect.Click += new EventHandler(OnConnect);

            _btnDisconnect = new Button();
            _btnDisconnect.Text = "断开";
            _btnDisconnect.Location = new Point(794, 10);
            _btnDisconnect.Size = new Size(126, 30);
            _btnDisconnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnDisconnect.Click += new EventHandler(OnDisconnect);

            _btnTest = new Button();
            _btnTest.Text = "测试输入";
            _btnTest.Location = new Point(794, 44);
            _btnTest.Size = new Size(126, 30);
            _btnTest.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnTest.Click += new EventHandler(OnTestInput);

            _editor = new TextBox();
            _editor.Location = new Point(14, 112);
            _editor.Size = new Size(906, 356);
            _editor.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _editor.Multiline = true;
            _editor.AcceptsReturn = true;
            _editor.ScrollBars = ScrollBars.Vertical;
            _editor.Font = new Font("Microsoft YaHei UI", 11F);
            _editor.WordWrap = true;
            _editor.TextChanged += new EventHandler(OnEditorChanged);
            _editor.KeyDown += new KeyEventHandler(OnEditorKeyDown);

            _counter = new Label();
            _counter.Location = new Point(14, 480);
            _counter.AutoSize = true;
            _counter.Text = "字数：0　行数：1";
            _counter.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            _chkVerify = new CheckBox();
            _chkVerify.Location = new Point(150, 478);
            _chkVerify.AutoSize = true;
            _chkVerify.Text = "发送后回读校验";
            _chkVerify.Checked = true;
            _chkVerify.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            _chkClear = new CheckBox();
            _chkClear.Location = new Point(300, 478);
            _chkClear.AutoSize = true;
            _chkClear.Text = "发送后清空编辑框";
            _chkClear.Checked = false;
            _chkClear.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            _chkQuotes = new CheckBox();
            _chkQuotes.Location = new Point(460, 478);
            _chkQuotes.AutoSize = true;
            _chkQuotes.Text = "英文引号自动转中文引号";
            _chkQuotes.Checked = false;
            _chkQuotes.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            _btnSend = new Button();
            _btnSend.Text = "发送 (Ctrl+Enter)";
            _btnSend.Location = new Point(770, 474);
            _btnSend.Size = new Size(150, 32);
            _btnSend.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnSend.Click += new EventHandler(OnSend);

            _log = new TextBox();
            _log.Location = new Point(14, 512);
            _log.Size = new Size(906, 174);
            _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = Color.White;
            _log.Font = new Font("Consolas", 9F);

            Controls.Add(_status);
            Controls.Add(_deviceInfo);
            Controls.Add(_lblBundle);
            Controls.Add(_txtBundle);
            Controls.Add(_btnConnect);
            Controls.Add(_btnDisconnect);
            Controls.Add(_btnTest);
            Controls.Add(_editor);
            Controls.Add(_counter);
            Controls.Add(_chkVerify);
            Controls.Add(_chkClear);
            Controls.Add(_chkQuotes);
            Controls.Add(_btnSend);
            Controls.Add(_log);

            Load += new EventHandler(OnFormLoad);
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            _hdc = new HdcClient(HdcClient.AutoDetect());
            _inj = new Injector(_hdc);

            if (string.IsNullOrEmpty(_hdc.HdcPath))
            {
                Log("未找到 hdc.exe。请安装 DevEco Studio，或设置环境变量 KB_HDC 指向 hdc.exe。");
                SetStatus(false, "● 未找到 hdc");
                return;
            }
            Log("hdc 已就绪。正在检测设备…");
            DoConnect();
        }

        private void SetStatus(bool connected, string text)
        {
            _status.Text = text;
            _status.ForeColor = connected ? Color.SeaGreen : Color.Firebrick;
        }

        private void Log(string msg)
        {
            if (InvokeRequired)
            {
                try { Invoke(new Action<string>(Log), msg); } catch { }
                return;
            }
            if (_log.TextLength > 60000) _log.Clear();
            _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _btnConnect.Enabled = !busy;
            _btnDisconnect.Enabled = !busy;
            _btnTest.Enabled = !busy;
            _btnSend.Enabled = !busy;
        }

        private void OnConnect(object sender, EventArgs e)
        {
            DoConnect();
        }

        private void DoConnect()
        {
            if (_busy) return;
            SetBusy(true);
            Task.Factory.StartNew(delegate
            {
                try
                {
                    var targets = _hdc.ListTargets();
                    if (targets.Count == 0)
                    {
                        _target = "";
                        SetStatus(false, "● 未连接");
                        this.Invoke(new Action(delegate
                        {
                            _deviceInfo.Text = "设备：—（请开启无线调试后执行 hdc tconn <IP>:<端口>）";
                        }));
                        Log("未检测到设备。");
                        return;
                    }

                    _target = targets[0];
                    string api = SafeShell("param get const.ohos.apiversion");
                    string model = SafeShell("param get const.product.model");
                    string soft = SafeShell("param get const.product.software.version");

                    this.Invoke(new Action(delegate
                    {
                        _deviceInfo.Text = "设备：" + HdcClient.Mask(_target) +
                                           "　型号：" + model +
                                           "　API：" + api;
                        SetStatus(true, "● 已连接");
                    }));
                    Log("已连接设备 " + HdcClient.Mask(_target) + "（API " + api + "）");

                    if (targets.Count > 1)
                        Log("检测到 " + targets.Count + " 台设备，当前只使用第一台。");

                    _inj.CleanupStale();
                }
                catch (Exception ex)
                {
                    Log("连接检测异常：" + ex.Message);
                }
                finally
                {
                    this.Invoke(new Action(delegate { SetBusy(false); }));
                }
            });
        }

        private string SafeShell(string cmd)
        {
            try
            {
                HdcResult r = _hdc.Shell(cmd);
                return r.All.Replace("\r", "").Replace("\n", " ").Trim();
            }
            catch { return "?"; }
        }

        private void OnDisconnect(object sender, EventArgs e)
        {
            if (_busy) return;
            if (string.IsNullOrEmpty(_target))
            {
                Log("当前没有已连接的设备。");
                return;
            }

            if (_target.IndexOf(':') < 0)
            {
                Log("当前是 USB 连接，程序不会主动断开（避免影响你在其他工具中的调试）。已仅清除本地状态。");
                _target = "";
                SetStatus(false, "● 未连接");
                _deviceInfo.Text = "设备：—";
                return;
            }

            DialogResult dr = MessageBox.Show(
                "将断开无线调试连接 " + HdcClient.Mask(_target) + "。\n手机上的设置不会被修改，随时可以重新连接。\n\n继续？",
                "断开连接", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (dr != DialogResult.OK) return;

            SetBusy(true);
            Task.Factory.StartNew(delegate
            {
                try
                {
                    HdcResult r = _hdc.Run(new string[] { "tconn", _target, "-remove" });
                    Log("断开结果：" + (r.All.Length > 0 ? r.All : "无输出"));
                    _target = "";
                    this.Invoke(new Action(delegate
                    {
                        SetStatus(false, "● 未连接");
                        _deviceInfo.Text = "设备：—";
                    }));
                }
                catch (Exception ex) { Log("断开异常：" + ex.Message); }
                finally { this.Invoke(new Action(delegate { SetBusy(false); })); }
            });
        }

        private void OnEditorChanged(object sender, EventArgs e)
        {
            string t = _editor.Text;
            int lines = 1;
            for (int i = 0; i < t.Length; i++) if (t[i] == '\n') lines++;
            _counter.Text = "字数：" + t.Length + "　行数：" + lines;
        }

        private void OnEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return))
            {
                e.SuppressKeyPress = true;
                DoSend(_editor.Text);
            }
        }

        private void OnSend(object sender, EventArgs e)
        {
            DoSend(_editor.Text);
        }

        private void OnTestInput(object sender, EventArgs e)
        {
            DoSend("KeyboardBridge 测试输入 OK 123");
        }

        private void DoSend(string text)
        {
            if (_busy) return;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                Log("编辑框为空，没有可发送的内容。");
                return;
            }
            if (string.IsNullOrEmpty(_target))
            {
                Log("尚未连接设备，请先连接。");
                return;
            }

            // 发送前确认前台应用（只在不为空时校验）
            string wantBundle = _txtBundle.Text.Trim();

            SetBusy(true);
            Log("开始发送 " + text.Length + " 个字符…");

            Task.Factory.StartNew(delegate
            {
                try
                {
                    if (wantBundle.Length > 0)
                    {
                        string fg = _inj.GetForegroundBundle();
                        if (fg != null && !string.Equals(fg, wantBundle, StringComparison.OrdinalIgnoreCase))
                        {
                            Log("前台应用是 " + fg + "，与目标 " + wantBundle + " 不一致，已中止发送。");
                            return;
                        }
                    }

                    SendOptions opt = new SendOptions();
                    opt.VerifyAfterSend = _chkVerify.Checked;
                    opt.ReplaceDoubleQuotes = _chkQuotes.Checked;

                    SendReport rep = _inj.Send(text, opt, new Action<string>(Log));

                    if (rep.Ok)
                    {
                        Log(rep.Describe());
                        if (_chkClear.Checked)
                        {
                            this.Invoke(new Action(delegate { _editor.Clear(); }));
                        }
                        if (rep.VerifyAttempted && rep.Verified == false)
                        {
                            Log("提示：回读未匹配。可能是输入框不支持、焦点已变，或文本含特殊字符。请核对手机后再决定是否重发。");
                        }
                    }
                    else
                    {
                        Log("发送失败：" + rep.Message);
                        Log("请先核对手机，避免重复写入。");
                    }
                }
                catch (Exception ex)
                {
                    Log("发送异常：" + ex.Message);
                }
                finally
                {
                    this.Invoke(new Action(delegate { SetBusy(false); }));
                }
            });
        }
    }
}
