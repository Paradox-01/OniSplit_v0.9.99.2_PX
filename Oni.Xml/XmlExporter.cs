using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using Oni.Collections;
using Oni.Dae;
using Oni.Dae.IO;
using Oni.Game;
using Oni.Metadata;
using Oni.Motoko;
using Oni.Sound;
using Oni.Totoro;

namespace Oni.Xml
{
	internal sealed class XmlExporter : Exporter
	{
		private bool noAnimation;

		private bool recursive;

		private Body animBody;

		private bool mergeAnimations;

		private Node animBodyNode;

		private Animation mergedAnim;

		private string animDaeFileName;

		private bool allTextures;

		private InstanceDescriptor[] animTextures;

		private TextureDaeWriter animTextureWriter;

		private readonly Dictionary<InstanceDescriptor, string> externalChildren = new Dictionary<InstanceDescriptor, string>();

		private readonly Set<InstanceDescriptor> queued = new Set<InstanceDescriptor>();

		private readonly Queue<InstanceDescriptor> exportQueue = new Queue<InstanceDescriptor>();

		private InstanceDescriptor mainDescriptor;

		private string baseFileName;

		private XmlWriter xml;

		public bool NoAnimation
		{
			get
			{
				return noAnimation;
			}
			set
			{
				noAnimation = value;
			}
		}

		public bool Recursive
		{
			get
			{
				return recursive;
			}
			set
			{
				recursive = value;
			}
		}

		public Body AnimationBody
		{
			get
			{
				return animBody;
			}
			set
			{
				animBody = value;
				animBodyNode = null;
			}
		}

		public bool MergeAnimations
		{
			get
			{
				return mergeAnimations;
			}
			set
			{
				mergeAnimations = value;
			}
		}

		public bool AllTextures
		{
			get { return allTextures; }
			set { allTextures = value; }
		}

		public InstanceDescriptor[] AnimationTextures
		{
			get { return animTextures; }
			set { animTextures = value; }
		}

		public XmlExporter(InstanceFileManager fileManager, string outputDirPath)
			: base(fileManager, outputDirPath)
		{
		}

		protected override void ExportInstance(InstanceDescriptor descriptor)
		{
			exportQueue.Enqueue(descriptor);
			mainDescriptor = descriptor;
			string text = (baseFileName = CreateFileName(descriptor, ".xml"));
			baseFileName = Path.GetFileNameWithoutExtension(text);
			if (recursive && animBody == null && descriptor.Template.Tag == TemplateTag.ONCC)
			{
				CharacterClass characterClass = CharacterClass.Read(descriptor);
				animBody = BodyDatReader.Read(characterClass.Body);
				animTextures = characterClass.Textures;
			}
			using (xml = CreateXmlWriter(text))
			{
				ExportDescriptors(xml);
			}
		}

		private void ExportChild(InstanceDescriptor descriptor)
		{
			if (descriptor.Template.Tag == TemplateTag.TRCM && mainDescriptor.Template.Tag == TemplateTag.TRBS)
			{
				xml.WriteValue(WriteBody(descriptor));
				return;
			}
			if (descriptor.Template.Tag == TemplateTag.M3GM)
			{
				if (!descriptor.IsPlaceholder)
				{
					xml.WriteValue(WriteGeometry(descriptor));
					return;
				}
				if (recursive)
				{
					InstanceFile instanceFile = base.InstanceFileManager.FindInstance(descriptor.FullName, descriptor.File);
					if (instanceFile != null && instanceFile.Descriptors[0].Template.Tag == TemplateTag.M3GM && instanceFile.Descriptors[0].Name == descriptor.Name)
					{
						xml.WriteValue(WriteGeometry(instanceFile.Descriptors[0]));
						return;
					}
				}
			}
			if (!recursive || !descriptor.HasName)
			{
				if (descriptor.HasName)
				{
					xml.WriteValue(descriptor.FullName);
					return;
				}
				xml.WriteValue(string.Format(CultureInfo.InvariantCulture, "#{0}", new object[1] { descriptor.Index }));
				if (queued.Add(descriptor))
				{
					exportQueue.Enqueue(descriptor);
				}
				return;
			}
			InstanceFile instanceFile2 = base.InstanceFileManager.FindInstance(descriptor.FullName, descriptor.File);
			if (instanceFile2 == null || instanceFile2 == mainDescriptor.File)
			{
				xml.WriteValue(descriptor.FullName);
				return;
			}
			string value;
			if (!externalChildren.TryGetValue(descriptor, out value))
			{
				XmlExporter xmlExporter = new XmlExporter(base.InstanceFileManager, base.OutputDirPath)
				{
					recursive = recursive,
					animBody = animBody,
					mergeAnimations = mergeAnimations,
					allTextures = allTextures,
					animTextures = animTextures
				};
				xmlExporter.ExportFiles(new string[1] { instanceFile2.FilePath });
				value = Path.GetFileName(CreateFileName(descriptor, ".xml"));
				externalChildren.Add(descriptor, value);
			}
			xml.WriteValue(value);
		}

