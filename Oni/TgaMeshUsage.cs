using System;
using System.Collections.Generic;
using System.IO;

namespace Oni
{
	internal static class TgaMeshUsage
	{
		private static readonly object sync = new object();

		private static readonly Dictionary<string, List<string>> meshNamesByTexturePath = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

		public static void Register(string textureFilePath, string meshName)
		{
			if (string.IsNullOrEmpty(textureFilePath) || string.IsNullOrEmpty(meshName))
			{
				return;
			}
			textureFilePath = NormalizePath(textureFilePath);
			meshName = meshName.Trim();
			if (meshName.Length == 0)
			{
				return;
			}
			lock (sync)
			{
				List<string> meshNames;
				if (!meshNamesByTexturePath.TryGetValue(textureFilePath, out meshNames))
				{
					meshNames = new List<string>();
					meshNamesByTexturePath.Add(textureFilePath, meshNames);
				}
				if (!meshNames.Contains(meshName))
				{
					meshNames.Add(meshName);
				}
			}
		}

		public static string AppendUsage(string message, string textureFilePath)
		{
			if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(textureFilePath))
			{
				return message;
			}
			List<string> meshNames;
			lock (sync)
			{
				List<string> registeredMeshNames;
				if (!meshNamesByTexturePath.TryGetValue(NormalizePath(textureFilePath), out registeredMeshNames) || registeredMeshNames.Count == 0)
				{
					return message;
				}
				meshNames = new List<string>(registeredMeshNames);
			}
			meshNames.Sort(StringComparer.Ordinal);
			return string.Format("{0} (meshes: {1})", message, string.Join(", ", meshNames.ToArray()));
		}

		private static string NormalizePath(string textureFilePath)
		{
			return Path.GetFullPath(textureFilePath);
		}
	}
}
