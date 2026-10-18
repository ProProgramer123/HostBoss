// Temporary test plugin: compile/install only for an isolated startup check, then remove.
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using HostBoss;
using UnityEngine;
using ScamWYF.Modding.Core;
using Kolkata.UI;
using UnityEngine.UIElements;
using HarmonyLib;

[BepInPlugin("com.proprogramer123.hostboss.slidemakersmoketest", "HostBoss Slide Maker Smoke Test", "1.0.0")]
[BepInDependency(HostBoss.Plugin.PluginId)]
public sealed class SlideMakerSmoke : BaseUnityPlugin {
    static bool DenyHost(ref bool __result) { __result = false; return false; }
    int repaints;
    void OnGUI() { if (Event.current.type == EventType.Repaint) repaints++; }
    IEnumerator Start() {
        yield return new WaitForSeconds(3);
        try {
            var panel = Hooks.Panel;
            if (panel == null) throw new Exception("HostBoss panel missing.");
            ScamMod registered;
            if (!ModRegistry.TryGet(HostBoss.Plugin.PluginId, out registered) || registered.State != ModState.Active) throw new Exception("HostBoss is not active in mod-lib.");
            if (PatchCoordinator.ForOwner(HostBoss.Plugin.PluginId).Length != 21) throw new Exception("Expected fifteen HostBoss hooks plus six shared content hooks.");
            if (Hotkeys.ForOwner(HostBoss.Plugin.PluginId).Length != 1) throw new Exception("Expected one registered F6 hotkey.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var maker = (SlideMaker)typeof(BossPanel).GetField("maker", flags).GetValue(panel);
            maker.EnsureLoaded();
            var laptopObject = new GameObject("HostBoss laptop verification");
            laptopObject.SetActive(false);
            try {
                var controller = laptopObject.AddComponent<KolkataOsController>();
                LaptopApps.EnsureDefinitions(controller);
                var definitions = (IList)Hooks.Get(controller, "m_appDefinitions");
                if (definitions.Count != 2) throw new Exception("Laptop definitions were not inserted.");
                LaptopApps.EnsureDefinitions(controller);
                if (definitions.Count != 2) throw new Exception("Laptop apps duplicated.");
                var available = typeof(KolkataOsController).GetMethod("IsAppAvailableOnDesktop", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (object definition in definitions) {
                    if (!(bool)Hooks.Get(definition, "showOnDesktop")) throw new Exception("App desktop icon disabled.");
                    if (!(bool)available.Invoke(null, new object[] { definition, true, true })) throw new Exception("App missing on laptop.");
                    if ((bool)available.Invoke(null, new object[] { definition, false, true })) throw new Exception("App leaked to employee workstation.");
                }
                var testHarmony = new Harmony("hostboss.runtime.test.guest");
                try {
                    testHarmony.Patch(typeof(Hooks).GetMethod("IsHost"), prefix: new HarmonyMethod(typeof(SlideMakerSmoke).GetMethod("DenyHost", BindingFlags.Static | BindingFlags.NonPublic)));
                    var guestRoot = new VisualElement(); var guestView = new LaptopView(guestRoot, true); guestView.Start();
                    if (guestRoot.childCount != 1 || ((Label)guestRoot[0]).text != LaptopApps.HostRequired) throw new Exception("Guest message mismatch.");
                    bool actionRan = false; guestView.Run(() => actionRan = true);
                    if (actionRan) throw new Exception("Guest app action executed.");
                    guestView.Stop();
                } finally { testHarmony.UnpatchSelf(); }
                var nativeRoot = new VisualElement(); var nativeView = new LaptopView(nativeRoot, true);
                panel.BuildLaptopMaker(nativeView, nativeRoot);
                if (nativeRoot.childCount < 20) throw new Exception("Native slide maker controls missing.");
                LaptopView.StyleLaptop(nativeRoot);
                VerifyLaptopStyles(nativeRoot);
                Logger.LogInfo("LAPTOP_APPS_PASS: real stripped JSON, two native definitions, no duplicates, laptop-only availability, exact guest message and rejected guest action.");
            } finally { Destroy(laptopObject); }
            string liveName = "HostBoss save verification " + Guid.NewGuid().ToString("N");
            if (!maker.Document.DirectoryPath.StartsWith(Path.GetFullPath(Application.persistentDataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("Saves are not in the game's user-data folder: " + maker.Document.DirectoryPath);
            maker.Document.Save(liveName + ".json");
            string liveFile = maker.Document.FilePath(liveName);
            if (!File.Exists(liveFile)) throw new Exception("User-data save file was not written.");
            maker.Document.Load(liveName);
            Logger.LogInfo("SAVE_USER_DATA_PASS: " + maker.Document.DirectoryPath);
            File.Delete(liveFile); // This uniquely named file was created by this test above.
            string fixture = Path.Combine(Paths.GameRootPath, "HostBoss", "slide-maker-smoke");
            Directory.CreateDirectory(fixture);
            maker.Document = new SlideDocument(Path.Combine(fixture, "saves"), maker.Document.Deck);
            var texture = new Texture2D(800, 600, TextureFormat.RGBA32, false);
            try {
                var pixels = new Color[800 * 600];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color((i % 800) / 800f, (i / 800) / 600f, .4f, .7f);
                texture.SetPixels(pixels); texture.Apply();
                string png = Path.Combine(fixture, "test.png");
                File.WriteAllBytes(png, ImageConversion.EncodeToPNG(texture));
                maker.Document.Deck.slides[0].imagePath = SlideImages.Import(png);
            } finally { Destroy(texture); }
            var embedded = SlideImages.Get(maker.Document.Deck.slides[0].imagePath);
            if (embedded == null || embedded.width > 512 || embedded.height > 512) throw new Exception("PNG import or decoded size check failed.");
            maker.Document.Deck.slides[0].quote = "Smoke test custom quote";
            maker.Document.Deck.slides[0].narration = "Boss TTS test text.";
            maker.Document.Save("Smoke Test"); maker.Document.Load("Smoke Test");
            if (maker.Document.Deck.slides[0].quote != "Smoke test custom quote") throw new Exception("Save/load mismatch.");
            if (maker.Document.Deck.slides[0].narration != "Boss TTS test text.") throw new Exception("Narration save/load mismatch.");
            typeof(BossPanel).GetField("makerVisible", flags).SetValue(panel, true);
            typeof(BossPanel).GetMethod("Toggle", flags).Invoke(panel, new object[] {true});
            Logger.LogInfo("SLIDE_MAKER_SMOKE_DATA_PASS: mod-lib registration, fifteen coordinated patches, one F6 binding, templates, real PNG resize/decode, narration save/load.");
        } catch (Exception e) { Logger.LogError("SLIDE_MAKER_SMOKE_FAIL: " + e); yield break; }
        yield return new WaitForSeconds(5);
        if (repaints == 0) Logger.LogError("SLIDE_MAKER_SMOKE_FAIL: no IMGUI repaint events; this launch mode did not test the UI.");
        else Logger.LogInfo("SLIDE_MAKER_SMOKE_DRAW_COMPLETE: " + repaints + " repaint events; inspect Unity log for IMGUI exceptions.");
    }
    static void VerifyLaptopStyles(VisualElement element) {
        if (element is TextElement && (element.style.color.value.a != 1 || element.style.color.value.r > .2f)) throw new Exception("Laptop text is transparent or lacks contrast.");
        if (element is Button && element.style.minHeight.value.value < 30) throw new Exception("Laptop button collapsed.");
        for (int i = 0; i < element.childCount; i++) VerifyLaptopStyles(element[i]);
    }
}
