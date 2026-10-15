using System.Collections.Generic;
using ConnectFour.Core;
using Godot;
using static ConnectFour.Game.BoardLayout;

namespace ConnectFour.Game;

/// <summary>
/// The physical board: two holed panels with a slot between them, column dividers, a sliding
/// tray along the bottom that holds the discs in, and feet. This node is what the player spins.
/// </summary>
public partial class BoardRig : Node3D
{
	public const string Group = "board";

	private const float TrayTravel = 0.35f;

	private readonly List<MeshInstance3D> _parts = new();
	private StaticBody3D _tray = null!;
	private CollisionShape3D _trayShape = null!;
	private Vector3 _trayHome;

	public Style Style { get; private set; }

	public void Build(Style style)
	{
		var body = new StaticBody3D();
		body.AddToGroup(Group);
		AddChild(body);

		float height = OuterTop - OuterBottom;
		float midY = (OuterTop + OuterBottom) / 2f;
		float width = OuterRight - OuterLeft;
		float panelZ = (PanelInnerZ + PanelOuterZ) / 2f;

		// Front and back panels are separate meshes so transparent (glass) boards sort correctly.
		AddPart(body, Meshes.Panel(PanelInnerZ, PanelOuterZ), Vector3.Zero);
		AddPart(body, Meshes.Panel(-PanelInnerZ, -PanelOuterZ), Vector3.Zero);
		AddCollider(body, new Vector3(0, midY, panelZ), new Vector3(width, height, PanelThickness));
		AddCollider(body, new Vector3(0, midY, -panelZ), new Vector3(width, height, PanelThickness));

		// Side caps close the slot at each end, above the tray's exit gap.
		float capHeight = OuterTop;
		foreach (float side in new[] { -1f, 1f })
		{
			var center = new Vector3(side * (GridWidth / 2f + SideMargin / 2f), capHeight / 2f, 0);
			AddBox(body, center, new Vector3(SideMargin, capHeight, SlotGap));
		}

		// Dividers between columns, running the full height so released discs stay in their lanes.
		for (int c = 1; c < Board.Columns; c++)
		{
			var center = new Vector3(GridLeft + c * ColumnPitch, midY, 0);
			AddBox(body, center, new Vector3(DividerWidth, height, SlotGap));
		}

		// Feet: brackets under each end, deeper than the board so it stands up.
		foreach (float side in new[] { -1f, 1f })
		{
			var center = new Vector3(side * (GridWidth / 2f + SideMargin / 2f), (TableY + OuterBottom) / 2f, 0);
			AddBox(body, center, new Vector3(SideMargin * 0.9f, FootHeight, 1.7f));
		}

		// The tray is its own body so it can slide out sideways and let every disc drop.
		_tray = new StaticBody3D();
		_tray.AddToGroup(Group);
		AddChild(_tray);
		_trayHome = new Vector3(0, -TrayHeight / 2f, 0);
		_tray.Position = _trayHome;
		var traySize = new Vector3(GridWidth + 0.5f, TrayHeight, SlotGap - 0.02f);
		AddPart(_tray, new BoxMesh { Size = traySize }, Vector3.Zero);
		_trayShape = AddCollider(_tray, Vector3.Zero, traySize);

		ApplyStyle(style);
	}

	public void ApplyStyle(Style style)
	{
		Style = style;
		var material = MaterialFactory.For(Styles.BoardLook(style), isDisc: false);
		foreach (var part in _parts)
			part.MaterialOverride = material;
	}

	/// <summary>Slides the tray out to the right so every disc falls through the bottom.</summary>
	public void OpenTray()
	{
		_trayShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
		var tween = CreateTween();
		tween.TweenProperty(_tray, "position", _trayHome + Vector3.Right * (GridWidth + 0.6f), TrayTravel)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
	}

	public void CloseTray()
	{
		var tween = CreateTween();
		tween.TweenProperty(_tray, "position", _trayHome, TrayTravel)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.Finished += () => _trayShape.Disabled = false;
	}

	public bool IsTray(Node node) => node == _tray;

	/// <summary>World position of the tray's sticking-out end, for the release sound.</summary>
	public Vector3 TrayEnd => ToGlobal(_trayHome + Vector3.Right * (GridWidth / 2f));


	private void AddBox(StaticBody3D body, Vector3 center, Vector3 size)
	{
		AddPart(body, new BoxMesh { Size = size }, center);
		AddCollider(body, center, size);
	}

	private void AddPart(Node3D parent, Mesh mesh, Vector3 position)
	{
		var part = new MeshInstance3D { Mesh = mesh, Position = position };
		parent.AddChild(part);
		_parts.Add(part);
	}

	private static CollisionShape3D AddCollider(StaticBody3D body, Vector3 center, Vector3 size)
	{
		var shape = new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = center };
		body.AddChild(shape);
		return shape;
	}
}
