using System.Collections.Generic;
using System.IO;

namespace Oni.Akira
{
	internal class AkiraImporter : Importer
	{
		private bool debug;

		public AkiraImporter(string[] args)
		{
			foreach (string text in args)
			{
				if (text == "-debug")
				{
					debug = true;
				}
			}
		}

		public override void Import(string filePath, string outputDirPath)
		{
			Import(new string[1] { filePath }, outputDirPath);
		}

		public void Import(IList<string> files, string outputDirPath)
		{
			Import(files, outputDirPath, Path.GetFileNameWithoutExtension(files[0]));
		}

		public void Import(IList<string> files, string outputDirPath, string name)
		{
			PolygonMesh mesh = AkiraDaeReader.Read(files);
			BeginImport();
			AkiraDatWriter.Write(mesh, this, name, debug);
			Write(outputDirPath, files[0]);
		}
	}
}
