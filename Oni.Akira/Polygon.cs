using System;
using System.Collections.Generic;
using Oni.Imaging;

namespace Oni.Akira
{
	internal class Polygon
	{
		private PolygonMesh mesh;

		private GunkFlags flags;

		private int[] pointIndices;

		private int[] texCoordIndices;

		private int[] normalIndices;

		private Color[] colors;

		private Material material;

		private Plane plane;

		private int objectType = -1;

		private int objectId = -1;

		private int scriptId;

		private int agqgIndex = -1;

		private uint agqgFlags;

		private int agqgObjectId = -1;

		private Material originalMaterial;

		private string fileName;

		private string objectName;

		private string sourceNodeId;

		private PolygonEdge[] edges;

		private BoundingBox bbox;

		public PolygonMesh Mesh
		{
			get
			{
				return mesh;
			}
		}

		public GunkFlags Flags
		{
			get
			{
				if (material == null)
				{
					return flags;
				}
				return flags | material.Flags;
			}
			set
			{
				flags = value;
			}
		}

		public bool IsTransparent
		{
			get
			{
				return (Flags & GunkFlags.Transparent) != 0;
			}
		}

		public bool IsStairs
		{
			get
			{
				return (Flags & GunkFlags.Stairs) != 0;
			}
		}

		public Material Material
		{
			get
			{
				return material;
			}
			set
			{
				material = value;
			}
		}

		public int VertexCount
		{
			get
			{
				return pointIndices.Length;
			}
		}

		public int[] PointIndices
		{
			get
			{
				return pointIndices;
			}
		}

		public IEnumerable<Vector3> Points
		{
			get
			{
				int[] array = pointIndices;
				foreach (int index in array)
				{
					yield return mesh.Points[index];
				}
			}
		}

		public int[] TexCoordIndices
		{
			get
			{
				return texCoordIndices;
			}
			set
			{
				texCoordIndices = value;
			}
		}

		public int[] NormalIndices
		{
			get
			{
				return normalIndices;
			}
			set
			{
				normalIndices = value;
			}
		}

		public Color[] Colors
		{
			get
			{
				return colors;
			}
			set
			{
				colors = value;
			}
		}

		public Plane Plane
		{
			get
			{
				return plane;
			}
		}

		public int ObjectType
		{
			get
			{
				return objectType;
			}
			set
			{
				objectType = value;
			}
		}

		public int ObjectId
		{
			get
			{
				return objectId;
			}
			set
			{
				objectId = value;
			}
		}

		public int ScriptId
		{
			get
			{
				return scriptId;
			}
			set
			{
				scriptId = value;
			}
		}

		public int AgqgIndex
		{
			get
			{
				return agqgIndex;
			}
			set
			{
				agqgIndex = value;
			}
		}

		public uint AgqgFlags
		{
			get
			{
				return agqgFlags;
			}
			set
			{
				agqgFlags = value;
			}
		}

		public int AgqgObjectId
		{
			get
			{
				return agqgObjectId;
			}
			set
			{
				agqgObjectId = value;
			}
		}

		public Material OriginalMaterial
		{
			get
			{
				return originalMaterial;
			}
			set
			{
				originalMaterial = value;
			}
		}

		public string FileName
		{
			get
			{
				return fileName;
			}
			set
			{
				fileName = value;
			}
		}

		public string ObjectName
		{
			get
			{
				return objectName;
			}
			set
			{
				objectName = value;
			}
		}

		public string SourceNodeId
		{
			get
			{
				return sourceNodeId;
			}
			set
			{
				sourceNodeId = value;
			}
		}

		public BoundingBox BoundingBox
		{
			get
			{
				return bbox;
			}
		}

		public PolygonEdge[] Edges
		{
			get
			{
				if (edges == null)
				{
					edges = new PolygonEdge[pointIndices.Length];
					for (int i = 0; i < edges.Length; i++)
					{
						edges[i] = new PolygonEdge(this, i);
					}
				}
				return edges;
			}
		}

		public Polygon(PolygonMesh mesh, int[] pointIndices)
		{
			this.mesh = mesh;
			this.pointIndices = pointIndices;
			plane = GetPlane();
			bbox = GetBoundingBox();
			BuildFlags();
		}

		public Polygon(PolygonMesh mesh, int[] pointIndices, GunkFlags flags)
			: this(mesh, pointIndices)
		{
			this.flags |= flags;
		}

