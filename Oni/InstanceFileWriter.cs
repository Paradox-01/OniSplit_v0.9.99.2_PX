using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Oni.Collections;
using Oni.Metadata;

namespace Oni
{
	internal sealed class InstanceFileWriter
	{
		private class FileHeader
		{
			public const int Size = 64;

			public long TemplateChecksum;

			public int Version;

			public int InstanceCount;

			public int NameCount;

			public int TemplateCount;

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
				writer.Write(NameCount);
				writer.Write(TemplateCount);
				writer.Write(DataTableOffset);
				writer.Write(DataTableSize);
				writer.Write(NameTableOffset);
				writer.Write(NameTableSize);
				writer.Write(RawTableOffset);
				writer.Write(RawTableSize);
				writer.Write(0);
				writer.Write(0);
			}
		}

		private class DescriptorTableEntry
		{
			public const int Size = 20;

			public readonly InstanceDescriptor SourceDescriptor;

			public readonly int Id;

			public int DataOffset;

			public int NameOffset;

			public int DataSize;

			public bool AnimationPositionPointHack;

			public bool HasName
			{
				get
				{
					return SourceDescriptor.HasName;
				}
			}

			public string Name
			{
				get
				{
					return SourceDescriptor.FullName;
				}
			}

			public TemplateTag Code
			{
				get
				{
					return SourceDescriptor.Template.Tag;
				}
			}

			public InstanceFile SourceFile
			{
				get
				{
					return SourceDescriptor.File;
				}
			}

			public DescriptorTableEntry(int id, InstanceDescriptor descriptor)
			{
				Id = id;
				SourceDescriptor = descriptor;
			}

			public void Write(BinaryWriter writer, bool shared)
			{
				writer.Write((int)Code);
				writer.Write(DataOffset);
				writer.Write(NameOffset);
				writer.Write(DataSize);
				InstanceDescriptorFlags instanceDescriptorFlags = InstanceDescriptorFlags.None;
				if (!SourceDescriptor.HasName)
				{
					instanceDescriptorFlags |= InstanceDescriptorFlags.Private;
				}
				if (DataOffset == 0)
				{
					instanceDescriptorFlags |= InstanceDescriptorFlags.Placeholder;
				}
				if (shared)
				{
					instanceDescriptorFlags |= InstanceDescriptorFlags.Shared;
				}
				writer.Write((int)instanceDescriptorFlags);
			}
		}

		private class NameDescriptorTable
		{
			private class Entry : IComparable<Entry>
			{
				public const int Size = 8;

				public int InstanceNumber;

				public string Name;

				public void Write(BinaryWriter writer)
				{
					writer.Write(InstanceNumber);
					writer.Write(0);
				}

				int IComparable<Entry>.CompareTo(Entry other)
				{
					return string.CompareOrdinal(Name, other.Name);
				}
			}

			private List<Entry> entries;

			public int Count
			{
				get
				{
					return entries.Count;
				}
			}

			public int Size
			{
				get
				{
					return entries.Count * 8;
				}
			}

			public static NameDescriptorTable CreateFromDescriptors(List<DescriptorTableEntry> descriptorTable)
			{
				NameDescriptorTable nameDescriptorTable = new NameDescriptorTable();
				nameDescriptorTable.entries = new List<Entry>();
				for (int i = 0; i < descriptorTable.Count; i++)
				{
					DescriptorTableEntry descriptorTableEntry = descriptorTable[i];
					if (descriptorTableEntry.HasName)
					{
						Entry entry = new Entry();
						entry.Name = descriptorTableEntry.Name;
						entry.InstanceNumber = i;
						nameDescriptorTable.entries.Add(entry);
					}
				}
				nameDescriptorTable.entries.Sort();
				return nameDescriptorTable;
			}

			public void Write(BinaryWriter writer)
			{
				foreach (Entry entry in entries)
				{
					entry.Write(writer);
				}
			}
		}

		private class TemplateDescriptorTable
		{
			private class Entry : IComparable<Entry>
			{
				public const int Size = 16;

				public long Checksum;

				public TemplateTag Code;

				public int Count;

				public void Write(BinaryWriter writer)
				{
					writer.Write(Checksum);
					writer.Write((int)Code);
					writer.Write(Count);
				}

				int IComparable<Entry>.CompareTo(Entry other)
				{
					return Code.CompareTo(other.Code);
				}
			}

