using System;
using System.Collections.Generic;
using System.Globalization;
using Oni.Dae;

namespace Oni.Akira
{
	internal class RoomDaeReader
	{
		private readonly PolygonMesh mesh;

		private readonly List<Vector3> positions;

		private readonly Stack<Matrix> nodeTransformStack;

		private Scene scene;

		private Matrix nodeTransform;

		public static PolygonMesh Read(Scene scene)
		{
			RoomDaeReader roomDaeReader = new RoomDaeReader();
			roomDaeReader.ReadScene(scene);
			return roomDaeReader.mesh;
		}

		private RoomDaeReader()
		{
			mesh = new PolygonMesh(new MaterialLibrary());
			positions = mesh.Points;
			nodeTransformStack = new Stack<Matrix>();
			nodeTransform = Matrix.Identity;
		}

		private void ReadScene(Scene scene)
		{
			this.scene = scene;
			foreach (Node node in scene.Nodes)
			{
				ReadNode(node);
			}
		}

		private void ReadNode(Node node)
		{
			nodeTransformStack.Push(nodeTransform);
			foreach (Transform transform in node.Transforms)
			{
				nodeTransform = transform.ToMatrix() * nodeTransform;
			}
			foreach (GeometryInstance geometryInstance in node.GeometryInstances)
			{
				ReadGeometryInstance(node, geometryInstance);
			}
			foreach (Node node2 in node.Nodes)
			{
				ReadNode(node2);
			}
			nodeTransform = nodeTransformStack.Pop();
		}

		private void ReadGeometryInstance(Node node, GeometryInstance instance)
		{
			Geometry target = instance.Target;
			foreach (MeshPrimitives primitives in target.Primitives)
			{
				if (primitives.PrimitiveType != MeshPrimitiveType.Polygons)
				{
					Console.Error.WriteLine("Unsupported primitive type '{0}' found in geometry '{1}', ignoring.", primitives.PrimitiveType, target.Id);
					continue;
				}
				ReadPolygonPrimitives(node, primitives, instance.Materials.Find((MaterialInstance m) => m.Symbol == primitives.MaterialSymbol));
			}
		}

		private void ReadPolygonPrimitives(Node node, MeshPrimitives primitives, MaterialInstance materialInstance)
		{
			IndexedInput input = primitives.Inputs.FirstOrDefault((IndexedInput i) => i.Semantic == Semantic.Position);
			int[] array = ReadInputIndexed(input, positions, Source.ReadVector3);
			int[] array2 = array;
			foreach (int index in array2)
			{
				positions[index] = Vector3.Transform(positions[index], ref nodeTransform);
			}
			int num2 = 0;
			foreach (int vertexCount in primitives.VertexCounts)
			{
				int polygonStart = num2;
				Polygon polygon = CreatePolygon(array, polygonStart, vertexCount);
				num2 += vertexCount;
				if (polygon == null)
				{
					ReportDegeneratePolygon(node, array, polygonStart, vertexCount);
					continue;
				}
				polygon.FileName = node.FileName;
				polygon.ObjectName = node.Name;
				polygon.SourceNodeId = node.Id;
				if (Math.Abs(polygon.Plane.Normal.Y) < 0.0001f)
				{
					if (polygon.BoundingBox.Height < 1f)
					{
						Console.Error.WriteLine("BNV polygon: discarded, ghost height must be greater than 1, it is {0}", polygon.BoundingBox.Height);
					}
					else if (polygon.PointIndices.Length != 4)
					{
						Console.Error.WriteLine("BNV polygon: discarded, ghost is a {0}-gon", polygon.PointIndices.Length);
					}
					else
					{
						mesh.Ghosts.Add(polygon);
					}
				}
				else if ((polygon.Flags & GunkFlags.Horizontal) != GunkFlags.None)
				{
					mesh.Floors.Add(polygon);
				}
				else
				{
					Console.Error.WriteLine("BNV polygon: discarded, not a ghost and not a floor");
				}
			}
		}

