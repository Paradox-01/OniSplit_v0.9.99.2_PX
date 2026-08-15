using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Oni.Imaging;
using Oni.Metadata;
using Oni.Motoko;
using Oni.Sound;
using Oni.Totoro;

namespace Oni.Xml
{
	internal class XmlImporter : Importer
	{
		protected struct RawArray
		{
			private int offset;

			private int count;

			public int Offset
			{
				get
				{
					return offset;
				}
			}

			public int Count
			{
				get
				{
					return count;
				}
			}

			public RawArray(int offset, int count)
			{
				this.offset = offset;
				this.count = count;
			}
		}

		private class XmlToBinaryVisitor : IMetaTypeVisitor
		{
			private readonly XmlImporter importer;

			private readonly XmlReader xml;

			private readonly BinaryWriter writer;

			public XmlToBinaryVisitor(XmlImporter importer, XmlReader xml, BinaryWriter writer)
			{
				this.importer = importer;
				this.xml = xml;
				this.writer = writer;
			}

			void IMetaTypeVisitor.VisitEnum(MetaEnum type)
			{
				type.XmlToBinary(xml, writer);
			}

			void IMetaTypeVisitor.VisitByte(MetaByte type)
			{
				writer.Write(XmlConvert.ToByte(xml.ReadElementContentAsString()));
			}

			void IMetaTypeVisitor.VisitInt16(MetaInt16 type)
			{
				writer.Write(XmlConvert.ToInt16(xml.ReadElementContentAsString()));
			}

			void IMetaTypeVisitor.VisitUInt16(MetaUInt16 type)
			{
				writer.Write(XmlConvert.ToUInt16(xml.ReadElementContentAsString()));
			}

			void IMetaTypeVisitor.VisitInt32(MetaInt32 type)
			{
				writer.Write(xml.ReadElementContentAsInt());
			}

			void IMetaTypeVisitor.VisitUInt32(MetaUInt32 type)
			{
				writer.Write(XmlConvert.ToUInt32(xml.ReadElementContentAsString()));
			}

			void IMetaTypeVisitor.VisitInt64(MetaInt64 type)
			{
				writer.Write(xml.ReadElementContentAsLong());
			}

			void IMetaTypeVisitor.VisitUInt64(MetaUInt64 type)
			{
				writer.Write(XmlConvert.ToUInt64(xml.ReadElementContentAsString()));
			}

			void IMetaTypeVisitor.VisitFloat(MetaFloat type)
			{
				writer.Write(xml.ReadElementContentAsFloat());
			}

			void IMetaTypeVisitor.VisitColor(MetaColor type)
			{
				byte[] array = xml.ReadElementContentAsArray(byteConverter);
				if (array.Length > 3)
				{
					writer.Write(new Color(array[0], array[1], array[2], array[3]));
				}
				else
				{
					writer.Write(new Color(array[0], array[1], array[2]));
				}
			}

			void IMetaTypeVisitor.VisitVector2(MetaVector2 type)
			{
				writer.Write(xml.ReadElementContentAsVector2());
			}

			void IMetaTypeVisitor.VisitVector3(MetaVector3 type)
			{
				writer.Write(xml.ReadElementContentAsVector3());
			}

			void IMetaTypeVisitor.VisitMatrix4x3(MetaMatrix4x3 type)
			{
				writer.Write(xml.ReadElementContentAsArray(floatConverter, 12));
			}

			void IMetaTypeVisitor.VisitPlane(MetaPlane type)
			{
				writer.Write(xml.ReadElementContentAsArray(floatConverter, 4));
			}

			void IMetaTypeVisitor.VisitQuaternion(MetaQuaternion type)
			{
				writer.Write(xml.ReadElementContentAsArray(floatConverter, 4));
			}

			void IMetaTypeVisitor.VisitBoundingSphere(MetaBoundingSphere type)
			{
				ReadFields(type.Fields);
			}

			void IMetaTypeVisitor.VisitBoundingBox(MetaBoundingBox type)
			{
				ReadFields(type.Fields);
			}

			void IMetaTypeVisitor.VisitRawOffset(MetaRawOffset type)
			{
				throw new NotImplementedException();
			}

