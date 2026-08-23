using System;
using System.Collections;
using System.Collections.Generic;
using Oni.Imaging;

namespace Oni.Akira
{
	internal class AkiraDatWriter
	{
		private class UniqueList<T> : ICollection<T>, IEnumerable<T>, IEnumerable
		{
			private readonly List<T> list = new List<T>();

			private readonly Dictionary<T, int> indices = new Dictionary<T, int>();

			public int Count
			{
				get
				{
					return list.Count;
				}
			}

			bool ICollection<T>.IsReadOnly
			{
				get
				{
					return false;
				}
			}

			public int Add(T t)
			{
				int value;
				if (!indices.TryGetValue(t, out value))
				{
					value = list.Count;
					indices.Add(t, value);
					list.Add(t);
				}
				return value;
			}

			void ICollection<T>.Add(T item)
			{
				Add(item);
			}

			void ICollection<T>.Clear()
			{
				list.Clear();
				indices.Clear();
			}

			bool ICollection<T>.Contains(T item)
			{
				return indices.ContainsKey(item);
			}

			void ICollection<T>.CopyTo(T[] array, int arrayIndex)
			{
				list.CopyTo(array, arrayIndex);
			}

			bool ICollection<T>.Remove(T item)
			{
				throw new NotImplementedException();
			}

			IEnumerator<T> IEnumerable<T>.GetEnumerator()
			{
				return list.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return list.GetEnumerator();
			}
		}

		private class DatObject<T>
		{
			private readonly T source;

			public T Source
			{
				get
				{
					return source;
				}
			}

			public DatObject(T source)
			{
				this.source = source;
			}
		}

		private class DatPolygon : DatObject<Polygon>
		{
			private static readonly Color defaultColor = new Color(207, 207, 207, byte.MaxValue);

			private static readonly Color[] defaultColors = new Color[4] { defaultColor, defaultColor, defaultColor, defaultColor };

			private GunkFlags flags;

			private readonly int index;

			public readonly int[] PointIndices = new int[4];

			public readonly int[] TexCoordIndices = new int[4];

			public readonly Color[] Colors = new Color[4];

			private readonly int objectId;

			private readonly int scriptId;

			private int materialIndex;

			private int planeIndex;

			public int Index
			{
				get
				{
					return index;
				}
			}

			public int ObjectId
			{
				get
				{
					return objectId;
				}
			}

			public int ScriptId
			{
				get
				{
					return scriptId;
				}
			}

			public GunkFlags Flags
			{
				get
				{
					return flags;
				}
				set
				{
					flags = value;
				}
			}

			public int MaterialIndex
			{
				get
				{
					return materialIndex;
				}
				set
				{
					materialIndex = value;
				}
			}

			public int PlaneIndex
			{
				get
				{
					return planeIndex;
				}
				set
				{
					planeIndex = value;
				}
			}

			public DatPolygon(Polygon source, int index, UniqueList<Vector3> points, UniqueList<Vector2> texCoords)
				: base(source)
			{
				this.index = index;
				scriptId = source.ScriptId;
				objectId = (source.ObjectType << 24) | (source.ObjectId & 0xFFFFFF);
				flags = source.Flags;
				if (source.VertexCount == 3)
				{
					flags |= GunkFlags.Triangle;
					Array.Copy(source.PointIndices, PointIndices, 3);
					PointIndices[3] = PointIndices[2];
					Array.Copy(source.TexCoordIndices, TexCoordIndices, 3);
					TexCoordIndices[3] = TexCoordIndices[2];
					if (source.Colors == null)
					{
						Colors = defaultColors;
					}
					else
					{
						Array.Copy(source.Colors, Colors, 3);
						Colors[3] = Colors[2];
					}
				}
				else
				{
					Array.Copy(source.PointIndices, PointIndices, 4);
					Array.Copy(source.TexCoordIndices, TexCoordIndices, 4);
					if (source.Colors != null)
					{
						Colors = source.Colors;
					}
					else
					{
						Colors = defaultColors;
					}
				}
				for (int i = 0; i < 4; i++)
				{
					PointIndices[i] = points.Add(source.Mesh.Points[PointIndices[i]]);
					TexCoordIndices[i] = texCoords.Add(source.Mesh.TexCoords[TexCoordIndices[i]]);
				}
			}
		}

		private class DatBspNode<T> : DatObject<T> where T : BspNode<T>
		{
			public int PlaneIndex;

