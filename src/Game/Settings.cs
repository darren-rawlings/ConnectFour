using ConnectFour.Core;
using Godot;

namespace ConnectFour.Game;

/// <summary>Player preferences saved between sessions in user://settings.cfg.</summary>
public sealed class Settings
{
	private const string Path = "user://settings.cfg";
	private const string Section = "preferences";

	public Style Board { get; set; } = Style.Plastic;
	public Style Pieces { get; set; } = Style.Plastic;
	public Backdrop Backdrop { get; set; } = Backdrop.Midnight;
	public GameMode Mode { get; set; } = GameMode.PlayerVsPlayer;
	public Difficulty Difficulty { get; set; } = Difficulty.Medium;
	public bool Muted { get; set; }

	/// <summary>Stops drag-to-spin, for players who find the board moving when they meant to tap.</summary>
	public bool FreezeBoard { get; set; }

	/// <summary>The tutorial is offered once, the first time the game opens.</summary>
	public bool TutorialOffered { get; set; }

	/// <summary>Finished games since the last between-games ad; see <see cref="Renegade.Core.AdSchedule"/>.</summary>
	public int GamesSinceAd { get; set; }

	public static Settings Load()
	{
		var settings = new Settings();
		var file = new ConfigFile();
		if (file.Load(Path) != Error.Ok)
			return settings;
		settings.Board = ReadStyle(file, "board", settings.Board);
		settings.Pieces = ReadStyle(file, "pieces", settings.Pieces);
		settings.Backdrop = ReadEnum(file, "backdrop", settings.Backdrop);
		settings.Mode = ReadEnum(file, "mode", settings.Mode);
		settings.Difficulty = ReadEnum(file, "difficulty", settings.Difficulty);
		settings.Muted = file.GetValue(Section, "muted", false).AsBool();
		settings.FreezeBoard = file.GetValue(Section, "freeze_board", false).AsBool();
		settings.TutorialOffered = file.GetValue(Section, "tutorial_offered", false).AsBool();
		settings.GamesSinceAd = file.GetValue(Section, "games_since_ad", 0).AsInt32();
		return settings;
	}

	public void Save()
	{
		var file = new ConfigFile();
		file.SetValue(Section, "board", Board.ToString());
		file.SetValue(Section, "pieces", Pieces.ToString());
		file.SetValue(Section, "backdrop", Backdrop.ToString());
		file.SetValue(Section, "mode", Mode.ToString());
		file.SetValue(Section, "difficulty", Difficulty.ToString());
		file.SetValue(Section, "muted", Muted);
		file.SetValue(Section, "freeze_board", FreezeBoard);
		file.SetValue(Section, "tutorial_offered", TutorialOffered);
		file.SetValue(Section, "games_since_ad", GamesSinceAd);
		file.Save(Path);
	}

	private static Style ReadStyle(ConfigFile file, string key, Style fallback)
	{
		var style = ReadEnum(file, key, fallback);
		return style == Style.Table ? fallback : style;
	}

	private static T ReadEnum<T>(ConfigFile file, string key, T fallback) where T : struct, System.Enum
	{
		string name = file.GetValue(Section, key, fallback.ToString()).AsString();
		return System.Enum.TryParse(name, out T value) && System.Enum.IsDefined(value) ? value : fallback;
	}
}
