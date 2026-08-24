using System;
using System.Collections.Generic;
using System.IO;
using Oni.Imaging;
using Oni.Motoko;

namespace Oni.Akira
{
	internal class AkiraDatReader
	{
		private class DatRoom
		{
			public readonly int BspRootIndex;

			public readonly int SideListStart;

			public readonly int SideListEnd;

			public readonly int ChildIndex;

			public readonly int SiblingIndex;

			public readonly int XTiles;

			public readonly int ZTiles;

			public readonly BoundingBox BoundingBox;

			public readonly float TileSize;

			public readonly int XOrigin;

			public readonly int ZOrigin;

			public readonly RoomFlags Flags;

			public readonly Plane Floor;

			public readonly float Height;

			public readonly byte[] CompressedGridData;

			public DatRoom(InstanceDescriptor descriptor, BinaryReader reader)
			{
				BspRootIndex = reader.ReadInt32();
				reader.Skip(4);
				SideListStart = reader.ReadInt32();
				SideListEnd = reader.ReadInt32();
				ChildIndex = reader.ReadInt32();
				SiblingIndex = reader.ReadInt32();
				reader.Skip(4);
				XTiles = reader.ReadInt32();
				ZTiles = reader.ReadInt32();
				int num = reader.ReadInt32();
				int num2 = reader.ReadInt32();
				TileSize = reader.ReadSingle();
				BoundingBox = reader.ReadBoundingBox();
				XOrigin = reader.ReadInt16();
				ZOrigin = reader.ReadInt16();
				reader.Skip(16);
				Flags = (RoomFlags)reader.ReadInt32();
				Floor = reader.ReadPlane();
				Height = reader.ReadSingle();
				if (num != 0 && num2 != 0)
				{
					using (BinaryReader binaryReader = descriptor.GetRawReader(num))
					{
						CompressedGridData = binaryReader.ReadBytes(num2);
					}
				}
			}
		}

		private class DatRoomBspNode
		{
			public readonly int PlaneIndex;

			public readonly int FrontChildIndex;

			public readonly int BackChildIndex;

			public DatRoomBspNode(BinaryReader reader)
			{
				PlaneIndex = reader.ReadInt32();
				BackChildIndex = reader.ReadInt32();
				FrontChildIndex = reader.ReadInt32();
			}
		}

		private class DatRoomSide
		{
			public readonly int SideListStart;

			public readonly int SideListEnd;

			public DatRoomSide(BinaryReader reader)
			{
				reader.Skip(4);
				SideListStart = reader.ReadInt32();
				SideListEnd = reader.ReadInt32();
				reader.Skip(16);
			}
		}

		private class DatRoomAdjacency
		{
			public readonly int RoomIndex;

			public readonly int QuadIndex;

			public DatRoomAdjacency(BinaryReader reader)
			{
				RoomIndex = reader.ReadInt32();
				QuadIndex = reader.ReadInt32();
				reader.Skip(4);
			}
		}

		private InstanceDescriptor akev;

		private InstanceDescriptor agdb;

		private InstanceDescriptor pnta;

		private InstanceDescriptor plea;

		private InstanceDescriptor txca;

		private InstanceDescriptor agqg;

		private InstanceDescriptor agqc;

		private InstanceDescriptor agqr;

		private InstanceDescriptor txma;

		private InstanceDescriptor akva;

		private InstanceDescriptor akba;

		private InstanceDescriptor idxa1;

		private InstanceDescriptor idxa2;

		private InstanceDescriptor akbp;

		private InstanceDescriptor akaa;

		private PolygonMesh mesh;

		private Plane[] planes;

		private Polygon[] polygons;

		private bool getVanillaStairs;

		public static PolygonMesh Read(InstanceDescriptor akev)
		{
			return Read(akev, false);
		}

