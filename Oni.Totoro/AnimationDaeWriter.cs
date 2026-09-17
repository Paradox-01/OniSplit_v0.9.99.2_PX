using System;
using System.Collections.Generic;
using Oni.Dae;

namespace Oni.Totoro
{
	internal static class AnimationDaeWriter
	{
		public static void AppendFrames(Animation anim1, Animation anim2)
		{
			if ((anim2.Flags & AnimationFlags.Overlay) != 0)
			{
				Console.Error.WriteLine("Cannot merge {0} because it's an overlay animation", anim2.Name);
				return;
			}
			if (anim1.FrameSize == 0)
			{
				anim1.FrameSize = anim2.FrameSize;
			}
			else if (anim1.FrameSize != anim2.FrameSize)
			{
				Console.Error.WriteLine("Cannot merge {0} because its frame size doesn't match the frame size of the previous animation", anim2.Name);
				return;
			}
			anim1.Velocities.AddRange(anim2.Velocities);
			anim1.Heights.AddRange(anim2.Heights);
			if (anim1.Rotations.Count == 0)
			{
				anim1.Rotations.AddRange(anim2.Rotations);
				return;
			}
			for (int i = 0; i < anim1.Rotations.Count; i++)
			{
				anim1.Rotations[i].AddRange(anim2.Rotations[i]);
			}
		}

