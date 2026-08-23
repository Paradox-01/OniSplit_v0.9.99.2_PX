using System;
using System.Collections.Generic;
using System.IO;
using Oni.Metadata;

namespace Oni
{
	internal sealed class InstanceFile
	{
		private readonly InstanceFileManager fileManager;

		private readonly string filePath;

		private InstanceFileHeader header;

		private Dictionary<int, int> rawParts;

		private Dictionary<int, int> sepParts;

		private string rawFilePath;

		private string sepFilePath;

		private List<InstanceDescriptor> descriptors;

		private IList<InstanceDescriptor> readOnlyDescriptors;

		public InstanceFileManager FileManager
		{
			get
			{
				return fileManager;
			}
		}

		public string FilePath
		{
			get
			{
				return filePath;
			}
		}

		public InstanceFileHeader Header
		{
			get
			{
				return header;
			}
		}

		public string RawFilePath
		{
			get
			{
				if (rawFilePath == null)
				{
					if (header.Version == 1448227633)
					{
						rawFilePath = Path.ChangeExtension(filePath, ".raw");
					}
					else
					{
						rawFilePath = filePath;
					}
				}
				return rawFilePath;
			}
		}

		public string SepFilePath
		{
			get
			{
				if (sepFilePath == null)
				{
					sepFilePath = Path.ChangeExtension(filePath, ".sep");
				}
				return sepFilePath;
			}
		}

		public IList<InstanceDescriptor> Descriptors
		{
			get
			{
				if (readOnlyDescriptors == null)
				{
					readOnlyDescriptors = descriptors.AsReadOnly();
				}
				return readOnlyDescriptors;
			}
		}

		private InstanceFile(InstanceFileManager fileManager, string filePath)
		{
			this.fileManager = fileManager;
			this.filePath = filePath;
		}

		public static InstanceFile Read(InstanceFileManager fileManager, string filePath)
		{
			InstanceFile instanceFile = new InstanceFile(fileManager, filePath);
			using (BinaryReader reader = new BinaryReader(filePath))
			{
				InstanceFileHeader instanceFileHeader = InstanceFileHeader.Read(reader);
				List<InstanceDescriptor> list = new List<InstanceDescriptor>(instanceFileHeader.InstanceCount);
				instanceFile.header = instanceFileHeader;
				instanceFile.descriptors = list;
				for (int i = 0; i < instanceFile.header.InstanceCount; i++)
				{
					list.Add(InstanceDescriptor.Read(instanceFile, reader, i));
				}
				Dictionary<int, string> names = ReadNames(instanceFileHeader, reader);
				for (int j = 0; j < instanceFile.header.InstanceCount; j++)
				{
					list[j].ReadName(names);
				}
			}
			foreach (InstanceDescriptor descriptor in instanceFile.descriptors)
			{
				if (descriptor.Template.Tag == TemplateTag.AGDB)
				{
					List<InstanceDescriptor> namedDescriptors = instanceFile.GetNamedDescriptors(TemplateTag.AKEV);
					if (namedDescriptors.Count == 1)
					{
						string name = "AGDB" + namedDescriptors[0].Name;
						descriptor.SetName(name);
						break;
					}
				}
			}
			return instanceFile;
		}

		private static Dictionary<int, string> ReadNames(InstanceFileHeader header, BinaryReader reader)
		{
			reader.Position = header.NameTableOffset;
			byte[] array = reader.ReadBytes(header.NameTableSize);
			int i = 0;
			Dictionary<int, string> dictionary = new Dictionary<int, string>(header.NameCount);
			char[] array2 = new char[64];
			int num;
			for (; i < array.Length; i += num + 1)
			{
				num = 0;
				while (true)
				{
					byte b = array[i + num];
					if (b == 0)
					{
						break;
					}
					array2[num++] = (char)b;
				}
				dictionary.Add(i, new string(array2, 0, num));
			}
			return dictionary;
		}

