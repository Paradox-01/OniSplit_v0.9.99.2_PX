using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Oni.Dae;
using Oni.Imaging;

namespace Oni.Akira
{
	internal class AkiraDaeWriter
	{
		private class DaePolygon
		{
			private readonly Polygon source;

			private readonly Material material;

			private readonly int[] pointIndices;

			private readonly int[] texCoordIndices;

			private readonly int[] colorIndices;

			public Polygon Source
			{
				get
				{
					return source;
				}
			}

			public Material Material
			{
				get
				{
					return UseOriginalMaterial ? source.OriginalMaterial : material;
				}
			}

			// Set only by -getAgqgPerPolygon so legacy exports keep marker materials.
			public bool UseOriginalMaterial { get; set; }

			public int[] PointIndices
			{
				get
				{
					return pointIndices;
				}
			}

			public int[] TexCoordIndices
			{
				get
				{
					return texCoordIndices;
				}
			}

			public int[] ColorIndices
			{
				get
				{
					return colorIndices;
				}
			}

			public DaePolygon(Polygon source, int[] pointIndices, int[] texCoordIndices, int[] colorIndices)
			{
				this.source = source;
				material = source.Material;
				this.pointIndices = pointIndices;
				this.texCoordIndices = texCoordIndices;
				this.colorIndices = colorIndices;
			}

			public DaePolygon(Material material, int[] pointIndices, int[] texCoordIndices)
			{
				this.material = material;
				this.pointIndices = pointIndices;
				this.texCoordIndices = texCoordIndices;
			}
		}

		internal class DaeMeshBuilder
		{
			private readonly List<DaePolygon> polygons = new List<DaePolygon>();

			private readonly List<Vector3> points = new List<Vector3>();

			private readonly Dictionary<Vector3, int> uniquePoints = new Dictionary<Vector3, int>();

			private readonly List<Vector2> texCoords = new List<Vector2>();

			private readonly Dictionary<Vector2, int> uniqueTexCoords = new Dictionary<Vector2, int>();

			private readonly List<Color> colors = new List<Color>();

			private readonly Dictionary<Color, int> uniqueColors = new Dictionary<Color, int>();

			private string name;

			private Vector3 translation;

			private Geometry geometry;

			public string Name
			{
				get
				{
					return name;
				}
				set
				{
					name = value;
				}
			}

			public Vector3 Translation
			{
				get
				{
					return translation;
				}
			}

			public IEnumerable<Polygon> Polygons
			{
				get
				{
					return from p in polygons
						where p.Source != null
						select p.Source;
				}
			}

			public Geometry Geometry
			{
				get
				{
					return geometry;
				}
			}

			public DaeMeshBuilder(string name)
			{
				this.name = name;
			}

			public void ResetTransform()
			{
				Vector3 center = BoundingSphere.CreateFromPoints(points).Center;
				center.Y = BoundingBox.CreateFromPoints(points).Min.Y;
				translation = center;
				for (int i = 0; i < points.Count; i++)
				{
					points[i] -= center;
				}
			}

			public void AddPolygon(Material material, Vector3[] polygonPoints, Vector2[] polygonTexCoords)
			{
				polygons.Add(new DaePolygon(material, Remap(polygonPoints, points, uniquePoints), Remap(polygonTexCoords, texCoords, uniqueTexCoords)));
			}

			public void AddPolygon(Polygon polygon)
			{
				polygons.Add(new DaePolygon(polygon, Remap(polygon.Mesh.Points, polygon.PointIndices, points, uniquePoints), Remap(polygon.Mesh.TexCoords, polygon.TexCoordIndices, texCoords, uniqueTexCoords), Remap(polygon.Colors, colors, uniqueColors)));
			}

