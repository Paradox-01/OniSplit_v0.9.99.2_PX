using System;
using System.Collections.Generic;
using Oni.Dae;

namespace Oni.Motoko
{
	internal static class GeometryDaeReader
	{
		private struct Vertex : IEquatable<Vertex>
		{
			public readonly int PositionIndex;

			public readonly int TexcoordIndex;

			public readonly int NormalIndex;

			public Vertex(int pointIndex, int uvIndex, int normalIndex)
			{
				PositionIndex = pointIndex;
				TexcoordIndex = uvIndex;
				NormalIndex = normalIndex;
			}

			public static bool operator ==(Vertex v1, Vertex v2)
			{
				return v1.Equals(v2);
			}

			public static bool operator !=(Vertex v1, Vertex v2)
			{
				return !v1.Equals(v2);
			}

			public bool Equals(Vertex v)
			{
				if (PositionIndex == v.PositionIndex && TexcoordIndex == v.TexcoordIndex)
				{
					return NormalIndex == v.NormalIndex;
				}
				return false;
			}

			public override bool Equals(object obj)
			{
				if (obj is Vertex)
				{
					return Equals((Vertex)obj);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return PositionIndex ^ TexcoordIndex ^ NormalIndex;
			}
		}

		public static Geometry Read(Oni.Dae.Geometry daeGeometry)
		{
			return Read(daeGeometry, false, false, 0f);
		}

		public static IEnumerable<Geometry> Read(Node node, TextureImporter3 textureImporter)
		{
			FaceConverter.Triangulate(node);
			foreach (GeometryInstance geometryInstance in node.GeometryInstances)
			{
				Oni.Dae.Geometry target = geometryInstance.Target;
				Geometry geometry = Read(target, false, false, 0f);
				geometry.Name = node.Name;
				if (textureImporter != null && geometryInstance.Materials.Count > 0)
				{
					geometry.TextureName = textureImporter.AddMaterial(geometryInstance.Materials[0].Target, geometry.Name);
				}
				yield return geometry;
			}
		}

		public static Geometry Read(Oni.Dae.Geometry daeGeometry, bool generateNormals, bool flatNormals, float shellOffset)
		{
			if (daeGeometry.Primitives.Count > 1)
			{
				throw new NotSupportedException(string.Format("Geometry {0}: Multiple primitive groups per mesh are not supported", daeGeometry.Name));
			}
			MeshPrimitives meshPrimitives = daeGeometry.Primitives[0];
			if (meshPrimitives.PrimitiveType == MeshPrimitiveType.Lines || meshPrimitives.PrimitiveType == MeshPrimitiveType.LineStrips)
			{
				throw new NotSupportedException(string.Format("Geometry {0}: Line primitives are not supported", daeGeometry.Name));
			}
			Dictionary<Vector3, int> index = new Dictionary<Vector3, int>();
			List<Vector3> list = new List<Vector3>();
			int[] array = null;
			Dictionary<Vector3, int> index2 = new Dictionary<Vector3, int>();
			List<Vector3> list2 = new List<Vector3>();
			int[] array2 = null;
			Dictionary<Vector2, int> index3 = new Dictionary<Vector2, int>();
			List<Vector2> list3 = new List<Vector2>();
			int[] array3 = null;
			foreach (IndexedInput input in meshPrimitives.Inputs)
			{
				switch (input.Semantic)
				{
				case Semantic.Position:
					array = RemoveDuplicates(input, list, index, Source.ReadVector3);
					break;
				case Semantic.Normal:
					if (!generateNormals)
					{
						array2 = RemoveDuplicates(input, list2, index2, Source.ReadVector3);
					}
					break;
				case Semantic.TexCoord:
					array3 = RemoveDuplicates(input, list3, index3, Source.ReadTexCoord);
					break;
				}
			}
			if (array3 == null)
			{
				Console.WriteLine("Geometry {0} does not have texture coordinates", daeGeometry.Name);
			}
			if (array2 == null)
			{
				generateNormals = true;
			}
			Vector3[] array4 = null;
			if (generateNormals || shellOffset != 0f)
			{
				array4 = GenerateNormals(list, array, flatNormals);
			}
			if (generateNormals)
			{
				list2 = new List<Vector3>(array4);
				array2 = array;
			}
			int[] array5 = null;
			if (shellOffset != 0f)
			{
				Vector3[] normals = array4;
				if (flatNormals)
				{
					normals = GenerateNormals(list, array, false);
				}
				array5 = GenerateShell(list, array, normals, shellOffset);
			}
			int[] array6 = new int[(array5 == null) ? array.Length : (array.Length + array5.Length)];
			List<Vertex> list4 = new List<Vertex>();
			Dictionary<Vertex, int> dictionary = new Dictionary<Vertex, int>();
			for (int i = 0; i < array.Length; i++)
			{
				Vertex vertex = new Vertex(array[i], (array3 != null) ? array3[i] : (-1), (array2 != null) ? array2[i] : (-1));
				if (!dictionary.TryGetValue(vertex, out array6[i]))
				{
					array6[i] = list4.Count;
					list4.Add(vertex);
					dictionary.Add(vertex, array6[i]);
				}
			}
			if (array5 != null)
			{
				for (int j = 0; j < array5.Length; j++)
				{
					Vertex vertex2 = new Vertex(array5[j], -1, -1);
					int num = j + array.Length;
					if (!dictionary.TryGetValue(vertex2, out array6[num]))
					{
						array6[num] = list4.Count;
						list4.Add(vertex2);
						dictionary.Add(vertex2, array6[num]);
					}
				}
			}
			if (list4.Count > 2048)
			{
				Console.Error.WriteLine("Warning: Geometry {0} has too many vertices ({1})", daeGeometry.Name, list4.Count);
			}
			Geometry geometry = new Geometry();
			geometry.Points = new Vector3[list4.Count];
			geometry.Normals = new Vector3[list4.Count];
			geometry.TexCoords = new Vector2[list4.Count];
			geometry.Triangles = array6;
			Geometry geometry2 = geometry;
			for (int k = 0; k < list4.Count; k++)
			{
				geometry2.Points[k] = list[list4[k].PositionIndex];
				if (list4[k].NormalIndex != -1)
				{
					geometry2.Normals[k] = list2[list4[k].NormalIndex];
				}
				if (list4[k].TexcoordIndex != -1)
				{
					geometry2.TexCoords[k] = list3[list4[k].TexcoordIndex];
				}
			}
			return geometry2;
		}

