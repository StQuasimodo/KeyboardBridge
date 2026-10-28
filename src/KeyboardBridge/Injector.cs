using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace KeyboardBridge
{
    public class SendOptions
    {
        /// <summary>单次注入承载的最大字符数，超出后分段发送。</summary>
        public int MaxChunk = 1200;
        /// <summary>发送后是否回读界面校验（只在内存比对，校验文件用完即删）。</summary>
        public bool VerifyAfterSend = true;
        /// <summary>
        /// 是否允许把英文双引号自动替换为中文引号。
        /// 关闭时，正文含 " 会被拒绝发送（见 Send 中的说明）。
        /// </summary>
        public bool ReplaceDoubleQuotes = false;
    }

    public class SendReport
    {
        public bool Ok;
        public bool VerifyAttempted;
        public bool? Verified;
        public int Chunks;
        public int Lines;
        public bool QuotesReplaced;
        public string Message = "";

        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Ok ? "已发送" : "发送失败");
            sb.Append("（").Append(Chunks).Append(" 段 / ").Append(Lines).Append(" 行）");
            if (VerifyAttempted)
            {
                if (Verified == true) sb.Append("，回读校验通过");
                else if (Verified == false) sb.Append("，回读校验未通过");
                else sb.Append("，未校验（文本含控制字符）");
            }
            if (QuotesReplaced) sb.Append("，英文引号已转为中文引号");
            if (!string.IsNullOrEmpty(Message)) sb.Append("：").Append(Message);
            return sb.ToString();
        }
    }

    /// <summary>
    /// 文本注入。
    ///
    /// 传输方式：正文不拼进命令行，而是先写成本地 UTF-8 文件、用 hdc file send 送到
    /// 设备 /data/local/tmp/kb_in.txt，再由设备端的固定脚本 `uitest uiInput text "$(cat ...)"`
    /// 读取。命令行走的是固定的 ASCII 字符串。
    ///
    /// 这样做是因为实测发现：直接把正文拼进 hdc shell 的命令行时，双引号会在设备端
    /// 外壳里被二次解释（形如 A"B 的文本会多出引号或丢失），而文件通道对引号、反斜杠、
    /// $、反引号、单引号、连续空格全部原样保留。
    /// </summary>
    public class Injector
    {
        private readonly HdcClient _hdc;

        private const string RemoteInbox = "/data/local/tmp/kb_in.txt";
        private const string RemoteHelper = "/data/local/tmp/kb_send.sh";

        private bool _helperReady;

        public Injector(HdcClient hdc)
        {
            _hdc = hdc;
            _helperReady = false;
        }

        public HdcClient Client
        {
            get { return _hdc; }
        }

        /// <summary>按 sh 的规则用单引号包裹（仅用于固定命令，不用于正文）。</summary>
        public static string ShellQuote(string text)
        {
            if (text == null) text = "";
            return "'" + text.Replace("'", "'\\''") + "'";
        }

        /// <summary>把正文切成若干段，优先在换行处断开，避免超长参数。</summary>
        public static List<string> ChunkText(string text, int max)
        {
            List<string> parts = new List<string>();
            if (string.IsNullOrEmpty(text)) return parts;
            if (max < 1) max = 1;

            int start = 0;
            while (start < text.Length)
            {
                int len = Math.Min(max, text.Length - start);
                if (start + len < text.Length)
                {
                    int nl = text.LastIndexOf('\n', start + len - 1, len);
                    if (nl > start) len = nl - start + 1;
                }
                // 不要把代理对拆开
                if (start + len < text.Length && char.IsHighSurrogate(text[start + len - 1])) len--;
                if (len <= 0) len = 1;
                parts.Add(text.Substring(start, len));
                start += len;
            }
            return parts;
        }

        public static int CountLines(string text)
        {
            int n = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') n++;
            return n;
        }

        /// <summary>判断一次 hdc 调用是否视为成功。uitest 成功时打印 "No Error"。</summary>
        public static bool IsSuccess(HdcResult r)
        {
            if (r == null || !r.Ok) return false;
            string a = r.All;
            if (a.Length == 0) return true;
            if (a.IndexOf("No Error", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            string low = a.ToLowerInvariant();
            if (low.Contains("[fail]")) return false;
            if (low.Contains("invalid")) return false;
            if (low.Contains("not found")) return false;
            if (low.Contains("not exist")) return false;
            if (low.Contains("error")) return false;
            return true;
        }

        private static string Describe(HdcResult r)
        {
            if (r == null) return "未知错误";
            if (r.TimedOut) return "命令超时。文本可能已写入，请先核对手机再决定是否重发。";
            string s = r.All;
            if (s.Length > 300) s = s.Substring(0, 300) + "…";
            return s.Replace("\r", " ").Replace("\n", " ");
        }

        /// <summary>在设备上准备读取脚本（只需一次）。</summary>
        private bool EnsureHelper(Action<string> log)
        {
            if (_helperReady) return true;

            HdcResult ls = _hdc.Shell("ls " + RemoteHelper);
            if (IsSuccess(ls) && ls.All.IndexOf(RemoteHelper, StringComparison.Ordinal) >= 0)
            {
                _helperReady = true;
                return true;
            }

            string local = Path.Combine(Path.GetTempPath(), "kb_send_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".sh");
            try
            {
                // 用 "$$" 占位避免 C# 字符串插值歧义；实际写入 $(cat ...)
                string script = "uitest uiInput text \"$(cat " + RemoteInbox + ")\"\n";
                File.WriteAllText(local, script, new UTF8Encoding(false));

                HdcResult send = _hdc.Run(new string[] { "file", "send", local, RemoteHelper });
                if (!send.Ok)
                {
                    if (log != null) log("准备读取脚本失败：" + Describe(send));
                    return false;
                }
                _helperReady = true;
                return true;
            }
            finally
            {
                try { if (File.Exists(local)) File.Delete(local); } catch { }
            }
        }

        /// <summary>把一段文本送到设备并注入。</summary>
        private HdcResult InjectChunk(string chunk, Action<string> log)
        {
            string local = Path.Combine(Path.GetTempPath(), "kb_in_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt");
            try
            {
                File.WriteAllText(local, chunk, new UTF8Encoding(false));

                HdcResult send = _hdc.Run(new string[] { "file", "send", local, RemoteInbox });
                if (!send.Ok) return send;

                return _hdc.Shell("sh " + RemoteHelper);
            }
            finally
            {
                try { if (File.Exists(local)) File.Delete(local); } catch { }
            }
        }

        public SendReport Send(string text, SendOptions opt, Action<string> log)
        {
            SendReport rep = new SendReport();
            if (opt == null) opt = new SendOptions();

            if (text == null || text.Trim().Length == 0)
            {
                rep.Message = "没有内容可发送";
                return rep;
            }

            // uitest (7.0.0.1) 实测缺陷：正文里每个 ASCII 双引号 " 都会在注入结果的
            // 末尾被多复制一份（A"B -> A"B"）。用 \" 或 \\" 都绕不过去。
            // 按"不悄悄改字"原则：默认拒绝发送并说明原因，由用户决定是否转换为中文引号。
            if (text.IndexOf('"') >= 0)
            {
                if (!opt.ReplaceDoubleQuotes)
                {
                    rep.Message = "正文包含英文双引号（ASCII 0x22），当前注入通道会把每个引号在末尾重复一次，" +
                                  "无法原样写入。请改用中文引号「“ ”」，或勾选「自动把英文引号转为中文引号」。";
                    return rep;
                }
                text = NormalizeDoubleQuotes(text);
                rep.QuotesReplaced = true;
            }

            rep.Lines = CountLines(text);

            if (!EnsureHelper(log))
            {
                rep.Message = "无法在手机上准备读取脚本，发送中止。";
                return rep;
            }

            List<string> chunks = ChunkText(text, opt.MaxChunk);
            string lastChunk = "";

            for (int i = 0; i < chunks.Count; i++)
            {
                HdcResult r = InjectChunk(chunks[i], log);
                if (!IsSuccess(r))
                {
                    rep.Chunks = i;
                    rep.Message = "第 " + (i + 1) + " 段失败：" + Describe(r);
                    return rep;
                }
                rep.Chunks = i + 1;
                lastChunk = chunks[i];
            }

            rep.Ok = true;

            if (opt.VerifyAfterSend)
            {
                rep.VerifyAttempted = true;
                try
                {
                    rep.Verified = VerifyTail(lastChunk, log);
                }
                catch (Exception ex)
                {
                    rep.Verified = null;
                    if (log != null) log("校验异常（不影响已发送内容）：" + ex.Message);
                }
            }

            // 发送完成后立即清除设备上的中间文件（其中含正文）
            PurgeInbox();

            if (log != null) log(rep.Describe());
            return rep;
        }

        /// <summary>删除设备端与本机的正文中间文件。</summary>
        public void PurgeInbox()
        {
            try { _hdc.Shell("rm -f " + RemoteInbox); } catch { }
            try
            {
                string[] olds = Directory.GetFiles(Path.GetTempPath(), "kb_in_*.txt");
                for (int i = 0; i < olds.Length; i++)
                {
                    try { File.Delete(olds[i]); } catch { }
                }
            }
            catch { }
        }

        /// <summary>把英文双引号按出现顺序交替替换为中文左右引号（U+201C / U+201D）。</summary>
        public static string NormalizeDoubleQuotes(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            bool open = true;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '"')
                {
                    sb.Append(open ? '\u201C' : '\u201D');
                    open = !open;
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }

        /// <summary>取一段用于回读比对的后缀；含控制字符时放弃校验。</summary>
        public static string PickVerifyTail(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c < ' ' || c == 0x7f) return null;
            }
            string tail = s.TrimEnd();
            if (tail.Length == 0) return null;
            if (tail.Length > 12) tail = tail.Substring(tail.Length - 12);
            if (tail.Length < 2) return null;
            return tail;
        }

        /// <summary>按 JSON 字符串规则转义，用于在 dumpLayout 结果里匹配。</summary>
        public static string JsonEscape(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private bool? VerifyTail(string lastChunk, Action<string> log)
        {
            string tail = PickVerifyTail(lastChunk);
            if (tail == null) return null;

            string tag = Guid.NewGuid().ToString("N").Substring(0, 8);
            string remote = "/data/local/tmp/kb_v" + tag + ".json";
            string local = Path.Combine(Path.GetTempPath(), "kb_v" + tag + ".json");

            try
            {
                HdcResult dump = _hdc.Shell("uitest dumpLayout -p " + remote);
                if (!IsSuccess(dump)) return null;

                HdcResult recv = _hdc.Run(new string[] { "file", "recv", remote, local });
                if (!recv.Ok || !File.Exists(local)) return null;

                string json = File.ReadAllText(local, Encoding.UTF8);
                if (json.IndexOf(tail, StringComparison.Ordinal) >= 0) return true;
                string jsonTail = JsonEscape(tail);
                if (jsonTail != tail && json.IndexOf(jsonTail, StringComparison.Ordinal) >= 0) return true;
                return false;
            }
            finally
            {
                try { if (File.Exists(local)) File.Delete(local); } catch { }
                try { _hdc.Shell("rm -f " + remote); } catch { }
            }
        }

        /// <summary>清理上次异常退出可能残留的中间文件。</summary>
        public void CleanupStale()
        {
            try { _hdc.Shell("rm -f /data/local/tmp/kb_*.json " + RemoteInbox); } catch { }
            try
            {
                string[] patterns = new string[] { "kb_*.json", "kb_in_*.txt", "kb_send_*.sh" };
                for (int p = 0; p < patterns.Length; p++)
                {
                    string[] olds = Directory.GetFiles(Path.GetTempPath(), patterns[p]);
                    for (int i = 0; i < olds.Length; i++)
                    {
                        try { File.Delete(olds[i]); } catch { }
                    }
                }
            }
            catch { }
            _helperReady = false;
        }

        /// <summary>读取前台应用包名（用于发送前确认焦点仍在目标 App）。失败返回 null。</summary>
        public string GetForegroundBundle()
        {
            HdcResult r = _hdc.Shell("aa dump -l");
            if (!IsSuccess(r)) return null;
            Match m = Regex.Match(r.All, @"bundleName[:\s]*([A-Za-z0-9_\.]+)");
            if (m.Success) return m.Groups[1].Value;
            return null;
        }
    }
}