			// Reuses normal geometry and vertex-color handling for -getAgqgPerPolygon.
			public void AddAgqgPolygon(Polygon polygon)
			{
				DaePolygon daePolygon = new DaePolygon(polygon, Remap(polygon.Mesh.Points, polygon.PointIndices, points, uniquePoints), Remap(polygon.Mesh.TexCoords, polygon.TexCoordIndices, texCoords, uniqueTexCoords), Remap(polygon.Colors, colors, uniqueColors));
				daePolygon.UseOriginalMaterial = true;
				polygons.Add(daePolygon);
			}

			private static int[] Remap<T>(IList<T> values, int[] indices, List<T> list, Dictionary<T, int> unique) where T : struct
			{
				int[] array = new int[indices.Length];
				for (int i = 0; i < indices.Length; i++)
				{
					array[i] = AddUnique(list, unique, values[indices[i]]);
				}
				return array;
			}

			private static int[] Remap<T>(IList<T> values, List<T> list, Dictionary<T, int> unique) where T : struct
			{
				int[] array = new int[values.Count];
				for (int i = 0; i < values.Count; i++)
				{
					array[i] = AddUnique(list, unique, values[i]);
				}
				return array;
			}

			private static int AddUnique<T>(List<T> list, Dictionary<T, int> unique, T value) where T : struct
			{
				int value2;
				if (!unique.TryGetValue(value, out value2))
				{
					value2 = list.Count;
					unique.Add(value, value2);
					list.Add(value);
				}
				return value2;
			}

			public void Build()
			{
				geometry = new Geometry();
				geometry.Name = Name + "_geo";
				Source source = new Source(points);
				Source source2 = new Source(texCoords);
				Source source3 = new Source(ColorArrayToFloatArray(colors), 4);
				geometry.Vertices.Add(new Input(Semantic.Position, source));
				IndexedInput indexedInput = null;
				IndexedInput indexedInput2 = null;
				IndexedInput indexedInput3 = null;
				Dictionary<Material, MeshPrimitives> dictionary = new Dictionary<Material, MeshPrimitives>();
				polygons.Sort((DaePolygon x, DaePolygon y) => string.Compare(x.Material.Name, y.Material.Name));
				foreach (DaePolygon polygon in polygons)
				{
					MeshPrimitives value;
					if (!dictionary.TryGetValue(polygon.Material, out value))
					{
						value = new MeshPrimitives(MeshPrimitiveType.Polygons);
						dictionary.Add(polygon.Material, value);
						indexedInput = new IndexedInput(Semantic.Position, source);
						value.Inputs.Add(indexedInput);
						indexedInput2 = new IndexedInput(Semantic.TexCoord, source2);
						value.Inputs.Add(indexedInput2);
						if (polygon.ColorIndices != null)
						{
							indexedInput3 = new IndexedInput(Semantic.Color, source3);
							value.Inputs.Add(indexedInput3);
						}
						value.MaterialSymbol = polygon.Material.Name;
						geometry.Primitives.Add(value);
					}
					value.VertexCounts.Add(polygon.PointIndices.Length);
					indexedInput.Indices.AddRange(polygon.PointIndices);
					indexedInput2.Indices.AddRange(polygon.TexCoordIndices);
					if (indexedInput3 != null)
					{
						indexedInput3.Indices.AddRange(polygon.ColorIndices);
					}
					if (polygon.UseOriginalMaterial)
					{
						AddAgqgMetadata(value, polygon.Source);
					}
				}
			}

			// Adds the fields requested by -getAgqgPerPolygon in emitted polygon order.
			private static void AddAgqgMetadata(MeshPrimitives primitives, Polygon polygon)
			{
				primitives.MetadataProfile = "OniSplit";
				primitives.MetadataNamespace = "https://github.com/Paradox-01/OniSplit/metadata/agqg/1.0";
				Dictionary<string, string> metadata = new Dictionary<string, string>();
				metadata.Add("agqg_index", polygon.AgqgIndex.ToString(CultureInfo.InvariantCulture));
				metadata.Add("flags", FormatAgqgFlags(polygon.AgqgFlags));
				metadata.Add("object_id_raw", polygon.AgqgObjectId.ToString(CultureInfo.InvariantCulture));
				metadata.Add("cjbo_type", polygon.ObjectType.ToString(CultureInfo.InvariantCulture));
				metadata.Add("cjbo_id", polygon.ObjectId.ToString(CultureInfo.InvariantCulture));
				metadata.Add("bsl_id", polygon.ScriptId.ToString(CultureInfo.InvariantCulture));
				primitives.PolygonMetadata.Add(metadata);
			}