		private static int[] RemoveDuplicates<T>(IndexedInput input, List<T> list, Dictionary<T, int> index, Func<Source, int, T> elementReader)
		{
			int[] array = new int[input.Indices.Count];
			for (int i = 0; i < array.Length; i++)
			{
				T val = elementReader(input.Source, input.Indices[i]);
				if (!index.TryGetValue(val, out array[i]))
				{
					array[i] = list.Count;
					list.Add(val);
					index.Add(val, array[i]);
				}
			}
			return array;
		}

		private static Vector3[] GenerateNormals(List<Vector3> positions, int[] triangleList, bool flatNormals)
		{
			Vector3[] array = new Vector3[positions.Count];
			if (!flatNormals)
			{
				for (int i = 0; i < triangleList.Length; i += 3)
				{
					Vector3 vector = positions[triangleList[i]];
					Vector3 vector2 = positions[triangleList[i + 1]];
					Vector3 vector3 = positions[triangleList[i + 2]];
					Vector3 v = vector2 - vector;
					Vector3 v2 = vector3 - vector;
					Vector3 v3 = Vector3.Cross(v, v2);
					float num = FMath.Atan2(v3.Length(), Vector3.Dot(v, v2));
					v3 = Vector3.Normalize(v3) * num;
					for (int j = 0; j < 3; j++)
					{
						array[triangleList[i + j]] += v3;
					}
				}
				for (int k = 0; k < array.Length; k++)
				{
					array[k].Normalize();
				}
			}
			return array;
		}

		private static int[] GenerateShell(List<Vector3> positions, int[] positionIndices, Vector3[] normals, float offset)
		{
			int count = positions.Count;
			for (int i = 0; i < count; i++)
			{
				positions.Add(positions[i] + normals[i] * offset);
			}
			int[] array = new int[positionIndices.Length];
			for (int j = 0; j < positionIndices.Length; j += 3)
			{
				array[j] = positionIndices[j + 2] + count;
				array[j + 1] = positionIndices[j + 1] + count;
				array[j + 2] = positionIndices[j] + count;
			}
			return array;
		}
	}
}