			public int FrontChildIndex = -1;

			public int BackChildIndex = -1;

			public DatBspNode(T source)
				: base(source)
			{
			}
		}

		private class DatAlphaBspNode : DatBspNode<AlphaBspNode>
		{
			public readonly int PolygonIndex;

			public DatAlphaBspNode(AlphaBspNode source, int polygonIndex)
				: base(source)
			{
				PolygonIndex = polygonIndex;
			}
		}

		private class DatRoom : DatObject<Room>
		{
			private readonly int index;

			private readonly RoomFlags flags;

			public int BspTreeIndex;

			public int SideListStart;

			public int SideListEnd;

			public int ChildIndex = -1;

			public int SiblingIndex = -1;

			public byte[] CompressedGridData;

			public byte[] DebugData;

			public int Index
			{
				get
				{
					return index;
				}
			}

			public int Flags
			{
				get
				{
					return (int)flags;
				}
			}

			public DatRoom(Room source, int index)
				: base(source)
			{
				this.index = index;
				flags = RoomFlags.Room;
				if (source.FloorPlane.Normal.Y < 0.999f)
				{
					flags |= RoomFlags.Stairs;
				}
			}
		}

		private class DatRoomBspNode : DatBspNode<RoomBspNode>
		{
			public DatRoomBspNode(RoomBspNode source)
				: base(source)
			{
			}
		}

		private class DatRoomSide
		{
			public int AdjacencyListStart;

			public int AdjacencyListEnd;
		}

		private class DatRoomAdjacency : DatObject<RoomAdjacency>
		{
			public int AdjacentRoomIndex;

			public int GhostIndex;

			public DatRoomAdjacency(RoomAdjacency source)
				: base(source)
			{
			}
		}

		private class DatOctree
		{
			public int[] Nodes;

			public int[] QuadTrees;

			public int[] Adjacency;

			public DatOctreeBoundingBox[] BoundingBoxes;

			public DatOctreePolygonRange[] PolygonLists;

			public DatOctreeBnvRange[] BnvLists;

			public int[] PolygonIndex;

			public int[] BnvIndex;
		}

		private struct DatOctreeBoundingBox
		{
			private readonly uint value;

			public uint PackedValue
			{
				get
				{
					return value;
				}
			}

			public DatOctreeBoundingBox(BoundingBox bbox)
			{
				int num = (int)Math.Log(bbox.Max.X - bbox.Min.X, 2.0) - 4;
				int num2 = (int)(bbox.Max.X + 4080f);
				int num3 = (int)(bbox.Max.Y + 4080f);
				int num4 = (int)(bbox.Max.Z + 4080f);
				value = (uint)((num2 << 14) | (num3 << 5) | (num4 >> 4) | (num << 27));
			}
		}

		private struct DatOctreeBnvRange
		{
			private const int indexBitOffset = 8;

			private const int lengthBitMask = 255;

			private readonly uint value;

			public uint PackedValue
			{
				get
				{
					return value;
				}
			}

			public DatOctreeBnvRange(int start, int length)
			{
				ValidateRange(start, length);
				value = (uint)((start << 8) | (length & 0xFF));
			}

			private static void ValidateRange(int start, int length)
			{
				if (start > 16777215)
				{
					throw new ArgumentException(string.Format("Invalid bnv list start index {0}", start), "start");
				}
				if (length > 255)
				{
					throw new ArgumentException(string.Format("Invalid bnv list length {0}", length), "length");
				}
			}
		}

		private struct DatOctreePolygonRange
		{
			private const int indexBitOffset = 12;

			private const int lengthBitMask = 4095;

			private readonly uint value;

			public uint PackedValue
			{
				get
				{
					return value;
				}
			}

			public DatOctreePolygonRange(int start, int length)
			{
				if (start > 1048575)
				{
					start = 1048575;
					length = 0;
				}
				value = (uint)((start << 12) | (length & 0xFFF));
			}

			private static void ValidateRange(int start, int length)
			{
				if (start > 1048575)
				{
					throw new ArgumentException(string.Format("Invalid quad list start index {0}", start), "start");
				}
				if (length > 4095)
				{
					throw new ArgumentException(string.Format("Invalid quad list length {0}", length), "length");
				}
			}
		}

		private Importer importer;

		private string name;

		private bool debug;

		private PolygonMesh source;

		private List<DatPolygon> polygons = new List<DatPolygon>();

