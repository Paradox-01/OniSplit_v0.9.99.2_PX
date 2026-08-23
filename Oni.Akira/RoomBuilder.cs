using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oni.Imaging;

namespace Oni.Akira
{
	internal class RoomBuilder
	{
		private class RoomPair : IComparable<RoomPair>
		{
			public readonly Room Room0;

			public readonly Room Room1;

			public readonly float HeightDelta;

			public readonly float VolumeDelta;

			public RoomPair(Room r0, Vector3 p0, Room r1, Vector3 p1)
			{
				Room0 = r0;
				Room1 = r1;
				HeightDelta = r0.FloorPlane.DotCoordinate(p0) - r1.FloorPlane.DotCoordinate(p1);
				VolumeDelta = r0.BoundingBox.Volume() - r1.BoundingBox.Volume();
			}

			int IComparable<RoomPair>.CompareTo(RoomPair other)
			{
				if (Math.Abs(HeightDelta - other.HeightDelta) < 1E-05f)
				{
					return VolumeDelta.CompareTo(other.VolumeDelta);
				}
				if (HeightDelta < other.HeightDelta)
				{
					return -1;
				}
				return 1;
			}
		}

		private const float roomHeight = 20f;

		private readonly PolygonMesh mesh;

		private OctreeNode octtree;

		public static void BuildRooms(PolygonMesh mesh)
		{
			RoomBuilder roomBuilder = new RoomBuilder(mesh);
			roomBuilder.BuildRooms();
		}

		private RoomBuilder(PolygonMesh mesh)
		{
			this.mesh = mesh;
		}

		private void BuildRooms()
		{
			foreach (IGrouping<string, Polygon> floorGroup in mesh.Floors.GroupBy((Polygon floor) => floor.FileName + "\0" + (floor.SourceNodeId ?? floor.ObjectName)))
			{
				mesh.Rooms.Add(CreateRoom(floorGroup.ToList(), 20f));
			}
			ConnectRooms();
			UpdateRoomsHeight();
		}

		private Room CreateRoom(List<Polygon> floors, float height)
		{
			Polygon floor = floors[0];
			Plane plane = floor.Plane;
			BoundingBox boundingBox = BoundingBox.CreateFromPoints(floors.SelectMany((Polygon polygon) => polygon.Points));
			boundingBox.Max.Y += height * plane.Normal.Y;
			Room room = new Room
			{
				FloorPolygon = floor,
				BoundingBox = boundingBox,
				FloorPlane = floor.Plane,
				Height = height * plane.Normal.Y,
				BspTree = BuildBspTree(floors, height * plane.Normal.Y)
			};
			room.FloorPolygons.AddRange(floors.Skip(1));
			foreach (Polygon componentFloor in floors)
			{
				room.ComponentBspTrees.Add(BuildBspTree(componentFloor, height * plane.Normal.Y));
			}
			if (floor.Material != null)
			{
				room.Grid = CreateRoomGrid(floor);
			}
			return room;
		}

		private static RoomBspNode BuildBspTree(List<Polygon> floors, float height)
		{
			RoomBspNode roomBspNode = null;
			foreach (Polygon floor in floors)
			{
				RoomBspNode roomBspNode2 = BuildBspTree(floor, height);
				roomBspNode = ((roomBspNode == null) ? roomBspNode2 : UnionBspTrees(roomBspNode, roomBspNode2));
			}
			return roomBspNode;
		}

		private static RoomBspNode UnionBspTrees(RoomBspNode tree, RoomBspNode fallback)
		{
			return UnionBspTrees(tree, fallback, new Dictionary<RoomBspNode, RoomBspNode>());
		}

		private static RoomBspNode UnionBspTrees(RoomBspNode tree, RoomBspNode fallback, Dictionary<RoomBspNode, RoomBspNode> nodeMap)
		{
			RoomBspNode mappedNode;
			if (nodeMap.TryGetValue(tree, out mappedNode))
			{
				return mappedNode;
			}
			RoomBspNode backChild = ((tree.BackChild == null) ? null : UnionBspTrees(tree.BackChild, fallback, nodeMap));
			RoomBspNode frontChild = ((tree.FrontChild == null) ? fallback : UnionBspTrees(tree.FrontChild, fallback, nodeMap));
			mappedNode = new RoomBspNode(tree.Plane, backChild, frontChild);
			nodeMap.Add(tree, mappedNode);
			return mappedNode;
		}

		private static RoomBspNode BuildBspTree(Polygon floor, float height)
		{
			Vector3[] array = floor.Points.ToArray();
			Plane plane = floor.Plane;
			Plane plane2 = new Plane(-plane.Normal, 0f - plane.D);
			RoomBspNode backChild = new RoomBspNode(plane2, null, null);
			Plane plane3 = new Plane(plane.Normal, plane.D - height);
			backChild = new RoomBspNode(plane3, backChild, null);
			for (int i = 0; i < array.Length; i++)
			{
				Vector3 point = array[i];
				Vector3 vector = array[(i + 1) % array.Length];
				Vector3 point2 = vector + Vector3.Up;
				backChild = new RoomBspNode(new Plane(point, vector, point2), backChild, null);
			}
			return backChild;
		}