		public Polygon(PolygonMesh mesh, int[] pointIndices, Plane plane)
		{
			this.mesh = mesh;
			this.pointIndices = pointIndices;
			this.plane = plane;
			bbox = GetBoundingBox();
			BuildFlags();
		}

		private Plane GetPlane()
		{
			Plane result = new Plane(mesh.Points[pointIndices[0]], mesh.Points[pointIndices[1]], mesh.Points[pointIndices[2]]);
			BoundingBox boundingBox = GetBoundingBox();
			Vector3 vector = boundingBox.Max - boundingBox.Min;
			if (Math.Abs(vector.X) < 0.0001f)
			{
				result = ((!(result.Normal.X < 0f)) ? new Plane(Vector3.Right, 0f - boundingBox.Max.X) : new Plane(Vector3.Left, boundingBox.Min.X));
			}
			else if (Math.Abs(vector.Y) < 0.0001f)
			{
				result = ((!(result.Normal.Y < 0f)) ? new Plane(Vector3.Up, 0f - boundingBox.Max.Y) : new Plane(Vector3.Down, boundingBox.Min.Y));
			}
			else if (Math.Abs(vector.Z) < 0.0001f)
			{
				result = ((!(result.Normal.Z < 0f)) ? new Plane(Vector3.Backward, 0f - boundingBox.Max.Z) : new Plane(Vector3.Forward, boundingBox.Min.Z));
			}
			else
			{
				result.Normal.X = FMath.Round(result.Normal.X, 4);
				result.Normal.Y = FMath.Round(result.Normal.Y, 4);
				result.Normal.Z = FMath.Round(result.Normal.Z, 4);
			}
			return result;
		}

		private BoundingBox GetBoundingBox()
		{
			Vector3 vector = mesh.Points[pointIndices[0]];
			BoundingBox result = new BoundingBox(vector, vector);
			for (int i = 1; i < pointIndices.Length; i++)
			{
				vector = mesh.Points[pointIndices[i]];
				Vector3.Min(ref result.Min, ref vector, out result.Min);
				Vector3.Max(ref result.Max, ref vector, out result.Max);
			}
			return result;
		}

		private void BuildFlags()
		{
			SetProjectionPlane();
			SetHorizontalVertical();
		}

		private void SetHorizontalVertical()
		{
			if (Math.Abs(Vector3.Dot(plane.Normal, Vector3.UnitY)) < 0.3420201f)
			{
				flags |= GunkFlags.Vertical;
			}
			else
			{
				flags |= GunkFlags.Horizontal;
			}
		}

		private void SetProjectionPlane()
		{
			Vector3[] array = new Vector3[pointIndices.Length];
			for (int i = 0; i < pointIndices.Length; i++)
			{
				array[i] = mesh.Points[pointIndices[i]];
			}
			float num = MathHelper.Area(Project(array, PolygonProjectionPlane.XY));
			float num2 = MathHelper.Area(Project(array, PolygonProjectionPlane.YZ));
			float num3 = MathHelper.Area(Project(array, PolygonProjectionPlane.XZ));
			PolygonProjectionPlane polygonProjectionPlane = PolygonProjectionPlane.None;
			polygonProjectionPlane = ((num > num2) ? ((num > num3) ? PolygonProjectionPlane.XY : PolygonProjectionPlane.XZ) : ((!(num2 > num3)) ? PolygonProjectionPlane.XZ : PolygonProjectionPlane.YZ));
			flags |= (GunkFlags)((int)polygonProjectionPlane << 25);
		}

		private static Vector2[] Project(Vector3[] points, PolygonProjectionPlane plane)
		{
			Vector2[] array = new Vector2[points.Length];
			switch (plane)
			{
			case PolygonProjectionPlane.XY:
			{
				for (int j = 0; j < points.Length; j++)
				{
					array[j].X = points[j].X;
					array[j].Y = points[j].Y;
				}
				break;
			}
			case PolygonProjectionPlane.XZ:
			{
				for (int k = 0; k < points.Length; k++)
				{
					array[k].X = points[k].X;
					array[k].Y = points[k].Z;
				}
				break;
			}
			case PolygonProjectionPlane.YZ:
			{
				for (int i = 0; i < points.Length; i++)
				{
					array[i].X = points[i].Z;
					array[i].Y = points[i].Y;
				}
				break;
			}
			}
			return array;
		}
	}
}
