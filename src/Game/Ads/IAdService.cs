using System;

namespace ConnectFour.Game.Ads;

/// <summary>
/// Shows ads. The game only talks to this interface, so the on-screen stand-in used on desktop
/// and the real AdMob ads on phones are interchangeable.
/// </summary>
public interface IAdService
{
	/// <summary>Full-screen ad at a natural break. <paramref name="closed"/> runs when it's gone (or failed).</summary>
	void ShowInterstitial(Action closed);

	/// <summary>
	/// Opt-in ad that grants a reward. <paramref name="rewarded"/> runs only if the player watched it
	/// through; <paramref name="closed"/> always runs afterwards.
	/// </summary>
	void ShowRewarded(Action rewarded, Action closed);
}