			private static string FormatAgqgFlags(uint flags)
			{
				return string.Format(CultureInfo.InvariantCulture, "{0:X2} {1:X2} {2:X2} {3:X2}", (flags >> 24) & 0xFFu, (flags >> 16) & 0xFFu, (flags >> 8) & 0xFFu, flags & 0xFFu);
			}

			public void InstantiateMaterials(GeometryInstance inst, DaeSceneBuilder sceneBuilder)
			{
				Dictionary<Material, MaterialInstance> dictionary = new Dictionary<Material, MaterialInstance>();
				foreach (DaePolygon polygon in polygons)
				{
					if (dictionary.ContainsKey(polygon.Material))
					{
						continue;
					}
					string matSymbol = polygon.Material.Name;
					MaterialInstance materialInstance = new MaterialInstance(matSymbol, sceneBuilder.GetMaterial(polygon.Material));
					dictionary.Add(polygon.Material, materialInstance);
					MeshPrimitives meshPrimitives = geometry.Primitives.FirstOrDefault((MeshPrimitives p) => p.MaterialSymbol == matSymbol);
					if (meshPrimitives != null)
					{
						IndexedInput indexedInput = meshPrimitives.Inputs.Find((IndexedInput i) => i.Semantic == Semantic.TexCoord);
						if (indexedInput != null)
						{
							materialInstance.Bindings.Add(new MaterialBinding("diffuse_TEXCOORD", indexedInput));
							inst.Materials.Add(materialInstance);
						}
					}
				}
			}

			private static float[] ColorArrayToFloatArray(IList<Color> array)
			{
				float[] array2 = new float[array.Count * 4];
				for (int i = 0; i < array.Count; i++)
				{
					Vector3 vector = array[i].ToVector3();
					array2[i * 4] = vector.X;
					array2[i * 4 + 1] = vector.Y;
					array2[i * 4 + 2] = vector.Z;
				}
				return array2;
			}
		}

		internal class DaeSceneBuilder
		{
			private readonly Scene scene;

			private readonly Dictionary<string, DaeMeshBuilder> nameMeshBuilder;

			private readonly List<DaeMeshBuilder> meshBuilders;

			private readonly Dictionary<Material, Oni.Dae.Material> materials;

			private string imagesFolder = "images";

			public string ImagesFolder
			{
				get
				{
					return imagesFolder;
				}
				set
				{
					imagesFolder = value;
				}
			}

			public IEnumerable<DaeMeshBuilder> MeshBuilders
			{
				get
				{
					return meshBuilders;
				}
			}

			public DaeSceneBuilder(bool customAxisConversion = false)
			{
				scene = new Scene();
				scene.CustomAxisConversion = customAxisConversion;
				nameMeshBuilder = new Dictionary<string, DaeMeshBuilder>(StringComparer.Ordinal);
				meshBuilders = new List<DaeMeshBuilder>();
				materials = new Dictionary<Material, Oni.Dae.Material>();
			}

			public DaeMeshBuilder GetMeshBuilder(string name)
			{
				DaeMeshBuilder value;
				if (!nameMeshBuilder.TryGetValue(name, out value))
				{
					value = new DaeMeshBuilder(name);
					nameMeshBuilder.Add(name, value);
					meshBuilders.Add(value);
				}
				return value;
			}

			public Oni.Dae.Material GetMaterial(Material material)
			{
				Oni.Dae.Material value;
				if (!materials.TryGetValue(material, out value))
				{
					value = new Oni.Dae.Material();
					materials.Add(material, value);
				}
				return value;
			}

