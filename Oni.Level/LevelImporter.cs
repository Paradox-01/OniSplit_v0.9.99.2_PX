using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;
using Oni.Akira;
using Oni.Dae;
using Oni.Imaging;
using Oni.Metadata;
using Oni.Motoko;
using Oni.Objects;
using Oni.Physics;
using Oni.Xml;

namespace Oni.Level
{
	internal class LevelImporter : Importer
	{
		private class Film
		{
			public string Name;

			public Vector3 Position;

			public float Facing;

			public float DesiredFacing;

			public float HeadFacing;

			public float HeadPitch;

			public readonly string[] Animations = new string[2];

			public int Length;

			public readonly List<FilmFrame> Frames = new List<FilmFrame>();
		}

		private class FilmFrame
		{
			public Vector2 MouseDelta;

			public InstanceMetadata.FILMKeys Keys;

			public uint Time;
		}

		private class NodePropertiesReader
		{
			private readonly string basePath;

			private readonly TextWriter error;

			public readonly Dictionary<string, AkiraDaeNodeProperties> properties = new Dictionary<string, AkiraDaeNodeProperties>(StringComparer.Ordinal);

			public Dictionary<string, AkiraDaeNodeProperties> Properties
			{
				get
				{
					return properties;
				}
			}

			public NodePropertiesReader(string basePath, TextWriter error)
			{
				this.basePath = basePath;
				this.error = error;
			}

			public void ReadScene(XmlReader xml, Node scene)
			{
				ObjectDaeNodeProperties objectDaeNodeProperties = new ObjectDaeNodeProperties();
				properties.Add(scene.Id, objectDaeNodeProperties);
				while (xml.IsStartElement())
				{
					switch (xml.LocalName)
					{
					case "GunkFlags":
						objectDaeNodeProperties.GunkFlags = xml.ReadElementContentAsEnum<GunkFlags>();
						break;
					case "ScriptId":
						objectDaeNodeProperties.ScriptId = xml.ReadElementContentAsInt();
						break;
					case "Node":
						ReadNode(xml, scene, objectDaeNodeProperties);
						break;
					default:
						xml.Skip();
						break;
					}
				}
			}

			private void ReadNode(XmlReader xml, Node parentNode, ObjectDaeNodeProperties parentNodeProperties)
			{
				string attribute = xml.GetAttribute("Id");
				if (string.IsNullOrEmpty(attribute))
				{
					error.Write("Each import node must have an Id attribute");
					xml.Skip();
					return;
				}
				ObjectDaeNodeProperties objectDaeNodeProperties = new ObjectDaeNodeProperties
				{
					GunkFlags = parentNodeProperties.GunkFlags,
					ScriptId = parentNodeProperties.ScriptId,
					HasPhysics = parentNodeProperties.HasPhysics
				};
				properties.Add(attribute, objectDaeNodeProperties);
				xml.ReadStartElement("Node");
				while (xml.IsStartElement())
				{
					switch (xml.LocalName)
					{
					case "GunkFlags":
						objectDaeNodeProperties.GunkFlags |= xml.ReadElementContentAsEnum<GunkFlags>();
						break;
					case "ScriptId":
						objectDaeNodeProperties.ScriptId = xml.ReadElementContentAsInt();
						break;
					case "Physics":
						objectDaeNodeProperties.PhysicsType = xml.ReadElementContentAsEnum<ObjectPhysicsType>();
						objectDaeNodeProperties.HasPhysics = true;
						break;
					case "ObjectFlags":
						objectDaeNodeProperties.ObjectFlags = xml.ReadElementContentAsEnum<ObjectSetupFlags>();
						objectDaeNodeProperties.HasPhysics = true;
						break;
					case "Animation":
						objectDaeNodeProperties.Animations.Add(ReadAnimationClip(xml));
						objectDaeNodeProperties.HasPhysics = true;
						break;
					case "Particles":
						objectDaeNodeProperties.Particles.AddRange(ReadParticles(xml, basePath));
						objectDaeNodeProperties.HasPhysics = true;
						break;
					default:
						error.WriteLine("Unknown physics object element {0}", xml.LocalName);
						xml.Skip();
						break;
					}
				}
				xml.ReadEndElement();
			}

			private ObjectAnimationClip ReadAnimationClip(XmlReader xml)
			{
				ObjectAnimationClip objectAnimationClip = new ObjectAnimationClip(xml.GetAttribute("Name"));
				if (!xml.SkipEmpty())
				{
					xml.ReadStartElement();
					while (xml.IsStartElement())
					{
						switch (xml.LocalName)
						{
						case "Start":
							objectAnimationClip.Start = xml.ReadElementContentAsInt();
							break;
						case "Stop":
							objectAnimationClip.Stop = xml.ReadElementContentAsInt();
							break;
						case "End":
							objectAnimationClip.End = xml.ReadElementContentAsInt();
							break;
						case "Flags":
							objectAnimationClip.Flags = xml.ReadElementContentAsEnum<ObjectAnimationFlags>();
							break;
						default:
							error.WriteLine("Unknown object animation property {0}", xml.LocalName);
							xml.Skip();
							break;
						}
					}
					xml.ReadEndElement();
				}
				return objectAnimationClip;
			}
		}

		private List<Scene> roomScenes;

		private PolygonMesh model;

		private AkiraDaeReader daeReader;

		private List<ObjectBase> objects;

		private ObjectLoadContext objectLoadContext;

		private InstanceFileManager fileManager;

		private TextureImporter3 textureImporter;

		private TextureFormat defaultTextureFormat = TextureFormat.BGR;

		private TextureFormat defaultAlphaTextureFormat = TextureFormat.RGBA;

		private int maxTextureSize = 512;

		private readonly TextWriter info;

		private readonly TextWriter error;

		private bool debug;

		private string outputDirPath;

		private string inputFilePath;

		private LevelDatWriter.DatLevel level;

		private string sharedPath;

		private InstanceFileManager sharedManager;

		private Dictionary<string, InstanceDescriptor> sharedCache;

		private Dictionary<string, Scene> sceneCache;

		public bool Debug
		{
			get
			{
				return debug;
			}
			set
			{
				debug = value;
			}
		}

		private void ReadCameras(XmlReader xml, string basePath)
		{
			if (xml.IsStartElement("Cameras") && !xml.SkipEmpty())
			{
				xml.ReadStartElement();
				while (xml.IsStartElement())
				{
					ReadCamera(xml, basePath);
				}
				xml.ReadEndElement();
			}
		}

