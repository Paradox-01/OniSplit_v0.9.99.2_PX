using System;
using System.Collections.Generic;
using System.Globalization;
using Oni.Collections;
using Oni.Dae;

namespace Oni.Akira
{
	internal class RoomGridBuilder
	{
		private readonly Scene roomsScene;

		private readonly PolygonMesh geometryMesh;

		private PolygonMesh roomsMesh;

		private OctreeNode geometryOcttree;

		private OctreeNode dangerOcttree;

		public PolygonMesh Mesh
		{
			get
			{
				return roomsMesh;
			}
		}

		public RoomGridBuilder(Scene roomsScene, PolygonMesh geometryMesh)
		{
			this.roomsScene = roomsScene;
			this.geometryMesh = geometryMesh;
		}

		public void Build()
		{
			roomsMesh = RoomDaeReader.Read(roomsScene);
			RoomBuilder.BuildRooms(roomsMesh);
			Console.Error.WriteLine("Read {0} rooms", roomsMesh.Rooms.Count);
			geometryOcttree = OctreeBuilder.Build(geometryMesh, GunkFlags.NoCollision | GunkFlags.NoCharacterCollision);
			dangerOcttree = OctreeBuilder.Build(geometryMesh, (Polygon p) => (p.Flags & GunkFlags.Danger) != 0);
			ProcessStairsCollision();
			Parallel.ForEach(roomsMesh.Rooms, delegate(Room room)
			{
				BuildGrid(room);
			});
		}

		private void ProcessStairsCollision()
		{
			Vector3 verticalTolerance1 = new Vector3(0f, 0.1f, 0f);
			Vector3 verticalTolerance2 = new Vector3(0f, 7.5f, 0f);
			foreach (Polygon item in geometryMesh.Polygons.Where((Polygon p) => p.IsStairs && p.VertexCount == 4))
			{
				Vector3[] array = item.Points.Select((Vector3 v) => v + verticalTolerance1).ToArray();
				Vector3[] array2 = item.Points.Select((Vector3 v) => v + verticalTolerance2).ToArray();
				BoundingBox box = BoundingBox.CreateFromPoints(array.Concatenate(array2));
				Plane plane = new Plane(array[0], array[1], array[2]);
				Plane plane2 = new Plane(array2[0], array2[1], array2[2]);
				foreach (OctreeNode item2 in geometryOcttree.FindLeafs(box))
				{
					foreach (Polygon polygon in item2.Polygons)
					{
						if ((polygon.Flags & (GunkFlags.NoCollision | GunkFlags.NoCharacterCollision)) != GunkFlags.None || !polygon.BoundingBox.Intersects(box))
						{
							continue;
						}
						List<Vector3> points = polygon.Points.ToList();
						points = PolygonUtils.ClipToPlane(points, plane);
						if (points != null)
						{
							points = PolygonUtils.ClipToPlane(points, plane2);
							if (points == null)
							{
								polygon.Flags |= GunkFlags.NoCharacterCollision;
							}
						}
					}
				}
			}
		}

		private static bool PolygonIntersectsRoom(Room room, Polygon polygon)
		{
			foreach (Vector3 point in polygon.Points)
			{
				if (room.Contains(point, 10f))
				{
					return true;
				}
			}
			return room.Intersect(polygon.BoundingBox);
		}

