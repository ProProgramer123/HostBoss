using System;
using System.Collections.Generic;
using ScamWYF.Modding.Core.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace HostBoss;

public sealed class LaptopView
{
	private readonly VisualElement root;

	private readonly bool maker;

	private readonly List<Action> refresh = new List<Action>();

	private IVisualElementScheduledItem schedule;

	private VisualElement body;

	private Label notice;

	private bool authorized;

	private bool stopped;

	private Func<bool> stale;

	public bool CanUse
	{
		get
		{
			if (!stopped && LaptopApps.Active)
			{
				return Hooks.IsHost();
			}
			return false;
		}
	}

	public LaptopView(VisualElement root, bool maker)
	{
		this.root = root;
		this.maker = maker;
	}

	public bool Uses(VisualElement element)
	{
		return root == element;
	}

	public void Start()
	{
		Rebuild();
		schedule = root.schedule.Execute(Tick).Every(250L);
	}

	public void Stop()
	{
		stopped = true;
		schedule?.Pause();
		root.Clear();
		root.Add(new Label("HostBoss is disabled."));
	}

	public void Run(Action action, bool rebuild = false)
	{
		if (!CanUse || (stale != null && stale()))
		{
			Rebuild();
			return;
		}
		try
		{
			action();
			if (rebuild)
			{
				Rebuild();
			}
			else
			{
				Tick();
			}
		}
		catch (Exception ex)
		{
			notice.text = ex.Message;
			Debug.LogWarning("[HostBoss] Laptop: " + ex.Message);
		}
	}

	public void Rebuild()
	{
		refresh.Clear();
		stale = null;
		root.Clear();
		authorized = CanUse;
		if (UiTheme.RowHeight < 1f)
		{
			UiTheme.Sample(root);
		}
		root.style.flexGrow = 1f;
		root.style.minHeight = 0f;
		if (!authorized)
		{
			root.Add(new Label("You must be host to use HostBoss apps!"));
			StyleLaptop(root);
			return;
		}
		Hooks.Init();
		notice = Widgets.Paragraph(root, "");
		ScrollView scrollView = Widgets.Scroll(root);
		body = Widgets.Column((VisualElement)scrollView);
		body.style.paddingLeft = 8f;
		body.style.paddingRight = 8f;
		body.style.paddingBottom = 12f;
		try
		{
			if (maker)
			{
				Hooks.Panel.BuildLaptopMaker(this, body);
			}
			else
			{
				Hooks.Panel.BuildLaptopControls(this, body);
			}
		}
		catch (Exception ex)
		{
			notice.text = "HostBoss app could not open: " + ex.Message;
			Debug.LogError("[HostBoss] Laptop app: " + ex);
		}
		StyleLaptop(root);
		Tick();
	}

	public static void StyleLaptop(VisualElement element)
	{
		Color color = new Color(0.08f, 0.12f, 0.19f, 1f);
		element.style.color = color;
		if (element is TextElement)
		{
			element.style.fontSize = 14f;
			element.style.whiteSpace = WhiteSpace.Normal;
		}
		if (element is Button button)
		{
			button.style.height = 32f;
			button.style.minHeight = 32f;
			button.style.flexShrink = 0f;
			button.style.backgroundColor = (element.ClassListContains("hostboss-selected") ? new Color(0.74f, 0.84f, 0.98f, 1f) : new Color(0.9f, 0.93f, 0.97f, 1f));
			IStyle style = button.style;
			IStyle style2 = button.style;
			IStyle style3 = button.style;
			StyleFloat styleFloat = (button.style.borderBottomWidth = 1f);
			StyleFloat styleFloat3 = (style3.borderTopWidth = styleFloat);
			StyleFloat borderLeftWidth = (style2.borderRightWidth = styleFloat3);
			style.borderLeftWidth = borderLeftWidth;
			IStyle style4 = button.style;
			IStyle style5 = button.style;
			IStyle style6 = button.style;
			StyleColor styleColor = (button.style.borderBottomColor = new Color(0.6f, 0.67f, 0.77f, 1f));
			StyleColor styleColor3 = (style6.borderTopColor = styleColor);
			StyleColor borderLeftColor = (style5.borderRightColor = styleColor3);
			style4.borderLeftColor = borderLeftColor;
		}
		if (element is TextField textField)
		{
			textField.style.flexShrink = 0f;
			textField.style.minHeight = (textField.multiline ? 80 : 32);
		}
		if (element.ClassListContains("unity-text-field__input"))
		{
			element.style.backgroundColor = Color.white;
			element.style.color = color;
			IStyle style7 = element.style;
			StyleLength paddingLeft = (element.style.paddingRight = 6f);
			style7.paddingLeft = paddingLeft;
		}
		if (element is Toggle)
		{
			element.style.height = StyleKeyword.Auto;
			element.style.minHeight = 32f;
			element.style.flexShrink = 0f;
		}
		for (int i = 0; i < element.childCount; i++)
		{
			StyleLaptop(element[i]);
		}
	}

	private void Tick()
	{
		if (stopped)
		{
			return;
		}
		if (authorized != CanUse)
		{
			Rebuild();
		}
		else
		{
			if (!authorized)
			{
				return;
			}
			if (stale != null && stale())
			{
				Rebuild();
				return;
			}
			Action[] array = refresh.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				array[i]();
			}
		}
	}

	public void Watch(Action action)
	{
		refresh.Add(action);
	}

	public void RebuildWhen(Func<bool> condition)
	{
		stale = condition;
	}

	public Label Live(VisualElement parent, Func<string> text)
	{
		Label label = Widgets.Paragraph(parent, text());
		Watch(delegate
		{
			label.text = text();
		});
		return label;
	}

	public Button Button(VisualElement parent, string text, Action action, Func<bool> enabled = null, bool rebuild = false)
	{
		Button button = Widgets.Button(parent, text, (Action)delegate
		{
			Run(delegate
			{
				if (enabled == null || enabled())
				{
					action();
				}
			}, rebuild);
		});
		if (enabled != null)
		{
			Watch(delegate
			{
				button.SetEnabled(CanUse && enabled());
			});
			button.SetEnabled(enabled());
		}
		return button;
	}

	public TextField Field(VisualElement parent, string title, string text, Action<string> change, int max = 200, bool multiline = false)
	{
		TextField textField = Widgets.TextField(parent, title, text, (Action<string>)delegate(string value)
		{
			Run(delegate
			{
				change(value);
			});
		}, multiline);
		textField.maxLength = max;
		if (multiline)
		{
			textField.style.minHeight = 72f;
		}
		return textField;
	}

	public void Toggle(VisualElement parent, string title, Func<bool> value, Action<bool> change, bool rebuild = false, Func<bool> enabled = null)
	{
		Toggle toggle = Widgets.Toggle(parent, title, value(), (Action<bool>)delegate(bool next)
		{
			Run(delegate
			{
				if (enabled == null || enabled())
				{
					change(next);
				}
			}, rebuild);
		});
		Watch(delegate
		{
			toggle.SetValueWithoutNotify(value());
			toggle.SetEnabled(enabled == null || enabled());
		});
	}

	public void Choices(VisualElement parent, string title, string current, string[] values, Action<string> change)
	{
		Widgets.Heading(parent, title);
		VisualElement parent2 = Widgets.WrapRow(parent);
		foreach (string value in values)
		{
			Button button = Button(parent2, value, delegate
			{
				change(value);
			}, null, rebuild: true);
			Widgets.SetSelected((VisualElement)button, value == current);
			button.EnableInClassList("hostboss-selected", value == current);
		}
	}
}
