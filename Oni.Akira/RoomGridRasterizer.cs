using System;
using System.Collections.Generic;
using Oni.Imaging;

namespace Oni.Akira
{
	internal class RoomGridRasterizer
	{
		private enum RoomGridDebugType : byte
		{
			None,
			SlopedQuad,
			StairQuad,
			Wall,
			DangerQuad,
			ImpassableQuad,
			Floor
		}

		private class Edge
		{
			public float maxY;

			public float currentX;

			public float slopeRecip;

			public Edge(Point current, Point next)
			{
				maxY = Math.Max(current.Y, next.Y);
				slopeRecip = (float)(current.X - next.X) / (float)(current.Y - next.Y);
				if ((float)current.Y == maxY)
				{
					currentX = next.X;
				}
				else
				{
					currentX = current.X;
				}
			}

			public void Next()
			{
				currentX += slopeRecip;
			}
		}

		private const int origin = -2;

		private const float tileSize = 4f;

		private const int margin = 3;

		private readonly int xTiles;

		private readonly int zTiles;

		private readonly byte[] data;

		private readonly Vector3 worldOrigin;

		public int XTiles
		{
			get
			{
				return xTiles;
			}
		}

		public int ZTiles
		{
			get
			{
				return zTiles;
			}
		}

		public float TileSize
		{
			get
			{
				return 4f;
			}
		}

		public RoomGridWeight this[int x, int z]
		{
			get
			{
				return (RoomGridWeight)data[x + z * xTiles];
			}
			set
			{
				data[x + z * xTiles] = (byte)value;
			}
		}

		public RoomGridRasterizer(BoundingBox bbox)
		{
			xTiles = (int)((bbox.Max.X - bbox.Min.X) / 4f + 4f + 1f) + 6;
			zTiles = (int)((bbox.Max.Z - bbox.Min.Z) / 4f + 4f + 1f) + 6;
			data = new byte[xTiles * zTiles];
			worldOrigin = bbox.Min;
		}

		public void Clear(RoomGridWeight weight)
		{
			for (int i = 0; i < xTiles * zTiles; i++)
			{
				data[i] = (byte)weight;
			}
		}

		public void DrawFloor(IEnumerable<Vector3> points)
		{
			foreach (Point item in ScanPolygon(points.Select((Vector3 v) => WorldToGrid(v)).ToList()))
			{
				this[item.X, item.Y] = RoomGridWeight.Clear;
			}
		}

		public void DrawDanger(IEnumerable<Vector3> points)
		{
			foreach (Point item in ScanPolygon(points.Select((Vector3 v) => WorldToGrid(v)).ToList()))
			{
				this[item.X, item.Y] = RoomGridWeight.Danger;
			}
		}

		public void DrawStairCorridor(IEnumerable<Vector3> edge0, IEnumerable<Vector3> edge1)
		{
			Vector3[] array = GetLowestPoints(edge0);
			Vector3[] array2 = GetLowestPoints(edge1);
			Vector3[] array3;
			Vector3[] array4;
			if (array[0].Y + array[1].Y > array2[0].Y + array2[1].Y)
			{
				array3 = array;
				array4 = array2;
			}
			else
			{
				array3 = array2;
				array4 = array;
			}
			Vector3 v = array3[1] - array3[0];
			Vector3 v2 = array4[1] - array4[0];
			Vector3 v3 = array4[0] - array3[0];
			Vector3 vector;
			Vector3 vector2;
			Vector3 vector3;
			Vector3 vector4;
			if (v.X * v3.Z - v.Z * v3.X > 0f)
			{
				vector = array3[0];
				vector2 = array3[1];
			}
			else
			{
				vector = array3[1];
				vector2 = array3[0];
			}
			if (v2.X * v3.Z - v2.Z * v3.X < 0f)
			{
				vector3 = array4[0];
				vector4 = array4[1];
			}
			else
			{
				vector3 = array4[1];
				vector4 = array4[0];
			}
			Point point = WorldToGrid(vector);
			Point point2 = WorldToGrid(vector2);
			Point point3 = WorldToGrid(vector3);
			Point point4 = WorldToGrid(vector4);
			int num = point4.X - point.X;
			int num2 = point4.Y - point.Y;
			int num3 = Math.Max(Math.Abs(num), Math.Abs(num2));
			if (num3 > 0)
			{
				num = 4 * num / num3;
				num2 = 4 * num2 / num3;
				Point point5 = new Point(num, num2);
				point -= point5;
				point2 -= point5;
				point3 += point5;
				point4 += point5;
			}
			DrawTriangle(point, point2, point3, RoomGridWeight.Clear, false);
			DrawTriangle(point3, point4, point, RoomGridWeight.Clear, false);
		}

