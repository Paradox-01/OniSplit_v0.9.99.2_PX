using System;
using System.Collections.Generic;

namespace Oni.Akira
{
	internal class Room
	{
		private Polygon floorPolygon;

		private readonly List<Polygon> floorPolygons = new List<Polygon>();

		private readonly List<RoomBspNode> componentBspTrees = new List<RoomBspNode>();

		private RoomBspNode bspTree;

		private BoundingBox boundingBox;

		private RoomGrid grid;

		private Plane floorPlane;

		private float height;

		private readonly List<RoomAdjacency> adjacencies = new List<RoomAdjacency>();

		public BoundingBox BoundingBox
		{
			get
			{
				return boundingBox;
			}
			set
			{
				boundingBox = value;
			}
		}

		public RoomBspNode BspTree
		{
			get
			{
				return bspTree;
			}
			set
			{
				bspTree = value;
			}
		}

		public List<RoomBspNode> ComponentBspTrees
		{
			get
			{
				return componentBspTrees;
			}
		}

		public RoomGrid Grid
		{
			get
			{
				return grid;
			}
			set
			{
				grid = value;
			}
		}

		public Polygon FloorPolygon
		{
			get
			{
				return floorPolygon;
			}
			set
			{
				floorPolygon = value;
				floorPolygons.Clear();
				if (value != null)
				{
					floorPolygons.Add(value);
				}
			}
		}

		public List<Polygon> FloorPolygons
		{
			get
			{
				return floorPolygons;
			}
		}

		public Plane FloorPlane
		{
			get
			{
				return floorPlane;
			}
			set
			{
				floorPlane = value;
			}
		}

		public bool IsStairs
		{
			get
			{
				return floorPlane.Normal.Y < 0.999f;
			}
		}

		public float Height
		{
			get
			{
				return height;
			}
			set
			{
				height = value;
			}
		}

		public List<RoomAdjacency> Ajacencies
		{
			get
			{
				return adjacencies;
			}
		}

		public bool Contains(Vector3 point)
		{
			return Contains(point, 0f);
		}

		public bool Contains(Vector3 point, float tolerance)
		{
			BoundingBox box = boundingBox;
			box.Inflate(new Vector3(tolerance));
			if (!box.Contains(point))
			{
				return false;
			}
			if (componentBspTrees.Count > 0)
			{
				return componentBspTrees.Any((RoomBspNode componentTree) => Contains(componentTree, point, tolerance));
			}
			return Contains(bspTree, point, tolerance);
		}

		private static bool Contains(RoomBspNode bspRoot, Vector3 point, float tolerance)
		{
			bool flag = false;
			for (RoomBspNode roomBspNode = bspRoot; roomBspNode != null; roomBspNode = (flag ? roomBspNode.FrontChild : roomBspNode.BackChild))
			{
				float threshold = ((roomBspNode.FrontChild == null) ? tolerance : 0f);
				flag = roomBspNode.Plane.DotCoordinate(point) > threshold;
			}
			return !flag;
		}

		public bool Intersect(BoundingBox bbox)
		{
			if (!boundingBox.Intersects(bbox))
			{
				return false;
			}
			bool flag = false;
			for (RoomBspNode roomBspNode = bspTree; roomBspNode != null; roomBspNode = (flag ? roomBspNode.FrontChild : roomBspNode.BackChild))
			{
				int num = roomBspNode.Plane.Intersects(bbox);
				if (num == 0)
				{
					return true;
				}
				flag = num > 0;
			}
			return !flag;
		}

		public List<Vector3[]> GetFloorPolygons()
		{
			List<Vector3[]> list = new List<Vector3[]>();
			if (floorPolygons.Count > 0)
			{
				foreach (Polygon floor in floorPolygons)
				{
					list.Add(floor.Points.ToArray());
				}
				return list;
			}
			Vector2 vector = new Vector2(boundingBox.Min.X, boundingBox.Min.Z);
			Vector2 vector2 = new Vector2(boundingBox.Max.X, boundingBox.Max.Z);
			Polygon2 polygon = new Polygon2(new Vector2[4]
			{
				new Vector2(vector.X, vector.Y),
				new Vector2(vector2.X, vector.Y),
				new Vector2(vector2.X, vector2.Y),
				new Vector2(vector.X, vector2.Y)
			});
			foreach (Polygon2 item in new Polygon2Clipper(bspTree).Clip(polygon))
			{
				Vector3[] array = new Vector3[item.Length];
				for (int i = 0; i < array.Length; i++)
				{
					Vector2 vector3 = item[i];
					array[i].X = vector3.X;
					array[i].Y = (0f - floorPlane.D - floorPlane.Normal.Z * vector3.Y - floorPlane.Normal.X * vector3.X) / floorPlane.Normal.Y;
					array[i].Z = vector3.Y;
				}
				Array.Reverse(array);
				list.Add(array);
			}
			return list;
		}
	}
}
