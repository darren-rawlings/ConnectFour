using Godot;
using Renegade.Audio;

namespace ConnectFour.Game.Audio;

/// <summary>Counter Fire's impact sounds: one synthesised set per material, played through the shared pool.</summary>
public static class SoundBank
{
	private static readonly ImpactBank<Style> Impacts = new(Styles.Impact);

	/// <summary>A strike of the given material. Strength is 0..1; boostDb lifts key sounds such as landings.</summary>
	public static void PlayImpact(this SoundPool pool, Style style, Vector3 position, float strength, float boostDb = 0f) =>
		pool.PlayImpact(Impacts.Random(style), position, strength, Styles.GainDb(style) + boostDb);
}
