using System.Collections.Generic;
using System.Threading.Tasks;
using ConnectFour.Core;
using ConnectFour.Game.Audio;
using ConnectFour.Game.Ui;
using Godot;
using Renegade.Ads;
using Renegade.Audio;
using Renegade.Core;
using Renegade.Dev;
using static ConnectFour.Game.BoardLayout;
using Difficulty = ConnectFour.Core.Difficulty;

namespace ConnectFour.Game;

/// <summary>
/// Builds the 3D scene and runs a local two-player match. The rules live in ConnectFour.Core;
/// this class only turns moves into physical discs and handles input, spinning and the release.
/// </summary>
public partial class Main : Node3D
{
	private enum Phase
	{
		/// <summary>Waiting for a move.</summary>
		Ready,

		/// <summary>A disc is falling; input waits until it settles.</summary>
		Dropping,

		/// <summary>Someone won or the board is full; waiting for "Release Pieces".</summary>
		RoundOver,

		/// <summary>The tray is open and discs are tumbling out.</summary>
		Releasing,

		/// <summary>An ad is on screen; the game waits underneath.</summary>
		Advert,
	}

	private const float SpinPerPixel = 0.008f;
	private const float SpinDamping = 1.6f;
	private const float DragThreshold = 14f;
	private const float SettleSpeed = 0.15f;
	private const float SettleHold = 0.06f;
	private const float MaxFallTime = 3f;
	private const float ReleaseTime = 2.6f;

	/// <summary>Landings are the sound that matters most; lift them above the release clatter.</summary>
	private const float LandingBoostDb = 3f;

	private readonly Match _match = new();
	private readonly Dictionary<Player, int> _wins = new() { [Player.One] = 0, [Player.Two] = 0 };
	private readonly Disc?[,] _discs = new Disc?[Board.Columns, Board.Rows];
	private readonly List<Disc> _loose = new();
	private readonly HashSet<Disc> _tipped = new();
	private Player _nextStarter = Player.Two;
	private Settings _settings = null!;

	private Camera3D _camera = null!;
	private BoardRig _rig = null!;
	private Scenery _scenery = null!;
	private Node3D _looseRoot = null!;
	private SoundPool _sound = null!;
	private Hud _hud = null!;
	private MeshInstance3D _ghost = null!;

	private Phase _phase = Phase.Ready;
	private Disc? _falling;
	private MoveResult? _pending;
	private float _fallTime;
	private float _settleTime;
	private bool _landed;

	private Task<(int Generation, int Column)>? _thinking;
	private float _thinkTime;
	private int _computerGeneration;
	private bool _newMatchPending;

	/// <summary>
	/// Google's test ad units until the real ones exist in the AdMob account. The game is for
	/// everyone, children included, so every ad request is child-directed.
	/// </summary>
	private static readonly AdMobConfig AdUnits = AdMobConfig.Test;

	private IAdService _ads = null!;
	private AdSchedule _adSchedule = null!;
	private Player _roundStarter = Player.One;

	/// <summary>Set when a round ends in a win or draw; cleared if Undo reopens it.</summary>
	private bool _roundCompleted;
	private int _hoverColumn = -1;

	// Spin input: a press becomes a drag once it moves far enough, otherwise it's a tap.
	private bool _pressing;
	private bool _dragging;
	private Vector2 _pressPosition;
	private float _spinVelocity;
	private ulong _lastDragUsec;

	// Developer demo (launch with `-- --demo`): plays scripted moves, then releases the pieces.
	/// <summary>A "u" in the demo move list presses Undo.</summary>
	private const int DemoUndo = -1;
	private readonly Queue<int> _demoMoves = new();
	private bool _demo;
	private bool _demoSpin;
	private bool _openMenu;
	private bool _openDifficulty;
	private bool _quitAfterDemo;
	private float _demoDelay = 0.35f;
	private string? _recordAudioPath;
	private AudioEffectRecord? _recorder;
	private float _demoTimer;
	private float _initialSpin;
	private string? _clip;

