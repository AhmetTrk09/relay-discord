using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Xml;

namespace Relay
{
    internal static class StartupManager
    {
        internal static string LastError { get; private set; }
        private static string TaskName { get { return "Relay-" + WindowsIdentity.GetCurrent().User.Value; } }
        // Task created by releases published under the previous name.
        private static string LegacyTaskName { get { return "Rota-" + WindowsIdentity.GetCurrent().User.Value; } }
        public static bool IsEnabled()
        {
            try
            {
                string xml;
                if (!Run("/Query /TN \"" + TaskName + "\" /XML", out xml)) return false;
                XmlDocument document = new XmlDocument();
                document.XmlResolver = null;
                document.LoadXml(xml);
                XmlNamespaceManager namespaces = new XmlNamespaceManager(document.NameTable);
                namespaces.AddNamespace("t", "http://schemas.microsoft.com/windows/2004/02/mit/task");
                XmlNode enabled = document.SelectSingleNode("//t:Settings/t:Enabled", namespaces);
                return enabled == null || enabled.InnerText != "false";
            }
            catch (Exception) { return false; }
        }
        public static bool SetEnabled(bool enabled)
        {
            LastError = null;
            string temporary = null;
            try
            {
                string output;
                Run("/Delete /TN \"" + LegacyTaskName + "\" /F", out output);
                LastError = null;
                if (!enabled && !IsEnabled()) return true;
                if (!enabled) return Run("/Delete /TN \"" + TaskName + "\" /F", out output);
                temporary = Path.GetTempFileName();
                File.WriteAllText(temporary, BuildTaskXml(Process.GetCurrentProcess().MainModule.FileName, WindowsIdentity.GetCurrent().User.Value), Encoding.Unicode);
                return Run("/Create /TN \"" + TaskName + "\" /XML \"" + temporary + "\" /F", out output);
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
            finally { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static string BuildTaskXml(string executable, string userSid)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                "<RegistrationInfo><Description>Relay Discord bağlantı aracını oturum açıldığında başlatır.</Description></RegistrationInfo>" +
                "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + SecurityElement.Escape(userSid) + "</UserId></LogonTrigger></Triggers>" +
                "<Principals><Principal id=\"Author\"><UserId>" + SecurityElement.Escape(userSid) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>" +
                "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Enabled>true</Enabled></Settings>" +
                "<Actions Context=\"Author\"><Exec><Command>" + SecurityElement.Escape(executable) + "</Command><Arguments>--minimized</Arguments><WorkingDirectory>" + SecurityElement.Escape(Path.GetDirectoryName(executable)) + "</WorkingDirectory></Exec></Actions></Task>";
        }
        private static bool Run(string arguments, out string output)
        {
            using (Process process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Unicode, WindowStyle = ProcessWindowStyle.Hidden }))
            {
                // /XML writes UTF-16; status text is not used for the UI.
                System.Threading.Tasks.Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                System.Threading.Tasks.Task<string> errorTask = process.StandardError.ReadToEndAsync();
                output = "";
                if (!process.WaitForExit(5000)) { process.Kill(); LastError = "Görev Zamanlayıcı yanıt vermedi."; return false; }
                output = outputTask.Result;
                string error = errorTask.Result;
                if (process.ExitCode != 0) { LastError = error.Trim(); return false; }
                return true;
            }
        }
    }
}