			private List<Entry> entries;

			public int Count
			{
				get
				{
					return entries.Count;
				}
			}

			public int Size
			{
				get
				{
					return entries.Count * 16;
				}
			}

			public static TemplateDescriptorTable CreateFromDescriptors(InstanceMetadata metadata, List<DescriptorTableEntry> descriptorTable)
			{
				Dictionary<TemplateTag, int> dictionary = new Dictionary<TemplateTag, int>();
				foreach (DescriptorTableEntry item in descriptorTable)
				{
					int value;
					dictionary.TryGetValue(item.Code, out value);
					dictionary[item.Code] = value + 1;
				}
				TemplateDescriptorTable templateDescriptorTable = new TemplateDescriptorTable();
				templateDescriptorTable.entries = new List<Entry>(dictionary.Count);
				foreach (KeyValuePair<TemplateTag, int> item2 in dictionary)
				{
					Entry entry = new Entry();
					entry.Checksum = metadata.GetTemplate(item2.Key).Checksum;
					entry.Code = item2.Key;
					entry.Count = item2.Value;
					templateDescriptorTable.entries.Add(entry);
				}
				templateDescriptorTable.entries.Sort();
				return templateDescriptorTable;
			}

			public void Write(BinaryWriter writer)
			{
				foreach (Entry entry in entries)
				{
					entry.Write(writer);
				}
			}
		}

		private class NameTable
		{
			private List<string> names;

			private int size;

			public int Size
			{
				get
				{
					return size;
				}
			}

			public static NameTable CreateFromDescriptors(List<DescriptorTableEntry> descriptors)
			{
				NameTable nameTable = new NameTable();
				nameTable.names = new List<string>();
				int num = 0;
				foreach (DescriptorTableEntry descriptor in descriptors)
				{
					if (descriptor.HasName)
					{
						string name = descriptor.Name;
						nameTable.names.Add(name);
						descriptor.NameOffset = num;
						num += name.Length + 1;
						if (name.Length > 63)
						{
							Console.WriteLine("Warning: name '{0}' too long.", name);
						}
					}
				}
				nameTable.size = num;
				return nameTable;
			}

			public void Write(BinaryWriter writer)
			{
				byte[] array = new byte[256];
				foreach (string name in names)
				{
					int bytes = Encoding.UTF8.GetBytes(name, 0, name.Length, array, 0);
					array[bytes] = 0;
					writer.Write(array, 0, bytes + 1);
				}
			}
		}

		private class BinaryPartEntry : IComparable<BinaryPartEntry>
		{
			public readonly int SourceOffset;

			public readonly string SourceFile;

			public readonly int DestinationOffset;

			public readonly int Size;

			public readonly BinaryPartField Field;

			public BinaryPartEntry(string sourceFile, int sourceOffset, int size, int destinationOffset, Field field)
			{
				SourceFile = sourceFile;
				SourceOffset = sourceOffset;
				Size = size;
				DestinationOffset = destinationOffset;
				Field = (BinaryPartField)field;
			}

			int IComparable<BinaryPartEntry>.CompareTo(BinaryPartEntry other)
			{
				return DestinationOffset.CompareTo(other.DestinationOffset);
			}
		}

		private class ChecksumStream : Stream
		{
			private int checksum;

			private int position;

			public int Checksum
			{
				get
				{
					return checksum;
				}
			}

			public override bool CanRead
			{
				get
				{
					return false;
				}
			}

			public override bool CanSeek
			{
				get
				{
					return false;
				}
			}

			public override bool CanWrite
			{
				get
				{
					return true;
				}
			}

			public override long Length
			{
				get
				{
					return position;
				}
			}

			public override long Position
			{
				get
				{
					return position;
				}
				set
				{
					throw new NotSupportedException();
				}
			}

			public override void Flush()
			{
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				throw new NotSupportedException();
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				throw new NotSupportedException();
			}

			public override void SetLength(long value)
			{
				throw new NotSupportedException();
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				for (int i = offset; i < offset + count; i++)
				{
					checksum += buffer[i] ^ (i + position);
				}
				position += count;
			}
		}

		private class StreamCache : IDisposable
		{
			private class CacheEntry
			{
				public BinaryReader Stream;

				public long LastTimeUsed;
			}

			private const int maxCacheSize = 32;

			private Dictionary<string, CacheEntry> cacheEntries = new Dictionary<string, CacheEntry>();

