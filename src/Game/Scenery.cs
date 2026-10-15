using Godot;

namespace ConnectFour.Game;

/// <summary>A setting behind the board: sky, lighting mood and tabletop.</summary>
public enum Backdrop
{
	Midnight,
	GamesRoom,
	Sunset,
	Daylight,
	Dojo,
}

/// <summary>Everything that changes with the backdrop. The table's sound is what discs make landing on it.</summary>
public sealed record BackdropLook(
	string Name,
	Color SkyTop,
	Color SkyHorizon,
	Color Ground,
	Look Table,
	Style TableSound,
	Color KeyLight,
	float KeyEnergy,
	float KeyPitch,
	Color FillLight,
	float FillEnergy,
	float Ambient);

/// <summary>
/// The world around the board: sky, key and fill lights, and the table discs land on when
/// released. Built once and restyled in place when the player picks another backdrop.
/// </summary>
public partial class Scenery : Node3D
{
	public const string TableGroup = "table";

	public static readonly Backdrop[] Choices =
		{ Backdrop.Midnight, Backdrop.GamesRoom, Backdrop.Sunset, Backdrop.Daylight, Backdrop.Dojo };

	private readonly ProceduralSkyMaterial _sky = new() { SunAngleMax = 30f };
	private readonly Godot.Environment _environment = new();
	private readonly DirectionalLight3D _key = new() { ShadowEnabled = true };
	private readonly OmniLight3D _fill = new() { Position = new Vector3(-6, 6, 7), OmniRange = 25 };
	private readonly MeshInstance3D _tableTop = new() { Mesh = new PlaneMesh { Size = new Vector2(60, 60) } };

	public Backdrop Backdrop { get; private set; }

	/// <summary>Material sound of the tabletop for the current backdrop.</summary>
	public Style TableSound => Looks(Backdrop).TableSound;

	public static BackdropLook Looks(Backdrop backdrop) => backdrop switch
	{
		Backdrop.GamesRoom => new("Games Room",
			new("2a1a12"), new("4a3020"), new("120a06"),
			new(Look.PlasticPattern, new("143823"), new("0e2a19"), 0.95f, Scale: 3f), Style.Table,
			new("ffe2b0"), 1.3f, -55f, new("ffb070"), 0.4f, 0.45f),
		Backdrop.Sunset => new("Sunset",
			new("2b2a6b"), new("ff8a5c"), new("2a1410"),
			new(Look.PlankPattern, new("6a3f22"), new("2e1a0d"), 0.6f), Style.Wood,
			new("ffb27a"), 1.25f, -28f, new("8a7cff"), 0.5f, 0.55f),
		Backdrop.Daylight => new("Daylight",
			new("4a8fe0"), new("c9e3ff"), new("6b7a8f"),
			new(Look.PlankPattern, new("a8834f"), new("6e4c28"), 0.55f), Style.Wood,
			new("fff6e0"), 1.0f, -60f, new("c8dcff"), 0.35f, 0.45f),
		Backdrop.Dojo => new("Dojo",
			new("3a1c1c"), new("c9a46a"), new("2a1a10"),
			new(Look.TatamiPattern, new("7f8a55"), new("596234"), 0.85f), Style.Table,
			new("ffe8c0"), 1.0f, -50f, new("ff9d6e"), 0.4f, 0.6f),
		_ => new("Midnight",
			new("1d2463"), new("3a4590"), new("0b0d24"),
			new(Look.PlasticPattern, new("1b2240"), new("141a33"), 0.95f, Scale: 3f), Style.Table,
			new("ffffff"), 1.3f, -50f, new("b9c8ff"), 0.6f, 0.6f),
	};

	public void Build(Backdrop backdrop, float tableY)
	{
		_environment.BackgroundMode = Godot.Environment.BGMode.Sky;
		_environment.Sky = new Sky { SkyMaterial = _sky };
		_environment.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
		_environment.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
		_environment.TonemapMode = Godot.Environment.ToneMapper.Filmic;
		AddChild(new WorldEnvironment { Environment = _environment });
		AddChild(_key);
		AddChild(_fill);

		var table = new StaticBody3D { Position = new Vector3(0, tableY, 0) };
		table.AddToGroup(TableGroup);
		table.AddChild(_tableTop);
		table.AddChild(new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = new Vector3(60, 1, 60) },
			Position = new Vector3(0, -0.5f, 0),
		});
		AddChild(table);

		Apply(backdrop);
	}

	public void Apply(Backdrop backdrop)
	{
		Backdrop = backdrop;
		var look = Looks(backdrop);
		_sky.SkyTopColor = look.SkyTop;
		_sky.SkyHorizonColor = look.SkyHorizon;
		_sky.GroundHorizonColor = look.SkyHorizon;
		_sky.GroundBottomColor = look.Ground;
		_environment.AmbientLightEnergy = look.Ambient;
		_key.LightColor = look.KeyLight;
		_key.LightEnergy = look.KeyEnergy;
		_key.RotationDegrees = new Vector3(look.KeyPitch, 30, 0);
		_fill.LightColor = look.FillLight;
		_fill.LightEnergy = look.FillEnergy;
		_tableTop.MaterialOverride = MaterialFactory.For(look.Table, isDisc: false);
	}
}