	public override void _Ready()
	{
		DevLog.Tag = "cf";
		// Flight recorder: checks every sound reaches the output and logs pauses, focus and ads to user://blackbox.log.
		AudioWatch.Attach(this, "Counter Fire");
		_settings = Settings.Load();
		ReadDeveloperArgs();
		AudioServer.SetBusMute(0, _settings.Muted);
		if (_recordAudioPath != null)
		{
			// Captures the live mix (not movie-maker's offline one) to check real-time audio.
			_recorder = new AudioEffectRecord();
			AudioServer.AddBusEffect(0, _recorder);
			_recorder.SetRecordingActive(true);
		}
		BuildWorld();
		_rig.RotateY(_initialSpin);

		_adSchedule ??= new AdSchedule(_settings.GamesSinceAd);
		_ads = AdServices.Create(this, AdUnits, _demo, new PlaceholderAds
		{
			TextColor = Palette.Text,
			RewardTitle = "Watch to undo your move",
			ClaimText = "Claim undo",
			SkipText = "Skip (no undo)",
		});

		_hud = new Hud();
		AddChild(_hud);
		_hud.Build(_settings);
		_hud.ModeChosen += (mode, difficulty) =>
		{
			_settings.Mode = mode;
			_settings.Difficulty = difficulty;
			_settings.Save();
			StartNewMatch();
		};
		_hud.ClearPressed += Release;
		_hud.UndoPressed += OnUndoPressed;
		_hud.BoardChosen += style =>
		{
			_settings.Board = style;
			_settings.Save();
			_rig.ApplyStyle(style);
		};
		_hud.PiecesChosen += style =>
		{
			_settings.Pieces = style;
			_settings.Save();
			foreach (var disc in AllDiscs())
				disc.ApplyStyle(style);
			RefreshUi();
		};
		_hud.BackdropChosen += backdrop =>
		{
			_settings.Backdrop = backdrop;
			_settings.Save();
			_scenery.Apply(backdrop);
		};
		_hud.MuteToggled += muted =>
		{
			_settings.Muted = muted;
			_settings.Save();
			AudioServer.SetBusMute(0, muted);
		};
		_hud.FreezeToggled += frozen =>
		{
			_settings.FreezeBoard = frozen;
			_settings.Save();
		};
		_hud.RestartPressed += OnRestartPressed;
		_hud.SetTutorial(Tutorial.Pages);
		if (_openDifficulty)
			_hud.OpenDifficultyPopup();
		else if (_openMenu)
			_hud.OpenMenu();
		RefreshUi();
		if (_clip != null)
		{
			_hud.Visible = false;
			_ = PlayClip(_clip);
			return;
		}
		if (!_demo)
			_hud.OfferTutorialOnce(_settings);
		_hud.ApplyDevFlags();
	}

	// ---------------------------------------------------------------- Start over

	/// <summary>
	/// Start over clears the board mid-round. Against the computer that's resigning, so the round
	/// goes to the computer, unless you watch an ad. The abandoned round doesn't count towards ads.
	/// </summary>
	private void OnRestartPressed()
	{
		if (_phase != Phase.Ready || _match.MoveCount == 0 || IsComputerTurn)
			return;
		_hud.AskRestart(VsComputer && !_demo, countLoss =>
		{
			if (_phase != Phase.Ready)
				return;
			_computerGeneration++;
			_thinking = null;
			if (countLoss)
				_wins[ComputerSide]++;
			DevLog.Write($"start over (counted as a loss: {countLoss})");
			_phase = Phase.RoundOver;
			Release();
		}, then =>
		{
			DevLog.Write("ad: rewarded (start over)");
			_phase = Phase.Advert;
			_ads.ShowRewarded(
				rewarded: () =>
				{
					_phase = Phase.Ready;
					then();
				},
				closed: () =>
				{
					if (_phase == Phase.Advert)
						_phase = Phase.Ready;
					RefreshUi();
				});
		});
	}

	// ---------------------------------------------------------------- Tutorial clips

	/// <summary>
	/// Plays one scripted scene for the tutorial (with --clip=name; addons/renegade_core/tools/
	/// Record-Clips.ps1 records it), then quits. The HUD is hidden so the clip shows only the board.
	/// </summary>
	private async Task PlayClip(string clip)
	{
		_settings.Mode = GameMode.PlayerVsPlayer;
		_settings.Muted = false;
		await Wait(0.8f);
		int[] moves = clip == "drop" ? new[] { 3, 3, 4, 2, 4 } : new[] { 0, 0, 1, 1, 2, 2, 3 };
		foreach (int column in moves)
		{
			TryPlay(column, byComputer: true);
			while (_phase == Phase.Dropping)
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await Wait(clip == "release" ? 0.1f : 0.35f);
		}
		if (clip == "release")
		{
			await Wait(1.0f);
			Release();
			await Wait(ReleaseTime + 0.6f);
		}
		else
		{
			await Wait(clip == "win" ? 3f : 1f);
		}
		GetTree().Quit();
	}