			public BinaryReader GetReader(InstanceDescriptor descriptor)
			{
				CacheEntry value;
				if (!cacheEntries.TryGetValue(descriptor.FilePath, out value))
				{
					value = OpenStream(descriptor);
				}
				value.LastTimeUsed = DateTime.Now.Ticks;
				value.Stream.Position = descriptor.DataOffset;
				return value.Stream;
			}

			private CacheEntry OpenStream(InstanceDescriptor descriptor)
			{
				CacheEntry cacheEntry = null;
				string key = null;
				if (cacheEntries.Count >= 32)
				{
					foreach (KeyValuePair<string, CacheEntry> cacheEntry2 in cacheEntries)
					{
						if (cacheEntry == null || cacheEntry2.Value.LastTimeUsed < cacheEntry.LastTimeUsed)
						{
							key = cacheEntry2.Key;
							cacheEntry = cacheEntry2.Value;
						}
					}
				}
				if (cacheEntry == null)
				{
					cacheEntry = new CacheEntry();
				}
				else
				{
					cacheEntry.Stream.Dispose();
					cacheEntries.Remove(key);
				}
				cacheEntry.Stream = new BinaryReader(descriptor.FilePath);
				cacheEntries.Add(descriptor.FilePath, cacheEntry);
				return cacheEntry;
			}

			public void Dispose()
			{
				foreach (CacheEntry value in cacheEntries.Values)
				{
					value.Stream.Dispose();
				}
			}
		}

		private static readonly byte[] padding = new byte[512];

		private static byte[] copyBuffer1 = new byte[32768];

		private static byte[] copyBuffer2 = new byte[32768];

		private StreamCache streamCache;

		private readonly bool bigEndian;

		private readonly Dictionary<string, int> namedInstancedIdMap;

		private readonly FileHeader header;

		private readonly List<DescriptorTableEntry> descriptorTable;

		private NameDescriptorTable nameIndex;

		private TemplateDescriptorTable templateTable;

		private NameTable nameTable;

		private readonly Dictionary<InstanceDescriptor, InstanceDescriptor> sharedMap;

		private readonly Dictionary<InstanceFile, int[]> linkMaps;

		private readonly Dictionary<InstanceFile, Dictionary<int, int>> rawOffsetMaps;

		private readonly Dictionary<InstanceFile, Dictionary<int, int>> sepOffsetMaps;

		private int rawOffset;

		private int sepOffset;

		private List<BinaryPartEntry> rawParts;

		private List<BinaryPartEntry> sepParts;

		private bool IsV31
		{
			get
			{
				return header.Version == 1448227633;
			}
		}

		private bool IsV32
		{
			get
			{
				return header.Version == 1448227634;
			}
		}

		public static InstanceFileWriter CreateV31(long templateChecksum, bool bigEndian)
		{
			return new InstanceFileWriter(templateChecksum, 1448227633, bigEndian);
		}

		public static InstanceFileWriter CreateV32(List<InstanceDescriptor> descriptors)
		{
			long templateChecksum = ((!descriptors.Exists((InstanceDescriptor x) => x.Template.Tag == TemplateTag.SNDD && x.IsMacFile)) ? 1052091763926815L : 1052091493724257L);
			InstanceFileWriter instanceFileWriter = new InstanceFileWriter(templateChecksum, 1448227634, false);
			instanceFileWriter.AddDescriptors(descriptors, false);
			return instanceFileWriter;
		}

		private InstanceFileWriter(long templateChecksum, int version, bool bigEndian)
		{
			if (templateChecksum != 1052091763926815L && templateChecksum != 1052091493724257L && templateChecksum != 0L)
			{
				throw new ArgumentException("Unknown template checksum", "templateChecksum");
			}
			this.bigEndian = bigEndian;
			header = new FileHeader
			{
				TemplateChecksum = templateChecksum,
				Version = version
			};
			descriptorTable = new List<DescriptorTableEntry>();
			namedInstancedIdMap = new Dictionary<string, int>();
			linkMaps = new Dictionary<InstanceFile, int[]>();
			rawOffsetMaps = new Dictionary<InstanceFile, Dictionary<int, int>>();
			sepOffsetMaps = new Dictionary<InstanceFile, Dictionary<int, int>>();
			sharedMap = new Dictionary<InstanceDescriptor, InstanceDescriptor>();
		}

