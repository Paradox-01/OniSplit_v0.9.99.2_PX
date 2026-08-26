using System;
using System.Collections.Generic;
using System.IO;
using Oni.Akira;
using Oni.Dae;
using Oni.Game;
using Oni.Motoko;
using Oni.Physics;
using Oni.Totoro;

namespace Oni
{
	internal class DaeExporter : Exporter
	{
		private readonly bool noAnimation;

		private readonly List<string> animationNames = new List<string>();

		private readonly string geometryName;

		private readonly string fileType;

		private readonly bool getVanillaStairs;

		// Enables the additional DAE produced by -getAgqgPerPolygon.
		private readonly bool getAgqgPerPolygon;

		public DaeExporter(string[] args, InstanceFileManager fileManager, string outputDirPath, string fileType, bool getVanillaStairs, bool getAgqgPerPolygon)
			: base(fileManager, outputDirPath)
		{
			this.getVanillaStairs = getVanillaStairs;
			this.getAgqgPerPolygon = getAgqgPerPolygon;
			foreach (string text in args)
			{
				if (text == "-noanim")
				{
					noAnimation = true;
				}
				else if (text.StartsWith("-anim:", StringComparison.Ordinal))
				{
					animationNames.Add(text.Substring(6));
				}
				else if (text.StartsWith("-geom:", StringComparison.Ordinal))
				{
					geometryName = text.Substring(6);
				}
			}
			this.fileType = fileType;
		}

		protected override void ExportFile(string sourceFilePath)
		{
			string extension = Path.GetExtension(sourceFilePath);
			if (string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase))
			{
				SceneExporter sceneExporter = new SceneExporter(base.InstanceFileManager, base.OutputDirPath);
				sceneExporter.ExportScene(sourceFilePath);
			}
			else
			{
				base.ExportFile(sourceFilePath);
			}
		}

		protected override List<InstanceDescriptor> GetSupportedDescriptors(InstanceFile file)
		{
			List<InstanceDescriptor> list = new List<InstanceDescriptor>();
			list.AddRange(file.GetNamedDescriptors(TemplateTag.ONCC));
			list.AddRange(file.GetNamedDescriptors(TemplateTag.TRBS));
			list.AddRange(file.GetNamedDescriptors(TemplateTag.M3GM));
			list.AddRange(file.GetNamedDescriptors(TemplateTag.AKEV));
			list.AddRange(file.GetNamedDescriptors(TemplateTag.OBAN));
			list.AddRange(file.GetNamedDescriptors(TemplateTag.OFGA));
			list.AddRange(file.GetNamedDescriptors(TemplateTag.ONWC));
			return list;
		}

		protected override void ExportInstance(InstanceDescriptor descriptor)
		{
			TemplateTag tag = descriptor.Template.Tag;
			if (tag == TemplateTag.AKEV)
			{
				PolygonMesh mesh = AkiraDatReader.Read(descriptor, getVanillaStairs);
				AkiraDaeWriter.Write(mesh, descriptor.Name, base.OutputDirPath, fileType);
				// Keep the -getAgqgPerPolygon output isolated from the legacy AKEV exports.
				if (getAgqgPerPolygon)
				{
					AkiraAgqgDaeWriter.Write(mesh, descriptor.Name, base.OutputDirPath);
				}
				return;
			}
			Scene scene = new Scene();
			scene.Name = descriptor.Name;
			TextureDaeWriter textureWriter = new TextureDaeWriter(base.OutputDirPath);
			GeometryDaeWriter geometryWriter = new GeometryDaeWriter(textureWriter);
			BodyDaeWriter bodyWriter = new BodyDaeWriter(geometryWriter);
			switch (tag)
			{
			case TemplateTag.OFGA:
				ExportObjectGeometry(descriptor, scene, geometryWriter);
				break;
			case TemplateTag.OBAN:
				ExportObjectAnimation(descriptor, scene, geometryWriter);
				break;
			case TemplateTag.ONCC:
				ExportCharacterBody(descriptor, scene, bodyWriter);
				break;
			case TemplateTag.TRBS:
				ExportCharacterBodySet(descriptor, scene, bodyWriter);
				break;
			case TemplateTag.M3GM:
				ExportGeometry(descriptor, scene, geometryWriter);
				break;
			case TemplateTag.ONWC:
				ExportWeaponGeometry(descriptor, scene, geometryWriter);
				break;
			}
			if (scene.Nodes.Count > 0)
			{
				string filePath = Path.Combine(base.OutputDirPath, descriptor.Name + "." + fileType);
				Writer.WriteFile(filePath, scene);
			}
		}

