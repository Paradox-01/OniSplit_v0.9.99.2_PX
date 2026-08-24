using System;
using System.Collections.Generic;

namespace Oni.Akira
{
	internal sealed class StairRampClassifier
	{
		private const float MinNormalY = 0.35f;

		private const float MaxNormalY = 0.995f;

		private const float BandTolerance = 0.025f;

		private readonly OctreeNode geometryOctree;

		public StairRampClassifier(PolygonMesh mesh)
		{
			geometryOctree = OctreeBuilder.Build(mesh, (Polygon p) => true);
		}

		public bool IsStairRamp(Polygon polygon)
		{
			RampFrame ramp;
			if (!TryGetRampFrame(polygon, out ramp))
			{
				return false;
			}

			List<Polygon> cover = GetCoverGeometry(polygon, ramp);
			if (HasParallelRampAbove(polygon, cover, ramp))
			{
				return false;
			}
			if (polygon.Plane.Normal.Y < 0f)
			{
				return false;
			}

			List<Band> treads = GetBands(cover, ramp, true);
			if (treads.Count < 8 || GetBandSpan(treads) < 0.75f || GetWidthCoverage(treads) < 0.8f || GetSpacingVariation(treads) > 0.25f)
			{
				return false;
			}

			return GetBands(cover, ramp, false).Count >= 8;
		}

		private static bool TryGetRampFrame(Polygon polygon, out RampFrame ramp)
		{
			ramp = default(RampFrame);
			GunkFlags flags = polygon.Flags;
			GunkFlags excluded = GunkFlags.Ghost | GunkFlags.StairsUp | GunkFlags.StairsDown | GunkFlags.Invisible | GunkFlags.Danger | GunkFlags.NoCharacterCollision;
			float normalY = Math.Abs(polygon.Plane.Normal.Y);
			if (!polygon.IsStairs || polygon.VertexCount != 4 || (flags & excluded) != GunkFlags.None || normalY < MinNormalY || normalY >= MaxNormalY)
			{
				return false;
			}

			Vector3[] points = new List<Vector3>(polygon.Points).ToArray();
			float lowY = float.MaxValue;
			float highY = float.MinValue;
			for (int i = 0; i < points.Length; i++)
			{
				lowY = Math.Min(lowY, points[i].Y);
				highY = Math.Max(highY, points[i].Y);
			}
			if (highY - lowY <= 1f)
			{
				return false;
			}

			Vector3 low = Vector3.Zero;
			Vector3 high = Vector3.Zero;
			int lowCount = 0;
			int highCount = 0;
			for (int j = 0; j < points.Length; j++)
			{
				if (Math.Abs(points[j].Y - lowY) <= 0.01f)
				{
					low += points[j];
					lowCount++;
				}
				else if (Math.Abs(points[j].Y - highY) <= 0.01f)
				{
					high += points[j];
					highCount++;
				}
			}
			if (lowCount != 2 || highCount != 2)
			{
				return false;
			}

			low /= lowCount;
			high /= highCount;
			Vector3 runVector = new Vector3(high.X - low.X, 0f, high.Z - low.Z);
			float runLength = runVector.Length();
			if (runLength <= 1f)
			{
				return false;
			}

			Vector3 run = runVector / runLength;
			Vector3 width = new Vector3(0f - run.Z, 0f, run.X);
			float minWidth = float.MaxValue;
			float maxWidth = float.MinValue;
			for (int k = 0; k < points.Length; k++)
			{
				float value = Vector3.Dot(points[k] - low, width);
				minWidth = Math.Min(minWidth, value);
				maxWidth = Math.Max(maxWidth, value);
			}
			float widthLength = maxWidth - minWidth;
			if (widthLength / runLength < 0.25f)
			{
				return false;
			}

			ramp = new RampFrame(low, run, width, runLength, widthLength, minWidth, highY - lowY);
			return true;
		}

		private List<Polygon> GetCoverGeometry(Polygon candidate, RampFrame ramp)
		{
			BoundingBox box = candidate.BoundingBox;
			box.Min.Y += 0.1f;
			box.Max.Y += 7.5f;
			HashSet<Polygon> polygons = new HashSet<Polygon>();
			foreach (OctreeNode leaf in geometryOctree.FindLeafs(box))
			{
				foreach (Polygon polygon in leaf.Polygons)
				{
					if (polygon != candidate && polygon.BoundingBox.Intersects(box))
					{
						polygons.Add(polygon);
					}
				}
			}
			return new List<Polygon>(polygons);
		}