		public void AddDescriptors(List<InstanceDescriptor> descriptors, bool removeDuplicates)
		{
			if (removeDuplicates)
			{
				Console.WriteLine("Removing duplicates");
				using (streamCache = new StreamCache())
				{
					descriptors = RemoveDuplicates(descriptors);
				}
			}
			Set<InstanceFile> set = new Set<InstanceFile>();
			foreach (InstanceDescriptor descriptor in descriptors)
			{
				set.Add(descriptor.File);
			}
			foreach (InstanceFile item in set)
			{
				linkMaps[item] = new int[item.Descriptors.Count];
				rawOffsetMaps[item] = new Dictionary<int, int>();
				sepOffsetMaps[item] = new Dictionary<int, int>();
			}
			foreach (InstanceDescriptor descriptor2 in descriptors)
			{
				AddDescriptor(descriptor2);
			}
			CreateHeader();
		}

		private void AddDescriptor(InstanceDescriptor descriptor)
		{
			if (descriptor.Template.Tag == TemplateTag.SNDD)
			{
				if (header.TemplateChecksum == 0L)
				{
					header.TemplateChecksum = descriptor.TemplateChecksum;
				}
				else if (header.TemplateChecksum != descriptor.TemplateChecksum && header.TemplateChecksum == 1052091493724257L)
				{
					throw new NotSupportedException(string.Format("File {0} cannot be imported due to conflicting template checksums", descriptor.FilePath));
				}
			}
			int num = MakeInstanceId(descriptorTable.Count);
			linkMaps[descriptor.File][descriptor.Index] = num;
			if (descriptor.HasName)
			{
				namedInstancedIdMap[descriptor.FullName] = num;
			}
			DescriptorTableEntry descriptorTableEntry = new DescriptorTableEntry(num, descriptor);
			if (!descriptor.IsPlaceholder && (!IsV32 || !descriptor.HasName || descriptorTable.Count == 0 || descriptorTable[0].SourceDescriptor == descriptor))
			{
				int dataSize = descriptor.DataSize;
				if (descriptor.Template.Tag == TemplateTag.SNDD && header.TemplateChecksum == 1052091763926815L && descriptor.TemplateChecksum == 1052091493724257L)
				{
					dataSize = 96;
				}
				else if (descriptor.Template.Tag == TemplateTag.AKDA)
				{
					dataSize = 32;
				}
				descriptorTableEntry.DataSize = dataSize;
				descriptorTableEntry.DataOffset = header.DataTableSize + 8;
				header.DataTableSize += descriptorTableEntry.DataSize;
			}
			descriptorTable.Add(descriptorTableEntry);
		}

		private void CreateHeader()
		{
			if (header.TemplateChecksum == 0L)
			{
				throw new InvalidOperationException("Target file format was not specified and cannot be autodetected.");
			}
			header.InstanceCount = descriptorTable.Count;
			int num = 64 + descriptorTable.Count * 20;
			if (IsV31)
			{
				nameIndex = NameDescriptorTable.CreateFromDescriptors(descriptorTable);
				header.NameCount = nameIndex.Count;
				num += nameIndex.Size;
				templateTable = TemplateDescriptorTable.CreateFromDescriptors(InstanceMetadata.GetMetadata(header.TemplateChecksum), descriptorTable);
				header.TemplateCount = templateTable.Count;
				num += templateTable.Size;
				header.DataTableOffset = Utils.Align32(num);
				nameTable = NameTable.CreateFromDescriptors(descriptorTable);
				header.NameTableSize = nameTable.Size;
				header.NameTableOffset = Utils.Align32(header.DataTableOffset + header.DataTableSize);
			}
			else
			{
				nameTable = NameTable.CreateFromDescriptors(descriptorTable);
				header.NameTableSize = nameTable.Size;
				header.NameTableOffset = Utils.Align32(num);
				header.DataTableOffset = Utils.Align32(header.NameTableOffset + nameTable.Size);
				header.RawTableOffset = Utils.Align32(header.DataTableOffset + header.DataTableSize);
			}
		}