		private void ReadCamera(XmlReader xml, string basePath)
		{
			string text = Path.Combine(basePath, xml.GetAttribute("Path"));
			Scene scene = LoadScene(text);
			List<ObjectAnimationClip> clips = new List<ObjectAnimationClip>();
			if (text.Contains("Camout"))
			{
				System.Console.WriteLine(text);
			}
			ReadSequence(xml, "Camera", delegate(string name)
			{
				if (name != null && name == "Animation")
				{
					clips.Add(ReadAnimationClip(xml));
					return true;
				}
				return false;
			});
			ObjectDaeNodeProperties objectDaeNodeProperties = new ObjectDaeNodeProperties();
			objectDaeNodeProperties.HasPhysics = true;
			objectDaeNodeProperties.Animations.AddRange(clips);
			ObjectDaeImporter objectDaeImporter = new ObjectDaeImporter(null, new Dictionary<string, AkiraDaeNodeProperties> { { scene.Id, objectDaeNodeProperties } });
			objectDaeImporter.Import(scene);
			foreach (ObjectNode node in objectDaeImporter.Nodes)
			{
				ObjectAnimation[] animations = node.Animations;
				foreach (ObjectAnimation animation in animations)
				{
					DatWriter datWriter = new DatWriter();
					ObjectDatWriter.WriteAnimation(animation, datWriter);
					datWriter.Write(outputDirPath, text);
				}
			}
		}

		private void ReadSequence(XmlReader xml, string name, Func<string, bool> action)
		{
			if (xml.SkipEmpty())
			{
				return;
			}
			xml.ReadStartElement(name);
			while (xml.IsStartElement())
			{
				if (!action(xml.LocalName))
				{
					error.WriteLine("Unknown element {0}", xml.LocalName);
					xml.Skip();
				}
			}
			xml.ReadEndElement();
		}

		private static IEnumerable<ScriptCharacter> ReadCharacters(XmlReader xml, string basePath)
		{
			if (!xml.SkipEmpty())
			{
				xml.ReadStartElement();
				while (xml.IsStartElement())
				{
					yield return ScriptCharacter.Read(xml);
				}
				xml.ReadEndElement();
			}
		}

		private void ReadFilms(XmlReader xml, string basePath)
		{
			if (!xml.IsStartElement("Films") || xml.SkipEmpty())
			{
				return;
			}
			xml.ReadStartElement("Films");
			while (xml.IsStartElement())
			{
				xml.ReadStartElement("Import");
				string text = Path.Combine(basePath, xml.ReadElementContentAsString());
				xml.ReadEndElement();
				if (!File.Exists(text))
				{
					error.WriteLine("Could not find file '{0}'", text);
					continue;
				}
				string extension = Path.GetExtension(text);
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
				if (string.Equals(extension, ".oni", StringComparison.OrdinalIgnoreCase))
				{
					string destFileName = Path.Combine(outputDirPath, fileNameWithoutExtension + ".oni");
					File.Copy(text, destFileName, true);
				}
				else if (string.Equals(extension, ".dat", StringComparison.OrdinalIgnoreCase))
				{
					Film film = ReadBinFilm(text);
					DatWriter datWriter = new DatWriter();
					WriteDatFilm(datWriter, film);
					datWriter.Write(outputDirPath, text);
				}
				else if (string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase))
				{
					Film film2 = ReadXmlFilm(text);
					DatWriter datWriter2 = new DatWriter();
					WriteDatFilm(datWriter2, film2);
					datWriter2.Write(outputDirPath, text);
				}
				else
				{
					error.WriteLine("Unsupported film file type {0}", extension);
				}
			}
			xml.ReadEndElement();
		}

		private static Film ReadBinFilm(string filePath)
		{
			string text = Path.GetFileNameWithoutExtension(filePath);
			if (text.StartsWith("FILM", StringComparison.Ordinal))
			{
				text = text.Substring(4);
			}
			Film film = new Film();
			film.Name = text;
			using (BinaryReader binaryReader = new BinaryReader(filePath, true))
			{
				film.Animations[0] = binaryReader.ReadString(128);
				film.Animations[1] = binaryReader.ReadString(128);
				film.Position = binaryReader.ReadVector3();
				film.Facing = binaryReader.ReadSingle();
				film.DesiredFacing = binaryReader.ReadSingle();
				film.HeadFacing = binaryReader.ReadSingle();
				film.HeadPitch = binaryReader.ReadSingle();
				film.Length = binaryReader.ReadInt32();
				binaryReader.Skip(28);
				int num = binaryReader.ReadInt32();
				film.Frames.Capacity = num;
				for (int i = 0; i < num; i++)
				{
					FilmFrame filmFrame = new FilmFrame();
					filmFrame.MouseDelta = binaryReader.ReadVector2();
					filmFrame.Keys = (InstanceMetadata.FILMKeys)binaryReader.ReadUInt64();
					filmFrame.Time = binaryReader.ReadUInt32();
					binaryReader.Skip(4);
					film.Frames.Add(filmFrame);
				}
				return film;
			}
		}

		private static Film ReadXmlFilm(string filePath)
		{
			string text = Path.GetFileNameWithoutExtension(filePath);
			if (text.StartsWith("FILM", StringComparison.Ordinal))
			{
				text = text.Substring(4);
			}
			Film film = new Film();
			film.Name = text;
			XmlReaderSettings settings = new XmlReaderSettings
			{
				IgnoreWhitespace = true,
				IgnoreProcessingInstructions = true,
				IgnoreComments = true
			};
			using (XmlReader xmlReader = XmlReader.Create(filePath, settings))
			{
				xmlReader.ReadStartElement("Oni");
				text = xmlReader.GetAttribute("Name");
				if (!string.IsNullOrEmpty(text))
				{
					film.Name = text;
				}
				xmlReader.ReadStartElement("FILM");
				film.Position = xmlReader.ReadElementContentAsVector3("Position");
				film.Facing = xmlReader.ReadElementContentAsFloat("Facing", "");
				film.DesiredFacing = xmlReader.ReadElementContentAsFloat("DesiredFacing", "");
				film.HeadFacing = xmlReader.ReadElementContentAsFloat("HeadFacing", "");
				film.HeadPitch = xmlReader.ReadElementContentAsFloat("HeadPitch", "");
				film.Length = xmlReader.ReadElementContentAsInt("FrameCount", "");
				xmlReader.ReadStartElement("Animations");
				film.Animations[0] = xmlReader.ReadElementContentAsString("Link", "");
				film.Animations[1] = xmlReader.ReadElementContentAsString("Link", "");
				xmlReader.ReadEndElement();
				xmlReader.ReadStartElement("Frames");
				while (xmlReader.IsStartElement())
				{
					FilmFrame filmFrame = new FilmFrame();
					switch (xmlReader.LocalName)
					{
					case "FILMFrame":
						xmlReader.ReadStartElement();
						filmFrame.MouseDelta.X = xmlReader.ReadElementContentAsFloat("MouseDeltaX", "");
						filmFrame.MouseDelta.Y = xmlReader.ReadElementContentAsFloat("MouseDeltaY", "");
						filmFrame.Keys = xmlReader.ReadElementContentAsEnum<InstanceMetadata.FILMKeys>("Keys");
						filmFrame.Time = (uint)xmlReader.ReadElementContentAsInt("Frame", "");
						xmlReader.ReadEndElement();
						break;
					case "Frame":
						xmlReader.ReadStartElement();
						while (xmlReader.IsStartElement())
						{
							switch (xmlReader.LocalName)
							{
							case "Time":
								filmFrame.Time = (uint)xmlReader.ReadElementContentAsInt();
								break;
							case "MouseDelta":
								filmFrame.MouseDelta = xmlReader.ReadElementContentAsVector2();
								break;
							case "Keys":
								filmFrame.Keys = xmlReader.ReadElementContentAsEnum<InstanceMetadata.FILMKeys>();
								break;
							}
						}
						xmlReader.ReadEndElement();
						break;
					default:
						xmlReader.Skip();
						continue;
					}
					film.Frames.Add(filmFrame);
				}
				xmlReader.ReadEndElement();
				xmlReader.ReadEndElement();
				return film;
			}
		}