			void IMetaTypeVisitor.VisitSepOffset(MetaSepOffset type)
			{
				throw new NotImplementedException();
			}

			void IMetaTypeVisitor.VisitString(MetaString type)
			{
				writer.Write(xml.ReadElementContentAsString(), type.Count);
			}

			void IMetaTypeVisitor.VisitPadding(MetaPadding type)
			{
				writer.Write(type.FillByte, type.Count);
			}

			void IMetaTypeVisitor.VisitPointer(MetaPointer type)
			{
				string text = xml.ReadElementContentAsString();
				if (text != null)
				{
					text = text.Trim();
				}
				if (string.IsNullOrEmpty(text))
				{
					writer.Write(0);
				}
				else
				{
					writer.Write(importer.ResolveReference(text, type.Tag));
				}
			}

			void IMetaTypeVisitor.VisitStruct(MetaStruct type)
			{
				ReadFields(type.Fields);
			}

			void IMetaTypeVisitor.VisitArray(MetaArray type)
			{
				int num = ReadArray(type.ElementType, type.Count);
				if (num < type.Count)
				{
					writer.Skip((type.Count - num) * type.ElementType.Size);
				}
			}

			void IMetaTypeVisitor.VisitVarArray(MetaVarArray type)
			{
				int position = writer.Position;
				int value;
				if (type.CountField.Type == MetaType.Int16)
				{
					writer.WriteInt16(0);
					value = ReadArray(type.ElementType, 65535);
				}
				else
				{
					writer.Write(0);
					value = ReadArray(type.ElementType, int.MaxValue);
				}
				int position2 = writer.Position;
				writer.Position = position;
				if (type.CountField.Type == MetaType.Int16)
				{
					writer.WriteUInt16(value);
				}
				else
				{
					writer.Write(value);
				}
				writer.Position = position2;
			}

			private void ReadFields(IEnumerable<Field> fields)
			{
				xml.ReadStartElement();
				xml.MoveToContent();
				foreach (Field field in fields)
				{
					try
					{
						field.Type.Accept(this);
					}
					catch (Exception innerException)
					{
						throw new InvalidOperationException(string.Format("Cannot read field '{0}'", field.Name), innerException);
					}
				}
				xml.ReadEndElement();
			}

			protected void ReadStruct(MetaStruct s)
			{
				foreach (Field field in s.Fields)
				{
					try
					{
						field.Type.Accept(this);
					}
					catch (Exception innerException)
					{
						throw new InvalidOperationException(string.Format("Cannot read field '{0}'", field.Name), innerException);
					}
				}
			}

			private int ReadArray(MetaType elementType, int maxCount)
			{
				if (xml.IsEmptyElement)
				{
					xml.Read();
					return 0;
				}
				xml.ReadStartElement();
				xml.MoveToContent();
				string localName = xml.LocalName;
				int i;
				for (i = 0; i < maxCount; i++)
				{
					if (!xml.IsStartElement(localName))
					{
						break;
					}
					elementType.Accept(this);
				}
				xml.ReadEndElement();
				return i;
			}

			protected int ReadRawElement(string name, MetaType elementType)
			{
				if (!xml.IsStartElement(name))
				{
					return 0;
				}
				if (xml.IsEmptyElement)
				{
					xml.ReadStartElement();
					return 0;
				}
				int result = importer.RawWriter.Align32();
				elementType.Accept(new RawXmlImporter(xml, importer.RawWriter));
				return result;
			}

			protected RawArray ReadRawArray(string name, MetaType elementType)
			{
				if (!xml.IsStartElement(name))
				{
					return default(RawArray);
				}
				if (xml.IsEmptyElement)
				{
					xml.ReadStartElement();
					return default(RawArray);
				}
				xml.ReadStartElement();
				int offset = importer.RawWriter.Align32();
				RawXmlImporter visitor = new RawXmlImporter(xml, importer.RawWriter);
				int num = 0;
				while (xml.IsStartElement(elementType.Name))
				{
					elementType.Accept(visitor);
					num++;
				}
				xml.ReadEndElement();
				return new RawArray(offset, num);
			}
		}

		private static readonly Func<string, float> floatConverter = XmlConvert.ToSingle;

