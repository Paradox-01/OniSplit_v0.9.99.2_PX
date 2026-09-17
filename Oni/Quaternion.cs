using System;

namespace Oni
{
	internal struct Quaternion : IEquatable<Quaternion>
	{
		public float X;

		public float Y;

		public float Z;

		public float W;

		private static readonly Quaternion identity = new Quaternion(0f, 0f, 0f, 1f);

		private Vector3 XYZ
		{
			get
			{
				return new Vector3(X, Y, Z);
			}
		}

		public static Quaternion Identity
		{
			get
			{
				return identity;
			}
		}

		public Quaternion(Vector3 xyz, float w)
		{
			X = xyz.X;
			Y = xyz.Y;
			Z = xyz.Z;
			W = w;
		}

		public Quaternion(float x, float y, float z, float w)
		{
			X = x;
			Y = y;
			Z = z;
			W = w;
		}

		public Quaternion(Vector4 xyzw)
		{
			X = xyzw.X;
			Y = xyzw.Y;
			Z = xyzw.Z;
			W = xyzw.W;
		}

		public static Quaternion CreateFromAxisAngle(Vector3 axis, float angle)
		{
			float x = angle * 0.5f;
			float num = FMath.Sin(x);
			float w = FMath.Cos(x);
			return new Quaternion(axis * num, w);
		}

		public void ToAxisAngle(out Vector3 axis, out float angle)
		{
			float num = FMath.Acos(W);
			float num2 = FMath.Sqrt(1f - W * W);
			if (num2 < 1E-05f)
			{
				axis = XYZ;
				angle = 0f;
			}
			else
			{
				axis = XYZ / num2;
				angle = num * 2f;
			}
		}

		public static Quaternion CreateFromEulerXYZ(float x, float y, float z)
		{
			x = MathHelper.ToRadians(x);
			y = MathHelper.ToRadians(y);
			z = MathHelper.ToRadians(z);
			return CreateFromAxisAngle(Vector3.UnitX, x) * CreateFromAxisAngle(Vector3.UnitY, y) * CreateFromAxisAngle(Vector3.UnitZ, z);
		}

		public static Quaternion CreateFromEulerRevXYZ(float x, float y, float z)
		{
			x = MathHelper.ToRadians(x);
			y = MathHelper.ToRadians(y);
			z = MathHelper.ToRadians(z);
			return CreateFromAxisAngle(Vector3.UnitZ, z) * CreateFromAxisAngle(Vector3.UnitY, y) * CreateFromAxisAngle(Vector3.UnitX, x);
		}

		public Vector3 ToEulerXYZ()
		{
			float num = 2f * (X * Y - W * Z);
			float num2 = 1f - 2f * (Y * Y + Z * Z);
			Vector3 result = default(Vector3);
			result.Z = 0f - MathHelper.ToDegrees((float)Math.Atan2(num, num2));
			float num3 = 2f * ((0f - W) * Y - X * Z);
			if (Math.Abs(num3) >= 1f)
			{
				result.Y = -90f * (float)Math.Sign(num3);
			}
			else
			{
				result.Y = 0f - MathHelper.ToDegrees((float)Math.Asin(num3));
			}
			float num4 = 2f * (Y * Z - W * X);
			float num5 = 1f - 2f * (X * X + Y * Y);
			result.X = 0f - MathHelper.ToDegrees((float)Math.Atan2(num4, num5));
			return result;
		}

		public Vector3 ToEulerRevXYZ()
		{
			float num = 2f * (Y * Z + W * X);
			float num2 = 1f - 2f * (X * X + Y * Y);
			Vector3 result = default(Vector3);
			result.X = MathHelper.ToDegrees((float)Math.Atan2(num, num2));
			float num3 = 2f * (W * Y - X * Z);
			if (Math.Abs(num3) >= 1f)
			{
				result.Y = 90f * (float)Math.Sign(num3);
			}
			else
			{
				result.Y = MathHelper.ToDegrees((float)Math.Asin(num3));
			}
			float num4 = 2f * (X * Y + W * Z);
			float num5 = 1f - 2f * (Y * Y + Z * Z);
			result.Z = MathHelper.ToDegrees((float)Math.Atan2(num4, num5));
			return result;
		}