		private static void WriteDatFilm(DatWriter filmWriter, Film film)
		{
			ImporterDescriptor importerDescriptor = filmWriter.CreateInstance(TemplateTag.FILM, film.Name);
			ImporterDescriptor[] array = new ImporterDescriptor[2];
			for (int i = 0; i < array.Length; i++)
			{
				if (!string.IsNullOrEmpty(film.Animations[i]))
				{
					array[i] = filmWriter.CreateInstance(TemplateTag.TRAM, film.Animations[i]);
				}
			}
			using (BinaryWriter binaryWriter = importerDescriptor.OpenWrite())
			{
				binaryWriter.Write(film.Position);
				binaryWriter.Write(film.Facing);
				binaryWriter.Write(film.DesiredFacing);
				binaryWriter.Write(film.HeadFacing);
				binaryWriter.Write(film.HeadPitch);
				binaryWriter.Write(film.Length);
				binaryWriter.Write(array);
				binaryWriter.Skip(12);
				binaryWriter.Write(film.Frames.Count);
				foreach (FilmFrame frame in film.Frames)
				{
					binaryWriter.Write(frame.MouseDelta);
					binaryWriter.Write((ulong)frame.Keys);
					binaryWriter.Write(frame.Time);
					binaryWriter.Skip(4);
				}
			}
		}

		private void ReadModel(XmlReader xml, string basePath)
		{
			xml.ReadStartElement("Environment");
			xml.ReadStartElement("Model");
			daeReader = new AkiraDaeReader();
			model = daeReader.Mesh;
			level.model = model;
			info.WriteLine("Reading environment...");
			while (xml.IsStartElement())
			{
				switch (xml.LocalName)
				{
				case "Import":
				case "Scene":
					ImportModelScene(xml, basePath);
					break;
				case "Object":
					xml.Skip();
					break;
				case "Camera":
					ReadCamera(xml, basePath);
					break;
				case "Texture":
					textureImporter.ReadOptions(xml, basePath);
					break;
				default:
					error.WriteLine("Unknown element {0}", xml.LocalName);
					xml.Skip();
					break;
				}
			}
			info.WriteLine("Reading rooms...");
			roomScenes = new List<Scene>();
			xml.ReadEndElement();
			xml.ReadStartElement("Rooms");
			while (xml.IsStartElement("Import"))
			{
				string text = xml.GetAttribute("Path");
				if (text == null)
				{
					text = xml.ReadElementContentAsString();
				}
				else
				{
					xml.Skip();
				}
				text = Path.Combine(basePath, text);
				roomScenes.Add(LoadScene(text));
			}
			xml.ReadEndElement();
			if (xml.IsStartElement("Textures"))
			{
				ReadTextures(xml, basePath);
			}
			xml.ReadEndElement();
		}

		private void ImportModelScene(XmlReader xml, string basePath)
		{
			bool flag = false;
			string filePath = basePath;
			if (!string.IsNullOrEmpty(xml.GetAttribute("Path")))
			{
				filePath = Path.Combine(basePath, xml.GetAttribute("Path"));
			}
			else
			{
				string text = xml.ReadElementContentAsString();
				if (!string.IsNullOrEmpty(text))
				{
					filePath = Path.Combine(basePath, text);
				}
				flag = true;
			}
			Scene scene = LoadScene(filePath);
			NodePropertiesReader nodePropertiesReader = new NodePropertiesReader(basePath, error);
			if (!xml.SkipEmpty())
			{
				if (!flag)
				{
					xml.ReadStartElement();
				}
				nodePropertiesReader.ReadScene(xml, scene);
				if (!flag)
				{
					xml.ReadEndElement();
				}
			}
			daeReader.ReadScene(scene, nodePropertiesReader.Properties);
			if (!nodePropertiesReader.Properties.Values.Any((AkiraDaeNodeProperties p) => p.HasPhysics))
			{
				return;
			}
			ObjectDaeImporter objectDaeImporter = new ObjectDaeImporter(textureImporter, nodePropertiesReader.Properties);
			objectDaeImporter.Import(scene);
			foreach (ObjectNode item in objectDaeImporter.Nodes.Where((ObjectNode n) => n.Geometries.Length != 0))
			{
				ObjectSetup objectSetup = new ObjectSetup
				{
					Name = item.Name,
					FileName = item.FileName,
					ScriptId = item.ScriptId,
					Flags = item.Flags,
					PhysicsType = ObjectPhysicsType.Animated
				};
				object[] geometries = (from n in item.Geometries
					where (n.Flags & GunkFlags.Invisible) == 0
					select n.Geometry.Name).ToArray();
				objectSetup.Geometries = geometries;
				foreach (ObjectGeometry item2 in item.Geometries.Where((ObjectGeometry g) => (g.Flags & GunkFlags.Invisible) == 0))
				{
					DatWriter datWriter = new DatWriter();
					GeometryDatWriter.Write(item2.Geometry, datWriter.ImporterFile);
					datWriter.Write(outputDirPath, filePath);
				}
				objectSetup.Position = Vector3.Zero;
				objectSetup.Orientation = Quaternion.Identity;
				objectSetup.Scale = 1f;
				objectSetup.Origin = Matrix.CreateFromQuaternion(objectSetup.Orientation) * Matrix.CreateScale(objectSetup.Scale) * Matrix.CreateTranslation(objectSetup.Position);
				ObjectAnimation[] animations = item.Animations;
				foreach (ObjectAnimation objectAnimation in animations)
				{
					if ((objectAnimation.Flags & ObjectAnimationFlags.Local) == 0)
					{
						ObjectAnimationKey[] keys = objectAnimation.Keys;
						foreach (ObjectAnimationKey objectAnimationKey in keys)
						{
							objectAnimationKey.Rotation = objectSetup.Orientation * objectAnimationKey.Rotation;
							objectAnimationKey.Translation += objectSetup.Position;
						}
					}
					if ((objectAnimation.Flags & ObjectAnimationFlags.AutoStart) != ObjectAnimationFlags.None)
					{
						objectSetup.Animation = objectAnimation;
						objectSetup.PhysicsType = ObjectPhysicsType.Animated;
					}
					DatWriter datWriter2 = new DatWriter();
					datWriter2.BeginImport();
					ObjectDatWriter.WriteAnimation(objectAnimation, datWriter2);
					datWriter2.Write(outputDirPath, filePath);
				}
				if (objectSetup.Animation == null && item.Animations.Length != 0)
				{
					objectSetup.Animation = item.Animations[0];
				}
				if (objectSetup.Animation != null)
				{
					ObjectAnimationKey objectAnimationKey2 = objectSetup.Animation.Keys[0];
					objectSetup.Scale = objectAnimationKey2.Scale.X;
					objectSetup.Orientation = objectAnimationKey2.Rotation;
					objectSetup.Position = objectAnimationKey2.Translation;
				}
				level.physics.Add(objectSetup);
			}
		}

