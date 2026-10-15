using Godot;

namespace ConnectFour.Game;

/// <summary>Interface colours. Board and piece colours live in <see cref="Styles"/>, backdrops in <see cref="Scenery"/>.</summary>
public static class Palette
{
	public static readonly Color Text = new("f1f5ff");
	public static readonly Color TextMuted = new("9aa6d8");
	public static readonly Color Accent = new("3b6cf0");
	public static readonly Color ButtonDark = new("27306a");
	public static readonly Color Panel = new Color("141a45") with { A = 0.94f };
}