	private SignalAwaiter Wait(float seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	// ---------------------------------------------------------------- World

	private void BuildWorld()
	{
		_scenery = new Scenery();
		AddChild(_scenery);
		_scenery.Build(_settings.Backdrop, TableY);

		// Fixed horizontal FOV keeps the whole board in frame on any phone aspect ratio.
		_camera = new Camera3D
		{
			KeepAspect = Camera3D.KeepAspectEnum.Width,
			Fov = 40,
			Position = new Vector3(0, 4.6f, 13.6f),
		};
		AddChild(_camera);
		_camera.LookAt(new Vector3(0, 1.5f, 0));
		WarmUpShaders();

		_rig = new BoardRig();
		AddChild(_rig);
		_rig.Build(_settings.Board);

		_ghost = new MeshInstance3D
		{
			Mesh = Meshes.Disc,
			Transparency = 0.5f,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Visible = false,
		};
		_rig.AddChild(_ghost);

		_looseRoot = new Node3D();
		AddChild(_looseRoot);
		_sound = new SoundPool();
		AddChild(_sound);
	}

	/// <summary>
	/// Phones compile each shader the first time it is drawn, which stalls that frame; the first
	/// win used to hitch while its effect compiled. Draw every shader for a moment at startup so
	/// that cost is paid up front. The discs sit full size under the table: hidden, but lit and
	/// shadowed like real ones, so the exact same shader variants get compiled.
	/// </summary>
	private void WarmUpShaders()
	{
		var warm = new Node3D { Position = new Vector3(0, TableY - 1.5f, 0) };
		AddChild(warm);
		var opaque = MaterialFactory.For(Styles.Pieces(Style.Plastic).One, isDisc: true);
		var glass = MaterialFactory.For(Styles.Pieces(Style.Glass).One, isDisc: true);
		warm.AddChild(new MeshInstance3D { Mesh = Meshes.Disc, MaterialOverride = opaque, MaterialOverlay = MaterialFactory.Fire() });
		warm.AddChild(new DiscFire(withLight: true));
		warm.AddChild(new MeshInstance3D { Mesh = Meshes.Disc, MaterialOverride = glass });
		warm.AddChild(new MeshInstance3D { Mesh = Meshes.Disc, MaterialOverride = opaque, Transparency = 0.5f });
		GetTree().CreateTimer(0.3).Timeout += warm.QueueFree;
	}

	// ---------------------------------------------------------------- Input

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_hud.MenuOpen)
			return;

