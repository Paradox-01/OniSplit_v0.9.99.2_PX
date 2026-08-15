using System;
using System.Collections.Generic;

namespace Oni.Objects
{
	internal class ObjcDatWriter : Importer
	{
		private enum TypeTag
		{
			CHAR = 1128808786,
			CMBT = 1129136724,
			CONS = 1129270867,
			DOOR = 1146048338,
			FLAG = 1179402567,
			FURN = 1179996750,
			MELE = 1296387141,
			NEUT = 1313166676,
			PART = 1346458196,
			PATR = 1346458706,
			PWRU = 1347899989,
			SNDG = 1397638215,
			TRGV = 1414678358,
			TRIG = 1414678855,
			TURR = 1414877778,
			WEAP = 1464156496
		}

		private readonly TypeTag tag;

		private readonly string name;

		private readonly List<ObjectBase> objects;

		private readonly Type type;

		private ObjcDatWriter(TypeTag tag, string name, List<ObjectBase> objects)
		{
			this.tag = tag;
			this.name = name;
			this.objects = objects;
			switch (tag)
			{
			case TypeTag.CHAR:
				type = typeof(Character);
				break;
			case TypeTag.WEAP:
				type = typeof(Weapon);
				break;
			case TypeTag.PART:
				type = typeof(Particle);
				break;
			case TypeTag.PWRU:
				type = typeof(PowerUp);
				break;
			case TypeTag.FLAG:
				type = typeof(Flag);
				break;
			case TypeTag.DOOR:
				type = typeof(Door);
				break;
			case TypeTag.CONS:
				type = typeof(Console);
				break;
			case TypeTag.FURN:
				type = typeof(Furniture);
				break;
			case TypeTag.TRIG:
				type = typeof(Trigger);
				break;
			case TypeTag.TRGV:
				type = typeof(TriggerVolume);
				break;
			case TypeTag.SNDG:
				type = typeof(Sound);
				break;
			case TypeTag.TURR:
				type = typeof(Turret);
				break;
			case TypeTag.NEUT:
				type = typeof(Neutral);
				break;
			case TypeTag.PATR:
				type = typeof(PatrolPath);
				break;
			}
		}

		public static void Write(List<ObjectBase> objects, string outputDirPath, string inputFilePath)
		{
			System.Console.Error.WriteLine("Writing {0} objects...", objects.Count);
			Write(TypeTag.CHAR, "Character", objects, outputDirPath, inputFilePath);
			Write(TypeTag.CONS, "Console", objects, outputDirPath, inputFilePath);
			Write(TypeTag.DOOR, "Door", objects, outputDirPath, inputFilePath);
			Write(TypeTag.FLAG, "Flag", objects, outputDirPath, inputFilePath);
			Write(TypeTag.FURN, "Furniture", objects, outputDirPath, inputFilePath);
			Write(TypeTag.NEUT, "Neutral", objects, outputDirPath, inputFilePath);
			Write(TypeTag.PART, "Particle", objects, outputDirPath, inputFilePath);
			Write(TypeTag.PATR, "Patrol Path", objects, outputDirPath, inputFilePath);
			Write(TypeTag.PWRU, "PowerUp", objects, outputDirPath, inputFilePath);
			Write(TypeTag.SNDG, "Sound", objects, outputDirPath, inputFilePath);
			Write(TypeTag.TRIG, "Trigger", objects, outputDirPath, inputFilePath);
			Write(TypeTag.TRGV, "Trigger Volume", objects, outputDirPath, inputFilePath);
			Write(TypeTag.TURR, "Turret", objects, outputDirPath, inputFilePath);
			Write(TypeTag.WEAP, "Weapon", objects, outputDirPath, inputFilePath);
		}

		private static void Write(TypeTag tag, string name, List<ObjectBase> objects, string outputDirPath, string inputFilePath)
		{
			ObjcDatWriter objcDatWriter = new ObjcDatWriter(tag, name, objects);
			objcDatWriter.Import(inputFilePath, outputDirPath);
		}

		public override void Import(string filePath, string outputDirPath)
		{
			BeginImport();
			ImporterDescriptor importerDescriptor = CreateInstance(TemplateTag.BINA, "CJBO" + name);
			int value = base.RawWriter.Align32();
			int value2 = WriteCollection(base.RawWriter);
			using (BinaryWriter binaryWriter = importerDescriptor.OpenWrite())
			{
				binaryWriter.Write(value2);
				binaryWriter.Write(value);
			}
			Write(outputDirPath, filePath);
		}

		private int WriteCollection(BinaryWriter raw)
		{
			int position = raw.Position;
			raw.Write(1329744451);
			raw.Write(0);
			raw.Write(39);
			foreach (ObjectBase item in objects.Where((ObjectBase o) => o.GetType() == type))
			{
				int position2 = raw.Position;
				raw.Write(0);
				raw.Write((int)tag);
				item.Write(raw);
				raw.Position = Utils.Align4(raw.Position);
				int value = raw.Position - position2 - 4;
				raw.WriteAt(position2, value);
			}
			raw.Write(0);
			int num = raw.Position - position;
			raw.WriteAt(position + 4, num - 8);
			return num;
		}
	}
}
