using System;
using System.Collections.Generic;
using System.IO;
using Oni.Akira;
using Oni.Dae;

namespace Oni.Physics
{
	internal class ObjectAnimationImporter : Importer
	{
		public ObjectAnimationImporter(string[] args)
		{
		}

		public override void Import(string filePath, string outputDirPath)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
			if (fileNameWithoutExtension.StartsWith("OBAN", StringComparison.Ordinal))
			{
				fileNameWithoutExtension = fileNameWithoutExtension.Substring(4);
			}
			Scene scene = Reader.ReadFile(filePath);
			ObjectDaeImporter objectDaeImporter = new ObjectDaeImporter(null, new Dictionary<string, AkiraDaeNodeProperties> { 
			{
				scene.Id,
				new ObjectDaeNodeProperties
				{
					HasPhysics = true,
					Animations = 
					{
						new ObjectAnimationClip()
					}
				}
			} });
			objectDaeImporter.Import(scene);
			BeginImport();
			foreach (ObjectNode node in objectDaeImporter.Nodes)
			{
				ObjectAnimation[] animations = node.Animations;
				foreach (ObjectAnimation animation in animations)
				{
					ObjectDatWriter.WriteAnimation(animation, this);
				}
			}
			Write(outputDirPath, filePath);
		}
	}
}
