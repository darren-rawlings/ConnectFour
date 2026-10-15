using System;
using System.Collections.Generic;

namespace ConnectFour.Core;

public enum Difficulty
{
	Easy,
	Medium,
	Hard,
}

/// <summary>
/// Chooses moves for the computer opponent.
/// Easy takes a win it can see, usually blocks yours, and otherwise plays at random.
/// Medium and Hard search ahead with minimax and alpha-beta pruning (4 and 8 moves deep),
/// scoring unfinished positions by their open lines. Pure C#, so it can run on a worker thread.
/// </summary>
public sealed class ComputerPlayer
{
	private const int WinScore = 1_000_000;

	// Columns searched centre-first: central moves are usually best, which makes pruning cut more.
	private static readonly int[] SearchOrder = { 3, 2, 4, 1, 5, 0, 6 };

	private readonly Random _random;

	public Difficulty Difficulty { get; }

	/// <summary>Chance (0..1) that Easy fails to notice it must block. Keeps it beatable.</summary>
	public double EasyMissBlockChance { get; init; } = 0.3;

	public ComputerPlayer(Difficulty difficulty, Random? random = null)
	{
		Difficulty = difficulty;
		_random = random ?? new Random();
	}

	public int SearchDepth => Difficulty switch
	{
		Difficulty.Medium => 4,
		Difficulty.Hard => 8,
		_ => 1,
	};

	/// <summary>Picks a column for <paramref name="me"/>, or -1 if the board is full.</summary>
	public int ChooseMove(Board board, Player me)
	{
		var grid = new Grid(board);
		var legal = grid.LegalColumns();
		if (legal.Count == 0)
			return -1;

		var opponent = Match.Opponent(me);
		int win = grid.WinningMove(me);
		if (win >= 0)
			return win;

		if (Difficulty == Difficulty.Easy)
		{
			int block = grid.WinningMove(opponent);
			if (block >= 0 && _random.NextDouble() >= EasyMissBlockChance)
				return block;
			return legal[_random.Next(legal.Count)];
		}

		// Search every move and keep all that tie for best, then pick one at random so the
		// computer doesn't play the same game every time.
		var best = new List<int>();
		int bestScore = int.MinValue;
		foreach (int column in SearchOrder)
		{
			if (!grid.CanDrop(column))
				continue;
			int row = grid.Drop(column, me);
			int score = grid.IsWinAt(column, row)
				? WinScore
				: -Negamax(grid, opponent, SearchDepth - 1, -WinScore * 2, WinScore * 2);
			grid.Undo(column);

			if (score > bestScore)
			{
				bestScore = score;
				best.Clear();
			}
			if (score == bestScore)
				best.Add(column);
		}
		return best[_random.Next(best.Count)];
	}

	/// <summary>Score of the position for <paramref name="toMove"/>; higher is better for them.</summary>
	private static int Negamax(Grid grid, Player toMove, int depth, int alpha, int beta)
	{
		if (grid.IsFull)
			return 0;
		if (depth == 0)
			return grid.Evaluate(toMove);

		var opponent = Match.Opponent(toMove);
		int best = int.MinValue + 1;
		foreach (int column in SearchOrder)
		{
			if (!grid.CanDrop(column))
				continue;
			int row = grid.Drop(column, toMove);
			// Faster wins score higher, so the computer finishes games instead of toying with you.
			int score = grid.IsWinAt(column, row)
				? WinScore + depth
				: -Negamax(grid, opponent, depth - 1, -beta, -alpha);
			grid.Undo(column);

			best = Math.Max(best, score);
			alpha = Math.Max(alpha, score);
			if (alpha >= beta)
				break;
		}
		return best;
	}

	/// <summary>A compact, undoable copy of the board for searching.</summary>
	private sealed class Grid
	{
		private readonly Player[,] _cells = new Player[Board.Columns, Board.Rows];
		private readonly int[] _heights = new int[Board.Columns];
		private int _count;

