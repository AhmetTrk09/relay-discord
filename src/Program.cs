using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Windows;

namespace Relay
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool minimized = Array.Exists(args, delegate(string arg) {
                return string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase) || arg == "-m";
            });
            bool cleanup = Array.Exists(args, delegate(string arg) { return arg == "--uninstall-cleanup"; });
            if (!IsAdministrator())
            {
                try
                {
                    Process.Start(new ProcessStartInfo {
                        FileName = Process.GetCurrentProcess().MainModule.FileName,
                        Arguments = cleanup ? "--uninstall-cleanup" : minimized ? "--minimized" : "",
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        Verb = "runas", UseShellExecute = true
                    });
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    MessageBox.Show("Relay'in ağ araçlarını çalıştırmak için yönetici izni gerekiyor.", "Relay", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }
            bool created;
            using (Mutex mutex = new Mutex(true, @"Local\Relay_Discord_SingleInstance", out created))
            {
                if (!created)
                {
                    if (cleanup) { Environment.ExitCode = 2; return; }
                    MessageBox.Show("Relay zaten açık. Saatin yanındaki Relay simgesinden pencereyi açabilirsin.", "Relay", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (cleanup)
                {
                    bool hostsRemoved = HostsManager.Default.Remove();
                    bool startupRemoved = StartupManager.SetEnabled(false);
                    Environment.ExitCode = hostsRemoved && startupRemoved ? 0 : 1;
                    return;
                }
                MainWindow window = new MainWindow(minimized);
                Application app = new Application();
                try { app.Run(window); }
                finally { window.ShutdownEngine(); }
            }
        }
        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