		private void ImportModel(string basePath)
		{
			info.WriteLine("Importing objects...");
			ImportGunkObjects();
			info.WriteLine("Importing textures...");
			ImportModelTextures();
			info.WriteLine("Generating grids...");
			string filePath = Path.Combine(basePath, string.Format("temp/grids/{0}_grids.dae", level.name));
			RoomGridBuilder roomGridBuilder = new RoomGridBuilder(roomScenes[0], model);
			roomGridBuilder.Build();
			AkiraDaeWriter.WriteRooms(roomGridBuilder.Mesh, filePath);
			daeReader.ReadScene(Reader.ReadFile(filePath), new Dictionary<string, AkiraDaeNodeProperties>());
			info.WriteLine("Writing environment...");
			DatWriter datWriter = new DatWriter();
			AkiraDatWriter.Write(model, datWriter, level.name, debug);
			datWriter.Write(outputDirPath, inputFilePath);
		}

		private void ImportGunkNode(int gunkId, Matrix transform, GunkFlags flags, Oni.Motoko.Geometry geometry)
		{
			ImportGunk(gunkId, transform, flags, geometry, null);
		}

		private void ImportGunk(int gunkId, Matrix transform, GunkFlags flags, Oni.Motoko.Geometry geometry, string textureName)
		{
			TextureFormat? textureFormat = null;
			if (geometry.Texture != null)
			{
				Texture texture = null;
				if (!geometry.Texture.IsPlaceholder)
				{
					texture = TextureDatReader.ReadInfo(geometry.Texture);
				}
				else
				{
					InstanceDescriptor instanceDescriptor = FindSharedInstance(TemplateTag.TXMP, geometry.Texture.Name);
					if (instanceDescriptor != null)
					{
						texture = TextureDatReader.ReadInfo(instanceDescriptor);
					}
				}
				if (texture != null)
				{
					textureFormat = texture.Format;
				}
			}
			else if (geometry.TextureName != null)
			{
				TextureImporterOptions options = textureImporter.GetOptions(geometry.TextureName, false);
				if (options != null)
				{
					textureFormat = options.Format;
				}
			}
			switch (textureFormat)
			{
			case TextureFormat.BGRA4444:
			case TextureFormat.BGRA5551:
			case TextureFormat.RGBA:
				flags |= GunkFlags.Transparent | GunkFlags.TwoSided | GunkFlags.NoOcclusion;
				break;
			}
			Oni.Akira.Material material = ((!string.IsNullOrEmpty(textureName)) ? model.Materials.GetMaterial(textureName) : ((!string.IsNullOrEmpty(geometry.TextureName)) ? model.Materials.GetMaterial(geometry.TextureName) : ((geometry.Texture == null) ? model.Materials.GetMaterial("NONE") : model.Materials.GetMaterial(geometry.Texture.Name))));
			int count = model.Points.Count;
			int count2 = model.TexCoords.Count;
			model.Points.AddRange(Vector3.Transform(geometry.Points, ref transform));
			model.TexCoords.AddRange(geometry.TexCoords);
			foreach (int[] item in Quadify.Do(geometry))
			{
				int[] array = new int[item.Length];
				int[] array2 = new int[item.Length];
				Color[] array3 = new Color[item.Length];
				for (int i = 0; i < item.Length; i++)
				{
					array[i] = count + item[i];
					array2[i] = count2 + item[i];
					array3[i] = new Color(207, 207, 207);
				}
				Polygon polygon = new Polygon(model, array)
				{
					TexCoordIndices = array2,
					Colors = array3,
					Material = material,
					ObjectId = (gunkId & 0xFFFFFF),
					ObjectType = gunkId >> 24
				};
				polygon.Flags |= flags;
				model.Polygons.Add(polygon);
			}
		}

		private void ImportModelTextures()
		{
			int imported = 0;
			int copied = 0;
			List<InstanceDescriptor> list = new List<InstanceDescriptor>();
			foreach (Oni.Akira.Material item in model.Polygons.Select((Polygon p) => p.Material).Distinct())
			{
				if (File.Exists(item.ImageFilePath))
				{
					foreach (string meshName in model.Polygons.Where((Polygon p) => p.Material == item).Select((Polygon p) => p.ObjectName).Distinct())
					{
						TgaMeshUsage.Register(item.ImageFilePath, meshName);
					}
					TextureImporterOptions textureImporterOptions = textureImporter.AddMaterial(item);
					if (textureImporterOptions != null)
					{
						item.Flags |= textureImporterOptions.GunkFlags;
					}
					imported++;
				}
				else
				{
					InstanceDescriptor instanceDescriptor = FindSharedInstance(TemplateTag.TXMP, item.Name);
					if (instanceDescriptor != null)
					{
						list.Add(instanceDescriptor);
					}
				}
			}
			Parallel.ForEach(list, delegate(InstanceDescriptor txmp)
			{
				Texture texture = TextureDatReader.Read(txmp);
				if ((texture.Flags & TextureFlags.HasMipMaps) == 0)
				{
					texture.GenerateMipMaps();
					TextureDatWriter.Write(texture, outputDirPath);
					Interlocked.Increment(ref imported);
				}
				else
				{
					string filePath = txmp.File.FilePath;
					File.Copy(filePath, Path.Combine(outputDirPath, Path.GetFileName(filePath)), true);
					Interlocked.Increment(ref copied);
				}
			});
			error.WriteLine("Imported {0} textures, copied {1} textures", imported, copied);
		}

		private ObjectAnimationClip ReadAnimationClip(XmlReader xml)
		{
			ObjectAnimationClip objectAnimationClip = new ObjectAnimationClip(xml.GetAttribute("Name"));
			if (!xml.SkipEmpty())
			{
				xml.ReadStartElement();
				while (xml.IsStartElement())
				{
					switch (xml.LocalName)
					{
					case "Start":
						objectAnimationClip.Start = xml.ReadElementContentAsInt();
						break;
					case "Stop":
						objectAnimationClip.Stop = xml.ReadElementContentAsInt();
						break;
					case "End":
						objectAnimationClip.End = xml.ReadElementContentAsInt();
						break;
					case "Flags":
						objectAnimationClip.Flags = xml.ReadElementContentAsEnum<ObjectAnimationFlags>();
						break;
					default:
						error.WriteLine("Unknown object animation parameter {0}", xml.LocalName);
						xml.Skip();
						break;
					}
				}
				xml.ReadEndElement();
			}
			return objectAnimationClip;
		}

