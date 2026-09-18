using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Oni.Dae;
using Oni.Imaging;

namespace Oni.Motoko
{
	internal class TextureDaeWriter
	{
		private readonly string outputDirPath;

		private readonly bool allTextures;

		private readonly Dictionary<InstanceDescriptor, Material> materials = new Dictionary<InstanceDescriptor, Material>();

		private readonly HashSet<string> exportedTextureMetadata = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> envMapNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private int envMapMaterialCount;

		public int EnvMapCount
		{
			get { return envMapNames.Count; }
		}

		public int EnvMapMaterialCount
		{
			get { return envMapMaterialCount; }
		}

		public TextureDaeWriter(string outputDirPath)
			: this(outputDirPath, false)
		{
		}

		public TextureDaeWriter(string outputDirPath, bool allTextures)
		{
			this.outputDirPath = outputDirPath;
			this.allTextures = allTextures;
		}

		public Material WriteMaterial(InstanceDescriptor txmp)
		{
			Material value;
			if (!materials.TryGetValue(txmp, out value))
			{
				value = CreateMaterial(txmp);
				materials.Add(txmp, value);
			}
			return value;
		}

		private Material CreateMaterial(InstanceDescriptor txmp)
		{
			Texture texture = TextureDatReader.Read(txmp);
			string imageBaseName = Utils.CleanupTextureName(txmp.Name);
			string path = Path.Combine("images", imageBaseName + ".tga");
			TgaWriter.Write(texture.Surfaces[0], Path.Combine(outputDirPath, path));
			if (allTextures)
			{
				WriteTextureMetadata(txmp, imageBaseName);
			}
			string name = TextureNameToId(txmp);
			Image initFrom = new Image
			{
				FilePath = "./" + path.Replace('\\', '/'),
				Name = name
			};
			EffectSurface effectSurface = new EffectSurface(initFrom);
			EffectSampler effectSampler = new EffectSampler(effectSurface)
			{
				WrapS = (texture.WrapU ? EffectSamplerWrap.Wrap : EffectSamplerWrap.None),
				WrapT = (texture.WrapV ? EffectSamplerWrap.Wrap : EffectSamplerWrap.None)
			};
			EffectTexture effectTexture = new EffectTexture(effectSampler, "diffuse_TEXCOORD");
			Effect effect = new Effect
			{
				Name = name,
				DiffuseValue = effectTexture,
				TransparentValue = (texture.HasAlpha ? effectTexture : null),
				Parameters = 
				{
					new EffectParameter("surface", effectSurface),
					new EffectParameter("sampler", effectSampler)
				}
			};
			if (allTextures)
			{
				InstanceDescriptor envMap = TextureDatReader.ReadEnvMap(txmp);
				if (envMap != null)
				{
					AttachEnvMap(effect, envMap);
				}
			}
			return new Material
			{
				Name = name,
				Effect = effect
			};
		}

		private void AttachEnvMap(Effect effect, InstanceDescriptor envMap)
		{
			Texture texture = TextureDatReader.Read(envMap);
			string imageBaseName = Utils.CleanupTextureName(envMap.Name);
			string path = Path.Combine("images", imageBaseName + ".tga");
			TgaWriter.Write(texture.Surfaces[0], Path.Combine(outputDirPath, path));
			WriteTextureMetadata(envMap, imageBaseName);
			envMapNames.Add(envMap.FullName);
			envMapMaterialCount++;

			Image image = new Image
			{
				FilePath = "./" + path.Replace('\\', '/'),
				Name = TextureNameToId(envMap) + "_env"
			};
			EffectSurface surface = new EffectSurface(image);
			EffectSampler sampler = new EffectSampler(surface)
			{
				WrapS = (texture.WrapU ? EffectSamplerWrap.Wrap : EffectSamplerWrap.None),
				WrapT = (texture.WrapV ? EffectSamplerWrap.Wrap : EffectSamplerWrap.None)
			};
			EffectTexture envTexture = new EffectTexture(sampler, "diffuse_TEXCOORD")
			{
				Channel = EffectTextureChannel.Reflective
			};
			effect.Parameters.Add(new EffectParameter("env_surface", surface));
			effect.Parameters.Add(new EffectParameter("env_sampler", sampler));
			effect.Reflective.Value = envTexture;
			effect.TransparentValue = null;
		}

		private void WriteTextureMetadata(InstanceDescriptor txmp, string imageBaseName)
		{
			if (!exportedTextureMetadata.Add(txmp.FullName))
			{
				return;
			}
			string imagesDirectory = Path.Combine(outputDirPath, "images");
			Directory.CreateDirectory(imagesDirectory);
			string xmlPath = Path.Combine(imagesDirectory, Importer.EncodeFileName(txmp.FullName) + ".xml");
			XmlWriterSettings settings = new XmlWriterSettings
			{
				CloseOutput = true,
				Indent = true,
				IndentChars = "    "
			};
			using (XmlWriter writer = XmlWriter.Create(File.Create(xmlPath), settings))
			{
				writer.WriteStartElement("Oni");
				TextureXmlExporter.Export(txmp, writer, imagesDirectory, imageBaseName);
				writer.WriteEndElement();
			}
		}

		private static string TextureNameToId(InstanceDescriptor txmp)
		{
			string text = Utils.CleanupTextureName(txmp.Name);
			if (text.StartsWith("Iteration", StringComparison.Ordinal))
			{
				text = text.Substring(9);
				if (char.IsDigit(text[0]) && char.IsDigit(text[1]) && char.IsDigit(text[2]) && text[3] == '_')
				{
					text = text.Substring(4);
				}
			}
			return text;
		}
	}
}
