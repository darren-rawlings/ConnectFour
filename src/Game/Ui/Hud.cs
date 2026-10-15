using System;
using ConnectFour.Core;
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
	public event Action<GameMode, Difficulty>? ModeChosen;

	private Label _score = null!;
	private Label _status = null!;
	private Label _hint = null!;
	private Button _clear = null!;
	private Button _mute = null!;
	private Control _menu = null!;
	private Button _pvp = null!;
	private Button _pvc = null!;
	private Control _difficultyPopup = null!;

	public void Build(Settings settings)
	{
		bool muted = settings.Muted;
		var title = MakeLabel("CONNECT FOUR", 60, Palette.Text);
		Anchor(title, Control.LayoutPreset.TopWide, top: 60, bottom: 140);
		AddChild(title);

		_score = MakeLabel("", 30, Palette.TextMuted);
		Anchor(_score, Control.LayoutPreset.TopWide, top: 140, bottom: 190);
		AddChild(_score);

		_status = MakeLabel("", 42, Palette.Text);
		Anchor(_status, Control.LayoutPreset.BottomWide, top: -335, bottom: -275);
		AddChild(_status);

		_hint = MakeLabel("Tap a column to drop  ·  Drag sideways to spin", 24, Palette.TextMuted);
		Anchor(_hint, Control.LayoutPreset.BottomWide, top: -268, bottom: -234);
		AddChild(_hint);

		_clear = MakeButton("Release Pieces", 34, Palette.Accent);
		Anchor(_clear, Control.LayoutPreset.CenterBottom, left: -190, right: 190, top: -255, bottom: -175);
		_clear.Pressed += () => ClearPressed?.Invoke();
		AddChild(_clear);

		var style = MakeButton("Style", 26, Palette.ButtonDark);
		Anchor(style, Control.LayoutPreset.BottomLeft, left: 20, right: 160, top: -118, bottom: -58);
		style.Pressed += () => _menu.Visible = true;
		AddChild(style);

		_mute = MakeButton("", 24, Palette.ButtonDark);
		Anchor(_mute, Control.LayoutPreset.BottomRight, left: -170, right: -20, top: -118, bottom: -58);
		_mute.Pressed += () =>
		{
			muted = !muted;
			SetMuteText(muted);
			MuteToggled?.Invoke(muted);
		};
		SetMuteText(muted);
		AddChild(_mute);

		BuildModeButtons(settings.Mode, settings.Difficulty);

		_menu = BuildMenu(settings.Board, settings.Pieces, settings.Backdrop);
		_menu.Visible = false;
		AddChild(_menu);
	}

	/// <summary>True while a menu or pop-up is open; the board ignores taps meanwhile.</summary>
	public bool MenuOpen => _menu.Visible || _difficultyPopup.Visible;

	public void OpenMenu() => _menu.Visible = true;

	public void OpenDifficultyPopup() => _difficultyPopup.Visible = true;

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

	/// <summary>
	/// Two stacked buttons between Style and Sound. Choosing "Player vs Computer" pops up the
	/// difficulty choice first; the mode only changes once a level is picked, and tapping
	/// outside the pop-up leaves everything as it was.
	/// </summary>
	private void BuildModeButtons(GameMode mode, Difficulty difficulty)
	{
		_pvp = MakeButton("Player vs Player", 24, Palette.ButtonDark);
		Anchor(_pvp, Control.LayoutPreset.CenterBottom, left: -188, right: 188, top: -150, bottom: -92);
		AddChild(_pvp);

		_pvc = MakeButton("", 24, Palette.ButtonDark);
		Anchor(_pvc, Control.LayoutPreset.CenterBottom, left: -188, right: 188, top: -84, bottom: -26);
		AddChild(_pvc);

		_pvp.Pressed += () => ChooseMode(GameMode.PlayerVsPlayer, difficulty);
		_pvc.Pressed += () => _difficultyPopup.Visible = true;

		// Transparent full-screen catcher: a tap anywhere outside the three buttons closes it.
		var catcher = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
		catcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		catcher.GuiInput += input =>
		{
			if (input is InputEventMouseButton { Pressed: true })
				_difficultyPopup.Visible = false;
		};
		_difficultyPopup = catcher;

		var panel = new PanelContainer();
		var box = new StyleBoxFlat { BgColor = Palette.Panel, AntiAliasing = true };
		box.SetCornerRadiusAll(28);
		box.SetContentMarginAll(14);
		panel.AddThemeStyleboxOverride("panel", box);
		Anchor(panel, Control.LayoutPreset.CenterBottom, left: -230, right: 230, top: -240, bottom: -158);
		catcher.AddChild(panel);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		panel.AddChild(row);
		foreach (var level in new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard })
		{
			var button = MakeButton(level.ToString(), 26, Palette.Accent);
			button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			button.Pressed += () =>
			{
				difficulty = level;
				_difficultyPopup.Visible = false;
				ChooseMode(GameMode.PlayerVsComputer, level);
			};
			row.AddChild(button);
		}
		catcher.Visible = false;
		AddChild(catcher);

		ShowMode(mode, difficulty);
	}

	private void ChooseMode(GameMode mode, Difficulty difficulty)
	{
		ShowMode(mode, difficulty);
		ModeChosen?.Invoke(mode, difficulty);
	}

	/// <summary>Highlights the active mode; the computer button names its level once one is chosen.</summary>
	private void ShowMode(GameMode mode, Difficulty difficulty)
	{
		bool vsComputer = mode == GameMode.PlayerVsComputer;
		_pvc.Text = vsComputer ? $"Player vs Computer: {difficulty}" : "Player vs Computer";
		Restyle(_pvp, vsComputer ? Palette.ButtonDark : Palette.Accent);
		Restyle(_pvc, vsComputer ? Palette.Accent : Palette.ButtonDark);
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
		Restyle(button, color);
		return button;
	}

	private static void Restyle(Button button, Color color)
	{
		foreach (var (state, shade) in new[] { ("normal", color), ("hover", color.Lightened(0.12f)), ("pressed", Palette.Accent), ("hover_pressed", Palette.Accent.Lightened(0.12f)) })
		{
			var box = new StyleBoxFlat { BgColor = shade, AntiAliasing = true };
			box.SetCornerRadiusAll(32);
			box.SetContentMarginAll(12);
			button.AddThemeStyleboxOverride(state, box);
		}
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