		private Dictionary<Polygon, DatPolygon> polygonMap = new Dictionary<Polygon, DatPolygon>();

		private UniqueList<Vector3> points = new UniqueList<Vector3>();

		private UniqueList<Vector2> texCoords = new UniqueList<Vector2>();

		private UniqueList<Material> materials = new UniqueList<Material>();

		private UniqueList<Plane> planes = new UniqueList<Plane>();

		private List<DatAlphaBspNode> alphaBspNodes = new List<DatAlphaBspNode>();

		private List<DatRoom> rooms = new List<DatRoom>();

		private Dictionary<Room, DatRoom> roomMap = new Dictionary<Room, DatRoom>();

		private List<DatRoomBspNode> roomBspNodes = new List<DatRoomBspNode>();

		private List<DatRoomSide> roomSides = new List<DatRoomSide>();

		private List<DatRoomAdjacency> roomAdjacencies = new List<DatRoomAdjacency>();

		private DatOctree octree;

		public static void Write(PolygonMesh mesh, Importer importer, string name, bool debug)
		{
			AkiraDatWriter akiraDatWriter = new AkiraDatWriter
			{
				name = name,
				importer = importer,
				source = mesh,
				debug = debug
			};
			akiraDatWriter.Write();
		}

		private void Write()
		{
			Console.Error.WriteLine("Environment bounding box is {0}", source.GetBoundingBox());
			RoomBuilder.BuildRooms(source);
			ConvertPolygons(source.Polygons);
			ConvertPolygons(source.Doors);
			ConvertPolygons(source.Ghosts);
			ConvertAlphaBspTree(AlphaBspBuilder.Build(source, debug));
			ConvertRooms();
			ConvertOctree();
			WriteAKEV();
		}

		private void ConvertPolygons(List<Polygon> sourcePolygons)
		{
			foreach (Polygon sourcePolygon in sourcePolygons)
			{
				if (sourcePolygon.VertexCount > 4)
				{
					Console.Error.WriteLine("Geometry '{0}' has a {1}-gon, ignoring.", sourcePolygon.ObjectName, sourcePolygon.VertexCount);
					continue;
				}
				if (sourcePolygon.TexCoordIndices == null)
				{
					Console.Error.WriteLine("Geometry '{0}' does not contain texture coordinates, ignoring.", sourcePolygon.ObjectName);
					continue;
				}
				DatPolygon datPolygon = new DatPolygon(sourcePolygon, polygons.Count, points, texCoords)
				{
					PlaneIndex = planes.Add(sourcePolygon.Plane),
					MaterialIndex = materials.Add(sourcePolygon.Material)
				};
				polygons.Add(datPolygon);
				polygonMap.Add(sourcePolygon, datPolygon);
			}
		}

		private int ConvertAlphaBspTree(AlphaBspNode source)
		{
			if (source == null)
			{
				return -1;
			}
			int count = alphaBspNodes.Count;
			DatAlphaBspNode datAlphaBspNode = new DatAlphaBspNode(source, polygonMap[source.Polygon].Index)
			{
				PlaneIndex = planes.Add(source.Plane)
			};
			alphaBspNodes.Add(datAlphaBspNode);
			if (source.FrontChild != null)
			{
				datAlphaBspNode.FrontChildIndex = ConvertAlphaBspTree(source.FrontChild);
			}
			if (source.BackChild != null)
			{
				datAlphaBspNode.BackChildIndex = ConvertAlphaBspTree(source.BackChild);
			}
			return count;
		}

		private void ConvertRooms()
		{
			foreach (Room room in source.Rooms)
			{
				DatRoom datRoom = new DatRoom(room, rooms.Count)
				{
					BspTreeIndex = ConvertRoomBspTree(room.BspTree, new Dictionary<RoomBspNode, int>()),
					CompressedGridData = room.Grid.Compress(),
					DebugData = room.Grid.DebugData,
					SideListStart = roomSides.Count
				};
				if (room.Ajacencies.Count > 0)
				{
					DatRoomSide datRoomSide = new DatRoomSide
					{
						AdjacencyListStart = roomAdjacencies.Count
					};
					foreach (RoomAdjacency ajacency in room.Ajacencies)
					{
						roomAdjacencies.Add(new DatRoomAdjacency(ajacency)
						{
							AdjacentRoomIndex = source.Rooms.IndexOf(ajacency.AdjacentRoom),
							GhostIndex = polygonMap[ajacency.Ghost].Index
						});
					}
					datRoomSide.AdjacencyListEnd = roomAdjacencies.Count;
					roomSides.Add(datRoomSide);
				}
				datRoom.SideListEnd = roomSides.Count;
				rooms.Add(datRoom);
				roomMap.Add(room, datRoom);
			}
		}