		switch (@event)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left } button when button.Pressed:
				_pressing = true;
				_dragging = false;
				_pressPosition = button.Position;
				_spinVelocity = 0f;
				break;

			case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
				if (_pressing && !_dragging)
					TryPlay(PickColumn(button.Position));
				_pressing = false;
				_dragging = false;
				break;

			case InputEventMouseMotion motion:
				if (_pressing && !_dragging && motion.Position.DistanceTo(_pressPosition) > DragThreshold && CanSpin)
				{
					_dragging = true;
					_lastDragUsec = Time.GetTicksUsec();
				}
				if (_dragging)
					Drag(motion.Relative.X);
				_hoverColumn = _pressing ? -1 : PickColumn(motion.Position);
				break;

			case InputEventKey { Pressed: true, Echo: false } key
				when key.Keycode >= Key.Key1 && key.Keycode < Key.Key1 + Board.Columns:
				TryPlay((int)(key.Keycode - Key.Key1));
				break;
		}
	}

	/// <summary>Spinning is allowed whenever no disc is in flight, so the physics stays put.</summary>
	private bool CanSpin => _phase is Phase.Ready or Phase.RoundOver && !_settings.FreezeBoard;

	private void Drag(float pixels)
	{
		float angle = pixels * SpinPerPixel;
		_rig.RotateY(angle);
		ulong now = Time.GetTicksUsec();
		float dt = Mathf.Max((now - _lastDragUsec) / 1_000_000f, 1f / 240f);
		_lastDragUsec = now;
		// Smooth the release speed so a flick spins the board instead of jittering it.
		_spinVelocity = Mathf.Lerp(_spinVelocity, angle / dt, 0.5f);
	}

	/// <summary>Finds the column under a screen point by intersecting with the board's mid-plane.</summary>
	private int PickColumn(Vector2 screen)
	{
		var toLocal = _rig.GlobalTransform.AffineInverse();
		var origin = toLocal * _camera.ProjectRayOrigin(screen);
		var direction = toLocal.Basis * _camera.ProjectRayNormal(screen);
		if (Mathf.Abs(direction.Z) < 0.05f)
			return -1; // Looking at the board edge-on.
		var hit = origin + direction * (-origin.Z / direction.Z);
		if (hit.Y < OuterBottom || hit.Y > SpawnY + 1.2f)
			return -1;
		return ColumnAt(hit.X);
	}

	// ---------------------------------------------------------------- Frame updates

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (dt > 0.05f)
			DevLog.Write($"slow frame {dt * 1000:0}ms phase={_phase}");
		RunDemo(dt);
		UpdateComputer(dt);
		if (!_dragging && _spinVelocity != 0f)
		{
			// Ease to a stop, and freeze the spin the moment a disc needs the board still.
			if (!CanSpin || Mathf.Abs(_spinVelocity) < 0.02f)
				_spinVelocity = 0f;
			_rig.RotateY(_spinVelocity * dt);
			_spinVelocity *= Mathf.Exp(-SpinDamping * dt);
		}
		// Freeze the board also turns it back to where it started, whenever no disc is moving.
		if (_settings.FreezeBoard && _phase is Phase.Ready or Phase.RoundOver)
		{
			_spinVelocity = 0f;
			float off = Mathf.AngleDifference(_rig.Rotation.Y, 0f);
			if (Mathf.Abs(off) > 0.0005f)
				_rig.RotateY(Mathf.Abs(off) < 0.002f ? off : off * (1f - Mathf.Exp(-9f * dt)));
		}
		SyncSettledDiscs();

		bool showGhost = _phase == Phase.Ready && _hoverColumn >= 0 && !_hud.MenuOpen && !IsComputerTurn;
		_ghost.Visible = showGhost;
		if (showGhost)
		{
			float bob = Mathf.Sin(Time.GetTicksMsec() / 250f) * 0.05f;
			_ghost.Position = new Vector3(ColumnX(_hoverColumn), SpawnY + 0.3f + bob, 0);
			_ghost.Rotation = new Vector3(Mathf.Pi / 2f, 0, 0);
			_ghost.MaterialOverride = MaterialFactory.For(Styles.Pieces(_settings.Pieces).LookOf(_match.CurrentPlayer), isDisc: true);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		foreach (var disc in _loose)
		{
			var local = _rig.ToLocal(disc.GlobalPosition);

			// As each released disc clears the bottom of the board, tip it so it topples and scatters
			// across the table instead of landing upright in a neat row.
			if (_phase == Phase.Releasing && local.Y < OuterBottom - DiscRadius && _tipped.Add(disc))
			{
				var basis = _rig.GlobalBasis;
				disc.AngularVelocity = basis * new Vector3((float)GD.RandRange(-9.0, 9.0), 0, (float)GD.RandRange(-2.0, 2.0));
				disc.LinearVelocity += basis * new Vector3((float)GD.RandRange(-1.0, 1.0), 0, (float)GD.RandRange(-2.5, 2.5));
			}
		}

		if (_falling is { } falling)
			CheckSettled(falling, (float)delta);
	}

	// ---------------------------------------------------------------- Moves

	private void TryPlay(int column, bool byComputer = false)
	{
		if (_phase != Phase.Ready || column < 0 || (_hud.MenuOpen && !byComputer) || (IsComputerTurn && !byComputer))
			return;
		var result = _match.Play(column);
		if (result is not { } move)
			return;

		_spinVelocity = 0f;
		var spawn = _rig.GlobalTransform * new Transform3D(Basis.Identity, new Vector3(ColumnX(column), SpawnY, 0));
		var disc = SpawnDisc(move.Player, move.Cell, spawn, _rig.GlobalBasis * new Vector3(0, -2f, 0));
		_landed = false;

		_falling = disc;
		_pending = move;
		_fallTime = 0f;
		_settleTime = 0f;
		_phase = Phase.Dropping;
		_hud.HideHint();
		RefreshUi();
	}

	private Disc SpawnDisc(Player player, Cell cell, Transform3D transform, Vector3 velocity)
	{
		var disc = new Disc();
		disc.Setup(player, cell, _settings.Pieces);
		disc.Launch(transform, velocity);
		disc.Hit += OnDiscHit;
		_looseRoot.AddChild(disc);
		_loose.Add(disc);
		return disc;
	}

	/// <summary>
	/// Keeps settled discs locked to their cells as the board spins. Physics bodies don't reliably
	/// follow a moving parent (the engine writes back their own last-known position), so instead
	/// of parenting them to the board we place them explicitly every frame.
	/// </summary>
	private void SyncSettledDiscs()
	{
		var board = _rig.GlobalTransform;
		foreach (var disc in _discs)
		{
			if (disc != null)
				disc.GlobalTransform = board * new Transform3D(Basis.Identity, CellCenter(disc.Cell));
		}
	}

	private void CheckSettled(Disc disc, float dt)
	{
		_fallTime += dt;
		var local = _rig.ToLocal(disc.GlobalPosition);
		var target = CellCenter(disc.Cell);
		// A disc resting on another balances on a curved edge and can rock for a second or more,
		// so once it has landed near its cell and isn't rebounding, call it settled and lock it in.
		bool landedInPlace = _landed && Mathf.Abs(local.Y - target.Y) < 0.12f && Mathf.Abs(disc.LinearVelocity.Y) < 1.5f;
		bool resting = landedInPlace || (disc.LinearVelocity.Length() < SettleSpeed && Mathf.Abs(local.Y - target.Y) < 0.2f);
		_settleTime = resting ? _settleTime + dt : 0f;

		// The timeout is a safety net in case physics ever wedges a disc; it snaps into place.
		if (_settleTime >= SettleHold || _fallTime >= MaxFallTime)
		{
			_falling = null;
			CallDeferred(MethodName.Settle, disc);
		}
	}

	private void Settle(Disc disc)
	{
		DevLog.Write($"settle {disc.Cell} after {_fallTime:0.00}s");
		disc.Freeze = true;
		_loose.Remove(disc);
		_discs[disc.Cell.Column, disc.Cell.Row] = disc;
		SyncSettledDiscs();

		var move = _pending;
		_pending = null;
		if (move is { Status: MatchStatus.Won } win)
		{
			_wins[win.Player]++;
			SetLineAlight(win);
			_phase = Phase.RoundOver;
		}
		else
		{
			_phase = move is { Status: MatchStatus.Draw } ? Phase.RoundOver : Phase.Ready;
		}
		_roundCompleted = _phase == Phase.RoundOver;
		DevLog.Write($"phase {_phase}");
		RefreshUi();
		if (_newMatchPending)
			StartNewMatch();
	}

	/// <summary>Seconds for the fire to jump from one disc to the next along the winning line.</summary>
	private const float FireSpreadDelay = 0.16f;

	/// <summary>Bumped whenever the fires are put out, so a spread still on its way is cancelled.</summary>
	private int _fireGeneration;

	/// <summary>
	/// "Counter Fire": the winning disc bursts into flame and the fire runs along the line to the
	/// rest of the four. Only the winning disc carries a light, to keep the phone's lighting cheap.
	/// </summary>
	private void SetLineAlight(MoveResult win)
	{
		int generation = _fireGeneration;
		foreach (var cell in win.WinningCells)
		{
			var disc = _discs[cell.Column, cell.Row];
			if (disc == null)
				continue;
			int steps = Mathf.Max(Mathf.Abs(cell.Column - win.Cell.Column), Mathf.Abs(cell.Row - win.Cell.Row));
			bool first = cell == win.Cell;
			if (steps == 0)
			{
				disc.Ignite(withLight: first);
				continue;
			}
			GetTree().CreateTimer(steps * FireSpreadDelay).Timeout += () =>
			{
				if (generation == _fireGeneration && IsInstanceValid(disc))
					disc.Ignite(withLight: false);
			};
		}
	}

	private void PutOutFires()
	{
		_fireGeneration++;
		foreach (var disc in AllDiscs())
			disc.Extinguish();
	}

	// ---------------------------------------------------------------- Undo (vs computer)

	/// <summary>
	/// Undo is offered against the computer once you have a move on the board: on your turn, or
	/// after the computer wins or the board fills. Not after you win, and not mid-drop.
	/// </summary>
	private bool CanUndo
	{
		get
		{
			if (!VsComputer)
				return false;
			int movesBeforeYours = _roundStarter == ComputerSide ? 1 : 0;
			if (_match.MoveCount <= movesBeforeYours)
				return false;
			return _phase switch
			{
				Phase.Ready => !IsComputerTurn,
				Phase.RoundOver => _match.Winner != Match.Opponent(ComputerSide),
				_ => false,
			};
		}
	}

	/// <summary>Undo costs a rewarded ad: it only happens if the player watches it through.</summary>
	private void OnUndoPressed()
	{
		if (!CanUndo)
			return;
		DevLog.Write("ad: rewarded (undo)");
		var before = _phase;
		_phase = Phase.Advert;
		_ads.ShowRewarded(
			rewarded: UndoLastTurn,
			closed: () =>
			{
				if (_phase == Phase.Advert)
					_phase = before;
				RefreshUi();
			});
	}

	/// <summary>Takes back moves until one of yours is undone (so it's your turn again).</summary>
	private void UndoLastTurn()
	{
		_computerGeneration++;
		_thinking = null;
		if (_match.Status == MatchStatus.Won)
			_wins[_match.Winner]--;
		PutOutFires();

		var you = Match.Opponent(ComputerSide);
		while (_match.MoveCount > 0)
		{
			var mover = _match.LastMover;
			if (_match.Undo() is { } cell)
				RemoveDisc(cell);
			if (mover == you)
				break;
		}
		_roundCompleted = false;
		_phase = Phase.Ready;
		DevLog.Write($"undo: {_match.MoveCount} moves left, {_match.CurrentPlayer} to play");
	}

	/// <summary>Lifts an undone disc off the board: it shrinks away and is removed.</summary>
	private void RemoveDisc(Cell cell)
	{
		var disc = _discs[cell.Column, cell.Row];
		_discs[cell.Column, cell.Row] = null;
		if (disc == null)
			return;
		var tween = CreateTween();
		tween.TweenProperty(disc.Mesh, "scale", Vector3.One * 0.01f, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		tween.Finished += disc.QueueFree;
	}

	// ---------------------------------------------------------------- Computer opponent

	/// <summary>The computer always plays Yellow (Player Two); you play Red.</summary>
	private const Player ComputerSide = Player.Two;

	/// <summary>Shortest "thinking" pause, so the computer's reply doesn't land instantly.</summary>
	private const float MinThinkTime = 0.6f;

	private bool VsComputer => _settings.Mode == GameMode.PlayerVsComputer;

	private bool IsComputerTurn =>
		VsComputer && _match.Status == MatchStatus.InProgress && _match.CurrentPlayer == ComputerSide;

	/// <summary>
	/// On the computer's turn, searches for its move on a worker thread (Hard looks 7 moves ahead,
	/// which could stall a frame on a phone), then plays it once a natural pause has passed.
	/// A generation number discards a search that finishes after the match was reset.
	/// </summary>
	private void UpdateComputer(float dt)
	{
		if (_phase != Phase.Ready || !IsComputerTurn)
			return;

		if (_thinking == null)
		{
			var player = new ComputerPlayer(_settings.Difficulty);
			var snapshot = _match.Board.Clone();
			int generation = _computerGeneration;
			_thinking = Task.Run(() =>
			{
				var watch = System.Diagnostics.Stopwatch.StartNew();
				int column = player.ChooseMove(snapshot, ComputerSide);
				DevLog.Write($"computer ({player.Difficulty}) chose column {column} in {watch.ElapsedMilliseconds} ms");
				return (generation, column);
			});
			_thinkTime = 0f;
			RefreshUi();
			return;
		}

		_thinkTime += dt;
		if (!_thinking.IsCompleted || _thinkTime < MinThinkTime)
			return;
		var (forGeneration, column) = _thinking.Result;
		_thinking = null;
		if (forGeneration == _computerGeneration)
			TryPlay(column, byComputer: true);
	}

	/// <summary>
	/// Starts over after the mode or difficulty changes: score back to 0-0, you move first, and
	/// any discs on the board are released. Waits for a falling or tumbling disc to finish first.
	/// </summary>
	private void StartNewMatch()
	{
		_roundCompleted = false;
		_computerGeneration++;
		_thinking = null;
		_wins[Player.One] = 0;
		_wins[Player.Two] = 0;

		if (_phase is Phase.Dropping or Phase.Releasing)
		{
			_newMatchPending = true;
			RefreshUi();
			return;
		}
		_newMatchPending = false;
		_nextStarter = Player.One;

		bool hasDiscs = false;
		foreach (var _ in AllDiscs())
		{
			hasDiscs = true;
			break;
		}
		if (hasDiscs)
		{
			_phase = Phase.RoundOver;
			Release();
		}
		else
		{
			BeginRound();
			_phase = Phase.Ready;
			RefreshUi();
		}
	}

	/// <summary>Clears the rules board for a new round; the starting player alternates.</summary>
	private void BeginRound()
	{
		_roundStarter = _nextStarter;
		_match.Reset(_nextStarter);
		_nextStarter = Match.Opponent(_nextStarter);
	}

	// ---------------------------------------------------------------- Release

	/// <summary>Slides the tray out so every disc drops through the bottom, then starts a new round.</summary>
	private void Release()
	{
		if (_phase != Phase.RoundOver)
			return;
		_phase = Phase.Releasing;
		_spinVelocity = 0f;
		_dragging = false;

		_sound.PlayImpact(_rig.Style, _rig.TrayEnd, 0.7f);
		_rig.OpenTray();
		PutOutFires();
		foreach (var disc in new List<Disc>(AllDiscs()))
		{
			disc.Lively = true;
			disc.Freeze = false;
			disc.Sleeping = false;
			_loose.Add(disc);
		}
		System.Array.Clear(_discs);
		RefreshUi();

		GetTree().CreateTimer(ReleaseTime).Timeout += FinishRelease;
	}

	private void FinishRelease()
	{
		if (_demo && _quitAfterDemo && _demoMoves.Count == 0)
			GetTree().CreateTimer(1.0).Timeout += () => GetTree().Quit();
		foreach (var disc in _loose)
		{
			var d = disc;
			var tween = CreateTween();
			tween.TweenProperty(d.Mesh, "scale", Vector3.One * 0.01f, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			tween.Finished += d.QueueFree;
		}
		_loose.Clear();
		_tipped.Clear();
		_rig.CloseTray();

		BeginRound();
		_phase = Phase.Ready;

		// Every fifth finished game, show an ad at this natural break, before the next round.
		// Rounds abandoned by switching mode don't count.
		if (_roundCompleted && !_newMatchPending)
		{
			_roundCompleted = false;
			bool adDue = _adSchedule.GameFinished();
			_settings.GamesSinceAd = _adSchedule.GamesSinceAd;
			if (!_demo)
				_settings.Save();
			if (adDue)
			{
				DevLog.Write("ad: between-games interstitial");
				_phase = Phase.Advert;
				RefreshUi();
				_ads.ShowInterstitial(() =>
				{
					_phase = Phase.Ready;
					RefreshUi();
				});
				return;
			}
		}
		RefreshUi();
		if (_newMatchPending)
			StartNewMatch();
	}

	// ---------------------------------------------------------------- Sound

	/// <summary>
	/// Volume of a disc-on-disc landing by the row it fills: full in the second row, then 20%
	/// quieter per row, down to 20% at the top, since the disc falls less far.
	/// </summary>
	public static float LandingVolume(int row) => row <= 1 ? 1f : Mathf.Max(0.2f, 1f - 0.2f * (row - 1));

	private void OnDiscHit(Disc disc, float impact, Node? other)
	{
		DevLog.Write($"hit {disc.Cell} impact={impact:0.0} other={other?.Name ?? "none"} falling={disc == _falling} landed={_landed}");
		if (other == null)
			return;

		if (disc == _falling)
		{
			// A dropped disc makes exactly one sound: the moment it lands on the tray or the disc
			// below. Rattles against the slot walls and small rebounds stay silent so the sound
			// lines up with what you see.
			bool landing = other is Disc || _rig.IsTray(other);
			if (_landed || !landing)
				return;
			_landed = true;
			if (disc.Cell.Row == 0)
			{
				// On the bottom row the disc meets the board itself, so both materials are heard. Each is
				// played softer so together they stay below a disc-on-disc landing in the second row.
				_sound.PlayImpact(disc.Style, disc.GlobalPosition, 0.5f, LandingBoostDb);
				_sound.PlayImpact(_rig.Style, disc.GlobalPosition, 0.5f, LandingBoostDb);
			}
			else
			{
				_sound.PlayImpact(disc.Style, disc.GlobalPosition, LandingVolume(disc.Cell.Row), LandingBoostDb);
			}
			return;
		}

		// When two loose discs collide both report it; only the one with the lower id plays it.
		if (other is Disc { Freeze: false } peer && peer.GetInstanceId() < disc.GetInstanceId())
			return;

		float strength = Mathf.Clamp(impact / 20f, 0.04f, 1f);
		var position = disc.GlobalPosition;
		_sound.PlayImpact(disc.Style, position, strength);

		Style? surface = other switch
		{
			Disc peerDisc => peerDisc.Style,
			Node node when node.IsInGroup(BoardRig.Group) => _rig.Style,
			Node node when node.IsInGroup(Scenery.TableGroup) => _scenery.TableSound,
			_ => null,
		};
		if (surface is { } s && s != disc.Style)
			_sound.PlayImpact(s, position, strength * 0.6f);
	}

	public override void _ExitTree()
	{
		if (_recorder != null && _recordAudioPath != null)
			_recorder.GetRecording()?.SaveToWav(_recordAudioPath);
	}

	// ---------------------------------------------------------------- Developer demo

	/// <summary>
	/// Optional user args after `--`: `--demo[=3,3,4,4,5,5,6]`, `--board=Wood`, `--pieces=Metal`, `--backdrop=Sunset`,
	/// `--spin=35` (degrees), `--demo-spin` (spin the board before releasing), `--demo-delay=0.35` (pause before each
	/// demo move), `--record-audio=path.wav` (save the live mix on exit). Style overrides are not saved. Used to record test footage.
	/// </summary>
	private void ReadDeveloperArgs()
	{
		foreach (var (name, value) in DevArgs.Read())
		{
			switch (name)
			{
				case "--demo":
					_demo = true;
					_settings.Muted = false; // Test runs always record sound; not saved.
					foreach (string column in (value == "" ? "3,3,4,4,5,5,6" : value).Split(','))
						_demoMoves.Enqueue(column == "u" ? DemoUndo : int.Parse(column));
					break;
				case "--board" when System.Enum.TryParse(value, out Style board):
					_settings.Board = board;
					break;
				case "--backdrop" when System.Enum.TryParse(value, out Backdrop backdrop):
					_settings.Backdrop = backdrop;
					break;
				case "--pieces" when System.Enum.TryParse(value, out Style pieces):
					_settings.Pieces = pieces;
					break;
				case "--record-audio":
					_recordAudioPath = value;
					break;
				case "--demo-delay":
					_demoDelay = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
					break;
				case "--mode" when System.Enum.TryParse(value, out GameMode mode):
					_settings.Mode = mode;
					break;
				case "--difficulty" when System.Enum.TryParse(value, out Difficulty difficulty):
					_settings.Difficulty = difficulty;
					break;
				case "--games-since-ad":
					_adSchedule = new AdSchedule(int.Parse(value));
					break;
				case "--quit-after-demo":
					_quitAfterDemo = true;
					break;
				case "--log":
					DevLog.Enabled = true;
					break;
				case "--menu":
					_openMenu = true;
					_openDifficulty = value == "difficulty";
					break;
				case "--demo-spin":
					_demoSpin = true;
					break;
				case "--spin":
					_initialSpin = Mathf.DegToRad(float.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
					break;
				case "--clip":
					_clip = value;
					break;
			}
		}
	}

	private void RunDemo(float dt)
	{
		if (!_demo)
			return;
		_demoTimer += dt;
		bool undoNext = _demoMoves.Count > 0 && _demoMoves.Peek() == DemoUndo;
		if ((_phase == Phase.Ready && !IsComputerTurn || _phase == Phase.RoundOver && undoNext) && _demoMoves.Count > 0 && _demoTimer > _demoDelay)
		{
			_demoTimer = 0f;
			int next = _demoMoves.Dequeue();
			if (next == DemoUndo)
				OnUndoPressed();
			else
				TryPlay(next);
		}
		else if (_phase == Phase.RoundOver && _demoSpin && _demoTimer > 0.5f)
		{
			// Spin the finished board to check the discs stay locked in place.
			_demoSpin = false;
			_demoTimer = -2.5f;
			_spinVelocity = 3.5f;
		}
		else if (_phase == Phase.RoundOver && _demoTimer > 1.5f)
		{
			_demoTimer = 0f;
			Release();
		}
	}

	// ---------------------------------------------------------------- UI

	private IEnumerable<Disc> AllDiscs()
	{
		foreach (var disc in _discs)
		{
			if (disc != null)
				yield return disc;
		}
		foreach (var disc in _loose)
			yield return disc;
	}

	private void RefreshUi()
	{
		var pieces = Styles.Pieces(_settings.Pieces);
		// Against the computer, "You" and "Computer" read better than the piece colours.
		string NameOf(Player player) => !VsComputer ? pieces.NameOf(player) : player == ComputerSide ? "Computer" : "You";
		_hud.SetScore($"{NameOf(Player.One)}  {_wins[Player.One]}   —   {_wins[Player.Two]}  {NameOf(Player.Two)}");
		_hud.SetClearVisible(_phase == Phase.RoundOver);
		_hud.SetUndoVisible(CanUndo);
		_hud.SetRestartVisible(_phase == Phase.Ready && _match.MoveCount > 0 && !IsComputerTurn);

		var current = _match.CurrentPlayer;
		switch (_phase)
		{
			case Phase.Advert:
				break;
			case Phase.Releasing:
				_hud.SetStatus("Releasing…", Palette.TextMuted);
				break;
			case Phase.RoundOver when _match.Status == MatchStatus.Won:
				string winner = NameOf(_match.Winner);
				_hud.SetStatus(winner == "You" ? "You win!" : $"{winner} wins!", pieces.UiColorOf(_match.Winner));
				break;
			case Phase.RoundOver:
				_hud.SetStatus("It's a draw", Palette.Text);
				break;
			case Phase.Dropping:
				break;
			case Phase.Ready when VsComputer && current == ComputerSide:
				_hud.SetStatus("Computer is thinking…", pieces.UiColorOf(current));
				break;
			case Phase.Ready when VsComputer:
				_hud.SetStatus("Your turn", pieces.UiColorOf(current));
				break;
			default:
				_hud.SetStatus($"{NameOf(current)}'s turn", pieces.UiColorOf(current));
				break;
		}
	}
}