		private void ReadObjects(XmlReader xml, string basePath)
		{
			info.WriteLine("Reading objects...");
			objects = new List<ObjectBase>();
			objectLoadContext = new ObjectLoadContext(FindSharedInstance, info);
			xml.ReadStartElement("Objects");
			while (xml.IsStartElement())
			{
				ReadObjectFile(Path.Combine(basePath, xml.ReadElementContentAsString("Import", "")));
			}
			xml.ReadEndElement();
		}

		private void ReadObjectFile(string filePath)
		{
			string directoryName = Path.GetDirectoryName(filePath);
			objectLoadContext.BasePath = directoryName;
			objectLoadContext.FilePath = filePath;
			XmlReaderSettings settings = new XmlReaderSettings
			{
				IgnoreWhitespace = true,
				IgnoreProcessingInstructions = true,
				IgnoreComments = true
			};
			using (XmlReader xmlReader = XmlReader.Create(filePath, settings))
			{
				xmlReader.ReadStartElement("Oni");
				switch (xmlReader.LocalName)
				{
				case "Objects":
					objects.AddRange(ReadObjects(xmlReader));
					break;
				case "Particles":
					level.particles.AddRange(ReadParticles(xmlReader, directoryName));
					break;
				case "Characters":
					level.characters.AddRange(ReadCharacters(xmlReader, directoryName));
					break;
				case "Physics":
					ReadPhysics(xmlReader, directoryName);
					break;
				case "Corpses":
				case "CRSA":
					level.corpses.AddRange(ReadCorpses(xmlReader, directoryName));
					break;
				default:
					error.WriteLine("Unknown object file type {0}", xmlReader.LocalName);
					xmlReader.Skip();
					break;
				}
				xmlReader.ReadEndElement();
			}
		}

		private IEnumerable<ObjectBase> ReadObjects(XmlReader xml)
		{
			if (xml.SkipEmpty())
			{
				yield break;
			}
			xml.ReadStartElement();
			while (xml.IsStartElement())
			{
				ObjectBase objectBase;
				switch (xml.LocalName)
				{
				case "CHAR":
				case "Character":
					objectBase = new Character();
					break;
				case "WEAP":
				case "Weapon":
					objectBase = new Weapon();
					break;
				case "PART":
				case "Particle":
					objectBase = new Particle();
					break;
				case "PWRU":
				case "PowerUp":
					objectBase = new PowerUp();
					break;
				case "FLAG":
				case "Flag":
					objectBase = new Flag();
					break;
				case "DOOR":
				case "Door":
					objectBase = new Door();
					break;
				case "CONS":
				case "Console":
					objectBase = new Oni.Objects.Console();
					break;
				case "FURN":
				case "Furniture":
					objectBase = new Furniture();
					break;
				case "TURR":
				case "Turret":
					objectBase = new Turret();
					break;
				case "SNDG":
				case "Sound":
					objectBase = new Oni.Objects.Sound();
					break;
				case "TRIG":
				case "Trigger":
					objectBase = new Trigger();
					break;
				case "TRGV":
				case "TriggerVolume":
					objectBase = new TriggerVolume();
					break;
				case "NEUT":
				case "Neutral":
					objectBase = new Neutral();
					break;
				case "PATR":
				case "Patrol":
					objectBase = new PatrolPath();
					break;
				default:
					error.WriteLine("Unknonw object type {0}", xml.LocalName);
					xml.Skip();
					continue;
				}
				objectBase.Read(xml, objectLoadContext);
				GunkObject gunkObject = objectBase as GunkObject;
				if (gunkObject == null || gunkObject.GunkClass != null)
				{
					yield return objectBase;
				}
			}
			xml.ReadEndElement();
		}

		private void ImportGunkObjects()
		{
			int num = 1;
			foreach (ObjectBase @object in objects)
			{
				@object.ObjectId = num++;
				if (@object is Door)
				{
					ImportDoor((Door)@object);
				}
				else if (@object is Furniture)
				{
					ImportFurniture((Furniture)@object);
				}
				else if (@object is GunkObject)
				{
					ImportGunkObject((GunkObject)@object, GunkFlags.NoOcclusion);
				}
			}
		}

		private void ImportFurniture(Furniture furniture)
		{
			ImportGunkObject(furniture, GunkFlags.NoOcclusion | GunkFlags.Furniture);
			ObjectParticle[] particles = furniture.Class.Geometry.Particles;
			foreach (ObjectParticle particle in particles)
			{
				ImportParticle(furniture.ParticleTag, furniture.Transform, particle);
			}
		}

		private void ImportGunkObject(GunkObject gunkObject, GunkFlags flags)
		{
			ObjectGeometry[] gunkNodes = gunkObject.GunkClass.GunkNodes;
			foreach (ObjectGeometry objectGeometry in gunkNodes)
			{
				ImportGunkNode(gunkObject.GunkId, gunkObject.Transform, objectGeometry.Flags | flags, objectGeometry.Geometry);
			}
		}

		private void ImportDoor(Door door)
		{
			Matrix transform = door.Transform;
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			Matrix m = Matrix.CreateScale(door.Class.Animation.Keys[0].Scale) * Matrix.CreateRotationX(-1.5707965f);
			ObjectGeometry[] gunkNodes = door.Class.GunkNodes;
			foreach (ObjectGeometry objectGeometry in gunkNodes)
			{
				BoundingBox boundingBox = BoundingBox.CreateFromPoints(Vector3.Transform(objectGeometry.Geometry.Points, ref m));
				num = Math.Min(num, boundingBox.Min.Y);
				num2 = Math.Min(num2, boundingBox.Min.X);
				num3 = Math.Max(num3, boundingBox.Max.X);
			}
			transform.Translation -= Vector3.UnitY * num;
			float num4;
			int num5;
			if ((door.Flags & DoorFlags.DoubleDoor) == 0)
			{
				num4 = 0f;
				num5 = 1;
			}
			else
			{
				num4 = (num3 - num2) / 2f;
				num5 = 2;
			}
			for (int j = 0; j < num5; j++)
			{
				Matrix matrix2;
				Matrix transform2;
				if (j == 0)
				{
					Matrix matrix = Matrix.CreateTranslation(num4, 0f, 0f) * transform;
					matrix2 = m * matrix;
					transform2 = matrix2;
				}
				else
				{
					Matrix matrix3 = Matrix.CreateTranslation(0f - num4, 0f, 0f) * transform;
					matrix2 = Matrix.CreateRotationY(3.141593f) * m * matrix3;
					transform2 = m * Matrix.CreateRotationY(3.141593f) * matrix3;
				}
				int num6 = door.ScriptId | (j << 12);
				Oni.Motoko.Geometry[] array = ImportDoorGeometry(door, j);
				List<ObjectSetup> physics = level.physics;
				ObjectSetup obj = new ObjectSetup
				{
					Name = string.Format("door_{0}", num6),
					Flags = ObjectSetupFlags.FaceCollision,
					DoorScriptId = num6,
					Origin = matrix2
				};
				object[] geometries = array;
				obj.Geometries = geometries;
				physics.Add(obj);
				Oni.Motoko.Geometry[] array2 = array;
				foreach (Oni.Motoko.Geometry geometry in array2)
				{
					ImportGunkNode(door.GunkId, transform2, GunkFlags.NoCollision | GunkFlags.NoDecals, geometry);
				}
			}
		}

