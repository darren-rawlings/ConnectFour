using System;
using Godot;

namespace ConnectFour.Game.Ui;

/// <summary>
/// Title, score, turn status and buttons, plus the style menu. Everything that isn't a button
/// ignores the mouse so drags and taps reach the 3D board underneath.
/// </summary>
public partial class Hud : CanvasLayer
{
	public event Action? ClearPressed;
	public event Action<Style>? BoardChosen;
	public event Action<Style>? PiecesChosen;
	public event Action<Backdrop>? BackdropChosen;
	public event Action<bool>? MuteToggled;

	private Label _score = null!;
	private Label _status = null!;
	private Label _hint = null!;
	private Button _clear = null!;
	private Button _mute = null!;
	private Control _menu = null!;

	public void Build(Style board, Style pieces, Backdrop backdrop, bool muted)
	{
		var title = MakeLabel("CONNECT FOUR", 60, Palette.Text);
		Anchor(title, Control.LayoutPreset.TopWide, top: 60, bottom: 140);
		AddChild(title);

		_score = MakeLabel("", 30, Palette.TextMuted);
		Anchor(_score, Control.LayoutPreset.TopWide, top: 140, bottom: 190);
		AddChild(_score);

		_status = MakeLabel("", 42, Palette.Text);
		Anchor(_status, Control.LayoutPreset.BottomWide, top: -300, bottom: -240);
		AddChild(_status);

		_hint = MakeLabel("Tap a column to drop  ·  Drag sideways to spin", 24, Palette.TextMuted);
		Anchor(_hint, Control.LayoutPreset.BottomWide, top: -235, bottom: -200);
		AddChild(_hint);

		_clear = MakeButton("Release Pieces", 34, Palette.Accent);
		Anchor(_clear, Control.LayoutPreset.CenterBottom, left: -190, right: 190, top: -180, bottom: -100);
		_clear.Pressed += () => ClearPressed?.Invoke();
		AddChild(_clear);

		var style = MakeButton("Style", 28, Palette.ButtonDark);
		Anchor(style, Control.LayoutPreset.BottomLeft, left: 30, right: 190, top: -84, bottom: -24);
		style.Pressed += () => _menu.Visible = true;
		AddChild(style);

		_mute = MakeButton("", 28, Palette.ButtonDark);
		Anchor(_mute, Control.LayoutPreset.BottomRight, left: -230, right: -30, top: -84, bottom: -24);
		_mute.Pressed += () =>
		{
			muted = !muted;
			SetMuteText(muted);
			MuteToggled?.Invoke(muted);
		};
		SetMuteText(muted);
		AddChild(_mute);

		_menu = BuildMenu(board, pieces, backdrop);
		_menu.Visible = false;
		AddChild(_menu);
	}

	public bool MenuOpen => _menu.Visible;

	public void OpenMenu() => _menu.Visible = true;

	public void SetStatus(string text, Color color)
	{
		_status.Text = text;
		_status.AddThemeColorOverride("font_color", color);
	}

	public void SetScore(string text) => _score.Text = text;

	public void SetClearVisible(bool visible) => _clear.Visible = visible;

	/// <summary>The controls hint fades after the first move; it's only needed once.</summary>
	public void HideHint()
	{
		if (_hint.Modulate.A < 1f)
			return;
		CreateTween().TweenProperty(_hint, "modulate:a", 0f, 0.6f);
	}

	private void SetMuteText(bool muted) => _mute.Text = muted ? "Sound: Off" : "Sound: On";

	private Control BuildMenu(Style board, Style pieces, Backdrop backdrop)
	{
		// A light scrim blocks taps on the board while still showing style changes live behind it.
		var scrim = new ColorRect { Color = new Color(0, 0, 0, 0.25f), MouseFilter = Control.MouseFilterEnum.Stop };
		scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		var panel = new PanelContainer();
		var box = new StyleBoxFlat { BgColor = Palette.Panel, AntiAliasing = true };
		box.SetCornerRadiusAll(28);
		box.SetContentMarginAll(28);
		panel.AddThemeStyleboxOverride("panel", box);
		Anchor(panel, Control.LayoutPreset.BottomWide, left: 24, right: -24, top: -790, bottom: -24);
		scrim.AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 16);
		panel.AddChild(column);

		column.AddChild(MakeLabel("Board", 30, Palette.TextMuted));
		column.AddChild(MakeChoices(Styles.Choices, board, Styles.BoardName, s => BoardChosen?.Invoke(s)));
		column.AddChild(MakeLabel("Pieces", 30, Palette.TextMuted));
		column.AddChild(MakeChoices(Styles.Choices, pieces, s => Styles.Pieces(s).Name, s => PiecesChosen?.Invoke(s)));
		column.AddChild(MakeLabel("Background", 30, Palette.TextMuted));
		column.AddChild(MakeChoices(Scenery.Choices, backdrop, b => Scenery.Looks(b).Name, b => BackdropChosen?.Invoke(b)));

		var done = MakeButton("Done", 30, Palette.Accent);
		done.CustomMinimumSize = new Vector2(0, 70);
		done.Pressed += () => _menu.Visible = false;
		column.AddChild(done);
		return scrim;
	}

	private static GridContainer MakeChoices<T>(T[] choices, T selected, Func<T, string> name, Action<T> chosen)
		where T : struct, Enum
	{
		var grid = new GridContainer { Columns = 3 };
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 12);
		var group = new ButtonGroup();
		foreach (var choice in choices)
		{
			var button = MakeButton(name(choice), 26, Palette.ButtonDark);
			button.ToggleMode = true;
			button.ButtonGroup = group;
			button.ButtonPressed = choice.Equals(selected);
			button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			button.CustomMinimumSize = new Vector2(0, 64);
			button.Pressed += () => chosen(choice);
			grid.AddChild(button);
		}
		return grid;
	}

	private static Label MakeLabel(string text, int fontSize, Color color)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
		label.AddThemeConstantOverride("shadow_offset_y", 3);
		// A soft outline keeps text readable on every backdrop, from midnight to daylight.
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.55f));
		label.AddThemeConstantOverride("outline_size", 8);
		return label;
	}

	private static Button MakeButton(string text, int fontSize, Color color)
	{
		var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
		button.AddThemeFontSizeOverride("font_size", fontSize);
		foreach (var (state, shade) in new[] { ("normal", color), ("hover", color.Lightened(0.12f)), ("pressed", Palette.Accent), ("hover_pressed", Palette.Accent.Lightened(0.12f)) })
		{
			var box = new StyleBoxFlat { BgColor = shade, AntiAliasing = true };
			box.SetCornerRadiusAll(32);
			box.SetContentMarginAll(12);
			button.AddThemeStyleboxOverride(state, box);
		}
		return button;
	}

	private static void Anchor(Control control, Control.LayoutPreset preset, float left = 0, float right = 0, float top = 0, float bottom = 0)
	{
		control.SetAnchorsPreset(preset);
		control.OffsetLeft = left;
		control.OffsetRight = right;
		control.OffsetTop = top;
		control.OffsetBottom = bottom;
	}
}