		private void ExportObjectGeometry(InstanceDescriptor descriptor, Scene scene, GeometryDaeWriter geometryWriter)
		{
			ObjectNode objectNode = ObjectDatReader.ReadObjectGeometry(descriptor);
			Node node = new Node();
			ObjectGeometry[] geometries = objectNode.Geometries;
			foreach (ObjectGeometry objectGeometry in geometries)
			{
				node.Nodes.Add(geometryWriter.WriteNode(objectGeometry.Geometry, objectGeometry.Geometry.Name));
			}
			scene.Nodes.Add(node);
		}

		private void ExportObjectAnimation(InstanceDescriptor descriptor, Scene scene, GeometryDaeWriter geometryWriter)
		{
			ObjectAnimation objectAnimation = ObjectDatReader.ReadAnimation(descriptor);
			Node node;
			if (geometryName == "camera")
			{
				node = new Node
				{
					Name = descriptor.Name + "_camera",
					Instances = { (Instance)new CameraInstance
					{
						Target = new Camera
						{
							XFov = 45f,
							AspectRatio = 1.3333334f,
							ZNear = 1f,
							ZFar = 10000f
						}
					} }
				};
			}
			else if (geometryName != null)
			{
				InstanceFile instanceFile = base.InstanceFileManager.OpenFile(geometryName);
				if (instanceFile == null)
				{
					Console.Error.WriteLine("Cannot fine file {0}", geometryName);
					node = new Node();
				}
				else
				{
					Oni.Motoko.Geometry geometry = GeometryDatReader.Read(instanceFile.Descriptors[0]);
					node = geometryWriter.WriteNode(geometry, geometry.Name);
				}
			}
			else
			{
				node = new Node();
			}
			scene.Nodes.Add(node);
			ExportAnimation(node, new List<ObjectAnimationKey>(objectAnimation.Keys));
		}

		private void ExportGeometry(InstanceDescriptor descriptor, Scene scene, GeometryDaeWriter geometryWriter)
		{
			List<ObjectAnimation> list = new List<ObjectAnimation>(animationNames.Count);
			foreach (string animationName in animationNames)
			{
				InstanceFile instanceFile = base.InstanceFileManager.OpenFile(animationName);
				if (instanceFile == null)
				{
					Console.Error.WriteLine("Cannot find animation {0}", animationName);
				}
				else
				{
					list.Add(ObjectDatReader.ReadAnimation(instanceFile.Descriptors[0]));
				}
			}
			ExportGeometry(scene, geometryWriter, descriptor, list);
		}

		private void ExportCharacterBodySet(InstanceDescriptor descriptor, Scene scene, BodyDaeWriter bodyWriter)
		{
			Body body = BodyDatReader.Read(descriptor);
			Node item = bodyWriter.Write(body, noAnimation, null);
			scene.Nodes.Add(item);
		}

		private void ExportCharacterBody(InstanceDescriptor descriptor, Scene scene, BodyDaeWriter bodyWriter)
		{
			string animationName = ((animationNames.Count > 0) ? animationNames[0] : null);
			CharacterClass characterClass = CharacterClass.Read(descriptor, animationName);
			Body body = BodyDatReader.Read(characterClass.Body);
			InstanceDescriptor[] textures = characterClass.Textures;
			Node node = bodyWriter.Write(body, noAnimation, textures);
			scene.Nodes.Add(node);
			InstanceDescriptor instanceDescriptor = (noAnimation ? null : characterClass.Animation);
			if (instanceDescriptor != null)
			{
				Animation animation = AnimationDatReader.Read(instanceDescriptor);
				AnimationDaeWriter.Write(node, animation);
			}
		}

