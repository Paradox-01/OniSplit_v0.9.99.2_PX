using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Oni.Akira;
using Oni.Collections;
using Oni.Dae;
using Oni.Dae.IO;
using Oni.Level;
using Oni.Metadata;
using Oni.Motoko;
using Oni.Particles;
using Oni.Physics;
using Oni.Sound;
using Oni.Totoro;
using Oni.Xml;

namespace Oni
{
	internal class Program
	{
		private static readonly InstanceFileManager fileManager = new InstanceFileManager();

		private static int Main(string[] args)
		{
			if (args.Length == 0)
			{
				Help(args);
				Console.WriteLine("Press any key to continue");
				Console.ReadKey();
				return 0;
			}
			if (args[0] == "-cdump")
			{
				InstanceMetadata.DumpCStructs(Console.Out);
				return 0;
			}
			DaeReader.CommandLineArgs = args;
			if (args[0] == "-silent")
			{
				Console.SetOut(new StreamWriter(Stream.Null));
				Console.SetError(new StreamWriter(Stream.Null));
				string[] array = new string[args.Length - 1];
				Array.Copy(args, 1, array, 0, array.Length);
				args = array;
			}
			if (args[0] == "-noexcept")
			{
				string[] array2 = new string[args.Length - 1];
				Array.Copy(args, 1, array2, 0, array2.Length);
				args = array2;
				return Execute(args);
			}
			args = AddSearchPaths(args);
			try
			{
				return Execute(args);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex.ToString());
				return 1;
			}
		}

		private static int Execute(string[] args)
		{
			if (args[0].StartsWith("-export:", StringComparison.Ordinal))
			{
				return Unpack(args);
			}
			switch (args[0])
			{
			case "-help":
				return Help(args);
			case "-version":
				return PrintVersion();
			case "-export":
				return Unpack(args);
			case "pack":
			case "-import":
			case "-import:nosep":
			case "-import:sep":
			case "-import:ppc":
			case "-import:pc":
				return Pack(args);
			case "-copy":
				return Copy(args);
			case "-move":
			case "-move:overwrite":
			case "-move:delete":
				return Move(args);
			case "-list":
				return List(args);
			case "-deps":
				return GetDependencies(args);
			case "extract":
			case "-extract:xml":
				return ExportXml(args);
			case "-extract:tga":
			case "-extract:dds":
			case "-extract:png":
			case "-extract:jpg":
			case "-extract:bmp":
			case "-extract:tif":
				return ExportTextures(args);
			case "-extract:wav":
			case "-extract:aif":
				return ExportSounds(args);
			case "-extract:obj":
			case "-extract:dae":
				return ExportGeometry(args);
			case "-extract:txt":
				return ExportSubtitles(args);
			case "-create:akev":
				return CreateAkira(args);
			case "-create:tram":
			case "-create:trbs":
			case "-create:txmp":
			case "-create:m3gm":
			case "-create:subt":
			case "-create:oban":
			case "-create":
			case "create":
				return CreateGeneric(args);
			case "-grid:create":
				return CreateGrids(args);
			case "-create:level":
				return ImportLevel(args);
			case "-room:extract":
				return ExtractRooms(args);
			case "film2xml":
				return ConvertFilm2Xml(args);
			default:
				Console.Error.WriteLine("Unknown command {0}", args[0]);
				return 1;
			}
		}