		private int ConvertRoomBspTree(RoomBspNode node, Dictionary<RoomBspNode, int> nodeMap)
		{
			int count;
			if (nodeMap.TryGetValue(node, out count))
			{
				return count;
			}
			count = roomBspNodes.Count;
			DatRoomBspNode datRoomBspNode = new DatRoomBspNode(node)
			{
				PlaneIndex = planes.Add(node.Plane)
			};
			roomBspNodes.Add(datRoomBspNode);
			nodeMap.Add(node, count);
			if (node.FrontChild != null)
			{
				datRoomBspNode.FrontChildIndex = ConvertRoomBspTree(node.FrontChild, nodeMap);
			}
			if (node.BackChild != null)
			{
				datRoomBspNode.BackChildIndex = ConvertRoomBspTree(node.BackChild, nodeMap);
			}
			return count;
		}

		private void ConvertOctree()
		{
			Console.Error.WriteLine("Building octtree for {0} polygons...", source.Polygons.Count);
			OctreeNode octreeNode = OctreeBuilder.Build(source, debug);
			List<OctreeNode> nodeList = new List<OctreeNode>();
			List<OctreeNode> leafList = new List<OctreeNode>();
			int quadListLength = 0;
			int roomListLength = 0;
			octreeNode.DfsTraversal(delegate(OctreeNode node)
			{
				if (node.IsLeaf)
				{
					node.Index = leafList.Count;
					leafList.Add(node);
					quadListLength += node.Polygons.Count;
					roomListLength += node.Rooms.Count;
				}
				else
				{
					node.Index = nodeList.Count;
					nodeList.Add(node);
				}
			});
			octree = new DatOctree
			{
				Nodes = new int[nodeList.Count * 8],
				Adjacency = new int[leafList.Count * 6],
				BoundingBoxes = new DatOctreeBoundingBox[leafList.Count],
				PolygonIndex = new int[quadListLength],
				PolygonLists = new DatOctreePolygonRange[leafList.Count],
				BnvLists = new DatOctreeBnvRange[leafList.Count],
				BnvIndex = new int[roomListLength]
			};
			Console.WriteLine("Octtree: {0} interior nodes, {1} leafs", nodeList.Count, leafList.Count);
			int[] array = new int[8] { 0, 4, 2, 6, 1, 5, 3, 7 };
			DatOctree datOctree = octree;
			foreach (OctreeNode item in nodeList)
			{
				int num = item.Index * 8;
				for (int num2 = 0; num2 < 8; num2++)
				{
					OctreeNode octreeNode2 = item.Children[num2];
					int num3 = array[num2];
					if (octreeNode2.IsLeaf)
					{
						datOctree.Nodes[num + num3] = octreeNode2.Index | int.MinValue;
					}
					else
					{
						datOctree.Nodes[num + num3] = octreeNode2.Index;
					}
				}
			}
			int start = 0;
			int start2 = 0;
			foreach (OctreeNode item2 in leafList)
			{
				datOctree.BoundingBoxes[item2.Index] = new DatOctreeBoundingBox(item2.BoundingBox);
				if (item2.Polygons.Count > 0)
				{
					datOctree.PolygonLists[item2.Index] = new DatOctreePolygonRange(start, item2.Polygons.Count);
					foreach (Polygon polygon in item2.Polygons)
					{
						datOctree.PolygonIndex[start++] = polygonMap[polygon].Index;
					}
				}
				if (item2.Rooms.Count <= 0)
				{
					continue;
				}
				datOctree.BnvLists[item2.Index] = new DatOctreeBnvRange(start2, item2.Rooms.Count);
				foreach (Room room in item2.Rooms)
				{
					datOctree.BnvIndex[start2++] = roomMap[room].Index;
				}
			}
			List<int> list = new List<int>();
			foreach (OctreeNode item3 in leafList)
			{
				item3.RefineAdjacency();
				foreach (OctreeNode.Face item4 in OctreeNode.Face.All)
				{
					OctreeNode octreeNode3 = item3.Adjacency[item4.Index];
					int num4 = item3.Index * 6 + item4.Index;
					if (octreeNode3 == null)
					{
						datOctree.Adjacency[num4] = -1;
						continue;
					}
					if (octreeNode3.IsLeaf)
					{
						datOctree.Adjacency[num4] = octreeNode3.Index | int.MinValue;
						continue;
					}
					int num5 = list.Count / 4;
					datOctree.Adjacency[num4] = num5;
					QuadtreeNode quadtreeNode = item3.BuildFaceQuadTree(item4);
					foreach (QuadtreeNode dfs in quadtreeNode.GetDfsList())
					{
						for (int num6 = 0; num6 < 4; num6++)
						{
							if (dfs.Nodes[num6] != null)
							{
								list.Add(num5 + dfs.Nodes[num6].Index);
							}
							else
							{
								list.Add(dfs.Leafs[num6].Index | int.MinValue);
							}
						}
					}
				}
			}
			datOctree.QuadTrees = list.ToArray();
		}

