using System;
using System.IO;

namespace Oni.Totoro
{
	internal class BodySetImporter : Importer
	{
		private BodyDaeImporter bodyImporter;

		public BodySetImporter(string[] args)
		{
			bodyImporter = new BodyDaeImporter(args);
		}

		public override void Import(string filePath, string outputDirPath)
		{
			string text = Path.GetFileNameWithoutExtension(filePath);
			if (text.StartsWith("ONCC", StringComparison.Ordinal))
			{
				text = text.Substring(4);
			}
			BeginImport();
			ImporterDescriptor trbs = CreateInstance(TemplateTag.TRBS, text);
			ImporterDescriptor trcm = bodyImporter.Import(filePath, base.ImporterFile);
			WriteTRBS(trbs, trcm);
			Write(outputDirPath, filePath);
		}

		private void WriteTRBS(ImporterDescriptor trbs, ImporterDescriptor trcm)
		{
			using (BinaryWriter binaryWriter = trbs.OpenWrite())
			{
				for (int i = 0; i < 5; i++)
				{
					binaryWriter.Write(trcm);
				}
			}
		}
	}
}