		private static string[] AddSearchPaths(string[] args)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "-search")
				{
					i++;
					if (i < args.Length)
					{
						fileManager.AddSearchPath(args[i]);
					}
				}
				else
				{
					list.Add(args[i]);
				}
			}
			return list.ToArray();
		}

		private static int Help(string[] args)
		{
			if (args.Length > 1 && args[1] == "enums")
			{
				HelpEnums();
				return 0;
			}
			Console.WriteLine("{0} [options] datfile", Environment.GetCommandLineArgs()[0]);
			Console.WriteLine();
			Console.WriteLine("Options:");
			Console.WriteLine("\t-export <directory>\t\tExport a Oni .dat file to directory");
			Console.WriteLine("\t-import <directory>\t\tImport a Oni .dat file from directory");
			Console.WriteLine("\t\t\t\t\tTarget file format is determined from source files (when possible)");
			Console.WriteLine("\t-import:sep <directory>\t\tImport a Oni .dat file from directory");
			Console.WriteLine("\t\t\t\t\tCreate a .dat file that uses .raw and .sep binary files (Mac and PC Demo)");
			Console.WriteLine("\t-import:nosep <directory>\tImport a Oni .dat file from directory");
			Console.WriteLine("\t\t\t\t\tCreate a .dat file that uses only .raw binary file (PC)");
			Console.WriteLine();
			Console.WriteLine("\t-extract:dds <directory>\tExtracts all textures (TXMP) from a Oni .dat/.oni file in DDS format");
			Console.WriteLine("\t-extract:tga <directory>\tExtracts all textures (TXMP) from a Oni .dat/.oni file in TGA format");
			Console.WriteLine("\t-extract:png <directory>\tExtracts all textures (TXMP) from a Oni .dat/.oni file in PNG format");
			Console.WriteLine("\t-extract:wav <directory>\tExtracts all sounds (SNDD) from a Oni .dat/.oni file in WAV format");
			Console.WriteLine("\t-extract:aif <directory>\tExtracts all sounds (SNDD) from a Oni .dat/.oni file in AIF format");
			Console.WriteLine("\t-extract:txt <directory>\tExtracts all subtitles (SUBT) from a Oni .dat/.oni file in TXT format");
			Console.WriteLine("\t-extract:obj <directory>\tExtracts all M3GM and ONCC instances to Wavefront OBJ files");
			Console.WriteLine("\t-extract:dae <directory>\tExtracts all M3GM, ONCC, and AKEV instances to Collada files");
			Console.WriteLine("\t\t<AKEV input> [-getVanillaStairs]\tRecover implicit vanilla stair ramps from AKEV input");
			Console.WriteLine("\t\t<AKEV input> [-getAgqgPerPolygon]\tAlso export original materials and per-polygon AGQG metadata");
			Console.WriteLine("\t\t\t[-getLevelWithAgqgFlagsPerPolygon] is an alias for -getAgqgPerPolygon");
			Console.WriteLine("\t-extract:xml <directory>\tExtracts all instances to XML files");
			Console.WriteLine();
			Console.WriteLine("\t-create:txmp <directory> [-nomipmaps] [-nouwrap] [-novwrap] [-format:bgr|rgba|bgr555|bgra5551|bgra4444|dxt1] [-envmap:texture_name] [-large] image_file");
			Console.WriteLine("\t-create:m3gm <directory> [-tex:texture_name] obj_file");
			Console.WriteLine("\t-create:trbs <directory> dae_file");
			Console.WriteLine("\t-create:subt <directory> txt_file");
			Console.WriteLine("\t-create <directory> xml_file\tCreates an .oni file from an XML file");
			Console.WriteLine();
			Console.WriteLine("\t-grid:create -out:<directory> rooms_src.dae level_geometry1.dae level_geometry2.dae ...\tGenerates pathfinding grids");
			Console.WriteLine();
			Console.WriteLine("\t-list\t\t\t\tLists the named instances contained in datfile");
			Console.WriteLine("\t-copy <directory>\t\tCopy an exported .oni file and its dependencies to directory");
			Console.WriteLine("\t-move <directory>\t\tMove an exported .oni file and its dependencies to directory");
			Console.WriteLine("\t-move:overwrite <directory>\tMove an exported .oni file and its dependencies to directory");
			Console.WriteLine("\t\t\t\t\tOverwrites any existing files");
			Console.WriteLine("\t-move:delete <directory>\tMove an exported .oni file and its dependencies to directory");
			Console.WriteLine("\t\t\t\t\tDeletes files at source when they already exist at destination");
			Console.WriteLine("\t-deps\t\t\t\tGet a list of exported .oni files the specified files depends on");
			Console.WriteLine("\t-version\t\t\tShow OniSplit versions");
			Console.WriteLine("\t-help\t\t\t\tShow this help");
			Console.WriteLine("\t-help enums\t\t\tShow a list of enums and flags used in XML files");
			Console.WriteLine();
			return 0;
		}

		private static void HelpEnums()
		{
			WriteEnums(typeof(InstanceMetadata));
			WriteEnums(typeof(ObjectMetadata));
			Console.WriteLine("-----------------------------------------------------");
			Console.WriteLine("Particles enums");
			Console.WriteLine("-----------------------------------------------------");
			Console.WriteLine();
			Utils.WriteEnum(typeof(ParticleFlags1));
			Utils.WriteEnum(typeof(ParticleFlags2));
			Utils.WriteEnum(typeof(EmitterFlags));
			Utils.WriteEnum(typeof(EmitterOrientation));
			Utils.WriteEnum(typeof(EmitterPosition));
			Utils.WriteEnum(typeof(EmitterRate));
			Utils.WriteEnum(typeof(EmitterSpeed));
			Utils.WriteEnum(typeof(EmitterDirection));
			Utils.WriteEnum(typeof(DisableDetailLevel));
			Utils.WriteEnum(typeof(AttractorSelector));
			Utils.WriteEnum(typeof(AttractorTarget));
			Utils.WriteEnum(typeof(EventType));
			Utils.WriteEnum(typeof(SpriteType));
			Utils.WriteEnum(typeof(StorageType));
			Utils.WriteEnum(typeof(Oni.Particles.ValueType));
			Console.WriteLine("-----------------------------------------------------");
			Console.WriteLine("Object enums");
			Console.WriteLine("-----------------------------------------------------");
			Console.WriteLine();
			Utils.WriteEnum(typeof(ObjectSetupFlags));
			Utils.WriteEnum(typeof(ObjectPhysicsType));
			Utils.WriteEnum(typeof(ObjectAnimationFlags));
		}

		private static void WriteEnums(Type type)
		{
			Type[] nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
			foreach (Type type2 in nestedTypes)
			{
				if (type2.IsEnum)
				{
					Utils.WriteEnum(type2);
				}
			}
		}

		private static int Unpack(string[] args)
		{
			if (args.Length < 3)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			List<string> list = new List<string>();
			string text = null;
			string text2 = null;
			foreach (string text3 in args)
			{
				if (text3.StartsWith("-export", StringComparison.Ordinal))
				{
					string text4 = null;
					int num = text3.IndexOf(':');
					if (num != -1)
					{
						text4 = text3.Substring(num + 1);
					}
					if (!string.IsNullOrEmpty(text4))
					{
						list.Add(text4);
					}
				}
				else if (text == null)
				{
					text = Path.GetFullPath(text3);
				}
				else if (text2 == null)
				{
					text2 = Path.GetFullPath(text3);
				}
			}
			DatUnpacker datUnpacker = new DatUnpacker(fileManager, text);
			if (list.Count > 0)
			{
				datUnpacker.NameFilter = Utils.WildcardToRegex(list);
			}
			datUnpacker.ExportFiles(new string[1] { text2 });
			return 0;
		}

		private static int ExportTextures(string[] args)
		{
			if (args.Length < 3)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			int num = args[0].IndexOf(':');
			string fileType = null;
			if (num != -1)
			{
				fileType = args[0].Substring(num + 1);
			}
			string fullPath = Path.GetFullPath(args[1]);
			List<string> fileList = GetFileList(args, 2);
			TextureExporter textureExporter = new TextureExporter(fileManager, fullPath, fileType);
			textureExporter.ExportFiles(fileList);
			return 0;
		}

		private static int ExportSounds(string[] args)
		{
			if (args.Length < 3)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			int num = args[0].IndexOf(':');
			string text = null;
			if (num != -1)
			{
				text = args[0].Substring(num + 1);
			}
			string fullPath = Path.GetFullPath(args[1]);
			List<string> fileList = GetFileList(args, 2);
			SoundExporter soundExporter;
			switch (text)
			{
			case "aif":
				soundExporter = new AifExporter(fileManager, fullPath);
				break;
			case "wav":
				soundExporter = new WavExporter(fileManager, fullPath);
				break;
			default:
				throw new NotSupportedException(string.Format("Unsupported file type {0}", text));
			}
			soundExporter.ExportFiles(fileList);
			return 0;
		}

		private static int ExportGeometry(string[] args)
		{
			if (args.Length < 3)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			int num = args[0].IndexOf(':');
			string fileType = null;
			if (num != -1)
			{
				fileType = args[0].Substring(num + 1);
			}
			bool getAgqgPerPolygon = Array.IndexOf(args, "-getAgqgPerPolygon") >= 0 || Array.IndexOf(args, "-getLevelWithAgqgFlagsPerPolygon") >= 0;
			bool getVanillaStairs = !getAgqgPerPolygon && Array.IndexOf(args, "-getVanillaStairs") >= 0;
			if ((getVanillaStairs || getAgqgPerPolygon) && !string.Equals(fileType, "dae", StringComparison.Ordinal))
			{
				throw new ArgumentException("-getVanillaStairs and -getAgqgPerPolygon are supported only by -extract:dae for AKEV input.");
			}
			string fullPath = Path.GetFullPath(args[1]);
			List<string> fileList = GetFileList(args, 2);
			DaeExporter daeExporter = new DaeExporter(args, fileManager, fullPath, fileType, getVanillaStairs, getAgqgPerPolygon);
			daeExporter.ExportFiles(fileList);
			return 0;
		}

		private static int ExportSubtitles(string[] args)
		{
			if (args.Length < 3)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			string fullPath = Path.GetFullPath(args[1]);
			List<string> fileList = GetFileList(args, 2);
			SubtitleExporter subtitleExporter = new SubtitleExporter(fileManager, fullPath);
			subtitleExporter.ExportFiles(fileList);
			return 0;
		}

		private static int ExportXml(string[] args)
		{
			if (args.Length < 3)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			string fullPath = Path.GetFullPath(args[1]);
			List<string> fileList = GetFileList(args, 2);
			XmlExporter xmlExporter = new XmlExporter(fileManager, fullPath)
			{
				Recursive = args.Any((string a) => a == "-recurse"),
				MergeAnimations = args.Any((string a) => a == "-anim-merge"),
				NoAnimation = args.Any((string a) => a == "-noanim")
			};
			string text = args.FirstOrDefault((string a) => a.StartsWith("-anim-body:", StringComparison.Ordinal));
			if (text != null)
			{
				text = Path.GetFullPath(text.Substring("-anim-body:".Length));
				InstanceFile instanceFile = fileManager.OpenFile(text);
				xmlExporter.AnimationBody = BodyDatReader.Read(instanceFile.Descriptors[0]);
			}
			xmlExporter.ExportFiles(fileList);
			return 0;
		}

		private static int Pack(string[] args)
		{
			if (args.Length < 3)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			DatPacker datPacker = new DatPacker();
			List<string> list = new List<string>();
			if (args[0] == "pack")
			{
				for (int i = 1; i < args.Length; i++)
				{
					string text = args[i];
					if (text == "-out")
					{
						i++;
						datPacker.TargetFilePath = Path.GetFullPath(args[i]);
						continue;
					}
					if (text.StartsWith("-type:", StringComparison.Ordinal))
					{
						switch (text.Substring(6))
						{
						case "nosep":
						case "pc":
							datPacker.TargetTemplateChecksum = 1052091763926815L;
							break;
						case "sep":
						case "pcdemo":
						case "macintel":
							datPacker.TargetTemplateChecksum = 1052091493724257L;
							break;
						case "ppc":
							datPacker.TargetTemplateChecksum = 1052091493724257L;
							datPacker.TargetBigEndian = true;
							break;
						default:
							throw new ArgumentException(string.Format("Unknown output type {0}", text.Substring(6)));
						}
						continue;
					}
					if (Directory.Exists(text))
					{
						text = Path.GetFullPath(text);
						Console.WriteLine("Reading directory {0}", text);
						list.AddRange(Directory.GetFiles(text, "*.oni", SearchOption.AllDirectories));
						continue;
					}
					string directoryName = Path.GetDirectoryName(text);
					string fileName = Path.GetFileName(text);
					directoryName = ((!string.IsNullOrEmpty(directoryName)) ? Path.GetFullPath(directoryName) : Directory.GetCurrentDirectory());
					if (Directory.Exists(directoryName))
					{
						string[] files = Directory.GetFiles(directoryName, fileName);
						foreach (string text2 in files)
						{
							Console.WriteLine("Reading {0}", text2);
							list.Add(text2);
						}
					}
				}
				datPacker.Pack(fileManager, list);
			}
			else
			{
				switch (args[0])
				{
				case "-import:nosep":
				case "-import:pc":
					datPacker.TargetTemplateChecksum = 1052091763926815L;
					break;
				case "-import:sep":
				case "-import:pcdemo":
				case "-import:macintel":
					datPacker.TargetTemplateChecksum = 1052091493724257L;
					break;
				case "-import:ppc":
					datPacker.TargetTemplateChecksum = 1052091493724257L;
					datPacker.TargetBigEndian = true;
					break;
				}
				for (int k = 1; k < args.Length - 1; k++)
				{
					list.Add(Path.GetFullPath(args[k]));
				}
				datPacker.TargetFilePath = Path.GetFullPath(args[args.Length - 1]);
				datPacker.Import(fileManager, list.ToArray());
			}
			return 0;
		}

		private static int Copy(string[] args)
		{
			if (args.Length < 3)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			string fullPath = Path.GetFullPath(args[1]);
			List<string> fileList = GetFileList(args, 2);
			InstanceFileOperations instanceFileOperations = new InstanceFileOperations();
			instanceFileOperations.Copy(fileManager, fileList, fullPath);
			return 0;
		}

		private static int Move(string[] args)
		{
			if (args.Length < 3)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			string fullPath = Path.GetFullPath(args[1]);
			List<string> fileList = GetFileList(args, 2);
			InstanceFileOperations instanceFileOperations = new InstanceFileOperations();
			if (args[0] == "-move:delete")
			{
				instanceFileOperations.MoveDelete(fileManager, fileList, fullPath);
			}
			else if (args[0] == "-move:overwrite")
			{
				instanceFileOperations.MoveOverwrite(fileManager, fileList, fullPath);
			}
			else
			{
				instanceFileOperations.Move(fileManager, fileList, fullPath);
			}
			return 0;
		}

		private static int List(string[] args)
		{
			if (args.Length < 2)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			string fullPath = Path.GetFullPath(args[1]);
			InstanceFile instanceFile = fileManager.OpenFile(fullPath);
			foreach (InstanceDescriptor namedDescriptor in instanceFile.GetNamedDescriptors())
			{
				Console.WriteLine(namedDescriptor.FullName);
			}
			return 0;
		}

		private static int GetDependencies(string[] args)
		{
			if (args.Length < 2)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			List<string> fileList = GetFileList(args, 1);
			InstanceFileOperations instanceFileOperations = new InstanceFileOperations();
			instanceFileOperations.GetDependencies(fileManager, fileList);
			return 0;
		}

		private static int PrintVersion()
		{
			Console.WriteLine("OniSplit version {0}", Utils.Version);
			return 0;
		}

		private static int CreateGrids(string[] args)
		{
			if (args.Length < 2)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			List<string> fileList = GetFileList(args, 1);
			Scene roomsScene = Reader.ReadFile(fileList[0]);
			PolygonMesh geometryMesh = AkiraDaeReader.Read(fileList.Skip(1));
			RoomGridBuilder roomGridBuilder = new RoomGridBuilder(roomsScene, geometryMesh);
			string text = null;
			foreach (string text2 in args)
			{
				if (text2.StartsWith("-out:", StringComparison.Ordinal))
				{
					text = Path.GetFullPath(text2.Substring(5));
				}
			}
			if (string.IsNullOrEmpty(text))
			{
				Console.Error.WriteLine("Output path must be specified");
				return 1;
			}
			roomGridBuilder.Build();
			AkiraDaeWriter.WriteRooms(roomGridBuilder.Mesh, Path.GetFileNameWithoutExtension(fileList[0]), text);
			return 0;
		}

		private static int ExtractRooms(string[] args)
		{
			if (args.Length < 2)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			string text = null;
			foreach (string text2 in args)
			{
				if (text2.StartsWith("-out:", StringComparison.Ordinal))
				{
					text = Path.GetFullPath(text2.Substring(5));
				}
			}
			if (string.IsNullOrEmpty(text))
			{
				Console.Error.WriteLine("Output file path must be specified");
				return 1;
			}
			RoomExtractor roomExtractor = new RoomExtractor(GetFileList(args, 1), text);
			roomExtractor.Extract();
			return 0;
		}

		private static int CreateAkira(string[] args)
		{
			if (args.Length < 2)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			string fullPath = Path.GetFullPath(args[1]);
			List<string> fileList = GetFileList(args, 2);
			Directory.CreateDirectory(fullPath);
			Set<string> set = new Set<string>(StringComparer.OrdinalIgnoreCase);
			Queue<ImporterTask> queue = new Queue<ImporterTask>();
			foreach (string item in fileList)
			{
				set.Add(item);
			}
			AkiraImporter akiraImporter = new AkiraImporter(args);
			Console.WriteLine("Importing {0}", fileList[0]);
			akiraImporter.Import(fileList, fullPath);
			QueueTasks(set, queue, akiraImporter);
			ExecuteTasks(args, fullPath, set, queue);
			return 0;
		}

		private static int CreateGeneric(string[] args)
		{
			if (args.Length < 2)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			TemplateTag type = TemplateTag.NONE;
			int num = args[0].IndexOf(':');
			if (num != -1)
			{
				string value = args[0].Substring(num + 1);
				type = (TemplateTag)Enum.Parse(typeof(TemplateTag), value, true);
			}
			string fullPath = Path.GetFullPath(args[1]);
			List<string> fileList = GetFileList(args, 2);
			Directory.CreateDirectory(fullPath);
			Set<string> set = new Set<string>(StringComparer.OrdinalIgnoreCase);
			Queue<ImporterTask> queue = new Queue<ImporterTask>();
			foreach (string item in fileList)
			{
				if (set.Add(item))
				{
					queue.Enqueue(new ImporterTask(item, type));
				}
			}
			ExecuteTasks(args, fullPath, set, queue);
			return 0;
		}

		private static void ExecuteTasks(string[] args, string outputDirPath, Set<string> importedFiles, Queue<ImporterTask> taskQueue)
		{
			while (taskQueue.Count > 0)
			{
				ImporterTask task = taskQueue.Dequeue();
				if (!File.Exists(task.FilePath))
				{
					Console.Error.WriteLine("File {0} does not exist", task.FilePath);
					continue;
				}
				Importer importer = CreateImporterFromFileName(args, task);
				if (importer == null)
				{
					Console.Error.WriteLine("{0} files cannot be imported as {1}", Path.GetExtension(task.FilePath), task.Type);
					continue;
				}
				Console.WriteLine("Importing {0}", task.FilePath);
				importer.Import(task.FilePath, outputDirPath);
				QueueTasks(importedFiles, taskQueue, importer);
			}
		}

		private static Importer CreateImporterFromFileName(string[] args, ImporterTask task)
		{
			Importer result = null;
			switch (Path.GetExtension(task.FilePath).ToLowerInvariant())
			{
			case ".bin":
				result = new BinImporter();
				break;
			case ".xml":
				result = new XmlImporter(args);
				break;
			case ".tga":
			case ".dds":
			case ".png":
			case ".jpg":
			case ".bmp":
			case ".tif":
				if (task.Type == TemplateTag.NONE || task.Type == TemplateTag.TXMP)
				{
					result = new TextureImporter(args);
				}
				break;
			case ".obj":
			case ".dae":
				if (task.Type == TemplateTag.NONE || task.Type == TemplateTag.M3GM)
				{
					result = new GeometryImporter(args);
				}
				else if (task.Type == TemplateTag.AKEV)
				{
					result = new AkiraImporter(args);
				}
				else if (task.Type == TemplateTag.TRBS)
				{
					result = new BodySetImporter(args);
				}
				else if (task.Type == TemplateTag.OBAN)
				{
					result = new ObjectAnimationImporter(args);
				}
				break;
			case ".wav":
				if (task.Type == TemplateTag.NONE || task.Type == TemplateTag.SNDD)
				{
					result = new WavImporter();
				}
				break;
			case ".aif":
			case ".aifc":
			case ".afc":
				if (task.Type == TemplateTag.NONE || task.Type == TemplateTag.SNDD)
				{
					result = new AifImporter();
				}
				break;
			case ".txt":
				if (task.Type == TemplateTag.NONE || task.Type == TemplateTag.SUBT)
				{
					result = new SubtitleImporter();
				}
				break;
			}
			return result;
		}

		private static void QueueTasks(Set<string> imported, Queue<ImporterTask> importQueue, Importer importer)
		{
			foreach (ImporterTask dependency in importer.Dependencies)
			{
				if (!imported.Contains(dependency.FilePath))
				{
					imported.Add(dependency.FilePath);
					importQueue.Enqueue(dependency);
				}
			}
		}

		private static int ConvertFilm2Xml(string[] args)
		{
			if (args.Length < 3)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			string fullPath = Path.GetFullPath(args[1]);
			List<string> fileList = GetFileList(args, 2);
			Directory.CreateDirectory(fullPath);
			foreach (string item in fileList)
			{
				FilmToXmlConverter.Convert(item, fullPath);
			}
			return 0;
		}

		private static int ImportLevel(string[] args)
		{
			if (args.Length < 2)
			{
				Console.Error.WriteLine("Invalid command line.");
				return 1;
			}
			LevelImporter levelImporter = new LevelImporter
			{
				Debug = args.Any((string a) => a == "-debug")
			};
			string fullPath = Path.GetFullPath(args[1]);
			if (string.IsNullOrEmpty(fullPath))
			{
				Console.Error.WriteLine("Output path must be specified");
				return 1;
			}
			List<string> fileList = GetFileList(args, 2);
			if (fileList.Count == 0)
			{
				Console.Error.WriteLine("No input files specified");
				return 1;
			}
			if (fileList.Count > 1)
			{
				Console.Error.WriteLine("Too many input files specified, only one level can be created at a time");
				return 1;
			}
			levelImporter.Import(fileList[0], fullPath);
			return 0;
		}

		private static List<string> GetFileList(string[] args, int startIndex)
		{
			Set<string> set = new Set<string>(StringComparer.OrdinalIgnoreCase);
			List<string> list = new List<string>();
			foreach (string item in args.Skip(startIndex))
			{
				if (item[0] == '-')
				{
					continue;
				}
				string directoryName = Path.GetDirectoryName(item);
				string fileName = Path.GetFileName(item);
				directoryName = ((!string.IsNullOrEmpty(directoryName)) ? Path.GetFullPath(directoryName) : Directory.GetCurrentDirectory());
				if (!Directory.Exists(directoryName))
				{
					continue;
				}
				string[] files = Directory.GetFiles(directoryName, fileName);
				foreach (string text in files)
				{
					if (set.Add(text))
					{
						list.Add(text);
					}
				}
			}
			if (list.Count == 0)
			{
				throw new ArgumentException("No input files found");
			}
			return list;
		}
	}
}
