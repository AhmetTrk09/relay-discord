using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Relay
{
    public sealed class HostsManager
    {
        internal const string StartMarker = "# [Relay-Start]";
        internal const string EndMarker = "# [Relay-End]";
        private static readonly string[,] Markers = {
            { StartMarker, EndMarker },
            { "# [Rota-Start]", "# [Rota-End]" },
            { "# [DiscordShield-Start]", "# [DiscordShield-End]" },
            { "# >>> DISCORD-SHIELD-START", "# <<< DISCORD-SHIELD-END" }
        };
        public static readonly HostsManager Default = new HostsManager(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts"), FlushSystemDns);
        private readonly string _path;
        private readonly Action _flushDns;
        public string LastError { get; private set; }

        public HostsManager(string path, Action flushDns) { _path = path; _flushDns = flushDns; }
        public bool Apply(bool routeToLocalProxy) { return Update(delegate(string content) { return AddEntries(content, routeToLocalProxy); }); }
        public bool Remove() { return Update(RemoveEntries); }

        private bool Update(Func<string, string> transform)
        {
            LastError = null;
            try
            {
                byte[] original = File.Exists(_path) ? File.ReadAllBytes(_path) : new byte[0];
                Encoding encoding;
                string content;
                using (MemoryStream stream = new MemoryStream(original))
                using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true), true))
                {
                    try { content = reader.ReadToEnd(); encoding = reader.CurrentEncoding; }
                    catch (DecoderFallbackException) { encoding = Encoding.Default; content = encoding.GetString(original); }
                }
                string updated = transform(content);
                if (updated != content)
                {
                    bool hasBom = original.Length >= 3 && original[0] == 0xef && original[1] == 0xbb && original[2] == 0xbf;
                    if (encoding.CodePage == Encoding.UTF8.CodePage) encoding = new UTF8Encoding(hasBom);
                    File.WriteAllText(_path, updated, encoding);
                    FlushDns();
                }
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        internal static string AddEntries(string content, bool routeToLocalProxy)
        {
            string cleaned = RemoveEntries(content);
            string newline = content.Contains("\r\n") ? "\r\n" : "\n";
            StringBuilder result = new StringBuilder(cleaned);
            if (result.Length > 0 && result[result.Length - 1] != '\n') result.Append(newline);
            result.Append(StartMarker).Append(newline);
            foreach (KeyValuePair<string, string> entry in TlsSplitProxy.ProxyHostMap)
                result.Append(routeToLocalProxy ? "127.0.0.1" : entry.Value).Append(' ').Append(entry.Key).Append(newline);
            foreach (KeyValuePair<string, string> entry in TlsSplitProxy.DirectHostMap)
                result.Append(entry.Value).Append(' ').Append(entry.Key).Append(newline);
            result.Append(EndMarker).Append(newline);
            return result.ToString();
        }

        internal static string RemoveEntries(string content)
        {
            for (int i = 0; i < Markers.GetLength(0); i++)
            {
                string startTag = Markers[i, 0], endTag = Markers[i, 1];
                int start;
                while ((start = FindMarker(content, startTag, 0)) >= 0)
                {
                    int end = FindMarker(content, endTag, start + startTag.Length);
                    if (end < 0) throw new InvalidDataException("hosts dosyasında tamamlanmamış bir Relay kaydı var. Dosya değiştirilmedi.");
                    end += endTag.Length;
                    if (end < content.Length && content[end] == '\r') end++;
                    if (end < content.Length && content[end] == '\n') end++;
                    content = content.Remove(start, end - start);
                }
            }
            return content;
        }

        private static int FindMarker(string content, string marker, int offset)
        {
            int index = offset;
            while ((index = content.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
            {
                int after = index + marker.Length;
                if ((index == 0 || content[index - 1] == '\n') &&
                    (after == content.Length || content[after] == '\r' || content[after] == '\n')) return index;
                index = after;
            }
            return -1;
        }

        public void FlushDns() { if (_flushDns != null) _flushDns(); }
        private static void FlushSystemDns()
        {
            try
            {
                using (Process process = Process.Start(new ProcessStartInfo("ipconfig.exe", "/flushdns") {
                    CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden }))
                { if (process != null && !process.WaitForExit(3000)) process.Kill(); }
            }
            catch (Exception) { /* The hosts restore remains valid even if the cache flush fails. */ }
        }
    }
}
