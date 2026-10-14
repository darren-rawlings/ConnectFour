using Godot;

namespace ConnectFour.Game;

/// <summary>Player preferences saved between sessions in user://settings.cfg.</summary>
public sealed class Settings
{
	private const string Path = "user://settings.cfg";
	private const string Section = "preferences";

	public Style Board { get; set; } = Style.Plastic;
	public Style Pieces { get; set; } = Style.Plastic;
	public bool Muted { get; set; }

	public static Settings Load()
	{
		var settings = new Settings();
		var file = new ConfigFile();
		if (file.Load(Path) != Error.Ok)
			return settings;
		settings.Board = ReadStyle(file, "board", settings.Board);
		settings.Pieces = ReadStyle(file, "pieces", settings.Pieces);
		settings.Muted = file.GetValue(Section, "muted", false).AsBool();
		return settings;
	}

	public void Save()
	{
		var file = new ConfigFile();
		file.SetValue(Section, "board", Board.ToString());
		file.SetValue(Section, "pieces", Pieces.ToString());
		file.SetValue(Section, "muted", Muted);
		file.Save(Path);
	}

	private static Style ReadStyle(ConfigFile file, string key, Style fallback)
	{
		string name = file.GetValue(Section, key, fallback.ToString()).AsString();
		return System.Enum.TryParse(name, out Style style) && style != Style.Table ? style : fallback;
	}
}