		private void WriteAKEV()
		{
			ImporterDescriptor importerDescriptor = importer.CreateInstance(TemplateTag.AKEV, name);
			ImporterDescriptor descriptor = importer.CreateInstance(TemplateTag.PNTA);
			ImporterDescriptor descriptor2 = importer.CreateInstance(TemplateTag.PLEA);
			ImporterDescriptor descriptor3 = importer.CreateInstance(TemplateTag.TXCA);
			ImporterDescriptor descriptor4 = importer.CreateInstance(TemplateTag.AGQG);
			ImporterDescriptor descriptor5 = importer.CreateInstance(TemplateTag.AGQR);
			ImporterDescriptor descriptor6 = importer.CreateInstance(TemplateTag.AGQC);
			ImporterDescriptor descriptor7 = importer.CreateInstance(TemplateTag.AGDB);
			ImporterDescriptor descriptor8 = importer.CreateInstance(TemplateTag.TXMA);
			ImporterDescriptor descriptor9 = importer.CreateInstance(TemplateTag.AKVA);
			ImporterDescriptor descriptor10 = importer.CreateInstance(TemplateTag.AKBA);
			ImporterDescriptor importerDescriptor2 = importer.CreateInstance(TemplateTag.IDXA);
			ImporterDescriptor importerDescriptor3 = importer.CreateInstance(TemplateTag.IDXA);
			ImporterDescriptor descriptor11 = importer.CreateInstance(TemplateTag.AKBP);
			ImporterDescriptor descriptor12 = importer.CreateInstance(TemplateTag.ABNA);
			ImporterDescriptor importerDescriptor4 = importer.CreateInstance(TemplateTag.AKOT);
			ImporterDescriptor descriptor13 = importer.CreateInstance(TemplateTag.AKAA);
			ImporterDescriptor descriptor14 = importer.CreateInstance(TemplateTag.AKDA);
			using (BinaryWriter binaryWriter = importerDescriptor.OpenWrite())
			{
				binaryWriter.Write(descriptor);
				binaryWriter.Write(descriptor2);
				binaryWriter.Write(descriptor3);
				binaryWriter.Write(descriptor4);
				binaryWriter.Write(descriptor5);
				binaryWriter.Write(descriptor6);
				binaryWriter.Write(descriptor7);
				binaryWriter.Write(descriptor8);
				binaryWriter.Write(descriptor9);
				binaryWriter.Write(descriptor10);
				binaryWriter.Write(importerDescriptor2);
				binaryWriter.Write(importerDescriptor3);
				binaryWriter.Write(descriptor11);
				binaryWriter.Write(descriptor12);
				binaryWriter.Write(importerDescriptor4);
				binaryWriter.Write(descriptor13);
				binaryWriter.Write(descriptor14);
				binaryWriter.Write(source.GetBoundingBox());
				binaryWriter.Skip(24);
				binaryWriter.Write(12f);
			}
			descriptor.WritePoints(points);
			descriptor2.WritePlanes(planes);
			descriptor3.WriteTexCoords(texCoords);
			WriteAGQG(descriptor4);
			WriteAGQR(descriptor5);
			WriteAGQC(descriptor6);
			WriteTXMA(descriptor8);
			WriteAKVA(descriptor9);
			WriteAKBA(descriptor10);
			WriteAKBP(descriptor11);
			WriteABNA(descriptor12);
			WriteAKOT(importerDescriptor4);
			WriteAKAA(descriptor13);
			WriteAKDA(descriptor14);
			WriteScriptIds(importerDescriptor2, importerDescriptor3);
			WriteAGDB(descriptor7);
		}

