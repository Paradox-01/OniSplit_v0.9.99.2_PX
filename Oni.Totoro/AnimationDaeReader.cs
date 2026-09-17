using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Oni.Dae;
using Oni.Dae.IO;

namespace Oni.Totoro
{
	internal class AnimationDaeReader
	{
		private Animation animation;

		private Scene scene;

		private int startFrame;

		private int endFrame;

		private int minFrame;

		private int maxFrame;

		private Body body;

		private int frameCount;

		public Scene Scene
		{
			get
			{
				return scene;
			}
			set
			{
				scene = value;
			}
		}

		public int StartFrame
		{
			get
			{
				return startFrame;
			}
			set
			{
				startFrame = value;
			}
		}

		public int EndFrame
		{
			get
			{
				return endFrame;
			}
			set
			{
				endFrame = value;
			}
		}

		public void Read(Animation targetAnimation)
		{
			animation = targetAnimation;
			body = BodyDaeReader.Read(scene);
			ComputeFrameCount();
			ImportTranslation();
			ImportRotations();
			animation.ComputeExtents(body);
		}

		private void ComputeFrameCount()
		{
			float num = float.MaxValue;
			float num2 = float.MinValue;
			IEnumerable<Input> enumerable = Enumerable.Where(Enumerable.SelectMany(Enumerable.Where(Enumerable.SelectMany(Enumerable.Where(Enumerable.SelectMany(body.Nodes, delegate(BodyNode n)
			{
				return n.DaeNode.Transforms;
			}), delegate(Transform t)
			{
				return t.HasAnimations;
			}), delegate(Transform t)
			{
				return t.Animations;
			}), delegate(Sampler a)
			{
				return a != null;
			}), delegate(Sampler a)
			{
				return a.Inputs;
			}), delegate(Input i)
			{
				return i.Semantic == Semantic.Input;
			});
			foreach (Input item in enumerable)
			{
				num = Math.Min(num, Enumerable.Min(item.Source.FloatData));
				num2 = Math.Max(num2, Enumerable.Max(item.Source.FloatData));
			}
			if (num > num2)
			{
				Console.Error.WriteLine("Warning: animation has no keys; setting empty range.");
				num = 0f;
				num2 = 0f;
			}
			float num3 = num * 60f;
			float num4 = num2 * 60f;
			double num5 = 0.0005;
			if (Math.Abs((double)num3 - Math.Round(num3)) < num5)
			{
				minFrame = FMath.RoundToInt32(num3);
			}
			else
			{
				minFrame = FMath.TruncateToInt32(num3);
			}
			if (Math.Abs((double)num4 - Math.Round(num4)) < num5)
			{
				maxFrame = FMath.RoundToInt32(num4);
			}
			else
			{
				maxFrame = FMath.TruncateToInt32(num4) + 1;
			}
			if (endFrame < minFrame || startFrame > maxFrame)
			{
				Console.Error.WriteLine("Warning: the range defined by Start and End does not overlap the animated range; using animated range.");
				startFrame = minFrame;
				endFrame = maxFrame;
			}
			if (endFrame > maxFrame)
			{
				endFrame = maxFrame;
			}
			if (startFrame < minFrame)
			{
				startFrame = minFrame;
			}
			frameCount = endFrame - startFrame;
		}

		private void ImportTranslation()
		{
			Node daeNode = body.Nodes[0].DaeNode;
			bool flag = false;
			foreach (Transform transform in daeNode.Transforms)
			{
				TransformTranslate transformTranslate = transform as TransformTranslate;
				if (transformTranslate == null || flag)
				{
					continue;
				}
				flag = true;
				if (scene.CustomAxisConversion && scene.SceneZUP)
				{
					animation.Heights.AddRange(Sample(transformTranslate, 2, endFrame - 1));
				}
				else
				{
					animation.Heights.AddRange(Sample(transformTranslate, 1, endFrame - 1));
				}
				float[] array = Sample(transformTranslate, 0, endFrame);
				float[] array2 = Sample(transformTranslate, 1, endFrame);
				float[] array3 = Sample(transformTranslate, 2, endFrame);
				for (int i = 1; i < array.Length; i++)
				{
					if (scene.CustomAxisConversion && scene.SceneZUP)
					{
						animation.Velocities.Add(new Vector2(array[i] - array[i - 1], array2[i - 1] - array2[i]));
					}
					else
					{
						animation.Velocities.Add(new Vector2(array[i] - array[i - 1], array3[i] - array3[i - 1]));
					}
				}
			}
			if (!flag)
			{
				animation.Heights.AddRange(Enumerable.Repeat(0f, frameCount));
				animation.Velocities.AddRange(Enumerable.Repeat(Vector2.Zero, frameCount));
			}
		}