		private static XmlWriter CreateXmlWriter(string filePath)
		{
			XmlWriterSettings settings = new XmlWriterSettings
			{
				CloseOutput = true,
				Indent = true,
				IndentChars = "    "
			};
			FileStream output = File.Create(filePath);
			XmlWriter xmlWriter = XmlWriter.Create(output, settings);
			try
			{
				xmlWriter.WriteStartElement("Oni");
				return xmlWriter;
			}
			catch
			{
				xmlWriter.Close();
				throw;
			}
		}

		private void ExportDescriptors(XmlWriter writer)
		{
			while (exportQueue.Count > 0)
			{
				InstanceDescriptor instanceDescriptor = exportQueue.Dequeue();
				if (instanceDescriptor.IsPlaceholder || (instanceDescriptor.HasName && instanceDescriptor != mainDescriptor))
				{
					continue;
				}
				switch (instanceDescriptor.Template.Tag)
				{
				case TemplateTag.TRAM:
					WriteAnimation(instanceDescriptor);
					break;
				case TemplateTag.BINA:
					WriteBinaryObject(instanceDescriptor);
					break;
				case TemplateTag.TXMP:
					if (instanceDescriptor.HasName)
					{
						TextureXmlExporter.Export(instanceDescriptor, writer, base.OutputDirPath, baseFileName);
					}
					break;
				case TemplateTag.OSBD:
					WriteBinarySound(instanceDescriptor);
					break;
				default:
					GenericXmlWriter.Write(xml, ExportChild, instanceDescriptor);
					break;
				case TemplateTag.TXAN:
					break;
				}
			}
		}

		private void WriteAnimation(InstanceDescriptor tram)
		{
			Animation animation = AnimationDatReader.Read(tram);
			if (animBody == null)
			{
				AnimationXmlWriter.Write(animation, xml, null, 0, 0);
				return;
			}
			if (animBodyNode == null)
			{
				animTextureWriter = new TextureDaeWriter(base.OutputDirPath, allTextures);
				GeometryDaeWriter geometryWriter = new GeometryDaeWriter(animTextureWriter);
				BodyDaeWriter bodyDaeWriter = new BodyDaeWriter(geometryWriter);
				animBodyNode = bodyDaeWriter.Write(animBody, false, animTextures);
				ReportEnvMaps(baseFileName, animTextureWriter);
			}
			if (mergeAnimations)
			{
				if (mergedAnim == null)
				{
					mergedAnim = new Animation();
					animDaeFileName = tram.FullName + ".dae";
				}
				int count = mergedAnim.Heights.Count;
				AnimationDaeWriter.AppendFrames(mergedAnim, animation);
				int count2 = mergedAnim.Heights.Count;
				AnimationXmlWriter.Write(animation, xml, animDaeFileName, count, count2);
				return;
			}
			string text = tram.FullName + ".dae";
			bool flag = DaeReader.CommandLineArgs.Any((string a) => a == "-blender");
			bool plotEveryFrame = DaeReader.CommandLineArgs.Any((string a) => a == "-dense");
			if (flag)
			{
				Console.WriteLine("AnimationDaeWriter: custom axis conversion.");
			}
			AnimationDaeWriter.Write(animBodyNode, animation, 0, flag, flag, plotEveryFrame);
			Writer.WriteFile(Path.Combine(base.OutputDirPath, text), new Scene
			{
				CustomAxisConversion = flag,
				SceneZUP = flag,
				Nodes = { animBodyNode }
			});
			AnimationXmlWriter.Write(animation, xml, text, 0, 0);
		}