		public static PolygonMesh Read(InstanceDescriptor akev, bool getVanillaStairs)
		{
			AkiraDatReader akiraDatReader = new AkiraDatReader
			{
				akev = akev,
				getVanillaStairs = getVanillaStairs,
				mesh = new PolygonMesh(new MaterialLibrary())
			};
			akiraDatReader.Read();
			return akiraDatReader.mesh;
		}

		private void Read()
		{
			using (BinaryReader binaryReader = akev.OpenRead())
			{
				pnta = binaryReader.ReadInstance();
				plea = binaryReader.ReadInstance();
				txca = binaryReader.ReadInstance();
				agqg = binaryReader.ReadInstance();
				agqr = binaryReader.ReadInstance();
				agqc = binaryReader.ReadInstance();
				agdb = binaryReader.ReadInstance();
				txma = binaryReader.ReadInstance();
				akva = binaryReader.ReadInstance();
				akba = binaryReader.ReadInstance();
				idxa1 = binaryReader.ReadInstance();
				idxa2 = binaryReader.ReadInstance();
				akbp = binaryReader.ReadInstance();
				binaryReader.Skip(8);
				akaa = binaryReader.ReadInstance();
			}
			ReadGeometry();
			ReadDebugInfo();
			ReadMaterials();
			ReadScriptIndices();
			ReadRooms();
		}

		private void ReadGeometry()
		{
			using (BinaryReader binaryReader = pnta.OpenRead(52))
			{
				mesh.Points.AddRange(binaryReader.ReadVector3VarArray());
			}
			using (BinaryReader binaryReader2 = txca.OpenRead(20))
			{
				mesh.TexCoords.AddRange(binaryReader2.ReadVector2VarArray());
			}
			using (BinaryReader binaryReader3 = plea.OpenRead(20))
			{
				planes = binaryReader3.ReadPlaneVarArray();
			}
			int[] array;
			using (BinaryReader binaryReader4 = agqc.OpenRead(20))
			{
				array = new int[binaryReader4.ReadInt32()];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = binaryReader4.ReadInt32();
					binaryReader4.Skip(24);
				}
			}
			using (BinaryReader binaryReader5 = agqg.OpenRead(20))
			{
				polygons = new Polygon[binaryReader5.ReadInt32()];
				for (int j = 0; j < polygons.Length; j++)
				{
					int[] array2 = binaryReader5.ReadInt32Array(4);
					int[] array3 = binaryReader5.ReadInt32Array(4);
					Color[] array4 = binaryReader5.ReadColorArray(4);
					GunkFlags gunkFlags = (GunkFlags)binaryReader5.ReadInt32();
					int num = binaryReader5.ReadInt32();
					if ((gunkFlags & GunkFlags.Triangle) != GunkFlags.None)
					{
						Array.Resize(ref array2, 3);
						Array.Resize(ref array3, 3);
						Array.Resize(ref array4, 3);
						gunkFlags = (GunkFlags)((uint)gunkFlags & 0xFFFFFFBFu);
					}
					Polygon polygon = new Polygon(mesh, array2, PlaneFromIndex(array[j]))
					{
						Flags = (GunkFlags)((uint)gunkFlags & 0xFFFFFF7Fu),
						TexCoordIndices = array3,
						Colors = array4
					};
					if (num == -1)
					{
						polygon.ObjectType = -1;
						polygon.ObjectId = -1;
					}
					else
					{
						polygon.ObjectType = (num >> 24) & 0xFF;
						polygon.ObjectId = num & 0xFFFFFF;
					}
					polygons[j] = polygon;
				}
			}
			Polygon[] array5 = polygons;
			foreach (Polygon polygon2 in array5)
			{
				if ((polygon2.Flags & (GunkFlags.Ghost | GunkFlags.StairsUp | GunkFlags.StairsDown)) != GunkFlags.None)
				{
					mesh.Ghosts.Add(polygon2);
				}
				else
				{
					mesh.Polygons.Add(polygon2);
				}
			}
		}

		private Plane PlaneFromIndex(int index)
		{
			Plane result = planes[index & 0x7FFFFFFF];
			if (index < 0)
			{
				result.Normal = -result.Normal;
				result.D = 0f - result.D;
			}
			return result;
		}

