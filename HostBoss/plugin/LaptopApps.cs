using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using ScamWYF.Modding.Core;
using ScamWYF.Modding.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace HostBoss;

public sealed class LaptopApps
{
	public const string ControlsId = "hostboss-controls";

	public const string MakerId = "hostboss-slide-maker";

	public const string HostRequired = "You must be host to use HostBoss apps!";

	public static bool Active;

	private static readonly List<WeakReference> views = new List<WeakReference>();

	public static bool Owns(string id)
	{
		if (!(id == "hostboss-controls"))
		{
			return id == "hostboss-slide-maker";
		}
		return true;
	}

	public static void EnsureDefinitions(object controller)
	{
		if (!Active || controller == null)
		{
			return;
		}
		IList list = (IList)Hooks.Get(controller, "m_appDefinitions");
		Type type = controller.GetType().Assembly.GetType("Kolkata.UI.KolkataOsAppDefinition");
		var registeredApps = ContentRegistry.RegisteredApps;
		foreach (var val in registeredApps)
		{
			if (!Owns(val.Id))
			{
				continue;
			}
			bool flag = false;
			foreach (object item in list)
			{
				if ((string)Hooks.Get(item, "id") == val.Id)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				object obj = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(val.Spec), type, null);
				if (obj == null)
				{
					throw new InvalidOperationException("Could not deserialize " + val.Id);
				}
				Hooks.Call(controller, "NormalizeDefinition", obj);
				list.Add(obj);
				Debug.Log("[HostBoss] Added laptop app definition: " + val.Id);
			}
		}
	}

	public static void Register(ScamMod owner)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Expected O, but got Unknown
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Expected O, but got Unknown
		//IL_0131: Expected O, but got Unknown
		Active = true;
		bool[] array = new bool[2] { false, true };
		foreach (bool flag in array)
		{
			if (!ContentRegistry.RegisterApp(owner, new OsAppSpec
			{
				Id = (flag ? "hostboss-slide-maker" : "hostboss-controls"),
				Title = (flag ? "Slide Maker" : "HostBoss"),
				IconResource = (flag ? "KolkataOS/SVG/files" : "KolkataOS/SVG/script-text"),
				IconSvg = (flag ? "svg/files.svg" : "svg/script-text.svg"),
				ContentType = "placeholder",
				ShowOnDesktop = true,
				DesktopOrder = (flag ? 96 : 95),
				WindowLeft = 110f,
				WindowTop = 55f,
				WindowWidth = 850f,
				WindowHeight = 640f,
				MinWindowWidth = 550f,
				MinWindowHeight = 400f,
				AccentColor = "#376FC4",
				InitialState = "closed",
				Access = new OsAppAccessSpec
				{
					Mode = "default",
					UnlockDay = 0
				},
				Content = new OsAppContentSpec
				{
					Title = (flag ? "Slide Maker" : "HostBoss")
				}
			}))
			{
				throw new InvalidOperationException("Could not register HostBoss laptop app.");
			}
		}
	}

	public static void Build(object app)
	{
		VisualElement visualElement = (VisualElement)Hooks.Get(app, "WindowContent");
		if (visualElement == null)
		{
			return;
		}
		WeakReference[] array = views.ToArray();
		foreach (WeakReference weakReference in array)
		{
			if (weakReference.Target is LaptopView laptopView && laptopView.Uses(visualElement))
			{
				laptopView.Stop();
				views.Remove(weakReference);
			}
		}
		visualElement.Clear();
		Hooks.Set(app, "NeedsAutoSize", false);
		LaptopView laptopView2 = new LaptopView(visualElement, (string)Hooks.Get(Hooks.Get(app, "Definition"), "id") == "hostboss-slide-maker");
		views.RemoveAll((WeakReference reference) => !reference.IsAlive);
		views.Add(new WeakReference(laptopView2));
		laptopView2.Start();
		Hooks.Set(app, "ContentBuilt", true);
	}

	public static void Shutdown()
	{
		Active = false;
		foreach (WeakReference view in views)
		{
			if (view.Target is LaptopView laptopView)
			{
				laptopView.Stop();
			}
		}
		views.Clear();
	}
}
