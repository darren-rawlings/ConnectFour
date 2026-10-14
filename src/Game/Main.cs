using System.Collections.Generic;
using ConnectFour.Core;
using Godot;

namespace ConnectFour.Game;

/// <summary>
/// Renders the board and handles input for a local two-player match.
/// All drawing is procedural for now so the game looks finished before any art exists;
/// swap in textures later without touching the rules in ConnectFour.Core.
/// </summary>
public partial class Main : Node2D
{
	private readonly Match _match = new();
	private readonly Dictionary<Player, int> _wins = new() { [Player.One] = 0, [Player.Two] = 0 };
	private Player _nextStarter = Player.Two;

	// Layout, recalculated whenever the viewport changes size.
	private float _cell;
	private Vector2 _gridOrigin;
	private Rect2 _boardRect;
	private StyleBoxFlat _boardStyle = new();
	private StyleBoxFlat _glossStyle = new();

	private int _hoverColumn = -1;
	private float _time;

	// The disc currently falling; it is already on the board but drawn here instead.
	private bool _isDropping;
	private Cell _dropCell;
	private Player _dropPlayer;
	private float _dropY;
	private MoveResult? _pendingResult;

	private IReadOnlyList<Cell> _winningCells = new List<Cell>();

	private Label _statusLabel = null!;
	private Label _scoreLabel = null!;
	private Button _newRoundButton = null!;

