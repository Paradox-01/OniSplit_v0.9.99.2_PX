using System;
using System.Collections.Generic;
using System.IO;
using Oni.Metadata;

namespace Oni
{
	internal sealed class InstanceDescriptor
	{
		private InstanceFile file;

		private string fullName;

		private int index;

		private Template template;

		private int dataOffset;

		private int nameOffset;

		private int dataSize;

		private InstanceDescriptorFlags flags;

		public InstanceFile File
		{
			get
			{
				return file;
			}
		}

		public int Index
		{
			get
			{
				return index;
			}
		}

		public string FullName
		{
			get
			{
				if (fullName == null)
				{
					fullName = index.ToString();
				}
				return fullName;
			}
		}

		public string Name
		{
			get
			{
				string text = FullName;
				if (text.StartsWith(Template.Tag.ToString(), StringComparison.Ordinal))
				{
					text = text.Substring(4);
				}
				return text;
			}
		}

		public Template Template
		{
			get
			{
				return template;
			}
		}

		public bool HasName
		{
			get
			{
				return (flags & InstanceDescriptorFlags.Private) == 0;
			}
		}

		public bool IsPlaceholder
		{
			get
			{
				if ((flags & InstanceDescriptorFlags.Placeholder) == 0 && dataSize != 0)
				{
					return dataOffset == 0;
				}
				return true;
			}
		}

		public int DataOffset
		{
			get
			{
				return file.Header.DataTableOffset + dataOffset;
			}
		}

		public int DataSize
		{
			get
			{
				return dataSize;
			}
		}

		internal bool IsMacFile
		{
			get
			{
				return file.Header.TemplateChecksum == 1052091493724257L;
			}
		}

		public long TemplateChecksum
		{
			get
			{
				return file.Header.TemplateChecksum;
			}
		}

		public string FilePath
		{
			get
			{
				return file.FilePath;
			}
		}

		internal static InstanceDescriptor Read(InstanceFile file, BinaryReader reader, int index)
		{
			InstanceMetadata metadata = InstanceMetadata.GetMetadata(file);
			InstanceDescriptor instanceDescriptor = new InstanceDescriptor
			{
				file = file,
				index = index,
				template = metadata.GetTemplate((TemplateTag)reader.ReadInt32()),
				dataOffset = reader.ReadInt32(),
				nameOffset = reader.ReadInt32(),
				dataSize = reader.ReadInt32(),
				flags = (InstanceDescriptorFlags)(reader.ReadInt32() & 0xFF)
			};
			if (instanceDescriptor.IsPlaceholder && !instanceDescriptor.HasName)
			{
				throw new InvalidDataException("Empty descriptors must have names");
			}
			return instanceDescriptor;
		}

		internal void ReadName(Dictionary<int, string> names)
		{
			if (!HasName)
			{
				return;
			}
			if (IsPlaceholder || file.Header.Version == 1448227633)
			{
				names.TryGetValue(nameOffset, out fullName);
				return;
			}
			fullName = Importer.DecodeFileName(file.FilePath);
			string text = template.Tag.ToString();
			if (!fullName.StartsWith(text, StringComparison.Ordinal))
			{
				fullName = text + fullName;
			}
		}

		internal void SetName(string newName)
		{
			flags &= ~InstanceDescriptorFlags.Private;
			fullName = newName;
		}

		public List<InstanceDescriptor> GetReferencedDescriptors()
		{
			return file.GetReferencedDescriptors(this);
		}

		internal BinaryReader OpenRead()
		{
			if (IsPlaceholder)
			{
				throw new InvalidOperationException();
			}
			return new BinaryReader(file.FilePath, file, this)
			{
				Position = DataOffset
			};
		}

		internal BinaryReader OpenRead(int offset)
		{
			if (IsPlaceholder)
			{
				throw new InvalidOperationException();
			}
			return new BinaryReader(file.FilePath, file, this)
			{
				Position = DataOffset + offset
			};
		}

		internal BinaryReader GetRawReader(int offset)
		{
			return file.GetRawReader(offset);
		}

		internal BinaryReader GetSepReader(int offset)
		{
			if (!IsMacFile)
			{
				return GetRawReader(offset);
			}
			return file.GetSepReader(offset);
		}

		public bool HasRawParts()
		{
			if (IsPlaceholder)
			{
				return false;
			}
			switch (template.Tag)
			{
			case TemplateTag.AGDB:
			case TemplateTag.AKVA:
			case TemplateTag.BINA:
			case TemplateTag.OSBD:
			case TemplateTag.SNDD:
			case TemplateTag.SUBT:
			case TemplateTag.TRAM:
			case TemplateTag.TXMP:
				return true;
			default:
				return false;
			}
		}
	}
}