		private static readonly Func<string, byte> byteConverter = XmlConvert.ToByte;

		protected XmlReader xml;

		private readonly string[] args;

		private string baseDir;

		private string filePath;

		private bool firstInstance;

		private Dictionary<string, ImporterDescriptor> localRefs;

		private Dictionary<string, ImporterDescriptor> externalRefs;

		private ImporterDescriptor currentDescriptor;

		private BinaryWriter currentWriter;

		public XmlImporter(string[] args)
		{
			this.args = args;
		}

		public override void Import(string filePath, string outputDirPath)
		{
			this.filePath = filePath;
			BeginImport();
			using (xml = CreateXmlReader(filePath))
			{
				while (xml.IsStartElement())
				{
					switch (xml.LocalName)
					{
					case "Objects":
						ReadObjects();
						break;
					case "Texture":
						ReadTexture();
						break;
					case "ImpactEffects":
						ReadImpactEffects();
						break;
					case "SoundAnimation":
						ReadSoundAnimation();
						break;
					case "TextureMaterials":
						ReadTextureMaterials();
						break;
					case "Particle":
						ReadParticle();
						break;
					case "AmbientSound":
					case "ImpulseSound":
					case "SoundGroup":
						ReadSoundData();
						break;
					case "Animation":
						ReadAnimation(args);
						break;
					default:
						ReadInstance();
						break;
					}
				}
			}
			Write(outputDirPath, filePath);
		}

		public override void BeginImport()
		{
			base.BeginImport();
			baseDir = Path.GetDirectoryName(filePath);
			localRefs = new Dictionary<string, ImporterDescriptor>(StringComparer.Ordinal);
			externalRefs = new Dictionary<string, ImporterDescriptor>(StringComparer.Ordinal);
			firstInstance = true;
		}

		private static XmlReader CreateXmlReader(string filePath)
		{
			XmlReaderSettings settings = new XmlReaderSettings
			{
				CloseInput = true,
				IgnoreWhitespace = true,
				IgnoreProcessingInstructions = true,
				IgnoreComments = true
			};
			XmlReader xmlReader = XmlReader.Create(filePath, settings);
			try
			{
				if (!xmlReader.Read())
				{
					throw new InvalidDataException("Not an Oni XML file");
				}
				xmlReader.MoveToContent();
				if (!xmlReader.IsStartElement("Oni"))
				{
					throw new InvalidDataException("Not an Oni XML file");
				}
				if (xmlReader.IsEmptyElement)
				{
					throw new InvalidDataException("No instances found");
				}
				xmlReader.ReadStartElement();
				xmlReader.MoveToContent();
				return xmlReader;
			}
			catch
			{
				xmlReader.Close();
				throw;
			}
		}

		private void ReadInstance()
		{
			string attribute = xml.GetAttribute("id");
			string text = xml.GetAttribute("type");
			if (text == null)
			{
				text = xml.LocalName;
			}
			TemplateTag tag = (TemplateTag)Enum.Parse(typeof(TemplateTag), text);
			InstanceMetadata metadata = InstanceMetadata.GetMetadata(1052091763926815L);
			Template template = metadata.GetTemplate(tag);
			string text2 = null;
			if (firstInstance)
			{
				text2 = Path.GetFileNameWithoutExtension(filePath);
				if (!text2.StartsWith(text, StringComparison.Ordinal))
				{
					text2 = text + text2;
				}
				firstInstance = false;
			}
			BinaryWriter writer = BeginXmlInstance(tag, text2, attribute);
			template.Type.Accept(new XmlToBinaryVisitor(this, xml, writer));
			EndXmlInstance();
		}

		private void ReadAnimation(string[] args)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
			BinaryWriter dat = BeginXmlInstance(TemplateTag.TRAM, fileNameWithoutExtension, "0");
			Animation animation = AnimationXmlReader.Read(xml, Path.GetDirectoryName(filePath));
			AnimationDatWriter.Write(animation, this, dat);
			EndXmlInstance();
		}

