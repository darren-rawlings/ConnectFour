using System.Collections.Generic;
using ConnectFour.Core;
using ConnectFour.Game.Audio;
using ConnectFour.Game.Ui;
using Godot;
using static ConnectFour.Game.BoardLayout;

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
	}

	private const float SpinPerPixel = 0.008f;
	private const float SpinDamping = 1.6f;
	private const float DragThreshold = 14f;
	private const float SettleSpeed = 0.15f;
	private const float SettleHold = 0.12f;
	private const float MaxFallTime = 3f;
	private const float ReleaseTime = 2.6f;

	private readonly Match _match = new();
	private readonly Dictionary<Player, int> _wins = new() { [Player.One] = 0, [Player.Two] = 0 };
	private readonly Disc?[,] _discs = new Disc?[Board.Columns, Board.Rows];
	private readonly List<Disc> _loose = new();
	private readonly HashSet<Disc> _tipped = new();
	private Player _nextStarter = Player.Two;
	private Settings _settings = null!;

	private Camera3D _camera = null!;
	private BoardRig _rig = null!;
	private Node3D _looseRoot = null!;
	private SoundPool _sound = null!;
	private Hud _hud = null!;
	private MeshInstance3D _ghost = null!;

	private Phase _phase = Phase.Ready;
	private Disc? _falling;
	private MoveResult? _pending;
	private float _fallTime;
	private float _settleTime;
	private int _hoverColumn = -1;

	// Spin input: a press becomes a drag once it moves far enough, otherwise it's a tap.
	private bool _pressing;
	private bool _dragging;
	private Vector2 _pressPosition;
	private float _spinVelocity;
	private ulong _lastDragUsec;

	// Developer demo (launch with `-- --demo`): plays scripted moves, then releases the pieces.
	private readonly Queue<int> _demoMoves = new();
	private bool _demo;
	private float _demoTimer;
	private float _initialSpin;

	public override void _Ready()
	{
		_settings = Settings.Load();
		ReadDeveloperArgs();
		AudioServer.SetBusMute(0, _settings.Muted);
		BuildWorld();
		_rig.RotateY(_initialSpin);

		_hud = new Hud();
		AddChild(_hud);
		_hud.Build(_settings.Board, _settings.Pieces, _settings.Muted);
		_hud.ClearPressed += Release;
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
		_hud.MuteToggled += muted =>
		{
			_settings.Muted = muted;
			_settings.Save();
			AudioServer.SetBusMute(0, muted);
		};
		RefreshUi();
	}

	// ---------------------------------------------------------------- World

	private void BuildWorld()
	{
		var sky = new ProceduralSkyMaterial
		{
			SkyTopColor = Palette.SkyTop,
			SkyHorizonColor = Palette.SkyHorizon,
			GroundHorizonColor = Palette.SkyHorizon,
			GroundBottomColor = Palette.Ground,
			SunAngleMax = 30f,
		};
		var environment = new Godot.Environment
		{
			BackgroundMode = Godot.Environment.BGMode.Sky,
			Sky = new Sky { SkyMaterial = sky },
			AmbientLightSource = Godot.Environment.AmbientSource.Sky,
			AmbientLightEnergy = 0.6f,
			ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
			TonemapMode = Godot.Environment.ToneMapper.Filmic,
		};
		AddChild(new WorldEnvironment { Environment = environment });

		AddChild(new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-50, 30, 0),
			LightEnergy = 1.3f,
			ShadowEnabled = true,
		});
		AddChild(new OmniLight3D
		{
			Position = new Vector3(-6, 6, 7),
			OmniRange = 25,
			LightEnergy = 0.6f,
			LightColor = new Color("b9c8ff"),
		});

		// Fixed horizontal FOV keeps the whole board in frame on any phone aspect ratio.
		_camera = new Camera3D
		{
			KeepAspect = Camera3D.KeepAspectEnum.Width,
			Fov = 40,
			Position = new Vector3(0, 4.6f, 13.6f),
		};
		AddChild(_camera);
		_camera.LookAt(new Vector3(0, 2.0f, 0));

		var table = new StaticBody3D { Position = new Vector3(0, TableY, 0) };
		table.AddToGroup("table");
		table.AddChild(new MeshInstance3D
		{
			Mesh = new PlaneMesh { Size = new Vector2(60, 60) },
			MaterialOverride = MaterialFactory.For(Styles.TableLook, isDisc: false),
		});
		table.AddChild(new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = new Vector3(60, 1, 60) },
			Position = new Vector3(0, -0.5f, 0),
		});
		AddChild(table);

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
	private bool CanSpin => _phase is Phase.Ready or Phase.RoundOver;

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
		RunDemo(dt);
		if (!_dragging && _spinVelocity != 0f)
		{
			// Ease to a stop, and freeze the spin the moment a disc needs the board still.
			if (!CanSpin || Mathf.Abs(_spinVelocity) < 0.02f)
				_spinVelocity = 0f;
			_rig.RotateY(_spinVelocity * dt);
			_spinVelocity *= Mathf.Exp(-SpinDamping * dt);
		}

		bool showGhost = _phase == Phase.Ready && _hoverColumn >= 0 && !_hud.MenuOpen;
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
			float speed = disc.LinearVelocity.Length();
			disc.SetSlide(BoardRig.InSlot(local) && speed > 0.5f ? Mathf.Clamp(speed / 25f, 0f, 1f) : 0f);

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

	private void TryPlay(int column)
	{
		if (_phase != Phase.Ready || column < 0 || _hud.MenuOpen)
			return;
		var result = _match.Play(column);
		if (result is not { } move)
			return;

		_spinVelocity = 0f;
		var disc = SpawnDisc(move.Player, move.Cell);
		disc.GlobalTransform = _rig.GlobalTransform * new Transform3D(Basis.Identity, new Vector3(ColumnX(column), SpawnY, 0));
		disc.LinearVelocity = _rig.GlobalBasis * new Vector3(0, -2f, 0);

		_falling = disc;
		_pending = move;
		_fallTime = 0f;
		_settleTime = 0f;
		_phase = Phase.Dropping;
		_hud.HideHint();
		RefreshUi();
	}

	private Disc SpawnDisc(Player player, Cell cell)
	{
		var disc = new Disc();
		disc.Setup(player, cell, _settings.Pieces);
		disc.Hit += OnDiscHit;
		_looseRoot.AddChild(disc);
		_loose.Add(disc);
		return disc;
	}

	private void CheckSettled(Disc disc, float dt)
	{
		_fallTime += dt;
		var local = _rig.ToLocal(disc.GlobalPosition);
		var target = CellCenter(disc.Cell);
		bool resting = disc.LinearVelocity.Length() < SettleSpeed && Mathf.Abs(local.Y - target.Y) < 0.2f;
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
		disc.Freeze = true;
		disc.SetSlide(0f);
		_loose.Remove(disc);
		disc.Reparent(_rig);
		disc.Position = CellCenter(disc.Cell);
		disc.Rotation = Vector3.Zero;
		_discs[disc.Cell.Column, disc.Cell.Row] = disc;

		var move = _pending;
		_pending = null;
		if (move is { Status: MatchStatus.Won } win)
		{
			_wins[win.Player]++;
			foreach (var cell in win.WinningCells)
				_discs[cell.Column, cell.Row]!.Mesh.MaterialOverlay = MaterialFactory.Glow;
			_phase = Phase.RoundOver;
		}
		else
		{
			_phase = move is { Status: MatchStatus.Draw } ? Phase.RoundOver : Phase.Ready;
		}
		RefreshUi();
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
		foreach (var disc in new List<Disc>(AllDiscs()))
		{
			disc.Mesh.MaterialOverlay = null;
			disc.Reparent(_looseRoot);
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
		foreach (var disc in _loose)
		{
			var d = disc;
			d.SetSlide(0f);
			var tween = CreateTween();
			tween.TweenProperty(d.Mesh, "scale", Vector3.One * 0.01f, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			tween.Finished += d.QueueFree;
		}
		_loose.Clear();
		_tipped.Clear();
		_rig.CloseTray();

		_match.Reset(_nextStarter);
		_nextStarter = Match.Opponent(_nextStarter);
		_phase = Phase.Ready;
		RefreshUi();
	}

	// ---------------------------------------------------------------- Sound

	private void OnDiscHit(Disc disc, float impact, Node? other)
	{
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
			Node node when node.IsInGroup("table") => Style.Table,
			_ => null,
		};
		if (surface is { } s && s != disc.Style)
			_sound.PlayImpact(s, position, strength * 0.6f);
	}

	// ---------------------------------------------------------------- Developer demo

	/// <summary>
	/// Optional user args after `--`: `--demo[=3,3,4,4,5,5,6]`, `--board=Wood`, `--pieces=Metal`,
	/// `--spin=35` (degrees). Style overrides are not saved. Used to record test footage.
	/// </summary>
	private void ReadDeveloperArgs()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			var parts = arg.Split('=', 2);
			string value = parts.Length > 1 ? parts[1] : "";
			switch (parts[0])
			{
				case "--demo":
					_demo = true;
					foreach (string column in (value == "" ? "3,3,4,4,5,5,6" : value).Split(','))
						_demoMoves.Enqueue(int.Parse(column));
					break;
				case "--board" when System.Enum.TryParse(value, out Style board):
					_settings.Board = board;
					break;
				case "--pieces" when System.Enum.TryParse(value, out Style pieces):
					_settings.Pieces = pieces;
					break;
				case "--spin":
					_initialSpin = Mathf.DegToRad(float.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
					break;
			}
		}
	}

	private void RunDemo(float dt)
	{
		if (!_demo)
			return;
		_demoTimer += dt;
		if (_phase == Phase.Ready && _demoMoves.Count > 0 && _demoTimer > 0.35f)
		{
			_demoTimer = 0f;
			TryPlay(_demoMoves.Dequeue());
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
		_hud.SetScore($"{pieces.NameOne}  {_wins[Player.One]}   —   {_wins[Player.Two]}  {pieces.NameTwo}");
		_hud.SetClearVisible(_phase == Phase.RoundOver);

		switch (_phase)
		{
			case Phase.Releasing:
				_hud.SetStatus("Releasing…", Palette.TextMuted);
				break;
			case Phase.RoundOver when _match.Status == MatchStatus.Won:
				_hud.SetStatus($"{pieces.NameOf(_match.Winner)} wins!", pieces.UiColorOf(_match.Winner));
				break;
			case Phase.RoundOver:
				_hud.SetStatus("It's a draw", Palette.Text);
				break;
			case Phase.Dropping:
				break;
			default:
				_hud.SetStatus($"{pieces.NameOf(_match.CurrentPlayer)}'s turn", pieces.UiColorOf(_match.CurrentPlayer));
				break;
		}
	}
}
