using System;
using System.Collections.Generic;
using ConnectFour.Core;
using Godot;
using Renegade.Store;
using Renegade.Ui;

namespace ConnectFour.Game.Ui;

/// <summary>
/// Title, score, turn status and buttons, plus the style menu. Everything that isn't a button
/// ignores the mouse so drags and taps reach the 3D board underneath. The top row matches the
/// studio's other games (renegade-core's GameHud): Style and How to play on the left, Start over
/// and Settings on the right; Settings, the tutorial and dialogs are the kit's own panels.
/// </summary>
public partial class Hud : CanvasLayer
{
	public const string Title = "Counter Fire";

	public event Action? ClearPressed;
	public event Action? UndoPressed;
	public event Action? RestartPressed;
	public event Action<Style>? BoardChosen;
	public event Action<Style>? PiecesChosen;
	public event Action<Backdrop>? BackdropChosen;
	public event Action<bool>? MuteToggled;
	public event Action<bool>? FreezeToggled;
	public event Action<GameMode, Difficulty>? ModeChosen;

	/// <summary>The kit panels (Settings, tutorial, dialogs) in Counter Fire's own blues.</summary>
	private static readonly HudTheme Theme = new(Palette.Text, Palette.TextMuted, Palette.Accent, Palette.ButtonDark, Palette.Panel);

	private Label _score = null!;
	private Label _status = null!;
	private Label _hint = null!;
	private Button _clear = null!;
	private Button _undo = null!;
	private Button _restart = null!;
	private Button _howTo = null!;
	private Control _menu = null!;
	private Button _pvp = null!;
	private Button _pvc = null!;
	private Control _difficultyPopup = null!;
	private SettingsPanel _settingsPanel = null!;
	private TutorialPanel? _tutorial;
	private int _dialogs;

	public void Build(Settings settings)
	{
		var title = MakeLabel(Title, 54, Palette.Text);
		Anchor(title, Control.LayoutPreset.TopWide, top: 34, bottom: 104);
		AddChild(title);

		_score = MakeLabel("", 26, Palette.TextMuted);
		Anchor(_score, Control.LayoutPreset.TopWide, top: 100, bottom: 140);
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

		// Undo is only offered against the computer, and it plays a rewarded ad, so the label says so.
		_undo = MakeButton("Undo (ad)", 22, Palette.ButtonDark);
		Anchor(_undo, Control.LayoutPreset.BottomLeft, left: 20, right: 168, top: -190, bottom: -130);
		_undo.Pressed += () => UndoPressed?.Invoke();
		_undo.Visible = false;
		AddChild(_undo);

		// The top row: small, so the board keeps the screen.
		var style = MakeButton("Style", 22, Palette.ButtonDark);
		Anchor(style, Control.LayoutPreset.TopLeft, left: 20, right: 124, top: 148, bottom: 196);
		style.Pressed += () => _menu.Visible = true;
		AddChild(style);

		// "Demo": the guided tour with video clips of the game.
		_howTo = MakeButton("Demo", 22, Palette.ButtonDark);
		Anchor(_howTo, Control.LayoutPreset.TopLeft, left: 134, right: 234, top: 148, bottom: 196);
		_howTo.Pressed += () => _tutorial?.Offer();
		_howTo.Visible = false;
		AddChild(_howTo);

		_restart = MakeButton("Start over", 22, Palette.ButtonDark);
		Anchor(_restart, Control.LayoutPreset.TopRight, left: -290, right: -154, top: 148, bottom: 196);
		_restart.Pressed += () => RestartPressed?.Invoke();
		AddChild(_restart);

		var settingsButton = MakeButton("Settings", 22, Palette.ButtonDark);
		Anchor(settingsButton, Control.LayoutPreset.TopRight, left: -144, right: -20, top: 148, bottom: 196);
		settingsButton.Pressed += () => _settingsPanel.Open();
		AddChild(settingsButton);

		BuildModeButtons(settings.Mode, settings.Difficulty);

		_menu = BuildMenu(settings.Board, settings.Pieces, settings.Backdrop);
		_menu.Visible = false;
		AddChild(_menu);

		_settingsPanel = new SettingsPanel(Theme);
		_settingsPanel.AddToggle("Sound", !settings.Muted, on => MuteToggled?.Invoke(!on));
		_settingsPanel.AddToggle("Freeze the board (no spinning)", settings.FreezeBoard, on => FreezeToggled?.Invoke(on));
		AddChild(_settingsPanel);

		// The shop at the foot of Settings; once ads are removed, "(ad)" leaves the Undo label.
		ShopRows.Add(_settingsPanel, this, message => Dialog("", message, ("OK", true, null)));
		if (Shop.Current is { } shop)
		{
			void Relabel() => _undo.Text = ShopRows.AdText("Undo (ad)");
			shop.Changed += Relabel;
			TreeExiting += () => shop.Changed -= Relabel;
			Relabel();
		}
	}

