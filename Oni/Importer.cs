using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Oni
{
	internal abstract class Importer
	{
		private readonly ImporterFile file;

		private Dictionary<string, ImporterTask> dependencies;

		public ImporterFile ImporterFile
		{
			get
			{
				return file;
			}
		}

		public BinaryWriter RawWriter
		{
			get
			{
				return file.RawWriter;
			}
		}

		public ICollection<ImporterTask> Dependencies
		{
			get
			{
				if (dependencies == null)
				{
					return new ImporterTask[0];
				}
				return dependencies.Values;
			}
		}

		public Importer()
		{
			file = new ImporterFile();
		}

		protected Importer(long templateChecksum)
		{
			file = new ImporterFile(templateChecksum);
		}

		public virtual void Import(string filePath, string outputDirPath)
		{
		}

		public virtual void BeginImport()
		{
			file.BeginImport();
			dependencies = new Dictionary<string, ImporterTask>();
		}

		public void AddDependency(string filePath, TemplateTag type)
		{
			if (!dependencies.ContainsKey(filePath))
			{
				dependencies[filePath] = new ImporterTask(filePath, type);
			}
		}

		public ImporterDescriptor CreateInstance(TemplateTag tag, string name = null)
		{
			return file.CreateInstance(tag, name);
		}

		public int WriteRawPart(byte[] data)
		{
			return file.WriteRawPart(data);
		}

		public int WriteRawPart(string text)
		{
			return file.WriteRawPart(text);
		}

		public void Write(string outputDirPath)
		{
			file.Write(outputDirPath);
		}

		public void Write(string outputDirPath, string inputFilePath)
		{
			file.Write(outputDirPath, inputFilePath);
		}

		protected static string MakeInstanceName(TemplateTag tag, string name)
		{
			string text = tag.ToString();
			if (!name.StartsWith(text, StringComparison.Ordinal))
			{
				name = text + name;
			}
			return name;
		}

		public static string EncodeFileName(string name, Dictionary<string, string> fileNames = null)
		{
			char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
			for (int i = 0; i < invalidFileNameChars.Length; i++)
			{
				char c = invalidFileNameChars[i];
				name = name.Replace(c.ToString(), string.Format(CultureInfo.InvariantCulture, "%{0:X2}", new object[1] { (int)c }));
			}
			if (fileNames != null)
			{
				string value;
				while (fileNames.TryGetValue(name, out value))
				{
					int j = 0;
					if (name.Length > 4)
					{
						j = 4;
					}
					for (; j < name.Length && char.ToLowerInvariant(name[j]) != char.ToLowerInvariant(value[j]); j++)
					{
					}
					name = name.Substring(0, j) + string.Format(CultureInfo.InvariantCulture, "%{0:X2}", new object[1] { (int)name[j] }) + name.Substring(j + 1);
				}
				fileNames[name] = name;
			}
			return name;
		}

		public static string DecodeFileName(string fileName)
		{
			fileName = Path.GetFileNameWithoutExtension(fileName);
			StringBuilder stringBuilder = new StringBuilder();
			int num = 0;
			while (num != -1)
			{
				int num2 = fileName.IndexOf('%', num);
				if (num2 == -1)
				{
					stringBuilder.Append(fileName, num, fileName.Length - num);
				}
				else
				{
					stringBuilder.Append(fileName, num, num2 - num);
					stringBuilder.Append((char)int.Parse(fileName.Substring(num2 + 1, 2), NumberStyles.HexNumber));
					num2 += 3;
				}
				num = num2;
			}
			return stringBuilder.ToString();
		}
	}
}
