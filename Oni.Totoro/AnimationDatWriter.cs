using System;
using System.Collections.Generic;
using System.IO;

namespace Oni.Totoro
{
	internal class AnimationDatWriter
	{
		private class DatExtent
		{
			public readonly int Frame;

			public readonly AttackExtent Extent;

			public DatExtent(int frame, AttackExtent extent)
			{
				Frame = frame;
				Extent = extent;
			}
		}

		private class DatExtentInfo
		{
			public float MaxDistance;

			public float MinY = 1E+09f;

			public float MaxY = -1E+09f;

			public readonly DatExtentInfoFrame FirstExtent = new DatExtentInfoFrame();

			public readonly DatExtentInfoFrame MaxExtent = new DatExtentInfoFrame();
		}

		private class DatExtentInfoFrame
		{
			public int Frame = -1;

			public int Attack;

			public int AttackOffset;

			public Vector2 Location;

			public float Height;

			public float Length;

			public float MinY;

			public float MaxY;

			public float Angle;
		}

		private Animation animation;

		private List<DatExtent> extents;

		private DatExtentInfo extentInfo;

		private Importer importer;

		private BinaryWriter dat;

		private BinaryWriter raw;

		private AnimationDatWriter()
		{
		}

		public static void Write(Animation animation, Importer importer, BinaryWriter dat)
		{
			AnimationDatWriter animationDatWriter = new AnimationDatWriter();
			animationDatWriter.animation = animation;
			animationDatWriter.importer = importer;
			animationDatWriter.dat = dat;
			animationDatWriter.raw = importer.RawWriter;
			AnimationDatWriter animationDatWriter2 = animationDatWriter;
			animationDatWriter2.WriteAnimation();
		}

		private void WriteAnimation()
		{
			extentInfo = new DatExtentInfo();
			extents = new List<DatExtent>();
			if (animation.Attacks.Count > 0)
			{
				if (animation.Attacks[0].Extents.Count == 0)
				{
					GenerateExtentInfo();
				}
				foreach (Attack attack in animation.Attacks)
				{
					int start = attack.Start;
					foreach (AttackExtent extent in attack.Extents)
					{
						extents.Add(new DatExtent(start++, extent));
					}
				}
				GenerateExtentSummary();
			}
			List<List<KeyFrame>> list = animation.Rotations;
			int num = animation.FrameSize;
			if (num == 16 && (animation.Flags & AnimationFlags.Overlay) == 0)
			{
				list = CompressFrames(list);
				num = 6;
			}
			dat.Write(0);
			WriteRawArray(animation.Heights, delegate(float x)
			{
				raw.Write(x);
			});
			WriteRawArray(animation.Velocities, delegate(Vector2 x)
			{
				raw.Write(x);
			});
			WriteRawArray(animation.Attacks, Write);
			WriteRawArray(animation.SelfDamage, Write);
			WriteRawArray(animation.MotionBlur, Write);
			WriteRawArray(animation.Shortcuts, Write);
			WriteThrowInfo();
			WriteRawArray(animation.Footsteps, Write);
			WriteRawArray(animation.Particles, Write);
			WriteRawArray(animation.Positions, Write);
			WriteRotations(list, num);
			WriteRawArray(animation.Sounds, Write);
			dat.Write((int)animation.Flags);
			if (!string.IsNullOrEmpty(animation.DirectAnimations[0]))
			{
				dat.Write(importer.CreateInstance(TemplateTag.TRAM, animation.DirectAnimations[0]));
			}
			else
			{
				dat.Write(0);
			}
			if (!string.IsNullOrEmpty(animation.DirectAnimations[1]))
			{
				dat.Write(importer.CreateInstance(TemplateTag.TRAM, animation.DirectAnimations[1]));
			}
			else
			{
				dat.Write(0);
			}
			dat.Write((int)animation.OverlayUsedBones);
			dat.Write((int)animation.OverlayReplacedBones);
			dat.Write(animation.FinalRotation);
			dat.Write((ushort)animation.Direction);
			dat.WriteUInt16(animation.Vocalization);
			WriteExtentInfo();
			dat.Write(animation.Impact, 16);
			dat.WriteUInt16(animation.HardPause);
			dat.WriteUInt16(animation.SoftPause);
			dat.Write(animation.Sounds.Count);
			dat.Skip(6);
			dat.WriteUInt16(60);
			dat.WriteUInt16(num);
			dat.WriteUInt16((int)animation.Type);
			dat.WriteUInt16((int)animation.AimingType);
			dat.WriteUInt16((ushort)animation.FromState);
			dat.WriteUInt16((ushort)animation.ToState);
			dat.WriteUInt16(list.Count);
			dat.WriteUInt16(animation.Velocities.Count);
			dat.WriteUInt16(animation.Velocities.Count);
			dat.WriteUInt16((ushort)animation.Varient);
			dat.Skip(2);
			dat.WriteUInt16(animation.AtomicStart);
			dat.WriteUInt16(animation.AtomicEnd);
			dat.WriteUInt16(animation.InterpolationEnd);
			dat.WriteUInt16(animation.InterpolationMax);
			dat.WriteUInt16(animation.ActionFrame);
			dat.WriteUInt16(animation.FirstLevelAvailable);
			dat.WriteByte(animation.InvulnerableStart);
			dat.WriteByte(animation.InvulnerableEnd);
			dat.WriteByte(animation.Attacks.Count);
			dat.WriteByte(animation.SelfDamage.Count);
			dat.WriteByte(animation.MotionBlur.Count);
			dat.WriteByte(animation.Shortcuts.Count);
			dat.WriteByte(animation.Footsteps.Count);
			dat.WriteByte(animation.Particles.Count);
		}

