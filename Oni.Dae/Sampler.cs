using System;
using System.Collections.Generic;
using System.IO;

namespace Oni.Dae
{
	internal class Sampler : Entity
	{
		private readonly List<Input> inputs = new List<Input>();

		private float outputScale = 1f;

		public List<Input> Inputs
		{
			get
			{
				return inputs;
			}
		}

		public int FrameCount
		{
			get
			{
				Input input = inputs.Find(delegate(Input i)
				{
					return i.Semantic == Semantic.Input;
				});
				if (input == null)
				{
					return 0;
				}
				return FMath.RoundToInt32(Utils.Last(input.Source.FloatData) * 60f) + 1;
			}
		}

		public Sampler Scale(float scale)
		{
			Sampler sampler = new Sampler();
			sampler.outputScale = scale;
			Sampler sampler2 = sampler;
			sampler2.inputs.AddRange(inputs);
			return sampler2;
		}

		public Sampler Split(int offset)
		{
			Sampler sampler = new Sampler();
			foreach (Input input3 in inputs)
			{
				Source source = input3.Source;
				switch (input3.Semantic)
				{
				case Semantic.Input:
					sampler.inputs.Add(input3);
					break;
				case Semantic.Interpolation:
					sampler.inputs.Add(input3);
					break;
				case Semantic.Output:
				{
					float[] array2 = new float[source.Count];
					for (int j = 0; j < array2.Length; j++)
					{
						array2[j] = source.FloatData[j * source.Stride + offset];
					}
					List<Input> list2 = sampler.inputs;
					Input input2 = new Input();
					input2.Source = new Source(array2, 1);
					input2.Semantic = input3.Semantic;
					list2.Add(input2);
					break;
				}
				case Semantic.InTangent:
				case Semantic.OutTangent:
				{
					float[] array = new float[source.Count * 2];
					for (int i = 0; i < array.Length; i++)
					{
						array[i] = source.FloatData[i * source.Stride];
						array[i + 1] = source.FloatData[i * source.Stride + (offset + 1)];
					}
					List<Input> list = sampler.inputs;
					Input input = new Input();
					input.Source = new Source(array, 2);
					input.Semantic = input3.Semantic;
					list.Add(input);
					break;
				}
				}
			}
			return sampler;
		}

		public float[] Sample()
		{
			return Sample(0, FrameCount - 1);
		}

		public float[] Sample(int start, int end)
		{
			float[] array = Sample(start, end, 0);
			if (outputScale != 1f)
			{
				for (int i = 0; i < array.Length; i++)
				{
					array[i] *= outputScale;
				}
			}
			return array;
		}

		public float[] SampleInput()
		{
			float[] result = null;
			foreach (Input input in inputs)
			{
				Semantic semantic = input.Semantic;
				if (semantic == Semantic.Input)
				{
					result = input.Source.FloatData;
				}
			}
			return result;
		}

		public float[] SampleOutput()
		{
			float[] result = null;
			int num = 1;
			foreach (Input input in inputs)
			{
				Semantic semantic = input.Semantic;
				if (semantic == Semantic.Output)
				{
					result = input.Source.FloatData;
					num = input.Source.Stride;
				}
			}
			if (num != 1)
			{
				throw new InvalidDataException("Unexpected output stride " + num);
			}
			return result;
		}

		private float[] Sample(int start, int end, int offset)
		{
			float[] array = null;
			float[] array2 = null;
			int num = 1;
			Vector2[] array3 = null;
			Vector2[] array4 = null;
			string[] array5 = null;
			foreach (Input input in inputs)
			{
				switch (input.Semantic)
				{
				case Semantic.Input:
					array = input.Source.FloatData;
					break;
				case Semantic.Output:
					array2 = input.Source.FloatData;
					num = input.Source.Stride;
					break;
				case Semantic.InTangent:
					array3 = FloatArrayToVector2Array(input.Source.FloatData);
					break;
				case Semantic.OutTangent:
					array4 = FloatArrayToVector2Array(input.Source.FloatData);
					break;
				case Semantic.Interpolation:
					array5 = input.Source.NameData;
					break;
				}
			}
			if (offset >= num)
			{
				throw new ArgumentException("The offset must be less than the output stride", "offset");
			}
			float[] array6 = new float[end - start + 1];
			if (array == null || array2 == null || array5 == null)
			{
				return array6;
			}
			if (array2.Length == num)
			{
				for (int i = 0; i < array6.Length; i++)
				{
					array6[i] = array2[offset];
				}
				return array6;
			}
			float num2 = Utils.First(array);
			float num3 = array2[offset];
			float num4 = Utils.Last(array);
			float num5 = array2[array2.Length - num + offset];
			for (int j = 0; j < array6.Length; j++)
			{
				float num6 = (float)(j + start) / 60f;
				if (num6 <= num2)
				{
					array6[j] = num3;
					continue;
				}
				if (num6 >= num4)
				{
					array6[j] = num5;
					continue;
				}
				int num7 = Array.BinarySearch(array, num6);
				if (num7 >= 0)
				{
					array6[j] = array2[num7 * num + offset];
					continue;
				}
				num7 = ~num7;
				if (num7 == 0)
				{
					array6[j] = num3;
					continue;
				}
				if (num7 * num + offset >= array2.Length)
				{
					array6[j] = num5;
					continue;
				}
				Vector2 vector = new Vector2(array[num7 - 1], array2[(num7 - 1) * num + offset]);
				Vector2 vector2 = new Vector2(array[num7], array2[num7 * num + offset]);
				float num8 = (num6 - vector.X) / (vector2.X - vector.X);
				switch (array5[num7 - 1])
				{
				default:
					Console.Error.WriteLine("Interpolation type '{0}' is not supported, using LINEAR", array5[num7 - 1]);
					goto case "LINEAR";
				case "LINEAR":
					array6[j] = vector.Y + num8 * (vector2.Y - vector.Y);
					break;
				case "BEZIER":
				{
					if (array3 == null || array4 == null)
					{
						throw new InvalidDataException("Bezier interpolation was specified but in/out tangents are not present");
					}
					Vector2 vector3 = array4[num7 - 1];
					Vector2 vector4 = array3[num7];
					float num9 = 1f - num8;
					array6[j] = vector.Y * num9 * num9 * num9 + 3f * vector3.Y * num9 * num9 * num8 + 3f * vector4.Y * num9 * num8 * num8 + vector2.Y * num8 * num8 * num8;
					break;
				}
				}
			}
			return array6;
		}

		private static Vector2[] FloatArrayToVector2Array(float[] array)
		{
			Vector2[] array2 = new Vector2[array.Length / 2];
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i].X = array[i * 2];
				array2[i].Y = array[i * 2 + 1];
			}
			return array2;
		}
	}
}