		private Oni.Motoko.Geometry[] ImportDoorGeometry(Door door, int side)
		{
			InstanceDescriptor instanceDescriptor = null;
			if (!string.IsNullOrEmpty(door.Textures[side]))
			{
				instanceDescriptor = FindSharedInstance(TemplateTag.TXMP, door.Textures[side]);
			}
			ObjectGeometry[] geometries = door.Class.Geometry.Geometries;
			Oni.Motoko.Geometry[] array = new Oni.Motoko.Geometry[geometries.Length];
			for (int i = 0; i < geometries.Length; i++)
			{
				ObjectGeometry objectGeometry = geometries[i];
				Oni.Motoko.Geometry geometry = new Oni.Motoko.Geometry
				{
					Points = objectGeometry.Geometry.Points,
					TexCoords = objectGeometry.Geometry.TexCoords,
					Normals = objectGeometry.Geometry.Normals,
					Triangles = objectGeometry.Geometry.Triangles
				};
				if (instanceDescriptor != null)
				{
					geometry.Texture = instanceDescriptor;
					geometry.TextureName = instanceDescriptor.Name;
				}
				else if (objectGeometry.Geometry.Texture != null)
				{
					geometry.TextureName = objectGeometry.Geometry.Texture.Name;
				}
				array[i] = geometry;
			}
			return array;
		}

		private void WriteObjects()
		{
			ObjcDatWriter.Write(objects, outputDirPath, inputFilePath);
		}

		private IEnumerable<Corpse> ReadCorpses(XmlReader xml, string basePath)
		{
			string fileName = Path.GetFileName(objectLoadContext.FilePath);
			bool isOldFormat = xml.IsStartElement("CRSA");
			int readCount = 0;
			int fixedCount = 0;
			int usedCount = 0;
			if (isOldFormat)
			{
				xml.ReadStartElement("CRSA");
				if (xml.IsStartElement("FixedCount"))
				{
					fixedCount = xml.ReadElementContentAsInt("FixedCount", "");
				}
				if (xml.IsStartElement("UsedCount"))
				{
					usedCount = xml.ReadElementContentAsInt("UsedCount", "");
				}
				if (usedCount < fixedCount)
				{
					error.WriteLine("There are more fixed corpses ({0}) than used corpses ({1}) - assuming fixed = used", fixedCount, usedCount);
					fixedCount = usedCount;
				}
			}
			xml.ReadStartElement("Corpses");
			while (xml.IsStartElement())
			{
				Corpse corpse = new Corpse();
				corpse.IsFixed = isOldFormat && readCount < fixedCount;
				corpse.IsUsed = !isOldFormat || readCount < usedCount;
				corpse.FileName = fileName;
				if (xml.IsEmptyElement)
				{
					corpse.IsUsed = false;
				}
				if (!corpse.IsUsed)
				{
					xml.Skip();
				}
				else if (xml.LocalName == "Corpse" || xml.LocalName == "CRSACorpse")
				{
					xml.ReadStartElement();
					if (!isOldFormat)
					{
						if (xml.IsStartElement("CanDelete"))
						{
							corpse.IsFixed = false;
							xml.Skip();
						}
						else
						{
							corpse.IsFixed = true;
						}
					}
					if (xml.IsStartElement("Class") || xml.IsStartElement("CharacterClass"))
					{
						corpse.CharacterClass = xml.ReadElementContentAsString();
					}
					if (string.IsNullOrEmpty(corpse.CharacterClass))
					{
						corpse.IsUsed = false;
						corpse.IsFixed = false;
					}
					xml.ReadStartElement("Transforms");
					for (int i = 0; i < corpse.Transforms.Length; i++)
					{
						if (xml.IsStartElement("Matrix4x3"))
						{
							corpse.Transforms[i] = xml.ReadElementContentAsMatrix43("Matrix4x3");
						}
						else if (xml.IsStartElement("Matrix"))
						{
							corpse.Transforms[i] = xml.ReadElementContentAsMatrix43("Matrix");
						}
					}
					xml.ReadEndElement();
					if (xml.IsStartElement("BoundingBox"))
					{
						xml.ReadStartElement("BoundingBox");
						corpse.BoundingBox.Min = xml.ReadElementContentAsVector3("Min");
						corpse.BoundingBox.Max = xml.ReadElementContentAsVector3("Max");
						xml.ReadEndElement();
					}
					else
					{
						corpse.BoundingBox.Min = corpse.Transforms[0].Translation;
						corpse.BoundingBox.Max = corpse.Transforms[0].Translation;
						corpse.BoundingBox.Inflate(new Vector3(10f, 5f, 10f));
					}
					xml.ReadEndElement();
				}
				else
				{
					string path = xml.ReadElementContentAsString("Import", "");
					path = Path.Combine(basePath, path);
					using (BinaryReader binaryReader = new BinaryReader(path))
					{
						corpse.FileName = Path.GetFileName(path);
						corpse.IsUsed = true;
						corpse.IsFixed = true;
						corpse.CharacterClass = binaryReader.ReadString(128);
						binaryReader.Skip(4);
						for (int j = 0; j < corpse.Transforms.Length; j++)
						{
							corpse.Transforms[j] = binaryReader.ReadMatrix4x3();
						}
						corpse.BoundingBox = binaryReader.ReadBoundingBox();
					}
				}
				readCount++;
				yield return corpse;
			}
			if (readCount < usedCount)
			{
				error.WriteLine("{0} corpses were expected but only {1} have been read", usedCount, readCount);
			}
			info.WriteLine("Read {0} corpses", readCount);
		}

		private InstanceDescriptor FindSharedInstance(TemplateTag tag, string name, ObjectLoadContext loadContext)
		{
			if (!name.EndsWith(".oni", StringComparison.OrdinalIgnoreCase))
			{
				return FindSharedInstance(tag, name);
			}
			string fullPath = Path.GetFullPath(Path.Combine(loadContext.BasePath, name));
			if (File.Exists(fullPath))
			{
				if (fileManager == null)
				{
					fileManager = new InstanceFileManager();
				}
				return fileManager.OpenFile(fullPath).Descriptors[0];
			}
			fullPath = Path.GetFullPath(Path.Combine(sharedPath, name));
			if (File.Exists(fullPath))
			{
				InstanceFile instanceFile = sharedManager.OpenFile(fullPath);
				return instanceFile.Descriptors[0];
			}
			error.WriteLine("Could not find {0}", name);
			return null;
		}

