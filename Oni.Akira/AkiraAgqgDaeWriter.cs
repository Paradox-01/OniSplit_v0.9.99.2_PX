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
			// Recombine normal and special AGQG polygons into one file while retaining separate nodes.
			List<Polygon> polygons = new List<Polygon>(mesh.Polygons.Count + mesh.Ghosts.Count);
			polygons.AddRange(mesh.Polygons);
			polygons.AddRange(mesh.Ghosts);
			polygons.Sort((Polygon x, Polygon y) => x.AgqgIndex.CompareTo(y.AgqgIndex));
			foreach (Polygon polygon in polygons)
			{
				if (polygon.OriginalMaterial != null)
				{
					AkiraDaeWriter.DaeMeshBuilder meshBuilder = sceneBuilder.GetMeshBuilder(GetNodeName(mesh, polygon));
					meshBuilder.AddAgqgPolygon(polygon);
				}
			}
			sceneBuilder.Build();
			sceneBuilder.Write(Path.Combine(outputDirPath, name + "_env_agqg.dae"));
		}

		private static string GetNodeName(PolygonMesh mesh, Polygon polygon)
		{
			if (polygon.ScriptId != 0)
			{
				return string.Format("script_{0}", polygon.ScriptId);
			}
			if (polygon.ObjectType == -1)
			{
				return mesh.HasDebugInfo ? polygon.FileName : "world";
			}
			return string.Format("{0}_{1}", AkiraDaeWriter.objectTypeNames[polygon.ObjectType], polygon.ObjectId);
		}
	}
}
