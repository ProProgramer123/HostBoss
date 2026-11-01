using System;using System.IO;using System.Linq;using Mono.Cecil;
class SetupTests {
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS: "+label);}
 static string Hash(string f){return HostBoss.DesktopSetup.Files.Hash(f);}
 static int Main(string[] args){try{
  string game=args[0],sourceManaged=args[1];
  HostBoss.DesktopSetup.Operations.Apply(game,false,Console.WriteLine);
  string plugin=Path.Combine(game,@"BepInEx\plugins\ScamWYF.HostBoss.dll"),core=Path.Combine(game,@"BepInEx\core\ScamWYF.Modding.Core.dll");
  Check(System.Reflection.AssemblyName.GetAssemblyName(plugin).Version.ToString()=="1.60.6.0","clean setup installs HostBoss 1.60.6 and its loader");
  string managed=Path.Combine(game,@"Scam With Your Friends_Data\Managed");foreach(string f in Directory.GetFiles(managed,"*.dll"))Check(Hash(f)==Hash(Path.Combine(sourceManaged,Path.GetFileName(f))),"unchanged Managed/"+Path.GetFileName(f));
  string save=Path.Combine(game,"HostBoss","Presentations","keep.json");Directory.CreateDirectory(Path.GetDirectoryName(save));File.WriteAllText(save,"fixture");
  HostBoss.DesktopSetup.Operations.Apply(game,false,Console.WriteLine);
  Check(File.ReadAllText(save)=="fixture","repeat setup preserves saved files");
  string staleMirror=args[2],overrideMirror=Path.Combine(game,@"unstripped_corlib\Mirror.dll");
  File.Copy(staleMirror,overrideMirror,true);File.WriteAllText(Path.Combine(game,@"unstripped_corlib\.derived-from"),new string('0',64)+" *Mirror.dll\n");
  HostBoss.DesktopSetup.Operations.Apply(game,false,Console.WriteLine);
  string active=File.Exists(overrideMirror)?overrideMirror:Path.Combine(managed,"Mirror.dll");
  using(var a=AssemblyDefinition.ReadAssembly(active))Check(a.MainModule.GetType("Mirror.NetworkServer").Fields.Any(f=>f.Name=="commandValidator"),"stale same-version Mirror override repaired");
  using(var a=AssemblyDefinition.ReadAssembly(core)){a.Name.Version=new Version(0,0,0,1);a.Write(core+".fixture");}File.Copy(core+".fixture",core,true);string previous=Hash(core);bool failed=false;
  using(var locked=File.Open(plugin,FileMode.Open,FileAccess.Read,FileShare.Read)){try{HostBoss.DesktopSetup.Operations.Apply(game,true,Console.WriteLine);}catch{failed=true;}}
  Check(failed&&Hash(core)==previous,"failed update rolls back shared core bytes");
  using(var a=AssemblyDefinition.ReadAssembly(core)){a.Name.Version=new Version(9,0,0,0);a.Write(core+".fixture");}File.Copy(core+".fixture",core,true);previous=Hash(core);
  HostBoss.DesktopSetup.Operations.Apply(game,true,Console.WriteLine);Check(Hash(core)==previous,"newer shared mod-lib retained");
  var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(managed);resolver.AddSearchDirectory(Path.Combine(game,@"BepInEx\core"));string main=Path.Combine(managed,"Assembly-CSharp.dll");using(var a=AssemblyDefinition.ReadAssembly(main,new ReaderParameters{AssemblyResolver=resolver})){a.MainModule.AssemblyReferences.Add(new AssemblyNameReference("HostBoss.Runtime",new Version(1,0)));a.Write(main+".fixture");}File.Copy(main+".fixture",main,true);
  failed=false;try{HostBoss.DesktopSetup.Operations.Apply(game,true,Console.WriteLine);}catch(InvalidOperationException e){failed=e.Message.Contains("legacy");}Check(failed,"legacy disk patches refused");
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
