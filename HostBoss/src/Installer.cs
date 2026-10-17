using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("HostBoss Patcher")]
[assembly: AssemblyDescription("Install or remove HostBoss for Scam With Your Friends")]
[assembly: AssemblyProduct("HostBoss game mod patcher")]
[assembly: AssemblyVersion("1.4.3.0")]
[assembly: AssemblyFileVersion("1.4.3.0")]

namespace HostBoss.Desktop {
static class Program {
    [STAThread]
    static int Main(string[] args) {
        try { return Run(args); }
        catch (Exception e) {
            if (args.Length > 0) Console.Error.WriteLine(e.Message);
            else MessageBox.Show(e.Message, "HostBoss Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
    static int Run(string[] args) {
        if (args.Length > 0) {
            if (args.Length == 1 && args[0] == "--self-check") {
                foreach (string name in Payload.Files) if (Payload.Read(name).Length == 0) throw new Exception("Empty package file: " + name);
                Console.WriteLine("PASS: package files available; standard on-disk DLL loading.");
                return 0;
            }
            if (args.Length != 2) throw new ArgumentException("Usage: --install <game-folder>, --remove <game-folder>, --status <game-folder>, or --self-check");
            if (args[0] == "--install") Operations.Install(args[1]);
            else if (args[0] == "--remove") Operations.Remove(args[1]);
            else if (args[0] != "--status") throw new ArgumentException("Unknown option: " + args[0]);
            Console.WriteLine(Operations.Status(args[1])); return 0;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new PatcherForm());
        return 0;
    }
}

static class Payload {
    public static readonly string[] Files = { "Mono.Cecil.dll", "HostBoss.Patcher.dll", "HostBoss.Runtime.dll", "Mono.Cecil.LICENSE.txt", "README.md", "install.ps1", "install.bat", "uninstall.ps1", "uninstall.bat", "play.ps1", "Play Normal.bat", "Play Host Boss.bat" };
    public static byte[] Read(string name) {
        if (Array.IndexOf(Files, name) < 0) throw new ArgumentException("Unknown package file: " + name);
        bool launcher = name == "Play Normal.bat" || name == "Play Host Boss.bat";
        bool library = name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || name == "Mono.Cecil.LICENSE.txt";
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string source = Path.Combine(launcher ? root : library ? Path.Combine(root, "HostBoss", "lib") : Path.Combine(root, "HostBoss"), name);
        if (!File.Exists(source)) throw new FileNotFoundException("Package file missing. Extract the whole HostBoss patcher ZIP and keep its files together: " + source);
        return File.ReadAllBytes(source);
    }
    public static void Extract(string game) {
        // Check the complete distribution before changing any destination files.
        foreach (string name in Files) if (Read(name).Length == 0) throw new IOException("Empty package file: " + name);
        string root = Path.Combine(game, "HostBoss"), lib = Path.Combine(root, "lib");
        Directory.CreateDirectory(lib);
        foreach (string name in Files) {
            bool launcher = name == "Play Normal.bat" || name == "Play Host Boss.bat";
            bool library = name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || name == "Mono.Cecil.LICENSE.txt";
            string target = Path.Combine(launcher ? game : library ? lib : root, name);
            string pending = target + ".installer-new";
            File.WriteAllBytes(pending, Read(name));
            if (File.Exists(target)) File.Replace(pending, target, null);
            else File.Move(pending, target);
        }
    }
}

static class Operations {
    const string GameExe = "Scam With Your Friends.exe";
    static readonly string[] Assemblies = { "Assembly-CSharp.dll", "ScriptsAssDef.dll" };
    public static string Managed(string game, string name) { return Path.Combine(game, "Scam With Your Friends_Data", "Managed", name); }
    public static void Validate(string game, bool closed) {
        if (string.IsNullOrWhiteSpace(game) || !File.Exists(Path.Combine(game, GameExe)))
            throw new DirectoryNotFoundException("Select the game folder containing Scam With Your Friends.exe.");
        foreach (string name in Assemblies) if (!File.Exists(Managed(game, name))) throw new FileNotFoundException("Required game file is missing: " + name);
        if (closed && Process.GetProcessesByName("Scam With Your Friends").Length > 0)
            throw new InvalidOperationException("Close Scam With Your Friends before installing or removing the mod.");
    }
    public static void Install(string game) {
        Validate(game, true);
        Payload.Extract(game);
        HostBoss.Install.Patcher.Install(game);
    }
    public static void Remove(string game) { Validate(game, true); HostBoss.Install.Patcher.Uninstall(game); }
    public static string Status(string game) {
        Validate(game, false);
        int installed = 0;
        foreach (string name in Assemblies) {
            using (var a = Mono.Cecil.AssemblyDefinition.ReadAssembly(Managed(game, name))) {
                foreach (var reference in a.MainModule.AssemblyReferences) if (reference.Name == "HostBoss.Runtime") { installed++; break; }
            }
        }
        if (installed == 0) return "Normal play — HostBoss is disabled.";
        if (installed != 2 || !File.Exists(Managed(game, "HostBoss.Runtime.dll"))) return "Incomplete HostBoss installation. Install / Update to repair it.";
        return "HostBoss is enabled. Host a lobby and press F6.";
    }
    public static void Launch(string game) {
        Validate(game, false);
        Process.Start(new ProcessStartInfo(Path.Combine(game, GameExe)) { WorkingDirectory = game, UseShellExecute = true });
    }
    public static string Detect() {
        string nearby = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 3 && !string.IsNullOrEmpty(nearby); i++) {
            if (File.Exists(Path.Combine(nearby, GameExe))) return nearby.TrimEnd(Path.DirectorySeparatorChar);
            nearby = Path.GetDirectoryName(nearby.TrimEnd(Path.DirectorySeparatorChar));
        }
        List<string> libraries = new List<string>();
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) {
            if (key != null) { string steam = key.GetValue("SteamPath") as string; if (!string.IsNullOrEmpty(steam)) libraries.Add(steam); }
        }
        libraries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        foreach (string library in libraries.ToArray()) {
            string file = Path.Combine(library, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(file)) continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "\"path\"\\s+\"([^\"]+)\""))
                libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
        }
        foreach (string library in libraries) {
            string game = Path.Combine(library, "steamapps", "common", "Scam With Your Friends");
            if (File.Exists(Path.Combine(game, GameExe))) return game;
        }
        return "";
    }
}

