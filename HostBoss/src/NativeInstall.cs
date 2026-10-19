using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using ScamWYF.Setup;

namespace HostBoss.Setup {
static class NativeInstall {
    sealed class Snapshot { public string Path,Backup; public bool Existed; }
    public static void Apply(string folder,bool modsOnly,Action<string> log) {
        string root=Path.GetFullPath(folder).TrimEnd('\\');
        var game=GameInstall.ResolveOrThrow(root);
        foreach(string name in new[]{"Assembly-CSharp.dll","ScriptsAssDef.dll"}) {
            string file=Path.Combine(game.ManagedFolder,name);
            using(var assembly=Mono.Cecil.AssemblyDefinition.ReadAssembly(file)) foreach(var reference in assembly.MainModule.AssemblyReferences)
                if(reference.Name=="HostBoss.Runtime") throw new InvalidOperationException("Remove the legacy HostBoss disk patch through its old patcher first.");
        }
        string backup=Path.Combine(root,"HostBoss","backup","setup-"+Guid.NewGuid().ToString("N"));
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        paths.Add(@"BepInEx\plugins\ScamWYF.HostBoss.dll"); paths.Add(@"BepInEx\core\ScamWYF.Modding.Core.dll");
        if(!modsOnly) {
            foreach(string name in new[]{"winhttp.dll",".doorstop_version","doorstop_config.ini"}) paths.Add(name);
            foreach(string file in Directory.GetFiles(game.ManagedFolder,"*.dll")) paths.Add(@"unstripped_corlib\"+Path.GetFileName(file));
            foreach(string file in Directory.GetFiles(Path.Combine(Payload.Root,"vendor","corlib"),"*.dll")) paths.Add(@"unstripped_corlib\"+Path.GetFileName(file));
            foreach(string file in Directory.GetFiles(Path.Combine(Payload.Root,"BepInEx","core"))) paths.Add(@"BepInEx\core\"+Path.GetFileName(file));
        }
        var snapshots=new List<Snapshot>();
        foreach(string relative in paths) {
            string target=Path.GetFullPath(Path.Combine(root,relative));
            if(!target.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid target.");
            var item=new Snapshot {Path=target,Backup=Path.Combine(backup,relative),Existed=File.Exists(target)};
            if(item.Existed) { Directory.CreateDirectory(Path.GetDirectoryName(item.Backup)); File.Copy(target,item.Backup); }
            snapshots.Add(item);
        }
        try {
            if(!modsOnly) {
                RepairInput(game,backup,log);
                // Use only the visible, verified on-disk files. No downloads or shell process.
                var upstreamLog=new DelegateSetupLog((level,message)=>log(message));
                var options=new InstallOptions {Offline=true};
                Directory.CreateDirectory(game.BepInExCore);
                foreach(string source in Directory.GetFiles(Path.Combine(Payload.Root,"BepInEx","core"))) {
                    string target=Path.Combine(game.BepInExCore,Path.GetFileName(source));
                    if(!File.Exists(target)) File.Copy(source,target);
                }
                // Native loader copies core files from the supplied directory in this adaptation.
                new LoaderInstaller().Install(game,SetupPayload.FromDirectory(Path.Combine(Payload.Root,"vendor")),options,upstreamLog);
                new CorlibSeeder().Seed(game,SetupPayload.FromDirectory(Path.Combine(Payload.Root,"vendor")),upstreamLog);
                new ScamWYF.Setup.Patching.AwaiterUnstripper().Run(game,ScamWYF.Setup.Patching.AwaiterUnstripper.DefaultAssemblies,null,upstreamLog);
                new ScamWYF.Setup.Patching.VtableBreakFixer().Run(game,true,null,null,upstreamLog);
                var problems=new SetupVerifier().Verify(game,upstreamLog);
                if(problems.Count!=0) throw new InvalidOperationException(string.Join("; ",problems));
            }
            foreach(string relative in new[]{@"BepInEx\core\BepInEx.dll",@"BepInEx\core\0Harmony.dll","winhttp.dll",@"unstripped_corlib\mscorlib.dll"})
                if(!File.Exists(Path.Combine(root,relative))) throw new InvalidOperationException("Loader is incomplete; run the full installation.");
            InstallDll(root,"ScamWYF.Modding.Core.dll","core",log);
            InstallDll(root,"ScamWYF.HostBoss.dll","plugins",log);
            log("Backups: "+backup); log("HostBoss installed successfully. Start the game and press F6.");
        } catch {
            log("Installation failed. Restoring changed files from "+backup);
            foreach(var item in snapshots) try {
                if(item.Existed) { if(!File.Exists(item.Path)||Payload.Hash(item.Path)!=Payload.Hash(item.Backup)) File.Copy(item.Backup,item.Path,true); }
                else if(File.Exists(item.Path)) File.Delete(item.Path);
            } catch(Exception e) { log("Could not restore "+item.Path+": "+e.Message+". Backup: "+item.Backup); }
            throw;
        }
    }
    static void RepairInput(GameInstall game,string backup,Action<string> log) {
        string original=Path.Combine(game.ManagedFolder,"Unity.InputSystem.dll"),old=Path.Combine(game.OverrideFolder,"Unity.InputSystem.dll");
        if(File.Exists(old)&&File.Exists(original)&&AssemblyName.GetAssemblyName(old).Version<AssemblyName.GetAssemblyName(original).Version) {
            string target=Path.Combine(backup,"outdated-Unity.InputSystem.dll"); Directory.CreateDirectory(backup); File.Move(old,target); log("Backed up outdated Input System override.");
        }
    }
    static void InstallDll(string root,string name,string category,Action<string> log) {
        string source=Path.Combine(Payload.Root,"mod",name),target=Path.Combine(root,"BepInEx",category,name);
        if(category=="core"&&File.Exists(target)&&AssemblyName.GetAssemblyName(target).Version>AssemblyName.GetAssemblyName(source).Version) {log("Keeping newer shared core.");return;}
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        string pending=target+"."+Guid.NewGuid().ToString("N")+".new";
        try { File.Copy(source,pending); if(File.Exists(target)) File.Replace(pending,target,null); else File.Move(pending,target); }
        finally { if(File.Exists(pending)) File.Delete(pending); }
        if(Payload.Hash(source)!=Payload.Hash(target)) throw new IOException("Installed checksum mismatch: "+name);
        log("Installed "+target);
    }
}
}
