using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Kolkata.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using ScamWYF.Modding.Core;
using ScamWYF.Modding.Core.Ui;

[assembly: AssemblyTitle("HostBoss BepInEx Plugin")]
[assembly: AssemblyVersion("1.60.0.0")]
[assembly: AssemblyFileVersion("1.60.0.0")]

namespace HostBoss {
    [BepInPlugin(PluginId, "HostBoss", "1.60.0")]
    [BepInProcess("Scam With Your Friends.exe")]
    public sealed class Plugin : ScamMod {
        public const string PluginId = "com.proprogramer123.scamwyf.hostboss";
        bool patchesInstalled;
        static string cameraJson;
        static PresentationOptions cameraOptions;
        protected override void OnModLoad() {
            GameBuild.CheckUnityVersion(ModLog, ModId, "6000.3.10f1");
            EnablePlugin();
            LaptopApps.Register(this);
            Hotkeys.Register(this, Key.F6, "Open HostBoss / Slide Maker", () => { ModMenu.Close(); Hooks.Init(); Hooks.Panel.TogglePanel(); });
            ModMenu.AddPage(this, "HostBoss", page => {
                Widgets.Paragraph(page, "HostBoss: F6 opens host controls and the saved Slide Maker. Boss controls require hosting a lobby.");
                var row = Widgets.WrapRow(page);
                Widgets.DescribedButton(row, "Slide Maker", "Make, save and load presentations anytime.", () => { ModMenu.Close(); Hooks.Init(); Hooks.Panel.OpenMaker(); });
                Widgets.DescribedButton(row, "Host controls", "Control your lobby's boss review.", () => { ModMenu.Close(); Hooks.Init(); Hooks.Panel.OpenHostControls(); });
                foreach (var patch in PatchCoordinator.ForOwner(ModId)) Widgets.FieldRow(page, patch.Method, patch.Kinds);
            });
        }
        protected override void OnModUnload() { DisablePlugin(); }

        void EnablePlugin() {
            if (patchesInstalled) return;
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
                Add(patches, typeof(ReviewWhiteboardController), "LoadTexture", new[] { typeof(string) }, "EmbeddedPng");
                Add(patches, typeof(ReviewWhiteboardController), "GetBossWebcamCornerClass", new[] { typeof(int) }, "CameraCorner");
                Add(patches, typeof(KolkataOsController), "IsAppAvailableOnDesktop", new[] { typeof(KolkataOsController).Assembly.GetType("Kolkata.UI.KolkataOsAppDefinition", true, false), typeof(bool), typeof(bool) }, "LaptopAvailability");
                Add(patches, typeof(KolkataOsController), "EnsureAppContentBuilt", new[] { typeof(KolkataOsController).Assembly.GetType("Kolkata.UI.KolkataOsAppRuntime", true, false) }, "LaptopContent");
                var desktop = typeof(KolkataOsController).GetMethod("BuildDesktop", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (desktop == null) throw new MissingMethodException("KolkataOsController", "BuildDesktop");
                Add(patches, typeof(KolkataOsController), "BuildDesktop", Array.ConvertAll(desktop.GetParameters(), parameter => parameter.ParameterType), "LaptopDefinitions");
                patchesInstalled = true;
                foreach (var patch in patches) if (!PatchCoordinator.TryPatch(this, patch.Key, "HostBoss " + patch.Key.Name, prefix: patch.Value)) throw new InvalidOperationException("Could not install HostBoss hook " + patch.Key.Name);
                Hooks.Init();
                ModLog.LogInfo("HostBoss 1.60.0 enabled with mod-lib. F1: shared menu. F6: HostBoss / Slide Maker.");
            } catch (Exception e) {
                DisablePlugin();
                ModLog.LogError("HostBoss could not enable; native boss remains active. " + e);
                throw;
            }
        }

        static void Add(List<KeyValuePair<MethodInfo, HarmonyMethod>> patches, Type type, string name, Type[] parameters, string prefix) {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo target = type.GetMethod(name, flags, null, parameters, null);
            if (target == null) throw new MissingMethodException(type.FullName, name);
            MethodInfo hook = typeof(Plugin).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic);
            if (hook == null) throw new MissingMethodException(typeof(Plugin).FullName, prefix);
            patches.Add(new KeyValuePair<MethodInfo, HarmonyMethod>(target, new HarmonyMethod(hook)));
        }

        void DisablePlugin() {
            if (!patchesInstalled) return;
            try { if (Hooks.Panel != null) Hooks.Panel.ReleaseHostControl(); }
            catch (Exception e) { ModLog.LogError("Could not restore all review state during plugin shutdown: " + e); }
            finally {
                LaptopApps.Shutdown();
                PatchCoordinator.UnpatchOwner(PluginId); patchesInstalled = false;
                SlideImages.Clear();
                cameraJson = null; cameraOptions = null;
                if (Hooks.Panel != null) {
                    Hooks.Panel.enabled = false;
                    Destroy(Hooks.Panel.gameObject);
                    Hooks.Panel = null;
                }
            }
        }

        static void GameAwake() { Hooks.Init(); }
        static void LaptopDefinitions(KolkataOsController __instance) { LaptopApps.EnsureDefinitions(__instance); }
        static bool LaptopAvailability(object __0, bool __1, ref bool __result) {
            if (!LaptopApps.Active || __0 == null || !LaptopApps.Owns((string)Hooks.Get(__0, "id"))) return true;
            __result = __1; return false;
        }
        static bool LaptopContent(object __0) {
            if (!LaptopApps.Active || __0 == null || Hooks.Get(__0, "Definition") == null || !LaptopApps.Owns((string)Hooks.Get(Hooks.Get(__0, "Definition"), "id"))) return true;
            LaptopApps.Build(__0);
            return false;
        }
        static bool EmbeddedPng(string __0, ref Texture2D __result) { return SlideImages.Load(__0, ref __result); }
        static bool CameraCorner(ReviewWhiteboardController __instance, int __0, ref string __result) {
            string json = __instance.Networkm_syncedDeckJson;
            if (json != cameraJson) {
                cameraJson = json; cameraOptions = null;
                try { if (!string.IsNullOrEmpty(json) && json.Length <= 60000) cameraOptions = Newtonsoft.Json.Linq.JObject.Parse(json)["hostBossOptions"]?.ToObject<PresentationOptions>(); }
                catch (Exception) { cameraOptions = null; }
            }
            if (cameraOptions == null || cameraOptions.Slides == null) return true;
            var deck = (ReviewSlideDeckDefinition)Hooks.Get(__instance, "m_deck");
            if (deck == null || deck.slides == null || __0 < 0 || __0 >= deck.slides.Length) return true;
            string corner = SlideDocument.CameraClass(cameraOptions, deck.slides[__0].id);
            if (corner == null) return true;
            __result = corner; return false;
        }
        static bool ManualNoOp() { return !Hooks.ManualBossActive(); }
        static bool BuildDeck(ReviewWhiteboardController __instance, int __0) { return !Hooks.BuildDeck(__instance, __0); }
        static bool Narration(ReviewWhiteboardController __instance, ref bool __result) {
            if (!Hooks.SuppressAutomaticBoss()) return true;
            if (Hooks.Panel != null && Hooks.Panel.CanNarrateSlide(__instance)) { Hooks.Set(__instance, "m_playNarration", true); return true; }
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
