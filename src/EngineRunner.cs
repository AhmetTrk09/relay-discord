using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Relay
{
    public sealed class EngineProfile
    {
        public string Name { get; private set; }
        public string GoodbyeDpiArgs { get; private set; }
        public bool EnableZapretVoice { get; private set; }
        public bool EnableTlsSplitProxy { get; private set; }
        public EngineProfile(string name, string args, bool voice, bool tls) { Name = name; GoodbyeDpiArgs = args; EnableZapretVoice = voice; EnableTlsSplitProxy = tls; }
        public override string ToString() { return Name; }
    }

    public sealed class EngineRunner : IDisposable
    {
        private readonly object _sync = new object();
        private readonly string _baseDir, _gdpiDir, _gdpiExe, _zapretDir, _zapretExe, _domainList;
        private readonly HostsManager _hosts;
        private readonly TlsSplitProxy _tlsProxy = new TlsSplitProxy();
        private Process _gdpiProc, _zapretProc;
        private ProcessGroup _processGroup;
        private volatile bool _isRunning;
        private bool _hostsApplied, _voiceRequired, _tlsRequired, _disposed;
        public event Action<string> LogMessage;
        public bool IsRunning { get { return _isRunning; } }
        public bool IsTcpRunning { get { return IsAlive(_gdpiProc); } }
        public bool IsVoiceRunning { get { return IsAlive(_zapretProc); } }
        public bool HostsApplied { get { return _hostsApplied; } }
        public string LastError { get; private set; }
        public TlsSplitProxy TlsProxy { get { return _tlsProxy; } }

        public static readonly EngineProfile[] Profiles = {
            new EngineProfile("Standart", "-5 --dns-addr 77.88.8.8 --dns-port 1253", true, true),
            new EngineProfile("Alternatif TCP", "-6 --dns-addr 77.88.8.8 --dns-port 1253", true, true),
            new EngineProfile("Agresif", "-9 --dns-addr 77.88.8.8 --dns-port 1253", true, true),
            new EngineProfile("Düşük TTL", "-5 --set-ttl 3 --dns-addr 77.88.8.8 --dns-port 1253", true, true)
        };

        public EngineRunner() : this(AppDomain.CurrentDomain.BaseDirectory, HostsManager.Default) { }
        public EngineRunner(string baseDirectory, HostsManager hosts)
        {
            _baseDir = Path.GetFullPath(baseDirectory);
            _gdpiDir = Path.Combine(_baseDir, @"bin\goodbyedpi");
            _gdpiExe = Path.Combine(_gdpiDir, "goodbyedpi.exe");
            _zapretDir = Path.Combine(_baseDir, @"bin\zapret");
            _zapretExe = Path.Combine(_zapretDir, "winws.exe");
            _domainList = Path.Combine(_baseDir, @"lists\discord_domains.txt");
            _hosts = hosts;
            _tlsProxy.LogMessage += EmitLog;
        }

        public bool Start(EngineProfile profile, bool enableTlsAndDns, bool useDomainList)
        {
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException("EngineRunner");
                StopCore();
                LastError = null;
                try
                {
                    if (_hostsApplied) throw new IOException("Önceki hosts kayıtları temizlenemedi: " + _hosts.LastError);
                    if (profile == null) throw new ArgumentNullException("profile");
                    RequireFile(_gdpiExe);
                    RequireFile(Path.Combine(_gdpiDir, "WinDivert.dll"));
                    RequireFile(Path.Combine(_gdpiDir, "WinDivert64.sys"));
                    if (useDomainList) RequireFile(_domainList);
                    _voiceRequired = profile.EnableZapretVoice;
                    _tlsRequired = enableTlsAndDns && profile.EnableTlsSplitProxy;
                    if (_voiceRequired)
                        foreach (string file in new[] { "winws.exe", "cygwin1.dll", "WinDivert.dll", "WinDivert64.sys", "ACTIVE_DISCORD_UDP.bin", "quic_initial_www_google_com.bin", "tls_clienthello_www_google_com.bin" })
                            RequireFile(Path.Combine(_zapretDir, file));

                    _processGroup = new ProcessGroup();
                    string args = profile.GoodbyeDpiArgs + (useDomainList ? " --blacklist \"" + _domainList + "\"" : "");
                    _gdpiProc = StartOwnedProcess(_gdpiExe, args, _gdpiDir);
                    if (_voiceRequired) _zapretProc = StartOwnedProcess(_zapretExe, BuildZapretVoiceArgs(useDomainList), _zapretDir);
                    if (_tlsRequired && !_tlsProxy.Start()) throw new IOException("127.0.0.1:443 açılamadı. Bu portu kullanan başka bir uygulama olabilir.");
                    if (enableTlsAndDns)
                    {
                        _hostsApplied = true;
                        if (!_hosts.Apply(_tlsRequired)) throw new IOException("hosts dosyası güncellenemedi: " + _hosts.LastError);
                    }
                    _isRunning = true;
                    EmitLog("Relay hazır. Profil: " + profile.Name + ". Discord bağlantısını Erişim testi ile deneyebilirsin.");
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    EmitLog("Başlatılamadı: " + LastError);
                    StopCore();
                    return false;
                }
            }
        }

        public void CheckHealth()
        {
            lock (_sync)
            {
                if (_isRunning && (!IsTcpRunning || (_voiceRequired && !IsVoiceRunning) || (_tlsRequired && !_tlsProxy.IsRunning)))
                {
                    LastError = "Ağ araçlarından biri durdu. Ayrıntılar için günlüğe bakabilirsin.";
                    EmitLog(LastError);
                    StopCore();
                }
            }
        }

        public void Stop() { lock (_sync) { StopCore(); } }
        private void StopCore()
        {
            _isRunning = false;
            _tlsProxy.Stop();
            StopOwnedProcess(ref _zapretProc);
            StopOwnedProcess(ref _gdpiProc);
            if (_processGroup != null) { _processGroup.Dispose(); _processGroup = null; }
            if (_hostsApplied)
            {
                if (_hosts.Remove()) _hostsApplied = false;
                else { LastError = "hosts kayıtları temizlenemedi: " + _hosts.LastError; EmitLog(LastError); }
            }
        }

        private static void RequireFile(string path) { if (!File.Exists(path)) throw new FileNotFoundException("Eksik dosya: " + path); }
        private Process StartOwnedProcess(string path, string args, string workingDirectory)
        {
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(path, args) {
                WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true
            };
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (!string.IsNullOrWhiteSpace(e.Data)) EmitLog(Path.GetFileName(path) + ": " + e.Data); };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (!string.IsNullOrWhiteSpace(e.Data)) EmitLog(Path.GetFileName(path) + ": " + e.Data); };
            try
            {
                if (!process.Start()) throw new IOException(Path.GetFileName(path) + " başlatılamadı.");
                _processGroup.Add(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (process.WaitForExit(700)) throw new IOException(Path.GetFileName(path) + " kapandı (kod " + process.ExitCode + ").");
                EmitLog(Path.GetFileName(path) + " çalışıyor (PID " + process.Id + ").");
                return process;
            }
            catch { StopOwnedProcess(ref process); throw; }
        }
        private static bool IsAlive(Process process) { try { return process != null && !process.HasExited; } catch (InvalidOperationException) { return false; } }
        private static void StopOwnedProcess(ref Process process)
        {
            if (process == null) return;
            try { if (!process.HasExited) { process.Kill(); process.WaitForExit(2000); } }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally { process.Dispose(); process = null; }
        }

        private string BuildZapretVoiceArgs(bool useDomainList)
        {
            string hostlist = useDomainList ? "--hostlist=\"" + _domainList + "\" " : "";
            return "--wf-tcp=2053,2083,2087,2096,8443 --wf-udp=443,3478,19294-19344,50000-65535 " +
                "--filter-udp=3478,19294-19344,50000-65535 --filter-l7=discord,stun --dpi-desync=fake --dpi-desync-repeats=4 --dpi-desync-autottl=2 " +
                "--dpi-desync-fake-discord=ACTIVE_DISCORD_UDP.bin --dpi-desync-fake-stun=quic_initial_www_google_com.bin --new " +
                "--filter-udp=443 " + hostlist + "--dpi-desync=fake --dpi-desync-repeats=6 --dpi-desync-fake-quic=quic_initial_www_google_com.bin --new " +
                "--filter-tcp=2053,2083,2087,2096,8443 --hostlist-domains=discord.media --dpi-desync=multisplit " +
                "--dpi-desync-split-seqovl=681 --dpi-desync-split-pos=1 --dpi-desync-split-seqovl-pattern=tls_clienthello_www_google_com.bin";
        }

        internal static bool IsDiscordClientPath(string executablePath, string localAppData)
        {
            if (string.IsNullOrEmpty(executablePath)) return false;
            string fullPath = Path.GetFullPath(executablePath);
            string name = Path.GetFileName(fullPath);
            foreach (string client in new[] { "Discord", "DiscordPTB", "DiscordCanary" })
            {
                string root = Path.GetFullPath(Path.Combine(localAppData, client)) + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, "Update.exe", StringComparison.OrdinalIgnoreCase))
                    return string.Equals(fullPath, Path.Combine(root, "Update.exe"), StringComparison.OrdinalIgnoreCase);
                return string.Equals(name, client + ".exe", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public void RestartDiscordClient()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            EmitLog("Discord istemcisi yeniden başlatılıyor...");
            foreach (string name in new[] { "Discord", "DiscordPTB", "DiscordCanary", "Update" })
            {
                foreach (Process process in Process.GetProcessesByName(name))
                using (process)
                {
                    try
                    {
                        if (IsDiscordClientPath(process.MainModule.FileName, local)) { process.Kill(); process.WaitForExit(2000); }
                    }
                    catch (Exception ex) { EmitLog("Bir süreç kapatılamadı: " + ex.Message); }
                }
            }
            _hosts.FlushDns();
            foreach (string client in new[] { "Discord", "DiscordPTB", "DiscordCanary" })
            {
                string update = Path.Combine(local, client, "Update.exe");
                if (!File.Exists(update)) continue;
                try { Process.Start(new ProcessStartInfo(update, "--processStart " + client + ".exe") { UseShellExecute = true }); return; }
                catch (Exception ex) { EmitLog("Discord açılamadı: " + ex.Message); }
            }
            try { Process.Start(new ProcessStartInfo("discord:") { UseShellExecute = true }); }
            catch (Exception ex) { EmitLog("Discord açılamadı: " + ex.Message); }
        }

        public static bool IsStartupEnabled() { return StartupManager.IsEnabled(); }
        public static bool SetStartupEnabled(bool enabled) { return StartupManager.SetEnabled(enabled); }
        private void EmitLog(string message) { Action<string> handler = LogMessage; if (handler != null) handler(message); }
        public void Dispose() { lock (_sync) { if (!_disposed) { StopCore(); _disposed = true; } } }
    }
}
