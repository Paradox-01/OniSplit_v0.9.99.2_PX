using System;
using System.IO;

namespace Oni.Sound
{
	internal class WavImporter : Importer
	{
		public override void Import(string filePath, string outputDirPath)
		{
			WavFile wavFile = WavFile.FromFile(filePath);
			if (wavFile.Format != WavFormat.Pcm && wavFile.Format != WavFormat.Adpcm)
			{
				Console.Error.WriteLine("Unsupported WAV format (0x{0:X})", wavFile.Format);
				return;
			}
			if (wavFile.ChannelCount != 1 && wavFile.ChannelCount != 2)
			{
				Console.Error.WriteLine("Unsupported number of channels ({0})", wavFile.ChannelCount);
				return;
			}
			if (wavFile.SampleRate != 22050 && wavFile.SampleRate != 44100)
			{
				Console.Error.WriteLine("Unsupported sample rate ({0} Hz)", wavFile.SampleRate);
				return;
			}
			if (wavFile.ExtraData.Length > 32)
			{
				throw new NotSupportedException(string.Format("Unsupported wave format extra data size ({0})", wavFile.ExtraData.Length));
			}
			BeginImport();
			WriteSNDD(Path.GetFileNameWithoutExtension(filePath), wavFile);
			Write(outputDirPath, filePath);
		}

		private void WriteSNDD(string name, WavFile wav)
		{
			float num = (float)wav.SoundData.Length * 8f / (float)wav.BitsPerSample;
			num /= (float)wav.SampleRate;
			num /= (float)wav.ChannelCount;
			num *= 60f;
			ImporterDescriptor importerDescriptor = CreateInstance(TemplateTag.SNDD, name);
			using (BinaryWriter binaryWriter = importerDescriptor.OpenWrite(8))
			{
				binaryWriter.Write((short)wav.Format);
				binaryWriter.WriteInt16(wav.ChannelCount);
				binaryWriter.Write(wav.SampleRate);
				binaryWriter.Write(wav.AverageBytesPerSecond);
				binaryWriter.WriteInt16(wav.BlockAlign);
				binaryWriter.WriteInt16(wav.BitsPerSample);
				binaryWriter.WriteInt16(wav.ExtraData.Length);
				binaryWriter.Write(wav.ExtraData);
				binaryWriter.Skip(32 - wav.ExtraData.Length);
				binaryWriter.Write((short)num);
				binaryWriter.Write(wav.SoundData.Length);
				binaryWriter.Write(WriteRawPart(wav.SoundData));
			}
		}
	}
}
