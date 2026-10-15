using System.Collections.Generic;
using Godot;

namespace ConnectFour.Game;

/// <summary>
/// Flames on a winning disc: a flare when it catches, steady flames and embers, and optionally a
/// flickering light. Particles live in world space, so they rise straight up and trail behind as
/// the board spins. The meshes and materials are shared by every fire, so lighting a line costs
/// no extra shader compiles after <see cref="Main"/> warms them up.
/// </summary>
public partial class DiscFire : Node3D
{
	private const float FlameLifetime = 0.9f;
	private const float EmberLifetime = 1.4f;

	private static QuadMesh? _flameQuad;

	/// <summary>Taller than wide, so each billboard reads as a tongue of flame rather than a puff.</summary>
	private static QuadMesh FlameQuad => _flameQuad ??= new QuadMesh { Size = new Vector2(0.45f, 0.7f) };
	private static QuadMesh? _emberQuad;
	private static StandardMaterial3D? _material;

	/// <summary>
	/// The disc sits between the board's panels, so flames rising from it would be hidden behind
	/// the wood. They burn just outside each face instead, in the hole, front and back.
	/// </summary>
	private static readonly float FaceOffset = BoardLayout.PanelOuterZ + 0.1f;

	private readonly List<CpuParticles3D> _burning = new();
	private readonly List<CpuParticles3D> _flares = new();
	private OmniLight3D? _light;
	private float _lightEnergy;
	private bool _out;

	/// <param name="withLight">One light per line is plenty; more would cost a lighting pass each on phones.</param>
	public DiscFire(bool withLight)
	{
		foreach (float z in new[] { FaceOffset, -FaceOffset })
		{
			var side = new Node3D { Position = new Vector3(0, 0, z) };
			AddChild(side);
			var flames = MakeFlames();
			var embers = MakeEmbers();
			var flare = MakeFlare();
			side.AddChild(flames);
			side.AddChild(embers);
			side.AddChild(flare);
			_burning.Add(flames);
			_burning.Add(embers);
			_flares.Add(flare);
		}
		if (withLight)
		{
			// Toward the camera, so the light falls on the board face and the burning discs.
			_light = new OmniLight3D
			{
				Position = new Vector3(0, 0.3f, 0.9f),
				LightColor = new Color(1f, 0.55f, 0.2f),
				OmniRange = 5f,
				LightEnergy = 0f,
			};
			AddChild(_light);
		}
	}

	public override void _Ready()
	{
		foreach (var flare in _flares)
			flare.Restart();
		if (_light != null)
			CreateTween().TweenMethod(Callable.From<float>(e => _lightEnergy = e), 0f, 2.2f, 0.25f);
	}

	public override void _Process(double delta)
	{
		if (_light == null)
			return;
		// Two out-of-step wobbles plus a little noise read as firelight rather than a pulse.
		float t = Time.GetTicksMsec() / 1000f;
		float flicker = 0.8f + 0.12f * Mathf.Sin(t * 13f) + 0.08f * Mathf.Sin(t * 29f + 1.3f) + (float)GD.RandRange(-0.05, 0.05);
		_light.LightEnergy = _lightEnergy * flicker;
	}

	/// <summary>Stops emitting, lets the last flames rise and fade, then removes itself.</summary>
	public void Extinguish()
	{
		if (_out)
			return;
		_out = true;
		foreach (var emitter in _burning)
			emitter.Emitting = false;
		if (_light != null)
			CreateTween().TweenMethod(Callable.From<float>(e => _lightEnergy = e), _lightEnergy, 0f, 0.3f);
		GetTree().CreateTimer(EmberLifetime).Timeout += QueueFree;
	}

	private static StandardMaterial3D Material => _material ??= new StandardMaterial3D
	{
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		BlendMode = BaseMaterial3D.BlendModeEnum.Add,
		BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
		VertexColorUseAsAlbedo = true,
		AlbedoTexture = SoftDot(),
		DisableReceiveShadows = true,
	};