		public void Write(string filePath)
		{
			string directoryName = Path.GetDirectoryName(filePath);
			Directory.CreateDirectory(directoryName);
			int fileId = (IsV31 ? MakeFileId(filePath) : 0);
			using (streamCache = new StreamCache())
			{
				using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536))
				{
					using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
					{
						fileStream.Position = 64L;
						foreach (DescriptorTableEntry item in descriptorTable)
						{
							item.Write(binaryWriter, sharedMap.ContainsKey(item.SourceDescriptor));
						}
						if (IsV31)
						{
							nameIndex.Write(binaryWriter);
							templateTable.Write(binaryWriter);
						}
						else
						{
							binaryWriter.Position = header.NameTableOffset;
							nameTable.Write(binaryWriter);
						}
						WriteDataTable(binaryWriter, fileId);
						if (IsV31)
						{
							binaryWriter.Position = header.NameTableOffset;
							nameTable.Write(binaryWriter);
						}
						WriteBinaryParts(binaryWriter, filePath);
						if (IsV32 && fileStream.Length > header.RawTableOffset)
						{
							header.RawTableSize = (int)fileStream.Length - header.RawTableOffset;
						}
						fileStream.Position = 0L;
						header.Write(binaryWriter);
					}
				}
			}
		}

		private void WriteDataTable(BinaryWriter writer, int fileId)
		{
			writer.Position = header.DataTableOffset;
			rawOffset = 32;
			rawParts = new List<BinaryPartEntry>();
			sepOffset = 32;
			sepParts = new List<BinaryPartEntry>();
			DescriptorTableEntry[] array = descriptorTable.ToArray();
			Array.Sort(array, (DescriptorTableEntry x, DescriptorTableEntry y) => x.DataOffset.CompareTo(y.DataOffset));
			DescriptorTableEntry[] array2 = array;
			foreach (DescriptorTableEntry entry in array2)
			{
				if (entry.DataSize == 0)
				{
					continue;
				}
				int num2 = header.DataTableOffset + entry.DataOffset - 8 - writer.Position;
				if (num2 <= 512)
				{
					writer.Write(padding, 0, num2);
				}
				else
				{
					writer.Position = header.DataTableOffset + entry.DataOffset - 8;
				}
				writer.Write(entry.Id);
				writer.Write(fileId);
				Template template = entry.SourceDescriptor.Template;
				if (template.Tag == TemplateTag.SNDD && entry.SourceDescriptor.File.Header.TemplateChecksum == 1052091493724257L && header.TemplateChecksum == 1052091763926815L)
				{
					ConvertSNDDHack(entry, writer);
					continue;
				}
				try
				{
					template.Type.Copy(streamCache.GetReader(entry.SourceDescriptor), writer, delegate(CopyVisitor state)
					{
						if (state.Type == MetaType.RawOffset)
						{
							RemapRawOffset(entry, state);
						}
						else if (state.Type == MetaType.SepOffset)
						{
							RemapSepOffset(entry, state);
						}
						else if (state.Type is MetaPointer)
						{
							RemapLinkId(entry, state);
						}
					});
				}
				catch (InvalidDataException ex)
				{
					throw new InvalidDataException(string.Format("Could not copy instance '{0}' ({1}) from '{2}': {3}", entry.SourceDescriptor.FullName, template.Tag, entry.SourceDescriptor.File.FilePath, ex.Message), ex);
				}
				if (entry.Code == TemplateTag.TXMP)
				{
					ConvertTXMPHack(entry, writer.BaseStream);
				}
			}
		}

		private void ConvertSNDDHack(DescriptorTableEntry entry, BinaryWriter writer)
		{
			BinaryReader reader = streamCache.GetReader(entry.SourceDescriptor);
			int num = reader.ReadInt32();
			int num2 = reader.ReadInt32();
			int value = reader.ReadInt32();
			int oldOffset = reader.ReadInt32();
			int value2 = ((num != 3) ? 1 : 2);
			writer.Write(8);
			writer.WriteInt16(2);
			writer.WriteInt16(value2);
			writer.Write(22050);
			writer.Write(11155);
			writer.WriteInt16(512);
			writer.WriteInt16(4);
			writer.WriteInt16(32);
			writer.Write(new byte[32]
			{
				244, 3, 7, 0, 0, 1, 0, 0, 0, 2,
				0, 255, 0, 0, 0, 0, 192, 0, 64, 0,
				240, 0, 0, 0, 204, 1, 48, 255, 136, 1,
				24, 255
			});
			writer.Write((short)num2);
			writer.Write(value);
			writer.Write(RemapRawOffsetCore(entry, oldOffset, null));
		}

		private void ConvertTXMPHack(DescriptorTableEntry entry, Stream stream)
		{
			stream.Position = header.DataTableOffset + entry.DataOffset + 128;
			stream.Read(copyBuffer1, 0, 28);
			if (header.TemplateChecksum == 1052091763926815L)
			{
				copyBuffer1[1] |= 16;
			}
			else if (IsV31 && header.TemplateChecksum == 1052091493724257L)
			{
				if (bigEndian && copyBuffer1[8] == 7)
				{
					copyBuffer1[1] &= 239;
				}
				else
				{
					copyBuffer1[1] |= 16;
				}
			}
			if (entry.SourceDescriptor.TemplateChecksum != header.TemplateChecksum)
			{
				for (int i = 20; i < 24; i++)
				{
					byte b = copyBuffer1[i];
					copyBuffer1[i] = copyBuffer1[i + 4];
					copyBuffer1[i + 4] = b;
				}
			}
			stream.Position = header.DataTableOffset + entry.DataOffset + 128;
			stream.Write(copyBuffer1, 0, 28);
		}

		private bool ZeroTRAMPositionPointsHack(DescriptorTableEntry entry, CopyVisitor state)
		{
			if (entry.Code != TemplateTag.TRAM)
			{
				return false;
			}
			int @int = state.GetInt32();
			if (state.Position == 4)
			{
				entry.AnimationPositionPointHack = @int == 0;
			}
			else if (state.Position == 40 && entry.AnimationPositionPointHack)
			{
				if (@int != 0)
				{
					InstanceFile sourceFile = entry.SourceFile;
					int rawPartSize = sourceFile.GetRawPartSize(@int);
					@int = AllocateRawPart(null, 0, rawPartSize, null);
					state.SetInt32(@int);
				}
				return true;
			}
			return false;
		}

		private void RemapRawOffset(DescriptorTableEntry entry, CopyVisitor state)
		{
			if (!ZeroTRAMPositionPointsHack(entry, state))
			{
				state.SetInt32(RemapRawOffsetCore(entry, state.GetInt32(), state.Field));
			}
		}

		private int RemapRawOffsetCore(DescriptorTableEntry entry, int oldOffset, Field field)
		{
			if (oldOffset == 0)
			{
				return 0;
			}
			InstanceFile sourceFile = entry.SourceFile;
			Dictionary<int, int> dictionary = rawOffsetMaps[sourceFile];
			int value;
			if (!dictionary.TryGetValue(oldOffset, out value))
			{
				int rawPartSize = sourceFile.GetRawPartSize(oldOffset);
				value = (dictionary[oldOffset] = ((header.TemplateChecksum != 1052091493724257L || (entry.Code != TemplateTag.TXMP && entry.Code != TemplateTag.OSBD && entry.Code != TemplateTag.BINA)) ? AllocateRawPart(sourceFile.RawFilePath, oldOffset + sourceFile.Header.RawTableOffset, rawPartSize, field) : AllocateSepPart(sourceFile.RawFilePath, oldOffset + sourceFile.Header.RawTableOffset, rawPartSize, null)));
			}
			return value;
		}

		private void RemapSepOffset(DescriptorTableEntry entry, CopyVisitor state)
		{
			int @int = state.GetInt32();
			if (@int != 0)
			{
				InstanceFile sourceFile = entry.SourceFile;
				Dictionary<int, int> dictionary = sepOffsetMaps[sourceFile];
				int value;
				if (!dictionary.TryGetValue(@int, out value))
				{
					int sepPartSize = sourceFile.GetSepPartSize(@int);
					value = (dictionary[@int] = ((header.TemplateChecksum != 1052091763926815L) ? AllocateSepPart(sourceFile.SepFilePath, @int, sepPartSize, null) : AllocateRawPart(sourceFile.SepFilePath, @int, sepPartSize, null)));
				}
				state.SetInt32(value);
			}
		}

		private int AllocateRawPart(string sourceFile, int sourceOffset, int size, Field field)
		{
			BinaryPartEntry binaryPartEntry = new BinaryPartEntry(sourceFile, sourceOffset, size, rawOffset, field);
			rawOffset = Utils.Align32(rawOffset + size);
			rawParts.Add(binaryPartEntry);
			return binaryPartEntry.DestinationOffset;
		}

		private int AllocateSepPart(string sourceFile, int sourceOffset, int size, Field field)
		{
			BinaryPartEntry binaryPartEntry = new BinaryPartEntry(sourceFile, sourceOffset, size, sepOffset, field);
			sepOffset = Utils.Align32(sepOffset + size);
			sepParts.Add(binaryPartEntry);
			return binaryPartEntry.DestinationOffset;
		}

		private void RemapLinkId(DescriptorTableEntry entry, CopyVisitor state)
		{
			int @int = state.GetInt32();
			if (@int != 0)
			{
				int int2 = RemapLinkIdCore(entry.SourceDescriptor, @int);
				state.SetInt32(int2);
			}
		}

		private int RemapLinkIdCore(InstanceDescriptor descriptor, int id)
		{
			InstanceFile file = descriptor.File;
			if (IsV31)
			{
				InstanceDescriptor descriptor2 = file.GetDescriptor(id);
				int value;
				if (descriptor2.HasName && namedInstancedIdMap.TryGetValue(descriptor2.FullName, out value))
				{
					return value;
				}
				InstanceDescriptor value2;
				if (sharedMap.TryGetValue(descriptor2, out value2))
				{
					return linkMaps[value2.File][value2.Index];
				}
			}
			return linkMaps[file][id >> 8];
		}

		private void WriteBinaryParts(BinaryWriter writer, string filePath)
		{
			if (IsV32)
			{
				WriteParts(writer, rawParts);
				return;
			}
			string text = Path.ChangeExtension(filePath, ".raw");
			Console.WriteLine("Writing {0}", text);
			using (FileStream stream = new FileStream(text, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
			{
				using (BinaryWriter writer2 = new BinaryWriter(stream))
				{
					WriteParts(writer2, rawParts);
				}
			}
			if (header.TemplateChecksum != 1052091493724257L)
			{
				return;
			}
			string text2 = Path.ChangeExtension(filePath, ".sep");
			Console.WriteLine("Writing {0}", text2);
			using (FileStream stream2 = new FileStream(text2, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
			{
				using (BinaryWriter writer3 = new BinaryWriter(stream2))
				{
					WriteParts(writer3, sepParts);
				}
			}
		}

		private void WriteParts(BinaryWriter writer, List<BinaryPartEntry> binaryParts)
		{
			if (binaryParts.Count == 0)
			{
				writer.Write(padding, 0, 32);
				return;
			}
			binaryParts.Sort();
			int num = 0;
			foreach (BinaryPartEntry binaryPart in binaryParts)
			{
				if (binaryPart.DestinationOffset + binaryPart.Size > num)
				{
					num = binaryPart.DestinationOffset + binaryPart.Size;
				}
			}
			if (IsV31)
			{
				writer.BaseStream.SetLength(num);
			}
			else
			{
				writer.BaseStream.SetLength(num + header.RawTableOffset);
			}
			BinaryReader binaryReader = null;
			foreach (BinaryPartEntry binaryPart2 in binaryParts)
			{
				if (binaryPart2.SourceFile == null)
				{
					continue;
				}
				if (binaryReader == null)
				{
					binaryReader = new BinaryReader(binaryPart2.SourceFile);
				}
				else if (binaryReader.Name != binaryPart2.SourceFile)
				{
					binaryReader.Dispose();
					binaryReader = new BinaryReader(binaryPart2.SourceFile);
				}
				binaryReader.Position = binaryPart2.SourceOffset;
				int num2 = binaryPart2.DestinationOffset + header.RawTableOffset - writer.Position;
				if (num2 <= 32)
				{
					writer.Write(padding, 0, num2);
				}
				else
				{
					writer.Position = binaryPart2.DestinationOffset + header.RawTableOffset;
				}
				if (binaryPart2.Field == null || binaryPart2.Field.RawType == null)
				{
					if (copyBuffer1.Length < binaryPart2.Size)
					{
						copyBuffer1 = new byte[binaryPart2.Size * 2];
					}
					binaryReader.Read(copyBuffer1, 0, binaryPart2.Size);
					writer.Write(copyBuffer1, 0, binaryPart2.Size);
					continue;
				}
				int num3 = binaryPart2.Size;
				while (num3 > 0)
				{
					int num4 = binaryPart2.Field.RawType.Copy(binaryReader, writer, null);
					if (num4 > num3)
					{
						throw new InvalidOperationException(string.Format("Bad metadata copying field {0}", binaryPart2.Field.Name));
					}
					num3 -= num4;
				}
			}
			if (binaryReader != null)
			{
				binaryReader.Dispose();
			}
		}

		private List<InstanceDescriptor> RemoveDuplicates(List<InstanceDescriptor> descriptors)
		{
			Dictionary<int, List<InstanceDescriptor>> dictionary = new Dictionary<int, List<InstanceDescriptor>>();
			List<InstanceDescriptor> list = new List<InstanceDescriptor>(descriptors.Count);
			foreach (InstanceDescriptor descriptor in descriptors)
			{
				if (descriptor.Template.Tag != TemplateTag.IDXA && descriptor.Template.Tag != TemplateTag.PNTA && descriptor.Template.Tag != TemplateTag.VCRA && descriptor.Template.Tag != TemplateTag.TXCA && descriptor.Template.Tag != TemplateTag.TRTA && descriptor.Template.Tag != TemplateTag.TRIA && descriptor.Template.Tag != TemplateTag.ONCP && descriptor.Template.Tag != TemplateTag.ONIA)
				{
					list.Add(descriptor);
					continue;
				}
				int instanceChecksum = GetInstanceChecksum(descriptor);
				List<InstanceDescriptor> value;
				if (!dictionary.TryGetValue(instanceChecksum, out value))
				{
					value = new List<InstanceDescriptor>();
					dictionary.Add(instanceChecksum, value);
				}
				else
				{
					InstanceDescriptor instanceDescriptor = value.Find((InstanceDescriptor x) => AreInstancesEqual(descriptor, x));
					if (instanceDescriptor != null)
					{
						sharedMap.Add(descriptor, instanceDescriptor);
						continue;
					}
				}
				value.Add(descriptor);
				list.Add(descriptor);
			}
			return list;
		}

		private int GetInstanceChecksum(InstanceDescriptor descriptor)
		{
			using (ChecksumStream checksumStream = new ChecksumStream())
			{
				using (BinaryWriter output = new BinaryWriter(checksumStream))
				{
					descriptor.Template.Type.Copy(streamCache.GetReader(descriptor), output, null);
					return checksumStream.Checksum;
				}
			}
		}

		private bool AreInstancesEqual(InstanceDescriptor d1, InstanceDescriptor d2)
		{
			if (d1.File == d2.File && d1.Index == d2.Index)
			{
				return true;
			}
			if (d1.Template.Tag != d2.Template.Tag || d1.DataSize != d2.DataSize)
			{
				return false;
			}
			if (copyBuffer1.Length < d1.DataSize)
			{
				copyBuffer1 = new byte[d1.DataSize * 2];
			}
			if (copyBuffer2.Length < d2.DataSize)
			{
				copyBuffer2 = new byte[d2.DataSize * 2];
			}
			MetaType type = d1.Template.Type;
			using (BinaryWriter output = new BinaryWriter(new MemoryStream(copyBuffer1)))
			{
				using (BinaryWriter output2 = new BinaryWriter(new MemoryStream(copyBuffer2)))
				{
					int num = type.Copy(streamCache.GetReader(d1), output, null);
					int num2 = type.Copy(streamCache.GetReader(d2), output2, null);
					if (num != num2)
					{
						return false;
					}
					for (int i = 0; i < num; i++)
					{
						if (copyBuffer1[i] != copyBuffer2[i])
						{
							return false;
						}
					}
				}
			}
			return true;
		}

		private static int MakeFileId(string filePath)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
			if (fileNameWithoutExtension.Length < 6)
			{
				return 0;
			}
			fileNameWithoutExtension = fileNameWithoutExtension.Substring(5);
			int result = 0;
			int num = 0;
			int num2 = fileNameWithoutExtension.IndexOf('_');
			if (num2 != -1)
			{
				int.TryParse(fileNameWithoutExtension.Substring(0, num2), out result);
				if (!string.Equals(fileNameWithoutExtension.Substring(num2 + 1), "Final", StringComparison.Ordinal))
				{
					for (int i = 1; num2 + i < fileNameWithoutExtension.Length; i++)
					{
						num += (char.ToUpperInvariant(fileNameWithoutExtension[num2 + i]) - 64) * i;
					}
				}
			}
			return (((result << 24) | (num & 0xFFFFFF)) << 1) | 1;
		}

		public static int MakeInstanceId(int index)
		{
			return (index << 8) | 1;
		}
	}
}
