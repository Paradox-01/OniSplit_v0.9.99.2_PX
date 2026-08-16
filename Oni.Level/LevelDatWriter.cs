using System;
using System.Collections.Generic;
using Oni.Akira;
using Oni.Motoko;
using Oni.Physics;

namespace Oni.Level
{
	internal class LevelDatWriter
	{
		public class DatLevel
		{
			public string name;

			public string skyName;

			public string aisaName;

			public readonly List<ObjectSetup> physics = new List<ObjectSetup>();

			public readonly List<ObjectParticle> particles = new List<ObjectParticle>();

			public readonly List<ScriptCharacter> characters = new List<ScriptCharacter>();

			public readonly List<Corpse> corpses = new List<Corpse>();

			public PolygonMesh model;
		}

		private readonly Importer importer;

		private readonly DatLevel level;

		private LevelDatWriter(Importer importer, DatLevel level)
		{
			this.importer = importer;
			this.level = level;
		}

		public static void Write(Importer importer, DatLevel level)
		{
			LevelDatWriter levelDatWriter = new LevelDatWriter(importer, level);
			levelDatWriter.WriteONLV();
		}

		private void WriteONLV()
		{
			ImporterDescriptor importerDescriptor = importer.CreateInstance(TemplateTag.ONLV, level.name);
			ImporterDescriptor descriptor = importer.CreateInstance(TemplateTag.AKEV, level.name);
			ImporterDescriptor importerDescriptor2 = importer.CreateInstance(TemplateTag.OBOA);
			ImporterDescriptor descriptor2 = importer.CreateInstance(TemplateTag.ONSK, level.skyName);
			ImporterDescriptor importerDescriptor3 = null;
			if (!string.IsNullOrEmpty(level.aisaName))
			{
				importerDescriptor3 = importer.CreateInstance(TemplateTag.AISA, level.aisaName);
			}
			ImporterDescriptor importerDescriptor4 = importer.CreateInstance(TemplateTag.ONOA);
			ImporterDescriptor importerDescriptor5 = importer.CreateInstance(TemplateTag.ENVP);
			ImporterDescriptor importerDescriptor6 = importer.CreateInstance(TemplateTag.CRSA);
			using (BinaryWriter binaryWriter = importerDescriptor.OpenWrite())
			{
				binaryWriter.Write(level.name, 64);
				binaryWriter.Write(descriptor);
				binaryWriter.Write(importerDescriptor2);
				binaryWriter.Write(0);
				binaryWriter.Write(0);
				binaryWriter.Write(0);
				binaryWriter.Write(descriptor2);
				binaryWriter.Write(0f);
				binaryWriter.Write(importerDescriptor3);
				binaryWriter.Write(0);
				binaryWriter.Write(0);
				binaryWriter.Write(0);
				binaryWriter.Write(importerDescriptor4);
				binaryWriter.Write(importerDescriptor5);
				binaryWriter.Skip(644);
				binaryWriter.Write(importerDescriptor6);
			}
			WriteOBOA(importerDescriptor2);
			WriteONOA(importerDescriptor4);
			WriteENVP(importerDescriptor5, level.particles);
			WriteCRSA(importerDescriptor6, level.corpses);
		}

