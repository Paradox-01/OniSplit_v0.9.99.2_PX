using System.Collections.Generic;

namespace Oni.Dae
{
	internal class MeshPrimitives
	{
		private readonly MeshPrimitiveType primitiveType;

		private readonly List<IndexedInput> inputs;

		private readonly List<int> vertexCounts;

		// Optional COLLADA extension data used by -getAgqgPerPolygon.
		private readonly List<Dictionary<string, string>> polygonMetadata;

		public MeshPrimitiveType PrimitiveType
		{
			get
			{
				return primitiveType;
			}
		}

		public string MaterialSymbol { get; set; }

		public List<IndexedInput> Inputs
		{
			get
			{
				return inputs;
			}
		}

		public List<int> VertexCounts
		{
			get
			{
				return vertexCounts;
			}
		}

		public string MetadataProfile { get; set; }

		public string MetadataNamespace { get; set; }

		public List<Dictionary<string, string>> PolygonMetadata
		{
			get
			{
				return polygonMetadata;
			}
		}

		public MeshPrimitives(MeshPrimitiveType primitiveType)
		{
			this.primitiveType = primitiveType;
			inputs = new List<IndexedInput>(3);
			vertexCounts = new List<int>();
			polygonMetadata = new List<Dictionary<string, string>>();
		}

		public MeshPrimitives(MeshPrimitiveType primitiveType, IEnumerable<IndexedInput> inputs)
		{
			this.primitiveType = primitiveType;
			this.inputs = new List<IndexedInput>(inputs);
			vertexCounts = new List<int>();
			polygonMetadata = new List<Dictionary<string, string>>();
		}
	}
}
