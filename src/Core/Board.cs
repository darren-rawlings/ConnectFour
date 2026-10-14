using System.Collections.Generic;

namespace ConnectFour.Core;

public enum Player : byte
{
	None,
	One,
	Two,
}

public readonly record struct Cell(int Column, int Row);

/// <summary>
/// Pure game-board logic with no Godot dependency, so it can be unit tested
/// and reused if the presentation layer changes. Row 0 is the bottom row.
/// </summary>
public sealed class Board
{
	public const int Columns = 7;
	public const int Rows = 6;
	public const int ConnectLength = 4;

	private static readonly (int dc, int dr)[] Directions = { (1, 0), (0, 1), (1, 1), (1, -1) };

	private readonly Player[,] _cells = new Player[Columns, Rows];

	public int DiscCount { get; private set; }

	public bool IsFull => DiscCount == Columns * Rows;

	public Player this[int column, int row] => _cells[column, row];

	public static bool IsInside(int column, int row) =>
		column >= 0 && column < Columns && row >= 0 && row < Rows;

	/// <summary>Returns the row a disc would land in, or -1 if the column is full or invalid.</summary>
	public int NextOpenRow(int column)
	{
		if (column < 0 || column >= Columns)
			return -1;
		for (int row = 0; row < Rows; row++)
		{
			if (_cells[column, row] == Player.None)
				return row;
		}
		return -1;
	}

	/// <summary>Drops a disc and returns the row it landed in, or -1 if the move is illegal.</summary>
	public int Drop(int column, Player player)
	{
		int row = NextOpenRow(column);
		if (row < 0 || player == Player.None)
			return -1;
		_cells[column, row] = player;
		DiscCount++;
		return row;
	}

	/// <summary>
	/// Returns every cell in a winning line through the given cell, or an empty list.
	/// Only lines through the last move need checking, so this is O(1) per move.
	/// </summary>
	public IReadOnlyList<Cell> FindWinThrough(int column, int row)
	{
		var player = _cells[column, row];
		var result = new List<Cell>();
		if (player == Player.None)
			return result;

		foreach (var (dc, dr) in Directions)
		{
			var line = new List<Cell> { new(column, row) };
			Extend(line, column, row, dc, dr, player);
			Extend(line, column, row, -dc, -dr, player);
			if (line.Count >= ConnectLength)
				result.AddRange(line);
		}
		return result;
	}

	public void Clear()
	{
		System.Array.Clear(_cells);
		DiscCount = 0;
	}

	private void Extend(List<Cell> line, int column, int row, int dc, int dr, Player player)
	{
		int c = column + dc, r = row + dr;
		while (IsInside(c, r) && _cells[c, r] == player)
		{
			line.Add(new Cell(c, r));
			c += dc;
			r += dr;
		}
	}
}
