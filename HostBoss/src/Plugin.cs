using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Kolkata.UI;
using UnityEngine;

[assembly: AssemblyTitle("HostBoss BepInEx Plugin")]
[assembly: AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]

namespace HostBoss {
    [BepInPlugin(PluginId, "HostBoss", "1.5.0")]
    [BepInProcess("Scam With Your Friends.exe")]
    public sealed class Plugin : BaseUnityPlugin {
        public const string PluginId = "com.proprogramer123.scamwyf.hostboss";
        Harmony harmony;
        bool started;

        void Awake() { started = true; EnablePlugin(); }
        void OnEnable() { if (started) EnablePlugin(); }
        void OnDisable() { DisablePlugin(); }
        void OnDestroy() { DisablePlugin(); }

        void EnablePlugin() {
            if (harmony != null) return;
            try {
                // The disk patcher and this plugin must not both own the same game hooks.
                foreach (Assembly game in new[] { typeof(ScammerGameManager).Assembly, typeof(ReviewWhiteboardController).Assembly })
                    foreach (AssemblyName reference in game.GetReferencedAssemblies())
                        if (reference.Name == "HostBoss.Runtime")
                            throw new InvalidOperationException("Remove the legacy HostBoss DLL patches with HostBoss Patcher (Remove / Normal Play), then restart before using this plugin.");

                // Validate every target before applying any runtime patches.
                var patches = new List<KeyValuePair<MethodInfo, HarmonyMethod>>();
                Add(patches, typeof(ScammerGameManager), "Awake", Type.EmptyTypes, "GameAwake");
                Add(patches, typeof(ScammerGameManager), "ServerToggleReviewSkipVote", new[] { typeof(PlayerName) }, "ManualNoOp");
                Add(patches, typeof(ScammerGameManager), "ServerReviewPhaseRoutine", new[] { typeof(int) }, "ReviewRoutine");
                Add(patches, typeof(ScammerGameManager), "ServerAddToMoneyPool", new[] { typeof(int), typeof(PlayerName) }, "AddMoney");
                Add(patches, typeof(ReviewWhiteboardController), "HandleDayEnded", new[] { typeof(int) }, "BuildDeck");
                Add(patches, typeof(ReviewWhiteboardController), "BeginReviewPhaseDeckLoad", new[] { typeof(int) }, "BuildDeck");
                Add(patches, typeof(ReviewWhiteboardController), "ScheduleNextAdvance", Type.EmptyTypes, "ManualNoOp");
                Add(patches, typeof(ReviewWhiteboardController), "ShouldPlayNarration", Type.EmptyTypes, "Narration");
                Add(patches, typeof(ReviewWhiteboardController), "StartFiredPresentation", Type.EmptyTypes, "FiredPresentation");
                Add(patches, typeof(ReviewWhiteboardController), "PlaySlideNarration", new[] { typeof(ReviewSlideDefinition) }, "QuotePresentation");
                harmony = new Harmony(PluginId);
                foreach (var patch in patches) harmony.Patch(patch.Key, prefix: patch.Value);
                Hooks.Init();
                Logger.LogInfo("HostBoss 1.5.0 enabled. Host a lobby and press F6. Game files are unchanged.");
            } catch (Exception e) {
                DisablePlugin();
                Logger.LogError("HostBoss could not enable; native boss remains active. " + e);
            }
        }

        static void Add(List<KeyValuePair<MethodInfo, HarmonyMethod>> patches, Type type, string name, Type[] parameters, string prefix) {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo target = type.GetMethod(name, flags, null, parameters, null);
            if (target == null) throw new MissingMethodException(type.FullName, name);
            MethodInfo hook = typeof(Plugin).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic);
            if (hook == null) throw new MissingMethodException(typeof(Plugin).FullName, prefix);
            patches.Add(new KeyValuePair<MethodInfo, HarmonyMethod>(target, new HarmonyMethod(hook)));
        }

        void DisablePlugin() {
            if (harmony == null) return;
            try { if (Hooks.Panel != null) Hooks.Panel.ReleaseHostControl(); }
            catch (Exception e) { Logger.LogError("Could not restore all review state during plugin shutdown: " + e); }
            finally {
                harmony.UnpatchSelf(); harmony = null;
                if (Hooks.Panel != null) {
                    Hooks.Panel.enabled = false;
                    Destroy(Hooks.Panel.gameObject);
                    Hooks.Panel = null;
                }
            }
        }

        static void GameAwake() { Hooks.Init(); }
        static bool ManualNoOp() { return !Hooks.ManualBossActive(); }
        static bool BuildDeck(ReviewWhiteboardController __instance, int __0) { return !Hooks.BuildDeck(__instance, __0); }
        static bool Narration(ref bool __result) {
            if (!Hooks.SuppressAutomaticBoss()) return true;
            __result = false; return false;
        }
        static bool FiredPresentation() { return !Hooks.SuppressAutomaticBoss(); }
        static bool QuotePresentation(ReviewWhiteboardController __instance, ReviewSlideDefinition __0) { return !Hooks.PresentQuoteOnly(__instance, __0); }
        static bool ReviewRoutine(ScammerGameManager __instance, int __0, ref IEnumerator __result) {
            if (!Hooks.ManualBossActive()) return true;
            __result = Hooks.ManualReview(__instance, __0); return false;
        }
        static bool AddMoney(PlayerName __1, ref bool __result) {
            if (Hooks.AllowEarnings(__1)) return true;
            __result = false; return false;
        }
    }
}