	/// <summary>A round blob, bright in the middle and fading to nothing at the edge.</summary>
	private static GradientTexture2D SoftDot()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, Colors.White);
		gradient.SetColor(1, new Color(1, 1, 1, 0));
		return new GradientTexture2D
		{
			Gradient = gradient,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f),
			Width = 64,
			Height = 64,
		};
	}

	private static CpuParticles3D Base(int amount, float lifetime, QuadMesh mesh)
	{
		mesh.Material = Material;
		return new CpuParticles3D
		{
			Amount = amount,
			Lifetime = lifetime,
			Mesh = mesh,
			LocalCoords = false,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Direction = Vector3.Up,
		};
	}

	/// <summary>The body of the fire: hot white-yellow at the base, cooling to red as it rises.</summary>
	private static CpuParticles3D MakeFlames()
	{
		var flames = Base(28, FlameLifetime, FlameQuad);
		flames.EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere;
		flames.EmissionSphereRadius = BoardLayout.HoleRadius * 0.75f;
		flames.Spread = 12f;
		flames.Gravity = new Vector3(0, 4.5f, 0);
		flames.InitialVelocityMin = 0.6f;
		flames.InitialVelocityMax = 1.4f;
		flames.ScaleAmountMin = 0.6f;
		flames.ScaleAmountMax = 1.1f;
		flames.ScaleAmountCurve = Shrink();
		// Blending is additive, so colours are kept dim: bright ones washed out to white on light boards.
		flames.ColorRamp = Ramp(
			(0f, new Color(1f, 0.7f, 0.2f, 0f)),
			(0.1f, new Color(1f, 0.6f, 0.15f, 0.7f)),
			(0.45f, new Color(0.95f, 0.3f, 0.03f, 0.55f)),
			(0.8f, new Color(0.6f, 0.08f, 0.01f, 0.3f)),
			(1f, new Color(0.2f, 0.02f, 0f, 0f)));
		return flames;
	}

	/// <summary>Sparks that drift higher than the flames and wink out.</summary>
	private static CpuParticles3D MakeEmbers()
	{
		var embers = Base(7, EmberLifetime, _emberQuad ??= new QuadMesh { Size = new Vector2(0.09f, 0.09f) });
		embers.EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere;
		embers.EmissionSphereRadius = BoardLayout.HoleRadius * 0.6f;
		embers.Spread = 35f;
		embers.Gravity = new Vector3(0, 1.5f, 0);
		embers.InitialVelocityMin = 1f;
		embers.InitialVelocityMax = 2.2f;
		embers.ColorRamp = Ramp(
			(0f, new Color(1f, 0.9f, 0.5f, 1f)),
			(0.6f, new Color(1f, 0.4f, 0.05f, 0.9f)),
			(1f, new Color(0.6f, 0.1f, 0f, 0f)));
		return embers;
	}

	/// <summary>The burst when the disc catches: one outward puff of flame.</summary>
	private static CpuParticles3D MakeFlare()
	{
		var flare = Base(14, 0.45f, FlameQuad);
		flare.OneShot = true;
		flare.Emitting = false;
		flare.Explosiveness = 0.95f;
		flare.EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere;
		flare.EmissionSphereRadius = BoardLayout.HoleRadius * 0.5f;
		flare.Spread = 180f;
		flare.Gravity = new Vector3(0, 2f, 0);
		flare.InitialVelocityMin = 1.5f;
		flare.InitialVelocityMax = 2.5f;
		flare.ScaleAmountMin = 0.9f;
		flare.ScaleAmountMax = 1.4f;
		flare.ScaleAmountCurve = Shrink();
		flare.ColorRamp = Ramp(
			(0f, new Color(1f, 1f, 0.85f, 1f)),
			(0.35f, new Color(1f, 0.6f, 0.15f, 0.8f)),
			(1f, new Color(0.6f, 0.1f, 0.02f, 0f)));
		return flare;
	}

	private static Curve Shrink()
	{
		var curve = new Curve();
		curve.AddPoint(new Vector2(0f, 0.7f));
		curve.AddPoint(new Vector2(0.3f, 1f));
		curve.AddPoint(new Vector2(1f, 0.2f));
		return curve;
	}

	private static Gradient Ramp(params (float Offset, Color Color)[] stops)
	{
		var gradient = new Gradient();
		gradient.Offsets = System.Array.ConvertAll(stops, s => s.Offset);
		gradient.Colors = System.Array.ConvertAll(stops, s => s.Color);
		return gradient;
	}
}
