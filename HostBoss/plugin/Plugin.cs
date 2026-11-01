using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Kolkata.UI;
using Newtonsoft.Json.Linq;
using ScamWYF.Modding.Core;
using ScamWYF.Modding.Core.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HostBoss;

[BepInPlugin("com.proprogramer123.scamwyf.hostboss", "HostBoss", "1.60.6")]
[BepInProcess("Scam With Your Friends.exe")]
public sealed class Plugin : ScamMod
{
	public const string PluginId = "com.proprogramer123.scamwyf.hostboss";

	private bool patchesInstalled;

	private static string cameraJson;

	private static PresentationOptions cameraOptions;

	protected override void OnModLoad()
	{
		GameBuild.CheckUnityVersion(((ScamMod)this).ModLog, ((ScamMod)this).ModId, "6000.3.26f1");
		ReplayCompatibility.Validate();
        EnablePlugin();
		LaptopApps.Register((ScamMod)(object)this);
		Hotkeys.Register((ScamMod)(object)this, Key.F6, "Open HostBoss / Slide Maker", (Action)delegate
		{
			ModMenu.Close();
			Hooks.Init();
			Hooks.Panel.TogglePanel();
		}, Array.Empty<Key>());
		ModMenu.AddPage((ScamMod)(object)this, "HostBoss", (Action<VisualElement>)delegate(VisualElement page)
		{
			Widgets.Paragraph(page, "HostBoss: F6 opens host controls and the saved Slide Maker. Boss controls require hosting a lobby.");
			VisualElement visualElement = Widgets.WrapRow(page);
			Widgets.DescribedButton(visualElement, "Slide Maker", "Make, save and load presentations anytime.", (Action)delegate
			{
				ModMenu.Close();
				Hooks.Init();
				Hooks.Panel.OpenMaker();
			});
			Widgets.DescribedButton(visualElement, "Host controls", "Control your lobby's boss review.", (Action)delegate
			{
				ModMenu.Close();
				Hooks.Init();
				Hooks.Panel.OpenHostControls();
			});
			var array = PatchCoordinator.ForOwner(((ScamMod)this).ModId);
			foreach (var val in array)
			{
				Widgets.FieldRow(page, val.Method, val.Kinds);
			}
		});
	}

	protected override void OnModUnload()
	{
		DisablePlugin();
	}