		private void ReadMaterials()
		{
			Material[] array;
			using (BinaryReader binaryReader = txma.OpenRead(20))
			{
				array = new Material[binaryReader.ReadInt32()];
				for (int i = 0; i < array.Length; i++)
				{
					InstanceDescriptor instanceDescriptor = binaryReader.ReadInstance();
					if (instanceDescriptor != null)
					{
						Material material = mesh.Materials.GetMaterial(Utils.CleanupTextureName(instanceDescriptor.Name));
						material.Image = TextureDatReader.Read(instanceDescriptor).Surfaces[0];
						if (material.Image.HasAlpha)
						{
							material.Flags |= GunkFlags.Transparent;
						}
						array[i] = material;
					}
				}
			}
			using (BinaryReader binaryReader2 = agqr.OpenRead(20))
			{
				int num = binaryReader2.ReadInt32();
				for (int j = 0; j < num; j++)
				{
					polygons[j].Material = array[binaryReader2.ReadInt32() & 0xFFFF];
				}
			}
			StairRampClassifier stairRampClassifier = getVanillaStairs ? new StairRampClassifier(mesh) : null;
			Material[] array2 = new Material[polygons.Length];
			for (int k = 0; k < polygons.Length; k++)
			{
				Material marker = mesh.Materials.Markers.GetMarker(polygons[k]);
				if (marker == null && stairRampClassifier != null && stairRampClassifier.IsStairRamp(polygons[k]))
				{
					marker = mesh.Materials.Markers.Stairs;
				}
				array2[k] = marker;
			}
			for (int l = 0; l < polygons.Length; l++)
			{
				if (array2[l] != null)
				{
					polygons[l].Material = array2[l];
				}
			}
		}

		private void ReadScriptIndices()
		{
			if (idxa1 != null && idxa2 != null)
			{
				int[] array;
				using (BinaryReader binaryReader = idxa1.OpenRead(20))
				{
					array = binaryReader.ReadInt32VarArray();
				}
				int[] array2;
				using (BinaryReader binaryReader2 = idxa2.OpenRead(20))
				{
					array2 = binaryReader2.ReadInt32VarArray();
				}
				for (int i = 0; i < array.Length; i++)
				{
					polygons[array[i]].ScriptId = array2[i];
				}
			}
		}

		private void ReadDebugInfo()
		{
			if (agdb == null)
			{
				string path = "AGDB" + akev.Name + ".oni";
				string text = Path.Combine(Path.GetDirectoryName(akev.File.FilePath), path);
				if (!File.Exists(text))
				{
					return;
				}
				Console.WriteLine(text);
				InstanceFile instanceFile = akev.File.FileManager.OpenFile(text);
				if (instanceFile == null)
				{
					return;
				}
				agdb = instanceFile.Descriptors[0];
			}
			if (agdb == null || agdb.Template.Tag != TemplateTag.AGDB)
			{
				return;
			}
			using (BinaryReader binaryReader = agdb.OpenRead(20))
			{
				int num = binaryReader.ReadInt32();
				Dictionary<int, string> dictionary = new Dictionary<int, string>();
				Dictionary<int, string> dictionary2 = new Dictionary<int, string>();
				for (int i = 0; i < num; i++)
				{
					int num2 = binaryReader.ReadInt32();
					string value;
					if (!dictionary2.TryGetValue(num2, out value))
					{
						using (BinaryReader binaryReader2 = agdb.GetRawReader(num2))
						{
							value = binaryReader2.ReadString(256);
						}
						value = value.Replace('.', '_');
						dictionary2.Add(num2, value);
					}
					int num3 = binaryReader.ReadInt32();
					string value2;
					if (!dictionary.TryGetValue(num3, out value2))
					{
						using (BinaryReader binaryReader3 = agdb.GetRawReader(num3))
						{
							value2 = binaryReader3.ReadString(256);
						}
						value2 = Path.GetFileNameWithoutExtension(value2);
						dictionary.Add(num3, value2);
					}
					if (!string.IsNullOrEmpty(value))
					{
						mesh.HasDebugInfo = true;
					}
					polygons[i].ObjectName = value;
					polygons[i].FileName = value2;
				}
			}
		}