		private void WriteOBOA(ImporterDescriptor oboa)
		{
			List<ObjectSetup> physics = level.physics;
			ImporterDescriptor[] array = new ImporterDescriptor[physics.Count];
			ImporterDescriptor[] array2 = new ImporterDescriptor[physics.Count];
			ImporterDescriptor[] array3 = new ImporterDescriptor[physics.Count];
			for (int i = 0; i < physics.Count; i++)
			{
				ObjectSetup objectSetup = physics[i];
				List<ImporterDescriptor> list = new List<ImporterDescriptor>();
				object[] geometries = objectSetup.Geometries;
				foreach (object obj in geometries)
				{
					if (obj is string)
					{
						list.Add(importer.CreateInstance(TemplateTag.M3GM, (string)obj));
					}
					else
					{
						list.Add(GeometryDatWriter.Write((Geometry)obj, importer.ImporterFile));
					}
				}
				array[i] = importer.CreateInstance(TemplateTag.M3GA);
				WriteM3GA(array[i], list);
				if (objectSetup.Animation != null)
				{
					array2[i] = importer.CreateInstance(TemplateTag.OBAN, objectSetup.Animation.Name);
				}
				if (objectSetup.Particles.Count > 0)
				{
					array3[i] = importer.CreateInstance(TemplateTag.ENVP);
					WriteENVP(array3[i], objectSetup.Particles);
				}
			}
			int num = 32;
			using (BinaryWriter binaryWriter = oboa.OpenWrite(22))
			{
				binaryWriter.WriteUInt16(physics.Count + num);
				for (int k = 0; k != physics.Count; k++)
				{
					ObjectSetup objectSetup2 = physics[k];
					binaryWriter.Write(array[k]);
					binaryWriter.Write(array2[k]);
					binaryWriter.Write(array3[k]);
					binaryWriter.Write((uint)(objectSetup2.Flags | ObjectSetupFlags.InUse));
					binaryWriter.Write(0);
					binaryWriter.Write(objectSetup2.DoorScriptId);
					binaryWriter.Write((uint)objectSetup2.PhysicsType);
					binaryWriter.Write(objectSetup2.ScriptId);
					binaryWriter.Write(objectSetup2.Position);
					binaryWriter.Write(objectSetup2.Orientation);
					binaryWriter.Write(objectSetup2.Scale);
					binaryWriter.WriteMatrix4x3(objectSetup2.Origin);
					binaryWriter.Write(objectSetup2.Name, 64);
					binaryWriter.Write(objectSetup2.FileName, 64);
				}
				binaryWriter.Skip(num * 240);
			}
		}

		private void WriteM3GA(ImporterDescriptor m3ga, ICollection<ImporterDescriptor> geometries)
		{
			using (BinaryWriter binaryWriter = m3ga.OpenWrite(20))
			{
				binaryWriter.Write(geometries.Count);
				binaryWriter.Write(geometries);
			}
		}

		private void WriteAISA(ImporterDescriptor aisa)
		{
			Dictionary<string, ImporterDescriptor> dictionary = new Dictionary<string, ImporterDescriptor>(StringComparer.Ordinal);
			Dictionary<string, ImporterDescriptor> dictionary2 = new Dictionary<string, ImporterDescriptor>(StringComparer.Ordinal);
			foreach (ScriptCharacter character in level.characters)
			{
				if (!dictionary.ContainsKey(character.className))
				{
					dictionary.Add(character.className, importer.CreateInstance(TemplateTag.ONCC, character.className));
				}
				if (!string.IsNullOrEmpty(character.weaponClassName) && !dictionary2.ContainsKey(character.weaponClassName))
				{
					dictionary2.Add(character.weaponClassName, importer.CreateInstance(TemplateTag.ONWC, character.weaponClassName));
				}
			}
			using (BinaryWriter binaryWriter = aisa.OpenWrite(22))
			{
				binaryWriter.WriteUInt16(level.characters.Count);
				foreach (ScriptCharacter character2 in level.characters)
				{
					ImporterDescriptor value;
					dictionary.TryGetValue(character2.className, out value);
					ImporterDescriptor value2;
					if (!string.IsNullOrEmpty(character2.weaponClassName))
					{
						dictionary2.TryGetValue(character2.weaponClassName, out value2);
					}
					else
					{
						value2 = null;
					}
					binaryWriter.Write(character2.name, 32);
					binaryWriter.WriteInt16(character2.scriptId);
					binaryWriter.WriteInt16(character2.flagId);
					binaryWriter.WriteUInt16((int)character2.flags);
					binaryWriter.WriteUInt16((int)character2.team);
					binaryWriter.Write(value);
					binaryWriter.Skip(36);
					binaryWriter.Write(character2.onSpawn, 32);
					binaryWriter.Write(character2.onDeath, 32);
					binaryWriter.Write(character2.onSeenEnemy, 32);
					binaryWriter.Write(character2.onAlarmed, 32);
					binaryWriter.Write(character2.onHurt, 32);
					binaryWriter.Write(character2.onDefeated, 32);
					binaryWriter.Write(character2.onOutOfAmmo, 32);
					binaryWriter.Write(character2.onNoPath, 32);
					binaryWriter.Write(value2);
					binaryWriter.WriteInt16(character2.ammo);
					binaryWriter.Skip(10);
				}
			}
		}

