using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using ScamWYF.Setup;

namespace HostBoss.Setup {
static class Payload {
    public static string Root { get { return Path.Combine(AppContext.BaseDirectory,"SetupFiles"); } }
    public static string Hash(string file) { using(var input=File.OpenRead(file)) return Convert.ToHexString(SHA256.HashData(input)); }
    public static void Verify() {
        string root=Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        foreach(string line in File.ReadAllLines(Path.Combine(root,"SHA256SUMS.txt"))) {
            if(string.IsNullOrWhiteSpace(line)) continue;
            if(line.Length<67) throw new InvalidDataException("Invalid checksum manifest.");
            string relative=line.Substring(66).Replace('\\',Path.DirectorySeparatorChar);
            string file=Path.GetFullPath(Path.Combine(root,relative));
            if(!file.StartsWith(root,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)) throw new InvalidDataException("Unsafe checksum path.");
            if(!File.Exists(file)||!string.Equals(Hash(file),line.Substring(0,64),StringComparison.OrdinalIgnoreCase)) throw new IOException("Missing or changed setup file: "+relative);
        }
    }
}
static class LinuxProgram {
    const string LaunchOption="WINEDLLOVERRIDES=\"winhttp=n,b\" %command% -whisper-cpu";
    static int Main(string[] args) {
        try {
            if(args.Length==1&&args[0]=="--self-check") {Payload.Verify();Console.WriteLine("Package checksums verified.");return 0;}
            if(args.Length==1&&args[0]=="--launch-option") {Console.WriteLine(LaunchOption);return 0;}
            if(args.Length==1&&args[0]=="--version") {Console.WriteLine("HostBoss Linux Setup 1.60.4 (Steam Proton, x64)");return 0;}
            if(args.Length==0||args[0]=="--help") {
                Console.WriteLine("HostBoss Linux Setup 1.60.4 — experimental Steam Proton support\nUsage: ./hostboss-setup --game-dir \"/path/to/Scam With Your Friends\" [--mods-only]\n       ./hostboss-setup --self-check\n       ./hostboss-setup --launch-option\n\nClose the game before installing. Do not run as root.\nKeep the entire extracted folder together. The game uses its Windows build through Proton.");
                return 0;
            }
            string folder=null;bool modsOnly=false;
            for(int i=0;i<args.Length;i++) {
                if(args[i]=="--game-dir"&&i+1<args.Length) folder=args[++i];
                else if(args[i]=="--mods-only") modsOnly=true;
                else throw new ArgumentException("Unknown or incomplete option: "+args[i]);
            }
            if(!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This installer targets Linux Steam/Proton. Use the Windows setup on Windows.");
            if(Environment.UserName=="root") throw new InvalidOperationException("Run this installer as your Steam user, without sudo.");
            if(string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Pass --game-dir with the game folder from Steam > Properties > Installed Files > Browse.");
            string game=Path.GetFullPath(folder);
            if(!File.Exists(Path.Combine(game,"Scam With Your Friends.exe"))) throw new DirectoryNotFoundException("Select the folder containing Scam With Your Friends.exe.");
            RequireClosed(); Payload.Verify();
            string state=Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            if(string.IsNullOrWhiteSpace(state)) state=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".local","state");
            string logs=Path.Combine(state,"hostboss","setup");Directory.CreateDirectory(logs);
            string logPath=Path.Combine(logs,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".log");
            Console.WriteLine("Installation log: "+logPath);
            using(var writer=new StreamWriter(logPath)) {
                NativeInstall.Apply(game,modsOnly,line=>{Console.WriteLine(line);writer.WriteLine(line);writer.Flush();});
            }
            Console.WriteLine("\nIn Steam > game Properties > General > Launch Options, use:\n"+LaunchOption+"\nKeep Steam's Proton compatibility enabled. Host a lobby and press F6.\nThe installer has not changed Steam launch options or the Proton registry.");
            return 0;
        } catch(Exception e) {Console.Error.WriteLine("HostBoss setup failed: "+e.Message);return 1;}
    }
    static void RequireClosed() {
        // Wine process names can be truncated. Inspect visible proc command lines as well.
        foreach(string directory in Directory.GetDirectories("/proc")) {
            int pid;if(!int.TryParse(Path.GetFileName(directory),out pid)||pid==Environment.ProcessId) continue;
            try {
                string[] command=File.ReadAllText(Path.Combine(directory,"cmdline")).Split('\0',StringSplitOptions.RemoveEmptyEntries);
                if(command.Length==0) continue;
                string executable=Path.GetFileName(command[0].Replace('\\','/'));
                bool wine=executable.StartsWith("wine",StringComparison.OrdinalIgnoreCase);
                if(executable.Equals("Scam With Your Friends.exe",StringComparison.OrdinalIgnoreCase)||
                    (wine&&Array.Exists(command,arg=>Path.GetFileName(arg.Replace('\\','/')).Equals("Scam With Your Friends.exe",StringComparison.OrdinalIgnoreCase))))
                    throw new InvalidOperationException("Close Scam With Your Friends before installing HostBoss.");
            } catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
    }
}
}