		public void DrawStairOutlet(IEnumerable<Vector3> ghostPoints, Vector3 stairRoomCenter)
		{
			Vector3[] array = GetLowestPoints(ghostPoints);
			Vector3 vector = array[0];
			Vector3 vector2 = array[1];
			Vector3 vector3 = new Vector3(0f - (vector2.Z - vector.Z), 0f, vector2.X - vector.X);
			Vector3 vector4 = stairRoomCenter - (vector + vector2) / 2f;
			if (Vector3.Dot(vector3, vector4) < 0f)
			{
				vector3 = -vector3;
			}
			vector3 = Vector3.Normalize(vector3) * 15f;
			Point point = WorldToGrid(vector);
			Point point2 = WorldToGrid(vector2);
			Point point3 = WorldToGrid(vector + vector3);
			Point point4 = WorldToGrid(vector2 + vector3);
			DrawTriangle(point, point2, point3, RoomGridWeight.Stairs, true);
			DrawTriangle(point2, point4, point3, RoomGridWeight.Stairs, true);
			Point outletStart = new Point((point.X + point2.X) / 2, (point.Y + point2.Y) / 2);
			Point outletEnd = new Point((point3.X + point4.X) / 2, (point3.Y + point4.Y) / 2);
			// A SAT must not be severed by geometry admitted through OniSplit's coarser BNV intersection test.
			if (ScanLine(outletStart, outletEnd).Any((Point outletPoint) => this[outletPoint.X, outletPoint.Y] == RoomGridWeight.Impassable))
			{
				DrawTriangle(point, point2, point3, RoomGridWeight.Stairs, false);
				DrawTriangle(point2, point4, point3, RoomGridWeight.Stairs, false);
			}
			DrawImpassable(vector);
			DrawImpassable(vector2);
		}

		private static Vector3[] GetLowestPoints(IEnumerable<Vector3> points)
		{
			float minY = points.Min((Vector3 point) => point.Y);
			Vector3[] array = points.Where((Vector3 point) => Math.Abs(point.Y - minY) <= 0.1f).ToArray();
			if (array.Length != 2)
			{
				throw new InvalidOperationException("A stair ghost must have exactly two lowest points.");
			}
			return array;
		}

		private void DrawTriangle(Point point1, Point point2, Point point3, RoomGridWeight weight, bool checkPrecedence)
		{
			Point[] array = new Point[3] { point1, point2, point3 };
			Array.Sort(array, (Point left, Point right) => left.Y.CompareTo(right.Y));
			Point top = array[0];
			Point middle = array[1];
			Point bottom = array[2];
			int totalDistance = bottom.Y - top.Y;
			if (totalDistance == 0)
			{
				int minX = Math.Max(Math.Min(top.X, Math.Min(middle.X, bottom.X)), 0);
				int maxX = Math.Min(Math.Max(top.X, Math.Max(middle.X, bottom.X)), xTiles - 1);
				if (top.Y >= 0 && top.Y < zTiles)
				{
					for (int x = minX; x <= maxX; x++)
					{
						if (!checkPrecedence || (int)this[x, top.Y] < (int)weight)
							this[x, top.Y] = weight;
					}
				}
				return;
			}
			int middleDistance = middle.Y - top.Y;
			float interpolatedX = (top.X * (totalDistance - middleDistance) + middleDistance * bottom.X) / (float)totalDistance;
			float leftMiddleX = Math.Min(interpolatedX, middle.X);
			float rightMiddleX = Math.Max(interpolatedX, middle.X);
			DrawTrianglePart(top.Y, middle.Y, top.X, top.X, leftMiddleX, rightMiddleX, weight, checkPrecedence);
			DrawTrianglePart(middle.Y, bottom.Y, leftMiddleX, rightMiddleX, bottom.X, bottom.X, weight, checkPrecedence);
		}

		private void DrawTrianglePart(int y0, int y1, float left0, float right0, float left1, float right1, RoomGridWeight weight, bool checkPrecedence)
		{
			float yDistance = y1 - y0;
			float left = left0;
			float right = right0;
			float deltaLeft = (left1 - left0) / yDistance;
			float deltaRight = (right1 - right0) / yDistance;
			for (int y = y0; y <= y1; y++)
			{
				if (y >= 0 && y < zTiles)
				{
					int minX = Math.Max(FMath.RoundToInt32(left), 0);
					int maxX = Math.Min(FMath.RoundToInt32(right), xTiles - 1);
					for (int x = minX; x <= maxX; x++)
					{
						if (!checkPrecedence || (int)this[x, y] < (int)weight)
							this[x, y] = weight;
					}
				}
				left += deltaLeft;
				right += deltaRight;
			}
		}

