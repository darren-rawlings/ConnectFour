using System;
using System.Collections.Generic;

namespace ConnectFour.Core;

public enum MatchStatus
{
	InProgress,
	Won,
	Draw,
}

public readonly record struct MoveResult(
	Cell Cell,
	Player Player,
	MatchStatus Status,
	IReadOnlyList<Cell> WinningCells);

/// <summary>Turn order and win/draw rules for a single round on a <see cref="Board"/>.</summary>
public sealed class Match
{
	public Board Board { get; } = new();
	public Player CurrentPlayer { get; private set; }
	public Player Winner { get; private set; }
	public MatchStatus Status { get; private set; }

	public Match(Player startingPlayer = Player.One)
	{
		Reset(startingPlayer);
	}

	public void Reset(Player startingPlayer)
	{
		if (startingPlayer == Player.None)
			throw new ArgumentException("A match must start with a real player.", nameof(startingPlayer));
		Board.Clear();
		CurrentPlayer = startingPlayer;
		Winner = Player.None;
		Status = MatchStatus.InProgress;
	}

	public bool CanPlay(int column) =>
		Status == MatchStatus.InProgress && Board.NextOpenRow(column) >= 0;

	/// <summary>Plays the current player's disc in a column. Returns null if the move is illegal.</summary>
	public MoveResult? Play(int column)
	{
		if (!CanPlay(column))
			return null;

		var player = CurrentPlayer;
		int row = Board.Drop(column, player);
		var winningCells = Board.FindWinThrough(column, row);

		if (winningCells.Count > 0)
		{
			Status = MatchStatus.Won;
			Winner = player;
		}
		else if (Board.IsFull)
		{
			Status = MatchStatus.Draw;
		}
		else
		{
			CurrentPlayer = Opponent(player);
		}

		return new MoveResult(new Cell(column, row), player, Status, winningCells);
	}

	public static Player Opponent(Player player) => player == Player.One ? Player.Two : Player.One;
}
