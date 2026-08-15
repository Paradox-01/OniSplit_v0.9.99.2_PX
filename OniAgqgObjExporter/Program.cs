using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

internal static class Program
{
    private static readonly string[] ObjectTypeNames = new string[19]
    {
        "world", "char", "patr", "door", "flag", "furn", "type06", "type07", "part", "pwru",
        "sndg", "trgv", "weap", "trig", "turr", "cons", "cmbt", "mele", "neut"
    };

    private sealed class Vertex
    {
        public int PointIndex;
        public byte R;
        public byte G;
        public byte B;
        public float U;
        public float V;
    }

    private sealed class Face
    {
        public readonly List<int> Vertices = new List<int>();
        public string MaterialName;
    }

    private sealed class ObjectMesh
    {
        public int Type;
        public int Id;
        public readonly List<Vertex> Vertices = new List<Vertex>();
        public readonly List<Face> Faces = new List<Face>();
        public readonly Dictionary<string, int> VertexLookup = new Dictionary<string, int>(StringComparer.Ordinal);

        public int GetVertex(int pointIndex, byte r, byte g, byte b, float u, float v)
        {
            string key = string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}:{4:R}:{5:R}", pointIndex, r, g, b, u, v);
            int index;
            if (!VertexLookup.TryGetValue(key, out index))
            {
                index = Vertices.Count;
                VertexLookup.Add(key, index);
                Vertices.Add(new Vertex { PointIndex = pointIndex, R = r, G = g, B = b, U = u, V = v });
            }
            return index;
        }
    }

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: OniAgqgObjExporter.exe <AKEV*.xml> <output-directory>");
            return 2;
        }

        string inputPath = Path.GetFullPath(args[0]);
        string outputDirectory = Path.GetFullPath(args[1]);
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine("Input file not found: " + inputPath);
            return 2;
        }
        if (!Directory.Exists(outputDirectory))
        {
            Console.Error.WriteLine("Output directory not found: " + outputDirectory);
            return 2;
        }

        try
        {
            Export(inputPath, outputDirectory);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void Export(string inputPath, string outputDirectory)
    {
        XmlDocument document = new XmlDocument();
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.ProhibitDtd = true;
        settings.XmlResolver = null;
        using (XmlReader reader = XmlReader.Create(inputPath, settings))
        {
            document.Load(reader);
        }

        XmlNodeList positionNodes = document.SelectNodes("/Oni/PNTA/Positions/Vector3");
        if (positionNodes == null || positionNodes.Count == 0)
        {
            throw new InvalidDataException("The AKEV XML contains no PNTA positions.");
        }

        List<float[]> positions = new List<float[]>(positionNodes.Count);
        foreach (XmlNode node in positionNodes)
        {
            positions.Add(ParseFloats(node, 3));
        }

        XmlNodeList texCoordNodes = document.SelectNodes("/Oni/TXCA/TexCoords/Vector2");
        XmlNodeList textureNodes = document.SelectNodes("/Oni/TXMA/Textures/Link");
        XmlNodeList quadTextureNodes = document.SelectNodes("/Oni/AGQR/Elements/AGQRElement/Texture");
        if (texCoordNodes == null || texCoordNodes.Count == 0 ||
            textureNodes == null || textureNodes.Count == 0 ||
            quadTextureNodes == null || quadTextureNodes.Count == 0)
        {
            throw new InvalidDataException("The AKEV XML is missing TXCA, TXMA, or AGQR texture data.");
        }

        List<float[]> texCoords = new List<float[]>(texCoordNodes.Count);
        foreach (XmlNode node in texCoordNodes)
        {
            texCoords.Add(ParseFloats(node, 2));
        }

        Dictionary<string, ObjectMesh> meshes = new Dictionary<string, ObjectMesh>(StringComparer.Ordinal);
        XmlNodeList quadNodes = document.SelectNodes("/Oni/AGQG/Quads/AGQGQuad");
        if (quadNodes == null)
        {
            throw new InvalidDataException("The AKEV XML contains no AGQG quads.");
        }

        if (quadTextureNodes.Count != quadNodes.Count)
        {
            throw new InvalidDataException("AGQG and AGQR do not contain the same number of quads.");
        }

        for (int quadIndex = 0; quadIndex < quadNodes.Count; quadIndex++)
        {
            XmlNode quad = quadNodes[quadIndex];
            int objectId = ParseInt(quad.SelectSingleNode("ObjectId"));
            if (objectId < 0)
            {
                continue;
            }

            int objectType = (objectId >> 24) & 0xff;
            int localId = objectId & 0xffffff;
            string meshKey = objectType.ToString(CultureInfo.InvariantCulture) + ":" + localId.ToString(CultureInfo.InvariantCulture);
            ObjectMesh mesh;
            if (!meshes.TryGetValue(meshKey, out mesh))
            {
                mesh = new ObjectMesh { Type = objectType, Id = localId };
                meshes.Add(meshKey, mesh);
            }

            XmlNodeList pointNodes = quad.SelectNodes("Points/Int32");
            XmlNodeList texIndexNodes = quad.SelectNodes("TextureCoordinates/Int32");
            XmlNodeList colorNodes = quad.SelectNodes("Colors/Color");
            if (pointNodes == null || texIndexNodes == null || colorNodes == null ||
                pointNodes.Count != texIndexNodes.Count || pointNodes.Count != colorNodes.Count ||
                (pointNodes.Count != 3 && pointNodes.Count != 4))
            {
                throw new InvalidDataException("An AGQG quad has mismatched or invalid point and color data.");
            }

            XmlNode flagsNode = quad.SelectSingleNode("Flags");
            bool isTriangle = flagsNode != null && flagsNode.InnerText.IndexOf("Triangle", StringComparison.Ordinal) >= 0;
            int vertexCount = isTriangle ? 3 : pointNodes.Count;
            if (vertexCount > pointNodes.Count)
            {
                throw new InvalidDataException("An AGQG triangle has fewer than three vertices.");
            }

            Face face = new Face();
            int textureIndex = ParseInt(quadTextureNodes[quadIndex]);
            face.MaterialName = GetMaterialName(textureIndex);
            for (int i = 0; i < vertexCount; i++)
            {
                int pointIndex = ParseInt(pointNodes[i]);
                if (pointIndex < 0 || pointIndex >= positions.Count)
                {
                    throw new InvalidDataException("An AGQG quad references a position outside PNTA.");
                }
                int texCoordIndex = ParseInt(texIndexNodes[i]);
                if (texCoordIndex < 0 || texCoordIndex >= texCoords.Count)
                {
                    throw new InvalidDataException("An AGQG quad references texture coordinates outside TXCA.");
                }
                byte[] color = ParseColor(colorNodes[i]);
                float[] texCoord = texCoords[texCoordIndex];
                face.Vertices.Add(mesh.GetVertex(pointIndex, color[0], color[1], color[2], texCoord[0], texCoord[1]));
            }
            mesh.Faces.Add(face);
        }

        List<ObjectMesh> orderedMeshes = new List<ObjectMesh>(meshes.Values);
        orderedMeshes.Sort(delegate(ObjectMesh left, ObjectMesh right)
        {
            int typeComparison = left.Type.CompareTo(right.Type);
            return typeComparison != 0 ? typeComparison : left.Id.CompareTo(right.Id);
        });
        foreach (ObjectMesh mesh in orderedMeshes)
        {
            string fileName = GetObjectTypeName(mesh.Type) + "_" + mesh.Id.ToString(CultureInfo.InvariantCulture) + ".obj";
            WriteObj(Path.Combine(outputDirectory, fileName), mesh, positions, textureNodes);
            Console.WriteLine(fileName);
        }
        string manifestPath = Path.Combine(outputDirectory, "OniAgqgObjManifest.xml");
        WriteObjectManifest(manifestPath, Path.GetFileName(inputPath), orderedMeshes, positions);
        Console.WriteLine(Path.GetFileName(manifestPath));
        Console.WriteLine("Exported {0} object(s).", meshes.Count);
    }

    private static void WriteObjectManifest(string path, string sourceFileName, List<ObjectMesh> meshes, List<float[]> positions)
    {
        XmlWriterSettings settings = new XmlWriterSettings();
        settings.Indent = true;
        settings.Encoding = System.Text.Encoding.UTF8;
        using (XmlWriter writer = XmlWriter.Create(path, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("OniAgqgObjExport");
            writer.WriteAttributeString("source", sourceFileName);
            writer.WriteAttributeString("coordinateSystem", "AKEV world");
            foreach (ObjectMesh mesh in meshes)
            {
                float minX = float.MaxValue;
                float minY = float.MaxValue;
                float minZ = float.MaxValue;
                float maxX = float.MinValue;
                float maxY = float.MinValue;
                float maxZ = float.MinValue;
                foreach (Vertex vertex in mesh.Vertices)
                {
                    float[] position = positions[vertex.PointIndex];
                    minX = Math.Min(minX, position[0]);
                    minY = Math.Min(minY, position[1]);
                    minZ = Math.Min(minZ, position[2]);
                    maxX = Math.Max(maxX, position[0]);
                    maxY = Math.Max(maxY, position[1]);
                    maxZ = Math.Max(maxZ, position[2]);
                }

                string typeName = GetObjectTypeName(mesh.Type);
                writer.WriteStartElement("Object");
                writer.WriteAttributeString("file", typeName + "_" + mesh.Id.ToString(CultureInfo.InvariantCulture) + ".obj");
                writer.WriteAttributeString("type", typeName);
                writer.WriteAttributeString("typeId", mesh.Type.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("id", mesh.Id.ToString(CultureInfo.InvariantCulture));
                WriteWorldPosition(writer, "WorldBoundsMin", minX, minY, minZ);
                WriteWorldPosition(writer, "WorldBoundsMax", maxX, maxY, maxZ);
                WriteWorldPosition(writer, "WorldBoundsCenter",
                    (minX + maxX) / 2.0f,
                    (minY + maxY) / 2.0f,
                    (minZ + maxZ) / 2.0f);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
    }

    private static void WriteWorldPosition(XmlWriter writer, string elementName, float x, float y, float z)
    {
        writer.WriteStartElement(elementName);
        writer.WriteAttributeString("x", x.ToString("R", CultureInfo.InvariantCulture));
        writer.WriteAttributeString("y", y.ToString("R", CultureInfo.InvariantCulture));
        writer.WriteAttributeString("z", z.ToString("R", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static void WriteObj(string path, ObjectMesh mesh, List<float[]> positions, XmlNodeList textureNodes)
    {
        string materialFileName = Path.ChangeExtension(Path.GetFileName(path), ".mtl");
        using (StreamWriter writer = new StreamWriter(path, false))
        {
            writer.WriteLine("# Generated by OniAgqgObjExporter");
            writer.WriteLine("# Vertex positions are original AKEV/level map coordinates; no object-local transform was applied.");
            writer.WriteLine("# Vertex colors use the common OBJ extension: v x y z r g b");
            writer.WriteLine("mtllib {0}", materialFileName);
            writer.WriteLine("o {0}_{1}", GetObjectTypeName(mesh.Type), mesh.Id);
            foreach (Vertex vertex in mesh.Vertices)
            {
                float[] position = positions[vertex.PointIndex];
                writer.WriteLine("v {0} {1} {2} {3} {4} {5}",
                    position[0].ToString(CultureInfo.InvariantCulture),
                    position[1].ToString(CultureInfo.InvariantCulture),
                    position[2].ToString(CultureInfo.InvariantCulture),
                    (vertex.R / 255.0f).ToString(CultureInfo.InvariantCulture),
                    (vertex.G / 255.0f).ToString(CultureInfo.InvariantCulture),
                    (vertex.B / 255.0f).ToString(CultureInfo.InvariantCulture));
            }
            foreach (Vertex vertex in mesh.Vertices)
            {
                writer.WriteLine("vt {0} {1}",
                    vertex.U.ToString(CultureInfo.InvariantCulture),
                    vertex.V.ToString(CultureInfo.InvariantCulture));
            }
            string currentMaterial = null;
            foreach (Face face in mesh.Faces)
            {
                if (!string.Equals(currentMaterial, face.MaterialName, StringComparison.Ordinal))
                {
                    writer.WriteLine("usemtl {0}", face.MaterialName);
                    currentMaterial = face.MaterialName;
                }
                writer.Write("f");
                foreach (int vertexIndex in face.Vertices)
                {
                    int objIndex = vertexIndex + 1;
                    writer.Write(" {0}/{1}", objIndex.ToString(CultureInfo.InvariantCulture), objIndex.ToString(CultureInfo.InvariantCulture));
                }
                writer.WriteLine();
            }
        }
        WriteMaterialFile(Path.ChangeExtension(path, ".mtl"), mesh, textureNodes);
    }

    private static void WriteMaterialFile(string path, ObjectMesh mesh, XmlNodeList textureNodes)
    {
        Dictionary<string, string> materials = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Face face in mesh.Faces)
        {
            if (!materials.ContainsKey(face.MaterialName))
            {
                int textureIndex = ParseMaterialIndex(face.MaterialName);
                materials.Add(face.MaterialName, GetTextureFileName(textureIndex, textureNodes));
            }
        }

        using (StreamWriter writer = new StreamWriter(path, false))
        {
            writer.WriteLine("# Generated by OniAgqgObjExporter");
            foreach (KeyValuePair<string, string> material in materials)
            {
                writer.WriteLine("newmtl {0}", material.Key);
                writer.WriteLine("Ka 1 1 1");
                writer.WriteLine("Kd 1 1 1");
                writer.WriteLine("Ks 0 0 0");
                writer.WriteLine("d 1");
                writer.WriteLine("illum 2");
                writer.WriteLine("map_Kd images/{0}", material.Value);
                writer.WriteLine();
            }
        }
    }

    private static string GetMaterialName(int textureIndex)
    {
        return "texture_" + textureIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static int ParseMaterialIndex(string materialName)
    {
        return XmlConvert.ToInt32(materialName.Substring("texture_".Length));
    }

    private static string GetTextureFileName(int textureIndex, XmlNodeList textureNodes)
    {
        if (textureIndex < 0 || textureIndex >= textureNodes.Count)
        {
            throw new InvalidDataException("AGQR references a texture outside TXMA.");
        }
        string name = textureNodes[textureIndex].InnerText.Trim();
        if (name.StartsWith("TXMP", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(4);
        }
        return name + ".tga";
    }

    private static string GetObjectTypeName(int objectType)
    {
        if (objectType >= 0 && objectType < ObjectTypeNames.Length)
        {
            return ObjectTypeNames[objectType];
        }
        return "type" + objectType.ToString("X2", CultureInfo.InvariantCulture);
    }

    private static int ParseInt(XmlNode node)
    {
        if (node == null)
        {
            throw new InvalidDataException("An expected integer XML element is missing.");
        }
        return XmlConvert.ToInt32(node.InnerText.Trim());
    }

    private static float[] ParseFloats(XmlNode node, int count)
    {
        string[] values = node.InnerText.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != count)
        {
            throw new InvalidDataException("An XML vector has an unexpected number of components.");
        }
        float[] result = new float[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = XmlConvert.ToSingle(values[i]);
        }
        return result;
    }

    private static byte[] ParseColor(XmlNode node)
    {
        string[] values = node.InnerText.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 3)
        {
            throw new InvalidDataException("An AGQG color has fewer than three components.");
        }
        return new byte[] { XmlConvert.ToByte(values[0]), XmlConvert.ToByte(values[1]), XmlConvert.ToByte(values[2]) };
    }
}
