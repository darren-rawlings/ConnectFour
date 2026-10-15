using ConnectFour.Core;
using ConnectFour.Core.Synth;
using Godot;

namespace ConnectFour.Game;

/// <summary>A material family. Each one has a look, a sound, and physical bounciness.</summary>
public enum Style
{
	Plastic,
	Wood,
	Metal,
	Glass,
	Marble,

	/// <summary>The tabletop under the board. Not selectable in the menu.</summary>
	Table,
}

/// <summary>Shader settings for one surface. <see cref="Pattern"/> ids match surface.gdshader.</summary>
public sealed record Look(
	int Pattern,
	Color A,
	Color B,
	float Roughness,
	float Metallic = 0f,
	float Scale = 1f,
	float Opacity = 1f)
{
	public const int PlasticPattern = 0;
	public const int WoodPattern = 1;
	public const int MetalPattern = 2;
	public const int MarblePattern = 3;
	public const int GlassPattern = 4;
	public const int PlankPattern = 5;
	public const int TatamiPattern = 6;

	public bool IsGlass => Pattern == GlassPattern;
}

public sealed record PieceSet(string Name, string NameOne, string NameTwo, Look One, Look Two, Color UiOne, Color UiTwo)
{
	public string NameOf(Player player) => player == Player.One ? NameOne : NameTwo;
	public Look LookOf(Player player) => player == Player.One ? One : Two;
	public Color UiColorOf(Player player) => player == Player.One ? UiOne : UiTwo;
}

/// <summary>Every tunable number for the materials lives here, so restyling never touches game code.</summary>
public static class Styles
{
	public static readonly Style[] Choices = { Style.Plastic, Style.Wood, Style.Metal, Style.Glass, Style.Marble };

	public static string BoardName(Style style) => style switch
	{
		Style.Plastic => "Classic",
		Style.Wood => "Oak",
		Style.Metal => "Steel",
		Style.Glass => "Glass",
		Style.Marble => "Marble",
		_ => "Table",
	};

	public static Look BoardLook(Style style) => style switch
	{
		Style.Wood => new(Look.WoodPattern, new("c89058"), new("8a5a2b"), 0.55f, Scale: 0.9f),
		Style.Metal => new(Look.MetalPattern, new("c3c9d2"), new("7d848f"), 0.3f, Metallic: 1f),
		Style.Glass => new(Look.GlassPattern, new("9fd4ff"), new("5aa0e0"), 0.05f, Opacity: 0.28f),
		Style.Marble => new(Look.MarblePattern, new("b9b3a8"), new("5e5a54"), 0.2f, Scale: 0.55f),
		_ => new(Look.PlasticPattern, new("2f5fe8"), new("1c3a9c"), 0.3f),
	};

	public static PieceSet Pieces(Style style) => style switch
	{
		Style.Wood => new("Wood", "Walnut", "Maple",
			new(Look.WoodPattern, new("6b4426"), new("3b2414"), 0.55f),
			new(Look.WoodPattern, new("e8c891"), new("c49a5e"), 0.5f),
			new("c08850"), new("e8c891")),
		Style.Metal => new("Metal", "Copper", "Silver",
			new(Look.MetalPattern, new("d08850"), new("8f4a24"), 0.32f, Metallic: 1f),
			new(Look.MetalPattern, new("e3e7ec"), new("9aa1ab"), 0.28f, Metallic: 1f),
			new("e09060"), new("e3e7ec")),
		Style.Glass => new("Glass", "Ruby", "Sapphire",
			new(Look.GlassPattern, new("e11d48"), new("7f0a26"), 0.05f, Opacity: 0.6f),
			new(Look.GlassPattern, new("2563eb"), new("1e3a8a"), 0.05f, Opacity: 0.6f),
			new("ff4d7a"), new("6b9bff")),
		Style.Marble => new("Marble", "Onyx", "Ivory",
			new(Look.MarblePattern, new("202026"), new("8a8a96"), 0.15f),
			new(Look.MarblePattern, new("f3eee2"), new("b9ae96"), 0.15f),
			new("a6abbf"), new("f3eee2")),
		_ => new("Plastic", "Red", "Yellow",
			new(Look.PlasticPattern, new("ef4444"), new("b91c1c"), 0.28f),
			new(Look.PlasticPattern, new("facc15"), new("ca8a04"), 0.28f),
			new("ef4444"), new("facc15")),
	};

	public static ImpactRecipe Impact(Style style) => style switch
	{
		Style.Wood => new(750, new[] { 1.0, 2.57, 4.2, 6.1 }, new[] { 1.0, 0.6, 0.3, 0.15 }, 0.045, 0.7, 0.006, 0.35, 0.3),
		Style.Metal => new(2100, new[] { 1.0, 2.756, 5.404, 8.933 }, new[] { 1.0, 0.7, 0.5, 0.3 }, 0.45, 0.3, 0.002, 0.7, 0.9),
		Style.Glass => new(3300, new[] { 1.0, 2.32, 4.25, 6.63 }, new[] { 1.0, 0.5, 0.35, 0.2 }, 0.3, 0.25, 0.0015, 0.8, 0.7),
		Style.Marble => new(1400, new[] { 1.0, 1.83, 2.9, 4.4 }, new[] { 1.0, 0.7, 0.4, 0.2 }, 0.05, 0.9, 0.005, 0.55, 0.3),
		Style.Table => new(420, new[] { 1.0, 2.4, 3.9 }, new[] { 1.0, 0.4, 0.2 }, 0.03, 0.8, 0.008, 0.2, 0.25),
		_ => new(1900, new[] { 1.0, 2.2, 3.7 }, new[] { 1.0, 0.5, 0.25 }, 0.035, 0.5, 0.004, 0.5, 0.3),
	};

	/// <summary>Tone of the scrape as a disc slides down a slot (noise brightness, low cut).</summary>
	public static (double Brightness, double LowCut) Slide(Style style) => style switch
	{
		Style.Wood => (0.2, 0.02),
		Style.Metal => (0.6, 0.15),
		Style.Glass => (0.7, 0.2),
		Style.Marble => (0.3, 0.04),
		_ => (0.35, 0.05),
	};

	/// <summary>Long-ringing materials are quieter per hit so a cascade doesn't get harsh.</summary>
	public static float GainDb(Style style) => style switch
	{
		Style.Metal => -5f,
		Style.Glass => -4f,
		Style.Table => -2f,
		_ => 0f,
	};

	public static float Bounce(Style style) => style switch
	{
		Style.Metal => 0.3f,
		Style.Glass => 0.35f,
		Style.Wood => 0.2f,
		Style.Marble => 0.15f,
		_ => 0.25f,
	};
}