		private void ReportDegeneratePolygon(Node node, int[] positionIndices, int startIndex, int vertexCount)
		{
			string objectName = node.Name ?? node.Id ?? "<unnamed>";
			string nodeId = node.Id ?? "<unknown>";
			string fileName = node.FileName ?? "<unknown>";
			if (vertexCount == 0)
			{
				Console.Error.WriteLine("BNV polygon: discarded degenerate polygon for object '{0}' (node '{1}') in '{2}'; no source vertices", objectName, nodeId, fileName);
				return;
			}

			Vector3 min = positions[positionIndices[startIndex]];
			Vector3 max = min;
			List<string> worldVertices = new List<string>(vertexCount);
			for (int i = startIndex; i < startIndex + vertexCount; i++)
			{
				Vector3 point = positions[positionIndices[i]];
				min.X = Math.Min(min.X, point.X);
				min.Y = Math.Min(min.Y, point.Y);
				min.Z = Math.Min(min.Z, point.Z);
				max.X = Math.Max(max.X, point.X);
				max.Y = Math.Max(max.Y, point.Y);
				max.Z = Math.Max(max.Z, point.Z);
				worldVertices.Add(FormatVector(point));
			}
			Console.Error.WriteLine("BNV polygon: discarded degenerate polygon for object '{0}' (node '{1}') in '{2}'; world center {3}; bounds {4} to {5}; vertices {6}", objectName, nodeId, fileName, FormatVector((min + max) / 2f), FormatVector(min), FormatVector(max), string.Join(", ", worldVertices.ToArray()));
		}

		private static string FormatVector(Vector3 value)
		{
			return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", value.X, value.Y, value.Z);
		}

		private Polygon CreatePolygon(int[] positionIndices, int startIndex, int vertexCount)
		{
			List<int> list = new List<int>(vertexCount);
			for (int i = startIndex; i < startIndex + vertexCount; i++)
			{
				int index = positionIndices[i];
				if (list.Count == 0 || Vector3.DistanceSquared(mesh.Points[list[list.Count - 1]], mesh.Points[index]) >= 1E-06f)
				{
					list.Add(index);
				}
			}
			if (list.Count > 1 && Vector3.DistanceSquared(mesh.Points[list[0]], mesh.Points[list[list.Count - 1]]) < 1E-06f)
			{
				list.RemoveAt(list.Count - 1);
			}
			for (int i = 0; i < list.Count && list.Count >= 3;)
			{
				Vector3 vector = mesh.Points[list[(i + list.Count - 1) % list.Count]];
				Vector3 vector2 = mesh.Points[list[i]];
				Vector3 vector3 = mesh.Points[list[(i + 1) % list.Count]];
				Vector3 v = vector2 - vector;
				Vector3 v2 = vector3 - vector2;
				if (Vector3.Cross(v2, v).LengthSquared() < 1E-06f && Vector3.Dot(v, v2) > 0f)
				{
					list.RemoveAt(i);
					if (i == list.Count)
					{
						i = 0;
					}
					continue;
				}
				i++;
			}
			int[] array = list.ToArray();
			if (CheckDegenerate(mesh.Points, array))
			{
				return null;
			}
			return new Polygon(mesh, array);
		}

		private static bool CheckDegenerate(List<Vector3> positions, int[] indices)
		{
			if (indices.Length < 3)
			{
				return true;
			}
			Vector3 v = positions[indices[0]];
			Vector3 v2 = positions[indices[1]];
			for (int i = 2; i < indices.Length; i++)
			{
				Vector3 v3 = positions[indices[i]];
				Vector3 r;
				Vector3.Substract(ref v, ref v2, out r);
				Vector3 r2;
				Vector3.Substract(ref v3, ref v2, out r2);
				Vector3 r3;
				Vector3.Cross(ref r, ref r2, out r3);
				if (Math.Abs(r3.LengthSquared()) < 0.0001f && Vector3.Dot(ref r, ref r2) > 0f)
				{
					return true;
				}
				v = v2;
				v2 = v3;
			}
			return false;
		}

		private static int[] ReadInputIndexed<T>(IndexedInput input, List<T> list, Func<Source, int, T> elementReader) where T : struct
		{
			int[] array = new int[input.Indices.Count];
			for (int i = 0; i < input.Indices.Count; i++)
			{
				T item = elementReader(input.Source, input.Indices[i]);
				array[i] = list.Count;
				list.Add(item);
			}
			return array;
		}
	}
}
