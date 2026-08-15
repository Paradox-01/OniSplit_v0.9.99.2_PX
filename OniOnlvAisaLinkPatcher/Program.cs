using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal static class Program
{
	private const int Version31 = 1448227633;
	private const int Version32 = 1448227634;
	private const int AisaTag = 1095324481;
	private const int OnlvTag = 1330531414;
	private const int DescriptorSize = 20;
	private const int OnlvAisaOffset = 92;
	private const int PrivateFlag = 1;
	private const int PlaceholderFlag = 2;

	private sealed class Header
	{
		public int Version;
		public int InstanceCount;
		public int NameCount;
		public int TemplateCount;
		public int DataTableOffset;
		public int DataTableSize;
		public int NameTableOffset;
		public int NameTableSize;
		public int RawTableOffset;
	}

	private sealed class Descriptor
	{
		public int Tag;
		public int DataOffset;
		public int NameOffset;
		public int DataSize;
		public int Flags;

		public bool HasName
		{
			get { return (Flags & PrivateFlag) == 0; }
		}
	}

	private static int Main(string[] args)
	{
		if (args.Length != 2)
		{
			Console.Error.WriteLine("Usage: OniOnlvAisaLinkPatcher.exe <ONLV-file.oni> <AISA-file.oni>");
			return 2;
		}

		string onlvPath = Path.GetFullPath(args[0]);
		string aisaPath = Path.GetFullPath(args[1]);

		try
		{
			ValidateInputPath(onlvPath, "ONLV");
			ValidateInputPath(aisaPath, "AISA");
			string aisaName = Path.GetFileNameWithoutExtension(aisaPath);
			ValidateAisaFile(aisaPath);
			Patch(onlvPath, aisaName);
			Console.WriteLine("Linked " + Path.GetFileName(onlvPath) + " to " + aisaName + ".");
			return 0;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception.Message);
			return 1;
		}
	}

	private static void ValidateInputPath(string path, string prefix)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException(prefix + " file not found.", path);
		}

		string fileName = Path.GetFileName(path);
		if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
			!string.Equals(Path.GetExtension(fileName), ".oni", StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException(prefix + " input must be an " + prefix + "*.oni file.");
		}
	}

	private static void ValidateAisaFile(string path)
	{
		byte[] data = File.ReadAllBytes(path);
		Header header = ReadHeader(data);
		List<Descriptor> descriptors = ReadDescriptors(data, header);
		// OniSplit may declare trailing alignment bytes that are omitted from the
		// physical file. Only the AISA identity and descriptor start are needed.
		foreach (Descriptor descriptor in descriptors)
		{
			if (descriptor.Tag == AisaTag && descriptor.DataOffset != 0 && descriptor.DataSize != 0)
			{
				ValidateDescriptorStart(data, header, descriptor, "AISA");
				return;
			}
		}

		throw new InvalidDataException("The AISA input does not contain an AISA instance.");
	}

	private static void Patch(string path, string aisaName)
	{
		Encoding utf8 = new UTF8Encoding(false, true);
		byte[] encodedName = utf8.GetBytes(aisaName);
		if (encodedName.Length == 0 || encodedName.Length > 63 || aisaName.IndexOf('\0') >= 0)
		{
			throw new InvalidDataException("The AISA filename must contain 1 to 63 UTF-8 bytes.");
		}

		byte[] data = File.ReadAllBytes(path);
		Header header = ReadHeader(data);
		List<Descriptor> descriptors = ReadDescriptors(data, header);
		int onlvIndex = FindDataDescriptor(descriptors, OnlvTag, "ONLV");
		Descriptor onlv = descriptors[onlvIndex];
		int onlvDataOffset = checked(header.DataTableOffset + onlv.DataOffset);
		if (onlv.DataSize < OnlvAisaOffset + 4)
		{
			throw new InvalidDataException("The ONLV instance is too small to contain an AISA slot.");
		}
		ValidateDescriptorStart(data, header, onlv, "ONLV");

		int aisaId = ReadInt32(data, onlvDataOffset + OnlvAisaOffset);
		int aisaIndex = aisaId >> 8;
		if (aisaId == 0)
		{
			aisaIndex = FindUniqueDescriptor(descriptors, AisaTag, "AISA");
			WriteInt32(data, onlvDataOffset + OnlvAisaOffset, MakeInstanceId(aisaIndex));
		}
		else if ((aisaId & 0xff) != 1 || aisaIndex < 0 || aisaIndex >= descriptors.Count)
		{
			throw new InvalidDataException("The ONLV AISA slot contains an invalid instance ID.");
		}

		Descriptor aisa = descriptors[aisaIndex];
		if (aisa.Tag != AisaTag)
		{
			throw new InvalidDataException("The ONLV AISA slot does not reference an AISA descriptor.");
		}

		string existingName = ReadDescriptorName(data, header, aisa);
		if ((aisa.Flags & PlaceholderFlag) != 0 && string.Equals(existingName, aisaName, StringComparison.Ordinal))
		{
			return;
		}

		aisa.DataOffset = 0;
		aisa.DataSize = 0;
		aisa.NameOffset = header.NameTableSize;
		aisa.Flags = (aisa.Flags & ~PrivateFlag) | PlaceholderFlag;

		byte[] output = header.Version == Version32
			? RebuildVersion32(data, header, descriptors, encodedName)
			: RebuildVersion31(data, header, descriptors, aisaIndex, encodedName);
		WriteAtomically(path, output);
	}

	private static byte[] RebuildVersion32(byte[] source, Header header, List<Descriptor> descriptors, byte[] encodedName)
	{
		int newNameSize = checked(header.NameTableSize + encodedName.Length + 1);
		int newNameOffset = Align32(checked(64 + descriptors.Count * DescriptorSize));
		int newDataOffset = Align32(checked(newNameOffset + newNameSize));
		int dataEnd = checked(header.DataTableOffset + header.DataTableSize);
		ValidateRange(source, header.NameTableOffset, header.NameTableSize);

		// Preserve actual bytes instead of requiring all header-declared trailing
		// padding; fresh OniSplit ONLV files can end before that padding.
		int oldTailOffset = source.Length;
		int dataCopySize = source.Length - header.DataTableOffset;
		int newRawOffset = 0;
		if (header.RawTableOffset != 0)
		{
			if (header.RawTableOffset < header.DataTableOffset || header.RawTableOffset > source.Length)
			{
				throw new InvalidDataException("The ONLV raw table offset is invalid.");
			}
			oldTailOffset = header.RawTableOffset;
			dataCopySize = oldTailOffset - header.DataTableOffset;
			newRawOffset = Align32(checked(newDataOffset + header.DataTableSize));
		}

		int newTailOffset = newRawOffset != 0 ? newRawOffset : checked(newDataOffset + header.DataTableSize);
		byte[] output = new byte[checked(newTailOffset + source.Length - oldTailOffset)];
		Buffer.BlockCopy(source, 0, output, 0, 64);
		WriteDescriptors(output, descriptors);
		Buffer.BlockCopy(source, header.NameTableOffset, output, newNameOffset, header.NameTableSize);
		Buffer.BlockCopy(encodedName, 0, output, newNameOffset + header.NameTableSize, encodedName.Length);
		Buffer.BlockCopy(source, header.DataTableOffset, output, newDataOffset, dataCopySize);
		Buffer.BlockCopy(source, oldTailOffset, output, newTailOffset, source.Length - oldTailOffset);

		WriteInt32(output, 32, newDataOffset);
		WriteInt32(output, 40, newNameOffset);
		WriteInt32(output, 44, newNameSize);
		WriteInt32(output, 48, newRawOffset);
		return output;
	}

	private static byte[] RebuildVersion31(byte[] source, Header header, List<Descriptor> descriptors, int aisaIndex, byte[] encodedName)
	{
		int oldAuxiliaryOffset = checked(64 + descriptors.Count * DescriptorSize);
		int oldAuxiliarySize = checked(header.NameCount * 8 + header.TemplateCount * 16);
		ValidateRange(source, oldAuxiliaryOffset, oldAuxiliarySize);
		ValidateRange(source, header.DataTableOffset, header.DataTableSize);
		ValidateRange(source, header.NameTableOffset, header.NameTableSize);

		List<int> namedIndices = new List<int>();
		Dictionary<int, string> names = new Dictionary<int, string>();
		for (int i = 0; i < descriptors.Count; i++)
		{
			if (!descriptors[i].HasName)
			{
				continue;
			}
			string name = i == aisaIndex
				? Encoding.UTF8.GetString(encodedName)
				: ReadDescriptorName(source, header, descriptors[i]);
			if (name == null)
			{
				throw new InvalidDataException("The ONLV name table is invalid.");
			}
			namedIndices.Add(i);
			names.Add(i, name);
		}
		namedIndices.Sort(delegate(int left, int right) { return string.CompareOrdinal(names[left], names[right]); });

		int newNameCount = namedIndices.Count;
		int newAuxiliarySize = checked(newNameCount * 8 + header.TemplateCount * 16);
		int newDataOffset = Align32(checked(oldAuxiliaryOffset + newAuxiliarySize));
		int newNameOffset = Align32(checked(newDataOffset + header.DataTableSize));
		int newNameSize = checked(header.NameTableSize + encodedName.Length + 1);
		int oldNameEnd = checked(header.NameTableOffset + header.NameTableSize);
		byte[] output = new byte[checked(newNameOffset + newNameSize + source.Length - oldNameEnd)];
		Buffer.BlockCopy(source, 0, output, 0, 64);
		WriteDescriptors(output, descriptors);

		int auxiliaryWriteOffset = oldAuxiliaryOffset;
		foreach (int index in namedIndices)
		{
			WriteInt32(output, auxiliaryWriteOffset, index);
			auxiliaryWriteOffset += 8;
		}
		int oldTemplateOffset = checked(oldAuxiliaryOffset + header.NameCount * 8);
		Buffer.BlockCopy(source, oldTemplateOffset, output, auxiliaryWriteOffset, header.TemplateCount * 16);
		Buffer.BlockCopy(source, header.DataTableOffset, output, newDataOffset, header.DataTableSize);
		Buffer.BlockCopy(source, header.NameTableOffset, output, newNameOffset, header.NameTableSize);
		Buffer.BlockCopy(encodedName, 0, output, newNameOffset + header.NameTableSize, encodedName.Length);
		Buffer.BlockCopy(source, oldNameEnd, output, newNameOffset + newNameSize, source.Length - oldNameEnd);

		WriteInt32(output, 24, newNameCount);
		WriteInt32(output, 32, newDataOffset);
		WriteInt32(output, 40, newNameOffset);
		WriteInt32(output, 44, newNameSize);
		return output;
	}

	private static string ReadDescriptorName(byte[] data, Header header, Descriptor descriptor)
	{
		if (!descriptor.HasName || descriptor.NameOffset < 0 || descriptor.NameOffset >= header.NameTableSize)
		{
			return null;
		}
		int offset = checked(header.NameTableOffset + descriptor.NameOffset);
		int end = offset;
		int tableEnd = checked(header.NameTableOffset + header.NameTableSize);
		while (end < tableEnd && data[end] != 0)
		{
			end++;
		}
		if (end == tableEnd)
		{
			throw new InvalidDataException("The ONLV name table contains an unterminated name.");
		}
		return new UTF8Encoding(false, true).GetString(data, offset, end - offset);
	}

	private static Header ReadHeader(byte[] data)
	{
		ValidateRange(data, 0, 64);
		Header header = new Header();
		header.Version = ReadInt32(data, 8);
		if (header.Version != Version31 && header.Version != Version32)
		{
			throw new InvalidDataException("The input is not a supported Oni instance file.");
		}
		header.InstanceCount = ReadInt32(data, 20);
		header.NameCount = ReadInt32(data, 24);
		header.TemplateCount = ReadInt32(data, 28);
		header.DataTableOffset = ReadInt32(data, 32);
		header.DataTableSize = ReadInt32(data, 36);
		header.NameTableOffset = ReadInt32(data, 40);
		header.NameTableSize = ReadInt32(data, 44);
		header.RawTableOffset = header.Version == Version32 ? ReadInt32(data, 48) : 0;
		if (header.InstanceCount <= 0 || header.NameTableSize < 0 || header.DataTableSize < 0)
		{
			throw new InvalidDataException("The Oni instance file header is invalid.");
		}
		return header;
	}

	private static List<Descriptor> ReadDescriptors(byte[] data, Header header)
	{
		ValidateRange(data, 64, checked(header.InstanceCount * DescriptorSize));
		List<Descriptor> descriptors = new List<Descriptor>(header.InstanceCount);
		for (int i = 0; i < header.InstanceCount; i++)
		{
			int offset = 64 + i * DescriptorSize;
			Descriptor descriptor = new Descriptor();
			descriptor.Tag = ReadInt32(data, offset);
			descriptor.DataOffset = ReadInt32(data, offset + 4);
			descriptor.NameOffset = ReadInt32(data, offset + 8);
			descriptor.DataSize = ReadInt32(data, offset + 12);
			descriptor.Flags = ReadInt32(data, offset + 16);
			descriptors.Add(descriptor);
		}
		return descriptors;
	}

	private static void ValidateTableRange(byte[] data, Header header, string name)
	{
		ValidateRange(data, header.DataTableOffset, header.DataTableSize,
			name + " data table (offset " + header.DataTableOffset + ", size " + header.DataTableSize + ")");
		ValidateRange(data, header.NameTableOffset, header.NameTableSize,
			name + " name table (offset " + header.NameTableOffset + ", size " + header.NameTableSize + ")");
	}

	private static void ValidateDescriptorRange(byte[] data, Header header, Descriptor descriptor, string name)
	{
		int absoluteOffset = checked(header.DataTableOffset + descriptor.DataOffset);
		ValidateRange(data, absoluteOffset, descriptor.DataSize,
			name + " descriptor data (absolute offset " + absoluteOffset + ", size " + descriptor.DataSize + ")");
	}

	private static void ValidateDescriptorStart(byte[] data, Header header, Descriptor descriptor, string name)
	{
		int absoluteOffset = checked(header.DataTableOffset + descriptor.DataOffset);
		ValidateRange(data, absoluteOffset, 1,
			name + " descriptor data start (absolute offset " + absoluteOffset + ")");
	}

	private static int FindDataDescriptor(List<Descriptor> descriptors, int tag, string name)
	{
		int result = -1;
		for (int i = 0; i < descriptors.Count; i++)
		{
			Descriptor descriptor = descriptors[i];
			if (descriptor.Tag == tag && descriptor.DataOffset != 0 && descriptor.DataSize != 0)
			{
				if (result != -1)
				{
					throw new InvalidDataException("The input contains multiple " + name + " instances.");
				}
				result = i;
			}
		}
		if (result == -1)
		{
			throw new InvalidDataException("The input does not contain an " + name + " instance.");
		}
		return result;
	}

	private static int FindUniqueDescriptor(List<Descriptor> descriptors, int tag, string name)
	{
		int result = -1;
		for (int i = 0; i < descriptors.Count; i++)
		{
			if (descriptors[i].Tag == tag)
			{
				if (result != -1)
				{
					throw new InvalidDataException("The input contains multiple " + name + " descriptors.");
				}
				result = i;
			}
		}
		if (result == -1)
		{
			throw new InvalidDataException("The input does not contain an " + name + " descriptor.");
		}
		return result;
	}

	private static void WriteDescriptors(byte[] data, List<Descriptor> descriptors)
	{
		for (int i = 0; i < descriptors.Count; i++)
		{
			int offset = 64 + i * DescriptorSize;
			Descriptor descriptor = descriptors[i];
			WriteInt32(data, offset, descriptor.Tag);
			WriteInt32(data, offset + 4, descriptor.DataOffset);
			WriteInt32(data, offset + 8, descriptor.NameOffset);
			WriteInt32(data, offset + 12, descriptor.DataSize);
			WriteInt32(data, offset + 16, descriptor.Flags);
		}
	}

	private static void WriteAtomically(string path, byte[] data)
	{
		string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			File.WriteAllBytes(temporaryPath, data);
			File.Replace(temporaryPath, path, null);
		}
		finally
		{
			if (File.Exists(temporaryPath))
			{
				File.Delete(temporaryPath);
			}
		}
	}

	private static int MakeInstanceId(int index)
	{
		return (index << 8) | 1;
	}

	private static int Align32(int value)
	{
		return checked((value + 31) & ~31);
	}

	private static int ReadInt32(byte[] data, int offset)
	{
		ValidateRange(data, offset, 4);
		return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
	}

	private static void WriteInt32(byte[] data, int offset, int value)
	{
		ValidateRange(data, offset, 4);
		data[offset] = (byte)value;
		data[offset + 1] = (byte)(value >> 8);
		data[offset + 2] = (byte)(value >> 16);
		data[offset + 3] = (byte)(value >> 24);
	}

	private static void ValidateRange(byte[] data, int offset, int length)
	{
		ValidateRange(data, offset, length, "instance data");
	}

	private static void ValidateRange(byte[] data, int offset, int length, string description)
	{
		if (offset < 0 || length < 0 || offset > data.Length - length)
		{
			long end = (long)offset + length;
			throw new InvalidDataException("The Oni instance file contains an invalid offset for " + description +
				": range " + offset + ".." + end + " exceeds file length " + data.Length + ".");
		}
	}
}