		private void WriteRotations(List<List<KeyFrame>> rotations, int frameSize)
		{
			dat.Write(raw.Align32());
			ushort[] array = new ushort[rotations.Count];
			array[0] = (ushort)(rotations.Count * 2);
			for (int i = 1; i < array.Length; i++)
			{
				int num = array[i - 1];
				num += rotations[i - 1].Count * (frameSize + 1) - 1;
				if (num > 65535)
				{
					throw new InvalidDataException("Rotation data is too large for 16-bit indexing (bone " + i + " and above do not fit). Try -tolerance:0.1, -tolerance:0.2, etc.");
				}
				array[i] = (ushort)num;
			}
			raw.Write(array);
			foreach (List<KeyFrame> rotation in rotations)
			{
				foreach (KeyFrame item in rotation)
				{
					switch (frameSize)
					{
					case 6:
						raw.WriteInt16((short)Math.Round(item.Rotation.X / 180f * 32767.5f));
						raw.WriteInt16((short)Math.Round(item.Rotation.Y / 180f * 32767.5f));
						raw.WriteInt16((short)Math.Round(item.Rotation.Z / 180f * 32767.5f));
						break;
					case 16:
						raw.Write(new Quaternion(item.Rotation));
						break;
					}
					if (item != Utils.Last(rotation))
					{
						raw.WriteByte(item.Duration);
					}
				}
			}
		}

		private void WriteThrowInfo()
		{
			if (animation.ThrowSource == null)
			{
				dat.Write(0);
				return;
			}
			dat.Write(raw.Align32());
			raw.Write(animation.ThrowSource.Position);
			raw.Write(animation.ThrowSource.Angle);
			raw.Write(animation.ThrowSource.Distance);
			raw.WriteUInt16((int)animation.ThrowSource.Type);
		}

		private void WriteExtentInfo()
		{
			dat.Write(extentInfo.MaxDistance);
			dat.Write(extentInfo.MinY);
			dat.Write(extentInfo.MaxY);
			dat.Write(animation.AttackRing);
			Write(extentInfo.FirstExtent);
			Write(extentInfo.MaxExtent);
			dat.Write(0);
			dat.Write(extents.Count);
			WriteRawArray(extents, Write);
		}

		private void Write(DatExtentInfoFrame info)
		{
			dat.WriteInt16(info.Frame);
			dat.WriteByte(info.Attack);
			dat.WriteByte(info.AttackOffset);
			dat.Write(info.Location);
			dat.Write(info.Height);
			dat.Write(info.Length);
			dat.Write(info.MinY);
			dat.Write(info.MaxY);
			dat.Write(info.Angle);
		}

		private void Write(Position position)
		{
			raw.Write((short)Math.Round(position.X * 100f));
			raw.Write((short)Math.Round(position.Z * 100f));
			raw.Write((ushort)Math.Round(position.Height * 100f));
			raw.Write((short)Math.Round(position.YOffset * 100f));
		}

		private void Write(Damage damage)
		{
			raw.WriteUInt16(damage.Points);
			raw.WriteUInt16(damage.Frame);
		}

		private void Write(Shortcut shortcut)
		{
			raw.WriteUInt16((ushort)shortcut.FromState);
			raw.WriteUInt16(shortcut.Length);
			raw.Write(shortcut.ReplaceAtomic ? 1 : 0);
		}

