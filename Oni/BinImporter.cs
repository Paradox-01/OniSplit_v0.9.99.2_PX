using System;
using System.IO;
using Oni.Metadata;

namespace Oni
{
	internal class BinImporter : Importer
	{
		public override void Import(string filePath, string outputDirPath)
		{
			using (BinaryReader binaryReader = new BinaryReader(filePath))
			{
				int num = binaryReader.ReadInt32();
				if (!Enum.IsDefined(typeof(BinaryTag), num))
				{
					throw new NotSupportedException(string.Format(".bin file with tag '{0:x}' is unuspported", num));
				}
				BinaryTag binaryTag = (BinaryTag)num;
				string text = binaryTag.ToString();
				BeginImport();
				ImporterDescriptor importerDescriptor = CreateInstance(TemplateTag.BINA, text + Path.GetFileNameWithoutExtension(filePath));
				using (BinaryWriter binaryWriter = importerDescriptor.OpenWrite())
				{
					binaryWriter.Write(binaryReader.Length);
					binaryWriter.Write(32);
				}
				base.RawWriter.Write(num);
				base.RawWriter.Write(binaryReader.ReadBytes(binaryReader.Length - 4));
				Write(outputDirPath, filePath);
			}
		}
	}
}
