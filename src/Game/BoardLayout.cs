using ConnectFour.Core;
using Godot;

namespace ConnectFour.Game;

/// <summary>
/// Physical dimensions of the 3D board, in board-local space. The origin is at the bottom centre
/// of the grid, in the middle of the slot between the front and back panels; +Z faces the camera.
/// Discs stack directly on each other, so the row pitch equals the disc diameter.
/// </summary>
public static class BoardLayout
{
	public const float DiscRadius = 0.46f;
	public const float DiscThickness = 0.24f;

	public const float ColumnPitch = 1.04f;
	public const float RowPitch = DiscRadius * 2f;
	public const float HoleRadius = 0.38f;

	/// <summary>Gap between the panels' inner faces; a little wider than a disc.</summary>
	public const float SlotGap = 0.30f;
	public const float PanelThickness = 0.14f;

	public const float SideMargin = 0.34f;
	public const float TopMargin = 0.28f;
	public const float BottomMargin = 0.34f;

	/// <summary>Height of the sliding bar that holds the discs in; its top is at y = 0.</summary>
	public const float TrayHeight = 0.16f;

	/// <summary>
	/// Clearance between the bottom of the board and the table, so released discs can fall out.
	/// About two rows high: with less, released discs could pile up under the board and stay stuck.
	/// </summary>
	public const float FootHeight = 1.25f + RowPitch;

	/// <summary>Divider between columns, sized so discs have a small clearance on each side.</summary>
	public const float DividerWidth = ColumnPitch - (RowPitch + 0.04f);

	public static float GridWidth => Board.Columns * ColumnPitch;
	public static float GridHeight => Board.Rows * RowPitch;
	public static float GridLeft => -GridWidth / 2f;

	public static float OuterLeft => GridLeft - SideMargin;
	public static float OuterRight => -OuterLeft;
	public static float OuterBottom => -BottomMargin;
	public static float OuterTop => GridHeight + TopMargin;

	public static float PanelInnerZ => SlotGap / 2f;
	public static float PanelOuterZ => SlotGap / 2f + PanelThickness;

	public static float TableY => OuterBottom - FootHeight;

	/// <summary>Where a new disc appears: just above the slot, so it drops in.</summary>
	public static float SpawnY => OuterTop + DiscRadius + 0.1f;

	public static float ColumnX(int column) => GridLeft + (column + 0.5f) * ColumnPitch;

	public static Vector3 CellCenter(Cell cell) => new(ColumnX(cell.Column), (cell.Row + 0.5f) * RowPitch, 0f);

	/// <summary>Column under a board-local X coordinate, or -1 outside the grid.</summary>
	public static int ColumnAt(float x)
	{
		float offset = x - GridLeft;
		if (offset < 0 || offset >= GridWidth)
			return -1;
		return (int)(offset / ColumnPitch);
	}
}