		private void Write(Footstep footstep)
		{
			raw.WriteUInt16(footstep.Frame);
			raw.WriteUInt16((ushort)footstep.Type);
		}

		private void Write(Sound sound)
		{
			raw.Write(sound.Name, 32);
			raw.WriteUInt16(sound.Start);
		}

		private void Write(Particle particle)
		{
			raw.WriteUInt16(particle.Start);
			raw.WriteUInt16(particle.End);
			raw.Write((int)particle.Bone);
			raw.Write(particle.Name, 16);
		}

		private void Write(MotionBlur m)
		{
			raw.Write((int)m.Bones);
			raw.WriteUInt16(m.Start);
			raw.WriteUInt16(m.End);
			raw.WriteByte(m.Lifetime);
			raw.WriteByte(m.Alpha);
			raw.WriteByte(m.Interval);
			raw.WriteByte(0);
		}

		private void Write(DatExtent extent)
		{
			raw.WriteInt16(extent.Frame);
			raw.Write((short)Math.Round(extent.Extent.Angle * 65535f / 360f));
			raw.Write((ushort)Math.Round(extent.Extent.Length * 100f));
			raw.WriteInt16(0);
			raw.Write((short)Math.Round(extent.Extent.MinY * 100f));
			raw.Write((short)Math.Round(extent.Extent.MaxY * 100f));
		}

		private void Write(Attack attack)
		{
			raw.Write((int)attack.Bones);
			raw.Write(attack.Knockback);
			raw.Write((int)attack.Flags);
			raw.WriteInt16(attack.HitPoints);
			raw.WriteInt16(attack.Start);
			raw.WriteInt16(attack.End);
			raw.WriteInt16((short)attack.HitType);
			raw.WriteInt16(attack.HitLength);
			raw.WriteInt16(attack.StunLength);
			raw.WriteInt16(attack.StaggerLength);
			raw.WriteInt16(0);
			raw.Write(0);
		}

		private void WriteRawArray<T>(List<T> list, Action<T> writeElement)
		{
			if (list.Count == 0)
			{
				dat.Write(0);
				return;
			}
			dat.Write(raw.Align32());
			foreach (T item in list)
			{
				writeElement(item);
			}
		}

		private void GenerateExtentInfo()
		{
			float[] attackRing = animation.AttackRing;
			Array.Clear(attackRing, 0, attackRing.Length);
			foreach (Attack attack in animation.Attacks)
			{
				attack.Extents.Clear();
				for (int i = attack.Start; i <= attack.End; i++)
				{
					Vector2 xZ = animation.Positions[i].XZ;
					List<Vector3> list = animation.AllPoints[i];
					for (int j = 0; j < list.Count / 8; j++)
					{
						if (((uint)attack.Bones & (uint)(1 << j)) == 0)
						{
							continue;
						}
						for (int k = j * 8; k < (j + 1) * 8; k++)
						{
							Vector2 vector = list[k].XZ - animation.Positions[0].XZ;
							float val = vector.Length();
							float num = FMath.Atan2(vector.X, vector.Y);
							if (num < 0f)
							{
								num += 6.283186f;
							}
							for (int l = 0; l < attackRing.Length; l++)
							{
								float num2 = (float)l * 6.283186f / (float)attackRing.Length;
								if (Math.Abs(num2 - num) < MathHelper.ToRadians(30f))
								{
									attackRing[l] = Math.Max(attackRing[l], val);
								}
							}
						}
					}
					float num3 = 1E+09f;
					float num4 = -1E+09f;
					float num5 = -1E+09f;
					float num6 = 0f;
					for (int m = 0; m < list.Count / 8; m++)
					{
						if (((uint)attack.Bones & (uint)(1 << m)) == 0)
						{
							continue;
						}
						for (int n = m * 8; n < (m + 1) * 8; n++)
						{
							Vector3 vector2 = list[n];
							Vector2 vector3 = vector2.XZ - xZ;
							float num7;
							switch (animation.Direction)
							{
							case Direction.Forward:
								num7 = vector3.Y;
								break;
							case Direction.Left:
								num7 = vector3.X;
								break;
							case Direction.Right:
								num7 = 0f - vector3.X;
								break;
							case Direction.Backward:
								num7 = 0f - vector3.Y;
								break;
							default:
								num7 = vector3.Length();
								break;
							}
							if (num7 > num5)
							{
								num5 = num7;
								num6 = FMath.Atan2(vector3.X, vector3.Y);
							}
							num3 = Math.Min(num3, vector2.Y);
							num4 = Math.Max(num4, vector2.Y);
						}
					}
					num5 = Math.Max(num5, 0f);
					if (num6 < 0f)
					{
						num6 += 6.283186f;
					}
					List<AttackExtent> list2 = attack.Extents;
					AttackExtent attackExtent = new AttackExtent();
					attackExtent.Angle = MathHelper.ToDegrees(num6);
					attackExtent.Length = num5;
					attackExtent.MinY = num3;
					attackExtent.MaxY = num4;
					list2.Add(attackExtent);
				}
			}
		}

