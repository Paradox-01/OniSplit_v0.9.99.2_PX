using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Oni.Dae;

namespace Oni.Motoko
{
	internal class GeometryImporter : Importer
	{
		private readonly bool generateNormals;

		private readonly bool flatNormals;

		private readonly float shellOffset;

		private readonly bool overrideTextureName;

		private string textureName;

		public GeometryImporter(string[] args)
		{
			foreach (string text in args)
			{
				switch (text)
				{
				case "-normals":
					generateNormals = true;
					break;
				case "-flat":
					flatNormals = true;
					break;
				default:
					if (!text.StartsWith("-cel:", StringComparison.Ordinal))
					{
						if (text.StartsWith("-tex:", StringComparison.Ordinal))
						{
							textureName = text.Substring(5);
							overrideTextureName = true;
						}
						break;
					}
					goto case "-cel";
				case "-cel":
				{
					int num = text.IndexOf(':');
					if (num != -1)
					{
						shellOffset = float.Parse(text.Substring(num + 1), CultureInfo.InvariantCulture);
					}
					else
					{
						shellOffset = 0.07f;
					}
					break;
				}
				}
			}
		}

		public ImporterDescriptor Import(string filePath, ImporterFile importer)
		{
			Scene scene = Reader.ReadFile(filePath);
			FaceConverter.Triangulate(scene);
			List<GeometryInstance> list = new List<GeometryInstance>();
			foreach (Node node in scene.Nodes)
			{
				GeometryInstance geometryInstance = node.GeometryInstances.FirstOrDefault();
				if (geometryInstance != null && geometryInstance.Target != null)
				{
					list.Add(geometryInstance);
				}
			}
			using (List<GeometryInstance>.Enumerator enumerator2 = list.GetEnumerator())
			{
				if (enumerator2.MoveNext())
				{
					GeometryInstance current2 = enumerator2.Current;
					Oni.Dae.Geometry target = current2.Target;
					Geometry geometry = GeometryDaeReader.Read(target, generateNormals, flatNormals, shellOffset);
					string path = null;
					if (!overrideTextureName && current2.Materials.Count > 0)
					{
						path = ReadMaterial(current2.Materials[0].Target);
					}
					geometry.TextureName = Path.GetFileNameWithoutExtension(path);
					return GeometryDatWriter.Write(geometry, importer);
				}
			}
			return null;
		}

		public override void Import(string filePath, string outputDirPath)
		{
			Scene scene = Reader.ReadFile(filePath);
			FaceConverter.Triangulate(scene);
			List<GeometryInstance> list = new List<GeometryInstance>();
			foreach (Node node in scene.Nodes)
			{
				GeometryInstance geometryInstance = node.GeometryInstances.FirstOrDefault();
				if (geometryInstance != null && geometryInstance.Target != null)
				{
					list.Add(geometryInstance);
				}
			}
			foreach (GeometryInstance item in list)
			{
				Oni.Dae.Geometry target = item.Target;
				Geometry geometry = GeometryDaeReader.Read(target, generateNormals, flatNormals, shellOffset);
				geometry.Name = Path.GetFileNameWithoutExtension(filePath);
				if (list.Count > 1)
				{
					geometry.Name += target.Name;
				}
				string textureFilePath = null;
				if (!overrideTextureName && item.Materials.Count > 0)
				{
					textureFilePath = ReadMaterial(item.Materials[0].Target);
				}
				WriteM3GM(geometry, textureFilePath, outputDirPath, filePath);
			}
		}

		private void WriteM3GM(Geometry geometry, string textureFilePath, string outputDirPath, string inputFilePath)
		{
			geometry.Name = Importer.MakeInstanceName(TemplateTag.M3GM, geometry.Name);
			if (string.IsNullOrEmpty(textureFilePath))
			{
				geometry.TextureName = textureName;
			}
			else
			{
				geometry.TextureName = Path.GetFileNameWithoutExtension(textureFilePath);
			}
			BeginImport();
			GeometryDatWriter.Write(geometry, base.ImporterFile);
			Write(outputDirPath, inputFilePath);
		}

		private string ReadMaterial(Material material)
		{
			if (material == null || material.Effect == null)
			{
				return null;
			}
			EffectTexture effectTexture = material.Effect.Textures.FirstOrDefault((EffectTexture t) => t.Channel == EffectTextureChannel.Diffuse);
			if (effectTexture == null)
			{
				return null;
			}
			EffectSampler sampler = effectTexture.Sampler;
			if (sampler == null || sampler.Surface == null || sampler.Surface.InitFrom == null || string.IsNullOrEmpty(sampler.Surface.InitFrom.FilePath))
			{
				return null;
			}
			return sampler.Surface.InitFrom.FilePath;
		}
	}
}