		private void BuildGrid(Room room)
		{
			Polygon floorPolygon = room.FloorPolygon;
			BoundingBox boundingBox = room.BoundingBox;
			RoomGridRasterizer roomGridRasterizer = new RoomGridRasterizer(boundingBox);
			roomGridRasterizer.Clear(RoomGridWeight.Danger);
			boundingBox.Inflate(2f * new Vector3(roomGridRasterizer.TileSize, 0f, roomGridRasterizer.TileSize));
			BoundingBox box = boundingBox;
			box.Min.X--;
			box.Min.Y = boundingBox.Min.Y - 6f;
			box.Min.Z--;
			box.Max.X++;
			box.Max.Y = boundingBox.Max.Y - 6f;
			box.Max.Z++;
			Set<Polygon> set = new Set<Polygon>();
			Set<Polygon> set2 = new Set<Polygon>();
			foreach (OctreeNode item in geometryOcttree.FindLeafs(box))
			{
				set.UnionWith(item.Polygons);
			}
			foreach (OctreeNode item2 in dangerOcttree.FindLeafs(box))
			{
				set2.UnionWith(item2.Polygons);
			}
			if (room.IsStairs)
			{
				List<Polygon> list = new List<Polygon>();
				foreach (RoomAdjacency ajacency in room.Ajacencies)
				{
					if (!list.Contains(ajacency.Ghost))
					{
						list.Add(ajacency.Ghost);
					}
				}
				list.Sort((Polygon x, Polygon y) => x.BoundingBox.Min.Y.CompareTo(y.BoundingBox.Min.Y));
				if (list.Count >= 2)
				{
					roomGridRasterizer.DrawStairCorridor(list[0].Points, list[list.Count - 1].Points);
				}
				else
				{
					List<string> ghostDescriptions = new List<string>(list.Count);
					foreach (Polygon ghost in list)
					{
						ghostDescriptions.Add(string.Format("'{0}' at {1}", ghost.ObjectName ?? "<unnamed>", FormatVector((ghost.BoundingBox.Min + ghost.BoundingBox.Max) / 2f)));
					}
					Console.Error.WriteLine("BNV Builder: Stair room '{0}' (node '{1}') in '{2}' has {3} stair ghosts [{4}]; world bounds {5} to {6}; using its floor polygons", floorPolygon.ObjectName ?? "<unnamed>", floorPolygon.SourceNodeId ?? "<unknown>", floorPolygon.FileName ?? "<unknown>", list.Count, string.Join(", ", ghostDescriptions.ToArray()), FormatVector(room.BoundingBox.Min), FormatVector(room.BoundingBox.Max));
					foreach (Polygon floor in room.FloorPolygons)
					{
						roomGridRasterizer.DrawFloor(floor.Points);
					}
				}
			}
			else
			{
				foreach (Polygon item3 in set)
				{
					if (item3.Plane.Normal.Y > 0.5f && PolygonIntersectsRoom(room, item3))
					{
						roomGridRasterizer.DrawFloor(item3.Points);
					}
				}
				float y = floorPolygon.BoundingBox.Max.Y;
				Plane plane = new Plane(floorPolygon.Plane.Normal, floorPolygon.Plane.D - 4f);
				Plane plane2 = new Plane(-floorPolygon.Plane.Normal, 0f - (floorPolygon.Plane.D - 20f));
				foreach (Polygon item4 in set)
				{
					if (!PolygonIntersectsRoom(room, item4))
					{
						continue;
					}
					if ((item4.Flags & (GunkFlags.Stairs | GunkFlags.NoCharacterCollision | GunkFlags.Impassable)) == 0)
					{
						BoundingBox boundingBox2 = item4.BoundingBox;
						if (Math.Abs(item4.Plane.Normal.Y) < 1E-05f && boundingBox2.Height <= 4f && Math.Abs(boundingBox2.Max.Y - y) <= 4f)
						{
							item4.Flags |= GunkFlags.NoCharacterCollision;
							continue;
						}
					}
					if ((item4.Flags & (GunkFlags.Stairs | GunkFlags.NoCollision | GunkFlags.NoCharacterCollision | GunkFlags.GridIgnore)) != GunkFlags.None)
					{
						continue;
					}
					List<Vector3> points = item4.Points.ToList();
					points = PolygonUtils.ClipToPlane(points, plane);
					if (points == null)
					{
						continue;
					}
					points = PolygonUtils.ClipToPlane(points, plane2);
					if (points != null)
					{
						if (Math.Abs(item4.Plane.Normal.Y) <= 0.1f)
						{
							roomGridRasterizer.DrawWall(points);
						}
						else if (Math.Abs(item4.Plane.Normal.Y) <= 0.5f || item4.BoundingBox.Min.Y - y >= 2f)
						{
							roomGridRasterizer.DrawImpassable(points);
						}
					}
				}
				foreach (Polygon item5 in set2)
				{
					if (PolygonIntersectsRoom(room, item5))
					{
						roomGridRasterizer.DrawDanger(item5.Points);
					}
				}
				foreach (RoomAdjacency adjacency in room.Ajacencies)
				{
					if (adjacency.AdjacentRoom.IsStairs && (adjacency.Ghost.Flags & (GunkFlags.StairsUp | GunkFlags.StairsDown)) != GunkFlags.None)
					{
						BoundingBox stairBoundingBox = adjacency.AdjacentRoom.BoundingBox;
						roomGridRasterizer.DrawStairOutlet(adjacency.Ghost.Points, (stairBoundingBox.Min + stairBoundingBox.Max) / 2f);
					}
				}
			}
			roomGridRasterizer.AddBorders();
			room.Grid = roomGridRasterizer.GetGrid();
			long tileCount = (long)room.Grid.XTiles * room.Grid.ZTiles;
			if (tileCount > 65536)
			{
				Console.Error.WriteLine("Warning: pathfinding grid for BNV '{0}' (node '{1}') in '{2}' is too large: {3} x {4} = {5} tiles; world bounds {6} to {7}", floorPolygon.ObjectName ?? "<unnamed>", floorPolygon.SourceNodeId ?? "<unknown>", floorPolygon.FileName ?? "<unknown>", room.Grid.XTiles, room.Grid.ZTiles, tileCount, FormatVector(room.BoundingBox.Min), FormatVector(room.BoundingBox.Max));
			}
		}

		private static string FormatVector(Vector3 value)
		{
			return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", value.X, value.Y, value.Z);
		}
	}
}