		private void ExportWeaponGeometry(InstanceDescriptor descriptor, Scene scene, GeometryDaeWriter geometryWriter)
		{
			WeaponClass weaponClass = WeaponClass.Read(descriptor);
			if (weaponClass.Geometry != null)
			{
				ExportGeometry(weaponClass.Geometry, scene, geometryWriter);
			}
		}

		private static void ExportGeometry(Scene scene, GeometryDaeWriter geometryWriter, InstanceDescriptor m3gm, List<ObjectAnimation> animations)
		{
			Oni.Motoko.Geometry geometry = GeometryDatReader.Read(m3gm);
			if (animations != null && animations.Count > 0)
			{
				geometry.HasTransform = true;
				geometry.Transform = Matrix.CreateScale(animations[0].Keys[0].Scale);
			}
			Node node = geometryWriter.WriteNode(geometry, m3gm.Name);
			scene.Nodes.Add(node);
			if (animations == null || animations.Count <= 0)
			{
				return;
			}
			List<ObjectAnimationKey> list = new List<ObjectAnimationKey>();
			int num = 0;
			foreach (ObjectAnimation animation in animations)
			{
				ObjectAnimationKey[] keys = animation.Keys;
				foreach (ObjectAnimationKey objectAnimationKey in keys)
				{
					list.Add(new ObjectAnimationKey
					{
						Translation = objectAnimationKey.Translation,
						Rotation = objectAnimationKey.Rotation,
						Time = objectAnimationKey.Time + num
					});
				}
				num += animation.Length;
			}
			ExportAnimation(node, list);
		}

		private static void ExportAnimation(Node node, List<ObjectAnimationKey> frames)
		{
			float[] array = new float[frames.Count];
			string[] array2 = new string[array.Length];
			Vector3[] positions = new Vector3[frames.Count];
			Vector3[] angles = new Vector3[frames.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = (float)frames[i].Time / 60f;
			}
			for (int j = 0; j < array2.Length; j++)
			{
				array2[j] = "LINEAR";
			}
			for (int k = 0; k < frames.Count; k++)
			{
				positions[k] = frames[k].Translation;
			}
			for (int l = 0; l < frames.Count; l++)
			{
				angles[l] = frames[l].Rotation.ToEulerXYZ();
			}
			TransformTranslate transform = node.Transforms.Translate("translate", positions[0]);
			TransformRotate transform2 = node.Transforms.Rotate("rotX", Vector3.UnitX, angles[0].X);
			TransformRotate transform3 = node.Transforms.Rotate("rotY", Vector3.UnitY, angles[0].Y);
			TransformRotate transform4 = node.Transforms.Rotate("rotZ", Vector3.UnitZ, angles[0].Z);
			WriteSampler(array, array2, (int num) => positions[num].X, transform, "X");
			WriteSampler(array, array2, (int num) => positions[num].Y, transform, "Y");
			WriteSampler(array, array2, (int num) => positions[num].Z, transform, "Z");
			WriteSampler(array, array2, (int num) => angles[num].X, transform2, "ANGLE");
			WriteSampler(array, array2, (int num) => angles[num].Y, transform3, "ANGLE");
			WriteSampler(array, array2, (int num) => angles[num].Z, transform4, "ANGLE");
		}

		private static void WriteSampler(float[] times, string[] interpolations, Func<int, float> getValue, Transform transform, string targetName)
		{
			float[] array = new float[times.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = getValue(i);
			}
			transform.BindAnimation(targetName, new Sampler
			{
				Inputs = 
				{
					new Input(Semantic.Input, new Source(times, 1)),
					new Input(Semantic.Output, new Source(array, 1)),
					new Input(Semantic.Interpolation, new Source(interpolations, 1))
				}
			});
		}
	}
}
