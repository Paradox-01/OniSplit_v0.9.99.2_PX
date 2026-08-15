using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Oni.Akira;
using Oni.Dae;
using Oni.Imaging;
using Oni.Metadata;

namespace Oni.Motoko
{
	internal class TextureImporter3
	{
		private class TexImporter : Importer
		{
			private readonly TextureImporter3 importer;

			private readonly TextureImporterOptions options;

			public TexImporter(TextureImporter3 importer, TextureImporterOptions options)
			{
				this.importer = importer;
				this.options = options;
				BeginImport();
			}

			public void Import()
			{
				List<Surface> list = new List<Surface>();
				string[] images = options.Images;
				foreach (string filePath in images)
				{
					list.Add(TextureUtils.LoadImage(filePath));
				}
				if (list.Count == 0)
				{
					throw new InvalidDataException("No images found. A texture must have at least one image.");
				}
				TextureFormat textureFormat;
				if (options.Format.HasValue)
				{
					textureFormat = options.Format.Value;
				}
				else
				{
					Surface surface = list[0];
					textureFormat = (surface.HasTransparentPixels() ? importer.defaultAlphaFormat : ((surface.Width % 4 != 0 || surface.Height % 4 != 0) ? importer.defaultFormat : importer.defaultSquareFormat));
				}
				int num = 0;
				int num2 = 0;
				foreach (Surface item in list)
				{
					if (num == 0)
					{
						num = item.Width;
					}
					else if (num != item.Width)
					{
						throw new NotSupportedException("All animation frames must have the same size.");
					}
					if (num2 == 0)
					{
						num2 = item.Height;
					}
					else if (num2 != item.Height)
					{
						throw new NotSupportedException("All animation frames must have the same size.");
					}
				}
				int num3 = options.Width;
				int num4 = options.Height;
				if (num3 == 0)
				{
					num3 = num;
				}
				else if (num3 > num)
				{
					throw new NotSupportedException("Cannot upscale images.");
				}
				if (num4 == 0)
				{
					num4 = num2;
				}
				else if (num4 > num2)
				{
					throw new NotSupportedException("Cannot upscale images.");
				}
				if (num3 > importer.maxSize || num4 > importer.maxSize)
				{
					if (num3 > num4)
					{
						num4 = importer.maxSize * num4 / num3;
						num3 = importer.maxSize;
					}
					else
					{
						num3 = importer.maxSize * num3 / num4;
						num4 = importer.maxSize;
					}
				}
				num3 = TextureUtils.RoundToPowerOf2(num3);
				num4 = TextureUtils.RoundToPowerOf2(num4);
				if (num3 != num || num4 != num2)
				{
					for (int j = 0; j < list.Count; j++)
					{
						list[j] = list[j].Resize(num3, num4);
					}
				}
				TextureFlags textureFlags = options.Flags | TextureFlags.HasMipMaps;
				textureFlags &= ~(TextureFlags.HasEnvMap | TextureFlags.SwapBytes);
				string environmentMap = options.EnvironmentMap;
				if (textureFormat != TextureFormat.RGBA)
				{
					textureFlags |= TextureFlags.SwapBytes;
				}
				if (!string.IsNullOrEmpty(environmentMap))
				{
					textureFlags |= TextureFlags.HasEnvMap;
				}
				string name = options.Name;
				int speed = options.Speed;
				for (int k = 0; k < list.Count; k++)
				{
					ImporterDescriptor importerDescriptor = CreateInstance(TemplateTag.TXMP, (k == 0) ? name : null);
					using (BinaryWriter binaryWriter = importerDescriptor.OpenWrite(128))
					{
						binaryWriter.Write((int)textureFlags);
						binaryWriter.WriteUInt16(num3);
						binaryWriter.WriteUInt16(num4);
						binaryWriter.Write((int)textureFormat);
						if (k == 0 && list.Count > 1)
						{
							binaryWriter.WriteInstanceId(list.Count);
						}
						else
						{
							binaryWriter.Write(0);
						}
						if (!string.IsNullOrEmpty(environmentMap))
						{
							binaryWriter.WriteInstanceId(list.Count + ((list.Count > 1) ? 1 : 0));
						}
						else
						{
							binaryWriter.Write(0);
						}
						binaryWriter.Write(base.RawWriter.Align32());
						binaryWriter.Skip(12);
						Surface surface2 = list[k];
						List<Surface> list2 = new List<Surface>(16);
						list2.Add(surface2);
						if ((textureFlags & TextureFlags.HasMipMaps) != TextureFlags.None)
						{
							int num5 = num3;
							int num6 = num4;
							Surface surface3 = surface2;
							while (num5 > 1 || num6 > 1)
							{
								num5 = Math.Max(num5 >> 1, 1);
								num6 = Math.Max(num6 >> 1, 1);
								surface3 = surface3.Resize(num5, num6);
								list2.Add(surface3);
							}
						}
						foreach (Surface item2 in list2)
						{
							Surface surface4 = item2.Convert(textureFormat.ToSurfaceFormat());
							base.RawWriter.Write(surface4.Data);
						}
					}
				}
				if (list.Count > 1)
				{
					ImporterDescriptor importerDescriptor2 = CreateInstance(TemplateTag.TXAN);
					using (BinaryWriter binaryWriter2 = importerDescriptor2.OpenWrite(12))
					{
						binaryWriter2.WriteInt16(speed);
						binaryWriter2.WriteInt16(speed);
						binaryWriter2.Write(0);
						binaryWriter2.Write(list.Count);
						binaryWriter2.Write(0);
						for (int l = 1; l < list.Count; l++)
						{
							binaryWriter2.WriteInstanceId(l);
						}
					}
				}
				if (!string.IsNullOrEmpty(environmentMap))
				{
					CreateInstance(TemplateTag.TXMP, environmentMap);
				}
			}
		}

