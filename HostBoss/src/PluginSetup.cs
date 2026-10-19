using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("HostBoss Setup")]
[assembly: AssemblyDescription("HostBoss installer with inspectable on-disk setup files")]
[assembly: AssemblyVersion("1.60.4.0")]
[assembly: AssemblyFileVersion("1.60.4.0")]

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
    public static string Root { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SetupFiles"); } }
    public static void Verify() {
        string manifest=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SHA256SUMS.txt");
        if (!File.Exists(manifest)) throw new FileNotFoundException("Extract the entire setup ZIP and keep its files together.");
        foreach(string line in File.ReadAllLines(manifest)) {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Length < 67) throw new InvalidDataException("Invalid checksum manifest.");
            string target=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,line.Substring(66)));
            string root=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\') + "\\";
            if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe manifest path.");
            if (!File.Exists(target) || !string.Equals(Hash(target),line.Substring(0,64),StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Missing or changed setup file: " + line.Substring(66));
        }
    }
    public static string Hash(string file) { using(var input=File.OpenRead(file)) using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-",""); }
    public static void Extract(string destination) {
        Verify();
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Choose a new extraction path.");
        foreach(string file in Directory.GetFiles(Root,"*",SearchOption.AllDirectories)) {
            string target=Path.Combine(destination,file.Substring(Root.Length+1)); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file,target);
        }
    }
}
static class Installer {
    public static int Run(string game, bool modsOnly, Action<string> log) {
        game = Path.GetFullPath(game.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar);
        if (!File.Exists(Path.Combine(game,"Scam With Your Friends.exe"))) throw new DirectoryNotFoundException("Select the game folder containing Scam With Your Friends.exe.");
        if (Process.GetProcessesByName("Scam With Your Friends").Length != 0) throw new InvalidOperationException("Close the game before installing HostBoss.");
        string logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HostBoss","Setup","Logs");
        Directory.CreateDirectory(logs);
        string logPath = Path.Combine(logs,DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".log");
        log("Installation log: " + logPath);
        Payload.Verify();
            using (var writer = new StreamWriter(logPath,false,new UTF8Encoding(false))) {
                Action<string> record = line => { if (line == null) return; lock(writer) { writer.WriteLine(line); writer.Flush(); log(line); } };
                NativeInstall.Apply(game,modsOnly,record);
            }
            return 0;
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
        Text = "HostBoss Setup 1.60.4"; ClientSize = new Size(760,540); MinimumSize = Size; StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI",10);
        var title = new Label {Text="Install HostBoss",Font=new Font("Segoe UI",22,FontStyle.Bold),AutoSize=true,Location=new Point(22,18)}; Controls.Add(title);
        var details = new Label {Text="Installs HostBoss, its shared library and BepInEx. Close the game first.\nKeep the extracted SetupFiles folder beside this EXE. Windows 10 / 11 x64.",Location=new Point(24,72),Size=new Size(710,48)}; Controls.Add(details);
        folder.SetBounds(24,136,590,29); folder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; folder.Text=Installer.Detect(); Controls.Add(folder);
        browse.Text="Browse…"; browse.SetBounds(626,133,110,33); browse.Anchor=AnchorStyles.Top|AnchorStyles.Right; browse.Click += delegate { using(var picker=new OpenFileDialog {Title="Select the game executable",Filter="Scam With Your Friends|Scam With Your Friends.exe"}) if(picker.ShowDialog(this)==DialogResult.OK) folder.Text=Path.GetDirectoryName(picker.FileName); }; Controls.Add(browse);
        install.Text="Install / Update"; install.SetBounds(24,184,712,42); install.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; install.Click += async delegate { await Install(); }; Controls.Add(install);
        output.Multiline=true; output.ReadOnly=true; output.ScrollBars=ScrollBars.Vertical; output.SetBounds(24,245,712,270); output.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; output.Text="Setup uses the official C# loader implementation. No PowerShell is launched.\r\nExisting files are backed up. Saved presentations are kept.\r\nRemove legacy disk patches before installing.\r\n"; Controls.Add(output);
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