		public Grid(Board board)
		{
			for (int c = 0; c < Board.Columns; c++)
			{
				for (int r = 0; r < Board.Rows; r++)
				{
					_cells[c, r] = board[c, r];
					if (board[c, r] != Player.None)
					{
						_heights[c] = r + 1;
						_count++;
					}
				}
			}
		}

		public bool IsFull => _count == Board.Columns * Board.Rows;

		public bool CanDrop(int column) => _heights[column] < Board.Rows;

		public List<int> LegalColumns()
		{
			var columns = new List<int>();
			for (int c = 0; c < Board.Columns; c++)
			{
				if (CanDrop(c))
					columns.Add(c);
			}
			return columns;
		}

		public int Drop(int column, Player player)
		{
			int row = _heights[column]++;
			_cells[column, row] = player;
			_count++;
			return row;
		}

		public void Undo(int column)
		{
			int row = --_heights[column];
			_cells[column, row] = Player.None;
			_count--;
		}

		/// <summary>A column where <paramref name="player"/> would win immediately, or -1.</summary>
		public int WinningMove(Player player)
		{
			foreach (int column in SearchOrder)
			{
				if (!CanDrop(column))
					continue;
				int row = Drop(column, player);
				bool wins = IsWinAt(column, row);
				Undo(column);
				if (wins)
					return column;
			}
			return -1;
		}

		public bool IsWinAt(int column, int row)
		{
			var player = _cells[column, row];
			return Run(column, row, 1, 0, player) >= Board.ConnectLength
				|| Run(column, row, 0, 1, player) >= Board.ConnectLength
				|| Run(column, row, 1, 1, player) >= Board.ConnectLength
				|| Run(column, row, 1, -1, player) >= Board.ConnectLength;
		}

		private int Run(int column, int row, int dc, int dr, Player player) =>
			1 + Count(column, row, dc, dr, player) + Count(column, row, -dc, -dr, player);

		private int Count(int column, int row, int dc, int dr, Player player)
		{
			int n = 0;
			int c = column + dc, r = row + dr;
			while (Board.IsInside(c, r) && _cells[c, r] == player)
			{
				n++;
				c += dc;
				r += dr;
			}
			return n;
		}

		/// <summary>
		/// Heuristic for a position with no winner yet: rewards every line of four that is still
		/// open to you (more so the fuller it is), penalises the opponent's, and values the centre.
		/// </summary>
		public int Evaluate(Player me)
		{
			var opponent = Match.Opponent(me);
			int score = 0;
			for (int r = 0; r < Board.Rows; r++)
			{
				if (_cells[3, r] == me)
					score += 3;
				else if (_cells[3, r] == opponent)
					score -= 3;
			}

			for (int c = 0; c < Board.Columns; c++)
			{
				for (int r = 0; r < Board.Rows; r++)
				{
					score += Window(c, r, 1, 0, me, opponent);
					score += Window(c, r, 0, 1, me, opponent);
					score += Window(c, r, 1, 1, me, opponent);
					score += Window(c, r, 1, -1, me, opponent);
				}
			}
			return score;
		}

		private int Window(int column, int row, int dc, int dr, Player me, Player opponent)
		{
			int endC = column + dc * 3, endR = row + dr * 3;
			if (!Board.IsInside(endC, endR))
				return 0;
			int mine = 0, theirs = 0;
			for (int i = 0; i < Board.ConnectLength; i++)
			{
				var cell = _cells[column + dc * i, row + dr * i];
				if (cell == me)
					mine++;
				else if (cell == opponent)
					theirs++;
			}
			if (mine > 0 && theirs > 0)
				return 0;
			return mine switch
			{
				3 => 5,
				2 => 2,
				_ => 0,
			} - theirs switch
			{
				3 => 4,
				2 => 1,
				_ => 0,
			};
		}
	}
}