			public void Build()
			{
				BuildNodes();
				BuildMaterials();
			}

			private void BuildNodes()
			{
				foreach (DaeMeshBuilder meshBuilder in meshBuilders)
				{
					meshBuilder.Build();
					GeometryInstance geometryInstance = new GeometryInstance(meshBuilder.Geometry);
					meshBuilder.InstantiateMaterials(geometryInstance, this);
					Node node = new Node();
					node.Name = meshBuilder.Name;
					node.Instances.Add(geometryInstance);
					if (meshBuilder.Translation != Vector3.Zero)
					{
						node.Transforms.Add(new TransformTranslate(meshBuilder.Translation));
					}
					scene.Nodes.Add(node);
				}
			}

			private void BuildMaterials()
			{
				foreach (KeyValuePair<Material, Oni.Dae.Material> material in materials)
				{
					Material key = material.Key;
					Oni.Dae.Material value = material.Value;
					string imageFileName = GetImageFileName(key);
					Image initFrom = new Image
					{
						FilePath = "./" + imageFileName.Replace('\\', '/'),
						Name = key.Name + "_img"
					};
					EffectSurface effectSurface = new EffectSurface(initFrom);
					EffectSampler effectSampler = new EffectSampler(effectSurface);
					EffectTexture effectTexture = new EffectTexture(effectSampler, "diffuse_TEXCOORD");
					Effect effect = new Effect
					{
						Name = key.Name + "_fx",
						AmbientValue = Vector4.One,
						SpecularValue = Vector4.Zero,
						DiffuseValue = effectTexture,
						TransparentValue = (key.Image.HasAlpha ? effectTexture : null),
						Parameters = 
						{
							new EffectParameter("surface", effectSurface),
							new EffectParameter("sampler", effectSampler)
						}
					};
					value.Name = key.Name;
					value.Effect = effect;
				}
			}

			private string GetImageFileName(Material material)
			{
				string path = material.Name + ".tga";
				if (material.IsMarker)
				{
					return Path.Combine("markers", path);
				}
				return Path.Combine(imagesFolder, path);
			}

			public void Write(string filePath)
			{
				string directoryName = Path.GetDirectoryName(filePath);
				foreach (Material key in materials.Keys)
				{
					TgaWriter.Write(key.Image, Path.Combine(directoryName, GetImageFileName(key)));
				}
				Writer.WriteFile(filePath, scene);
			}
		}

		private readonly PolygonMesh source;

		private DaeSceneBuilder world;

		private DaeSceneBuilder worldMarkers;

		private DaeSceneBuilder[] objects;

		private DaeSceneBuilder rooms;

		private Dictionary<int, DaeSceneBuilder> scripts;

		internal static readonly string[] objectTypeNames = new string[19]
		{
			"", "char", "patr", "door", "flag", "furn", "", "", "part", "pwru",
			"sndg", "trgv", "weap", "trig", "turr", "cons", "cmbt", "mele", "neut"
		};

		public static void WriteRooms(PolygonMesh mesh, string name, string outputDirPath)
		{
			AkiraDaeWriter akiraDaeWriter = new AkiraDaeWriter(mesh);
			akiraDaeWriter.WriteRooms();
			akiraDaeWriter.rooms.Write(Path.Combine(outputDirPath, name + "_bnv.dae"));
		}

		public static void WriteRooms(PolygonMesh mesh, string filePath)
		{
			AkiraDaeWriter akiraDaeWriter = new AkiraDaeWriter(mesh);
			akiraDaeWriter.WriteRooms();
			akiraDaeWriter.rooms.Write(filePath);
		}

