using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Oni
{
	internal sealed class SubtitleImporter : Importer
	{
		private List<byte> subtitles;

		private List<int> offsets;

		public override void Import(string filePath, string outputDirPath)
		{
			ReadTextFile(filePath);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
			BeginImport();
			WriteSUBT(fileNameWithoutExtension, subtitles.ToArray());
			Write(outputDirPath, filePath);
		}

		private void ReadTextFile(string filePath)
		{
			subtitles = new List<byte>();
			offsets = new List<int>();
			byte[] array = File.ReadAllBytes(filePath);
			int i = SkipPreamble(array);
			while (i < array.Length)
			{
				int num = i;
				for (; !IsNewLine(array, i) && array[i] != 61; i++)
				{
				}
				int num2 = i;
				if (IsNewLine(array, i))
				{
					continue;
				}
				i++;
				int num3 = i;
				for (; !IsNewLine(array, i); i++)
				{
				}
				int num4 = i;
				if (num2 > num)
				{
					offsets.Add(subtitles.Count);
					for (int j = num; j < num2; j++)
					{
						subtitles.Add(array[j]);
					}
					subtitles.Add(0);
					for (int k = num3; k < num4; k++)
					{
						subtitles.Add(array[k]);
					}
					subtitles.Add(0);
				}
				i = SkipNewLine(array, i);
			}
		}

		private static int SkipPreamble(byte[] data)
		{
			int num = CheckPreamble(data, Encoding.UTF8.GetPreamble());
			if (num > 0)
			{
				return num;
			}
			if (CheckPreamble(data, Encoding.Unicode.GetPreamble()) != 0 || CheckPreamble(data, Encoding.BigEndianUnicode.GetPreamble()) != 0 || CheckPreamble(data, Encoding.UTF32.GetPreamble()) != 0)
			{
				throw new InvalidDataException("UTF16/32 input text files are not supported.");
			}
			if (data.Length >= 4 && ((data[1] == 0 && data[3] == 0) || (data[0] == 0 && data[2] == 0)))
			{
				throw new InvalidDataException("UTF16/32 input text files are not supported.");
			}
			return 0;
		}

		private static int CheckPreamble(byte[] data, byte[] preamble)
		{
			if (data.Length < preamble.Length)
			{
				return 0;
			}
			for (int i = 0; i < preamble.Length; i++)
			{
				if (data[i] != preamble[i])
				{
					return 0;
				}
			}
			return preamble.Length;
		}

		private static bool IsNewLine(byte[] data, int offset)
		{
			if (offset >= data.Length)
			{
				return true;
			}
			return SkipNewLine(data, offset) > offset;
		}

		private static int SkipNewLine(byte[] data, int offset)
		{
			if (offset < data.Length)
			{
				if (data[offset] == 10)
				{
					offset++;
				}
				else if (data[offset] == 13)
				{
					offset++;
					if (offset < data.Length && data[offset] == 10)
					{
						offset++;
					}
				}
			}
			return offset;
		}

		private void WriteSUBT(string name, byte[] subtitles)
		{
			ImporterDescriptor importerDescriptor = CreateInstance(TemplateTag.SUBT, name);
			using (BinaryWriter binaryWriter = importerDescriptor.OpenWrite(16))
			{
				binaryWriter.Write(WriteRawPart(subtitles));
				binaryWriter.Write(offsets.Count);
				binaryWriter.Write(offsets.ToArray());
			}
		}
	}
}