		private static IEnumerable<ObjectParticle> ReadParticles(XmlReader xml, string basePath)
		{
			if (!xml.SkipEmpty())
			{
				xml.ReadStartElement();
				while (xml.IsStartElement())
				{
					yield return ObjectXmlReader.ReadParticle(xml);
				}
				xml.ReadEndElement();
			}
		}

		private void ImportParticle(string tag, Matrix matrix, ObjectParticle particle)
		{
			level.particles.Add(new ObjectParticle
			{
				ParticleClass = particle.ParticleClass,
				Tag = tag + "_" + particle.Tag,
				Matrix = particle.Matrix * matrix,
				DecalScale = particle.DecalScale,
				Flags = particle.Flags
			});
		}

		private void ReadPhysics(XmlReader xml, string basePath)
		{
			if (!xml.SkipEmpty())
			{
				xml.ReadStartElement("Physics");
				while (xml.IsStartElement())
				{
					ReadObjectSetup(xml, basePath);
				}
				xml.ReadEndElement();
			}
		}

		private void ReadObjectSetup(XmlReader xml, string basePath)
		{
			int scriptId = -1;
			string attribute = xml.GetAttribute("Name");
			Vector3 position = Vector3.Zero;
			Quaternion orientation = Quaternion.Identity;
			float scale = 1f;
			ObjectSetupFlags flags = ObjectSetupFlags.None;
			ObjectPhysicsType physicsType = ObjectPhysicsType.None;
			List<ObjectParticle> list = new List<ObjectParticle>();
			List<ObjectNode> list2 = new List<ObjectNode>();
			string text = null;
			string text2 = null;
			xml.ReadStartElement("Object");
			while (xml.IsStartElement())
			{
				switch (xml.LocalName)
				{
				case "Name":
					attribute = xml.ReadElementContentAsString();
					break;
				case "ScriptId":
					scriptId = xml.ReadElementContentAsInt();
					break;
				case "Flags":
					flags = xml.ReadElementContentAsEnum<ObjectSetupFlags>() & ~ObjectSetupFlags.InUse;
					break;
				case "Position":
					position = xml.ReadElementContentAsVector3();
					break;
				case "Rotation":
					orientation = xml.ReadElementContentAsEulerXYZ();
					break;
				case "Scale":
					scale = xml.ReadElementContentAsFloat();
					break;
				case "Physics":
					physicsType = xml.ReadElementContentAsEnum<ObjectPhysicsType>();
					break;
				case "Particles":
					list.AddRange(ReadParticles(xml, basePath));
					break;
				case "Geometry":
					text = xml.ReadElementContentAsString();
					if (list2.Count > 0)
					{
						error.WriteLine("Geometry cannot be used together with Import, ignoring");
					}
					break;
				case "Animation":
					text2 = xml.ReadElementContentAsString();
					if (list2.Count > 0)
					{
						error.WriteLine("Animation cannot be used together with Import, ignoring");
					}
					break;
				case "Import":
					if (text != null || text2 != null)
					{
						error.WriteLine("Import cannot be used together with Geometry and Animation, ignoring");
					}
					else
					{
						list2.AddRange(ImportObjectGeometry(xml, basePath));
					}
					break;
				default:
					error.WriteLine("Unknown physics object element {0}", xml.LocalName);
					xml.Skip();
					break;
				}
			}
			xml.ReadEndElement();
			if (text != null)
			{
				InstanceDescriptor instanceDescriptor = FindSharedInstance(TemplateTag.M3GM, text, objectLoadContext);
				Oni.Motoko.Geometry geometry = GeometryDatReader.Read(instanceDescriptor);
				ObjectAnimation[] animations = new ObjectAnimation[0];
				if (text2 != null)
				{
					InstanceDescriptor oban = FindSharedInstance(TemplateTag.OBAN, text2, objectLoadContext);
					animations = new ObjectAnimation[1] { ObjectDatReader.ReadAnimation(oban) };
				}
				list2.Add(new ObjectNode(new ObjectGeometry[1]
				{
					new ObjectGeometry(geometry)
				})
				{
					FileName = Path.GetFileName(text),
					Name = instanceDescriptor.Name,
					ScriptId = scriptId,
					Flags = flags,
					Animations = animations
				});
			}
			for (int i = 0; i < list2.Count; i++)
			{
				ObjectNode objectNode = list2[i];
				string sourceFilePath = objectNode.SourceFilePath ?? inputFilePath;
				ObjectSetup objectSetup = new ObjectSetup
				{
					Name = objectNode.Name,
					FileName = objectNode.FileName,
					ScriptId = scriptId++,
					Flags = flags,
					PhysicsType = physicsType
				};
				objectSetup.Particles.AddRange(list);
				object[] geometries = (from n in objectNode.Geometries
					where (n.Flags & GunkFlags.Invisible) == 0
					select n.Geometry.Name).ToArray();
				objectSetup.Geometries = geometries;
				foreach (ObjectGeometry item in objectNode.Geometries.Where((ObjectGeometry g) => (g.Flags & GunkFlags.Invisible) == 0))
				{
					DatWriter datWriter = new DatWriter();
					GeometryDatWriter.Write(item.Geometry, datWriter.ImporterFile);
					datWriter.Write(outputDirPath, sourceFilePath);
				}
				objectSetup.Position = position;
				objectSetup.Orientation = orientation;
				objectSetup.Scale = scale;
				objectSetup.Origin = Matrix.CreateFromQuaternion(objectSetup.Orientation) * Matrix.CreateScale(objectSetup.Scale) * Matrix.CreateTranslation(objectSetup.Position);
				ObjectAnimation[] animations2 = objectNode.Animations;
				foreach (ObjectAnimation objectAnimation in animations2)
				{
					if (list2.Count > 1)
					{
						objectAnimation.Name += i.ToString("d2", CultureInfo.InvariantCulture);
					}
					if ((objectAnimation.Flags & ObjectAnimationFlags.Local) != ObjectAnimationFlags.None)
					{
						ObjectAnimationKey[] keys = objectAnimation.Keys;
						foreach (ObjectAnimationKey objectAnimationKey in keys)
						{
							objectAnimationKey.Rotation = objectSetup.Orientation * objectAnimationKey.Rotation;
							objectAnimationKey.Translation += objectSetup.Position;
						}
					}
					if ((objectAnimation.Flags & ObjectAnimationFlags.AutoStart) != ObjectAnimationFlags.None)
					{
						objectSetup.Animation = objectAnimation;
						objectSetup.PhysicsType = ObjectPhysicsType.Animated;
					}
					DatWriter datWriter2 = new DatWriter();
					ObjectDatWriter.WriteAnimation(objectAnimation, datWriter2);
					datWriter2.Write(outputDirPath, sourceFilePath);
				}
				if (objectSetup.Animation == null && objectNode.Animations.Length != 0)
				{
					objectSetup.Animation = objectNode.Animations[0];
				}
				if (objectSetup.Animation != null)
				{
					ObjectAnimationKey objectAnimationKey2 = objectSetup.Animation.Keys[0];
					objectSetup.Scale = objectAnimationKey2.Scale.X;
					objectSetup.Orientation = objectAnimationKey2.Rotation;
					objectSetup.Position = objectAnimationKey2.Translation;
				}
				level.physics.Add(objectSetup);
			}
		}

