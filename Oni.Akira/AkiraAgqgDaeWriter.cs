using System;
using System.Collections.Generic;
using System.IO;

namespace Oni.Akira
{
	// Dedicated writer for the -getAgqgPerPolygon output; it does not emit BNV geometry.
	internal static class AkiraAgqgDaeWriter
	{
		public static void Write(PolygonMesh mesh, string name, string outputDirPath)
		{
			AkiraDaeWriter.DaeSceneBuilder sceneBuilder = new AkiraDaeWriter.DaeSceneBuilder();
			AkiraDaeWriter.DaeMeshBuilder meshBuilder = sceneBuilder.GetMeshBuilder("world");
			// Recombine normal and special AGQG polygons into one environment scene.
			List<Polygon> polygons = new List<Polygon>(mesh.Polygons.Count + mesh.Ghosts.Count);
			polygons.AddRange(mesh.Polygons);
			polygons.AddRange(mesh.Ghosts);
			polygons.Sort((Polygon x, Polygon y) => x.AgqgIndex.CompareTo(y.AgqgIndex));
			foreach (Polygon polygon in polygons)
			{
				if (polygon.OriginalMaterial != null)
				{
					meshBuilder.AddAgqgPolygon(polygon);
				}
			}
			sceneBuilder.Build();
			sceneBuilder.Write(Path.Combine(outputDirPath, name + "_env_agqg.dae"));
		}
	}
}