		public static void Write(PolygonMesh mesh, string name, string outputDirPath, string fileType)
		{
			AkiraDaeWriter akiraDaeWriter = new AkiraDaeWriter(mesh);
			akiraDaeWriter.WriteGeometry();
			akiraDaeWriter.WriteRooms();
			akiraDaeWriter.world.Write(Path.Combine(outputDirPath, name + "_env." + fileType));
			akiraDaeWriter.worldMarkers.Write(Path.Combine(outputDirPath, name + "_env_markers." + fileType));
			akiraDaeWriter.rooms.Write(Path.Combine(outputDirPath, name + "_bnv." + fileType));
			for (int i = 0; i < akiraDaeWriter.objects.Length; i++)
			{
				DaeSceneBuilder daeSceneBuilder = akiraDaeWriter.objects[i];
				if (daeSceneBuilder != null)
				{
					daeSceneBuilder.Write(Path.Combine(outputDirPath, string.Format("{0}_{1}." + fileType, name, objectTypeNames[i])));
				}
			}
			foreach (KeyValuePair<int, DaeSceneBuilder> script in akiraDaeWriter.scripts)
			{
				int key = script.Key;
				DaeSceneBuilder value = script.Value;
				value.Write(Path.Combine(outputDirPath, string.Format("{0}_script_{1}." + fileType, name, key)));
			}
		}

		private AkiraDaeWriter(PolygonMesh source)
		{
			this.source = source;
		}

		private void WriteGeometry()
		{
			world = new DaeSceneBuilder();
			worldMarkers = new DaeSceneBuilder();
			objects = new DaeSceneBuilder[objectTypeNames.Length];
			scripts = new Dictionary<int, DaeSceneBuilder>();
			foreach (Polygon polygon in source.Polygons)
			{
				if (polygon.Material == null)
				{
					continue;
				}
				int objectType = polygon.ObjectType;
				int scriptId = polygon.ScriptId;
				if (scriptId != 0)
				{
					string name = string.Format(CultureInfo.InvariantCulture, "script_{0}", new object[1] { scriptId });
					DaeSceneBuilder value;
					if (!scripts.TryGetValue(scriptId, out value))
					{
						value = new DaeSceneBuilder();
						scripts.Add(scriptId, value);
					}
					DaeMeshBuilder meshBuilder = value.GetMeshBuilder(name);
					meshBuilder.AddPolygon(polygon);
					continue;
				}
				if (objectType == -1)
				{
					string name2 = ((!source.HasDebugInfo) ? "world" : polygon.FileName);
					DaeMeshBuilder daeMeshBuilder = ((!polygon.Material.IsMarker) ? world.GetMeshBuilder(name2) : worldMarkers.GetMeshBuilder(name2));
					daeMeshBuilder.AddPolygon(polygon);
					continue;
				}
				string name3 = string.Format(CultureInfo.InvariantCulture, "{0}_{1}", new object[2]
				{
					objectTypeNames[objectType],
					polygon.ObjectId
				});
				DaeSceneBuilder daeSceneBuilder = objects[objectType];
				if (daeSceneBuilder == null)
				{
					daeSceneBuilder = new DaeSceneBuilder();
					objects[objectType] = daeSceneBuilder;
				}
				DaeMeshBuilder meshBuilder2 = daeSceneBuilder.GetMeshBuilder(name3);
				meshBuilder2.AddPolygon(polygon);
			}
			DaeSceneBuilder[] array = objects;
			foreach (DaeSceneBuilder daeSceneBuilder2 in array)
			{
				if (daeSceneBuilder2 == null)
				{
					continue;
				}
				foreach (DaeMeshBuilder meshBuilder3 in daeSceneBuilder2.MeshBuilders)
				{
					meshBuilder3.ResetTransform();
					if (!source.HasDebugInfo)
					{
						continue;
					}
					List<string> list = new List<string>();
					int num = 0;
					foreach (Polygon polygon2 in meshBuilder3.Polygons)
					{
						num = polygon2.ObjectId;
						list.Add(polygon2.ObjectName);
					}
					string text = Utils.CommonPrefix(list);
					if (!string.IsNullOrEmpty(text) && text.Length > 3)
					{
						if (!text.EndsWith("_", StringComparison.Ordinal))
						{
							text += "_";
						}
						meshBuilder3.Name = string.Format(CultureInfo.InvariantCulture, "{0}{1}", new object[2] { text, num });
					}
				}
			}
			foreach (DaeSceneBuilder value2 in scripts.Values)
			{
				foreach (DaeMeshBuilder meshBuilder4 in value2.MeshBuilders)
				{
					meshBuilder4.ResetTransform();
					if (!source.HasDebugInfo)
					{
						continue;
					}
					List<string> list2 = new List<string>();
					int num2 = 0;
					foreach (Polygon polygon3 in meshBuilder4.Polygons)
					{
						num2 = polygon3.ScriptId;
						list2.Add(polygon3.ObjectName);
					}
					string text2 = Utils.CommonPrefix(list2);
					if (!string.IsNullOrEmpty(text2) && text2.Length > 3)
					{
						if (!text2.EndsWith("_", StringComparison.Ordinal))
						{
							text2 += "_";
						}
						meshBuilder4.Name = string.Format(CultureInfo.InvariantCulture, "{0}{1}", new object[2] { text2, num2 });
					}
				}
			}
			world.Build();
			worldMarkers.Build();
			DaeSceneBuilder[] array2 = objects;
			foreach (DaeSceneBuilder daeSceneBuilder3 in array2)
			{
				if (daeSceneBuilder3 != null)
				{
					daeSceneBuilder3.Build();
				}
			}
			foreach (DaeSceneBuilder value3 in scripts.Values)
			{
				value3.Build();
			}
		}

