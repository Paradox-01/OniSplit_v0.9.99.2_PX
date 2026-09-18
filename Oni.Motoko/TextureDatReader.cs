using System;
using Oni.Imaging;

namespace Oni.Motoko
{
	internal static class TextureDatReader
	{
		public static Texture ReadInfo(InstanceDescriptor txmp)
		{
			Texture texture = new Texture
			{
				Name = txmp.Name
			};
			using (BinaryReader binaryReader = txmp.OpenRead(128))
			{
				texture.Flags = (TextureFlags)binaryReader.ReadInt32();
				texture.Width = binaryReader.ReadInt16();
				texture.Height = binaryReader.ReadInt16();
				texture.Format = (TextureFormat)binaryReader.ReadInt32();
				binaryReader.Skip(8);
				if (txmp.IsMacFile)
				{
					binaryReader.Skip(4);
				}
				binaryReader.Skip(4);
				return texture;
			}
		}

		public static InstanceDescriptor ReadEnvMap(InstanceDescriptor txmp)
		{
			using (BinaryReader binaryReader = txmp.OpenRead(128))
			{
				binaryReader.Skip(12);
				binaryReader.ReadInstance();
				InstanceDescriptor envMap = binaryReader.ReadInstance();
				if (envMap == null || !envMap.IsPlaceholder)
				{
					return envMap;
				}
				InstanceFile file = txmp.File.FileManager.FindInstance(envMap.FullName, txmp.File);
				return file == null ? null : file.Descriptors[0];
			}
		}

		public static Texture Read(InstanceDescriptor txmp)
		{
			Texture texture = new Texture
			{
				Name = txmp.Name
			};
			int offset;
			using (BinaryReader binaryReader = txmp.OpenRead(128))
			{
				texture.Flags = (TextureFlags)binaryReader.ReadInt32();
				texture.Width = binaryReader.ReadInt16();
				texture.Height = binaryReader.ReadInt16();
				texture.Format = (TextureFormat)binaryReader.ReadInt32();
				binaryReader.Skip(8);
				if (txmp.IsMacFile)
				{
					binaryReader.Skip(4);
				}
				offset = binaryReader.ReadInt32();
			}
			using (BinaryReader reader = txmp.GetSepReader(offset))
			{
				ReadSurfaces(texture, reader);
				return texture;
			}
		}

		private static void ReadSurfaces(Texture texture, BinaryReader reader)
		{
			SurfaceFormat format = texture.Format.ToSurfaceFormat();
			int num = texture.Width;
			int num2 = texture.Height;
			bool flag = (texture.Flags & TextureFlags.HasMipMaps) != 0;
			do
			{
				Surface surface = new Surface(num, num2, format);
				reader.Read(surface.Data, 0, surface.Data.Length);
				texture.Surfaces.Add(surface);
				num = Math.Max(num >> 1, 1);
				num2 = Math.Max(num2 >> 1, 1);
			}
			while (flag && (num > 1 || num2 > 1));
		}
	}
}