sealed class PatcherForm : Form {
    readonly TextBox folder = new TextBox();
    readonly Label status = new Label();
    readonly Button install = new Button(), remove = new Button(), launch = new Button(), browse = new Button(), refresh = new Button();
    readonly Color ink = Color.FromArgb(230, 235, 242), surface = Color.FromArgb(32, 38, 49);
    public PatcherForm() {
        Text = "HostBoss Patcher"; ClientSize = new Size(690, 445);
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(20, 25, 34); ForeColor = ink;
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        Label title = Label("HOST BOSS", 26, 22, 630, 42); title.Font = new Font("Segoe UI", 23, FontStyle.Bold);
        Label("Patcher 1.4.3  /  Scam With Your Friends", 28, 72, 630, 26);
        Label("GAME FOLDER", 28, 119, 630, 24);
        folder.SetBounds(28, 149, 526, 29); folder.BackColor = surface; folder.ForeColor = ink; folder.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(folder);
        Button(browse, "Browse…", 564, 146, 98, 33, false, delegate { Browse(); });
        status.SetBounds(28, 194, 634, 48); status.ForeColor = Color.FromArgb(150, 208, 220); Controls.Add(status);
        Button(install, "Install / Update", 28, 257, 307, 47, true, delegate { Act(true); });
        Button(remove, "Remove / Normal Play", 351, 257, 311, 47, false, delegate { Act(false); });
        Button(launch, "Launch game", 28, 320, 307, 39, false, delegate { Try(delegate { Operations.Launch(folder.Text.Trim()); }); });
        Button(refresh, "Refresh status", 351, 320, 311, 39, false, delegate { RefreshStatus(); });
        Label("Close the game before patching. Backups are kept in HostBoss\\backup.\nTo join other hosts, choose Remove / Normal Play, then launch the game.", 28, 378, 634, 49).ForeColor = Color.FromArgb(169, 181, 199);
        folder.Text = Operations.Detect();
        folder.TextChanged += delegate { RefreshStatus(); };
        RefreshStatus();
    }
    Label Label(string text, int x, int y, int w, int h) {
        Label label = new Label { Text = text }; label.SetBounds(x,y,w,h); Controls.Add(label); return label;
    }
    void Button(Button button, string text, int x, int y, int w, int h, bool accent, EventHandler action) {
        button.Text = text; button.SetBounds(x,y,w,h); button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0; button.BackColor = accent ? Color.FromArgb(50, 161, 183) : surface;
        button.ForeColor = accent ? Color.FromArgb(10, 24, 32) : ink;
        button.Click += action; Controls.Add(button);
    }
    void Browse() {
        using (OpenFileDialog picker = new OpenFileDialog()) {
            picker.Title = "Select Scam With Your Friends.exe"; picker.Filter = "Scam With Your Friends|Scam With Your Friends.exe";
            if (Directory.Exists(folder.Text)) picker.InitialDirectory = folder.Text;
            if (picker.ShowDialog(this) == DialogResult.OK) folder.Text = Path.GetDirectoryName(picker.FileName);
        }
    }
    void RefreshStatus() {
        try { status.Text = Operations.Status(folder.Text.Trim()); launch.Enabled = true; }
        catch (Exception e) { status.Text = e.Message; launch.Enabled = false; }
    }
    void Act(bool installing) {
        UseWaitCursor = true; install.Enabled = remove.Enabled = browse.Enabled = refresh.Enabled = launch.Enabled = false;
        folder.Enabled = false; status.Text = installing ? "Installing HostBoss…" : "Restoring normal play…";
        Update();
        try {
            if (installing) Operations.Install(folder.Text.Trim()); else Operations.Remove(folder.Text.Trim());
            RefreshStatus();
        } catch (Exception e) { Report(e); }
        finally { UseWaitCursor = false; install.Enabled = remove.Enabled = browse.Enabled = refresh.Enabled = folder.Enabled = true; }
    }
    void Try(Action action) { try { action(); } catch (Exception e) { Report(e); } }
    void Report(Exception e) {
        string message = e.Message;
        if (e is UnauthorizedAccessException) message += "\n\nWindows denied access to the game folder. Run this patcher as administrator if needed.";
        status.Text = message;
        MessageBox.Show(this, message, "HostBoss Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
}