		private void ImportRotations()
		{
			animation.FrameSize = 16;
			bool flag = false;
			bool flag2 = false;
			string[] commandLineArgs = DaeReader.CommandLineArgs;
			if (Enumerable.Any(commandLineArgs, delegate(string a)
			{
				return a == "-keepkeys";
			}))
			{
				flag = true;
			}
			if (Enumerable.Any(commandLineArgs, delegate(string a)
			{
				return a == "-dense";
			}))
			{
				flag2 = true;
				if (flag)
				{
					flag = false;
					Console.Error.WriteLine("Warning: -keepkeys was overridden by -dense.");
				}
			}
			foreach (Node item2 in Enumerable.Select(body.Nodes, delegate(BodyNode n)
			{
				return n.DaeNode;
			}))
			{
				List<KeyFrame> list = new List<KeyFrame>();
				List<KeyFrame> list2 = new List<KeyFrame>();
				animation.Rotations.Add(list);
				animation.RotationsDense.Add(list2);
				List<TransformRotate> list3 = new List<TransformRotate>();
				List<float[]> list4 = new List<float[]>();
				List<float[]> list5 = new List<float[]>();
				List<Quaternion> list6 = new List<Quaternion>();
				List<Quaternion> list7 = new List<Quaternion>();
				foreach (Transform transform in item2.Transforms)
				{
					TransformRotate transformRotate = transform as TransformRotate;
					if (transformRotate != null)
					{
						list3.Add(transformRotate);
						list4.Add(SampleInput(transformRotate, 3));
						list5.Add(SampleOutput(transformRotate, 3));
					}
				}
				if (list3.Count != 3)
				{
					throw new InvalidDataException("Unexpected rotation count " + list3.Count + " for bone " + item2.Name);
				}
				int num = 0;
				float[] array = new float[0];
				if (!flag)
				{
					num = frameCount;
					array = new float[num];
					for (int num2 = 0; num2 < num; num2++)
					{
						array[num2] = (float)(startFrame + num2) / 60f;
					}
					for (int num3 = 0; num3 < 3; num3++)
					{
						list4[num3] = array;
						list5[num3] = Sample(list3[num3], 3, startFrame, endFrame - 1);
					}
				}
				else
				{
					for (int num4 = 0; num4 < 3; num4++)
					{
						if (num < list4[num4].Length)
						{
							num = list4[num4].Length;
							array = list4[num4];
						}
					}
					if (num == 1)
					{
						num = (frameCount + 253) / 255 + 1;
						array = new float[num];
						for (int num5 = 0; num5 < num; num5++)
						{
							int num6 = Math.Min(startFrame + 255 * num5, endFrame - 1);
							array[num5] = (float)num6 / 60f;
						}
					}
					for (int num7 = 0; num7 < 3; num7++)
					{
						if (list4[num7].Length == 1)
						{
							list4[num7] = array;
							float constantValue = list5[num7][0];
							list5[num7] = new float[num];
							for (int fillIndex = 0; fillIndex < num; fillIndex++)
							{
								list5[num7][fillIndex] = constantValue;
							}
						}
						else if (list4[num7].Length != num)
						{
							throw new InvalidDataException("Cannot keep rotation keys for node " + item2.Name + " (inconsistent keyframe counts)!");
						}
						if (list5[num7].Length != num)
						{
							throw new InvalidDataException("Cannot keep rotation keys for node " + item2.Name + " (input/output size mismatch)!");
						}
					}
				}
				int[] array2 = new int[num];
				int num8 = -1;
				int num9 = -1;
				for (int num10 = 0; num10 < num; num10++)
				{
					if (num10 > 0 && array[num10] <= array[num10 - 1])
					{
						throw new InvalidDataException("Invalid rotation curves for node " + item2.Name + " (non-increasing timeline)!");
					}
					if ((double)Math.Abs(list4[0][num10] - array[num10]) > 0.001 || (double)Math.Abs(list4[1][num10] - array[num10]) > 0.001 || (double)Math.Abs(list4[2][num10] - array[num10]) > 0.001)
					{
						throw new InvalidDataException("Invalid rotation curves for node " + item2.Name + " (asynchronous keyframe times)!");
					}
					array2[num10] = (int)Math.Round(array[num10] * 60f);
					if (array2[num10] == startFrame)
					{
						num8 = num10;
					}
					if (array2[num10] == endFrame - 1)
					{
						num9 = num10;
					}
					if (flag)
					{
						if ((double)Math.Abs(array[num10] * 60f - (float)array2[num10]) > 0.001)
						{
							throw new InvalidDataException("Cannot keep rotation keys: keyframe " + num10 + " for bone " + item2.Name + " (value " + array[num10] * 60f + ") is too far from closest game tick " + array2[num10]);
						}
						if (num10 > 0 && array2[num10] == array2[num10 - 1])
						{
							throw new InvalidDataException("Cannot keep rotation keys: bone " + item2.Name + " has multiple keyframes at/near frame " + array2[num10]);
						}
						if (num10 > 0 && array2[num10] - array2[num10 - 1] > 255)
						{
							throw new InvalidDataException("Cannot keep rotation keys: bone " + item2.Name + " has overlong interval between frames " + array2[num10 - 1] + " and " + array2[num10]);
						}
					}
					Quaternion identity = Quaternion.Identity;
					int num11 = 0;
					int num12 = 0;
					int num13 = 0;
					float num14 = 0f;
					float num15 = 0f;
					float num16 = 0f;
					for (int num17 = 0; num17 < list3.Count; num17++)
					{
						if (list3[num17].Axis == Vector3.UnitX)
						{
							num14 = list5[num17][num10];
							num11++;
							continue;
						}
						if (list3[num17].Axis == -Vector3.UnitX)
						{
							num14 = 0f - list5[num17][num10];
							num11++;
							continue;
						}
						if (list3[num17].Axis == Vector3.UnitY)
						{
							num15 = list5[num17][num10];
							num12++;
							continue;
						}
						if (list3[num17].Axis == -Vector3.UnitY)
						{
							num15 = 0f - list5[num17][num10];
							num12++;
							continue;
						}
						if (list3[num17].Axis == Vector3.UnitZ)
						{
							num16 = list5[num17][num10];
							num13++;
							continue;
						}
						if (list3[num17].Axis == -Vector3.UnitZ)
						{
							num16 = 0f - list5[num17][num10];
							num13++;
							continue;
						}
						throw new InvalidDataException("Unexpected rotation axis!" + list3[num17].Axis.ToString() + " for node " + item2.Name);
					}
					if (num11 != 1 || num12 != 1 || num13 != 1)
					{
						throw new InvalidDataException("Node " + item2.Name + " does not have a valid rotation triplet");
					}
					Quaternion quaternion = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(num14));
					Quaternion quaternion2 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.ToRadians(num15));
					Quaternion quaternion3 = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathHelper.ToRadians(num16));
					Quaternion quaternion4 = Quaternion.CreateFromEulerXYZ(num14, num15, num16);
					Quaternion quaternion5 = Quaternion.CreateFromEulerRevXYZ(num14, num15, num16);
					if (scene.CustomAxisConversion && scene.SceneZUP && item2.Name.Contains("pelvis"))
					{
						identity *= Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(-90f));
					}
					if (scene.CustomAxisConversion)
					{
						identity *= quaternion5;
					}
					else
					{
						identity *= quaternion * quaternion2 * quaternion3;
					}
					list6.Add(identity);
				}
				if (flag && num8 == -1)
				{
					throw new InvalidDataException("Cannot keep rotation keys: bone " + item2.Name + " has no key at start frame");
				}
				if (flag && num9 == -1)
				{
					throw new InvalidDataException("Cannot keep rotation keys: bone " + item2.Name + " has no key near end frame");
				}
				for (int num18 = startFrame; num18 < endFrame; num18++)
				{
					float num19 = (float)num18 / 60f;
					int num20 = Array.BinarySearch(list4[0], num19);
					if (num20 >= 0)
					{
						list7.Add(list6[num20]);
						continue;
					}
					num20 = ~num20;
					if (num20 == 0)
					{
						list7.Add(list6[0]);
						continue;
					}
					if (num20 >= num)
					{
						list7.Add(list6[num - 1]);
						continue;
					}
					float amount = (num19 - array[num20 - 1]) / (array[num20] - array[num20 - 1]);
					Quaternion item = Quaternion.Slerp(list6[num20 - 1], list6[num20], amount, true);
					list7.Add(item);
				}
				foreach (Quaternion item3 in list7)
				{
					KeyFrame keyFrame = new KeyFrame();
					keyFrame.Duration = 1;
					keyFrame.Rotation = item3.ToVector4();
					list2.Add(keyFrame);
				}
				float num21 = 0f;
				string[] array3 = commandLineArgs;
				foreach (string text in array3)
				{
					if (text.StartsWith("-tolerance:", StringComparison.Ordinal))
					{
						int num23 = text.IndexOf(':');
						num21 = float.Parse(text.Substring(num23 + 1), CultureInfo.InvariantCulture);
						if (num21 < 0f || num21 > 10f)
						{
							throw new InvalidDataException("Angular tolerance is out of range!");
						}
						if (flag)
						{
							Console.Error.WriteLine("Warning: keeping original keys, ignoring -tolerance.");
						}
						if (flag2)
						{
							Console.Error.WriteLine("Warning: outputting dense keys, ignoring -tolerance.");
						}
					}
				}
				if (flag)
				{
					int num24 = 0;
					for (int num25 = num8; num25 <= num9; num25++)
					{
						int num26 = 1;
						if (num25 < num9)
						{
							num26 = array2[num25 + 1] - array2[num25];
						}
						KeyFrame keyFrame2 = new KeyFrame();
						keyFrame2.Duration = num26;
						keyFrame2.Rotation = list6[num25].ToVector4();
						list.Add(keyFrame2);
						num24 += num26;
					}
					continue;
				}
				float maxAllowedW = FMath.Cos(MathHelper.ToRadians(num21) * 0.5f);
				int num27 = 0;
				int num29;
				for (int num28 = 0; num28 < frameCount; num28 += num29)
				{
					Quaternion quaternion6 = list7[num28];
					num29 = 1;
					if (!flag2)
					{
						for (int num30 = num28 + 2; num30 < frameCount && num30 < num28 + 256 && IsLinearRange(list2, num28, num30, maxAllowedW); num30++)
						{
							num29 = num30 - num28;
						}
					}
					KeyFrame keyFrame3 = new KeyFrame();
					keyFrame3.Duration = num29;
					keyFrame3.Rotation = quaternion6.ToVector4();
					list.Add(keyFrame3);
					num27 += num29;
				}
			}
		}

		private static bool IsLinearRange(List<KeyFrame> frames, int first, int last, float maxAllowedW)
		{
			Quaternion q = new Quaternion(frames[first].Rotation);
			Quaternion q2 = new Quaternion(frames[last].Rotation);
			float num = last - first;
			for (int i = first + 1; i < last; i++)
			{
				float amount = (float)(i - first) / num;
				Quaternion q3 = Quaternion.Slerp(q, q2, amount, true);
				Quaternion quaternion = new Quaternion(frames[i].Rotation);
				if (Math.Abs((Quaternion.Conjugate(q3) * quaternion).W) < maxAllowedW)
				{
					return false;
				}
			}
			return true;
		}

		private float[] Sample(Transform transform, int index, int endFrame)
		{
			Sampler sampler = null;
			if (transform.HasAnimations)
			{
				sampler = transform.Animations[index];
			}
			if (sampler == null)
			{
				float num = transform.Values[index];
				float[] array = new float[endFrame - startFrame + 1];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = num;
				}
				return array;
			}
			return sampler.Sample(startFrame, endFrame);
		}

		private float[] Sample(Transform transform, int index, int startFrame, int endFrame)
		{
			Sampler sampler = null;
			if (transform.HasAnimations)
			{
				sampler = transform.Animations[index];
			}
			if (sampler == null)
			{
				float num = transform.Values[index];
				float[] array = new float[endFrame - startFrame + 1];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = num;
				}
				return array;
			}
			return sampler.Sample(startFrame, endFrame);
		}

		private float[] SampleInput(Transform transform, int index)
		{
			Sampler sampler = null;
			if (transform.HasAnimations)
			{
				sampler = transform.Animations[index];
			}
			float[] result = new float[1] { 0f };
			if (sampler == null)
			{
				return result;
			}
			float[] array = sampler.SampleInput();
			if (array == null)
			{
				return result;
			}
			return array;
		}

		private float[] SampleOutput(Transform transform, int index)
		{
			Sampler sampler = null;
			if (transform.HasAnimations)
			{
				sampler = transform.Animations[index];
			}
			float num = transform.Values[index];
			float[] result = new float[1] { num };
			if (sampler == null)
			{
				return result;
			}
			float[] array = sampler.SampleOutput();
			if (array == null)
			{
				return result;
			}
			return array;
		}
	}
}