		public static Quaternion CreateFromYawPitchRoll(float yaw, float pitch, float roll)
		{
			float x = roll * 0.5f;
			float num = FMath.Sin(x);
			float num2 = FMath.Cos(x);
			float x2 = pitch * 0.5f;
			float num3 = FMath.Sin(x2);
			float num4 = FMath.Cos(x2);
			float x3 = yaw * 0.5f;
			float num5 = FMath.Sin(x3);
			float num6 = FMath.Cos(x3);
			Quaternion result = default(Quaternion);
			result.X = num6 * num3 * num2 + num5 * num4 * num;
			result.Y = num5 * num4 * num2 - num6 * num3 * num;
			result.Z = num6 * num4 * num - num5 * num3 * num2;
			result.W = num6 * num4 * num2 + num5 * num3 * num;
			return result;
		}

		public static Quaternion CreateFromRotationMatrix(Matrix m)
		{
			float num = m.M11 + m.M22 + m.M33;
			Quaternion result = default(Quaternion);
			if (num > 0f)
			{
				float num2 = FMath.Sqrt(1f + num);
				float num3 = 0.5f / num2;
				result.X = (m.M23 - m.M32) * num3;
				result.Y = (m.M31 - m.M13) * num3;
				result.Z = (m.M12 - m.M21) * num3;
				result.W = num2 * 0.5f;
			}
			else if (m.M11 >= m.M22 && m.M11 >= m.M33)
			{
				float num4 = FMath.Sqrt(1f + m.M11 - m.M22 - m.M33);
				float num5 = 0.5f / num4;
				result.X = num4 * 0.5f;
				result.Y = (m.M12 + m.M21) * num5;
				result.Z = (m.M13 + m.M31) * num5;
				result.W = (m.M23 - m.M32) * num5;
			}
			else if (m.M22 > m.M33)
			{
				float num6 = FMath.Sqrt(1f - m.M11 + m.M22 - m.M33);
				float num7 = 0.5f / num6;
				result.X = (m.M21 + m.M12) * num7;
				result.Y = num6 * 0.5f;
				result.Z = (m.M32 + m.M23) * num7;
				result.W = (m.M31 - m.M13) * num7;
			}
			else
			{
				float num8 = FMath.Sqrt(1f - m.M11 - m.M22 + m.M33);
				float num9 = 0.5f / num8;
				result.X = (m.M31 + m.M13) * num9;
				result.Y = (m.M32 + m.M23) * num9;
				result.Z = num8 * 0.5f;
				result.W = (m.M12 - m.M21) * num9;
			}
			return result;
		}

		public static Quaternion Lerp(Quaternion q1, Quaternion q2, float amount, bool reduceTo360 = true)
		{
			float num = 1f - amount;
			if (reduceTo360 && Dot(q1, q2) < 0f)
			{
				amount = 0f - amount;
			}
			q1.X = num * q1.X + amount * q2.X;
			q1.Y = num * q1.Y + amount * q2.Y;
			q1.Z = num * q1.Z + amount * q2.Z;
			q1.W = num * q1.W + amount * q2.W;
			q1.Normalize();
			return q1;
		}

		public static Quaternion Slerp(Quaternion q1, Quaternion q2, float amount, bool reduceTo360 = true)
		{
			bool flag = false;
			double num = Dot(q1, q2);
			if (reduceTo360 && num < 0.0)
			{
				num = 0.0 - num;
				flag = true;
			}
			num = Math.Max(-1.0, Math.Min(1.0, num));
			double num2 = Math.Acos(num);
			double num3 = Math.Sin(num2);
			double num4;
			double num5;
			if (num3 > 0.005)
			{
				num4 = Math.Sin((double)(1f - amount) * num2) / num3;
				num5 = Math.Sin((double)amount * num2) / num3;
			}
			else
			{
				num4 = 1f - amount;
				num5 = amount;
			}
			if (flag)
			{
				num5 = 0.0 - num5;
			}
			return q1 * (float)num4 + q2 * (float)num5;
		}

