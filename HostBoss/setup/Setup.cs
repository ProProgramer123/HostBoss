using System;using System.IO;using System.Linq;using System.Reflection;using System.Diagnostics;using System.Drawing;using System.Collections.Generic;using System.Security.Cryptography;using System.Threading.Tasks;using System.Windows.Forms;using Microsoft.Win32;using ScamWYF.Setup;
[assembly:AssemblyTitle("HostBoss Setup")]
[assembly:AssemblyVersion("1.60.6.0")]
[assembly:AssemblyFileVersion("1.60.6.0")]
namespace HostBoss.DesktopSetup {
static class Program {
 [STAThread]static int Main(string[] args){try{
  if(args.Length==1&&args[0]=="--self-check"){Files.Verify();return 0;}
  if(args.Length==1&&args[0]=="--ui-self-check"){Application.EnableVisualStyles();using(var form=new SetupWindow()){form.Opacity=0;form.ShowInTaskbar=false;form.Show();if(!form.IsHandleCreated)throw new Exception("Window initialization failed.");form.Close();}return 0;}
  if(args.Length==2&&(args[0]=="--install"||args[0]=="--mods-only")){Install(args[1],args[0]=="--mods-only",Console.WriteLine);return 0;}
  if(args.Length!=0)throw new ArgumentException("Usage: --self-check, --install <game-folder>, --mods-only <game-folder>");
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new SetupWindow());return 0;
 }catch(Exception e){if(args.Length!=0)Console.Error.WriteLine(e);else MessageBox.Show(e.Message,"HostBoss Setup",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}}
 internal static void Install(string folder,bool modsOnly,Action<string> log){
  if(Process.GetProcessesByName("Scam With Your Friends").Length!=0)throw new InvalidOperationException("Close Scam With Your Friends before installing.");
  Files.Verify();
  string logs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HostBoss","Setup","Logs");Directory.CreateDirectory(logs);
  string file=Path.Combine(logs,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".log");log("Log: "+file);
  using(var writer=new StreamWriter(file)){Operations.Apply(folder,modsOnly,line=>{writer.WriteLine(line);writer.Flush();log(line);});}
 }
}
static class Files {
 public static string Root{get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SetupFiles");}}
 public static string Hash(string file){using(var stream=File.OpenRead(file))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
 public static void Verify(){
  string root=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\')+"\\";
  string manifest=Path.Combine(root,"SHA256SUMS.txt");if(!File.Exists(manifest))throw new IOException("Extract the entire setup ZIP and keep its files together.");
  foreach(string line in File.ReadAllLines(manifest)){if(string.IsNullOrWhiteSpace(line))continue;if(line.Length<67)throw new IOException("Invalid setup checksum list.");string file=Path.GetFullPath(Path.Combine(root,line.Substring(66)));if(!file.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(file)||!string.Equals(Hash(file),line.Substring(0,64),StringComparison.OrdinalIgnoreCase))throw new IOException("Setup file missing or changed: "+line.Substring(66));}
 }
}
static class Operations {
 sealed class Snapshot{public string Target,Backup;public bool Existed;}
 internal static void Apply(string folder,bool modsOnly,Action<string> log){
  string root=Path.GetFullPath(folder.Trim().Trim('"')).TrimEnd('\\');
  if(!File.Exists(Path.Combine(root,"Scam With Your Friends.exe")))throw new IOException("Select the folder containing Scam With Your Friends.exe.");
  var game=GameInstall.ResolveOrThrow(root);
  foreach(string name in new[]{"Assembly-CSharp.dll","ScriptsAssDef.dll"})using(var a=Mono.Cecil.AssemblyDefinition.ReadAssembly(Path.Combine(game.ManagedFolder,name)))if(a.MainModule.AssemblyReferences.Any(r=>r.Name=="HostBoss.Runtime"))throw new InvalidOperationException("Remove legacy HostBoss disk patches with the old patcher's Remove / Normal Play first.");
  string backup=Path.Combine(root,"HostBoss","backup","setup-1.60.6-"+Guid.NewGuid().ToString("N"));
  var targets=new HashSet<string>(StringComparer.OrdinalIgnoreCase){@"BepInEx\plugins\ScamWYF.HostBoss.dll",@"BepInEx\core\ScamWYF.Modding.Core.dll"};
  if(!modsOnly){
   foreach(string name in new[]{"winhttp.dll",".doorstop_version","doorstop_config.ini"})targets.Add(name);
   foreach(string file in Directory.GetFiles(game.ManagedFolder,"*.dll"))targets.Add(@"unstripped_corlib\"+Path.GetFileName(file));
   if(Directory.Exists(game.OverrideFolder))foreach(string file in Directory.GetFiles(game.OverrideFolder))targets.Add(@"unstripped_corlib\"+Path.GetFileName(file));
   targets.Add(@"unstripped_corlib\.seeded");targets.Add(@"unstripped_corlib\.derived-from");
   foreach(string file in Directory.GetFiles(Path.Combine(Files.Root,"vendor","corlib"),"*.dll"))targets.Add(@"unstripped_corlib\"+Path.GetFileName(file));
   foreach(string file in Directory.GetFiles(Path.Combine(Files.Root,"BepInEx","core")))targets.Add(@"BepInEx\core\"+Path.GetFileName(file));
  }
  var snapshots=new List<Snapshot>();foreach(string relative in targets){string target=Path.GetFullPath(Path.Combine(root,relative));if(!target.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid install path.");var s=new Snapshot{Target=target,Backup=Path.Combine(backup,relative),Existed=File.Exists(target)};if(s.Existed){Directory.CreateDirectory(Path.GetDirectoryName(s.Backup));File.Copy(target,s.Backup);}snapshots.Add(s);}
  try{
   if(!modsOnly){
    var logger=new DelegateSetupLog((level,line)=>log(line));
    Directory.CreateDirectory(game.BepInExCore);foreach(string source in Directory.GetFiles(Path.Combine(Files.Root,"BepInEx","core"))){string target=Path.Combine(game.BepInExCore,Path.GetFileName(source));if(!File.Exists(target))File.Copy(source,target);}
    var payload=SetupPayload.FromDirectory(Path.Combine(Files.Root,"vendor"));
    new LoaderInstaller().Install(game,payload,new InstallOptions{Offline=true},logger);
    new CorlibSeeder().Seed(game,payload,logger);
    // Discard stale derived copies before patching either awaiters or interfaces.
    // The source-hash manifest catches updates whose assembly version stays 0.0.0.0.
    DerivedOverrides.DiscardStale(game,logger);
    new ScamWYF.Setup.Patching.AwaiterUnstripper().Run(game,ScamWYF.Setup.Patching.AwaiterUnstripper.DefaultAssemblies,null,logger);
    new ScamWYF.Setup.Patching.VtableBreakFixer().Run(game,true,null,null,logger);
    var problems=new SetupVerifier().Verify(game,logger);if(problems.Count!=0)throw new InvalidOperationException(string.Join("; ",problems));
   }
   foreach(string path in new[]{@"BepInEx\core\BepInEx.dll",@"BepInEx\core\0Harmony.dll",@"unstripped_corlib\mscorlib.dll","winhttp.dll"})if(!File.Exists(Path.Combine(root,path)))throw new IOException("Loader is incomplete. Run the full Install / Update.");
   InstallDll(game,"ScamWYF.Modding.Core.dll","core",log);InstallDll(game,"ScamWYF.HostBoss.dll","plugins",log);
   log("Backup: "+backup);log("HostBoss 1.60.6 installed. Start the game and press F6.");
  }catch{log("Installation failed; restoring files. Backup: "+backup);foreach(var s in snapshots)try{if(s.Existed){if(!File.Exists(s.Target)||Files.Hash(s.Target)!=Files.Hash(s.Backup))File.Copy(s.Backup,s.Target,true);}else if(File.Exists(s.Target))File.Delete(s.Target);}catch(Exception e){log("Restore failed: "+s.Target+" — "+e.Message+". Backup: "+s.Backup);}throw;}
 }
 static void InstallDll(GameInstall game,string name,string category,Action<string> log){
  string source=Path.Combine(Files.Root,"mod",name),target=Path.Combine(game.Path,"BepInEx",category,name);
  if(category=="core"&&File.Exists(target)&&AssemblyName.GetAssemblyName(target).Version>AssemblyName.GetAssemblyName(source).Version){log("Keeping newer shared mod-lib.");return;}
  Directory.CreateDirectory(Path.GetDirectoryName(target));string pending=target+"."+Guid.NewGuid().ToString("N")+".new";try{File.Copy(source,pending);if(File.Exists(target))File.Replace(pending,target,null);else File.Move(pending,target);}finally{if(File.Exists(pending))File.Delete(pending);}if(Files.Hash(source)!=Files.Hash(target))throw new IOException("Installed checksum mismatch: "+name);log("Installed "+target);
 }
 internal static string Detect(){
  var libraries=new List<string>();using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")){if(key!=null&&key.GetValue("SteamPath") is string)libraries.Add((string)key.GetValue("SteamPath"));}libraries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
  foreach(string library in libraries.ToArray()){string file=Path.Combine(library,"steamapps","libraryfolders.vdf");if(!File.Exists(file))continue;foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file),"\"path\"\\s+\"([^\"]+)\""))libraries.Add(m.Groups[1].Value.Replace("\\\\","\\"));}
  foreach(string library in libraries){string game=Path.Combine(library,"steamapps","common","Scam With Your Friends");if(File.Exists(Path.Combine(game,"Scam With Your Friends.exe")))return game;}return "";
 }
}
sealed class SetupWindow:Form {
 readonly TextBox folder=new TextBox(),output=new TextBox();readonly Button browse=new Button(),install=new Button();bool busy;
 public SetupWindow(){
  Text="HostBoss Setup 1.60.6";ClientSize=new Size(740,510);MinimumSize=Size;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);
  Controls.Add(new Label{Text="HostBoss Setup",Font=new Font("Segoe UI",22,FontStyle.Bold),Location=new Point(20,15),Size=new Size(690,45)});
  Controls.Add(new Label{Text="HostBoss 1.60.6 + offline mod-loader setup for game 0.1.95.\nClose the game first. Keep the extracted setup files together.",Location=new Point(24,66),Size=new Size(690,48)});
  folder.SetBounds(24,129,570,28);folder.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;folder.Text=Operations.Detect();Controls.Add(folder);
  browse.Text="Browse…";browse.SetBounds(606,126,110,34);browse.Anchor=AnchorStyles.Top|AnchorStyles.Right;browse.Click+=delegate{using(var picker=new OpenFileDialog{Filter="Scam With Your Friends|Scam With Your Friends.exe"})if(picker.ShowDialog(this)==DialogResult.OK)folder.Text=Path.GetDirectoryName(picker.FileName);};Controls.Add(browse);
  install.Text="Install / Update";install.SetBounds(24,177,692,41);install.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;install.Click+=async delegate{busy=true;folder.Enabled=browse.Enabled=install.Enabled=false;try{string target=folder.Text;await Task.Run(()=>Program.Install(target,false,Log));MessageBox.Show(this,"HostBoss installed. Start the game and press F6.","Setup complete");}catch(Exception e){Log(e.Message);MessageBox.Show(this,e.Message+"\nIf access is denied, close setup and run it as administrator.","Setup failed",MessageBoxButtons.OK,MessageBoxIcon.Error);}finally{busy=false;folder.Enabled=browse.Enabled=install.Enabled=true;}};Controls.Add(install);
  output.SetBounds(24,235,692,250);output.Multiline=true;output.ReadOnly=true;output.ScrollBars=ScrollBars.Vertical;output.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;output.Text="Installs both required DLLs and repairs outdated loader overrides.\r\nSaved presentations are kept. Existing files are backed up.\r\nNo PowerShell or download is used during installation.\r\n";Controls.Add(output);
  FormClosing+=(s,e)=>{if(busy){e.Cancel=true;MessageBox.Show(this,"Wait for installation to finish.");}};
 }
 void Log(string text){if(InvokeRequired){BeginInvoke(new Action<string>(Log),text);return;}output.AppendText(text+Environment.NewLine);}
}
}