		public List<InstanceDescriptor> GetReferencedDescriptors(InstanceDescriptor descriptor)
		{
			List<InstanceDescriptor> list = new List<InstanceDescriptor>();
			list.Add(descriptor);
			Stack<InstanceDescriptor> stack = new Stack<InstanceDescriptor>();
			bool[] array = new bool[descriptors.Count];
			stack.Push(descriptor);
			array[descriptor.Index] = true;
			using (BinaryReader binaryReader = new BinaryReader(filePath))
			{
				LinkVisitor linkVisitor = new LinkVisitor(binaryReader);
				while (stack.Count > 0)
				{
					descriptor = stack.Pop();
					binaryReader.Position = descriptor.DataOffset;
					linkVisitor.Links.Clear();
					descriptor.Template.Type.Accept(linkVisitor);
					foreach (int link in linkVisitor.Links)
					{
						if (!array[link >> 8])
						{
							InstanceDescriptor descriptor2 = GetDescriptor(link);
							if (!descriptor2.IsPlaceholder && !descriptor2.HasName)
							{
								stack.Push(descriptor2);
							}
							list.Add(descriptor2);
							array[descriptor2.Index] = true;
						}
					}
				}
				return list;
			}
		}

		public int GetRawPartSize(int offset)
		{
			EnsureRawAndSepParts();
			return rawParts[offset];
		}

		public int GetSepPartSize(int offset)
		{
			EnsureRawAndSepParts();
			return sepParts[offset];
		}

		public BinaryReader GetRawReader(int offset)
		{
			return GetBinaryReader(offset, RawFilePath);
		}

		public BinaryReader GetSepReader(int offset)
		{
			return GetBinaryReader(offset, SepFilePath);
		}

		private void EnsureRawAndSepParts()
		{
			if (rawParts == null)
			{
				rawParts = new Dictionary<int, int>();
				sepParts = new Dictionary<int, int>();
				InstanceMetadata.GetRawAndSepParts(this, rawParts, sepParts);
			}
		}

		private BinaryReader GetBinaryReader(int offset, string binaryFilePath)
		{
			BinaryReader binaryReader = new BinaryReader(binaryFilePath);
			binaryReader.Position = offset + header.RawTableOffset;
			return binaryReader;
		}

		public InstanceDescriptor ResolveLink(int id)
		{
			return ResolveLink(id, null);
		}

		public InstanceDescriptor ResolveLink(int id, InstanceDescriptor sourceDescriptor)
		{
			InstanceDescriptor descriptor = GetDescriptor(id);
			if (descriptor == null || !descriptor.IsPlaceholder)
			{
				return descriptor;
			}
			if (!descriptor.HasName)
			{
				return null;
			}
			InstanceFile instanceFile = fileManager.FindInstance(descriptor.FullName, this);
			if (instanceFile == null || instanceFile == this)
			{
				string resourceName = Path.GetFileNameWithoutExtension(filePath);
				if (sourceDescriptor == null)
				{
					Console.Error.WriteLine("Cannot find instance '{0}'; requested by object resource '{1}' from '{2}'", descriptor.FullName, resourceName, filePath);
				}
				else
				{
					string sourceType = sourceDescriptor.Template == null ? "<unknown>" : sourceDescriptor.Template.Tag.ToString();
					string sourceName = sourceDescriptor.HasName ? sourceDescriptor.FullName : string.Format("{0} instance #{1}", sourceType, sourceDescriptor.Index);
					Console.Error.WriteLine("Cannot find instance '{0}'; requested by '{1}' in object resource '{2}' from '{3}'", descriptor.FullName, sourceName, resourceName, filePath);
				}
				return null;
			}
			if (instanceFile.header.Version == 1448227634)
			{
				return instanceFile.GetDescriptor(1);
			}
			foreach (InstanceDescriptor descriptor2 in instanceFile.descriptors)
			{
				if (descriptor2.HasName && descriptor2.FullName == descriptor.FullName)
				{
					return descriptor2;
				}
			}
			return null;
		}

		public InstanceDescriptor GetDescriptor(int id)
		{
			if (id == 0)
			{
				return null;
			}
			return descriptors[id >> 8];
		}

		public List<InstanceDescriptor> GetNamedDescriptors()
		{
			return descriptors.FindAll((InstanceDescriptor x) => x.HasName && !x.IsPlaceholder);
		}

		public List<InstanceDescriptor> GetNamedDescriptors(TemplateTag tag)
		{
			return descriptors.FindAll((InstanceDescriptor x) => x.Template.Tag == tag && x.HasName && !x.IsPlaceholder);
		}

		public List<InstanceDescriptor> GetPlaceholders()
		{
			return descriptors.FindAll((InstanceDescriptor x) => x.HasName && x.IsPlaceholder);
		}
	}
}