		public static void Write(Node root, Animation animation, int startFrame = 0, bool convertSceneZUP = false, bool convertEulerXYZ = false, bool plotEveryFrame = true)
		{
			List<Vector2> velocities = animation.Velocities;
			List<float> heights = animation.Heights;
			List<List<KeyFrame>> rotations = animation.Rotations;
			bool flag = animation.FrameSize == 6;
			bool flag2 = (animation.Flags & AnimationFlags.Overlay) != 0;
			bool flag3 = (animation.Flags & AnimationFlags.RealWorld) != 0;
			uint num = (uint)(animation.OverlayUsedBones | animation.OverlayReplacedBones);


			List<Node> list = FindNodes(root);
			if (!flag2 && !flag3)
			{
				Node targetNode = list[0];
				Vector2[] array = new Vector2[velocities.Count + 1];
				for (int i = 1; i < array.Length; i++)
				{
					array[i] = array[i - 1] + velocities[i - 1];
				}
				CreateAnimationCurve(startFrame, Enumerable.ToList(Enumerable.Select(array, delegate(Vector2 p)
				{
					return p.X;
				})), targetNode, "pos", "X");
				if (convertSceneZUP)
				{
					CreateAnimationCurve(startFrame, Enumerable.ToList(Enumerable.Select(array, delegate(Vector2 p)
					{
						return 0f - p.Y;
					})), targetNode, "pos", "Y");
				}
				else
				{
					CreateAnimationCurve(startFrame, Enumerable.ToList(Enumerable.Select(array, delegate(Vector2 p)
					{
						return p.Y;
					})), targetNode, "pos", "Z");
				}
				if (convertSceneZUP)
				{
					CreateAnimationCurve(startFrame, Enumerable.ToList(heights), targetNode, "pos", "Z");
				}
				else
				{
					CreateAnimationCurve(startFrame, Enumerable.ToList(heights), targetNode, "pos", "Y");
				}
			}
			for (int num3 = 0; num3 < rotations.Count; num3++)
			{
				if (flag2 && (num & (uint)(1 << num3)) == 0)
				{
					continue;
				}
				Node targetNode2 = list[num3];
				List<KeyFrame> list2 = rotations[num3];
				int num4 = ((!plotEveryFrame) ? list2.Count : Enumerable.Sum(list2, delegate(KeyFrame k)
				{
					return k.Duration;
				}));
				float[] array2 = new float[num4];
				float[] array3 = new float[num4];
				float[] array4 = new float[num4];
				float[] array5 = new float[num4];
				if (plotEveryFrame)
				{
					Quaternion[] array6 = new Quaternion[list2.Count];
					for (int num5 = 0; num5 < list2.Count; num5++)
					{
						KeyFrame keyFrame = list2[num5];
						if (flag)
						{
							array6[num5] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(keyFrame.Rotation.X)) * Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.ToRadians(keyFrame.Rotation.Y)) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathHelper.ToRadians(keyFrame.Rotation.Z));
						}
						else
						{
							array6[num5] = new Quaternion(keyFrame.Rotation);
						}
					}
					int num6 = 0;
					for (int num7 = 0; num7 < list2.Count; num7++)
					{
						int duration = list2[num7].Duration;
						Quaternion q = array6[num7];
						Quaternion q2 = ((num7 == list2.Count - 1) ? array6[num7] : array6[num7 + 1]);
						for (int num8 = 0; num8 < duration; num8++)
						{
							Quaternion quaternion = Quaternion.Slerp(q, q2, (float)num8 / (float)duration, true);
							if ((num3 == 0) & convertSceneZUP)
							{
								Quaternion quaternion2 = quaternion;
								quaternion = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(90f)) * quaternion2;
							}
							Vector3 vector = (convertEulerXYZ ? quaternion.ToEulerRevXYZ() : quaternion.ToEulerXYZ());
							array2[num6] = (float)(num6 + startFrame) * (1f / 60f);
							array3[num6] = vector.X;
							array4[num6] = vector.Y;
							array5[num6] = vector.Z;
							num6++;
						}
					}
				}
				else
				{
					int num9 = 0;
					for (int num10 = 0; num10 < list2.Count; num10++)
					{
						KeyFrame keyFrame2 = list2[num10];
						array2[num10] = (float)(num9 + startFrame) * (1f / 60f);
						num9 += keyFrame2.Duration;
						if (flag)
						{
							array3[num10] = keyFrame2.Rotation.X;
							array4[num10] = keyFrame2.Rotation.Y;
							array5[num10] = keyFrame2.Rotation.Z;
							if (convertEulerXYZ | convertSceneZUP)
							{
								Quaternion quaternion3 = Quaternion.CreateFromEulerXYZ(array3[num10], array4[num10], array5[num10]);
								if ((num3 == 0) & convertSceneZUP)
								{
									Quaternion quaternion4 = quaternion3;
									quaternion3 = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(90f)) * quaternion4;
								}
								Vector3 vector2 = (convertEulerXYZ ? quaternion3.ToEulerRevXYZ() : quaternion3.ToEulerXYZ());
								array3[num10] = vector2.X;
								array4[num10] = vector2.Y;
								array5[num10] = vector2.Z;
							}
						}
						else
						{
							Quaternion quaternion5 = new Quaternion(keyFrame2.Rotation);
							if ((num3 == 0) & convertSceneZUP)
							{
								Quaternion quaternion6 = quaternion5;
								quaternion5 = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(90f)) * quaternion6;
							}
							Vector3 vector3 = (convertEulerXYZ ? quaternion5.ToEulerRevXYZ() : quaternion5.ToEulerXYZ());
							array3[num10] = vector3.X;
							array4[num10] = vector3.Y;
							array5[num10] = vector3.Z;
						}
					}
				}
				MakeRotationCurvesContinuous(array3, array4, array5);
				CreateAnimationCurve(array2, array3, targetNode2, "rotX", "ANGLE");
				CreateAnimationCurve(array2, array4, targetNode2, "rotY", "ANGLE");
				CreateAnimationCurve(array2, array5, targetNode2, "rotZ", "ANGLE");
			}
		}

		private static void MakeRotationCurveContinuous(float[] curve)
		{
			for (int i = 1; i < curve.Length; i++)
			{
				float num = curve[i - 1];
				float num2 = curve[i];
				if (Math.Abs(num2 - num) > 180f)
				{
					num2 = ((!(num2 > num)) ? (num2 + 360f) : (num2 - 360f));
					curve[i] = num2;
				}
			}
		}

		private static Vector3 EulerFilterNaive(Vector3 curr, Vector3 prev)
		{
			Vector3 result = curr;
			while (Math.Abs(result.X - prev.X) > 180f)
			{
				result.X += ((result.X < prev.X) ? 360 : (-360));
			}
			while (Math.Abs(result.Y - prev.Y) > 180f)
			{
				result.Y += ((result.Y < prev.Y) ? 360 : (-360));
			}
			while (Math.Abs(result.Z - prev.Z) > 180f)
			{
				result.Z += ((result.Z < prev.Z) ? 360 : (-360));
			}
			return result;
		}

		private static Vector3 EulerFilter(Vector3 curr, Vector3 prev)
		{
			Vector3 result = EulerFilterNaive(curr, prev);
			curr.X = 180f + curr.X;
			curr.Y = 180f - curr.Y;
			curr.Z = 180f + curr.Z;
			Vector3 result2 = EulerFilterNaive(curr, prev);
			double num = Math.Abs(result.X - prev.X) + Math.Abs(result.Y - prev.Y) + Math.Abs(result.Z - prev.Z);
			double num2 = Math.Abs(result2.X - prev.X) + Math.Abs(result2.Y - prev.Y) + Math.Abs(result2.Z - prev.Z);
			if (!(num < num2))
			{
				return result2;
			}
			return result;
		}

		private static void MakeRotationCurvesContinuous(float[] xcurve, float[] ycurve, float[] zcurve)
		{
			for (int i = 1; i < xcurve.Length; i++)
			{
				Vector3 prev = new Vector3(xcurve[i - 1], ycurve[i - 1], zcurve[i - 1]);
				Vector3 curr = new Vector3(xcurve[i], ycurve[i], zcurve[i]);
				Vector3 vector = EulerFilter(curr, prev);
				xcurve[i] = vector.X;
				ycurve[i] = vector.Y;
				zcurve[i] = vector.Z;
			}
		}

		private static void CreateAnimationCurve(int startFrame, IList<float> values, Node targetNode, string targetSid, string targetValue)
		{
			if (values.Count != 0)
			{
				float[] array = new float[values.Count];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = (float)(i + startFrame) * (1f / 60f);
				}
				CreateAnimationCurve(array, values, targetNode, targetSid, targetValue);
			}
		}

		private static void CreateAnimationCurve(IList<float> times, IList<float> values, Node targetNode, string targetSid, string targetValue)
		{
			string[] array = new string[times.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = "LINEAR";
			}
			Transform transform = targetNode.Transforms.Find(delegate(Transform x)
			{
				return x.Sid == targetSid;
			});
			Sampler sampler = new Sampler();
			sampler.Inputs.Add(new Input(Semantic.Input, new Source(times, 1)));
			sampler.Inputs.Add(new Input(Semantic.Output, new Source(values, 1)));
			sampler.Inputs.Add(new Input(Semantic.Interpolation, new Source(array, 1)));
			transform.BindAnimation(targetValue, sampler);
		}

		private static List<Node> FindNodes(Node root)
		{
			List<Node> result = new List<Node>(19);
			FindNodesRecursive(root, result);
			return result;
		}

		private static void FindNodesRecursive(Node node, List<Node> result)
		{
			result.Add(node);
			foreach (Node node2 in node.Nodes)
			{
				FindNodesRecursive(node2, result);
			}
		}
	}
}
