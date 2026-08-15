using System;
using System.IO;

namespace Oni.Sound
{
	internal class AifImporter : Importer
	{
		private const int fcc_ima4 = 1768775988;

		private static readonly byte[] sampleRate = new byte[10] { 64, 13, 172, 68, 0, 0, 0, 0, 0, 0 };

		public AifImporter()
			: base(1052091493724257L)
		{
		}

		public override void Import(string filePath, string outputDirPath)
		{
			AifFile aifFile = AifFile.FromFile(filePath);
			if (aifFile.Format != 1768775988)
			{
				Console.Error.WriteLine("Unsupported AIF compression (0x{0:X})", aifFile.Format);
				return;
			}
			if (!Utils.ArrayEquals(aifFile.SampleRate, sampleRate))
			{
				Console.Error.WriteLine("Unsupported sample rate");
				return;
			}
			if (aifFile.ChannelCount != 1 && aifFile.ChannelCount != 2)
			{
				Console.Error.WriteLine("Unsupported number of channels ({0})", aifFile.ChannelCount);
				return;
			}
			BeginImport();
			WriteSNDD(Path.GetFileNameWithoutExtension(filePath), aifFile);
			Write(outputDirPath, filePath);
		}

		private void WriteSNDD(string name, AifFile aif)
		{
			int value = (int)((float)aif.SampleFrames * 64f / 22050f * 60f);
			ImporterDescriptor importerDescriptor = CreateInstance(TemplateTag.SNDD, name);
			using (BinaryWriter binaryWriter = importerDescriptor.OpenWrite())
			{
				binaryWriter.Write((aif.ChannelCount == 1) ? 1 : 3);
				binaryWriter.Write(value);
				binaryWriter.Write(aif.SoundData.Length);
				binaryWriter.Write(WriteRawPart(aif.SoundData));
			}
		}
	}
}