		protected override void Flush()
		{
			if (mergedAnim != null)
			{
				bool flag = DaeReader.CommandLineArgs.Any((string a) => a == "-blender");
				bool plotEveryFrame = DaeReader.CommandLineArgs.Any((string a) => a == "-dense");
				AnimationDaeWriter.Write(animBodyNode, mergedAnim, 0, flag, flag, plotEveryFrame);
				Writer.WriteFile(Path.Combine(base.OutputDirPath, animDaeFileName), new Scene
				{
					CustomAxisConversion = flag,
					SceneZUP = flag,
					Nodes = { animBodyNode }
				});
				mergedAnim = null;
			}
		}

		private void ReportEnvMaps(string outputName, TextureDaeWriter textureWriter)
		{
			if (!allTextures)
			{
				return;
			}
			if (textureWriter.EnvMapCount == 0)
			{
				Console.WriteLine("Env maps found: none ({0})", outputName);
			}
			else
			{
				Console.WriteLine("Env maps found: {0} unique map(s) used by {1} material(s) ({2})", textureWriter.EnvMapCount, textureWriter.EnvMapMaterialCount, outputName);
			}
		}

		private string WriteBody(InstanceDescriptor descriptor)
		{
			string value;
			if (!externalChildren.TryGetValue(descriptor, out value))
			{
				Body body = BodyDatReader.Read(descriptor);
				TextureDaeWriter textureWriter = new TextureDaeWriter(base.OutputDirPath, allTextures);
				GeometryDaeWriter geometryWriter = new GeometryDaeWriter(textureWriter);
				BodyDaeWriter bodyDaeWriter = new BodyDaeWriter(geometryWriter);
				Node item = bodyDaeWriter.Write(body, noAnimation, null);
				value = string.Format("{0}_TRCM{1}.dae", mainDescriptor.FullName, descriptor.Index);
				Writer.WriteFile(Path.Combine(base.OutputDirPath, value), new Scene
				{
					Nodes = { item }
				});
				externalChildren.Add(descriptor, value);
			}
			return value;
		}

		private string WriteGeometry(InstanceDescriptor descriptor)
		{
			string value;
			if (!externalChildren.TryGetValue(descriptor, out value))
			{
				Oni.Motoko.Geometry geometry = GeometryDatReader.Read(descriptor);
				TextureDaeWriter textureWriter = new TextureDaeWriter(base.OutputDirPath, allTextures);
				GeometryDaeWriter geometryDaeWriter = new GeometryDaeWriter(textureWriter);
				value = ((!descriptor.HasName) ? string.Format("{0}_{1}.dae", mainDescriptor.Name, descriptor.Index) : (descriptor.FullName + ".dae"));
				Node item = geometryDaeWriter.WriteNode(geometry, geometry.Name);
				Writer.WriteFile(Path.Combine(base.OutputDirPath, value), new Scene
				{
					Nodes = { item }
				});
				externalChildren.Add(descriptor, value);
			}
			return value;
		}

		private void WriteBinarySound(InstanceDescriptor descriptor)
		{
			int offset;
			using (BinaryReader binaryReader = descriptor.OpenRead())
			{
				int num = binaryReader.ReadInt32();
				offset = binaryReader.ReadInt32();
			}
			using (BinaryReader reader = descriptor.GetRawReader(offset))
			{
				OsbdXmlExporter.Export(reader, xml);
			}
		}

		private void WriteBinaryObject(InstanceDescriptor descriptor)
		{
			int offset;
			using (BinaryReader binaryReader = descriptor.OpenRead())
			{
				int num = binaryReader.ReadInt32();
				offset = binaryReader.ReadInt32();
			}
			using (BinaryReader binaryReader2 = descriptor.GetRawReader(offset))
			{
				BinaryTag binaryTag = (BinaryTag)binaryReader2.ReadInt32();
				switch (binaryTag)
				{
				case BinaryTag.OBJC:
					ObjcXmlExporter.Export(binaryReader2, xml);
					break;
				case BinaryTag.PAR3:
					ParticleXmlExporter.Export(descriptor.FullName.Substring(8), binaryReader2, xml);
					break;
				case BinaryTag.TMBD:
					TmbdXmlExporter.Export(binaryReader2, xml);
					break;
				case BinaryTag.ONIE:
					OnieXmlExporter.Export(binaryReader2, xml);
					break;
				case BinaryTag.SABD:
					SabdXmlExporter.Export(binaryReader2, xml);
					break;
				default:
					throw new NotSupportedException(string.Format("Unsupported BINA type '{0}'", Utils.TagToString((int)binaryTag)));
				}
			}
		}
	}
}