		private void ReadRooms()
		{
			DatRoomBspNode[] array;
			using (BinaryReader binaryReader = akbp.OpenRead(22))
			{
				array = new DatRoomBspNode[binaryReader.ReadUInt16()];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = new DatRoomBspNode(binaryReader);
				}
			}
			DatRoomSide[] array2;
			using (BinaryReader binaryReader2 = akba.OpenRead(20))
			{
				array2 = new DatRoomSide[binaryReader2.ReadInt32()];
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j] = new DatRoomSide(binaryReader2);
				}
			}
			DatRoomAdjacency[] array3;
			using (BinaryReader binaryReader3 = akaa.OpenRead(20))
			{
				array3 = new DatRoomAdjacency[binaryReader3.ReadInt32()];
				for (int k = 0; k < array3.Length; k++)
				{
					array3[k] = new DatRoomAdjacency(binaryReader3);
				}
			}
			DatRoom[] array4;
			using (BinaryReader binaryReader4 = akva.OpenRead(20))
			{
				array4 = new DatRoom[binaryReader4.ReadInt32()];
				for (int l = 0; l < array4.Length; l++)
				{
					array4[l] = new DatRoom(akva, binaryReader4);
				}
			}
			Room[] array5 = new Room[array4.Length];
			for (int m = 0; m < array4.Length; m++)
			{
				DatRoom datRoom = array4[m];
				Room room = new Room
				{
					BspTree = BspNodeDataToBspNode(array, datRoom.BspRootIndex),
					BoundingBox = datRoom.BoundingBox
				};
				if ((datRoom.Flags & RoomFlags.Stairs) != RoomFlags.None)
				{
					room.FloorPlane = datRoom.Floor;
					room.Height = datRoom.Height;
				}
				else
				{
					room.FloorPlane = new Plane(Vector3.Up, 0f - datRoom.BoundingBox.Min.Y);
					room.Height = datRoom.BoundingBox.Max.Y - datRoom.BoundingBox.Min.Y;
				}
				room.Grid = RoomGrid.FromCompressedData(datRoom.XTiles, datRoom.ZTiles, datRoom.CompressedGridData);
				array5[m] = room;
			}
			for (int n = 0; n < array4.Length; n++)
			{
				DatRoom datRoom2 = array4[n];
				Room room2 = array5[n];
				for (int num = datRoom2.SideListStart; num < datRoom2.SideListEnd; num++)
				{
					DatRoomSide datRoomSide = array2[num];
					for (int num2 = datRoomSide.SideListStart; num2 < datRoomSide.SideListEnd; num2++)
					{
						DatRoomAdjacency datRoomAdjacency = array3[num2];
						Room room3 = array5[datRoomAdjacency.RoomIndex];
						Polygon ghost = polygons[datRoomAdjacency.QuadIndex];
						room2.Ajacencies.Add(new RoomAdjacency(room3, ghost));
					}
				}
			}
			mesh.Rooms.AddRange(array5);
		}

		private RoomBspNode BspNodeDataToBspNode(DatRoomBspNode[] data, int index)
		{
			DatRoomBspNode datRoomBspNode = data[index];
			RoomBspNode frontChild = null;
			RoomBspNode backChild = null;
			if (datRoomBspNode.BackChildIndex != -1)
			{
				backChild = BspNodeDataToBspNode(data, datRoomBspNode.BackChildIndex);
			}
			if (datRoomBspNode.FrontChildIndex != -1)
			{
				frontChild = BspNodeDataToBspNode(data, datRoomBspNode.FrontChildIndex);
			}
			return new RoomBspNode(PlaneFromIndex(datRoomBspNode.PlaneIndex), backChild, frontChild);
		}
	}
}