	/// <summary>True while a menu, pop-up, panel or dialog is open; the board ignores taps meanwhile.</summary>
	public bool MenuOpen => _menu.Visible || _difficultyPopup.Visible || _settingsPanel.Visible || _tutorial?.Visible == true || _dialogs > 0;

	/// <summary>Sets the tutorial behind the Demo button.</summary>
	public void SetTutorial(IReadOnlyList<TutorialPage> pages)
	{
		_tutorial?.QueueFree();
		_tutorial = new TutorialPanel(Theme, Title, pages);
		AddChild(_tutorial);
		_howTo.Visible = pages.Count > 0;
	}

	/// <summary>Offers the tutorial the first time the game is opened, once.</summary>
	public void OfferTutorialOnce(Settings settings)
	{
		if (_tutorial == null || settings.TutorialOffered)
			return;
		settings.TutorialOffered = true;
		settings.Save();
		_tutorial.Offer();
	}

	/// <summary>Developer checks: `--tutorial=N` opens the tour at step N (0 = the invitation), `--settings` opens Settings.</summary>
	public void ApplyDevFlags()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--tutorial=") && int.TryParse(arg[11..], out int page))
			{
				if (page == 0)
					_tutorial?.Offer();
				else
					_tutorial?.OpenAt(page - 1);
			}
			else if (arg == "--settings")
			{
				_settingsPanel.Open();
			}
		}
	}

	/// <summary>
	/// Confirms starting over. Against the computer that's a resignation, a loss, unless the player
	/// watches an ad: <paramref name="restart"/> is called with true when the loss should count.
	/// </summary>
	public void AskRestart(bool vsComputer, Action<bool> restart, Action<Action> watchAd)
	{
		if (vsComputer && Shop.Current?.AdsRemoved == true)
		{
			// No ads to watch: starting over is simply free.
			Dialog("Start over?", "This round will be cleared away and a new one started. It won't count as a loss.",
				("Start over", true, () => restart(false)),
				("Keep playing", false, null));
			return;
		}
		if (vsComputer)
		{
			Dialog("Start over?",
				"Against the computer, starting over counts as resigning: a loss. Watch a short ad and it won't count.",
				("Watch an ad: no loss", true, () => watchAd(() => restart(false))),
				("Resign and start over", false, () => restart(true)),
				("Keep playing", false, null));
		}
		else
		{
			Dialog("Start over?", "This round will be cleared away and a new one started.",
				("Start over", true, () => restart(false)),
				("Keep playing", false, null));
		}
	}

	private void Dialog(string title, string message, params (string Text, bool Accent, Action? Pressed)[] buttons)
	{
		_dialogs++;
		var wrapped = new (string Text, bool Accent, Action? Pressed)[buttons.Length];
		for (int i = 0; i < buttons.Length; i++)
		{
			var button = buttons[i];
			wrapped[i] = (button.Text, button.Accent, () =>
			{
				_dialogs--;
				button.Pressed?.Invoke();
			});
		}
		ConfirmDialog.Show(this, Theme, title, message, wrapped);
	}

	public void SetRestartVisible(bool visible) => _restart.Visible = visible;

	public void OpenMenu() => _menu.Visible = true;

	public void OpenDifficultyPopup() => _difficultyPopup.Visible = true;

	public void SetStatus(string text, Color color)
	{
		_status.Text = text;
		_status.AddThemeColorOverride("font_color", color);
	}

	public void SetScore(string text) => _score.Text = text;

	public void SetClearVisible(bool visible) => _clear.Visible = visible;

	public void SetUndoVisible(bool visible) => _undo.Visible = visible;

	/// <summary>The controls hint fades after the first move; it's only needed once.</summary>
	public void HideHint()
	{
		if (_hint.Modulate.A < 1f)
			return;
		CreateTween().TweenProperty(_hint, "modulate:a", 0f, 0.6f);
	}

	/// <summary>
	/// Two stacked buttons at the bottom. Choosing "Player vs Computer" pops up the
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