		private static bool HasParallelRampAbove(Polygon candidate, List<Polygon> cover, RampFrame ramp)
		{
			Vector3 normal = candidate.Plane.Normal;
			if (normal.Y < 0f)
			{
				normal = -normal;
			}
			for (int i = 0; i < cover.Count; i++)
			{
				Polygon polygon = cover[i];
				Vector3 coverNormal = polygon.Plane.Normal;
				if (coverNormal.Y < 0f)
				{
					coverNormal = -coverNormal;
				}
				if (polygon.VertexCount == 4 && Vector3.Dot(normal, coverNormal) >= 0.999f)
				{
					List<Vector3> clipped = ClipToPrism(new List<Vector3>(polygon.Points), ramp);
					if (clipped != null && clipped.Count >= 3)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static List<Band> GetBands(List<Polygon> cover, RampFrame ramp, bool horizontal)
		{
			List<Band> bands = new List<Band>();
			for (int i = 0; i < cover.Count; i++)
			{
				Polygon polygon = cover[i];
				float normalY = Math.Abs(polygon.Plane.Normal.Y);
				if (horizontal ? (normalY < 0.95f || (polygon.Flags & GunkFlags.NoCharacterCollision) == 0) : normalY > 0.05f)
				{
					continue;
				}

				List<Vector3> clipped = ClipToPrism(new List<Vector3>(polygon.Points), ramp);
				if (clipped == null || clipped.Count < 3)
				{
					continue;
				}

				float run = 0f;
				float minWidth = float.MaxValue;
				float maxWidth = float.MinValue;
				for (int j = 0; j < clipped.Count; j++)
				{
					run += ramp.GetRun(clipped[j]);
					float width = ramp.GetWidth(clipped[j]);
					minWidth = Math.Min(minWidth, width);
					maxWidth = Math.Max(maxWidth, width);
				}
				AddBand(bands, run / clipped.Count, minWidth, maxWidth);
			}
			bands.Sort((Band x, Band y) => x.Position.CompareTo(y.Position));
			return bands;
		}

		private static List<Vector3> ClipToPrism(List<Vector3> points, RampFrame ramp)
		{
			Plane[] planes = ramp.GetClipPlanes();
			for (int i = 0; i < planes.Length && points != null; i++)
			{
				points = PolygonUtils.ClipToPlane(points, planes[i]);
			}
			return points;
		}

		private static void AddBand(List<Band> bands, float position, float minWidth, float maxWidth)
		{
			for (int i = 0; i < bands.Count; i++)
			{
				if (Math.Abs(bands[i].Position - position) <= BandTolerance)
				{
					bands[i].Add(position, minWidth, maxWidth);
					return;
				}
			}
			bands.Add(new Band(position, minWidth, maxWidth));
		}

		private static float GetBandSpan(List<Band> bands)
		{
			return bands[bands.Count - 1].Position - bands[0].Position;
		}

		private static float GetWidthCoverage(List<Band> bands)
		{
			float coverage = 0f;
			for (int i = 0; i < bands.Count; i++)
			{
				coverage += bands[i].GetWidthCoverage();
			}
			return coverage / bands.Count;
		}

		private static float GetSpacingVariation(List<Band> bands)
		{
			float mean = GetBandSpan(bands) / (bands.Count - 1);
			float variance = 0f;
			for (int i = 1; i < bands.Count; i++)
			{
				float difference = bands[i].Position - bands[i - 1].Position - mean;
				variance += difference * difference;
			}
			return (float)Math.Sqrt(variance / (bands.Count - 1)) / mean;
		}

		private struct RampFrame
		{
			private readonly Vector3 origin;

			private readonly Vector3 run;

			private readonly Vector3 width;

			private readonly float runLength;

			private readonly float widthLength;

			private readonly float minWidth;

			private readonly float rise;

			public RampFrame(Vector3 origin, Vector3 run, Vector3 width, float runLength, float widthLength, float minWidth, float rise)
			{
				this.origin = origin;
				this.run = run;
				this.width = width;
				this.runLength = runLength;
				this.widthLength = widthLength;
				this.minWidth = minWidth;
				this.rise = rise;
			}

			public float GetRun(Vector3 point)
			{
				return Vector3.Dot(point - origin, run) / runLength;
			}

			public float GetWidth(Vector3 point)
			{
				return (Vector3.Dot(point - origin, width) - minWidth) / widthLength;
			}

			public Plane[] GetClipPlanes()
			{
				float slope = rise / runLength;
				Vector3 slopeNormal = new Vector3(0f - slope * run.X, 1f, 0f - slope * run.Z);
				float slopeD = 0f - origin.Y + slope * Vector3.Dot(origin, run);
				return new Plane[6]
				{
					new Plane(run, 0f - Vector3.Dot(run, origin)),
					new Plane(-run, Vector3.Dot(run, origin) + runLength),
					new Plane(width, 0f - Vector3.Dot(width, origin) - minWidth),
					new Plane(-width, Vector3.Dot(width, origin) + minWidth + widthLength),
					new Plane(slopeNormal, slopeD - 0.1f),
					new Plane(-slopeNormal, 7.5f - slopeD)
				};
			}
		}

		private sealed class Band
		{
			private float positionTotal;

			private int positionCount;

			private readonly List<WidthInterval> widths = new List<WidthInterval>();

			public float Position
			{
				get
				{
					return positionTotal / positionCount;
				}
			}

			public Band(float position, float minWidth, float maxWidth)
			{
				Add(position, minWidth, maxWidth);
			}

			public void Add(float position, float minWidth, float maxWidth)
			{
				positionTotal += position;
				positionCount++;
				widths.Add(new WidthInterval(Math.Max(0f, minWidth), Math.Min(1f, maxWidth)));
			}

			public float GetWidthCoverage()
			{
				widths.Sort((WidthInterval x, WidthInterval y) => x.Min.CompareTo(y.Min));
				float coverage = 0f;
				float min = widths[0].Min;
				float max = widths[0].Max;
				for (int i = 1; i < widths.Count; i++)
				{
					if (widths[i].Min > max)
					{
						coverage += Math.Max(0f, max - min);
						min = widths[i].Min;
						max = widths[i].Max;
					}
					else
					{
						max = Math.Max(max, widths[i].Max);
					}
				}
				return coverage + Math.Max(0f, max - min);
			}
		}

		private struct WidthInterval
		{
			public readonly float Min;

			public readonly float Max;

			public WidthInterval(float min, float max)
			{
				Min = min;
				Max = max;
			}
		}
	}
}