		private readonly string outputDirPath;

		private readonly Dictionary<string, TextureImporterOptions> textures = new Dictionary<string, TextureImporterOptions>(StringComparer.Ordinal);

		private TextureFormat defaultFormat = TextureFormat.BGR;

		private TextureFormat defaultSquareFormat = TextureFormat.BGR;

		private TextureFormat defaultAlphaFormat = TextureFormat.RGBA;

		private int maxSize = 512;

		public TextureFormat DefaultFormat
		{
			get
			{
				return defaultFormat;
			}
			set
			{
				defaultFormat = value;
				defaultSquareFormat = value;
			}
		}

		public TextureFormat DefaultAlphaFormat
		{
			get
			{
				return defaultAlphaFormat;
			}
			set
			{
				defaultAlphaFormat = value;
			}
		}

		public int MaxSize
		{
			get
			{
				return maxSize;
			}
			set
			{
				maxSize = value;
			}
		}

		public TextureImporter3(string outputDirPath)
		{
			this.outputDirPath = outputDirPath;
		}

		public string AddMaterial(Oni.Dae.Material material, string meshName)
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
			if (sampler == null || sampler.Surface == null || sampler.Surface.InitFrom == null)
			{
				return null;
			}
			string fullPath = Path.GetFullPath(sampler.Surface.InitFrom.FilePath);
			if (!File.Exists(fullPath))
			{
				return null;
			}
			TgaMeshUsage.Register(fullPath, meshName);
			TextureImporterOptions options = GetOptions(Path.GetFileNameWithoutExtension(fullPath), fullPath);
			return options.Name;
		}

		public TextureImporterOptions AddMaterial(Oni.Akira.Material material)
		{
			return GetOptions(material.Name, material.ImageFilePath);
		}

		private TextureImporterOptions GetOptions(string name, string filePath)
		{
			TextureImporterOptions value;
			if (!textures.TryGetValue(name, out value))
			{
				TextureImporterOptions textureImporterOptions = new TextureImporterOptions();
				textureImporterOptions.Name = name;
				textureImporterOptions.Images = new string[1] { filePath };
				value = textureImporterOptions;
				textures.Add(name, value);
			}
			return value;
		}

		public TextureImporterOptions GetOptions(string name, bool create)
		{
			TextureImporterOptions value;
			if (!textures.TryGetValue(name, out value) & create)
			{
				value = new TextureImporterOptions
				{
					Name = name
				};
				textures.Add(name, value);
			}
			return value;
		}

		public void ReadOptions(XmlReader xml, string basePath)
		{
			TextureImporterOptions options = GetOptions(xml.GetAttribute("Name"), true);
			List<string> list = new List<string>();
			xml.ReadStartElement("Texture");
			while (xml.IsStartElement())
			{
				switch (xml.LocalName)
				{
				case "Width":
					options.Width = xml.ReadElementContentAsInt();
					break;
				case "Height":
					options.Height = xml.ReadElementContentAsInt();
					break;
				case "Format":
					options.Format = TextureImporter.ParseTextureFormat(xml.ReadElementContentAsString());
					break;
				case "Flags":
					options.Flags = xml.ReadElementContentAsEnum<TextureFlags>();
					break;
				case "GunkFlags":
					options.GunkFlags = xml.ReadElementContentAsEnum<GunkFlags>();
					break;
				case "EnvMap":
					options.EnvironmentMap = xml.ReadElementContentAsString();
					break;
				case "Speed":
					options.Speed = xml.ReadElementContentAsInt();
					break;
				case "Image":
					list.Add(Path.Combine(basePath, xml.ReadElementContentAsString()));
					break;
				default:
					Console.Error.WriteLine("Unknown texture option {0}", xml.LocalName);
					xml.Skip();
					break;
				}
			}
			xml.ReadEndElement();
			options.Images = list.ToArray();
		}

		public void Write()
		{
			Parallel.ForEach(textures.Values, delegate(TextureImporterOptions options)
			{
				if (options.Images.Length != 0)
				{
					TexImporter texImporter = new TexImporter(this, options);
					texImporter.Import();
					texImporter.Write(outputDirPath);
				}
			});
			Console.WriteLine("Imported {0} textures", textures.Count);
		}
	}
}