	private void EnablePlugin()
	{
		if (patchesInstalled)
		{
			return;
		}
		try
		{
			Assembly[] array = new Assembly[2]
			{
				typeof(ScammerGameManager).Assembly,
				typeof(ReviewWhiteboardController).Assembly
			};
			for (int i = 0; i < array.Length; i++)
			{
				AssemblyName[] referencedAssemblies = array[i].GetReferencedAssemblies();
				for (int j = 0; j < referencedAssemblies.Length; j++)
				{
					if (referencedAssemblies[j].Name == "HostBoss.Runtime")
					{
						throw new InvalidOperationException("Remove the legacy HostBoss DLL patches with HostBoss Patcher (Remove / Normal Play), then restart before using this plugin.");
					}
				}
			}
			List<KeyValuePair<MethodInfo, HarmonyMethod>> list = new List<KeyValuePair<MethodInfo, HarmonyMethod>>();
			Add(list, typeof(ScammerGameManager), "Awake", Type.EmptyTypes, "GameAwake");
			Add(list, typeof(ScammerGameManager), "ServerToggleReviewSkipVote", new Type[1] { typeof(PlayerName) }, "ManualNoOp");
			Add(list, typeof(ScammerGameManager), "ServerReviewPhaseRoutine", new Type[1] { typeof(int) }, "ReviewRoutine");
			Add(list, typeof(ScammerGameManager), "ServerAddToMoneyPool", new Type[2]
			{
				typeof(int),
				typeof(PlayerName)
			}, "AddMoney");
			Add(list, typeof(ReviewWhiteboardController), "HandleDayEnded", new Type[1] { typeof(int) }, "BuildDeck");
			Add(list, typeof(ReviewWhiteboardController), "BeginReviewPhaseDeckLoad", new Type[1] { typeof(int) }, "BuildDeck");
			Add(list, typeof(ReviewWhiteboardController), "ScheduleNextAdvance", Type.EmptyTypes, "ManualNoOp");
			Add(list, typeof(ReviewWhiteboardController), "ShouldPlayNarration", Type.EmptyTypes, "Narration");
			Add(list, typeof(ReviewWhiteboardController), "StartFiredPresentation", Type.EmptyTypes, "FiredPresentation");
			Add(list, typeof(ReviewWhiteboardController), "PlaySlideNarration", new Type[1] { typeof(ReviewSlideDefinition) }, "QuotePresentation");
			Add(list, typeof(ReviewWhiteboardController), "LoadTexture", new Type[1] { typeof(string) }, "EmbeddedPng");
			Add(list, typeof(ReviewWhiteboardController), "GetBossWebcamCornerClass", new Type[1] { typeof(int) }, "CameraCorner");
			Add(list, typeof(KolkataOsController), "IsAppAvailableOnDesktop", new Type[3]
			{
				typeof(KolkataOsController).Assembly.GetType("Kolkata.UI.KolkataOsAppDefinition", throwOnError: true, ignoreCase: false),
				typeof(bool),
				typeof(bool)
			}, "LaptopAvailability");
			Add(list, typeof(KolkataOsController), "EnsureAppContentBuilt", new Type[1] { typeof(KolkataOsController).Assembly.GetType("Kolkata.UI.KolkataOsAppRuntime", throwOnError: true, ignoreCase: false) }, "LaptopContent");
			MethodInfo method = typeof(KolkataOsController).GetMethod("BuildDesktop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				throw new MissingMethodException("KolkataOsController", "BuildDesktop");
			}
			Add(list, typeof(KolkataOsController), "BuildDesktop", Array.ConvertAll(method.GetParameters(), (ParameterInfo parameter) => parameter.ParameterType), "LaptopDefinitions");
			patchesInstalled = true;
			foreach (KeyValuePair<MethodInfo, HarmonyMethod> item in list)
			{
				if (!PatchCoordinator.TryPatch((ScamMod)(object)this, (MethodBase)item.Key, "HostBoss " + item.Key.Name, item.Value, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null))
				{
					throw new InvalidOperationException("Could not install HostBoss hook " + item.Key.Name);
				}
			}
			Hooks.Init();
			((ScamMod)this).ModLog.LogInfo((object)"HostBoss 1.60.6 enabled with mod-lib. F1: shared menu. F6: HostBoss / Slide Maker.");
		}
		catch (Exception ex)
		{
			DisablePlugin();
			((ScamMod)this).ModLog.LogError((object)("HostBoss could not enable; native boss remains active. " + ex));
			throw;
		}
	}

