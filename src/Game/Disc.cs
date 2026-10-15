using System;
using ConnectFour.Core;
using Godot;

namespace ConnectFour.Game;

/// <summary>
/// A physical checker. It falls under (exaggerated) gravity and reports impacts so they can be
/// heard. Once it settles it is frozen and parented to the board so it spins with it.
/// </summary>
public partial class Disc : RigidBody3D
{
	/// <summary>Gravity multiplier. The board is ~6 m tall in world units; this makes drops feel table-sized.</summary>
	public const float Gravity = 6f;

	/// <summary>Velocity change (units/s) that counts as an impact. Resting contact is about g·dt ≈ 0.5.</summary>
	private const float MinImpact = 1.2f;
	private const float ImpactCooldown = 0.05f;

	public Player Player { get; private set; }
	public Cell Cell { get; private set; }
	public Style Style { get; private set; }
	public MeshInstance3D Mesh { get; } = new() { Mesh = Meshes.Disc };

	/// <summary>Raised with the impact speed and the body it hit (null if unknown).</summary>
	public event Action<Disc, float, Node?>? Hit;

	private readonly int _variant = GD.RandRange(0, MaterialFactory.Variants - 1);
	private Vector3 _previousVelocity;
	private float _cooldown;
	private float _gravity;
	private bool _lively;

	public void Setup(Player player, Cell cell, Style style)
	{
		Player = player;
		Cell = cell;
		GravityScale = Gravity;
		ContinuousCd = true;
		ContactMonitor = true;
		MaxContactsReported = 4;
		FreezeMode = FreezeModeEnum.Kinematic;
		CanSleep = true;

		// The lathed mesh's axis is local Y; stand it up so the faces point along the board's Z.
		var upright = new Basis(Vector3.Right, Mathf.Pi / 2f);
		Mesh.Transform = new Transform3D(upright, Vector3.Zero);
		AddChild(Mesh);
		AddChild(new CollisionShape3D
		{
			Shape = new CylinderShape3D { Radius = BoardLayout.DiscRadius, Height = BoardLayout.DiscThickness },
			Transform = new Transform3D(upright, Vector3.Zero),
		});
		ApplyStyle(style);
	}

	public override void _Ready()
	{
		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity") * Gravity;
	}

	/// <summary>
	/// Dropped discs barely bounce, so they land with one clean hit and settle quickly. Released
	/// discs get their material's full bounce so they clatter and scatter.
	/// </summary>
	public bool Lively
	{
		get => _lively;
		set
		{
			_lively = value;
			UpdatePhysicsMaterial();
		}
	}

	/// <summary>Places the disc and sets it moving. Call before adding it to the tree.</summary>
	public void Launch(Transform3D transform, Vector3 velocity)
	{
		Transform = transform;
		LinearVelocity = velocity;
		_previousVelocity = velocity;
	}

	public void ApplyStyle(Style style)
	{
		Style = style;
		Mesh.MaterialOverride = MaterialFactory.For(Styles.Pieces(style).LookOf(Player), isDisc: true, _variant);
		UpdatePhysicsMaterial();
	}

	private DiscFire? _fire;

	/// <summary>Sets the disc alight: a bright flash as it catches, then it burns until put out.</summary>
	public void Ignite(bool withLight)
	{
		if (_fire != null)
			return;
		var overlay = MaterialFactory.Fire();
		Mesh.MaterialOverlay = overlay;
		var tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(h => overlay.SetShaderParameter("heat", h)), 0f, 2.2f, 0.12f);
		tween.TweenMethod(Callable.From<float>(h => overlay.SetShaderParameter("heat", h)), 2.2f, 1f, 0.4f);
		_fire = new DiscFire(withLight);
		AddChild(_fire);
	}

	/// <summary>Puts the fire out; the last flames drift off on their own.</summary>
	public void Extinguish()
	{
		Mesh.MaterialOverlay = null;
		_fire?.Extinguish();
		_fire = null;
	}

	private void UpdatePhysicsMaterial() =>
		PhysicsMaterialOverride = new PhysicsMaterial { Bounce = Styles.Bounce(Style) * (_lively ? 1f : 0.15f), Friction = 0.35f };

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_cooldown -= dt;
		if (Freeze)
		{
			_previousVelocity = Vector3.Zero;
			return;
		}

		// Whatever change in velocity gravity doesn't explain was caused by hitting something.
		var expected = _previousVelocity + Vector3.Down * _gravity * dt;
		float impact = (LinearVelocity - expected).Length();
		_previousVelocity = LinearVelocity;

		if (impact < MinImpact || _cooldown > 0f)
			return;
		_cooldown = ImpactCooldown;
		var touching = GetCollidingBodies();
		Hit?.Invoke(this, impact, touching.Count > 0 ? touching[0] : null);
	}
}
