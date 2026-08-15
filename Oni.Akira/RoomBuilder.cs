using System;
using System.Collections.Generic;
using System.IO;
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
			foreach (Polygon floor in mesh.Floors)
			{
				mesh.Rooms.Add(CreateRoom(floor, 20f));
			}
			ConnectRooms();
			UpdateRoomsHeight();
		}

		private Room CreateRoom(Polygon floor, float height)
		{
			Plane plane = floor.Plane;
			BoundingBox boundingBox = floor.BoundingBox;
			boundingBox.Max.Y += height * plane.Normal.Y;
			Room room = new Room
			{
				FloorPolygon = floor,
				BoundingBox = boundingBox,
				FloorPlane = floor.Plane,
				Height = height * plane.Normal.Y,
				BspTree = BuildBspTree(floor, height * plane.Normal.Y)
			};
			if (floor.Material != null)
			{
				room.Grid = CreateRoomGrid(floor);
			}
			return room;
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
				Vector3 vector2 = vector - normal + Vector3.Up * 2f;
				Vector3 vector3 = vector + normal + Vector3.Up * 2f;
				RoomPair roomPair = PairRooms(vector2, vector3);
				if (roomPair == null)
				{
					Console.WriteLine("BNV Builder: Ghost '{0}' has no adjacencies at {1} and {2}, ignoring", ghost.ObjectName, vector2, vector3);
					continue;
				}
				if (roomPair.Room0.IsStairs || roomPair.Room1.IsStairs)
				{
					Room room = roomPair.Room0;
					if (!room.IsStairs)
					{
						room = roomPair.Room1;
					}
					ghost.Flags &= ~GunkFlags.Ghost;
					if (ghost.Material != null)
					{
						ghost.Material.Flags &= ~GunkFlags.Ghost;
					}
					if (ghost.BoundingBox.Min.Y > room.FloorPolygon.BoundingBox.Max.Y - 1f)
					{
						ghost.Flags |= GunkFlags.StairsDown;
					}
					else
					{
						ghost.Flags |= GunkFlags.StairsUp;
					}
				}
				else
				{
					ghost.Flags |= GunkFlags.Ghost;
				}
				roomPair.Room1.Ajacencies.Add(new RoomAdjacency(roomPair.Room0, ghost));
				roomPair.Room0.Ajacencies.Add(new RoomAdjacency(roomPair.Room1, ghost));
			}
		}

		private RoomPair PairRooms(Vector3 p0, Vector3 p1)
		{
			List<RoomPair> list = new List<RoomPair>();
			List<Room> list2 = FindRooms(p0);
			List<Room> list3 = FindRooms(p1);
			foreach (Room item in list2)
			{
				foreach (Room item2 in list3)
				{
					if (item != item2)
					{
						list.Add(new RoomPair(item, p0, item2, p1));
					}
				}
			}
			list.Sort();
			if (list.Count <= 0)
			{
				return null;
			}
			return list[0];
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
				float num = room.FloorPolygon.Points.Max((Vector3 p) => p.Y);
				float num2 = ((room.Ajacencies.Count != 0) ? room.Ajacencies.Max((RoomAdjacency a) => a.Ghost.Points.Max((Vector3 p) => p.Y)) : (num + 20f));
				BoundingBox boundingBox = room.FloorPolygon.BoundingBox;
				boundingBox.Max.Y = num2;
				room.BoundingBox = boundingBox;
				room.Height = (num2 - num) * room.FloorPlane.Normal.Y;
				room.BspTree = BuildBspTree(room.FloorPolygon, room.Height);
			}
		}
	}
}
