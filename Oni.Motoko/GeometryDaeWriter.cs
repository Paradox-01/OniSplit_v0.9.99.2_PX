using System.Collections.Generic;
using Oni.Dae;

namespace Oni.Motoko
{
	internal class GeometryDaeWriter
	{
		private readonly TextureDaeWriter textureWriter;

		public GeometryDaeWriter(TextureDaeWriter textureWriter)
		{
			this.textureWriter = textureWriter;
		}

		public Node WriteNode(Geometry geometry, string name)
		{
			GeometryInstance item = WriteGeometryInstance(geometry, name);
			return new Node
			{
				Name = name,
				Instances = { (Instance)item }
			};
		}

		public GeometryInstance WriteGeometryInstance(Geometry geometry, string name)
		{
			Oni.Dae.Geometry geometry2 = WriteGeometry(geometry, name);
			GeometryInstance geometryInstance = new GeometryInstance(geometry2);
			if (geometry.Texture != null)
			{
				Material material = textureWriter.WriteMaterial(geometry.Texture);
				// The DAE writer assigns the material's XML id from its Name, and
				// several importers (e.g. Blender simple COLLADA importers) only resolve
				// a primitive's material when the bind symbol equals that id. Emitting
				// the generic "default" symbol leaves these imports untextured; use the
				// material name as symbol instead, like AkiraDaeWriter does.
				string symbol = (!string.IsNullOrEmpty(material.Name)) ? material.Name : "default";
				foreach (MeshPrimitives primitive in geometry2.Primitives)
				{
					primitive.MaterialSymbol = symbol;
				}
				geometryInstance.Materials.Add(new MaterialInstance(symbol, material)
				{
					Bindings = 
					{
						new MaterialBinding("diffuse_TEXCOORD", geometry2.Primitives[0].Inputs.Find((IndexedInput i) => i.Semantic == Semantic.TexCoord))
					}
				});
			}
			return geometryInstance;
		}

		private Oni.Dae.Geometry WriteGeometry(Geometry geometry, string name)
		{
			Vector3[] array = geometry.Points;
			Vector3[] array2 = geometry.Normals;
			Vector2[] texCoords = geometry.TexCoords;
			if (geometry.HasTransform)
			{
				array = Vector3.Transform(array, ref geometry.Transform);
				array2 = Vector3.TransformNormal(array2, ref geometry.Transform);
			}
			int[] map;
			array = WeldPoints(array, out map);
			IndexedInput indexedInput = new IndexedInput(Semantic.Position, new Source(array));
			IndexedInput indexedInput2 = new IndexedInput(Semantic.Normal, new Source(array2));
			IndexedInput indexedInput3 = new IndexedInput(Semantic.TexCoord, new Source(texCoords));
			MeshPrimitives meshPrimitives = new MeshPrimitives(MeshPrimitiveType.Polygons)
			{
				MaterialSymbol = "default",
				Inputs = { indexedInput, indexedInput2, indexedInput3 }
			};
			for (int i = 0; i < geometry.Triangles.Length; i += 3)
			{
				meshPrimitives.VertexCounts.Add(3);
				for (int j = 0; j < 3; j++)
				{
					int num = geometry.Triangles[i + j];
					indexedInput.Indices.Add(map[num]);
					indexedInput3.Indices.Add(num);
					indexedInput2.Indices.Add(num);
				}
			}
			return new Oni.Dae.Geometry
			{
				Name = name,
				Vertices = { (Input)indexedInput },
				Primitives = { meshPrimitives }
			};
		}

		private static T[] WeldPoints<T>(T[] list, out int[] map)
		{
			int[] array = new int[list.Length];
			Dictionary<T, int> dictionary = new Dictionary<T, int>(list.Length);
			List<T> list2 = new List<T>(list.Length);
			for (int i = 0; i < array.Length; i++)
			{
				T val = list[i];
				if (!dictionary.TryGetValue(val, out array[i]))
				{
					array[i] = list2.Count;
					list2.Add(val);
					dictionary.Add(val, array[i]);
				}
			}
			map = array;
			return list2.ToArray();
		}
	}
}