		private static RoomGrid CreateRoomGrid(Polygon floor)
		{
			if (!File.Exists(floor.Material.ImageFilePath))
			{
				return null;
			}
			TgaMeshUsage.Register(floor.Material.ImageFilePath, floor.ObjectName);
			Surface image = TgaReader.Read(floor.Material.ImageFilePath);
			return RoomGrid.FromImage(image);
		}

		private void ConnectRooms()
		{
			octtree = OctreeBuilder.BuildRoomsOctree(mesh);
			foreach (Polygon ghost in mesh.Ghosts)
			{
				float minY = ghost.Points.Select((Vector3 p) => p.Y).Min();
				Vector3[] array = ghost.Points.Where((Vector3 p) => Math.Abs(p.Y - minY) <= 0.1f).ToArray();
				if (array.Length != 2)
				{
					Console.Error.WriteLine("BNV Builder: Bad ghost, it must have 2 lowest points, it has {0}, ignoring", array.Length);
					continue;
				}
				Vector3 vector = (array[0] + array[1]) / 2f;
				Vector3 normal = ghost.Plane.Normal;
				Vector3 vector2 = vector + Vector3.Up * 2f;
				Vector3 vector3 = vector2 - normal;
				Vector3 vector4 = vector2 + normal;
				RoomPair roomPair = PairRooms(vector2) ?? PairRooms(vector3, vector4);
				if (roomPair == null)
				{
					Console.WriteLine("BNV Builder: Ghost '{0}' has no adjacencies at {1} and {2}, ignoring", ghost.ObjectName, vector3, vector4);
					continue;
				}
				if (roomPair.Room0.IsStairs || roomPair.Room1.IsStairs)
				{
					ghost.Flags &= ~(GunkFlags.Ghost | GunkFlags.StairsUp | GunkFlags.StairsDown);
					if (ghost.Material != null)
					{
						ghost.Material.Flags &= ~GunkFlags.Ghost;
					}
				}
				else
				{
					ghost.Flags |= GunkFlags.Ghost;
				}
				roomPair.Room1.Ajacencies.Add(new RoomAdjacency(roomPair.Room0, ghost));
				roomPair.Room0.Ajacencies.Add(new RoomAdjacency(roomPair.Room1, ghost));
			}
			ClassifyStairGhosts();
		}

		private void ClassifyStairGhosts()
		{
			foreach (Room room in mesh.Rooms.Where((Room room) => room.IsStairs))
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
				if (list.Count > 0)
				{
					list[0].Flags |= GunkFlags.StairsUp;
				}
				if (list.Count > 1)
				{
					list[list.Count - 1].Flags |= GunkFlags.StairsDown;
				}
			}
		}

		private RoomPair PairRooms(Vector3 point)
		{
			List<Room> rooms = FindRooms(point);
			return PairRooms(rooms, point, rooms, point);
		}

		private RoomPair PairRooms(Vector3 p0, Vector3 p1)
		{
			return PairRooms(FindRooms(p0), p0, FindRooms(p1), p1);
		}

		private static RoomPair PairRooms(List<Room> rooms0, Vector3 p0, List<Room> rooms1, Vector3 p1)
		{
			List<RoomPair> pairs = new List<RoomPair>();
			foreach (Room room0 in rooms0)
			{
				foreach (Room room1 in rooms1)
				{
					if (room0 != room1)
					{
						pairs.Add(new RoomPair(room0, p0, room1, p1));
					}
				}
			}
			pairs.Sort();
			if (pairs.Count <= 0)
			{
				return null;
			}
			return pairs[0];
		}

		private List<Room> FindRooms(Vector3 point)
		{
			List<Room> list = new List<Room>();
			OctreeNode octreeNode = octtree.FindLeaf(point);
			if (octreeNode != null)
			{
				foreach (Room room in octreeNode.Rooms)
				{
					if (room.Contains(point))
					{
						list.Add(room);
					}
				}
			}
			return list;
		}

		private void UpdateRoomsHeight()
		{
			foreach (Room room in mesh.Rooms)
			{
				IEnumerable<Vector3> floorPoints = room.FloorPolygons.SelectMany((Polygon floor) => floor.Points);
				float num = floorPoints.Max((Vector3 p) => p.Y);
				float num2 = ((room.Ajacencies.Count != 0) ? room.Ajacencies.Max((RoomAdjacency a) => a.Ghost.Points.Max((Vector3 p) => p.Y)) : (num + 20f));
				BoundingBox boundingBox = BoundingBox.CreateFromPoints(floorPoints);
				boundingBox.Max.Y = num2;
				room.BoundingBox = boundingBox;
				room.Height = (num2 - num) * room.FloorPlane.Normal.Y;
				room.BspTree = BuildBspTree(room.FloorPolygons, room.Height);
				room.ComponentBspTrees.Clear();
				foreach (Polygon componentFloor in room.FloorPolygons)
				{
					room.ComponentBspTrees.Add(BuildBspTree(componentFloor, room.Height));
				}
			}
		}
	}
}
