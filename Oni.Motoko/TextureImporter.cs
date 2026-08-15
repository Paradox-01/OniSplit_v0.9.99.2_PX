using System;
using System.Collections.Generic;
using System.IO;
using Oni.Imaging;

namespace Oni.Motoko
{
	internal class TextureImporter : Importer
	{
		private static readonly int[] powersOf2 = new int[11]
		{
			1, 2, 4, 8, 16, 32, 64, 128, 256, 512,
			1024
		};

		private static readonly Dictionary<string, TextureFormat> formatNames = new Dictionary<string, TextureFormat>(StringComparer.OrdinalIgnoreCase)
		{
			{
				"bgr32",
				TextureFormat.BGR
			},
			{
				"bgr",
				TextureFormat.BGR
			},
			{
				"bgra32",
				TextureFormat.RGBA
			},
			{
				"rgba",
				TextureFormat.RGBA
			},
			{
				"bgra4444",
				TextureFormat.BGRA4444
			},
			{
				"bgr555",
				TextureFormat.BGR555
			},
			{
				"bgra5551",
				TextureFormat.BGRA5551
			},
			{
				"dxt1",
				TextureFormat.DXT1
			}
		};

		private readonly bool allowLargeTextures;

		private readonly bool noMipMaps;

		private readonly TextureFlags defaultFlags;

		private readonly TextureFormat? defaultFormat;

		private readonly string envmapName;

		public static TextureFormat ParseTextureFormat(string name)
		{
			TextureFormat value;
			if (!formatNames.TryGetValue(name, out value))
			{
				throw new FormatException(string.Format("Invalid texture format '{0}'", name));
			}
			return value;
		}

		public TextureImporter(TextureImporterOptions options)
		{
			if (options != null)
			{
				defaultFormat = options.Format;
				envmapName = options.EnvironmentMap;
				defaultFlags = options.Flags & ~TextureFlags.HasMipMaps;
				noMipMaps = (options.Flags & TextureFlags.HasMipMaps) == 0;
				allowLargeTextures = true;
			}
		}

		public TextureImporter(string[] args)
		{
			foreach (string text in args)
			{
				if (text.StartsWith("-format:", StringComparison.Ordinal))
				{
					string text2 = text.Substring(8);
					TextureFormat value;
					if (!formatNames.TryGetValue(text2, out value))
					{
						throw new NotSupportedException(string.Format("Unknown texture format {0}", text2));
					}
					defaultFormat = value;
					continue;
				}
				if (text.StartsWith("-envmap:", StringComparison.Ordinal))
				{
					string text3 = text.Substring(8);
					if (text3.Length > 0)
					{
						envmapName = text3;
					}
					continue;
				}
				switch (text)
				{
				case "-large":
					allowLargeTextures = true;
					break;
				case "-nouwrap":
					defaultFlags |= TextureFlags.NoUWrap;
					break;
				case "-novwrap":
					defaultFlags |= TextureFlags.NoVWrap;
					break;
				case "-nomipmaps":
					noMipMaps = true;
					break;
				}
			}
		}

		public override void Import(string filePath, string outputDirPath)
		{
			Texture texture = new Texture
			{
				Name = Importer.DecodeFileName(filePath),
				Flags = defaultFlags
			};
			LoadImage(texture, filePath);
			if (envmapName != null)
			{
				texture.EnvMap = new Texture();
				texture.EnvMap.Name = envmapName;
			}
			BeginImport();
			TextureDatWriter.Write(texture, this);
			Write(outputDirPath, filePath);
		}

		private void LoadImage(Texture texture, string filePath)
		{
			List<Surface> list = new List<Surface>();
			switch (Path.GetExtension(filePath).ToLowerInvariant())
			{
			case ".tga":
				list.Add(TgaReader.Read(filePath));
				break;
			case ".dds":
				list.AddRange(DdsReader.Read(filePath, noMipMaps));
				break;
			default:
				list.Add(SysReader.Read(filePath));
				break;
			}
			if (list.Count == 0)
			{
				throw new InvalidDataException(string.Format("Could not load image '{0}'", filePath));
			}
			Surface surface = list[0];
			if (Array.IndexOf(powersOf2, surface.Width) == -1)
			{
				Console.Error.WriteLine("Warning: Texture '{0}' width is not a power of 2", filePath);
			}
			if (Array.IndexOf(powersOf2, surface.Height) == -1)
			{
				Console.Error.WriteLine("Warning: Texture '{0}' height is not a power of 2", filePath);
			}
			if (list.Count == 1)
			{
				surface.CleanupAlpha();
			}
			if (surface.Format == SurfaceFormat.DXT1 && defaultFormat.HasValue && defaultFormat != TextureFormat.DXT1)
			{
				for (int i = 0; i < list.Count; i++)
				{
					list[i] = list[i].Convert(SurfaceFormat.RGBA);
				}
				surface = list[0];
			}
			if (!allowLargeTextures && (surface.Width > 256 || surface.Height > 256))
			{
				if (list.Count == 1)
				{
					int val = surface.Width / 256;
					int val2 = surface.Height / 256;
					int num = Math.Max(val, val2);
					surface = (list[0] = surface.Resize(surface.Width / num, surface.Height / num));
				}
				else
				{
					while (list.Count > 0 && (list[0].Width > 256 || list[0].Height > 256))
					{
						list.RemoveAt(0);
					}
					surface = list[0];
				}
			}
			if (list.Count == 1 && !noMipMaps && surface.Format != SurfaceFormat.DXT1)
			{
				Surface surface3 = surface;
				while (surface3.Width > 1 || surface3.Height > 1)
				{
					int newWidth = Math.Max(surface3.Width >> 1, 1);
					int newHeight = Math.Max(surface3.Height >> 1, 1);
					surface3 = surface3.Resize(newWidth, newHeight);
					list.Add(surface3);
				}
			}
			SurfaceFormat surfaceFormat = surface.Format;
			if (defaultFormat.HasValue)
			{
				texture.Format = defaultFormat.Value;
				surfaceFormat = texture.Format.ToSurfaceFormat();
			}
			else
			{
				switch (surface.Format)
				{
				case SurfaceFormat.BGRA4444:
					texture.Format = TextureFormat.BGRA4444;
					break;
				case SurfaceFormat.BGRX5551:
					texture.Format = TextureFormat.BGR555;
					break;
				case SurfaceFormat.BGR565:
					texture.Format = TextureFormat.BGR555;
					break;
				case SurfaceFormat.BGRA5551:
					texture.Format = TextureFormat.BGRA5551;
					break;
				case SurfaceFormat.BGRX:
				case SurfaceFormat.RGBX:
					texture.Format = TextureFormat.BGR;
					surfaceFormat = SurfaceFormat.BGRX;
					break;
				case SurfaceFormat.BGRA:
				case SurfaceFormat.RGBA:
					texture.Format = TextureFormat.BGRA4444;
					surfaceFormat = SurfaceFormat.BGRA4444;
					break;
				case SurfaceFormat.DXT1:
					texture.Format = TextureFormat.DXT1;
					break;
				default:
					throw new NotSupportedException(string.Format("Image format {0} cannot be imported", surface.Format));
				}
			}
			if (surfaceFormat != surface.Format)
			{
				for (int j = 0; j < list.Count; j++)
				{
					list[j] = list[j].Convert(surfaceFormat);
				}
				surface = list[0];
			}
			if (texture.Format != TextureFormat.RGBA)
			{
				texture.Flags |= TextureFlags.SwapBytes;
			}
			texture.Width = surface.Width;
			texture.Height = surface.Height;
			texture.Surfaces.AddRange(list);
		}
	}
}
