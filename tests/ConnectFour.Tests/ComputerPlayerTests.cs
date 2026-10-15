using System;
using System.Diagnostics;
using ConnectFour.Core;
using Xunit;
using Xunit.Abstractions;

namespace ConnectFour.Tests;

public class ComputerPlayerTests
{
	private readonly ITestOutputHelper _output;

	public ComputerPlayerTests(ITestOutputHelper output) => _output = output;

	private static Match Setup(params int[] columns)
	{
		var match = new Match();
		foreach (int column in columns)
			Assert.NotNull(match.Play(column));
		return match;
	}

	[Theory]
	[InlineData(Difficulty.Easy)]
	[InlineData(Difficulty.Medium)]
	[InlineData(Difficulty.Hard)]
	public void TakesAnImmediateWin(Difficulty difficulty)
	{
		// Red has three in a row along the bottom (columns 0-2) and it's Red's turn.
		var match = Setup(0, 0, 1, 1, 2, 6);
		var ai = new ComputerPlayer(difficulty, new Random(1));
		Assert.Equal(3, ai.ChooseMove(match.Board, Player.One));
	}

	[Theory]
	[InlineData(Difficulty.Medium)]
	[InlineData(Difficulty.Hard)]
	public void BlocksAnImmediateThreat(Difficulty difficulty)
	{
		// Red threatens column 3 on the bottom row; Yellow must block.
		var match = Setup(0, 6, 1, 6, 2);
		var ai = new ComputerPlayer(difficulty, new Random(1));
		Assert.Equal(3, ai.ChooseMove(match.Board, Player.Two));
	}

	[Fact]
	public void EasyBlocksWhenItNotices()
	{
		var match = Setup(0, 6, 1, 6, 2);
		var ai = new ComputerPlayer(Difficulty.Easy, new Random(1)) { EasyMissBlockChance = 0 };
		Assert.Equal(3, ai.ChooseMove(match.Board, Player.Two));
	}

	[Theory]
	[InlineData(Difficulty.Easy)]
	[InlineData(Difficulty.Hard)]
	public void NeverPicksAFullColumn(Difficulty difficulty)
	{
		var match = Setup(3, 3, 3, 3, 3, 3);
		var ai = new ComputerPlayer(difficulty, new Random(2));
		for (int i = 0; i < 20; i++)
			Assert.NotEqual(3, ai.ChooseMove(match.Board, Player.One));
	}

	[Theory]
	[InlineData(Difficulty.Medium, Difficulty.Easy)]
	[InlineData(Difficulty.Hard, Difficulty.Medium)]
	public void StrongerLevelUsuallyWins(Difficulty strong, Difficulty weak)
	{
		const int games = 10;
		int strongWins = 0, weakWins = 0;
		for (int game = 0; game < games; game++)
		{
			var first = new ComputerPlayer(strong, new Random(game));
			var second = new ComputerPlayer(weak, new Random(1000 + game));
			// Alternate who starts so neither side gets the first-move advantage every game.
			var starter = game % 2 == 0 ? Player.One : Player.Two;
			var match = new Match(starter);
			while (match.Status == MatchStatus.InProgress)
			{
				var mover = match.CurrentPlayer == Player.One ? first : second;
				Assert.NotNull(match.Play(mover.ChooseMove(match.Board, match.CurrentPlayer)));
			}
			if (match.Winner == Player.One)
				strongWins++;
			else if (match.Winner == Player.Two)
				weakWins++;
		}
		_output.WriteLine($"{strong} {strongWins} - {weakWins} {weak}");
		Assert.True(strongWins >= 7, $"{strong} won only {strongWins} of {games} against {weak}");
	}

	[Fact]
	public void HardMovesQuicklyFromAnEmptyBoard()
	{
		var ai = new ComputerPlayer(Difficulty.Hard, new Random(3));
		var watch = Stopwatch.StartNew();
		ai.ChooseMove(new Board(), Player.One);
		_output.WriteLine($"Hard opening move: {watch.ElapsedMilliseconds} ms");
		Assert.True(watch.ElapsedMilliseconds < 2000, $"took {watch.ElapsedMilliseconds} ms");
	}
}