		public static float Dot(Quaternion q1, Quaternion q2)
		{
			return q1.X * q2.X + q1.Y * q2.Y + q1.Z * q2.Z + q1.W * q2.W;
		}

		public static Quaternion operator +(Quaternion q1, Quaternion q2)
		{
			q1.X += q2.X;
			q1.Y += q2.Y;
			q1.Z += q2.Z;
			q1.W += q2.W;
			return q1;
		}

		public static Quaternion operator -(Quaternion q1, Quaternion q2)
		{
			q1.X -= q2.X;
			q1.Y -= q2.Y;
			q1.Z -= q2.Z;
			q1.W -= q2.W;
			return q1;
		}

		public static Quaternion operator *(Quaternion q1, Quaternion q2)
		{
			Quaternion result = default(Quaternion);
			result.X = q1.X * q2.W + q1.Y * q2.Z - q1.Z * q2.Y + q1.W * q2.X;
			result.Y = (0f - q1.X) * q2.Z + q1.Y * q2.W + q1.Z * q2.X + q1.W * q2.Y;
			result.Z = q1.X * q2.Y - q1.Y * q2.X + q1.Z * q2.W + q1.W * q2.Z;
			result.W = (0f - q1.X) * q2.X - q1.Y * q2.Y - q1.Z * q2.Z + q1.W * q2.W;
			return result;
		}

		public static Quaternion operator *(Quaternion q, float s)
		{
			q.X *= s;
			q.Y *= s;
			q.Z *= s;
			q.W *= s;
			return q;
		}

		public static bool operator ==(Quaternion q1, Quaternion q2)
		{
			return q1.Equals(q2);
		}

		public static bool operator !=(Quaternion q1, Quaternion q2)
		{
			return !q1.Equals(q2);
		}

		public static Quaternion Conjugate(Quaternion q)
		{
			q.X = 0f - q.X;
			q.Y = 0f - q.Y;
			q.Z = 0f - q.Z;
			return q;
		}

		public Quaternion Inverse()
		{
			float num = 1f / SquaredLength();
			Quaternion result = default(Quaternion);
			result.X = (0f - X) * num;
			result.Y = (0f - Y) * num;
			result.Z = (0f - Z) * num;
			result.W = W * num;
			return result;
		}

		public void Normalize()
		{
			float num = 1f / Length();
			X *= num;
			Y *= num;
			Z *= num;
			W *= num;
		}

		public float Length()
		{
			return FMath.Sqrt(SquaredLength());
		}

		public float SquaredLength()
		{
			return X * X + Y * Y + Z * Z + W * W;
		}

		public bool Equals(Quaternion other)
		{
			if (X == other.X && Y == other.Y && Z == other.Z)
			{
				return W == other.W;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is Quaternion)
			{
				return Equals((Quaternion)obj);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode() ^ W.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("{{{0} {1} {2} {3}}}", X, Y, Z, W);
		}

		public Matrix ToMatrix()
		{
			float num = X * X;
			float num2 = Y * Y;
			float num3 = Z * Z;
			float num4 = X * Y;
			float num5 = Z * W;
			float num6 = Z * X;
			float num7 = Y * W;
			float num8 = Y * Z;
			float num9 = X * W;
			Matrix result = default(Matrix);
			result.M11 = 1f - 2f * (num2 + num3);
			result.M12 = 2f * (num4 + num5);
			result.M13 = 2f * (num6 - num7);
			result.M14 = 0f;
			result.M21 = 2f * (num4 - num5);
			result.M22 = 1f - 2f * (num3 + num);
			result.M23 = 2f * (num8 + num9);
			result.M24 = 0f;
			result.M31 = 2f * (num6 + num7);
			result.M32 = 2f * (num8 - num9);
			result.M33 = 1f - 2f * (num2 + num);
			result.M34 = 0f;
			result.M41 = 0f;
			result.M42 = 0f;
			result.M43 = 0f;
			result.M44 = 1f;
			return result;
		}

		public Vector4 ToVector4()
		{
			return new Vector4(X, Y, Z, W);
		}
	}
}