		private void GenerateExtentSummary()
		{
			if (extents.Count == 0)
			{
				return;
			}
			List<Position> positions = animation.Positions;
			List<Attack> attacks = animation.Attacks;
			List<float> heights = animation.Heights;
			float num = float.MaxValue;
			float num2 = float.MinValue;
			foreach (DatExtent extent in extents)
			{
				num = Math.Min(num, extent.Extent.MinY);
				num2 = Math.Max(num2, extent.Extent.MaxY);
			}
			DatExtent datExtent = extents[0];
			DatExtent datExtent2 = datExtent;
			foreach (DatExtent extent2 in extents)
			{
				if (extent2.Extent.Length + positions[extent2.Frame].Z > datExtent2.Extent.Length + positions[datExtent2.Frame].Z)
				{
					datExtent2 = extent2;
				}
			}
			int attack = 0;
			int attackOffset = 0;
			for (int i = 0; i < attacks.Count; i++)
			{
				Attack attack2 = attacks[i];
				if (attack2.Start <= datExtent2.Frame && datExtent2.Frame <= attack2.End)
				{
					attack = i;
					attackOffset = datExtent2.Frame - attack2.Start;
					break;
				}
			}
			extentInfo.MaxDistance = Enumerable.Max(animation.AttackRing);
			extentInfo.MinY = num;
			extentInfo.MaxY = num2;
			extentInfo.FirstExtent.Frame = datExtent.Frame;
			extentInfo.FirstExtent.Attack = 0;
			extentInfo.FirstExtent.AttackOffset = 0;
			extentInfo.FirstExtent.Location.X = positions[datExtent.Frame].X;
			extentInfo.FirstExtent.Location.Y = 0f - positions[datExtent.Frame].Z;
			extentInfo.FirstExtent.Height = heights[datExtent.Frame];
			extentInfo.FirstExtent.Angle = MathHelper.ToRadians(datExtent.Extent.Angle);
			extentInfo.FirstExtent.Length = datExtent.Extent.Length;
			extentInfo.FirstExtent.MinY = FMath.Round(datExtent.Extent.MinY, 2);
			extentInfo.FirstExtent.MaxY = datExtent.Extent.MaxY;
			if ((animation.Flags & AnimationFlags.ThrowTarget) == 0)
			{
				extentInfo.MaxExtent.Frame = datExtent2.Frame;
				extentInfo.MaxExtent.Attack = attack;
				extentInfo.MaxExtent.AttackOffset = attackOffset;
				extentInfo.MaxExtent.Location.X = positions[datExtent2.Frame].X;
				extentInfo.MaxExtent.Location.Y = 0f - positions[datExtent2.Frame].Z;
				extentInfo.MaxExtent.Height = heights[datExtent2.Frame];
				extentInfo.MaxExtent.Angle = MathHelper.ToRadians(datExtent2.Extent.Angle);
				extentInfo.MaxExtent.Length = datExtent2.Extent.Length;
				extentInfo.MaxExtent.MinY = datExtent2.Extent.MinY;
				extentInfo.MaxExtent.MaxY = FMath.Round(datExtent2.Extent.MaxY, 2);
			}
		}

		private List<List<KeyFrame>> CompressFrames(List<List<KeyFrame>> tracks)
		{
			List<List<KeyFrame>> list = new List<List<KeyFrame>>();
			foreach (List<KeyFrame> track in tracks)
			{
				List<KeyFrame> list2 = new List<KeyFrame>(track.Count);
				for (int i = 0; i < track.Count; i++)
				{
					KeyFrame keyFrame = track[i];
					int duration = keyFrame.Duration;
					Vector3 vector = new Quaternion(keyFrame.Rotation).ToEulerXYZ();
					KeyFrame keyFrame2 = new KeyFrame();
					keyFrame2.Duration = duration;
					keyFrame2.Rotation.X = vector.X;
					keyFrame2.Rotation.Y = vector.Y;
					keyFrame2.Rotation.Z = vector.Z;
					list2.Add(keyFrame2);
				}
				list.Add(list2);
			}
			return list;
		}
	}
}
