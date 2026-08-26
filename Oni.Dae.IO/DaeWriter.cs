using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Oni.Dae.IO
{
	internal class DaeWriter
	{
		private class Animation : Entity
		{
			public readonly List<Source> Sources = new List<Source>();

			public readonly List<Sampler> Samplers = new List<Sampler>();

			public readonly List<AnimationChannel> Channels = new List<AnimationChannel>();
		}

		private class AnimationChannel
		{
			public readonly Sampler Sampler;

			public readonly string TargetPath;

			public AnimationChannel(Sampler sampler, string targetPath)
			{
				Sampler = sampler;
				TargetPath = targetPath;
			}
		}

		private class AnimationSource : Source
		{
			public readonly string[] Parameters;

			public AnimationSource(float[] data, string[] parameters)
				: base(data, parameters.Length)
			{
				Parameters = parameters;
			}

			public AnimationSource(string[] data, string[] parameters)
				: base(data, parameters.Length)
			{
				Parameters = parameters;
			}
		}

		private class WriteVisitor : Visitor
		{
			private readonly Dictionary<Entity, string> entities = new Dictionary<Entity, string>();

			private readonly Dictionary<string, Entity> ids = new Dictionary<string, Entity>(StringComparer.Ordinal);

			private readonly Dictionary<string, Sampler> samplers = new Dictionary<string, Sampler>(StringComparer.Ordinal);

			private readonly Dictionary<string, Source> sources = new Dictionary<string, Source>(StringComparer.Ordinal);

			private int uniqueEntityId = 1;

			public readonly List<Image> Images = new List<Image>();

			public readonly List<Effect> Effects = new List<Effect>();

			public readonly List<Material> Materials = new List<Material>();

			public readonly List<Geometry> Geometries = new List<Geometry>();

			public readonly List<Scene> Scenes = new List<Scene>();

			public readonly List<Animation> Animations = new List<Animation>();

			public readonly List<Camera> Cameras = new List<Camera>();

			public override void VisitScene(Scene scene)
			{
				AddEntity(scene);
				base.VisitScene(scene);
			}

			public override void VisitNode(Node node)
			{
				EnsureId(node);
				foreach (Transform item in node.Transforms.Where((Transform t) => t.HasAnimations))
				{
					for (int num = 0; num < item.Animations.Length; num++)
					{
						Sampler sampler = item.Animations[num];
						if (sampler != null)
						{
							AddAnimationChannel(sampler, node, item, num);
						}
					}
				}
				base.VisitNode(node);
			}

			public override void VisitGeometry(Geometry geometry)
			{
				AddEntity(geometry);
				string text = IdOf(geometry);
				if (text.EndsWith("_geometry", StringComparison.Ordinal))
				{
					text = text.Substring(0, text.Length - "_geometry".Length);
				}
				foreach (Input vertex in geometry.Vertices)
				{
					EnsureId(vertex.Source, string.Format(CultureInfo.InvariantCulture, "{0}_{1}", new object[2]
					{
						text,
						vertex.Semantic.ToString().ToLowerInvariant()
					}));
				}
				foreach (IndexedInput item in geometry.Primitives.SelectMany((MeshPrimitives p) => p.Inputs))
				{
					EnsureId(item.Source, string.Format(CultureInfo.InvariantCulture, "{0}_{1}", new object[2]
					{
						text,
						item.Semantic.ToString().ToLowerInvariant()
					}));
				}
				base.VisitGeometry(geometry);
			}

			public override void VisitMaterial(Material material)
			{
				AddEntity(material);
				base.VisitMaterial(material);
			}

			public override void VisitEffect(Effect effect)
			{
				AddEntity(effect);
				base.VisitEffect(effect);
			}

			public override void VisitImage(Image image)
			{
				AddEntity(image);
				base.VisitImage(image);
			}

			public override void VisitCamera(Camera camera)
			{
				AddEntity(camera);
				base.VisitCamera(camera);
			}

			private void AddEntity(Scene scene)
			{
				AddEntity(scene, Scenes);
			}

			private void AddEntity(Image image)
			{
				AddEntity(image, Images);
			}

			private void AddEntity(Effect effect)
			{
				AddEntity(effect, Effects);
			}

			private void AddEntity(Material material)
			{
				AddEntity(material, Materials);
			}

			private void AddEntity(Geometry geometry)
			{
				AddEntity(geometry, Geometries);
			}

			private void AddEntity(Animation animation)
			{
				AddEntity(animation, Animations);
			}

			private void AddEntity(Camera camera)
			{
				AddEntity(camera, Cameras);
			}

			private void AddEntity<T>(T entity, ICollection<T> entityCollection) where T : Entity
			{
				if (EnsureId(entity))
				{
					entityCollection.Add(entity);
				}
			}

			private bool EnsureId(Entity entity)
			{
				if (entities.ContainsKey(entity))
				{
					return false;
				}
				string name = entity.Name;
				string text;
				if (string.IsNullOrEmpty(name))
				{
					do
					{
						text = string.Format(CultureInfo.InvariantCulture, "unique_{0}", new object[2]
						{
							uniqueEntityId++,
							entity.GetType().Name.ToLowerInvariant()
						});
					}
					while (ids.ContainsKey(text));
				}
				else if (!ids.ContainsKey(name))
				{
					text = name;
				}
				else
				{
					text = string.Format(CultureInfo.InvariantCulture, "{0}_{1}", new object[2]
					{
						name,
						entity.GetType().Name.ToLowerInvariant()
					});
					while (ids.ContainsKey(text))
					{
						text = string.Format(CultureInfo.InvariantCulture, "{0}_{1}_{2}", new object[3]
						{
							name,
							uniqueEntityId++,
							entity.GetType().Name.ToLowerInvariant()
						});
					}
				}
				entities.Add(entity, text);
				ids.Add(text, entity);
				return true;
			}

			private bool EnsureId(Entity entity, string id)
			{
				if (entities.ContainsKey(entity))
				{
					return false;
				}
				entities.Add(entity, id);
				ids.Add(id, entity);
				return true;
			}

			public string IdOf(Entity entity)
			{
				string value;
				entities.TryGetValue(entity, out value);
				return value;
			}

			public string UrlOf(Entity entity)
			{
				return string.Format("#{0}", IdOf(entity));
			}

			private void AddAnimationChannel(Sampler sampler, Node node, Transform transform, int valueIndex)
			{
				Animation animation;
				if (Animations.Count == 0)
				{
					animation = new Animation();
					Animations.Add(animation);
				}
				else
				{
					animation = Animations[0];
				}
				EnsureId(sampler);
				string text = IdOf(node);
				string text2 = IdOf(sampler);
				string text3 = transform.ValueIndexToValueName(valueIndex);
				Sampler value;
				if (!samplers.TryGetValue(text2 + text3, out value))
				{
					value = new Sampler();
					EnsureId(value, string.Format("{0}_{1}_{2}", IdOf(node), transform.Sid, text3));
					animation.Samplers.Add(value);
					foreach (Input input in sampler.Inputs)
					{
						Source value2 = input.Source;
						EnsureId(value2);
						string key = IdOf(value2) + ((input.Semantic == Semantic.Output) ? text3 : "");
						if (!sources.TryGetValue(key, out value2))
						{
							value2 = input.Source;
							switch (input.Semantic)
							{
							case Semantic.Input:
								value2 = new AnimationSource(value2.FloatData, new string[1] { "TIME" });
								break;
							case Semantic.Output:
								value2 = new AnimationSource(value2.FloatData, new string[1] { text3 });
								break;
							case Semantic.Interpolation:
								value2 = new AnimationSource(value2.NameData, new string[1] { "INTERPOLATION" });
								break;
							case Semantic.InTangent:
							case Semantic.OutTangent:
								value2 = new AnimationSource(value2.FloatData, new string[2] { "X", "Y" });
								break;
							default:
								throw new NotSupportedException(string.Format("Invalid semantic {0} for animation input", input.Semantic));
							}
							sources.Add(key, value2);
							EnsureId(value2, string.Format(CultureInfo.InvariantCulture, "{0}_{1}", new object[2]
							{
								IdOf(value),
								input.Semantic.ToString().ToLowerInvariant()
							}));
							animation.Sources.Add(value2);
						}
						value.Inputs.Add(new Input(input.Semantic, value2));
					}
				}
				animation.Channels.Add(new AnimationChannel(value, string.Format(CultureInfo.InvariantCulture, "{0}/{1}.{2}", new object[3]
				{
					IdOf(node),
					transform.Sid,
					text3
				})));
			}
		}

		private XmlWriter xml;

		private Scene mainScene;

		private WriteVisitor visitor;

		private Dictionary<Source, string> writtenSources = new Dictionary<Source, string>();

		public static void WriteFile(string filePath, Scene scene)
		{
			DaeWriter daeWriter = new DaeWriter();
			daeWriter.visitor = new WriteVisitor();
			daeWriter.visitor.VisitScene(scene);
			daeWriter.mainScene = scene;
			XmlWriterSettings settings = new XmlWriterSettings
			{
				CloseOutput = true,
				ConformanceLevel = ConformanceLevel.Document,
				Encoding = Encoding.UTF8,
				Indent = true,
				IndentChars = "\t"
			};
			if (!scene.CustomAxisConversion && scene.SceneZUP)
			{
				AxisConverter.Convert(scene, Axis.Y, Axis.Z);
			}
			using (FileStream output = File.Create(filePath))
			{
				using (daeWriter.xml = XmlWriter.Create(output, settings))
				{
					daeWriter.WriteRoot();
				}
			}
		}

		private void WriteRoot()
		{
			WriteCollada();
			WriteLibrary("library_cameras", visitor.Cameras, WriteCamera);
			WriteLibrary("library_images", visitor.Images, WriteImage);
			WriteLibrary("library_effects", visitor.Effects, WriteEffect);
			WriteLibrary("library_materials", visitor.Materials, WriteMaterial);
			WriteLibrary("library_geometries", visitor.Geometries, WriteGeometry);
			WriteLibrary("library_visual_scenes", visitor.Scenes, WriteScene);
			WriteLibrary("library_animations", visitor.Animations, WriteAnimation);
			WriteScene();
		}

		private void WriteCollada()
		{
			xml.WriteStartDocument();
			xml.WriteStartElement("COLLADA", "http://www.collada.org/2005/11/COLLADASchema");
			xml.WriteAttributeString("version", "1.4.0");
			xml.WriteStartElement("asset");
			xml.WriteStartElement("contributor");
			xml.WriteElementString("authoring_tool", string.Format(CultureInfo.InvariantCulture, "OniSplit v{0}", new object[1] { Utils.Version }));
			xml.WriteEndElement();
			xml.WriteStartElement("unit");
			xml.WriteAttributeString("meter", "0.1");
			xml.WriteAttributeString("name", "decimeter");
			xml.WriteEndElement();
			if (mainScene.SceneZUP)
			{
				xml.WriteElementString("up_axis", "Z_UP");
			}
			else
			{
				xml.WriteElementString("up_axis", "Y_UP");
			}
			xml.WriteEndElement();
		}

		private void WriteLibrary<T>(string name, ICollection<T> library, Action<T> entityWriter)
		{
			if (library.Count == 0)
			{
				return;
			}
			xml.WriteStartElement(name);
			foreach (T item in library)
			{
				entityWriter(item);
			}
			xml.WriteEndElement();
		}

		private void WriteScene()
		{
			xml.WriteStartElement("scene");
			xml.WriteStartElement("instance_visual_scene");
			xml.WriteAttributeString("url", visitor.UrlOf(mainScene));
			xml.WriteEndElement();
			xml.WriteEndElement();
		}

		private void WriteImage(Image image)
		{
			BeginEntity("image", image);
			string value = ((!Path.IsPathRooted(image.FilePath)) ? image.FilePath.Replace('\\', '/') : ("file:///" + image.FilePath.Replace('\\', '/')));
			xml.WriteElementString("init_from", value);
			EndEntity();
		}

		private void WriteEffect(Effect effect)
		{
			BeginEntity("effect", effect);
			WriteEffectCommonProfile(effect);
			EndEntity();
		}

		private void WriteEffectCommonProfile(Effect effect)
		{
			xml.WriteStartElement("profile_COMMON");
			foreach (EffectParameter parameter in effect.Parameters)
			{
				WriteEffectParameter(parameter);
			}
			WriteEffectTechnique(effect);
			xml.WriteEndElement();
		}

		private void WriteEffectParameter(EffectParameter parameter)
		{
			xml.WriteStartElement("newparam");
			xml.WriteAttributeString("sid", parameter.Sid);
			if (!string.IsNullOrEmpty(parameter.Semantic))
			{
				xml.WriteStartElement("semantic");
				xml.WriteString(parameter.Semantic);
				xml.WriteEndElement();
			}
			if (parameter.Value is float)
			{
				float num = (float)parameter.Value;
				xml.WriteElementString("float", XmlConvert.ToString(num));
			}
			else if (parameter.Value is Vector2)
			{
				Vector2 vector = (Vector2)parameter.Value;
				xml.WriteElementString("float2", string.Format("{0} {1}", XmlConvert.ToString(vector.X), XmlConvert.ToString(vector.Y)));
			}
			else if (parameter.Value is Vector3)
			{
				Vector3 vector2 = (Vector3)parameter.Value;
				xml.WriteElementString("float3", string.Format("{0} {1} {3}", XmlConvert.ToString(vector2.X), XmlConvert.ToString(vector2.Y), XmlConvert.ToString(vector2.Z)));
			}
			else if (parameter.Value is EffectSurface)
			{
				EffectSurface effectSurface = (EffectSurface)parameter.Value;
				xml.WriteStartElement("surface");
				xml.WriteAttributeString("type", "2D");
				xml.WriteElementString("init_from", visitor.IdOf(effectSurface.InitFrom));
				xml.WriteEndElement();
			}
			else if (parameter.Value is EffectSampler)
			{
				EffectSampler effectSampler = (EffectSampler)parameter.Value;
				xml.WriteStartElement("sampler2D");
				xml.WriteStartElement("source");
				xml.WriteString(effectSampler.Surface.DeclaringParameter.Sid);
				xml.WriteEndElement();
				if (effectSampler.MinFilter != EffectSamplerFilter.None)
				{
					xml.WriteElementString("minfilter", effectSampler.MinFilter.ToString().ToUpperInvariant());
				}
				if (effectSampler.MagFilter != EffectSamplerFilter.None)
				{
					xml.WriteElementString("magfilter", effectSampler.MagFilter.ToString().ToUpperInvariant());
				}
				if (effectSampler.MipFilter != EffectSamplerFilter.None)
				{
					xml.WriteElementString("mipfilter", effectSampler.MipFilter.ToString().ToUpperInvariant());
				}
				xml.WriteEndElement();
			}
			xml.WriteEndElement();
		}

		private void WriteEffectTechnique(Effect effect)
		{
			xml.WriteStartElement("technique");
			xml.WriteStartElement("phong");
			WriteEffectTechniqueProperty("ambient", effect.Ambient);
			WriteEffectTechniqueProperty("diffuse", effect.Diffuse);
			WriteEffectTechniqueProperty("specular", effect.Specular);
			WriteEffectTechniqueProperty("transparent", effect.Transparent);
			xml.WriteEndElement();
			xml.WriteEndElement();
		}

		private void WriteEffectTechniqueProperty(string name, EffectParameter value)
		{
			bool flag = name == "transparent";
			if (!flag || value.Value != null)
			{
				xml.WriteStartElement(name);
				if (flag)
				{
					xml.WriteAttributeString("opaque", "A_ONE");
				}
				if (value.Reference != null)
				{
					xml.WriteStartElement("param");
					xml.WriteString(value.Reference);
					xml.WriteEndElement();
				}
				else if (value.Value is float)
				{
					float num = (float)value.Value;
					xml.WriteStartElement("float");
					xml.WriteAttributeString("sid", value.Sid);
					xml.WriteString(XmlConvert.ToString(num));
					xml.WriteEndElement();
				}
				else if (value.Value is Vector4)
				{
					Vector4 vector = (Vector4)value.Value;
					xml.WriteStartElement("color");
					xml.WriteAttributeString("sid", value.Sid);
					xml.WriteString(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", XmlConvert.ToString(vector.X), XmlConvert.ToString(vector.Y), XmlConvert.ToString(vector.Z), XmlConvert.ToString(vector.W)));
					xml.WriteEndElement();
				}
				else if (value.Value is EffectTexture)
				{
					EffectTexture effectTexture = (EffectTexture)value.Value;
					xml.WriteStartElement("texture");
					xml.WriteAttributeString("texture", effectTexture.Sampler.Owner.Sid);
					xml.WriteAttributeString("texcoord", effectTexture.TexCoordSemantic);
					xml.WriteEndElement();
				}
				xml.WriteEndElement();
			}
		}

		private void WriteMaterial(Material matrial)
		{
			BeginEntity("material", matrial);
			xml.WriteStartElement("instance_effect");
			xml.WriteAttributeString("url", visitor.UrlOf(matrial.Effect));
			xml.WriteEndElement();
			EndEntity();
		}

		private void WriteGeometry(Geometry geometry)
		{
			BeginEntity("geometry", geometry);
			xml.WriteStartElement("mesh");
			WriteGeometrySources(geometry);
			WriteGeometryVertices(geometry);
			foreach (MeshPrimitives primitive in geometry.Primitives)
			{
				WriteGeometryPrimitives(geometry, primitive);
			}
			xml.WriteEndElement();
			EndEntity();
		}

		private void WriteGeometrySources(Geometry geometry)
		{
			Dictionary<Source, List<Semantic>> dictionary = new Dictionary<Source, List<Semantic>>();
			foreach (MeshPrimitives primitive in geometry.Primitives)
			{
				foreach (IndexedInput input in primitive.Inputs)
				{
					List<Semantic> value;
					if (!dictionary.TryGetValue(input.Source, out value))
					{
						value = new List<Semantic>();
						dictionary.Add(input.Source, value);
					}
					if (!value.Contains(input.Semantic))
					{
						value.Add(input.Semantic);
					}
				}
			}
			foreach (KeyValuePair<Source, List<Semantic>> item in dictionary)
			{
				foreach (Semantic item2 in item.Value)
				{
					WriteSource(item.Key, item2);
				}
			}
		}

		private void WriteGeometryVertices(Geometry geometry)
		{
			string text = visitor.IdOf(geometry);
			if (text.EndsWith("_geometry", StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - "_geometry".Length);
			}
			xml.WriteStartElement("vertices");
			xml.WriteAttributeString("id", text + "_vertices");
			foreach (Input vertex in geometry.Vertices)
			{
				xml.WriteStartElement("input");
				WriteSemanticAttribute("semantic", vertex.Semantic);
				xml.WriteAttributeString("source", visitor.UrlOf(vertex.Source));
				xml.WriteEndElement();
			}
			xml.WriteEndElement();
		}

		private void WriteGeometryPrimitives(Geometry geometry, MeshPrimitives primitives)
		{
			MeshPrimitiveType primitiveType = primitives.PrimitiveType;
			if ((uint)primitiveType <= 1u || (uint)(primitiveType - 3) <= 1u)
			{
				throw new NotSupportedException(string.Format("Writing {0} is not supported", primitives.PrimitiveType));
			}
			bool flag = !primitives.VertexCounts.Exists((int x) => x != 3);
			if (!flag)
			{
				xml.WriteStartElement("polylist");
			}
			else
			{
				xml.WriteStartElement("triangles");
			}
			xml.WriteAttributeString("count", XmlConvert.ToString(primitives.VertexCounts.Count));
			if (!string.IsNullOrEmpty(primitives.MaterialSymbol))
			{
				xml.WriteAttributeString("material", primitives.MaterialSymbol);
			}
			int num = 0;
			bool flag2 = false;
			List<IndexedInput> list = new List<IndexedInput>();
			string text = visitor.UrlOf(geometry);
			if (text.EndsWith("_geometry", StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - "_geometry".Length);
			}
			foreach (IndexedInput input in primitives.Inputs)
			{
				if (geometry.Vertices.Any((Input x) => x.Source == input.Source))
				{
					if (!flag2)
					{
						list.Add(input);
						xml.WriteStartElement("input");
						xml.WriteAttributeString("semantic", "VERTEX");
						xml.WriteAttributeString("source", text + "_vertices");
						xml.WriteAttributeString("offset", XmlConvert.ToString(num++));
						xml.WriteEndElement();
					}
					flag2 = true;
				}
				else
				{
					list.Add(input);
					xml.WriteStartElement("input");
					WriteSemanticAttribute("semantic", input.Semantic);
					xml.WriteAttributeString("source", visitor.UrlOf(input.Source));
					xml.WriteAttributeString("offset", XmlConvert.ToString(num++));
					if (input.Set != 0)
					{
						xml.WriteAttributeString("set", XmlConvert.ToString(input.Set));
					}
					xml.WriteEndElement();
				}
			}
			if (!flag)
			{
				xml.WriteStartElement("vcount");
				xml.WriteWhitespace("\n");
				int num2 = 0;
				int num3 = 0;
				foreach (int vertexCount in primitives.VertexCounts)
				{
					xml.WriteString(XmlConvert.ToString(vertexCount) + " ");
					num2 += vertexCount;
					num3++;
					if (num3 == 32)
					{
						xml.WriteWhitespace("\n");
						num3 = 0;
					}
				}
				xml.WriteEndElement();
			}
			xml.WriteStartElement("p");
			xml.WriteWhitespace("\n");
			int num4 = 0;
			foreach (int vertexCount2 in primitives.VertexCounts)
			{
				for (int num5 = 0; num5 < vertexCount2; num5++)
				{
					foreach (IndexedInput item in list)
					{
						xml.WriteString(XmlConvert.ToString(item.Indices[num4 + num5]));
						if (item != list.Last() || num5 != vertexCount2 - 1)
						{
							xml.WriteWhitespace(" ");
						}
					}
				}
				xml.WriteWhitespace("\n");
				num4 += vertexCount2;
			}
			xml.WriteEndElement();
			WritePolygonMetadata(primitives);
			xml.WriteEndElement();
		}

		// Writes -getAgqgPerPolygon data as an ignorable COLLADA 1.4 extension.
		private void WritePolygonMetadata(MeshPrimitives primitives)
		{
			if (primitives.PolygonMetadata.Count == 0)
			{
				return;
			}
			if (primitives.PolygonMetadata.Count != primitives.VertexCounts.Count)
			{
				throw new InvalidOperationException("Per-polygon metadata count does not match the primitive polygon count.");
			}
			xml.WriteStartElement("extra");
			xml.WriteStartElement("technique");
			xml.WriteAttributeString("profile", primitives.MetadataProfile);
			xml.WriteStartElement("onisplit", "polygon_metadata", primitives.MetadataNamespace);
			foreach (Dictionary<string, string> metadata in primitives.PolygonMetadata)
			{
				xml.WriteStartElement("onisplit", "polygon", primitives.MetadataNamespace);
				foreach (KeyValuePair<string, string> field in metadata)
				{
					xml.WriteAttributeString(field.Key, field.Value);
				}
				xml.WriteEndElement();
			}
			xml.WriteEndElement();
			xml.WriteEndElement();
			xml.WriteEndElement();
		}

		private void WriteScene(Scene scene)
		{
			BeginEntity("visual_scene", scene);
			foreach (Node node in scene.Nodes)
			{
				WriteSceneNode(node);
			}
			EndEntity();
		}

		private void WriteSceneNode(Node node)
		{
			BeginEntity("node", node);
			foreach (Transform transform in node.Transforms)
			{
				WriteNodeTransform(transform);
			}
			foreach (Instance instance in node.Instances)
			{
				if (instance is GeometryInstance)
				{
					WriteGeometryInstance((GeometryInstance)instance);
				}
				else if (instance is CameraInstance)
				{
					WriteCameraInstance((CameraInstance)instance);
				}
			}
			foreach (Node node2 in node.Nodes)
			{
				WriteSceneNode(node2);
			}
			EndEntity();
		}

		private void WriteNodeTransform(Transform transform)
		{
			string localName = ((transform is TransformTranslate) ? "translate" : ((transform is TransformRotate) ? "rotate" : ((!(transform is TransformScale)) ? "matrix" : "scale")));
			xml.WriteStartElement(localName);
			if (!string.IsNullOrEmpty(transform.Sid))
			{
				xml.WriteAttributeString("sid", transform.Sid);
			}
			StringBuilder stringBuilder = new StringBuilder(transform.Values.Length * 16);
			float[] values = transform.Values;
			foreach (float num in values)
			{
				stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0:f6} ", new object[1] { num });
			}
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Length--;
			}
			xml.WriteValue(stringBuilder.ToString());
			xml.WriteEndElement();
		}

		private void WriteCameraInstance(CameraInstance instance)
		{
			xml.WriteStartElement("instance_camera");
			xml.WriteAttributeString("url", visitor.UrlOf(instance.Target));
			xml.WriteEndElement();
		}

		private void WriteGeometryInstance(GeometryInstance instance)
		{
			xml.WriteStartElement("instance_geometry");
			xml.WriteAttributeString("url", visitor.UrlOf(instance.Target));
			if (instance.Materials.Count > 0)
			{
				xml.WriteStartElement("bind_material");
				xml.WriteStartElement("technique_common");
				foreach (MaterialInstance material in instance.Materials)
				{
					WriteMaterialInstance(material);
				}
				xml.WriteEndElement();
				xml.WriteEndElement();
			}
			xml.WriteEndElement();
		}

		private void WriteMaterialInstance(MaterialInstance matInstance)
		{
			xml.WriteStartElement("instance_material");
			xml.WriteAttributeString("symbol", matInstance.Symbol);
			xml.WriteAttributeString("target", visitor.UrlOf(matInstance.Target));
			foreach (MaterialBinding binding in matInstance.Bindings)
			{
				xml.WriteStartElement("bind_vertex_input");
				xml.WriteAttributeString("semantic", binding.Semantic);
				WriteSemanticAttribute("input_semantic", binding.VertexInput.Semantic);
				xml.WriteAttributeString("input_set", XmlConvert.ToString(binding.VertexInput.Set));
				xml.WriteEndElement();
			}
			xml.WriteEndElement();
		}

		private void WriteAnimation(Animation animation)
		{
			BeginEntity("animation", animation);
			foreach (Source source in animation.Sources)
			{
				WriteSource(source, Semantic.None);
			}
			foreach (Sampler sampler in animation.Samplers)
			{
				WriteAnimationSampler(animation, sampler);
			}
			foreach (AnimationChannel channel in animation.Channels)
			{
				WriteAnimationChannel(channel);
			}
			EndEntity();
		}

		private void WriteAnimationSampler(Animation animation, Sampler sampler)
		{
			xml.WriteStartElement("sampler");
			xml.WriteAttributeString("id", visitor.IdOf(sampler));
			foreach (Input input in sampler.Inputs)
			{
				xml.WriteStartElement("input");
				WriteSemanticAttribute("semantic", input.Semantic);
				xml.WriteAttributeString("source", visitor.UrlOf(input.Source));
				xml.WriteEndElement();
			}
			xml.WriteEndElement();
		}

		private void WriteAnimationChannel(AnimationChannel channel)
		{
			xml.WriteStartElement("channel");
			xml.WriteAttributeString("source", visitor.UrlOf(channel.Sampler));
			xml.WriteAttributeString("target", channel.TargetPath);
			xml.WriteEndElement();
		}

		private void BeginEntity(string name, Entity entity)
		{
			xml.WriteStartElement(name);
			string value = visitor.IdOf(entity);
			if (!string.IsNullOrEmpty(value))
			{
				xml.WriteAttributeString("id", value);
			}
		}

		private void EndEntity()
		{
			xml.WriteEndElement();
		}

		private void WriteSource(Source source, Semantic semantic)
		{
			if (writtenSources.ContainsKey(source))
			{
				return;
			}
			string text = visitor.IdOf(source);
			writtenSources.Add(source, text);
			AnimationSource animationSource = source as AnimationSource;
			if (animationSource != null)
			{
				if (source.FloatData != null)
				{
					WriteSource(text, source.FloatData, XmlConvert.ToString, source.Stride, animationSource.Parameters);
					return;
				}
				WriteSource(text, source.NameData, (string x) => x, source.Stride, animationSource.Parameters);
				return;
			}
			switch (semantic)
			{
			case Semantic.Position:
			case Semantic.Normal:
				WriteSource(text, source.FloatData, XmlConvert.ToString, source.Stride, new string[3] { "X", "Y", "Z" });
				break;
			case Semantic.TexCoord:
				WriteSource(text, source.FloatData, XmlConvert.ToString, source.Stride, new string[2] { "S", "T" });
				break;
			case Semantic.Color:
				if (source.Stride == 4)
				{
					WriteSource(text, source.FloatData, XmlConvert.ToString, source.Stride, new string[4] { "R", "G", "B", "A" });
				}
				else
				{
					WriteSource(text, source.FloatData, XmlConvert.ToString, source.Stride, new string[3] { "R", "G", "B" });
				}
				break;
			case Semantic.Input:
				WriteSource(text, source.FloatData, XmlConvert.ToString, source.Stride, new string[1] { "TIME" });
				break;
			case Semantic.Output:
				WriteSource(text, source.FloatData, XmlConvert.ToString, source.Stride, new string[1] { "VALUE" });
				break;
			case Semantic.Interpolation:
				WriteSource(text, source.NameData, (string x) => x, source.Stride, new string[1] { "INTERPOLATION" });
				break;
			case Semantic.InTangent:
			case Semantic.OutTangent:
				WriteSource(text, source.FloatData, XmlConvert.ToString, source.Stride, new string[2] { "X", "Y" });
				break;
			default:
				throw new NotSupportedException(string.Format("Sources with semantic {0} are not supported", semantic));
			}
		}

		private void WriteSource<T>(string sourceId, T[] data, Func<T, string> toString, int stride, string[] paramNames)
		{
			string text = sourceId + "_array";
			string text2 = null;
			if (typeof(T) == typeof(float))
			{
				text2 = "float";
			}
			else if (typeof(T) == typeof(string))
			{
				text2 = "Name";
			}
			xml.WriteStartElement("source");
			xml.WriteAttributeString("id", sourceId);
			xml.WriteStartElement(text2 + "_array");
			xml.WriteAttributeString("id", text);
			xml.WriteAttributeString("count", XmlConvert.ToString(data.Length));
			xml.WriteWhitespace("\n");
			int num = ((stride == 1) ? 10 : stride);
			for (int i = 0; i < data.Length; i++)
			{
				xml.WriteString(toString(data[i]));
				if (i != data.Length - 1)
				{
					if (i % num == num - 1)
					{
						xml.WriteWhitespace("\n");
					}
					else
					{
						xml.WriteWhitespace(" ");
					}
				}
			}
			xml.WriteEndElement();
			xml.WriteStartElement("technique_common");
			WriteSourceAccessor<T>(text, data.Length / stride, stride, text2, paramNames);
			xml.WriteEndElement();
			xml.WriteEndElement();
		}

		private void WriteSourceAccessor<T>(string arrayId, int count, int stride, string type, string[] paramNames)
		{
			xml.WriteStartElement("accessor");
			xml.WriteAttributeString("source", "#" + arrayId);
			xml.WriteAttributeString("count", XmlConvert.ToString(count));
			xml.WriteAttributeString("stride", XmlConvert.ToString(stride));
			for (int i = 0; i < stride; i++)
			{
				xml.WriteStartElement("param");
				xml.WriteAttributeString("type", type);
				xml.WriteAttributeString("name", paramNames[i]);
				xml.WriteEndElement();
			}
			xml.WriteEndElement();
		}

		private void WriteSemanticAttribute(string name, Semantic semantic)
		{
			xml.WriteAttributeString(name, semantic.ToString().ToUpperInvariant());
		}

		private void WriteCamera(Camera camera)
		{
			BeginEntity("camera", camera);
			xml.WriteStartElement("optics");
			xml.WriteStartElement("technique_common");
			xml.WriteStartElement("perspective");
			xml.WriteElementString("xfov", XmlConvert.ToString(camera.XFov));
			xml.WriteElementString("aspect_ratio", XmlConvert.ToString(camera.AspectRatio));
			xml.WriteElementString("znear", XmlConvert.ToString(camera.ZNear));
			xml.WriteElementString("zfar", XmlConvert.ToString(camera.ZFar));
			xml.WriteEndElement();
			xml.WriteEndElement();
			xml.WriteEndElement();
			EndEntity();
		}
	}
}
