using System;
using System.IO;

namespace Oni.Imaging
{
	internal class TgaHeader
	{
		private bool hasColorMap;

		private TgaImageType imageType;

		private int colorMapIndex;

		private int colorMapLength;

		private int colorMapEntrySize;

		private int width;

		private int height;

		private int pixelDepth;

		private int imageDescriptor;

		private bool xFlip;

		private bool yFlip;

		private bool hasAlpha;

		public TgaImageType ImageType
		{
			get
			{
				return imageType;
			}
		}

		public int Width
		{
			get
			{
				return width;
			}
		}

		public int Height
		{
			get
			{
				return height;
			}
		}

		public int PixelSize
		{
			get
			{
				return pixelDepth / 8;
			}
		}

		public bool XFlip
		{
			get
			{
				return xFlip;
			}
		}

		public bool YFlip
		{
			get
			{
				return yFlip;
			}
		}

		public static TgaHeader Read(BinaryReader reader)
		{
			int num = reader.ReadByte();
			TgaHeader tgaHeader = new TgaHeader();
			tgaHeader.hasColorMap = reader.ReadByte() != 0;
			tgaHeader.imageType = (TgaImageType)reader.ReadByte();
			tgaHeader.colorMapIndex = reader.ReadUInt16();
			tgaHeader.colorMapLength = reader.ReadUInt16();
			tgaHeader.colorMapEntrySize = reader.ReadByte();
			reader.ReadUInt16();
			reader.ReadUInt16();
			tgaHeader.width = reader.ReadUInt16();
			tgaHeader.height = reader.ReadUInt16();
			tgaHeader.pixelDepth = reader.ReadByte();
			tgaHeader.imageDescriptor = reader.ReadByte();
			if (!Enum.IsDefined(typeof(TgaImageType), tgaHeader.ImageType) || tgaHeader.ImageType == TgaImageType.None)
			{
				throw new NotSupportedException(string.Format("Unsupported TGA image type {0}", tgaHeader.ImageType));
			}
			if (tgaHeader.Width == 0 || tgaHeader.Height == 0)
			{
				throw new InvalidDataException($"Invalid TGA file {Path.GetFileName(reader.Name)}");
			}
			if (tgaHeader.ImageType == TgaImageType.TrueColor && tgaHeader.pixelDepth != 16 && tgaHeader.pixelDepth != 24 && tgaHeader.pixelDepth != 32)
			{
				throw new InvalidDataException(string.Format("Invalid true color pixel depth {0}", tgaHeader.pixelDepth));
			}
			if (tgaHeader.hasColorMap)
			{
				if (tgaHeader.colorMapEntrySize != 16 && tgaHeader.colorMapEntrySize != 24 && tgaHeader.colorMapEntrySize != 32)
				{
					throw new InvalidDataException(string.Format("Invalid color map entry size {0}", tgaHeader.colorMapEntrySize));
				}
				if (tgaHeader.ImageType != TgaImageType.ColorMapped && tgaHeader.ImageType != TgaImageType.RleColorMapped)
				{
					reader.Position += tgaHeader.colorMapLength * tgaHeader.colorMapEntrySize / 8;
				}
			}
			reader.Position += num;
			if (tgaHeader.pixelDepth == 32)
			{
				tgaHeader.hasAlpha = (tgaHeader.imageDescriptor & 0xF) == 8;
			}
			else if (tgaHeader.pixelDepth == 16)
			{
				tgaHeader.hasAlpha = (tgaHeader.imageDescriptor & 0xF) == 1;
			}
			else
			{
				tgaHeader.hasAlpha = false;
			}
			tgaHeader.xFlip = (tgaHeader.imageDescriptor & 0x10) == 16;
			tgaHeader.yFlip = (tgaHeader.imageDescriptor & 0x20) == 32;
			return tgaHeader;
		}

		public static TgaHeader Create(int width, int height, TgaImageType imageType)
		{
			return new TgaHeader
			{
				imageType = imageType,
				width = width,
				height = height,
				pixelDepth = 32,
				imageDescriptor = 8
			};
		}

		public void Write(BinaryWriter writer)
		{
			writer.Write((byte)0);
			writer.Write((byte)(hasColorMap ? 1u : 0u));
			writer.Write((byte)imageType);
			writer.Write((ushort)colorMapIndex);
			writer.Write((ushort)colorMapLength);
			writer.Write((byte)colorMapEntrySize);
			writer.Write((ushort)0);
			writer.Write((ushort)0);
			writer.Write((ushort)width);
			writer.Write((ushort)height);
			writer.Write((byte)pixelDepth);
			writer.Write((byte)imageDescriptor);
		}

		public SurfaceFormat GetSurfaceFormat()
		{
			switch (pixelDepth)
			{
			case 16:
				if (!hasAlpha)
				{
					return SurfaceFormat.BGRX5551;
				}
				return SurfaceFormat.BGRA5551;
			case 24:
				return SurfaceFormat.BGRX;
			default:
				if (!hasAlpha)
				{
					return SurfaceFormat.BGRX;
				}
				return SurfaceFormat.BGRA;
			}
		}

		public Color GetPixel(byte[] src, int srcOffset)
		{
			switch (pixelDepth)
			{
			case 16:
				if (hasAlpha)
				{
					return Color.ReadBgra5551(src, srcOffset);
				}
				return Color.ReadBgrx5551(src, srcOffset);
			case 24:
				return Color.ReadBgrx(src, srcOffset);
			default:
				if (hasAlpha)
				{
					return Color.ReadBgra(src, srcOffset);
				}
				return Color.ReadBgrx(src, srcOffset);
			}
		}
	}
}