		public void DrawWall(IEnumerable<Vector3> points)
		{
			Vector3 vector = points.First();
			Vector3 world = vector;
			foreach (Vector3 point3 in points)
			{
				if (point3.X < vector.X || (point3.X == vector.X && point3.Z < vector.Z))
				{
					vector = point3;
				}
				if (point3.X > world.X || (point3.X == world.X && point3.Z > world.Z))
				{
					world = point3;
				}
			}
			Point point = WorldToGrid(vector);
			Point point2 = WorldToGrid(world);
			DrawLine(point, point2, RoomGridWeight.Impassable);
			DrawLine(point - Point.UnitY, point2 - Point.UnitY, RoomGridWeight.SemiPassable);
			DrawLine(point + Point.UnitY, point2 + Point.UnitY, RoomGridWeight.SemiPassable);
			DrawLine(point + Point.UnitX, point2 + Point.UnitX, RoomGridWeight.SemiPassable);
			DrawLine(point - Point.UnitX, point2 - Point.UnitX, RoomGridWeight.SemiPassable);
		}

		private void DrawLine(Point p0, Point p1, RoomGridWeight weight)
		{
			foreach (Point item in ScanLine(p0, p1))
			{
				if ((int)weight > (int)this[item.X, item.Y])
				{
					this[item.X, item.Y] = weight;
				}
			}
		}

		private void FillPolygon(IEnumerable<Vector3> points, RoomGridWeight weight)
		{
			foreach (Point item in ScanPolygon(points.Select((Vector3 v) => WorldToGrid(v)).ToList()))
			{
				if ((int)weight > (int)this[item.X, item.Y])
				{
					this[item.X, item.Y] = weight;
				}
			}
		}

		public void DrawImpassable(IEnumerable<Vector3> points)
		{
			FillPolygon(points, RoomGridWeight.Impassable);
		}

		public void DrawImpassable(Vector3 position)
		{
			Point point = WorldToGrid(position);
			int x = point.X;
			int y = point.Y;
			DrawTile(x, y, RoomGridWeight.Impassable);
			DrawTile(x - 1, y, RoomGridWeight.SemiPassable);
			DrawTile(x + 1, y, RoomGridWeight.SemiPassable);
			DrawTile(x, y - 1, RoomGridWeight.SemiPassable);
			DrawTile(x, y + 1, RoomGridWeight.SemiPassable);
			DrawTile(x - 1, y - 1, RoomGridWeight.SemiPassable);
			DrawTile(x + 1, y - 1, RoomGridWeight.SemiPassable);
			DrawTile(x + 1, y + 1, RoomGridWeight.SemiPassable);
			DrawTile(x - 1, y + 1, RoomGridWeight.SemiPassable);
		}

		private void DrawTile(int x, int y, RoomGridWeight weight)
		{
			if (0 <= x && x < xTiles && 0 <= y && y < zTiles && (int)weight > (int)this[x, y])
			{
				this[x, y] = weight;
			}
		}

		public void AddBorders()
		{
			AddBorder(RoomGridWeight.Danger, RoomGridWeight.Clear, RoomGridWeight.Border4);
			AddBorder(RoomGridWeight.Border4, RoomGridWeight.Clear, RoomGridWeight.Border3);
			AddBorder(RoomGridWeight.Border3, RoomGridWeight.Clear, RoomGridWeight.Border2);
			AddBorder(RoomGridWeight.Border2, RoomGridWeight.Clear, RoomGridWeight.Border1);
			AddBorder(RoomGridWeight.SemiPassable, RoomGridWeight.Clear, RoomGridWeight.NearWall);
		}

		private void AddBorder(RoomGridWeight aroundOf, RoomGridWeight onlyIf, RoomGridWeight border)
		{
			for (int i = 0; i < zTiles; i++)
			{
				for (int j = 0; j < xTiles; j++)
				{
					if (this[j, i] == aroundOf)
					{
						if (j - 1 >= 0 && this[j - 1, i] == onlyIf)
						{
							this[j - 1, i] = border;
						}
						if (j + 1 < xTiles && this[j + 1, i] == onlyIf)
						{
							this[j + 1, i] = border;
						}
						if (i - 1 >= 0 && this[j, i - 1] == onlyIf)
						{
							this[j, i - 1] = border;
						}
						if (i + 1 < zTiles && this[j, i + 1] == onlyIf)
						{
							this[j, i + 1] = border;
						}
					}
				}
			}
		}

		private Point WorldToGrid(Vector3 world)
		{
			return new Point(FMath.RoundToInt32((world.X - worldOrigin.X) / 4f) - -2 + 3, FMath.RoundToInt32((world.Z - worldOrigin.Z) / 4f) - -2 + 3);
		}

		public RoomGrid GetGrid()
		{
			int num = xTiles - 6;
			int num2 = zTiles - 6;
			byte[] array = new byte[num * num2];
			for (int i = 3; i < zTiles - 3; i++)
			{
				for (int j = 3; j < xTiles - 3; j++)
				{
					array[j - 3 + (i - 3) * num] = data[j + i * xTiles];
				}
			}
			return new RoomGrid(num, num2, array, null);
		}

