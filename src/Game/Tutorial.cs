using System.Collections.Generic;
using Renegade.Ui;

namespace ConnectFour.Game;

/// <summary>The guided tour. Clips are recorded from the game with --clip (see Main.PlayClip).</summary>
public static class Tutorial
{
	public static readonly IReadOnlyList<TutorialPage> Pages = new[]
	{
		new TutorialPage("Four in a row",
			"Take turns dropping discs into the board. The first to line up four of their colour wins: across, up and down, or diagonally.",
			"res://tutorial/drop.ogv"),
		new TutorialPage("Drop a disc",
			"Tap a column and your disc falls to the lowest free space. Watch where your opponent could make four, and block them.",
			"res://tutorial/drop.ogv"),
		new TutorialPage("Counter fire",
			"Make four in a row and the winning disc bursts into flame. The fire runs along your line to the rest of the four.",
			"res://tutorial/win.ogv"),
		new TutorialPage("Release the pieces",
			"After a round, press Release Pieces: the tray slides out and every disc drops away. Who goes first swaps each round.",
			"res://tutorial/release.ogv"),
		new TutorialPage("Tips",
			"Drag sideways to spin the board, or freeze it in Settings. Against the computer, Undo (ad) takes back a move, and Start over counts as resigning unless you watch an ad.",
			"res://tutorial/win.ogv"),
	};
}
