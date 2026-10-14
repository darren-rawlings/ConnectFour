using Godot;

namespace ConnectFour.Game.Audio;

/// <summary>
/// A fixed set of positional players reused round-robin, so a 42-disc cascade can't spawn
/// unbounded nodes. When all are busy the oldest sound is cut off, which is inaudible in a clatter.
/// </summary>
public partial class SoundPool : Node3D
{
	private const int Voices = 20;

	private readonly AudioStreamPlayer3D[] _players = new AudioStreamPlayer3D[Voices];
	private int _next;

	public override void _Ready()
	{
		for (int i = 0; i < Voices; i++)
		{
			_players[i] = MakePlayer();
			AddChild(_players[i]);
		}
	}

	/// <summary>Plays a strike of the given material. Strength is 0..1.</summary>
	public void PlayImpact(Style style, Vector3 position, float strength)
	{
		var player = _players[_next];
		_next = (_next + 1) % Voices;
		player.Stream = SoundBank.RandomImpact(style);
		player.GlobalPosition = position;
		player.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(strength, 0.02f, 1f)) + Styles.GainDb(style);
		// Light taps sound slightly higher than full-force hits, as with real objects.
		player.PitchScale = (float)GD.RandRange(0.95, 1.05) + (1f - strength) * 0.06f;
		player.Play();
	}

	/// <summary>
	/// Positional player with distance attenuation turned off: the camera is always about the same
	/// distance away, and we only want left/right panning as the board spins.
	/// </summary>
	public static AudioStreamPlayer3D MakePlayer() => new()
	{
		AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled,
		PanningStrength = 0.6f,
	};
}