		private void WriteRooms()
		{
			rooms = new DaeSceneBuilder(true);
			rooms.ImagesFolder = "grids";
			for (int i = 0; i < source.Rooms.Count; i++)
			{
				Room room = source.Rooms[i];
				DaeMeshBuilder meshBuilder = rooms.GetMeshBuilder(string.Format(CultureInfo.InvariantCulture, "room_{0}", new object[1] { i }));
				Material material = source.Materials.GetMaterial(string.Format(CultureInfo.InvariantCulture, "bnv_grid_{0:d3}", new object[1] { i }));
				material.Image = room.Grid.ToImage();
				foreach (Vector3[] floorPolygon in room.GetFloorPolygons())
				{
					Vector2[] array = new Vector2[floorPolygon.Length];
					for (int j = 0; j < floorPolygon.Length; j++)
					{
						Vector3 vector = floorPolygon[j];
						Vector3 min = room.BoundingBox.Min;
						Vector3 max = room.BoundingBox.Max;
						min += new Vector3(room.Grid.TileSize * (float)room.Grid.XOrigin, 0f, room.Grid.TileSize * (float)room.Grid.ZOrigin);
						max -= new Vector3(room.Grid.TileSize * (float)room.Grid.XOrigin, 0f, room.Grid.TileSize * (float)room.Grid.ZOrigin);
						Vector3 vector2 = max - min;
						float x = (vector.X - min.X) / vector2.X;
						float y = (vector.Z - min.Z) / vector2.Z;
						array[j] = new Vector2(x, y);
					}
					meshBuilder.AddPolygon(material, floorPolygon, array);
					meshBuilder.Build();
				}
			}
			Vector2[] polygonTexCoords = new Vector2[4]
			{
				new Vector2(0f, 0f),
				new Vector2(1f, 0f),
				new Vector2(1f, 1f),
				new Vector2(0f, 1f)
			};
			for (int k = 0; k < source.Ghosts.Count; k++)
			{
				Polygon polygon = source.Ghosts[k];
				DaeMeshBuilder meshBuilder2 = rooms.GetMeshBuilder(string.Format(CultureInfo.InvariantCulture, "ghost_{0}", new object[1] { k }));
				meshBuilder2.AddPolygon(source.Materials.Markers.Ghost, polygon.Points.ToArray(), polygonTexCoords);
				meshBuilder2.Build();
				meshBuilder2.ResetTransform();
			}
			rooms.Build();
		}
	}
}
