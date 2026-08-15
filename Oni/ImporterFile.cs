using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Oni
{
	internal sealed class ImporterFile
	{
		private class FileHeader
		{
			public const int Size = 64;

			public long TemplateChecksum;

			public int Version;

			public int InstanceCount;

			public int DataTableOffset;

			public int DataTableSize;

			public int NameTableOffset;

			public int NameTableSize;

			public int RawTableOffset;

			public int RawTableSize;

			public void Write(BinaryWriter writer)
			{
				writer.Write(TemplateChecksum);
				writer.Write(Version);
				writer.Write(2251868534472768L);
				writer.Write(InstanceCount);
				writer.Write(0uL);
				writer.Write(DataTableOffset);
				writer.Write(DataTableSize);
				writer.Write(NameTableOffset);
				writer.Write(NameTableSize);
				writer.Write(RawTableOffset);
				writer.Write(RawTableSize);
				writer.Write(0uL);
			}
		}

		private sealed class ImporterFileDescriptor : ImporterDescriptor
		{
			public const int Size = 20;

			private int nameOffset;

			private int dataOffset;

			private byte[] data;

			public int NameOffset
			{
				get
				{
					return nameOffset;
				}
				set
				{
					nameOffset = value;
				}
			}

			public int DataOffset
			{
				get
				{
					return dataOffset;
				}
				set
				{
					dataOffset = value;
				}
			}

			public int DataSize
			{
				get
				{
					if (data == null)
					{
						return 0;
					}
					return data.Length + 8;
				}
			}

			public byte[] Data
			{
				get
				{
					return data;
				}
			}

			public ImporterFileDescriptor(ImporterFile file, TemplateTag tag, int index, string name)
				: base(file, tag, index, name)
			{
				if (!string.IsNullOrEmpty(name))
				{
					nameOffset = file.nameOffset;
					file.nameOffset += name.Length + 1;
				}
			}

			public override BinaryWriter OpenWrite()
			{
				if (data != null)
				{
					throw new InvalidOperationException("Descriptor has already been written to");
				}
				return new InstanceWriter(this);
			}

			public override BinaryWriter OpenWrite(int offset)
			{
				if (data != null)
				{
					throw new InvalidOperationException("Descriptor has already been written to");
				}
				InstanceWriter instanceWriter = new InstanceWriter(this);
				instanceWriter.Skip(offset);
				return instanceWriter;
			}

			public void Close(byte[] data)
			{
				this.data = data;
			}
		}

		private class InstanceWriter : BinaryWriter
		{
			private readonly ImporterFileDescriptor descriptor;

			public InstanceWriter(ImporterFileDescriptor descriptor)
				: base(new MemoryStream())
			{
				this.descriptor = descriptor;
			}

			protected override void Dispose(bool disposing)
			{
				MemoryStream memoryStream = (MemoryStream)BaseStream;
				if (descriptor.Tag == TemplateTag.TXCA)
				{
					memoryStream.Write(txcaPadding, 0, txcaPadding.Length);
				}
				else if (memoryStream.Position > memoryStream.Length)
				{
					memoryStream.SetLength(memoryStream.Position);
				}
				descriptor.Close(memoryStream.ToArray());
				base.Dispose(disposing);
			}
		}

		private static readonly byte[] txcaPadding = new byte[480];

		private readonly long templateChecksum = 1052091763926815L;

		private MemoryStream rawStream;

		private BinaryWriter rawWriter;

		private List<ImporterFileDescriptor> descriptors;

		private int nameOffset;

		public BinaryWriter RawWriter
		{
			get
			{
				if (rawWriter == null)
				{
					rawStream = new MemoryStream();
					rawWriter = new BinaryWriter(rawStream);
					rawWriter.Write(new byte[32]);
				}
				return rawWriter;
			}
		}

		public ImporterFile()
		{
		}

		public ImporterFile(long templateChecksum)
		{
			this.templateChecksum = templateChecksum;
		}

		public void BeginImport()
		{
			rawStream = null;
			rawWriter = null;
			descriptors = new List<ImporterFileDescriptor>();
			nameOffset = 0;
		}

		public ImporterDescriptor CreateInstance(TemplateTag tag, string name = null)
		{
			ImporterFileDescriptor importerFileDescriptor = new ImporterFileDescriptor(this, tag, descriptors.Count, MakeInstanceName(tag, name));
			descriptors.Add(importerFileDescriptor);
			return importerFileDescriptor;
		}

		public int WriteRawPart(byte[] data)
		{
			int result = RawWriter.Align32();
			RawWriter.Write(data);
			return result;
		}

		public int WriteRawPart(string text)
		{
			return WriteRawPart(Encoding.UTF8.GetBytes(text));
		}

		public void Write(string outputDirPath, string inputFilePath = null)
		{
			string path = Path.Combine(outputDirPath, Importer.EncodeFileName(descriptors[0].Name) + ".oni");
			Directory.CreateDirectory(outputDirPath);
			int num = Utils.Align32(64 + 20 * descriptors.Count);
			int nameTableSize = nameOffset;
			int dataTableOffset = Utils.Align32(num + nameOffset);
			int num2 = 0;
			foreach (ImporterFileDescriptor item in descriptors.Where((ImporterFileDescriptor d) => d.Data != null))
			{
				item.DataOffset = num2 + 8;
				num2 += Utils.Align32(item.DataSize);
			}
			FileHeader fileHeader = new FileHeader
			{
				TemplateChecksum = templateChecksum,
				Version = 1448227634,
				InstanceCount = descriptors.Count,
				DataTableOffset = dataTableOffset,
				DataTableSize = num2,
				NameTableOffset = num,
				NameTableSize = nameTableSize
			};
			using (FileStream stream = File.Create(path))
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(stream))
				{
					bool flag = rawStream != null && rawStream.Length > 32;
					if (flag)
					{
						fileHeader.RawTableOffset = Utils.Align32(fileHeader.DataTableOffset + fileHeader.DataTableSize);
						fileHeader.RawTableSize = (int)rawStream.Length;
					}
					fileHeader.Write(binaryWriter);
					foreach (ImporterFileDescriptor descriptor in descriptors)
					{
						WriteDescriptor(binaryWriter, descriptor, inputFilePath);
					}
					binaryWriter.Position = fileHeader.NameTableOffset;
					foreach (ImporterFileDescriptor descriptor2 in descriptors)
					{
						if (descriptor2.Name != null)
						{
							binaryWriter.Write(descriptor2.Name, descriptor2.Name.Length + 1);
						}
					}
					binaryWriter.Position = fileHeader.DataTableOffset;
					foreach (ImporterFileDescriptor item2 in descriptors.Where((ImporterFileDescriptor d) => d.Data != null))
					{
						binaryWriter.Align32();
						binaryWriter.WriteInstanceId(item2.Index);
						binaryWriter.Write(0);
						binaryWriter.Write(item2.Data);
					}
					if (flag)
					{
						binaryWriter.Position = fileHeader.RawTableOffset;
						rawStream.WriteTo(stream);
					}
				}
			}
		}

		private void WriteDescriptor(BinaryWriter writer, ImporterFileDescriptor descriptor, string inputFilePath)
		{
			InstanceDescriptorFlags instanceDescriptorFlags = InstanceDescriptorFlags.None;
			if (descriptor.Name == null)
			{
				instanceDescriptorFlags |= InstanceDescriptorFlags.Private;
			}
			if (descriptor.Data == null)
			{
				instanceDescriptorFlags |= InstanceDescriptorFlags.Placeholder;
			}
			if (descriptor.Name == null && descriptor.Data == null)
			{
				if (string.IsNullOrEmpty(inputFilePath))
				{
					throw new InvalidOperationException("Link descriptors must have names (input source unavailable)");
				}
				throw new InvalidOperationException(string.Format("Link descriptors must have names in {0}", Path.GetFullPath(inputFilePath)));
			}
			writer.Write((int)descriptor.Tag);
			writer.Write(descriptor.DataOffset);
			writer.Write(descriptor.NameOffset);
			writer.Write(descriptor.DataSize);
			writer.Write((int)instanceDescriptorFlags);
		}

		private static string MakeInstanceName(TemplateTag tag, string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}
			string text = tag.ToString();
			if (!name.StartsWith(text, StringComparison.Ordinal))
			{
				name = text + name;
			}
			return name;
		}
	}
}
