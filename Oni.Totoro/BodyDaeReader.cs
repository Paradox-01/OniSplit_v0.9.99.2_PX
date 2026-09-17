using System;
using System.IO;
using Oni.Dae;
using Oni.Motoko;

namespace Oni.Totoro
{
	internal class BodyDaeReader
	{
		private Body body;

		private float shellOffset = 0.07f;

		private bool generateNormals;

		private bool flatNormals;

		private BodyDaeReader()
		{
		}

		public static Body Read(Scene scene)
		{
			BodyDaeReader bodyDaeReader = new BodyDaeReader();
			bodyDaeReader.body = new Body();
			BodyDaeReader bodyDaeReader2 = bodyDaeReader;
			bodyDaeReader2.ReadBodyParts(scene);
			return bodyDaeReader2.body;
		}

		public static Body Read(Scene scene, bool generateNormals, bool flatNormals, float shellOffset)
		{
			BodyDaeReader bodyDaeReader = new BodyDaeReader();
			bodyDaeReader.body = new Body();
			bodyDaeReader.flatNormals = flatNormals;
			bodyDaeReader.generateNormals = generateNormals;
			bodyDaeReader.shellOffset = shellOffset;
			BodyDaeReader bodyDaeReader2 = bodyDaeReader;
			bodyDaeReader2.ReadBodyParts(scene);
			return bodyDaeReader2.body;
		}

		private void ReadBodyParts(Scene scene)
		{
			BodyNode bodyNode = FindRootNode(scene);
			if (bodyNode == null)
			{
				throw new InvalidDataException("The scene does not contain any geometry nodes.");
			}
			bodyNode.Translation = Vector3.Zero;
			if (body.Nodes.Count != 19)
			{
				Console.Error.WriteLine("Non standard bone count: {0}", body.Nodes.Count);
			}
		}

		private BodyNode FindRootNode(Node daeNode)
		{
			if (Enumerable.Any(daeNode.GeometryInstances))
			{
				return ReadNode(daeNode, null);
			}
			foreach (Node node in daeNode.Nodes)
			{
				BodyNode bodyNode = FindRootNode(node);
				if (bodyNode != null)
				{
					return bodyNode;
				}
			}
			return null;
		}

		private BodyNode ReadNode(Node daeNode, BodyNode parentNode)
		{
			BodyNode bodyNode = new BodyNode();
			bodyNode.DaeNode = daeNode;
			bodyNode.Parent = parentNode;
			bodyNode.Index = body.Nodes.Count;
			BodyNode bodyNode2 = bodyNode;
			body.Nodes.Add(bodyNode2);
			foreach (GeometryInstance item in Enumerable.Where(daeNode.GeometryInstances, delegate(GeometryInstance n)
			{
				return n.Target != null;
			}))
			{
				Oni.Dae.Geometry target = item.Target;
				if (bodyNode2.Geometry != null)
				{
					Console.Error.WriteLine("The node {0} contains more than one geometry. Only the first geometry will be used.", target.Name);
				}
				bodyNode2.Geometry = GeometryDaeReader.Read(target, generateNormals, flatNormals, shellOffset);
			}
			bodyNode2.Translation = daeNode.Transforms.ToMatrix().Translation;
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			bool flag4 = false;
			Node daeNode2 = new Node();
			Node daeNode3 = new Node();
			Node daeNode4 = new Node();
			Node daeNode5 = new Node();
			foreach (Node node in daeNode.Nodes)
			{
				if (node.Name.Contains("l_thigh") || node.Name.Contains("left_thigh"))
				{
					flag = true;
					daeNode2 = node;
				}
				if (node.Name.Contains("r_thigh") || node.Name.Contains("right_thigh"))
				{
					flag2 = true;
					daeNode3 = node;
				}
				if (node.Name.Contains("l_shoulder") || node.Name.Contains("left_shoulder"))
				{
					flag3 = true;
					daeNode4 = node;
				}
				if (node.Name.Contains("r_shoulder") || node.Name.Contains("right_shoulder"))
				{
					flag4 = true;
					daeNode5 = node;
				}
			}
			if (flag != flag2)
			{
				throw new InvalidDataException("Only one thigh child for bone " + daeNode.Name);
			}
			if (flag3 != flag4)
			{
				throw new InvalidDataException("Only one shoulder child for bone " + daeNode.Name);
			}
			if ((flag | flag2) && (flag3 | flag4))
			{
				throw new InvalidDataException("Both shoulder and thigh children for bone " + daeNode.Name);
			}
			if (flag)
			{
				Console.WriteLine("Thigh children detected for bone " + daeNode.Name);
				bodyNode2.Nodes.Add(ReadNode(daeNode2, bodyNode2));
				bodyNode2.Nodes.Add(ReadNode(daeNode3, bodyNode2));
				foreach (Node node2 in daeNode.Nodes)
				{
					if (!node2.Name.Contains("l_thigh") && !node2.Name.Contains("r_thigh") && !node2.Name.Contains("left_thigh") && !node2.Name.Contains("right_thigh"))
					{
						bodyNode2.Nodes.Add(ReadNode(node2, bodyNode2));
					}
				}
			}
			else if (flag3)
			{
				Console.WriteLine("Shoulder children detected for bone " + daeNode.Name);
				foreach (Node node3 in daeNode.Nodes)
				{
					if (!node3.Name.Contains("l_shoulder") && !node3.Name.Contains("r_shoulder") && !node3.Name.Contains("left_shoulder") && !node3.Name.Contains("right_shoulder"))
					{
						bodyNode2.Nodes.Add(ReadNode(node3, bodyNode2));
					}
				}
				bodyNode2.Nodes.Add(ReadNode(daeNode4, bodyNode2));
				bodyNode2.Nodes.Add(ReadNode(daeNode5, bodyNode2));
			}
			else
			{
				foreach (Node node4 in daeNode.Nodes)
				{
					bodyNode2.Nodes.Add(ReadNode(node4, bodyNode2));
				}
			}
			return bodyNode2;
		}
	}
}