		private IEnumerable<ObjectNode> ImportObjectGeometry(XmlReader xml, string basePath)
		{
			string attribute = xml.GetAttribute("Path");
			if (attribute == null)
			{
				attribute = xml.GetAttribute("Url");
			}
			string filePath = Path.GetFullPath(Path.Combine(basePath, attribute));
			Scene scene = LoadScene(filePath);
			List<ObjectAnimationClip> list = new List<ObjectAnimationClip>();
			if (!xml.SkipEmpty())
			{
				xml.ReadStartElement();
				while (xml.IsStartElement())
				{
					string localName = xml.LocalName;
					if (localName != null && localName == "Animation")
					{
						list.Add(ReadAnimationClip(xml));
						continue;
					}
					error.WriteLine("Unknown element {0}", xml.LocalName);
					xml.Skip();
				}
				xml.ReadEndElement();
			}
			ObjectDaeImporter objectDaeImporter = new ObjectDaeImporter(textureImporter, null);
			objectDaeImporter.Import(scene);
			foreach (ObjectNode node in objectDaeImporter.Nodes)
			{
				node.SourceFilePath = filePath;
			}
			return objectDaeImporter.Nodes;
		}

		private void ReadSky(XmlReader xml, string basePath)
		{
			level.skyName = xml.ReadElementContentAsString("Sky", "");
			if (string.IsNullOrEmpty(level.skyName))
			{
				error.WriteLine("Warning: The <Sky> field in {0} is empty; using ONSKnight.", inputFilePath);
				level.skyName = "ONSKnight";
			}
		}

		private void ReadTextures(XmlReader xml, string basePath)
		{
			if (xml.SkipEmpty())
			{
				return;
			}
			string attribute = xml.GetAttribute("Format");
			string attribute2 = xml.GetAttribute("AlphaFormat");
			string attribute3 = xml.GetAttribute("MaxSize");
			if (attribute != null)
			{
				defaultTextureFormat = TextureImporter.ParseTextureFormat(attribute);
			}
			if (attribute2 != null)
			{
				defaultAlphaTextureFormat = TextureImporter.ParseTextureFormat(attribute2);
			}
			if (attribute3 != null)
			{
				maxTextureSize = int.Parse(attribute3);
			}
			xml.ReadStartElement("Textures");
			while (xml.IsStartElement())
			{
				if (xml.LocalName == "Import")
				{
					string text = Path.Combine(basePath, xml.ReadElementContentAsString());
					XmlReaderSettings settings = new XmlReaderSettings
					{
						IgnoreWhitespace = true,
						IgnoreProcessingInstructions = true,
						IgnoreComments = true
					};
					using (XmlReader xmlReader = XmlReader.Create(text, settings))
					{
						xmlReader.ReadStartElement("Oni");
						ReadTextures(xmlReader, Path.GetDirectoryName(text));
						xmlReader.ReadEndElement();
					}
				}
				else
				{
					textureImporter.ReadOptions(xml, basePath);
				}
			}
			xml.ReadEndElement();
		}

		public LevelImporter()
		{
			info = System.Console.Out;
			error = System.Console.Error;
		}

		public override void Import(string filePath, string outputDirPath)
		{
			this.outputDirPath = outputDirPath;
			inputFilePath = Path.GetFullPath(filePath);
			textureImporter = new TextureImporter3(outputDirPath);
			Read(inputFilePath);
			WriteLevel();
			WriteObjects();
		}

		private void Read(string filePath)
		{
			level = new LevelDatWriter.DatLevel();
			level.name = Path.GetFileNameWithoutExtension(filePath);
			string directoryName = Path.GetDirectoryName(filePath);
			XmlReaderSettings settings = new XmlReaderSettings
			{
				IgnoreWhitespace = true,
				IgnoreProcessingInstructions = true,
				IgnoreComments = true
			};
			using (XmlReader xmlReader = XmlReader.Create(filePath, settings))
			{
				xmlReader.ReadStartElement("Oni");
				ReadLevel(xmlReader, directoryName);
				xmlReader.ReadEndElement();
			}
			ImportModel(directoryName);
		}

		private void ReadLevel(XmlReader xml, string basePath)
		{
			string attribute = xml.GetAttribute("SharedPath");
			if (!string.IsNullOrEmpty(attribute))
			{
				sharedPath = Path.GetFullPath(Path.Combine(basePath, attribute));
			}
			else
			{
				sharedPath = Path.GetFullPath(Path.Combine(basePath, "classes"));
			}
			sharedManager = new InstanceFileManager();
			sharedManager.AddSearchPath(sharedPath);
			string attribute2 = xml.GetAttribute("Name");
			if (!string.IsNullOrEmpty(attribute2))
			{
				level.name = attribute2;
			}
			xml.ReadStartElement("Level");
			ReadModel(xml, basePath);
			ReadSky(xml, basePath);
			ReadObjects(xml, basePath);
			ReadFilms(xml, basePath);
			ReadCameras(xml, basePath);
			xml.ReadEndElement();
		}

		private void WriteLevel()
		{
			BeginImport();
			LevelDatWriter.Write(this, level);
			Write(outputDirPath, inputFilePath);
			textureImporter.Write();
		}

		private Scene LoadScene(string filePath)
		{
			if (sceneCache == null)
			{
				sceneCache = new Dictionary<string, Scene>(StringComparer.OrdinalIgnoreCase);
			}
			filePath = Path.GetFullPath(filePath);
			Scene value;
			if (!sceneCache.TryGetValue(filePath, out value))
			{
				value = Reader.ReadFile(filePath);
				sceneCache.Add(filePath, value);
			}
			return value;
		}

		private InstanceDescriptor FindSharedInstance(TemplateTag tag, string name)
		{
			if (sharedCache == null)
			{
				sharedCache = new Dictionary<string, InstanceDescriptor>(StringComparer.Ordinal);
			}
			string text = tag.ToString() + name;
			InstanceDescriptor value;
			if (!sharedCache.TryGetValue(text, out value))
			{
				InstanceFile instanceFile = sharedManager.FindInstance(text);
				if (instanceFile == null)
				{
					error.WriteLine("Could not find {0} instance {1}", tag, name);
				}
				else if (instanceFile.Descriptors[0].Template.Tag != tag)
				{
					error.WriteLine("Found '{0}' but its type {1} doesn't match the expected type {2}", name, instanceFile.Descriptors[0].Template.Tag, tag);
				}
				else
				{
					value = instanceFile.Descriptors[0];
				}
				sharedCache.Add(text, value);
			}
			return value;
		}
	}
}