		private void WriteONOA(ImporterDescriptor onoa)
		{
			Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
			int num = 0;
			foreach (Polygon polygon in level.model.Polygons)
			{
				if (polygon.ObjectId > 0)
				{
					int key = (polygon.ObjectType << 24) | polygon.ObjectId;
					List<int> value;
					if (!dictionary.TryGetValue(key, out value))
					{
						value = (dictionary[key] = new List<int>());
					}
					value.Add(num);
				}
				num++;
			}
			List<KeyValuePair<int, ImporterDescriptor>> list2 = new List<KeyValuePair<int, ImporterDescriptor>>();
			foreach (KeyValuePair<int, List<int>> item in dictionary)
			{
				ImporterDescriptor importerDescriptor = importer.CreateInstance(TemplateTag.IDXA);
				list2.Add(new KeyValuePair<int, ImporterDescriptor>(item.Key, importerDescriptor));
				using (BinaryWriter binaryWriter = importerDescriptor.OpenWrite(20))
				{
					binaryWriter.Write(item.Value.Count);
					binaryWriter.Write(item.Value.ToArray());
				}
			}
			using (BinaryWriter binaryWriter2 = onoa.OpenWrite(20))
			{
				binaryWriter2.Write(list2.Count);
				foreach (KeyValuePair<int, ImporterDescriptor> item2 in list2)
				{
					binaryWriter2.Write(item2.Key);
					binaryWriter2.Write(item2.Value);
				}
			}
		}

		private void WriteENVP(ImporterDescriptor envp, List<ObjectParticle> particles)
		{
			using (BinaryWriter binaryWriter = envp.OpenWrite(22))
			{
				binaryWriter.WriteUInt16(particles.Count);
				foreach (ObjectParticle particle in particles)
				{
					binaryWriter.Write(particle.ParticleClass, 64);
					binaryWriter.Write(particle.Tag, 48);
					binaryWriter.WriteMatrix4x3(particle.Matrix);
					binaryWriter.Write(particle.DecalScale);
					binaryWriter.Write((ushort)particle.Flags);
					binaryWriter.Skip(38);
				}
			}
		}

		private void WriteCRSA(ImporterDescriptor crsa, List<Corpse> corpses)
		{
			while (corpses.Count < 20)
			{
				corpses.Add(new Corpse());
			}
			int value = corpses.Count((Corpse c) => c.IsFixed);
			int num = corpses.Count((Corpse c) => c.IsUsed);
			while (corpses.Count - num < 5)
			{
				corpses.Add(new Corpse());
			}
			corpses.Sort((Corpse x, Corpse y) => x.Order.CompareTo(y.Order));
			Dictionary<string, ImporterDescriptor> dictionary = new Dictionary<string, ImporterDescriptor>();
			using (BinaryWriter binaryWriter = crsa.OpenWrite(12))
			{
				binaryWriter.Write(value);
				binaryWriter.Write(num);
				binaryWriter.Write(corpses.Count);
				foreach (Corpse corpse in corpses)
				{
					binaryWriter.Write(corpse.FileName ?? "", 32);
					binaryWriter.Skip(128);
					if (corpse.IsUsed)
					{
						ImporterDescriptor value2 = null;
						if (!string.IsNullOrEmpty(corpse.CharacterClass) && !dictionary.TryGetValue(corpse.CharacterClass, out value2))
						{
							value2 = importer.CreateInstance(TemplateTag.ONCC, corpse.CharacterClass);
							dictionary.Add(corpse.CharacterClass, value2);
						}
						binaryWriter.Write(value2);
						Matrix[] transforms = corpse.Transforms;
						foreach (Matrix m in transforms)
						{
							binaryWriter.WriteMatrix4x3(m);
						}
						binaryWriter.Write(corpse.BoundingBox);
					}
					else
					{
						binaryWriter.Skip(940);
					}
				}
			}
		}
	}
}
