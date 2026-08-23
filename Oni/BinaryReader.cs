using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Oni.Imaging;

namespace Oni
{
	internal sealed class BinaryReader : IDisposable
	{
		private static readonly byte[] seekBuffer = new byte[512];

		private static readonly Encoding encoding = Encoding.UTF8;

		private const float rotationAngleScale = 9.587527E-05f;

		private FileStream stream;

		private byte[] buffer;

		private bool bigEndian;

		private InstanceFile instanceFile;

		private InstanceDescriptor sourceDescriptor;

		public string Name
		{
			get
			{
				return stream.Name;
			}
		}

		public int Length
		{
			get
			{
				return (int)stream.Length;
			}
		}

		public int Position
		{
			get
			{
				return (int)stream.Position;
			}
			set
			{
				int num = (int)stream.Position;
				int num2 = value - num;
				if (num2 != 0)
				{
					if (num2 > 0 && num2 <= seekBuffer.Length)
					{
						stream.Read(seekBuffer, 0, num2);
					}
					else
					{
						stream.Position = value;
					}
				}
			}
		}

		public BinaryReader(string filePath)
		{
			buffer = new byte[8];
			stream = File.OpenRead(filePath);
		}

		public BinaryReader(string filePath, bool bigEndian)
			: this(filePath)
		{
			this.bigEndian = bigEndian;
		}

		public BinaryReader(string filePath, InstanceFile instanceFile, InstanceDescriptor sourceDescriptor)
			: this(filePath)
		{
			this.instanceFile = instanceFile;
			this.sourceDescriptor = sourceDescriptor;
		}

		public void Dispose()
		{
			if (stream != null)
			{
				stream.Dispose();
			}
			stream = null;
			buffer = null;
		}

		public void Skip(int length)
		{
			Position += length;
		}

		public void SkipCString()
		{
			int num = 1;
			while (num != 0 && num != -1)
			{
				num = stream.ReadByte();
			}
		}

		public int Read(byte[] buffer, int offset, int length)
		{
			return stream.Read(buffer, offset, length);
		}

		public byte[] ReadBytes(int length)
		{
			byte[] array = new byte[length];
			int num = 0;
			while (length > 0)
			{
				int num2 = stream.Read(array, num, length);
				if (num2 == 0)
				{
					break;
				}
				num += num2;
				length -= num2;
			}
			if (num != array.Length)
			{
				byte[] array2 = new byte[num];
				Buffer.BlockCopy(array, 0, array2, 0, num);
				array = array2;
			}
			return array;
		}

		public byte ReadByte()
		{
			int num = stream.ReadByte();
			if (num == -1)
			{
				throw new EndOfStreamException();
			}
			return (byte)num;
		}

		public bool ReadBoolean()
		{
			return ReadByte() != 0;
		}

		public ushort ReadUInt16()
		{
			FillBuffer(2);
			if (bigEndian)
			{
				return (ushort)(buffer[1] | (buffer[0] << 8));
			}
			return (ushort)(buffer[0] | (buffer[1] << 8));
		}

		public ushort[] ReadUInt16Array(int length)
		{
			ushort[] array = new ushort[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReadUInt16();
			}
			return array;
		}

		public uint ReadUInt32()
		{
			FillBuffer(4);
			if (bigEndian)
			{
				return (uint)(buffer[3] | (buffer[2] << 8) | (buffer[1] << 16) | (buffer[0] << 24));
			}
			return (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24));
		}

