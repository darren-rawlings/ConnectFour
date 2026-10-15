using ConnectFour.Core;
using Xunit;

namespace ConnectFour.Tests;

public class UndoAndAdTests
{
	[Fact]
	public void UndoRemovesTheLastDiscAndGivesTheTurnBack()
	{
		var match = new Match();
		match.Play(3); // Red
		match.Play(3); // Yellow

		Assert.Equal(new Cell(3, 1), match.Undo());
		Assert.Equal(Player.None, match.Board[3, 1]);
		Assert.Equal(Player.One, match.Board[3, 0]);
		Assert.Equal(Player.Two, match.CurrentPlayer);
		Assert.Equal(1, match.Board.DiscCount);
		Assert.Equal(Player.One, match.LastMover);
	}

	[Fact]
	public void UndoAfterAWinReopensTheRound()
	{
		var match = new Match();
		foreach (int column in new[] { 0, 6, 1, 6, 2, 6, 3 })
			match.Play(column);
		Assert.Equal(MatchStatus.Won, match.Status);

		match.Undo();
		Assert.Equal(MatchStatus.InProgress, match.Status);
		Assert.Equal(Player.None, match.Winner);
		Assert.Equal(Player.One, match.CurrentPlayer);
		Assert.NotNull(match.Play(5));
	}

	[Fact]
	public void UndoWithNoMovesDoesNothing()
	{
		var match = new Match();
		Assert.Null(match.Undo());
		Assert.Equal(Player.One, match.CurrentPlayer);
	}

	[Fact]
	public void ResetClearsHistory()
	{
		var match = new Match();
		match.Play(0);
		match.Reset(Player.Two);
		Assert.Equal(0, match.MoveCount);
		Assert.Null(match.Undo());
	}

	[Fact]
	public void AdShowsAfterEveryFifthGame()
	{
		var schedule = new AdSchedule();
		var due = new bool[12];
		for (int i = 0; i < due.Length; i++)
			due[i] = schedule.GameFinished();
		Assert.Equal(new[] { false, false, false, false, true, false, false, false, false, true, false, false }, due);
	}

	[Fact]
	public void AdCountSurvivesARestart()
	{
		var schedule = new AdSchedule(gamesSinceAd: 3);
		Assert.False(schedule.GameFinished());
		Assert.True(schedule.GameFinished());
	}
}