		private void WriteAGQG(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(polygons.Count);
				foreach (DatPolygon polygon in polygons)
				{
					binaryWriter.Write(polygon.PointIndices);
					binaryWriter.Write(polygon.TexCoordIndices);
					binaryWriter.Write(polygon.Colors);
					binaryWriter.Write((uint)polygon.Flags);
					binaryWriter.Write(polygon.ObjectId);
				}
			}
		}

		private void WriteAGQC(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(polygons.Count);
				foreach (DatPolygon polygon in polygons)
				{
					binaryWriter.Write(polygon.PlaneIndex);
					binaryWriter.Write(polygon.Source.BoundingBox);
				}
			}
		}

		private void WriteAGQR(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(polygons.Count);
				foreach (DatPolygon polygon in polygons)
				{
					binaryWriter.Write(polygon.MaterialIndex);
				}
			}
		}

		private void WriteTXMA(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(materials.Count);
				foreach (Material item in (IEnumerable<Material>)materials)
				{
					binaryWriter.Write(importer.CreateInstance(TemplateTag.TXMP, item.Name));
				}
			}
		}

		private void WriteABNA(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(alphaBspNodes.Count);
				foreach (DatAlphaBspNode alphaBspNode in alphaBspNodes)
				{
					binaryWriter.Write(alphaBspNode.PolygonIndex);
					binaryWriter.Write(alphaBspNode.PlaneIndex);
					binaryWriter.Write(alphaBspNode.FrontChildIndex);
					binaryWriter.Write(alphaBspNode.BackChildIndex);
				}
			}
		}

		private void WriteAKVA(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(rooms.Count);
				foreach (DatRoom room in rooms)
				{
					binaryWriter.Write(room.BspTreeIndex);
					binaryWriter.Write(room.Index);
					binaryWriter.Write(room.SideListStart);
					binaryWriter.Write(room.SideListEnd);
					binaryWriter.Write(room.ChildIndex);
					binaryWriter.Write(room.SiblingIndex);
					binaryWriter.Skip(4);
					binaryWriter.Write(room.Source.Grid.XTiles);
					binaryWriter.Write(room.Source.Grid.ZTiles);
					binaryWriter.Write(importer.WriteRawPart(room.CompressedGridData));
					binaryWriter.Write(room.CompressedGridData.Length);
					binaryWriter.Write(room.Source.Grid.TileSize);
					binaryWriter.Write(room.Source.BoundingBox);
					binaryWriter.WriteInt16(room.Source.Grid.XOrigin);
					binaryWriter.WriteInt16(room.Source.Grid.ZOrigin);
					binaryWriter.Write(room.Index);
					binaryWriter.Skip(4);
					if (room.DebugData != null)
					{
						binaryWriter.Write(room.DebugData.Length);
						binaryWriter.Write(importer.WriteRawPart(room.DebugData));
					}
					else
					{
						binaryWriter.Write(0);
						binaryWriter.Write(0);
					}
					binaryWriter.Write(room.Flags);
					binaryWriter.Write(room.Source.FloorPlane);
					binaryWriter.Write(room.Source.Height);
				}
			}
		}

		private void WriteAKBA(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(roomSides.Count);
				foreach (DatRoomSide roomSide in roomSides)
				{
					binaryWriter.Write(0);
					binaryWriter.Write(roomSide.AdjacencyListStart);
					binaryWriter.Write(roomSide.AdjacencyListEnd);
					binaryWriter.Skip(16);
				}
			}
		}

		private void WriteAKBP(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(22))
			{
				binaryWriter.WriteUInt16(roomBspNodes.Count);
				foreach (DatRoomBspNode roomBspNode in roomBspNodes)
				{
					binaryWriter.Write(roomBspNode.PlaneIndex);
					binaryWriter.Write(roomBspNode.BackChildIndex);
					binaryWriter.Write(roomBspNode.FrontChildIndex);
				}
			}
		}

		private void WriteAKAA(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(roomAdjacencies.Count);
				foreach (DatRoomAdjacency roomAdjacency in roomAdjacencies)
				{
					binaryWriter.Write(roomAdjacency.AdjacentRoomIndex);
					binaryWriter.Write(roomAdjacency.GhostIndex);
					binaryWriter.Write(0);
				}
			}
		}

		private void WriteAKDA(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(0);
			}
		}

		private void WriteAKOT(ImporterDescriptor akot)
		{
			ImporterDescriptor descriptor = importer.CreateInstance(TemplateTag.OTIT);
			ImporterDescriptor descriptor2 = importer.CreateInstance(TemplateTag.OTLF);
			ImporterDescriptor descriptor3 = importer.CreateInstance(TemplateTag.QTNA);
			ImporterDescriptor descriptor4 = importer.CreateInstance(TemplateTag.IDXA);
			ImporterDescriptor descriptor5 = importer.CreateInstance(TemplateTag.IDXA);
			using (BinaryWriter binaryWriter = akot.OpenWrite())
			{
				binaryWriter.Write(descriptor);
				binaryWriter.Write(descriptor2);
				binaryWriter.Write(descriptor3);
				binaryWriter.Write(descriptor4);
				binaryWriter.Write(descriptor5);
			}
			WriteOTIT(descriptor);
			WriteOTLF(descriptor2);
			WriteQTNA(descriptor3);
			descriptor4.WriteIndices(octree.PolygonIndex);
			descriptor5.WriteIndices(octree.BnvIndex);
		}

		private void WriteOTIT(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(octree.Nodes.Length / 8);
				binaryWriter.Write(octree.Nodes);
			}
		}

		private void WriteOTLF(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(octree.BoundingBoxes.Length);
				for (int i = 0; i < octree.BoundingBoxes.Length; i++)
				{
					binaryWriter.Write(octree.PolygonLists[i].PackedValue);
					binaryWriter.Write(octree.Adjacency, i * 6, 6);
					binaryWriter.Write(octree.BoundingBoxes[i].PackedValue);
					binaryWriter.Write(octree.BnvLists[i].PackedValue);
				}
			}
		}

		private void WriteQTNA(ImporterDescriptor descriptor)
		{
			using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
			{
				binaryWriter.Write(octree.QuadTrees.Length / 4);
				binaryWriter.Write(octree.QuadTrees);
			}
		}

		private void WriteScriptIds(ImporterDescriptor idxa1, ImporterDescriptor idxa2)
		{
			List<KeyValuePair<int, int>> list = new List<KeyValuePair<int, int>>(256);
			foreach (DatPolygon polygon in polygons)
			{
				if (polygon.ScriptId != 0)
				{
					list.Add(new KeyValuePair<int, int>(polygon.ScriptId, polygon.Index));
				}
			}
			list.Sort((KeyValuePair<int, int> x, KeyValuePair<int, int> y) => x.Key.CompareTo(y.Key));
			int[] array = new int[list.Count];
			int[] array2 = new int[list.Count];
			for (int num = 0; num < list.Count; num++)
			{
				array[num] = list[num].Key;
				array2[num] = list[num].Value;
			}
			idxa1.WriteIndices(array2);
			idxa2.WriteIndices(array);
		}

		private void WriteAGDB(ImporterDescriptor descriptor)
		{
			if (!debug)
			{
				using (BinaryWriter binaryWriter = descriptor.OpenWrite(20))
				{
					binaryWriter.Write(0);
					return;
				}
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>(polygons.Count, StringComparer.Ordinal);
			Dictionary<string, int> dictionary2 = new Dictionary<string, int>(polygons.Count, StringComparer.Ordinal);
			using (BinaryWriter binaryWriter2 = descriptor.OpenWrite(20))
			{
				binaryWriter2.Write(polygons.Count);
				foreach (DatPolygon polygon in polygons)
				{
					string text = polygon.Source.ObjectName;
					string text2 = polygon.Source.FileName;
					if (string.IsNullOrEmpty(text))
					{
						text = "(none)";
					}
					if (string.IsNullOrEmpty(text2))
					{
						text2 = "(none)";
					}
					int value;
					if (!dictionary.TryGetValue(text, out value))
					{
						value = importer.WriteRawPart(text);
						dictionary.Add(text, value);
					}
					int value2;
					if (!dictionary2.TryGetValue(text2, out value2))
					{
						value2 = importer.WriteRawPart(text2);
						dictionary2.Add(text2, value2);
					}
					binaryWriter2.Write(value);
					binaryWriter2.Write(value2);
				}
			}
		}
	}
}
