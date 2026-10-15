namespace ConnectFour.Core;

/// <summary>
/// Decides when the between-games ad is due: after every <see cref="GamesPerAd"/>th finished game.
/// The count is saved between sessions so quitting and relaunching doesn't skip ads.
/// </summary>
public sealed class AdSchedule
{
	public const int GamesPerAd = 5;

	public int GamesSinceAd { get; private set; }

	public AdSchedule(int gamesSinceAd = 0)
	{
		GamesSinceAd = gamesSinceAd < 0 ? 0 : gamesSinceAd % GamesPerAd;
	}

	/// <summary>Counts a finished game and returns true if an ad should show now.</summary>
	public bool GameFinished()
	{
		GamesSinceAd++;
		if (GamesSinceAd < GamesPerAd)
			return false;
		GamesSinceAd = 0;
		return true;
	}
}