		private void ReadParticle()
		{
			string text = Path.GetFileNameWithoutExtension(filePath);
			if (!text.StartsWith("BINA3RAP", StringComparison.Ordinal))
			{
				text = "BINA3RAP" + text;
			}
			xml.ReadStartElement();
			int num = base.RawWriter.Align32();
			base.RawWriter.Write(1346458163);
			base.RawWriter.Write(0);
			ParticleXmlImporter.Import(xml, base.RawWriter);
			int num2 = base.RawWriter.Position - num;
			base.RawWriter.WriteAt(num + 4, num2 - 8);
			BinaryWriter binaryWriter = BeginXmlInstance(TemplateTag.BINA, text, "0");
			binaryWriter.Write(num2);
			binaryWriter.Write(num);
			EndXmlInstance();
			xml.ReadEndElement();
		}

		private void ReadSoundData()
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
			int num = base.RawWriter.Align32();
			OsbdXmlImporter osbdXmlImporter = new OsbdXmlImporter(xml, base.RawWriter);
			osbdXmlImporter.Import();
			int num2 = base.RawWriter.Position - num;
			base.RawWriter.WriteAt(num + 4, num2 - 8);
			BinaryWriter binaryWriter = BeginXmlInstance(TemplateTag.OSBD, fileNameWithoutExtension, "0");
			binaryWriter.Write(num2);
			binaryWriter.Write(num);
			EndXmlInstance();
			xml.ReadEndElement();
		}

		private void ReadObjects()
		{
			string text = Path.GetFileNameWithoutExtension(filePath);
			if (!text.StartsWith("BINACJBO", StringComparison.Ordinal))
			{
				text = "BINACJBO" + text;
			}
			xml.ReadStartElement();
			int num = base.RawWriter.Align32();
			base.RawWriter.Write(1329744451);
			base.RawWriter.Write(0);
			ObjcXmlImporter.Import(xml, base.RawWriter);
			int num2 = base.RawWriter.Position - num;
			base.RawWriter.WriteAt(num + 4, num2 - 8);
			BinaryWriter binaryWriter = BeginXmlInstance(TemplateTag.BINA, text, "0");
			binaryWriter.Write(num2);
			binaryWriter.Write(num);
			EndXmlInstance();
			xml.ReadEndElement();
		}

		private void ReadTextureMaterials()
		{
			string text = Path.GetFileNameWithoutExtension(filePath);
			if (!text.StartsWith("BINADBMT", StringComparison.Ordinal))
			{
				text = "BINADBMT" + text;
			}
			xml.ReadStartElement();
			int num = base.RawWriter.Align32();
			base.RawWriter.Write(1414349380);
			base.RawWriter.Write(0);
			TmbdXmlImporter.Import(xml, base.RawWriter);
			int num2 = base.RawWriter.Position - num;
			base.RawWriter.WriteAt(num + 4, num2 - 8);
			BinaryWriter binaryWriter = BeginXmlInstance(TemplateTag.BINA, text, "0");
			binaryWriter.Write(num2);
			binaryWriter.Write(num);
			EndXmlInstance();
			xml.ReadEndElement();
		}

		private void ReadImpactEffects()
		{
			string text = Path.GetFileNameWithoutExtension(filePath);
			if (!text.StartsWith("BINAEINO", StringComparison.Ordinal))
			{
				text = "BINAEINO" + text;
			}
			xml.ReadStartElement();
			int num = base.RawWriter.Align32();
			base.RawWriter.Write(1330530629);
			base.RawWriter.Write(0);
			OnieXmlImporter.Import(xml, base.RawWriter);
			int num2 = base.RawWriter.Position - num;
			base.RawWriter.WriteAt(num + 4, num2 - 8);
			BinaryWriter binaryWriter = BeginXmlInstance(TemplateTag.BINA, text, "0");
			binaryWriter.Write(num2);
			binaryWriter.Write(num);
			EndXmlInstance();
			xml.ReadEndElement();
		}

		private void ReadSoundAnimation()
		{
			string text = Path.GetFileNameWithoutExtension(filePath);
			if (!text.StartsWith("BINADBAS", StringComparison.Ordinal))
			{
				text = "BINADBAS" + text;
			}
			int num = base.RawWriter.Align32();
			base.RawWriter.Write(1396785732);
			base.RawWriter.Write(0);
			SabdXmlImporter.Import(xml, base.RawWriter);
			int num2 = base.RawWriter.Position - num;
			base.RawWriter.WriteAt(num + 4, num2 - 8);
			BinaryWriter binaryWriter = BeginXmlInstance(TemplateTag.BINA, text, "0");
			binaryWriter.Write(num2);
			binaryWriter.Write(num);
			EndXmlInstance();
		}

		private void ReadTexture()
		{
			TextureXmlImporter textureXmlImporter = new TextureXmlImporter(this, xml, filePath);
			textureXmlImporter.Import();
		}

		public BinaryWriter BeginXmlInstance(TemplateTag tag, string name, string xmlid)
		{
			if (!localRefs.TryGetValue(xmlid, out currentDescriptor))
			{
				currentDescriptor = base.ImporterFile.CreateInstance(tag, name);
				localRefs.Add(xmlid, currentDescriptor);
			}
			else if (currentDescriptor.Tag != tag)
			{
				throw new InvalidDataException(string.Format("{0} was expected to be of type {1} but it's type is {2}", xmlid, tag, currentDescriptor.Tag));
			}
			currentWriter = currentDescriptor.OpenWrite();
			return currentWriter;
		}

		public void EndXmlInstance()
		{
			currentWriter.Dispose();
		}

		private ImporterDescriptor ResolveReference(string xmlid, TemplateTag tag)
		{
			if (xmlid[0] == '#')
			{
				return ResolveLocalReference(xmlid.Substring(1), tag);
			}
			return ResolveExternalReference(xmlid, tag);
		}

		private ImporterDescriptor ResolveLocalReference(string xmlid, TemplateTag tag)
		{
			ImporterDescriptor value;
			if (!localRefs.TryGetValue(xmlid, out value))
			{
				value = base.ImporterFile.CreateInstance(tag);
				localRefs.Add(xmlid, value);
			}
			else if (tag != TemplateTag.NONE && tag != value.Tag)
			{
				throw new InvalidDataException(string.Format("{0} was expected to be of type {1} but it's type is {2}", xmlid, tag, value.Tag));
			}
			return value;
		}

		private ImporterDescriptor ResolveExternalReference(string xmlid, TemplateTag tag)
		{
			ImporterDescriptor value;
			if (!externalRefs.TryGetValue(xmlid, out value))
			{
				if (xmlid.EndsWith(".xml", StringComparison.Ordinal) || xmlid.EndsWith(".dae", StringComparison.Ordinal) || xmlid.EndsWith(".obj", StringComparison.Ordinal) || xmlid.EndsWith(".tga", StringComparison.Ordinal))
				{
					string text = Path.Combine(baseDir, xmlid);
					if (!File.Exists(text))
					{
						throw new InvalidDataException(string.Format("Cannot find referenced file '{0}'", text));
					}
					if (tag == TemplateTag.TRCM)
					{
						BodyDaeImporter bodyDaeImporter = new BodyDaeImporter(args);
						value = bodyDaeImporter.Import(text, base.ImporterFile);
					}
					else if (tag == TemplateTag.M3GM && (currentDescriptor.Tag == TemplateTag.ONWC || currentDescriptor.Tag == TemplateTag.CONS || currentDescriptor.Tag == TemplateTag.DOOR || currentDescriptor.Tag == TemplateTag.OFGA))
					{
						GeometryImporter geometryImporter = new GeometryImporter(args);
						value = geometryImporter.Import(text, base.ImporterFile);
					}
					else
					{
						AddDependency(text, tag);
						string name = Importer.DecodeFileName(Path.GetFileNameWithoutExtension(text));
						value = base.ImporterFile.CreateInstance(tag, name);
					}
				}
				else
				{
					if (tag != TemplateTag.NONE)
					{
						string text2 = tag.ToString();
						if (!xmlid.StartsWith(text2, StringComparison.Ordinal))
						{
							xmlid = text2 + xmlid;
						}
					}
					else
					{
						string value2 = xmlid.Substring(0, 4);
						tag = (TemplateTag)Enum.Parse(typeof(TemplateTag), value2);
					}
					value = base.ImporterFile.CreateInstance(tag, xmlid);
				}
				externalRefs.Add(xmlid, value);
			}
			return value;
		}
	}
}