	public override void _Ready()
	{
		BuildUi();
		GetViewport().SizeChanged += UpdateLayout;
		UpdateLayout();
		RefreshUi();
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseMotion motion:
				_hoverColumn = ColumnAt(motion.Position);
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
				_hoverColumn = ColumnAt(click.Position);
				TryPlay(_hoverColumn);
				break;
			case InputEventKey { Pressed: true, Echo: false } key
				when key.Keycode >= Key.Key1 && key.Keycode < Key.Key1 + Board.Columns:
				TryPlay((int)(key.Keycode - Key.Key1));
				break;
		}
	}

	private void TryPlay(int column)
	{
		if (_isDropping || column < 0)
			return;

		var result = _match.Play(column);
		if (result is not { } move)
			return;

		_isDropping = true;
		_dropCell = move.Cell;
		_dropPlayer = move.Player;
		_pendingResult = move;

		float startY = PreviewY();
		float endY = CellCenter(move.Cell).Y;
		float fallRows = Board.Rows - move.Cell.Row;
		_dropY = startY;

		var tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(y => _dropY = y), startY, endY, 0.18 + 0.07 * fallRows)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		tween.Finished += OnDropFinished;
		RefreshUi();
	}

	private void OnDropFinished()
	{
		_isDropping = false;
		if (_pendingResult is { Status: MatchStatus.Won } move)
		{
			_winningCells = move.WinningCells;
			_wins[move.Player]++;
		}
		_pendingResult = null;
		RefreshUi();
	}

	private void StartNewRound()
	{
		if (_isDropping)
			return;
		_match.Reset(_nextStarter);
		_nextStarter = Match.Opponent(_nextStarter);
		_winningCells = new List<Cell>();
		RefreshUi();
	}

	// ---------------------------------------------------------------- Layout

	private void UpdateLayout()
	{
		var size = GetViewportRect().Size;
		_cell = Mathf.Min(size.X * 0.9f / Board.Columns, size.Y * 0.55f / Board.Rows);
		var gridSize = new Vector2(_cell * Board.Columns, _cell * Board.Rows);
		_gridOrigin = new Vector2((size.X - gridSize.X) / 2f, size.Y * 0.5f - gridSize.Y / 2f);
		float pad = _cell * 0.18f;
		_boardRect = new Rect2(_gridOrigin - new Vector2(pad, pad), gridSize + new Vector2(pad * 2, pad * 2));

		_boardStyle = new StyleBoxFlat
		{
			BgColor = Palette.Board,
			BorderColor = Palette.BoardEdge,
			BorderWidthBottom = (int)(_cell * 0.12f),
			ShadowColor = new Color(0, 0, 0, 0.45f),
			ShadowSize = (int)(_cell * 0.3f),
			ShadowOffset = new Vector2(0, _cell * 0.15f),
			AntiAliasing = true,
		};
		_boardStyle.SetCornerRadiusAll((int)(_cell * 0.28f));
		_glossStyle = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.08f), AntiAliasing = true };
		_glossStyle.SetCornerRadiusAll((int)(_cell * 0.28f));
	}

	private Vector2 CellCenter(Cell cell) =>
		_gridOrigin + new Vector2((cell.Column + 0.5f) * _cell, (Board.Rows - 1 - cell.Row + 0.5f) * _cell);

	private float PreviewY() => _boardRect.Position.Y - _cell * 0.65f;

	private float DiscRadius => _cell * 0.4f;

	private int ColumnAt(Vector2 position)
	{
		float x = position.X - _gridOrigin.X;
		if (x < 0 || x >= _cell * Board.Columns)
			return -1;
		return (int)(x / _cell);
	}

	// ---------------------------------------------------------------- Drawing

	public override void _Draw()
	{
		DrawBackground();

		if (_match.Status == MatchStatus.InProgress && !_isDropping && _hoverColumn >= 0)
		{
			var x = _gridOrigin.X + (_hoverColumn + 0.5f) * _cell;
			DrawRect(new Rect2(_gridOrigin.X + _hoverColumn * _cell, _gridOrigin.Y, _cell, _cell * Board.Rows),
				new Color(1, 1, 1, 0.07f));
			float bob = Mathf.Sin(_time * 4f) * _cell * 0.04f;
			DrawDisc(new Vector2(x, PreviewY() + bob), DiscRadius, Palette.For(_match.CurrentPlayer), 0.9f);
		}

		DrawBoardBody();

		for (int c = 0; c < Board.Columns; c++)
		{
			for (int r = 0; r < Board.Rows; r++)
			{
				var cell = new Cell(c, r);
				var center = CellCenter(cell);
				DrawHole(center);
				var owner = _match.Board[c, r];
				if (owner != Player.None && !(_isDropping && cell == _dropCell))
					DrawDisc(center, DiscRadius, Palette.For(owner));
			}
		}

		if (_isDropping)
		{
			var x = CellCenter(_dropCell).X;
			DrawDisc(new Vector2(x, _dropY), DiscRadius, Palette.For(_dropPlayer));
		}

		DrawWinGlow();
	}

	private void DrawBackground()
	{
		var size = GetViewportRect().Size;
		DrawPolygon(
			new[] { Vector2.Zero, new Vector2(size.X, 0), size, new Vector2(0, size.Y) },
			new[] { Palette.BackgroundTop, Palette.BackgroundTop, Palette.BackgroundBottom, Palette.BackgroundBottom });

		// Soft spotlight behind the board.
		var center = _boardRect.GetCenter();
		for (int i = 6; i >= 1; i--)
			DrawCircle(center, _boardRect.Size.X * 0.12f * i, new Color(0.35f, 0.45f, 1f, 0.025f));
	}

	private void DrawBoardBody()
	{
		DrawStyleBox(_boardStyle, _boardRect);

		// Glossy highlight along the top edge.
		DrawStyleBox(_glossStyle, new Rect2(_boardRect.Position, new Vector2(_boardRect.Size.X, _cell * 0.6f)));
	}

	private void DrawHole(Vector2 center)
	{
		float r = DiscRadius * 1.04f;
		DrawCircle(center, r + _cell * 0.03f, Palette.BoardEdge);
		DrawCircle(center, r, Palette.Hole);
		DrawArc(center, r * 0.92f, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 24, Palette.HoleRim, _cell * 0.04f, true);
	}

	private void DrawDisc(Vector2 center, float radius, Color color, float alpha = 1f)
	{
		var baseColor = color with { A = alpha };
		var dark = color.Darkened(0.35f) with { A = alpha };
		DrawCircle(center + new Vector2(0, radius * 0.1f), radius, new Color(0, 0, 0, 0.3f * alpha));
		DrawCircle(center, radius, dark);
		DrawCircle(center, radius * 0.86f, baseColor);
		DrawArc(center, radius * 0.6f, 0, Mathf.Tau, 48, dark with { A = 0.55f * alpha }, radius * 0.08f, true);
		DrawCircle(center + new Vector2(-radius * 0.32f, -radius * 0.32f), radius * 0.2f, new Color(1, 1, 1, 0.4f * alpha));
	}

	private void DrawWinGlow()
	{
		if (_winningCells.Count == 0)
			return;
		float pulse = 0.55f + 0.45f * Mathf.Sin(_time * 6f);
		foreach (var cell in _winningCells)
		{
			var center = CellCenter(cell);
			DrawArc(center, DiscRadius * 1.1f, 0, Mathf.Tau, 64, new Color(1, 1, 1, pulse), _cell * 0.07f, true);
		}
	}

	// ---------------------------------------------------------------- UI

	private void BuildUi()
	{
		var ui = new CanvasLayer();
		AddChild(ui);

		var title = MakeLabel("CONNECT FOUR", 64, Palette.Text);
		title.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		title.OffsetTop = 70;
		ui.AddChild(title);

		_scoreLabel = MakeLabel("", 32, Palette.TextMuted);
		_scoreLabel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_scoreLabel.OffsetTop = 160;
		ui.AddChild(_scoreLabel);

		_statusLabel = MakeLabel("", 44, Palette.Text);
		_statusLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		_statusLabel.OffsetTop = -300;
		_statusLabel.OffsetBottom = -230;
		ui.AddChild(_statusLabel);

		_newRoundButton = new Button { Text = "New Round", FocusMode = Control.FocusModeEnum.None };
		_newRoundButton.AddThemeFontSizeOverride("font_size", 34);
		foreach (var (state, color) in new[] { ("normal", "3b6cf0"), ("hover", "5580ff"), ("pressed", "2a52c4") })
		{
			var box = new StyleBoxFlat { BgColor = new Color(color), AntiAliasing = true };
			box.SetCornerRadiusAll(40);
			box.SetContentMarginAll(18);
			_newRoundButton.AddThemeStyleboxOverride(state, box);
		}
		_newRoundButton.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		_newRoundButton.OffsetLeft = -160;
		_newRoundButton.OffsetRight = 160;
		_newRoundButton.OffsetTop = -190;
		_newRoundButton.OffsetBottom = -110;
		_newRoundButton.Pressed += StartNewRound;
		ui.AddChild(_newRoundButton);
	}

	private static Label MakeLabel(string text, int fontSize, Color color)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
		label.AddThemeConstantOverride("shadow_offset_y", 3);
		return label;
	}

	private void RefreshUi()
	{
		_scoreLabel.Text = $"Red  {_wins[Player.One]}   —   {_wins[Player.Two]}  Yellow";

		bool roundOver = _match.Status != MatchStatus.InProgress && !_isDropping;
		_newRoundButton.Visible = roundOver;

		if (_isDropping)
			return;

		switch (_match.Status)
		{
			case MatchStatus.Won:
				_statusLabel.Text = $"{Palette.NameOf(_match.Winner)} wins!";
				_statusLabel.AddThemeColorOverride("font_color", Palette.For(_match.Winner));
				break;
			case MatchStatus.Draw:
				_statusLabel.Text = "It's a draw";
				_statusLabel.AddThemeColorOverride("font_color", Palette.Text);
				break;
			default:
				_statusLabel.Text = $"{Palette.NameOf(_match.CurrentPlayer)}'s turn";
				_statusLabel.AddThemeColorOverride("font_color", Palette.For(_match.CurrentPlayer));
				break;
		}
	}
}
