using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace KeyboardBridge
{
    /// <summary>一次 hdc 调用的结果。</summary>
    public class HdcResult
    {
        public int ExitCode = -1;
        public string Out = "";
        public string Err = "";
        public bool TimedOut = false;

        public bool Ok
        {
            get { return !TimedOut && ExitCode == 0; }
        }

        public string All
        {
            get { return (Out + "\n" + Err).Trim(); }
        }
    }

    /// <summary>
    /// hdc 命令行封装。所有与手机之间的交互都经过这里，
    /// 便于在零售机限制 uitest 时替换实现。
    /// </summary>
    public class HdcClient
    {
        public string HdcPath;
        public int TimeoutMs = 20000;

        public HdcClient(string hdcPath)
        {
            HdcPath = hdcPath;
        }

        /// <summary>按优先级自动寻找 hdc.exe。</summary>
        public static string AutoDetect()
        {
            string env = Environment.GetEnvironmentVariable("KB_HDC");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;

            // PATH
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                string[] dirs = path.Split(';');
                for (int i = 0; i < dirs.Length; i++)
                {
                    string d = dirs[i].Trim();
                    if (d.Length == 0) continue;
                    try
                    {
                        string candidate = Path.Combine(d, "hdc.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch { }
                }
            }

            // DevEco Studio 常见位置
            string[] known = new string[]
            {
                @"D:\Program Files\DevEco Studio\sdk\default\openharmony\toolchains\hdc.exe",
                @"C:\Program Files\DevEco Studio\sdk\default\openharmony\toolchains\hdc.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Huawei\Sdk\openharmony\toolchains\hdc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Huawei\DevEco Studio\sdk\default\openharmony\toolchains\hdc.exe")
            };
            for (int i = 0; i < known.Length; i++)
            {
                if (File.Exists(known[i])) return known[i];
            }
            return "";
        }

        /// <summary>Windows 命令行参数转义（遵循 CreateProcess 的解析规则）。</summary>
        public static string Quote(string s)
        {
            if (s == null) return "\"\"";
            if (s.Length == 0) return "\"\"";
            bool need = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '"' || c == '\n' || c == '\v') { need = true; break; }
            }
            if (!need) return s;

            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            int backslashes = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\') { backslashes++; continue; }
                if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                    continue;
                }
                if (backslashes > 0) { sb.Append('\\', backslashes); backslashes = 0; }
                sb.Append(c);
            }
            if (backslashes > 0) sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        public HdcResult Run(string[] args)
        {
            HdcResult r = new HdcResult();
            if (string.IsNullOrEmpty(HdcPath) || !File.Exists(HdcPath))
            {
                r.Err = "找不到 hdc.exe，请检查 DevEco Studio 安装或设置 KB_HDC 环境变量。";
                return r;
            }

            StringBuilder argLine = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) argLine.Append(' ');
                argLine.Append(Quote(args[i]));
            }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = HdcPath;
            psi.Arguments = argLine.ToString();
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            Process p = null;
            try
            {
                p = Process.Start(psi);
                // 并行读取两个流，避免任一管道写满导致死锁
                System.Threading.Tasks.Task<string> outTask = p.StandardOutput.ReadToEndAsync();
                System.Threading.Tasks.Task<string> errTask = p.StandardError.ReadToEndAsync();

                if (!p.WaitForExit(TimeoutMs))
                {
                    r.TimedOut = true;
                    try { p.Kill(); } catch { }
                    try { p.WaitForExit(3000); } catch { }
                }

                try
                {
                    if (outTask.Wait(5000)) r.Out = outTask.Result;
                }
                catch { r.Out = ""; }
                try
                {
                    if (errTask.Wait(5000)) r.Err = errTask.Result;
                }
                catch { r.Err = ""; }

                if (!r.TimedOut) r.ExitCode = p.ExitCode;
            }
            catch (Exception ex)
            {
                r.Err = "启动 hdc 失败: " + ex.Message;
            }
            finally
            {
                if (p != null) { try { p.Dispose(); } catch { } }
            }
            return r;
        }

        public HdcResult Shell(string command)
        {
            return Run(new string[] { "shell", command });
        }

        /// <summary>返回当前连接的设备标识（未脱敏，仅内部使用）。</summary>
        public List<string> ListTargets()
        {
            List<string> list = new List<string>();
            HdcResult r = Run(new string[] { "list", "targets" });
            if (!r.Ok) return list;
            string[] lines = r.Out.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i].Trim();
                if (s.Length == 0) continue;
                if (s.IndexOf("[Empty]", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (s.IndexOf("[Fail]", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                list.Add(s);
            }
            return list;
        }

        /// <summary>把设备标识脱敏后展示，避免截图/日志泄露。</summary>
        public static string Mask(string target)
        {
            if (string.IsNullOrEmpty(target)) return "";
            if (target.Length <= 8) return target;
            return target.Substring(0, 4) + "***";
        }
    }
}
