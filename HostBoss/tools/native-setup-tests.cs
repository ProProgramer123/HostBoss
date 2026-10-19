using System; using System.IO;
class SetupTest {
 static int Main(string[] args) {try {
  string game=args[0];
  HostBoss.Setup.NativeInstall.Apply(game,false,Console.WriteLine);
  string core=Path.Combine(game,@"BepInEx\core\ScamWYF.Modding.Core.dll"), plugin=Path.Combine(game,@"BepInEx\plugins\ScamWYF.HostBoss.dll");
  if(!File.Exists(core)||!File.Exists(plugin))throw new Exception("Missing installed DLLs");
  Console.WriteLine("PASS: full offline native install.");
  string managed=Path.Combine(game,@"Scam With Your Friends_Data\Managed");
  foreach(string file in Directory.GetFiles(managed,"*.dll")) if(HostBoss.Setup.Payload.Hash(file)!=HostBoss.Setup.Payload.Hash(Path.Combine(args[1],Path.GetFileName(file))))throw new Exception("Game DLL changed: "+file);
  Console.WriteLine("PASS: all game Managed assemblies remain byte-for-byte unchanged.");
  string saves=Path.Combine(game,"HostBoss","Presentations"); Directory.CreateDirectory(saves); File.WriteAllText(Path.Combine(saves,"keep.json"),"fixture");
  HostBoss.Setup.NativeInstall.Apply(game,true,Console.WriteLine);
  if(File.ReadAllText(Path.Combine(saves,"keep.json"))!="fixture")throw new Exception("Save modified");
  Console.WriteLine("PASS: repeat install and saves retained.");
  using(var a=Mono.Cecil.AssemblyDefinition.ReadAssembly(core)){a.Name.Version=new Version(1,0,0,0);a.Write(core+".fixture");} File.Copy(core+".fixture",core,true);
  string before=HostBoss.Setup.Payload.Hash(core); bool rejected=false;
  using(var locked=File.Open(plugin,FileMode.Open,FileAccess.Read,FileShare.Read)){try{HostBoss.Setup.NativeInstall.Apply(game,true,Console.WriteLine);}catch{rejected=true;}}
  if(!rejected||before!=HostBoss.Setup.Payload.Hash(core))throw new Exception("Rollback failed");
  Console.WriteLine("PASS: failed update restores previous core bytes.");
  using(var a=Mono.Cecil.AssemblyDefinition.ReadAssembly(core)){a.Name.Version=new Version(9,0,0,0);a.Write(core+".fixture");} File.Copy(core+".fixture",core,true); before=HostBoss.Setup.Payload.Hash(core);
  HostBoss.Setup.NativeInstall.Apply(game,true,Console.WriteLine); if(before!=HostBoss.Setup.Payload.Hash(core))throw new Exception("Newer core replaced");
  Console.WriteLine("PASS: newer shared core preserved.");
  string legacy=Path.Combine(managed,"Assembly-CSharp.dll"); var resolver=new Mono.Cecil.DefaultAssemblyResolver(); resolver.AddSearchDirectory(managed); resolver.AddSearchDirectory(Path.Combine(game,"BepInEx","core")); using(var a=Mono.Cecil.AssemblyDefinition.ReadAssembly(legacy,new Mono.Cecil.ReaderParameters {AssemblyResolver=resolver})){a.MainModule.AssemblyReferences.Add(new Mono.Cecil.AssemblyNameReference("HostBoss.Runtime",new Version(1,0)));a.Write(legacy+".fixture");} File.Copy(legacy+".fixture",legacy,true);
  rejected=false;try{HostBoss.Setup.NativeInstall.Apply(game,true,Console.WriteLine);}catch(InvalidOperationException e){rejected=e.Message.Contains("legacy");} if(!rejected)throw new Exception("Legacy patch accepted");
  Console.WriteLine("PASS: legacy disk patch rejected."); return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
