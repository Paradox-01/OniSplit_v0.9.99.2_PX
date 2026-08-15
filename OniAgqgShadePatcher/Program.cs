using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

internal static class Program
{
    private const int Version31 = 1448227633;
    private const int Version32 = 1448227634;
    private const int AgqgTag = 1095192903;
    private const int PntaTag = 1347310657;
    private const int DescriptorSize = 20;
    private const int AgqgHeaderSize = 24;
    private const int AgqgRecordSize = 56;
    private const int PointIndicesOffset = 0;
    private const int ColorOffset = 32;
    private const int ObjectIdOffset = 52;

    private static readonly Dictionary<string, int> ObjectTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        { "world", 0 }, { "char", 1 }, { "patr", 2 }, { "door", 3 }, { "flag", 4 }, { "furn", 5 },
        { "type06", 6 }, { "type07", 7 }, { "part", 8 }, { "pwru", 9 }, { "sndg", 10 }, { "trgv", 11 },
        { "weap", 12 }, { "trig", 13 }, { "turr", 14 }, { "cons", 15 }, { "cmbt", 16 }, { "mele", 17 }, { "neut", 18 }
    };

    private static readonly string[] ObjectTypeNames = new string[]
    {
        "world", "char", "patr", "door", "flag", "furn", "type06", "type07", "part", "pwru",
        "sndg", "trgv", "weap", "trig", "turr", "cons", "cmbt", "mele", "neut"
    };

    private sealed class Descriptor
    {
        public int Tag;
        public int DataOffset;
        public int DataSize;
    }

    private sealed class ObjVertex
    {
        public float X;
        public float Y;
        public float Z;
        public byte R;
        public byte G;
        public byte B;
        public byte A;
        public float U;
        public float V;
        public float W;
    }

    private sealed class ObjTexCoord
    {
        public float U;
        public float V;
        public float W;
    }

    private sealed class ObjCorner
    {
        public int PositionIndex;
        public int TexCoordIndex;
    }

    private sealed class ObjFace
    {
        public int CornerCount;
        public bool HasNormal;
        public double NormalX;
        public double NormalY;
        public double NormalZ;
        public readonly List<ObjCorner> Corners = new List<ObjCorner>();
        public readonly List<int> VertexIndices = new List<int>();
    }

    private sealed class ObjMesh
    {
        public int ObjectId;
        public int SourceVertexCount;
        public readonly List<ObjVertex> Vertices = new List<ObjVertex>();
        public readonly List<ObjFace> Faces = new List<ObjFace>();
    }

    private static int Main(string[] args)
    {
        if (args.Length < 2 || args.Length > 7)
        {
            Console.Error.WriteLine("Usage: OniAgqgShadePatcher.exe <akev-file.oni> <obj-directory> [-obj(type)|-obj(type)(id)|-obj(type)(idStart,idEnd)] [-average] [-reidentify] [-tolerateDiff:value] [-color(R,G,B,A)]");
            return 2;
        }

        int objectType = -1;
        int objectIdStart = -1;
        int objectIdEnd = -1;
        bool average = false;
        bool reidentify = false;
        bool hasTolerateDiff = false;
        float tolerateDiff = 0.0f;
        bool hasColor = false;
        byte colorR = 0;
        byte colorG = 0;
        byte colorB = 0;
        byte colorA = 0;
        for (int argumentIndex = 2; argumentIndex < args.Length; argumentIndex++)
        {
            if (args[argumentIndex].StartsWith("-obj(", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseObjectArgument(args[argumentIndex], out objectType, out objectIdStart, out objectIdEnd))
                {
                    Console.Error.WriteLine("-obj must use a supported type: -obj(type), -obj(type)(id), or -obj(type)(idStart,idEnd).");
                    return 2;
                }
            }
            else if (string.Equals(args[argumentIndex], "-average", StringComparison.OrdinalIgnoreCase))
            {
                average = true;
            }
            else if (string.Equals(args[argumentIndex], "-reidentify", StringComparison.OrdinalIgnoreCase))
            {
                reidentify = true;
            }
            else if (args[argumentIndex].StartsWith("-tolerateDiff:", StringComparison.OrdinalIgnoreCase))
            {
                string value = args[argumentIndex].Substring("-tolerateDiff:".Length);
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out tolerateDiff) ||
                    float.IsNaN(tolerateDiff) || float.IsInfinity(tolerateDiff) || tolerateDiff < 0.0f)
                {
                    Console.Error.WriteLine("-tolerateDiff must be a non-negative finite number: -tolerateDiff:value.");
                    return 2;
                }
                hasTolerateDiff = true;
            }
            else if (args[argumentIndex].StartsWith("-color(", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseColorArgument(args[argumentIndex], out colorR, out colorG, out colorB, out colorA))
                {
                    Console.Error.WriteLine("-color must use four values from 0 to 255: -color(R,G,B,A).");
                    return 2;
                }
                hasColor = true;
            }
            else
            {
                Console.Error.WriteLine("Optional arguments must be -obj, -average, -reidentify, -tolerateDiff:value, or -color(R,G,B,A).");
                return 2;
            }
        }
        if (hasTolerateDiff && !reidentify)
        {
            Console.Error.WriteLine("-tolerateDiff can only be used with -reidentify.");
            return 2;
        }

        string akevPath = Path.GetFullPath(args[0]);
        string objDirectory = Path.GetFullPath(args[1]);
        if (!File.Exists(akevPath))
        {
            Console.Error.WriteLine("AKEV file not found: " + akevPath);
            return 2;
        }
        if (!Directory.Exists(objDirectory))
        {
            Console.Error.WriteLine("OBJ directory not found: " + objDirectory);
            return 2;
        }

        try
        {
            string outputPath = akevPath;
            Patch(outputPath, objDirectory, objectType, objectIdStart, objectIdEnd, average, reidentify, tolerateDiff, hasColor, colorR, colorG, colorB, colorA);
            Console.WriteLine("Patched: " + outputPath);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void Patch(string akevPath, string objDirectory, int selectedObjectType, int objectIdStart, int objectIdEnd, bool average, bool reidentify, float tolerateDiff, bool hasColor, byte colorR, byte colorG, byte colorB, byte colorA)
    {
        byte[] data = File.ReadAllBytes(akevPath);
        int dataTableOffset;
        List<Descriptor> descriptors = ReadDescriptors(data, out dataTableOffset);
        Descriptor agqg = FindDescriptor(data, descriptors, AgqgTag);
        Descriptor pnta = FindDescriptor(data, descriptors, PntaTag);
        List<float[]> points = ReadPoints(data, pnta);
        Dictionary<int, ObjMesh> meshes = ReadObjMeshes(objDirectory);

        int agqgStart = agqg.DataOffset;
        ValidateRange(data, agqgStart, AgqgHeaderSize);
        int quadCount = ReadInt32(data, agqgStart + 20);
        ValidateRange(data, agqgStart + AgqgHeaderSize, checked(quadCount * AgqgRecordSize));
        int patchedCorners = 0;
        int matchedFaces = 0;
        int patchedFaces = 0;
        int coordinateFallbackFaces = 0;
        int collapsedTriangleFaces = 0;
        int targetFaces = 0;
        List<string> unmatchedPolygons = reidentify && !hasColor ? new List<string>() : null;
        Dictionary<int, byte[]> averageColors = new Dictionary<int, byte[]>();
        List<int> resolvedObjectIds = objectIdStart >= 0 ? new List<int>() : null;
        for (int quadIndex = 0; quadIndex < quadCount; quadIndex++)
        {
            int recordOffset = agqgStart + AgqgHeaderSize + quadIndex * AgqgRecordSize;
            int objectId = ReadInt32(data, recordOffset + ObjectIdOffset);
            if (objectId < 0)
            {
                continue;
            }
            bool objectTarget = IsTargetObject(objectId, selectedObjectType, objectIdStart, objectIdEnd);
            if (!objectTarget)
            {
                continue;
            }
            targetFaces++;
            if (resolvedObjectIds != null)
            {
                int localObjectId = objectId & 0xffffff;
                if (!resolvedObjectIds.Contains(localObjectId))
                {
                    resolvedObjectIds.Add(localObjectId);
                }
            }
            bool colorTarget = hasColor;
            ObjMesh mesh = null;
            int[] matchingVertexIndices = null;
            byte[] averageColor = null;
            if (!colorTarget)
            {
                bool usedCoordinateFallback;
                if (!TryFindMatchingFace(meshes, objectId, reidentify, tolerateDiff, data, recordOffset, points, out mesh, out matchingVertexIndices, out usedCoordinateFallback))
                {
                    if (unmatchedPolygons != null)
                    {
                        AppendUnmatchedPolygon(unmatchedPolygons, quadIndex, objectId, data, recordOffset, points);
                    }
                    continue;
                }
                matchedFaces++;
                if (usedCoordinateFallback)
                {
                    coordinateFallbackFaces++;
                }
                for (int corner = 0; corner < matchingVertexIndices.Length; corner++)
                {
                    if (matchingVertexIndices[corner] >= mesh.SourceVertexCount)
                    {
                        collapsedTriangleFaces++;
                        break;
                    }
                }
                if (average && !averageColors.TryGetValue(mesh.ObjectId, out averageColor))
                {
                    averageColor = GetAverageColor(mesh);
                    averageColors.Add(mesh.ObjectId, averageColor);
                }
            }

            int cornersToPatch = GetCornerCount(data, recordOffset);
            for (int corner = 0; corner < cornersToPatch; corner++)
            {
                byte r = 255;
                byte g = 0;
                byte b = 0;
                if (colorTarget)
                {
                    r = colorR;
                    g = colorG;
                    b = colorB;
                }
                else
                {
                    if (average)
                    {
                        r = averageColor[0];
                        g = averageColor[1];
                        b = averageColor[2];
                    }
                    else
                    {
                        ObjVertex vertex = mesh.Vertices[matchingVertexIndices[corner]];
                        r = vertex.R;
                        g = vertex.G;
                        b = vertex.B;
                    }
                }
                int colorOffset = recordOffset + ColorOffset + corner * 4;
                data[colorOffset] = b;
                data[colorOffset + 1] = g;
                data[colorOffset + 2] = r;
                if (colorTarget)
                {
                    data[colorOffset + 3] = colorA;
                }
                else if (average)
                {
                    data[colorOffset + 3] = averageColor[3];
                }
                patchedCorners++;
            }
            patchedFaces++;
        }

        if (resolvedObjectIds != null)
        {
            for (int objectId = objectIdStart; objectId <= objectIdEnd; objectId++)
            {
                if (!resolvedObjectIds.Contains(objectId))
                {
                    throw new InvalidDataException("No AGQG faces were found for requested object ID " + objectId.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
            Console.WriteLine("Resolved all {0} requested object ID(s).", objectIdEnd - objectIdStart + 1);
        }

        string unmatchedPath = null;
        if (reidentify && !hasColor)
        {
            unmatchedPath = Path.Combine(Environment.CurrentDirectory, "unpatched polys.txt");
            if (unmatchedPolygons.Count > 0)
            {
                File.WriteAllLines(unmatchedPath, unmatchedPolygons.ToArray());
            }
            else if (File.Exists(unmatchedPath))
            {
                File.Delete(unmatchedPath);
                unmatchedPath = null;
            }
        }

        if (!hasColor && matchedFaces == 0)
        {
            throw new InvalidDataException("No AGQG faces matched the supplied OBJ files. Exported OBJ geometry must retain original AKEV coordinates." +
                (unmatchedPath == null ? string.Empty : " Unmatched polygon coordinates: " + unmatchedPath));
        }

        File.WriteAllBytes(akevPath, data);
        Console.WriteLine("Patched {0} AGQG corners in {1} face(s).", patchedCorners, patchedFaces);
        if (reidentify && !hasColor)
        {
            Console.WriteLine("Reidentified {0} face(s) by independent vertex coordinates; {1} target face(s) remained unmatched.",
                coordinateFallbackFaces, targetFaces - matchedFaces);
            Console.WriteLine("Recovered {0} collapsed triangle(s) by nearest-mesh color interpolation.", collapsedTriangleFaces);
            if (unmatchedPath != null)
            {
                Console.WriteLine("Unmatched polygon coordinates: " + unmatchedPath);
            }
        }
    }

    private static void AppendUnmatchedPolygon(List<string> output, int quadIndex, int objectId, byte[] data, int recordOffset, List<float[]> points)
    {
        int objectType = (objectId >> 24) & 0xff;
        int localObjectId = objectId & 0xffffff;
        string objectTypeName = objectType < ObjectTypeNames.Length ? ObjectTypeNames[objectType] : "type" + objectType.ToString("D2", CultureInfo.InvariantCulture);
        output.Add(string.Format(CultureInfo.InvariantCulture,
            "AGQG polygon {0}, object ID 0x{1:X8} ({1}), type {2}, local ID {3}, OBJ {2}_{3}.obj",
            quadIndex, objectId, objectTypeName, localObjectId));
        int cornerCount = GetCornerCount(data, recordOffset);
        for (int corner = 0; corner < cornerCount; corner++)
        {
            int pointIndex = ReadInt32(data, recordOffset + PointIndicesOffset + corner * 4);
            if (pointIndex < 0 || pointIndex >= points.Count)
            {
                output.Add(string.Format(CultureInfo.InvariantCulture, "  corner {0}: invalid PNTA index {1}", corner, pointIndex));
                continue;
            }
            float[] point = points[pointIndex];
            output.Add(string.Format(CultureInfo.InvariantCulture, "  corner {0}: {1:R}, {2:R}, {3:R}", corner, point[0], point[1], point[2]));
        }
        output.Add(string.Empty);
    }

    private static List<Descriptor> ReadDescriptors(byte[] data, out int dataTableOffset)
    {
        ValidateRange(data, 0, 64);
        int version = ReadInt32(data, 8);
        if (version != Version31 && version != Version32)
        {
            throw new InvalidDataException("The input is not a supported Oni instance file.");
        }
        int instanceCount = ReadInt32(data, 20);
        dataTableOffset = ReadInt32(data, 32);
        ValidateRange(data, 64, checked(instanceCount * DescriptorSize));
        List<Descriptor> result = new List<Descriptor>(instanceCount);
        for (int i = 0; i < instanceCount; i++)
        {
            int offset = 64 + i * DescriptorSize;
            Descriptor descriptor = new Descriptor();
            descriptor.Tag = ReadInt32(data, offset);
            descriptor.DataOffset = checked(dataTableOffset + ReadInt32(data, offset + 4));
            descriptor.DataSize = ReadInt32(data, offset + 12);
            result.Add(descriptor);
        }
        return result;
    }

    private static Descriptor FindDescriptor(byte[] data, List<Descriptor> descriptors, int tag)
    {
        foreach (Descriptor descriptor in descriptors)
        {
            if (descriptor.Tag == tag)
            {
                ValidateRange(data, descriptor.DataOffset, descriptor.DataSize);
                return descriptor;
            }
        }
        throw new InvalidDataException("The AKEV instance does not reference the required data.");
    }

    private static List<float[]> ReadPoints(byte[] data, Descriptor pnta)
    {
        int offset = pnta.DataOffset + 52;
        ValidateRange(data, offset, 4);
        int count = ReadInt32(data, offset);
        ValidateRange(data, offset + 4, checked(count * 12));
        List<float[]> points = new List<float[]>(count);
        for (int i = 0; i < count; i++)
        {
            int pointOffset = offset + 4 + i * 12;
            points.Add(new float[] { ReadSingle(data, pointOffset), ReadSingle(data, pointOffset + 4), ReadSingle(data, pointOffset + 8) });
        }
        return points;
    }

    private static Dictionary<int, ObjMesh> ReadObjMeshes(string directory)
    {
        Dictionary<int, ObjMesh> result = new Dictionary<int, ObjMesh>();
        string[] files = Directory.GetFiles(directory, "*.obj");
        foreach (string file in files)
        {
            ObjMesh mesh = ReadObj(file);
            if (mesh != null)
            {
                result.Add(mesh.ObjectId, mesh);
            }
        }
        if (result.Count == 0)
        {
            throw new InvalidDataException("The OBJ directory contains no files named {object type}_{object id}.obj.");
        }
        return result;
    }

    private static ObjMesh ReadObj(string path)
    {
        int objectId;
        if (!TryParseObjectId(Path.GetFileNameWithoutExtension(path), out objectId))
        {
            return null;
        }
        ObjMesh mesh = new ObjMesh();
        mesh.ObjectId = objectId;
        List<ObjVertex> positions = new List<ObjVertex>();
        List<ObjTexCoord> texcoords = new List<ObjTexCoord>();
        using (StreamReader reader = new StreamReader(path))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || parts[0].StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }
                if (parts[0] == "v")
                {
                    if (parts.Length < 4)
                    {
                        throw new InvalidDataException("OBJ position is incomplete: " + path);
                    }
                    ObjVertex vertex = new ObjVertex();
                    vertex.X = ParseFloat(parts[1]);
                    vertex.Y = ParseFloat(parts[2]);
                    vertex.Z = ParseFloat(parts[3]);
                    vertex.A = 255;
                    if (parts.Length >= 7)
                    {
                        vertex.R = ToColorByte(ParseFloat(parts[4]));
                        vertex.G = ToColorByte(ParseFloat(parts[5]));
                        vertex.B = ToColorByte(ParseFloat(parts[6]));
                        if (parts.Length >= 8)
                        {
                            vertex.A = ToColorByte(ParseFloat(parts[7]));
                        }
                    }
                    positions.Add(vertex);
                }
                else if (parts[0] == "vt")
                {
                    if (parts.Length < 3)
                    {
                        throw new InvalidDataException("OBJ texture coordinate is incomplete: " + path);
                    }
                    ObjTexCoord texcoord = new ObjTexCoord();
                    texcoord.U = ParseFloat(parts[1]);
                    texcoord.V = ParseFloat(parts[2]);
                    texcoord.W = parts.Length >= 4 ? ParseFloat(parts[3]) : 0.0f;
                    texcoords.Add(texcoord);
                }
                else if (parts[0] == "f" && parts.Length >= 4)
                {
                    ObjFace face = new ObjFace();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string[] indices = parts[i].Split('/');
                        if (indices.Length == 0 || indices[0].Length == 0)
                        {
                            throw new InvalidDataException("OBJ face has no position index: " + path);
                        }
                        ObjCorner corner = new ObjCorner();
                        corner.PositionIndex = ResolveObjIndex(indices[0], positions.Count, "position", path);
                        corner.TexCoordIndex = -1;
                        if (indices.Length >= 2 && indices[1].Length != 0)
                        {
                            corner.TexCoordIndex = ResolveObjIndex(indices[1], texcoords.Count, "texture coordinate", path);
                        }
                        face.Corners.Add(corner);
                    }
                    face.CornerCount = face.Corners.Count;
                    if (face.Corners.Count == 3)
                    {
                        face.Corners.Add(face.Corners[2]);
                    }
                    if (face.Corners.Count != 4)
                    {
                        throw new InvalidDataException("Only triangle and quad OBJ faces are supported: " + path);
                    }
                    mesh.Faces.Add(face);
                }
                // vn records are intentionally ignored; normals are not part of the imported model.
            }
        }

        Dictionary<string, int> resolvedVertices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
        {
            ObjFace face = mesh.Faces[faceIndex];
            for (int cornerIndex = 0; cornerIndex < face.Corners.Count; cornerIndex++)
            {
                ObjCorner corner = face.Corners[cornerIndex];
                string key = corner.PositionIndex.ToString(CultureInfo.InvariantCulture) + ":" + corner.TexCoordIndex.ToString(CultureInfo.InvariantCulture);
                int vertexIndex;
                if (!resolvedVertices.TryGetValue(key, out vertexIndex))
                {
                    ObjVertex source = positions[corner.PositionIndex];
                    ObjVertex vertex = new ObjVertex();
                    vertex.X = source.X;
                    vertex.Y = source.Y;
                    vertex.Z = source.Z;
                    vertex.R = source.R;
                    vertex.G = source.G;
                    vertex.B = source.B;
                    vertex.A = source.A;
                    if (corner.TexCoordIndex >= 0)
                    {
                        ObjTexCoord texcoord = texcoords[corner.TexCoordIndex];
                        vertex.U = texcoord.U;
                        vertex.V = texcoord.V;
                        vertex.W = texcoord.W;
                    }
                    vertexIndex = mesh.Vertices.Count;
                    mesh.Vertices.Add(vertex);
                    resolvedVertices.Add(key, vertexIndex);
                }
                face.VertexIndices.Add(vertexIndex);
            }
            face.HasNormal = TryGetObjFaceNormal(mesh, face, out face.NormalX, out face.NormalY, out face.NormalZ);
        }
        mesh.SourceVertexCount = mesh.Vertices.Count;
        return mesh;
    }

    private static int ResolveObjIndex(string text, int count, string attributeName, string path)
    {
        int index = int.Parse(text, CultureInfo.InvariantCulture);
        if (index == 0)
        {
            throw new InvalidDataException("OBJ " + attributeName + " index cannot be zero: " + path);
        }
        int resolved = index > 0 ? index - 1 : count + index;
        if (resolved < 0 || resolved >= count)
        {
            throw new InvalidDataException("OBJ " + attributeName + " index is out of range: " + path);
        }
        return resolved;
    }

    private static bool TryFindMatchingFace(Dictionary<int, ObjMesh> meshes, int objectId, bool reidentify, float tolerateDiff, byte[] data, int recordOffset, List<float[]> points, out ObjMesh matchingMesh, out int[] matchingVertexIndices, out bool usedCoordinateFallback)
    {
        matchingMesh = null;
        matchingVertexIndices = null;
        usedCoordinateFallback = false;
        double targetNormalX;
        double targetNormalY;
        double targetNormalZ;
        bool hasTargetNormal = TryGetAgqgFaceNormal(data, recordOffset, points, out targetNormalX, out targetNormalY, out targetNormalZ);
        if (!reidentify)
        {
            if (!meshes.TryGetValue(objectId, out matchingMesh))
            {
                return false;
            }
            double matchingDistance;
            double matchingAlignment;
            matchingVertexIndices = FindMatchingFaceVertices(matchingMesh, tolerateDiff, data, recordOffset, points,
                hasTargetNormal, targetNormalX, targetNormalY, targetNormalZ, out matchingDistance, out matchingAlignment);
            if (matchingVertexIndices != null)
            {
                return true;
            }
            ObjVertex interpolatedVertex;
            if (!TryFindCollapsedTriangleVertices(matchingMesh, tolerateDiff, data, recordOffset, points,
                out matchingVertexIndices, out interpolatedVertex, out matchingDistance))
            {
                return false;
            }
            AddInterpolatedVertex(matchingMesh, matchingVertexIndices, interpolatedVertex);
            return true;
        }

        int objectType = (objectId >> 24) & 0xff;
        ObjMesh nearestMesh = null;
        int[] nearestVertexIndices = null;
        double nearestDistance = double.MaxValue;
        double nearestAlignment = double.MinValue;
        foreach (KeyValuePair<int, ObjMesh> entry in meshes)
        {
            if (((entry.Key >> 24) & 0xff) != objectType)
            {
                continue;
            }
            double distance;
            double alignment;
            int[] vertexIndices = FindMatchingFaceVertices(entry.Value, tolerateDiff, data, recordOffset, points,
                hasTargetNormal, targetNormalX, targetNormalY, targetNormalZ, out distance, out alignment);
            if (vertexIndices != null && IsBetterMatch(distance, alignment, entry.Key, nearestDistance, nearestAlignment, nearestMesh))
            {
                nearestMesh = entry.Value;
                nearestVertexIndices = vertexIndices;
                nearestDistance = distance;
                nearestAlignment = alignment;
            }
        }
        if (nearestMesh != null)
        {
            matchingMesh = nearestMesh;
            matchingVertexIndices = nearestVertexIndices;
            return true;
        }

        foreach (KeyValuePair<int, ObjMesh> entry in meshes)
        {
            if (((entry.Key >> 24) & 0xff) != objectType)
            {
                continue;
            }
            double distance;
            double alignment;
            int[] vertexIndices = FindNearestVertexIndices(entry.Value, tolerateDiff, data, recordOffset, points,
                hasTargetNormal, targetNormalX, targetNormalY, targetNormalZ, out distance, out alignment);
            if (vertexIndices != null && IsBetterMatch(distance, alignment, entry.Key, nearestDistance, nearestAlignment, nearestMesh))
            {
                nearestMesh = entry.Value;
                nearestVertexIndices = vertexIndices;
                nearestDistance = distance;
                nearestAlignment = alignment;
            }
        }
        ObjVertex nearestInterpolatedVertex = null;
        if (nearestMesh == null)
        {
            foreach (KeyValuePair<int, ObjMesh> entry in meshes)
            {
                if (((entry.Key >> 24) & 0xff) != objectType)
                {
                    continue;
                }
                double distance;
                ObjVertex interpolatedVertex;
                int[] vertexIndices;
                if (TryFindCollapsedTriangleVertices(entry.Value, tolerateDiff, data, recordOffset, points,
                    out vertexIndices, out interpolatedVertex, out distance) &&
                    IsBetterMatch(distance, 0.0, entry.Key, nearestDistance, nearestAlignment, nearestMesh))
                {
                    nearestMesh = entry.Value;
                    nearestVertexIndices = vertexIndices;
                    nearestInterpolatedVertex = interpolatedVertex;
                    nearestDistance = distance;
                    nearestAlignment = 0.0;
                }
            }
        }
        if (nearestMesh != null && nearestInterpolatedVertex != null)
        {
            AddInterpolatedVertex(nearestMesh, nearestVertexIndices, nearestInterpolatedVertex);
        }
        matchingMesh = nearestMesh;
        matchingVertexIndices = nearestVertexIndices;
        usedCoordinateFallback = matchingMesh != null;
        return matchingMesh != null;
    }

    private static bool IsBetterMatch(double distance, double alignment, int objectId, double bestDistance, double bestAlignment, ObjMesh bestMesh)
    {
        return distance < bestDistance ||
            (distance == bestDistance && (alignment > bestAlignment ||
            (alignment == bestAlignment && (bestMesh == null || objectId < bestMesh.ObjectId))));
    }

    private static int[] FindMatchingFaceVertices(ObjMesh mesh, float tolerateDiff, byte[] data, int recordOffset, List<float[]> points,
        bool hasTargetNormal, double targetNormalX, double targetNormalY, double targetNormalZ, out double totalDistance, out double normalAlignment)
    {
        totalDistance = double.MaxValue;
        normalAlignment = double.MinValue;
        int[] bestVertexIndices = null;
        int cornerCount = GetCornerCount(data, recordOffset);
        for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
        {
            ObjFace face = mesh.Faces[faceIndex];
            double alignment = GetNormalAlignment(hasTargetNormal, targetNormalX, targetNormalY, targetNormalZ, face);
            if (face.CornerCount < cornerCount || alignment < 0.0)
            {
                continue;
            }
            int[] vertexIndices = new int[cornerCount];
            bool[] usedCorners = new bool[face.CornerCount];
            bool matches = true;
            double faceDistance = 0.0;
            for (int corner = 0; corner < cornerCount; corner++)
            {
                int pointIndex = ReadInt32(data, recordOffset + PointIndicesOffset + corner * 4);
                if (pointIndex < 0 || pointIndex >= points.Count)
                {
                    matches = false;
                    break;
                }
                int matchingCorner = -1;
                double matchingDistance = double.MaxValue;
                for (int candidateCorner = 0; candidateCorner < face.CornerCount; candidateCorner++)
                {
                    if (usedCorners[candidateCorner])
                    {
                        continue;
                    }
                    double distance = GetPositionDistanceSquared(points[pointIndex], mesh.Vertices[face.VertexIndices[candidateCorner]]);
                    if (DistanceIsWithinTolerance(distance, tolerateDiff) && distance < matchingDistance)
                    {
                        matchingCorner = candidateCorner;
                        matchingDistance = distance;
                    }
                }
                if (matchingCorner < 0)
                {
                    matches = false;
                    break;
                }
                usedCorners[matchingCorner] = true;
                vertexIndices[corner] = face.VertexIndices[matchingCorner];
                faceDistance += matchingDistance;
            }
            if (matches && IsBetterGeometryMatch(faceDistance, alignment, totalDistance, normalAlignment))
            {
                bestVertexIndices = vertexIndices;
                totalDistance = faceDistance;
                normalAlignment = alignment;
            }
        }
        return bestVertexIndices;
    }

    private static int[] FindNearestVertexIndices(ObjMesh mesh, float tolerateDiff, byte[] data, int recordOffset, List<float[]> points,
        bool hasTargetNormal, double targetNormalX, double targetNormalY, double targetNormalZ, out double totalDistance, out double normalAlignment)
    {
        totalDistance = 0.0;
        normalAlignment = 0.0;
        int cornerCount = GetCornerCount(data, recordOffset);
        int[] vertexIndices = new int[cornerCount];
        bool[] usedVertices = new bool[mesh.Vertices.Count];
        for (int corner = 0; corner < cornerCount; corner++)
        {
            int pointIndex = ReadInt32(data, recordOffset + PointIndicesOffset + corner * 4);
            if (pointIndex < 0 || pointIndex >= points.Count)
            {
                totalDistance = double.MaxValue;
                return null;
            }
            int nearestVertexIndex = -1;
            double nearestDistance = double.MaxValue;
            double nearestVertexAlignment = double.MinValue;
            for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
            {
                ObjFace face = mesh.Faces[faceIndex];
                double alignment = GetNormalAlignment(hasTargetNormal, targetNormalX, targetNormalY, targetNormalZ, face);
                if (alignment < 0.0)
                {
                    continue;
                }
                for (int candidateCorner = 0; candidateCorner < face.CornerCount; candidateCorner++)
                {
                    int vertexIndex = face.VertexIndices[candidateCorner];
                    if (usedVertices[vertexIndex])
                    {
                        continue;
                    }
                    double distance = GetPositionDistanceSquared(points[pointIndex], mesh.Vertices[vertexIndex]);
                    if (IsBetterGeometryMatch(distance, alignment, nearestDistance, nearestVertexAlignment))
                    {
                        nearestVertexIndex = vertexIndex;
                        nearestDistance = distance;
                        nearestVertexAlignment = alignment;
                    }
                }
            }
            if (nearestVertexIndex < 0 || !DistanceIsWithinTolerance(nearestDistance, tolerateDiff))
            {
                totalDistance = double.MaxValue;
                return null;
            }
            vertexIndices[corner] = nearestVertexIndex;
            usedVertices[nearestVertexIndex] = true;
            totalDistance += nearestDistance;
            normalAlignment += nearestVertexAlignment;
        }
        normalAlignment /= cornerCount;
        return vertexIndices;
    }

    private static bool IsBetterGeometryMatch(double distance, double alignment, double bestDistance, double bestAlignment)
    {
        return distance < bestDistance || (distance == bestDistance && alignment > bestAlignment);
    }

    private static bool TryFindCollapsedTriangleVertices(ObjMesh mesh, float tolerateDiff, byte[] data, int recordOffset, List<float[]> points,
        out int[] vertexIndices, out ObjVertex interpolatedVertex, out double totalDistance)
    {
        vertexIndices = null;
        interpolatedVertex = null;
        totalDistance = double.MaxValue;
        if (GetCornerCount(data, recordOffset) != 3)
        {
            return false;
        }

        float[][] targetPoints = new float[3][];
        for (int corner = 0; corner < targetPoints.Length; corner++)
        {
            int pointIndex = ReadInt32(data, recordOffset + PointIndicesOffset + corner * 4);
            if (pointIndex < 0 || pointIndex >= points.Count)
            {
                return false;
            }
            targetPoints[corner] = points[pointIndex];
        }

        int endpointA = 0;
        int endpointB = 1;
        double longestDistance = GetPositionDistanceSquared(targetPoints[0], targetPoints[1]);
        for (int first = 0; first < targetPoints.Length; first++)
        {
            for (int second = first + 1; second < targetPoints.Length; second++)
            {
                double distance = GetPositionDistanceSquared(targetPoints[first], targetPoints[second]);
                if (distance > longestDistance)
                {
                    endpointA = first;
                    endpointB = second;
                    longestDistance = distance;
                }
            }
        }
        if (longestDistance <= 0.0)
        {
            return false;
        }

        int middleCorner = 3 - endpointA - endpointB;
        double interpolation;
        double lineDistance = GetPointSegmentDistanceSquared(targetPoints[middleCorner], targetPoints[endpointA], targetPoints[endpointB], out interpolation);
        double lineTolerance = Math.Max(tolerateDiff, 0.00025f);
        if (interpolation <= 0.0 || interpolation >= 1.0 || lineDistance > lineTolerance * lineTolerance)
        {
            return false;
        }

        int vertexA = FindNearestSourceVertex(mesh, targetPoints[endpointA], -1, out double distanceA);
        int vertexB = FindNearestSourceVertex(mesh, targetPoints[endpointB], vertexA, out double distanceB);
        if (vertexA < 0 || vertexB < 0)
        {
            return false;
        }

        ObjVertex sourceA = mesh.Vertices[vertexA];
        ObjVertex sourceB = mesh.Vertices[vertexB];
        interpolatedVertex = new ObjVertex();
        interpolatedVertex.X = targetPoints[middleCorner][0];
        interpolatedVertex.Y = targetPoints[middleCorner][1];
        interpolatedVertex.Z = targetPoints[middleCorner][2];
        interpolatedVertex.R = InterpolateColor(sourceA.R, sourceB.R, interpolation);
        interpolatedVertex.G = InterpolateColor(sourceA.G, sourceB.G, interpolation);
        interpolatedVertex.B = InterpolateColor(sourceA.B, sourceB.B, interpolation);
        interpolatedVertex.A = InterpolateColor(sourceA.A, sourceB.A, interpolation);
        vertexIndices = new int[] { -1, -1, -1 };
        vertexIndices[endpointA] = vertexA;
        vertexIndices[endpointB] = vertexB;
        totalDistance = distanceA + distanceB + lineDistance;
        return true;
    }

    private static int FindNearestSourceVertex(ObjMesh mesh, float[] point, int excludedVertex, out double nearestDistance)
    {
        int nearestVertex = -1;
        nearestDistance = double.MaxValue;
        for (int vertexIndex = 0; vertexIndex < mesh.SourceVertexCount; vertexIndex++)
        {
            if (vertexIndex == excludedVertex)
            {
                continue;
            }
            double distance = GetPositionDistanceSquared(point, mesh.Vertices[vertexIndex]);
            if (distance < nearestDistance)
            {
                nearestVertex = vertexIndex;
                nearestDistance = distance;
            }
        }
        return nearestVertex;
    }

    private static double GetPointSegmentDistanceSquared(float[] point, float[] endpointA, float[] endpointB, out double interpolation)
    {
        double edgeX = endpointB[0] - endpointA[0];
        double edgeY = endpointB[1] - endpointA[1];
        double edgeZ = endpointB[2] - endpointA[2];
        double edgeLengthSquared = edgeX * edgeX + edgeY * edgeY + edgeZ * edgeZ;
        double pointX = point[0] - endpointA[0];
        double pointY = point[1] - endpointA[1];
        double pointZ = point[2] - endpointA[2];
        interpolation = (pointX * edgeX + pointY * edgeY + pointZ * edgeZ) / edgeLengthSquared;
        double projectedX = endpointA[0] + interpolation * edgeX;
        double projectedY = endpointA[1] + interpolation * edgeY;
        double projectedZ = endpointA[2] + interpolation * edgeZ;
        double differenceX = point[0] - projectedX;
        double differenceY = point[1] - projectedY;
        double differenceZ = point[2] - projectedZ;
        return differenceX * differenceX + differenceY * differenceY + differenceZ * differenceZ;
    }

    private static byte InterpolateColor(byte first, byte second, double interpolation)
    {
        return (byte)Math.Round(first + (second - first) * interpolation, MidpointRounding.AwayFromZero);
    }

    private static void AddInterpolatedVertex(ObjMesh mesh, int[] vertexIndices, ObjVertex interpolatedVertex)
    {
        int vertexIndex = mesh.Vertices.Count;
        mesh.Vertices.Add(interpolatedVertex);
        for (int corner = 0; corner < vertexIndices.Length; corner++)
        {
            if (vertexIndices[corner] < 0)
            {
                vertexIndices[corner] = vertexIndex;
                return;
            }
        }
        throw new InvalidDataException("Collapsed triangle recovery did not identify a missing corner.");
    }

    private static double GetNormalAlignment(bool hasTargetNormal, double targetNormalX, double targetNormalY, double targetNormalZ, ObjFace face)
    {
        if (!hasTargetNormal || !face.HasNormal)
        {
            return 0.0;
        }
        double alignment = targetNormalX * face.NormalX + targetNormalY * face.NormalY + targetNormalZ * face.NormalZ;
        return alignment > 0.0 ? alignment : -1.0;
    }

    private static bool TryGetAgqgFaceNormal(byte[] data, int recordOffset, List<float[]> points, out double normalX, out double normalY, out double normalZ)
    {
        normalX = 0.0;
        normalY = 0.0;
        normalZ = 0.0;
        double longestEdgeSquared = 0.0;
        int cornerCount = GetCornerCount(data, recordOffset);
        for (int corner = 0; corner < cornerCount; corner++)
        {
            int nextCorner = (corner + 1) % cornerCount;
            int pointIndex = ReadInt32(data, recordOffset + PointIndicesOffset + corner * 4);
            int nextPointIndex = ReadInt32(data, recordOffset + PointIndicesOffset + nextCorner * 4);
            if (pointIndex < 0 || pointIndex >= points.Count || nextPointIndex < 0 || nextPointIndex >= points.Count)
            {
                return false;
            }
            float[] point = points[pointIndex];
            float[] nextPoint = points[nextPointIndex];
            normalX += (point[1] - nextPoint[1]) * (point[2] + nextPoint[2]);
            normalY += (point[2] - nextPoint[2]) * (point[0] + nextPoint[0]);
            normalZ += (point[0] - nextPoint[0]) * (point[1] + nextPoint[1]);
            longestEdgeSquared = Math.Max(longestEdgeSquared, GetPositionDistanceSquared(point, nextPoint));
        }
        return FaceNormalIsStable(normalX, normalY, normalZ, longestEdgeSquared) && TryNormalize(ref normalX, ref normalY, ref normalZ);
    }

    private static bool TryGetObjFaceNormal(ObjMesh mesh, ObjFace face, out double normalX, out double normalY, out double normalZ)
    {
        normalX = 0.0;
        normalY = 0.0;
        normalZ = 0.0;
        double longestEdgeSquared = 0.0;
        for (int corner = 0; corner < face.CornerCount; corner++)
        {
            int nextCorner = (corner + 1) % face.CornerCount;
            ObjVertex vertex = mesh.Vertices[face.VertexIndices[corner]];
            ObjVertex nextVertex = mesh.Vertices[face.VertexIndices[nextCorner]];
            normalX += (vertex.Y - nextVertex.Y) * (vertex.Z + nextVertex.Z);
            normalY += (vertex.Z - nextVertex.Z) * (vertex.X + nextVertex.X);
            normalZ += (vertex.X - nextVertex.X) * (vertex.Y + nextVertex.Y);
            longestEdgeSquared = Math.Max(longestEdgeSquared, GetPositionDistanceSquared(vertex, nextVertex));
        }
        return FaceNormalIsStable(normalX, normalY, normalZ, longestEdgeSquared) && TryNormalize(ref normalX, ref normalY, ref normalZ);
    }

    private static bool FaceNormalIsStable(double normalX, double normalY, double normalZ, double longestEdgeSquared)
    {
        const double minimumRelativeAreaSquared = 1.0e-7;
        double normalLengthSquared = normalX * normalX + normalY * normalY + normalZ * normalZ;
        return longestEdgeSquared > 0.0 && normalLengthSquared > longestEdgeSquared * longestEdgeSquared * minimumRelativeAreaSquared;
    }

    private static bool TryNormalize(ref double x, ref double y, ref double z)
    {
        double length = Math.Sqrt(x * x + y * y + z * z);
        if (length <= 1.0e-12)
        {
            x = 0.0;
            y = 0.0;
            z = 0.0;
            return false;
        }
        x /= length;
        y /= length;
        z /= length;
        return true;
    }

    private static int GetCornerCount(byte[] data, int recordOffset)
    {
        const int triangleFlag = 64;
        return (ReadInt32(data, recordOffset + 48) & triangleFlag) != 0 ? 3 : 4;
    }

    private static bool TryParseObjectId(string name, out int objectId)
    {
        objectId = 0;
        int separator = name.LastIndexOf('_');
        if (separator <= 0 || separator == name.Length - 1)
        {
            return false;
        }
        int objectType;
        if (!ObjectTypes.TryGetValue(name.Substring(0, separator), out objectType))
        {
            return false;
        }
        int localId;
        if (!int.TryParse(name.Substring(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out localId) || localId < 0 || localId > 0xffffff)
        {
            return false;
        }
        objectId = (objectType << 24) | localId;
        return true;
    }

    private static double GetPositionDistanceSquared(float[] point, ObjVertex vertex)
    {
        double x = point[0] - vertex.X;
        double y = point[1] - vertex.Y;
        double z = point[2] - vertex.Z;
        return x * x + y * y + z * z;
    }

    private static double GetPositionDistanceSquared(float[] point, float[] otherPoint)
    {
        double x = point[0] - otherPoint[0];
        double y = point[1] - otherPoint[1];
        double z = point[2] - otherPoint[2];
        return x * x + y * y + z * z;
    }

    private static double GetPositionDistanceSquared(ObjVertex vertex, ObjVertex otherVertex)
    {
        double x = vertex.X - otherVertex.X;
        double y = vertex.Y - otherVertex.Y;
        double z = vertex.Z - otherVertex.Z;
        return x * x + y * y + z * z;
    }

    private static bool DistanceIsWithinTolerance(double distanceSquared, float tolerateDiff)
    {
        double toleranceSquared = (double)tolerateDiff * tolerateDiff;
        return distanceSquared <= toleranceSquared;
    }

    private static byte[] GetAverageColor(ObjMesh mesh)
    {
        if (mesh.Vertices.Count == 0)
        {
            throw new InvalidDataException("Cannot average colors for an OBJ without vertices.");
        }
        long r = 0;
        long g = 0;
        long b = 0;
        long a = 0;
        for (int vertexIndex = 0; vertexIndex < mesh.SourceVertexCount; vertexIndex++)
        {
            ObjVertex vertex = mesh.Vertices[vertexIndex];
            r += vertex.R;
            g += vertex.G;
            b += vertex.B;
            a += vertex.A;
        }
        int count = mesh.SourceVertexCount;
        return new byte[]
        {
            (byte)(r / count),
            (byte)(g / count),
            (byte)(b / count),
            (byte)(a / count)
        };
    }

    private static bool TryParseColorArgument(string argument, out byte r, out byte g, out byte b, out byte a)
    {
        r = 0;
        g = 0;
        b = 0;
        a = 0;
        if (!argument.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }
        string[] values = argument.Substring("-color(".Length, argument.Length - "-color(".Length - 1).Split(',');
        if (values.Length != 4)
        {
            return false;
        }
        byte[] components = new byte[4];
        for (int i = 0; i < components.Length; i++)
        {
            int component;
            if (!int.TryParse(values[i].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out component) ||
                component < byte.MinValue || component > byte.MaxValue)
            {
                return false;
            }
            components[i] = (byte)component;
        }
        r = components[0];
        g = components[1];
        b = components[2];
        a = components[3];
        return true;
    }

    private static bool TryParseObjectArgument(string argument, out int objectType, out int start, out int end)
    {
        objectType = -1;
        start = 0;
        end = 0;
        if (!argument.EndsWith(")", StringComparison.Ordinal) || !argument.StartsWith("-obj(", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        string value = argument.Substring("-obj(".Length, argument.Length - "-obj(".Length - 1);
        int rangeStart = value.IndexOf(")(", StringComparison.Ordinal);
        string typeName = rangeStart < 0 ? value : value.Substring(0, rangeStart);
        if (typeName.Length == 0 || !ObjectTypes.TryGetValue(typeName, out objectType))
        {
            return false;
        }
        if (rangeStart < 0)
        {
            start = -1;
            end = -1;
            return true;
        }
        string[] values = value.Substring(rangeStart + 2).Split(',');
        if (values.Length == 1)
        {
            if (!int.TryParse(values[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out start) ||
                start < 0 || start > 0xffffff)
            {
                return false;
            }
            end = start;
            return true;
        }
        if (values.Length != 2 ||
            !int.TryParse(values[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out start) ||
            !int.TryParse(values[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out end) ||
            start < 0 || end < start || end > 0xffffff)
        {
            return false;
        }
        return true;
    }

    private static bool IsTargetObject(int objectId, int selectedObjectType, int objectIdStart, int objectIdEnd)
    {
        if (selectedObjectType < 0)
        {
            return true;
        }
        if (((objectId >> 24) & 0xff) != selectedObjectType)
        {
            return false;
        }
        int localId = objectId & 0xffffff;
        return objectIdStart < 0 || (localId >= objectIdStart && localId <= objectIdEnd);
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value, CultureInfo.InvariantCulture);
    }

    private static byte ToColorByte(float value)
    {
        value = Math.Max(0.0f, Math.Min(1.0f, value));
        return (byte)Math.Round(value * 255.0f, MidpointRounding.AwayFromZero);
    }

    private static int ReadInt32(byte[] data, int offset)
    {
        ValidateRange(data, offset, 4);
        return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
    }

    private static float ReadSingle(byte[] data, int offset)
    {
        return BitConverter.ToSingle(data, offset);
    }

    private static void ValidateRange(byte[] data, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
        {
            throw new InvalidDataException("The AKEV binary contains an invalid data offset.");
        }
    }
}
