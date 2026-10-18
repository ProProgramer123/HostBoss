using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("HostBoss Setup")]
[assembly: AssemblyDescription("Offline HostBoss and BepInEx setup for Scam With Your Friends")]
[assembly: AssemblyVersion("1.60.3.0")]
[assembly: AssemblyFileVersion("1.60.3.0")]

namespace HostBoss.Setup {
static class Program {
    [STAThread] static int Main(string[] args) {
        try {
            if (args.Length == 1 && args[0] == "--self-check") { Payload.Verify(); return 0; }
            if (args.Length == 1 && args[0] == "--ui-self-check") {
                Application.EnableVisualStyles();
                using (var form = new SetupForm()) { form.ShowInTaskbar=false; form.Opacity=0; form.Show(); if (!form.IsHandleCreated || form.Controls.Count < 6) throw new InvalidOperationException("Setup window did not initialize."); form.Close(); }
                return 0;
            }
            if (args.Length == 2 && args[0] == "--extract") { Payload.Extract(args[1]); return 0; }
            if (args.Length == 2 && (args[0] == "--install" || args[0] == "--mods-only")) return Installer.Run(args[1],args[0] == "--mods-only",Console.WriteLine);
            if (args.Length != 0) throw new ArgumentException("Usage: --self-check, --extract <new-folder>, --install <game-folder>, --mods-only <game-folder>");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new SetupForm()); return 0;
        } catch (Exception e) { if (args.Length > 0) Console.Error.WriteLine(e.ToString()); else MessageBox.Show(e.Message,"HostBoss Setup",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
    }
}
static class Payload {
    public static void Verify() {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HostBoss.Payload.zip"))
        using (var sha = SHA256.Create())
        using (var expected = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("HostBoss.Payload.sha256"))) {
            string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            if (!string.Equals(hash,expected.ReadToEnd().Trim(),StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Embedded setup checksum mismatch.");
        }
    }
    public static void Extract(string destination) {
        Verify();
        string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Directory.Exists(root) || File.Exists(destination)) throw new IOException("Choose a new, empty extraction path.");
        Directory.CreateDirectory(root);
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HostBoss.Payload.zip"))
        using (var zip = new ZipArchive(stream,ZipArchiveMode.Read)) foreach (var entry in zip.Entries) {
            string target = Path.GetFullPath(Path.Combine(root,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe setup archive path.");
            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using (var input = entry.Open()) using (var output = new FileStream(target,FileMode.CreateNew,FileAccess.Write)) input.CopyTo(output);
        }
    }
}
static class Installer {
    public static int Run(string game, bool modsOnly, Action<string> log) {
        game = Path.GetFullPath(game.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar);
        if (!File.Exists(Path.Combine(game,"Scam With Your Friends.exe"))) throw new DirectoryNotFoundException("Select the game folder containing Scam With Your Friends.exe.");
        if (Process.GetProcessesByName("Scam With Your Friends").Length != 0) throw new InvalidOperationException("Close the game before installing HostBoss.");
        string stage = Path.Combine(Path.GetTempPath(),"HostBoss-Setup-" + Guid.NewGuid().ToString("N"));
        string logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HostBoss","Setup","Logs");
        Directory.CreateDirectory(logs);
        string logPath = Path.Combine(logs,DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".log");
        log("Installation log: " + logPath);
        Payload.Extract(stage);
        try {
            using (var writer = new StreamWriter(logPath,false,new UTF8Encoding(false))) {
                Action<string> record = line => { if (line == null) return; lock(writer) { writer.WriteLine(line); writer.Flush(); log(line); } };
                string script = Path.Combine(stage,"Setup-HostBoss.ps1");
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"));
                start.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\" -GameDir \"" + game + "\"" + (modsOnly ? " -ModsOnly" : "");
                start.WorkingDirectory = stage; start.UseShellExecute = false; start.CreateNoWindow = true; start.RedirectStandardOutput = start.RedirectStandardError = true;
                using (var process = new Process {StartInfo=start}) {
                    process.OutputDataReceived += (s,e) => record(e.Data); process.ErrorDataReceived += (s,e) => record(e.Data);
                    process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); process.WaitForExit();
                    if (process.ExitCode != 0) throw new InvalidOperationException("Setup failed. See the installation log above. If access was denied, close this window and run HostBoss Setup as administrator.");
                }
            }
            return 0;
        } finally {
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string resolved = Path.GetFullPath(stage);
            if (resolved.StartsWith(tempRoot,StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("HostBoss-Setup-",StringComparison.Ordinal)) {
                try { Directory.Delete(resolved,true); } catch { log("Temporary setup files retained at: " + stage); }
            }
        }
    }
    public static string Detect() {
        var libraries = new List<string>();
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) { if (key != null && key.GetValue("SteamPath") is string) libraries.Add((string)key.GetValue("SteamPath")); }
        libraries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
        foreach (string library in libraries.ToArray()) {
            string vdf = Path.Combine(library,"steamapps","libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\"")) libraries.Add(match.Groups[1].Value.Replace("\\\\","\\"));
        }
        foreach (string library in libraries) { string game = Path.Combine(library,"steamapps","common","Scam With Your Friends"); if (File.Exists(Path.Combine(game,"Scam With Your Friends.exe"))) return game; }
        return "";
    }
}
sealed class SetupForm : Form {
    readonly TextBox folder = new TextBox(), output = new TextBox();
    readonly Button install = new Button(), browse = new Button();
    bool busy;
    public SetupForm() {
        Text = "HostBoss Setup 1.60.3"; ClientSize = new Size(760,540); MinimumSize = Size; StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI",10);
        var title = new Label {Text="Install HostBoss",Font=new Font("Segoe UI",22,FontStyle.Bold),AutoSize=true,Location=new Point(22,18)}; Controls.Add(title);
        var details = new Label {Text="Includes HostBoss, the shared library and BepInEx setup. Close the game first.\nWindows 10 / 11 x64. No separate launcher or download required.",Location=new Point(24,72),Size=new Size(710,48)}; Controls.Add(details);
        folder.SetBounds(24,136,590,29); folder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; folder.Text=Installer.Detect(); Controls.Add(folder);
        browse.Text="Browse…"; browse.SetBounds(626,133,110,33); browse.Anchor=AnchorStyles.Top|AnchorStyles.Right; browse.Click += delegate { using(var picker=new OpenFileDialog {Title="Select the game executable",Filter="Scam With Your Friends|Scam With Your Friends.exe"}) if(picker.ShowDialog(this)==DialogResult.OK) folder.Text=Path.GetDirectoryName(picker.FileName); }; Controls.Add(browse);
        install.Text="Install / Update"; install.SetBounds(24,184,712,42); install.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; install.Click += async delegate { await Install(); }; Controls.Add(install);
        output.Multiline=true; output.ReadOnly=true; output.ScrollBars=ScrollBars.Vertical; output.SetBounds(24,245,712,270); output.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; output.Text="Setup uses the official ScamWYF loader scripts. Existing files are backed up.\r\nSaved presentations are kept. Legacy disk patches must be removed first.\r\n"; Controls.Add(output);
        FormClosing += (s,e) => { if (busy) { e.Cancel=true; MessageBox.Show(this,"Wait for setup to finish before closing this window.","Setup running"); } };
    }
    void Log(string text) { if (InvokeRequired) { BeginInvoke(new Action<string>(Log),text); return; } output.AppendText(text + Environment.NewLine); }
    async Task Install() {
        string game=folder.Text; busy=true; install.Enabled=browse.Enabled=folder.Enabled=false;
        try { await Task.Run(() => Installer.Run(game,false,Log)); Log("Done. Start the game, host a lobby, and press F6."); MessageBox.Show(this,"HostBoss installed. Start the game and press F6.","Setup complete"); }
        catch(Exception e) { Log("ERROR: " + e.Message); MessageBox.Show(this,e.Message,"Setup failed",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        finally { busy=false; install.Enabled=browse.Enabled=folder.Enabled=true; }
    }
}
}
