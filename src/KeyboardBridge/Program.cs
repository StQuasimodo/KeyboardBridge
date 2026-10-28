using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace KeyboardBridge
{
    public static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;

        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal))
            {
                return RunCli(args);
            }

            // 图形模式：隐藏本进程附带的控制台窗口
            HideConsole();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        private static void HideConsole()
        {
            try
            {
                IntPtr h = GetConsoleWindow();
                if (h != IntPtr.Zero) ShowWindow(h, SW_HIDE);
            }
            catch { }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("KeyboardBridge 命令行模式");
            Console.WriteLine("  --status              显示 hdc 路径、连接状态与设备信息");
            Console.WriteLine("  --send <文本>          发送文本到手机当前获焦输入框");
            Console.WriteLine("  --test                发送一段固定测试文本");
            Console.WriteLine("  --foreground          显示手机前台应用包名");
            Console.WriteLine("  --no-verify           配合 --send 使用，跳过回读校验");
            Console.WriteLine("  无参数                 启动图形界面");
        }

        private static int RunCli(string[] args)
        {
            string cmd = args[0].ToLowerInvariant();

            if (cmd == "--help" || cmd == "-h")
            {
                PrintHelp();
                return 0;
            }

            string hdcPath = HdcClient.AutoDetect();
            HdcClient hdc = new HdcClient(hdcPath);
            Injector inj = new Injector(hdc);

            if (string.IsNullOrEmpty(hdcPath))
            {
                Console.WriteLine("ERROR: 找不到 hdc.exe。请安装 DevEco Studio 或设置 KB_HDC 环境变量。");
                return 2;
            }

            List<string> targets = hdc.ListTargets();
            if (targets.Count == 0)
            {
                Console.WriteLine("BLOCKED: 没有检测到设备。请开启手机无线调试后执行 hdc tconn <IP>:<端口>。");
                return 2;
            }
            string target = targets[0];

            if (cmd == "--status")
            {
                Console.WriteLine("hdc   : " + hdcPath);
                Console.WriteLine("设备  : " + HdcClient.Mask(target) + "（共 " + targets.Count + " 台）");
                Console.WriteLine("API   : " + OneLine(hdc.Shell("param get const.ohos.apiversion").All));
                Console.WriteLine("型号  : " + OneLine(hdc.Shell("param get const.product.model").All));
                Console.WriteLine("系统  : " + OneLine(hdc.Shell("param get const.product.software.version").All));

                string b = hdc.Shell("ls /system/bin").All;
                Console.WriteLine("uitest: " + (b.IndexOf("uitest", StringComparison.Ordinal) >= 0 ? "FOUND" : "MISSING"));
                Console.WriteLine("uinput: " + (b.IndexOf("uinput", StringComparison.Ordinal) >= 0 ? "FOUND" : "MISSING"));
                return 0;
            }

            if (cmd == "--foreground")
            {
                string fg = inj.GetForegroundBundle();
                Console.WriteLine(fg == null ? "UNKNOWN" : fg);
                return fg == null ? 1 : 0;
            }

            if (cmd == "--echo")
            {
                StringBuilder eb = new StringBuilder();
                for (int i = 1; i < args.Length; i++)
                {
                    if (eb.Length > 0) eb.Append(' ');
                    eb.Append(args[i]);
                }
                string raw = eb.ToString();
                string line = "echo " + Injector.ShellQuote(raw);
                Console.WriteLine("CMD: " + line);
                HdcResult er = hdc.Shell(line);
                string got = er.All.Replace("\r", "").Replace("\n", "");
                Console.WriteLine("OUT: [" + got + "]");
                Console.WriteLine("EQ : " + (got == raw ? "PASS" : "FAIL"));
                return 0;
            }

            if (cmd == "--send-file")
            {
                if (args.Length < 2 || !File.Exists(args[1]))
                {
                    Console.WriteLine("ERROR: 需要提供一个存在的文本文件路径。");
                    return 1;
                }
                string text;
                using (StreamReader sr = new StreamReader(args[1], Encoding.UTF8, true))
                {
                    text = sr.ReadToEnd();
                }
                SendOptions fo = new SendOptions();
                fo.VerifyAfterSend = true;
                for (int i = 2; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--replace-quotes", StringComparison.OrdinalIgnoreCase))
                        fo.ReplaceDoubleQuotes = true;
                    if (string.Equals(args[i], "--no-verify", StringComparison.OrdinalIgnoreCase))
                        fo.VerifyAfterSend = false;
                }
                SendReport frep = inj.Send(text, fo, new Action<string>(delegate(string s)
                {
                    Console.WriteLine("  " + s);
                }));
                Console.WriteLine(frep.Ok ? "OK" : "FAIL");
                return frep.Ok ? 0 : 1;
            }

            if (cmd == "--send" || cmd == "--test")
            {
                bool verify = true;
                string text;
                if (cmd == "--test")
                {
                    text = "KeyboardBridge 测试输入 OK 123";
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 1; i < args.Length; i++)
                    {
                        if (string.Equals(args[i], "--no-verify", StringComparison.OrdinalIgnoreCase))
                        {
                            verify = false;
                            continue;
                        }
                        if (sb.Length > 0) sb.Append(' ');
                        sb.Append(args[i]);
                    }
                    text = sb.ToString();
                }

                if (text.Trim().Length == 0)
                {
                    Console.WriteLine("ERROR: 没有要发送的文本。");
                    return 1;
                }

                SendOptions opt = new SendOptions();
                opt.VerifyAfterSend = verify;

                SendReport rep = inj.Send(text, opt, new Action<string>(delegate(string s)
                {
                    Console.WriteLine("  " + s);
                }));

                Console.WriteLine(rep.Ok ? "OK" : "FAIL");
                return rep.Ok ? 0 : 1;
            }

            PrintHelp();
            return 2;
        }

        private static string OneLine(string s)
        {
            if (s == null) return "";
            return s.Replace("\r", "").Replace("\n", " ").Trim();
        }
    }
}
