using ConnectFour.Core;
using Godot;

namespace ConnectFour.Game;

/// <summary>Central colour definitions so the look can be retuned in one place.</summary>
public static class Palette
{
	public static readonly Color BackgroundTop = new("1d2463");
	public static readonly Color BackgroundBottom = new("0b0d24");

	public static readonly Color Board = new("2f5fe8");
	public static readonly Color BoardEdge = new("1c3a9c");
	public static readonly Color Hole = new("101436");
	public static readonly Color HoleRim = new("24307a");

	public static readonly Color PlayerOne = new("ef4444");
	public static readonly Color PlayerTwo = new("facc15");

	public static readonly Color Text = new("f1f5ff");
	public static readonly Color TextMuted = new("9aa6d8");

	public static Color For(Player player) => player switch
	{
		Player.One => PlayerOne,
		Player.Two => PlayerTwo,
		_ => Hole,
	};

	public static string NameOf(Player player) => player switch
	{
		Player.One => "Red",
		Player.Two => "Yellow",
		_ => "",
	};
}
