using System.Collections.Generic;
using ConnectFour.Core;
using Godot;
using static ConnectFour.Game.BoardLayout;

namespace ConnectFour.Game;

/// <summary>Procedural meshes, built once and shared.</summary>
public static class Meshes
{
	private const int HoleSegmentsPerSide = 8;
	private const int DiscSegments = 40;

	private static ArrayMesh? _disc;

	/// <summary>A checker lathed around its local Y axis: recessed face, raised rim, bevelled edge.</summary>
	public static ArrayMesh Disc => _disc ??= BuildDisc();

	private static ArrayMesh BuildDisc()
	{
		const float r = DiscRadius, h = DiscThickness / 2f, bevel = 0.05f, rim = DiscRadius * 0.68f, dip = 0.03f;
		// (radius, height) from the top centre, out over the rim, down the edge and back underneath.
		var profile = new Vector2[]
		{
			new(0, h - dip), new(rim - 0.03f, h - dip), new(rim, h), new(r - bevel, h), new(r, h - bevel),
			new(r, -h + bevel), new(r - bevel, -h), new(rim, -h), new(rim - 0.03f, -h + dip), new(0, -h + dip),
		};

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		for (int i = 0; i < profile.Length - 1; i++)
		{
			var p0 = profile[i];
			var p1 = profile[i + 1];
			var edge = p1 - p0;
			var n = new Vector2(-edge.Y, edge.X).Normalized();
			for (int s = 0; s < DiscSegments; s++)
			{
				float a0 = Mathf.Tau * s / DiscSegments, a1 = Mathf.Tau * (s + 1) / DiscSegments;
				Vector3 P(Vector2 p, float a) => new(p.X * Mathf.Cos(a), p.Y, p.X * Mathf.Sin(a));
				Vector3 N(float a) => new Vector3(n.X * Mathf.Cos(a), n.Y, n.X * Mathf.Sin(a)).Normalized();
				var face = N((a0 + a1) / 2f);
				Tri(st, P(p0, a0), P(p1, a0), P(p1, a1), N(a0), N(a0), N(a1), face);
				Tri(st, P(p0, a0), P(p1, a1), P(p0, a1), N(a0), N(a1), N(a1), face);
			}
		}
		return st.Commit();
	}

	/// <summary>
	/// One board panel: a slab with a round hole per cell, between zInner (facing the slot) and
	/// zOuter (facing out). Each cell is a ring joining the cell's rectangle to its hole's circle.
	/// </summary>
	public static ArrayMesh Panel(float zInner, float zOuter)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		float outward = Mathf.Sign(zOuter - zInner);
		var (rect, circle) = CellRings();
		float gl = GridLeft, gr = -GridLeft, gb = 0f, gt = GridHeight;

		foreach (var (z, nz) in new[] { (zOuter, outward), (zInner, -outward) })
		{
			var n = new Vector3(0, 0, nz);
			FaceRect(st, OuterLeft, gl, OuterBottom, OuterTop, z, n);
			FaceRect(st, gr, OuterRight, OuterBottom, OuterTop, z, n);
			FaceRect(st, gl, gr, OuterBottom, gb, z, n);
			FaceRect(st, gl, gr, gt, OuterTop, z, n);

			for (int c = 0; c < Board.Columns; c++)
			{
				for (int row = 0; row < Board.Rows; row++)
				{
					var center = CellCenter(new Cell(c, row));
					var c2 = new Vector2(center.X, center.Y);
					for (int i = 0; i < rect.Length; i++)
					{
						int j = (i + 1) % rect.Length;
						Quad(st, V(c2 + rect[i], z), V(c2 + rect[j], z), V(c2 + circle[j], z), V(c2 + circle[i], z), n);
					}
				}
			}
		}