	private static void Add(List<KeyValuePair<MethodInfo, HarmonyMethod>> patches, Type type, string name, Type[] parameters, string prefix)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null);
		if (method == null)
		{
			throw new MissingMethodException(type.FullName, name);
		}
		MethodInfo method2 = typeof(Plugin).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic);
		if (method2 == null)
		{
			throw new MissingMethodException(typeof(Plugin).FullName, prefix);
		}
		patches.Add(new KeyValuePair<MethodInfo, HarmonyMethod>(method, new HarmonyMethod(method2)));
	}

	private void DisablePlugin()
	{
		if (!patchesInstalled)
		{
			return;
		}
		try
		{
			if (Hooks.Panel != null)
			{
				Hooks.Panel.ReleaseHostControl();
			}
		}
		catch (Exception ex)
		{
			((ScamMod)this).ModLog.LogError((object)("Could not restore all review state during plugin shutdown: " + ex));
		}
		finally
		{
			LaptopApps.Shutdown();
			PatchCoordinator.UnpatchOwner("com.proprogramer123.scamwyf.hostboss");
			patchesInstalled = false;
			SlideImages.Clear();
			cameraJson = null;
			cameraOptions = null;
			if (Hooks.Panel != null)
			{
				Hooks.Panel.enabled = false;
				UnityEngine.Object.Destroy(Hooks.Panel.gameObject);
				Hooks.Panel = null;
			}
		}
	}

	private static void GameAwake()
	{
		Hooks.Init();
	}

	private static void LaptopDefinitions(KolkataOsController __instance)
	{
		LaptopApps.EnsureDefinitions(__instance);
	}

	private static bool LaptopAvailability(object __0, bool __1, ref bool __result)
	{
		if (!LaptopApps.Active || __0 == null || !LaptopApps.Owns((string)Hooks.Get(__0, "id")))
		{
			return true;
		}
		__result = __1;
		return false;
	}

	private static bool LaptopContent(object __0)
	{
		if (!LaptopApps.Active || __0 == null || Hooks.Get(__0, "Definition") == null || !LaptopApps.Owns((string)Hooks.Get(Hooks.Get(__0, "Definition"), "id")))
		{
			return true;
		}
		LaptopApps.Build(__0);
		return false;
	}

	private static bool EmbeddedPng(string __0, ref Texture2D __result)
	{
		return SlideImages.Load(__0, ref __result);
	}

	private static bool CameraCorner(ReviewWhiteboardController __instance, int __0, ref string __result)
	{
		string networkm_syncedDeckJson = __instance.Networkm_syncedDeckJson;
		if (networkm_syncedDeckJson != cameraJson)
		{
			cameraJson = networkm_syncedDeckJson;
			cameraOptions = null;
			try
			{
				if (!string.IsNullOrEmpty(networkm_syncedDeckJson) && networkm_syncedDeckJson.Length <= 60000)
				{
					cameraOptions = JObject.Parse(networkm_syncedDeckJson)["hostBossOptions"]?.ToObject<PresentationOptions>();
				}
			}
			catch (Exception)
			{
				cameraOptions = null;
			}
		}
		if (cameraOptions == null || cameraOptions.Slides == null)
		{
			return true;
		}
		ReviewSlideDeckDefinition reviewSlideDeckDefinition = (ReviewSlideDeckDefinition)Hooks.Get(__instance, "m_deck");
		if (reviewSlideDeckDefinition == null || reviewSlideDeckDefinition.slides == null || __0 < 0 || __0 >= reviewSlideDeckDefinition.slides.Length)
		{
			return true;
		}
		string text = SlideDocument.CameraClass(cameraOptions, reviewSlideDeckDefinition.slides[__0].id);
		if (text == null)
		{
			return true;
		}
		__result = text;
		return false;
	}

	private static bool ManualNoOp()
	{
		return !Hooks.ManualBossActive();
	}

	private static bool BuildDeck(ReviewWhiteboardController __instance, int __0)
	{
		return !Hooks.BuildDeck(__instance, __0);
	}

	private static bool Narration(ReviewWhiteboardController __instance, ref bool __result)
	{
		if (!Hooks.SuppressAutomaticBoss())
		{
			return true;
		}
		if (Hooks.Panel != null && Hooks.Panel.CanNarrateSlide(__instance))
		{
			Hooks.Set(__instance, "m_playNarration", true);
			return true;
		}
		__result = false;
		return false;
	}

	private static bool FiredPresentation()
	{
		return !Hooks.SuppressAutomaticBoss();
	}

	private static bool QuotePresentation(ReviewWhiteboardController __instance, ReviewSlideDefinition __0)
	{
		return !Hooks.PresentQuoteOnly(__instance, __0);
	}

	private static bool ReviewRoutine(ScammerGameManager __instance, int __0, ref IEnumerator __result)
	{
		if (!Hooks.ManualBossActive())
		{
			return true;
		}
		__result = Hooks.ManualReview(__instance, __0);
		return false;
	}

	private static bool AddMoney(PlayerName __1, ref bool __result)
	{
		if (Hooks.AllowEarnings(__1))
		{
			return true;
		}
		__result = false;
		return false;
	}
}
