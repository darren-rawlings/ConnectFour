using System;
using Godot;

namespace ConnectFour.Game.Ads;

/// <summary>
/// Stand-in ads for desktop and for builds without AdMob: a full-screen card with a countdown,
/// so the whole ad flow can be tried anywhere. In demo runs it closes itself.
/// </summary>
public partial class PlaceholderAds : CanvasLayer, IAdService
{
	private const float Duration = 3f;

	public bool AutoClose { get; set; }

	private Label _title = null!;
	private Label _countdown = null!;
	private Button _close = null!;
	private Button _skip = null!;
	private Control _root = null!;
	private float _remaining;
	private Action? _onReward;
	private Action? _onClosed;

	public override void _Ready()
	{
		Layer = 10;
		_root = new ColorRect { Color = new Color("05060f") with { A = 0.96f }, MouseFilter = Control.MouseFilterEnum.Stop };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		column.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		column.AddThemeConstantOverride("separation", 28);
		_root.AddChild(column);

		_title = Label("", 46);
		column.AddChild(_title);
		column.AddChild(Label("Test ad: real ads appear in phone builds", 24));
		_countdown = Label("", 34);
		column.AddChild(_countdown);

		_close = new Button { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(320, 80), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		_close.AddThemeFontSizeOverride("font_size", 32);
		_close.Pressed += () => Finish(rewarded: _onReward != null);
		column.AddChild(_close);

		_skip = new Button { Text = "Skip (no undo)", FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		_skip.AddThemeFontSizeOverride("font_size", 24);
		_skip.Pressed += () => Finish(rewarded: false);
		column.AddChild(_skip);

		_root.Visible = false;
	}

	public void ShowInterstitial(Action closed) => Open("Advertisement", null, closed);

	public void ShowRewarded(Action rewarded, Action closed) => Open("Watch to undo your move", rewarded, closed);

	private void Open(string title, Action? reward, Action closed)
	{
		_title.Text = title;
		_onReward = reward;
		_onClosed = closed;
		_remaining = Duration;
		_close.Text = reward != null ? "Claim undo" : "Close";
		_close.Disabled = true;
		_skip.Visible = reward != null;
		_root.Visible = true;
	}

	public override void _Process(double delta)
	{
		if (!_root.Visible)
			return;
		_remaining -= (float)delta;
		_countdown.Text = _remaining > 0 ? $"{Mathf.CeilToInt(_remaining)}" : "";
		_close.Disabled = _remaining > 0;
		if (AutoClose && _remaining <= -0.3f)
			Finish(rewarded: _onReward != null);
	}

	private void Finish(bool rewarded)
	{
		_root.Visible = false;
		var reward = _onReward;
		var closed = _onClosed;
		_onReward = null;
		_onClosed = null;
		if (rewarded)
			reward?.Invoke();
		closed?.Invoke();
	}

	private static Label Label(string text, int size)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", Palette.Text);
		return label;
	}
}