		private IEnumerable<Point> ScanLine(Point p0, Point p1)
		{
			return ScanLine(p0.X, p0.Y, p1.X, p1.Y);
		}

		private IEnumerable<Point> ScanLine(int x0, int y0, int x1, int y1)
		{
			int dx = ((x0 < x1) ? (x1 - x0) : (x0 - x1));
			int dy = ((y0 < y1) ? (y1 - y0) : (y0 - y1));
			int sx = ((x0 < x1) ? 1 : (-1));
			int sy = ((y0 < y1) ? 1 : (-1));
			int err = dx - dy;
			while (true)
			{
				if (0 <= x0 && x0 < xTiles && 0 <= y0 && y0 < zTiles)
				{
					yield return new Point(x0, y0);
				}
				if (x0 != x1 || y0 != y1)
				{
					int num = 2 * err;
					if (num > -dy)
					{
						err -= dy;
						x0 += sx;
					}
					if (num < dx)
					{
						err += dx;
						y0 += sy;
					}
					continue;
				}
				break;
			}
		}

		private IEnumerable<Vector2> ScanLine(Vector2 p0, Vector2 p1)
		{
			return ScanLine(p0.X, p0.Y, p1.X, p1.Y);
		}

		private IEnumerable<Vector2> ScanLine(float x0, float y0, float x1, float y1)
		{
			float dx = ((x0 < x1) ? (x1 - x0) : (x0 - x1));
			float dy = ((y0 < y1) ? (y1 - y0) : (y0 - y1));
			float sx = ((x0 < x1) ? 1 : (-1));
			float sy = ((y0 < y1) ? 1 : (-1));
			float err = dx - dy;
			while (true)
			{
				if (0f <= x0 && x0 < (float)xTiles && 0f <= y0 && y0 < (float)zTiles)
				{
					yield return new Vector2(x0, y0);
				}
				if (x0 != x1 || y0 != y1)
				{
					float num = 2f * err;
					if (num > 0f - dy)
					{
						err -= dy;
						x0 += sx;
					}
					if (num < dx)
					{
						err += dx;
						y0 += sy;
					}
					continue;
				}
				break;
			}
		}

		private IEnumerable<Point> ScanPolygon(IList<Point> points)
		{
			List<Edge> activeEdgeList = new List<Edge>();
			List<List<Edge>> activeEdgeTable = new List<List<Edge>>();
			int minY = BuildActiveEdgeTable(points, activeEdgeTable);
			for (int y = 0; y < activeEdgeTable.Count; y++)
			{
				for (int i = 0; i < activeEdgeTable[y].Count; i++)
				{
					activeEdgeList.Add(activeEdgeTable[y][i]);
				}
				for (int j = 0; j < activeEdgeList.Count; j++)
				{
					if (activeEdgeList[j].maxY <= (float)(y + minY))
					{
						activeEdgeList.RemoveAt(j);
						j--;
					}
				}
				activeEdgeList.Sort((Edge a, Edge b) => (a.currentX != b.currentX) ? a.currentX.CompareTo(b.currentX) : a.slopeRecip.CompareTo(b.slopeRecip));
				for (int i2 = 0; i2 < activeEdgeList.Count; i2 += 2)
				{
					int yLine = minY + y;
					if (0 <= yLine && yLine < zTiles)
					{
						int num = Math.Max(0, (int)Math.Ceiling(activeEdgeList[i2].currentX));
						int xEnd = Math.Min(xTiles - 1, (int)activeEdgeList[i2 + 1].currentX);
						for (int x = num; x <= xEnd; x++)
						{
							yield return new Point(x, yLine);
						}
					}
				}
				for (int num2 = 0; num2 < activeEdgeList.Count; num2++)
				{
					activeEdgeList[num2].Next();
				}
			}
		}

		private static int BuildActiveEdgeTable(IList<Point> points, List<List<Edge>> activeEdgeTable)
		{
			activeEdgeTable.Clear();
			int num = points.Min((Point p) => p.Y);
			int num2 = points.Max((Point p) => p.Y);
			for (int num3 = num; num3 <= num2; num3++)
			{
				activeEdgeTable.Add(new List<Edge>());
			}
			for (int num4 = 0; num4 < points.Count; num4++)
			{
				Point current = points[num4];
				Point next = points[(num4 + 1) % points.Count];
				if (current.Y != next.Y)
				{
					Edge edge = new Edge(current, next);
					if ((float)current.Y == edge.maxY)
					{
						activeEdgeTable[next.Y - num].Add(edge);
					}
					else
					{
						activeEdgeTable[current.Y - num].Add(edge);
					}
				}
			}
			return num;
		}
	}
}
