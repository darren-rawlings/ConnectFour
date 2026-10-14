using System.Linq;
using ConnectFour.Core;
using Xunit;

namespace ConnectFour.Tests;

public class MatchTests
{
	private static MoveResult? PlayAll(Match match, params int[] columns)
	{
		MoveResult? last = null;
		foreach (int column in columns)
			last = match.Play(column);
		return last;
	}

	[Fact]
	public void DiscsStackFromTheBottom()
	{
		var match = new Match();
		Assert.Equal(0, match.Play(3)!.Value.Cell.Row);
		Assert.Equal(1, match.Play(3)!.Value.Cell.Row);
	}

	[Fact]
	public void PlayersAlternate()
	{
		var match = new Match(Player.Two);
		Assert.Equal(Player.Two, match.Play(0)!.Value.Player);
		Assert.Equal(Player.One, match.CurrentPlayer);
	}

	[Fact]
	public void FullColumnRejectsMoves()
	{
		var match = new Match();
		PlayAll(match, 0, 0, 0, 0, 0, 0);
		Assert.Null(match.Play(0));
		Assert.Null(match.Play(-1));
		Assert.Null(match.Play(Board.Columns));
	}

	[Fact]
	public void HorizontalWin()
	{
		var match = new Match();
		var result = PlayAll(match, 0, 0, 1, 1, 2, 2, 3);
		Assert.Equal(MatchStatus.Won, result!.Value.Status);
		Assert.Equal(Player.One, match.Winner);
		Assert.Equal(4, result.Value.WinningCells.Count);
	}

	[Fact]
	public void VerticalWin()
	{
		var match = new Match();
		var result = PlayAll(match, 4, 5, 4, 5, 4, 5, 4);
		Assert.Equal(MatchStatus.Won, result!.Value.Status);
	}

	[Fact]
	public void DiagonalWinIsDetectedFromAnyDiscInTheLine()
	{
		// Player One builds a rising diagonal (0,0) (1,1) (2,2) (3,3), finishing in the middle at (2,2).
		var match = new Match();
		var result = PlayAll(match, 0, 1, 1, 2, 3, 3, 3, 2, 3, 6, 2);
		Assert.Equal(MatchStatus.Won, result!.Value.Status);
		Assert.Equal(Player.One, result.Value.Player);
		Assert.Contains(new Cell(0, 0), result.Value.WinningCells);
		Assert.Contains(new Cell(3, 3), result.Value.WinningCells);
	}

	[Fact]
	public void NoMovesAfterWin()
	{
		var match = new Match();
		PlayAll(match, 0, 1, 0, 1, 0, 1, 0);
		Assert.Null(match.Play(2));
	}

	[Fact]
	public void FullBoardWithoutLineIsDraw()
	{
		// Column pairs filled in an order that never lines up four.
		var match = new Match();
		int[] order = { 0, 1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 0,
		                2, 3, 2, 3, 2, 3, 3, 2, 3, 2, 3, 2,
		                4, 5, 4, 5, 4, 5, 5, 4, 5, 4, 5, 4,
		                6, 6, 6, 6, 6, 6 };
		var result = PlayAll(match, order);
		Assert.Equal(MatchStatus.Draw, result!.Value.Status);
		Assert.True(match.Board.IsFull);
	}

	[Fact]
	public void ResetClearsBoard()
	{
		var match = new Match();
		PlayAll(match, 0, 1, 2);
		match.Reset(Player.Two);
		Assert.Equal(0, match.Board.DiscCount);
		Assert.Equal(Player.Two, match.CurrentPlayer);
		Assert.Equal(MatchStatus.InProgress, match.Status);
	}
}
