using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace HostBoss.Install {
public static class Patcher {
    static string Hash(string path) { using(var s=File.OpenRead(path)) using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)); }
    static MethodDefinition Method(AssemblyDefinition a, string type, string method, int count) {
        return a.MainModule.GetType(type).Methods.Single(m => m.Name == method && m.Parameters.Count == count);
    }
    static void Prefix(MethodDefinition m, params Instruction[] instructions) {
        var il=m.Body.GetILProcessor(); var first=m.Body.Instructions[0];
        foreach(var i in instructions) il.InsertBefore(first,i);
        m.Body.MaxStackSize=Math.Max(8,m.Body.MaxStackSize);
    }
    static MethodReference Hook(AssemblyDefinition target, AssemblyDefinition runtime, string name) {
        return target.MainModule.ImportReference(runtime.MainModule.GetType("HostBoss.Hooks").Methods.Single(m=>m.Name==name));
    }
    static void HostNoOp(AssemblyDefinition a, AssemblyDefinition runtime, string type, string name, int count, bool returnsBool, string guard = "ManualBossActive") {
        var m=Method(a,type,name,count); var first=m.Body.Instructions[0];
        if(returnsBool) Prefix(m,Instruction.Create(OpCodes.Call,Hook(a,runtime,guard)),Instruction.Create(OpCodes.Brfalse,first),Instruction.Create(OpCodes.Ldc_I4_0),Instruction.Create(OpCodes.Ret));
        else Prefix(m,Instruction.Create(OpCodes.Call,Hook(a,runtime,guard)),Instruction.Create(OpCodes.Brfalse,first),Instruction.Create(OpCodes.Ret));
    }
    static bool AddQuoteDisplayHook(AssemblyDefinition a, AssemblyDefinition runtime) {
        var m=Method(a,"Kolkata.UI.ReviewWhiteboardController","PlaySlideNarration",1);
        if(m.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.FullName=="HostBoss.Hooks" && ((MethodReference)i.Operand).Name=="PresentQuoteOnly")) return false;
        var first=m.Body.Instructions[0];
        Prefix(m,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,Hook(a,runtime,"PresentQuoteOnly")),Instruction.Create(OpCodes.Brfalse,first),Instruction.Create(OpCodes.Ret));
        return true;
    }
    public static void Install(string game) {
        string root=Path.Combine(game,"HostBoss"), managed=Path.Combine(game,"Scam With Your Friends_Data","Managed"), backup=Path.Combine(root,"backup");
        Directory.CreateDirectory(backup);
        foreach(string name in new[]{"Assembly-CSharp.dll","ScriptsAssDef.dll"}) {
            string stale=Path.Combine(root,name+".pending");
            if(File.Exists(stale)) File.Delete(stale);
        }
        var resolver=new DefaultAssemblyResolver(); resolver.AddSearchDirectory(managed); resolver.AddSearchDirectory(Path.Combine(root,"lib"));
        var rp=new ReaderParameters { AssemblyResolver=resolver, InMemory=true };
        using(var runtime=AssemblyDefinition.ReadAssembly(Path.Combine(root,"lib","HostBoss.Runtime.dll"),rp)) {
            string[] names={"Assembly-CSharp.dll","ScriptsAssDef.dll"};
            var preserveBackup=new System.Collections.Generic.HashSet<string>();
            foreach(string name in names) {
                string path=Path.Combine(managed,name), stamp=Path.Combine(backup,name+".sha256");
                using(var a=AssemblyDefinition.ReadAssembly(path,rp)) {
                    if(a.MainModule.AssemblyReferences.Any(x=>x.Name=="HostBoss.Runtime")) {
                        if(!File.Exists(stamp) || File.ReadAllText(stamp)!=Hash(path)) throw new Exception(name+" changed after HostBoss installation. Restore/verify game files before reinstalling; no files changed.");
                        if(name=="Assembly-CSharp.dll") {
                            bool upgraded=false;
                            foreach(string method in new[]{"ShouldPlayNarration","StartFiredPresentation"}) {
                                var m=Method(a,"Kolkata.UI.ReviewWhiteboardController",method,0);
                                var old=m.Body.Instructions[0].Operand as MethodReference;
                                if(old!=null && old.DeclaringType.FullName=="HostBoss.Hooks" && old.Name=="IsHost") {
                                    m.Body.Instructions[0].Operand=Hook(a,runtime,"SuppressAutomaticBoss"); upgraded=true;
                                }
                            }
                            if(AddQuoteDisplayHook(a,runtime)) upgraded=true;
                            var advance=Method(a,"Kolkata.UI.ReviewWhiteboardController","ScheduleNextAdvance",0);
                            var advanceGuard=advance.Body.Instructions[0].Operand as MethodReference;
                            if(advanceGuard!=null && advanceGuard.DeclaringType.FullName=="HostBoss.Hooks" && advanceGuard.Name=="IsHost") {
                                advance.Body.Instructions[0].Operand=Hook(a,runtime,"ManualBossActive"); upgraded=true;
                            }
                            if(upgraded) { a.Write(Path.Combine(root,name+".pending")); preserveBackup.Add(name); }
                        } else {
                            bool upgraded=false;
                            foreach(string method in new[]{"ServerToggleReviewSkipVote","ServerReviewPhaseRoutine"}) {
                                var m=Method(a,"ScammerGameManager",method,1);
                                var guard=m.Body.Instructions[0].Operand as MethodReference;
                                if(guard!=null && guard.DeclaringType.FullName=="HostBoss.Hooks" && guard.Name=="IsHost") {
                                    m.Body.Instructions[0].Operand=Hook(a,runtime,"ManualBossActive"); upgraded=true;
                                }
                            }
                            if(upgraded) { a.Write(Path.Combine(root,name+".pending")); preserveBackup.Add(name); }
                        }
                        continue;
                    }
                    if(name=="Assembly-CSharp.dll") {
                        string t="Kolkata.UI.ReviewWhiteboardController";
                        foreach(string method in new[]{"HandleDayEnded","BeginReviewPhaseDeckLoad"}) {
                            var m=Method(a,t,method,1); var first=m.Body.Instructions[0];
                            Prefix(m,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,Hook(a,runtime,"BuildDeck")),Instruction.Create(OpCodes.Brfalse,first),Instruction.Create(OpCodes.Ret));
                        }
                        HostNoOp(a,runtime,t,"ScheduleNextAdvance",0,false);
                        HostNoOp(a,runtime,t,"ShouldPlayNarration",0,true,"SuppressAutomaticBoss");
                        HostNoOp(a,runtime,t,"StartFiredPresentation",0,false,"SuppressAutomaticBoss");
                        AddQuoteDisplayHook(a,runtime);
                    } else {
                        string t="ScammerGameManager";
                        Prefix(Method(a,t,"Awake",0),Instruction.Create(OpCodes.Call,Hook(a,runtime,"Init")));
                        HostNoOp(a,runtime,t,"ServerToggleReviewSkipVote",1,false);
                        var review=Method(a,t,"ServerReviewPhaseRoutine",1); var first=review.Body.Instructions[0];
                        Prefix(review,Instruction.Create(OpCodes.Call,Hook(a,runtime,"ManualBossActive")),Instruction.Create(OpCodes.Brfalse,first),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,Hook(a,runtime,"ManualReview")),Instruction.Create(OpCodes.Ret));
                        var money=Method(a,t,"ServerAddToMoneyPool",2); first=money.Body.Instructions[0];
                        Prefix(money,Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,Hook(a,runtime,"AllowEarnings")),Instruction.Create(OpCodes.Brtrue,first),Instruction.Create(OpCodes.Ldc_I4_0),Instruction.Create(OpCodes.Ret));
                    }
                    // Stage both assemblies before changing either game file.
                    a.Write(Path.Combine(root,name+".pending"));
                }
            }
            resolver.Dispose();
            foreach(string name in names) {
                string pending=Path.Combine(root,name+".pending"), path=Path.Combine(managed,name);
                if(!File.Exists(pending)) continue;
                if(!preserveBackup.Contains(name)) File.Copy(path,Path.Combine(backup,name),true);
                File.Copy(pending,path,true);
                File.WriteAllText(Path.Combine(backup,name+".sha256"),Hash(path));
                File.Delete(pending);
            }
            string runtimeTarget=Path.Combine(managed,"HostBoss.Runtime.dll");
            string runtimeStaging=runtimeTarget+".new";
            File.Copy(Path.Combine(root,"lib","HostBoss.Runtime.dll"),runtimeStaging,true);
            if(File.Exists(runtimeTarget)) File.Replace(runtimeStaging,runtimeTarget,null);
            else File.Move(runtimeStaging,runtimeTarget);
        }
    }
    public static void Uninstall(string game) {
        string root=Path.Combine(game,"HostBoss"), managed=Path.Combine(game,"Scam With Your Friends_Data","Managed"), backup=Path.Combine(root,"backup");
        var restore=new System.Collections.Generic.List<string>();
        foreach(string name in new[]{"Assembly-CSharp.dll","ScriptsAssDef.dll"}) {
            string path=Path.Combine(managed,name), stamp=Path.Combine(backup,name+".sha256");
            if(!File.Exists(stamp)) {
                using(var a=AssemblyDefinition.ReadAssembly(path)) {
                    if(a.MainModule.AssemblyReferences.Any(x=>x.Name=="HostBoss.Runtime"))
                        throw new Exception("HostBoss backup information is missing for "+name+". Restore the original backup or use Steam Verify integrity. The mod was not removed.");
                }
                continue;
            }
            if(File.ReadAllText(stamp)!=Hash(path)) {
                using(var a=AssemblyDefinition.ReadAssembly(path)) {
                    // Steam updates replace patched assemblies. Never restore the previous version over them.
                    if(a.MainModule.AssemblyReferences.Any(x=>x.Name=="HostBoss.Runtime"))
                        throw new Exception(name+" changed since install. Refusing to restore an old backup. Use Steam Verify, then reinstall your mods.");
                }
            } else {
                if(!File.Exists(Path.Combine(backup,name))) throw new Exception("Original backup is missing for "+name+". Use Steam Verify integrity to restore the game.");
                restore.Add(name);
            }
        }
        foreach(string name in new[]{"Assembly-CSharp.dll","ScriptsAssDef.dll"}) {
            string stamp=Path.Combine(backup,name+".sha256");
            if(!File.Exists(stamp)) continue;
            if(restore.Contains(name)) File.Copy(Path.Combine(backup,name),Path.Combine(managed,name),true);
            File.Delete(stamp);
        }
        string runtime=Path.Combine(managed,"HostBoss.Runtime.dll"); if(File.Exists(runtime)) File.Delete(runtime);
    }
}
}