		public ulong ReadUInt64()
		{
			FillBuffer(8);
			ulong num;
			ulong num2;
			if (bigEndian)
			{
				num = (uint)(buffer[3] | (buffer[2] << 8) | (buffer[1] << 16) | (buffer[0] << 24));
				num2 = (uint)(buffer[7] | (buffer[6] << 8) | (buffer[5] << 16) | (buffer[4] << 24));
			}
			else
			{
				num2 = (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24));
				num = (uint)(buffer[4] | (buffer[5] << 8) | (buffer[6] << 16) | (buffer[7] << 24));
			}
			return (num << 32) | num2;
		}

		public short ReadInt16()
		{
			return (short)ReadUInt16();
		}

		public short[] ReadInt16Array(int length)
		{
			short[] array = new short[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReadInt16();
			}
			return array;
		}

		public int ReadInt32()
		{
			return (int)ReadUInt32();
		}

		public int[] ReadInt32VarArray()
		{
			return ReadInt32Array(ReadInt32());
		}

		public int[] ReadInt32Array(int length)
		{
			int[] array = new int[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReadInt32();
			}
			return array;
		}

		public long ReadInt64()
		{
			return (long)ReadUInt64();
		}

		public unsafe float ReadSingle()
		{
			uint num = ReadUInt32();
			return *(float*)(&num);
		}

		public float[] ReadSingleArray(int length)
		{
			float[] array = new float[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReadSingle();
			}
			return array;
		}

		public unsafe double ReadDouble()
		{
			ulong num = ReadUInt64();
			return *(double*)(&num);
		}

		public Vector2 ReadVector2()
		{
			return new Vector2(ReadSingle(), ReadSingle());
		}

		public Vector2[] ReadVector2VarArray()
		{
			return ReadVector2Array(ReadInt32());
		}

		public Vector2[] ReadVector2Array(int length)
		{
			Vector2[] array = new Vector2[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReadVector2();
			}
			return array;
		}

		public Vector3 ReadVector3()
		{
			return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
		}

		public Vector3[] ReadVector3VarArray()
		{
			return ReadVector3Array(ReadInt32());
		}

		public Vector3[] ReadVector3Array(int length)
		{
			Vector3[] array = new Vector3[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReadVector3();
			}
			return array;
		}

		public Plane ReadPlane()
		{
			return new Plane(ReadVector3(), ReadSingle());
		}

		public Plane[] ReadPlaneVarArray()
		{
			return ReadPlaneArray(ReadInt32());
		}

		public Plane[] ReadPlaneArray(int length)
		{
			Plane[] array = new Plane[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReadPlane();
			}
			return array;
		}

		public Quaternion ReadQuaternion()
		{
			return new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), 0f - ReadSingle());
		}

		public Quaternion ReadCompressedQuaternion()
		{
			return Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)ReadInt16() * 9.587527E-05f) * Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)ReadInt16() * 9.587527E-05f) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)ReadInt16() * 9.587527E-05f);
		}

		public BoundingBox ReadBoundingBox()
		{
			return new BoundingBox(ReadVector3(), ReadVector3());
		}

		public Matrix ReadMatrix4x3()
		{
			Matrix result = default(Matrix);
			result.M11 = ReadSingle();
			result.M12 = ReadSingle();
			result.M13 = ReadSingle();
			result.M14 = 0f;
			result.M21 = ReadSingle();
			result.M22 = ReadSingle();
			result.M23 = ReadSingle();
			result.M24 = 0f;
			result.M31 = ReadSingle();
			result.M32 = ReadSingle();
			result.M33 = ReadSingle();
			result.M34 = 0f;
			result.M41 = ReadSingle();
			result.M42 = ReadSingle();
			result.M43 = ReadSingle();
			result.M44 = 1f;
			return result;
		}

		public Color ReadColor()
		{
			uint num = ReadUInt32();
			byte r = (byte)((num >> 16) & 0xFF);
			byte g = (byte)((num >> 8) & 0xFF);
			byte b = (byte)(num & 0xFF);
			byte a = (byte)((num >> 24) & 0xFF);
			return new Color(r, g, b, a);
		}

		public Color[] ReadColorArray(int length)
		{
			Color[] array = new Color[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReadColor();
			}
			return array;
		}

		public string ReadString(int maxLength)
		{
			byte[] array = ReadBytes(maxLength);
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] == 0)
				{
					return encoding.GetString(array, 0, i);
				}
			}
			return encoding.GetString(array);
		}

		public string ReadCString()
		{
			List<byte> list = new List<byte>(64);
			byte item;
			while ((item = ReadByte()) != 0)
			{
				list.Add(item);
			}
			return encoding.GetString(list.ToArray());
		}

		public InstanceDescriptor ReadInstance()
		{
			return instanceFile.ResolveLink(ReadInt32(), sourceDescriptor);
		}

		public InstanceDescriptor[] ReadInstanceArray(int length)
		{
			InstanceDescriptor[] array = new InstanceDescriptor[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReadInstance();
			}
			return array;
		}

		public InstanceDescriptor ReadLink()
		{
			return instanceFile.GetDescriptor(ReadInt32());
		}

		public InstanceDescriptor[] ReadLinkArray(int length)
		{
			InstanceDescriptor[] array = new InstanceDescriptor[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ReadLink();
			}
			return array;
		}

		private void FillBuffer(int count)
		{
			int num = 0;
			while (count > 0)
			{
				int num2 = stream.Read(buffer, num, count);
				if (num2 == 0)
				{
					throw new EndOfStreamException();
				}
				num += num2;
				count -= num2;
			}
		}
	}
}