		// Inside walls of each hole, facing its centre.
		for (int c = 0; c < Board.Columns; c++)
		{
			for (int row = 0; row < Board.Rows; row++)
			{
				var center = CellCenter(new Cell(c, row));
				var c2 = new Vector2(center.X, center.Y);
				for (int i = 0; i < circle.Length; i++)
				{
					int j = (i + 1) % circle.Length;
					var ni = -new Vector3(circle[i].X, circle[i].Y, 0).Normalized();
					var nj = -new Vector3(circle[j].X, circle[j].Y, 0).Normalized();
					var face = (ni + nj).Normalized();
					Vector3 a = V(c2 + circle[i], zInner), b = V(c2 + circle[j], zInner);
					Vector3 d = V(c2 + circle[i], zOuter), e = V(c2 + circle[j], zOuter);
					Tri(st, a, b, e, ni, nj, nj, face);
					Tri(st, a, e, d, ni, nj, ni, face);
				}
			}
		}

		// Outer edges of the slab.
		Quad(st, new(OuterLeft, OuterBottom, zInner), new(OuterLeft, OuterTop, zInner),
			new(OuterLeft, OuterTop, zOuter), new(OuterLeft, OuterBottom, zOuter), Vector3.Left);
		Quad(st, new(OuterRight, OuterBottom, zInner), new(OuterRight, OuterTop, zInner),
			new(OuterRight, OuterTop, zOuter), new(OuterRight, OuterBottom, zOuter), Vector3.Right);
		Quad(st, new(OuterLeft, OuterTop, zInner), new(OuterRight, OuterTop, zInner),
			new(OuterRight, OuterTop, zOuter), new(OuterLeft, OuterTop, zOuter), Vector3.Up);
		Quad(st, new(OuterLeft, OuterBottom, zInner), new(OuterRight, OuterBottom, zInner),
			new(OuterRight, OuterBottom, zOuter), new(OuterLeft, OuterBottom, zOuter), Vector3.Down);

		return st.Commit();
	}

	/// <summary>
	/// Matching point rings around one cell: its rectangle (corners included, so neighbouring cells
	/// tile without gaps) and the hole circle, each point on the circle aimed at its rectangle point.
	/// </summary>
	private static (Vector2[] Rect, Vector2[] Circle) CellRings()
	{
		float hw = ColumnPitch / 2f, hh = RowPitch / 2f;
		var corners = new Vector2[] { new(hw, -hh), new(hw, hh), new(-hw, hh), new(-hw, -hh) };
		var rect = new List<Vector2>();
		for (int side = 0; side < 4; side++)
		{
			for (int k = 0; k < HoleSegmentsPerSide; k++)
				rect.Add(corners[side].Lerp(corners[(side + 1) % 4], (float)k / HoleSegmentsPerSide));
		}
		var circle = rect.ConvertAll(p => p.Normalized() * HoleRadius);
		return (rect.ToArray(), circle.ToArray());
	}

	private static Vector3 V(Vector2 p, float z) => new(p.X, p.Y, z);

	private static void FaceRect(SurfaceTool st, float x0, float x1, float y0, float y1, float z, Vector3 n) =>
		Quad(st, new(x0, y0, z), new(x1, y0, z), new(x1, y1, z), new(x0, y1, z), n);

	private static void Quad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
	{
		Tri(st, a, b, c, n, n, n, n);
		Tri(st, a, c, d, n, n, n, n);
	}

	/// <summary>
	/// Adds a triangle wound so it faces <paramref name="facing"/>. Godot treats clockwise
	/// triangles as front faces, so the order is flipped whenever the input is counter-clockwise.
	/// </summary>
	private static void Tri(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector3 facing)
	{
		if ((b - a).Cross(c - a).Dot(facing) > 0)
		{
			(b, c) = (c, b);
			(nb, nc) = (nc, nb);
		}
		foreach (var (v, n) in new[] { (a, na), (b, nb), (c, nc) })
		{
			st.SetNormal(n);
			st.SetUV(new Vector2(v.X, v.Y));
			st.AddVertex(v);
		}
	}
}
