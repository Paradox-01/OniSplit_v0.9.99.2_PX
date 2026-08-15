using System.Collections.Generic;

namespace Oni.Physics
{
	internal class ObjectNode
	{
		public string Name;

		public string FileName;

		public string SourceFilePath;

		public ObjectSetupFlags Flags;

		public int ScriptId;

		public readonly ObjectGeometry[] Geometries;

		public readonly ObjectParticle[] Particles;

		public ObjectAnimation[] Animations = new ObjectAnimation[0];

		public ObjectNode(IEnumerable<ObjectGeometry> geometries)
		{
			Geometries = geometries.ToArray();
			Particles = new ObjectParticle[0];
		}

		public ObjectNode(ObjectGeometry[] geometries, ObjectParticle[] particles)
		{
			Geometries = geometries;
			Particles = particles;
		}
	}
}
