using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Oni.Dae;
using Oni.Metadata;
using Oni.Xml;

namespace Oni.Totoro
{
	internal class AnimationXmlReader
	{
		private const string ns = "";

		private static readonly char[] emptyChars = new char[0];

		private XmlReader xml;

		private string basePath;

		private Animation animation;

		private AnimationDaeReader daeReader;

		private AnimationXmlReader()
		{
		}

		public static Animation Read(XmlReader xml, string baseDir)
		{
			AnimationXmlReader animationXmlReader = new AnimationXmlReader();
			animationXmlReader.xml = xml;
			animationXmlReader.basePath = baseDir;
			animationXmlReader.animation = new Animation();
			AnimationXmlReader animationXmlReader2 = animationXmlReader;
			Animation animation = animationXmlReader2.Read();
			animation.ValidateFrames();
			return animation;
		}

		private Animation Read()
		{
			animation.Name = xml.GetAttribute("Name");
			xml.ReadStartElement("Animation", "");
			if (xml.IsStartElement("DaeImport") || xml.IsStartElement("Import"))
			{
				ImportDaeAnimation();
			}
			xml.ReadStartElement("Lookup");
			animation.Type = MetaEnum.Parse<AnimationType>(xml.ReadElementContentAsString("Type", ""));
			animation.AimingType = MetaEnum.Parse<AnimationType>(xml.ReadElementContentAsString("AimingType", ""));
			animation.FromState = MetaEnum.Parse<AnimationState>(xml.ReadElementContentAsString("FromState", ""));
			animation.ToState = MetaEnum.Parse<AnimationState>(xml.ReadElementContentAsString("ToState", ""));
			animation.Varient = MetaEnum.Parse<AnimationVarient>(xml.ReadElementContentAsString("Varient", ""));
			animation.FirstLevelAvailable = xml.ReadElementContentAsInt("FirstLevel", "");
			ReadRawArray("Shortcuts", animation.Shortcuts, Read);
			xml.ReadEndElement();
			animation.Flags = MetaEnum.Parse<AnimationFlags>(xml.ReadElementContentAsString("Flags", ""));
			xml.ReadStartElement("Atomic", "");
			animation.AtomicStart = xml.ReadElementContentAsInt("Start", "");
			animation.AtomicEnd = xml.ReadElementContentAsInt("End", "");
			xml.ReadEndElement();
			xml.ReadStartElement("Invulnerable", "");
			animation.InvulnerableStart = xml.ReadElementContentAsInt("Start", "");
			animation.InvulnerableEnd = xml.ReadElementContentAsInt("End", "");
			xml.ReadEndElement();
			xml.ReadStartElement("Overlay", "");
			animation.OverlayUsedBones = MetaEnum.Parse<BoneMask>(xml.ReadElementContentAsString("UsedBones", ""));
			animation.OverlayReplacedBones = MetaEnum.Parse<BoneMask>(xml.ReadElementContentAsString("ReplacedBones", ""));
			xml.ReadEndElement();
			xml.ReadStartElement("DirectAnimations", "");
			animation.DirectAnimations[0] = xml.ReadElementContentAsString("Link", "");
			animation.DirectAnimations[1] = xml.ReadElementContentAsString("Link", "");
			xml.ReadEndElement();
			xml.ReadStartElement("Pause");
			animation.HardPause = xml.ReadElementContentAsInt("Hard", "");
			animation.SoftPause = xml.ReadElementContentAsInt("Soft", "");
			xml.ReadEndElement();
			xml.ReadStartElement("Interpolation", "");
			animation.InterpolationEnd = xml.ReadElementContentAsInt("End", "");
			animation.InterpolationMax = xml.ReadElementContentAsInt("Max", "");
			xml.ReadEndElement();
			animation.FinalRotation = MathHelper.ToRadians(xml.ReadElementContentAsFloat("FinalRotation", ""));
			animation.Direction = MetaEnum.Parse<Direction>(xml.ReadElementContentAsString("Direction", ""));
			animation.Vocalization = xml.ReadElementContentAsInt("Vocalization", "");
			animation.ActionFrame = xml.ReadElementContentAsInt("ActionFrame", "");
			animation.Impact = xml.ReadElementContentAsString("Impact", "");
			ReadRawArray("Particle", animation.Particles, Read);
			ReadRawArray("MotionBlur", animation.MotionBlur, Read);
			ReadRawArray("Footsteps", animation.Footsteps, Read);
			ReadRawArray("Sounds", animation.Sounds, Read);
			if (daeReader == null)
			{
				ReadHeights();
				ReadVelocities();
				ReadRotations();
				ReadPositions();
			}
			ReadThrowInfo();
			ReadRawArray("SelfDamage", animation.SelfDamage, Read);
			if (xml.IsStartElement("Attacks"))
			{
				ReadRawArray("Attacks", animation.Attacks, Read);
				ReadAttackRing();
			}
			xml.ReadEndElement();
			if (daeReader != null)
			{
				daeReader.Read(animation);
			}
			return animation;
		}

		private void ReadVelocities()
		{
			if (xml.IsStartElement("Velocities") && !Utils.SkipEmpty(xml))
			{
				xml.ReadStartElement();
				while (xml.IsStartElement())
				{
					animation.Velocities.Add(Oni.Xml.XmlReaderExtensions.ReadElementContentAsVector2(xml, null));
				}
				xml.ReadEndElement();
			}
		}

		private void ReadPositions()
		{
			Vector2 vector = default(Vector2);
			if (xml.IsStartElement("PositionOffset"))
			{
				xml.ReadStartElement();
				vector.X = xml.ReadElementContentAsFloat("X", "");
				vector.Y = xml.ReadElementContentAsFloat("Z", "");
				xml.ReadEndElement();
			}
			ReadRawArray("Positions", animation.Positions, ReadPosition);
			for (int i = 0; i < animation.Positions.Count; i++)
			{
				Position position = animation.Positions[i];
				position.X = vector.X;
				position.Z = vector.Y;
				vector += animation.Velocities[i];
			}
		}

		private void ReadRotations()
		{
			List<List<KeyFrame>> rotations = animation.Rotations;
			xml.ReadStartElement("Rotations");
			while (xml.IsStartElement())
			{
				xml.ReadStartElement("Bone");
				List<KeyFrame> list = new List<KeyFrame>();
				int num = 0;
				while (xml.IsStartElement())
				{
					string localName = xml.LocalName;
					string[] array = xml.ReadElementContentAsString().Split(emptyChars, StringSplitOptions.RemoveEmptyEntries);
					KeyFrame keyFrame = new KeyFrame();
					keyFrame.Duration = XmlConvert.ToByte(array[0]);
					switch (localName)
					{
					case "EKey":
						animation.FrameSize = 6;
						keyFrame.Rotation.X = XmlConvert.ToSingle(array[1]);
						keyFrame.Rotation.Y = XmlConvert.ToSingle(array[2]);
						keyFrame.Rotation.Z = XmlConvert.ToSingle(array[3]);
						break;
					case "QKey":
						animation.FrameSize = 16;
						keyFrame.Rotation.X = XmlConvert.ToSingle(array[1]);
						keyFrame.Rotation.Y = XmlConvert.ToSingle(array[2]);
						keyFrame.Rotation.Z = XmlConvert.ToSingle(array[3]);
						keyFrame.Rotation.W = 0f - XmlConvert.ToSingle(array[4]);
						break;
					default:
						throw new InvalidDataException(string.Format("Unknown animation key type '{0}'", localName));
					}
					num += keyFrame.Duration;
					list.Add(keyFrame);
				}
				if (num != animation.Velocities.Count)
				{
					throw new InvalidDataException("bad number of frames");
				}
				rotations.Add(list);
				xml.ReadEndElement();
			}
			xml.ReadEndElement();
		}

		private void ReadHeights()
		{
			if (xml.IsStartElement("Heights") && !Utils.SkipEmpty(xml))
			{
				xml.ReadStartElement();
				while (xml.IsStartElement())
				{
					animation.Heights.Add(xml.ReadElementContentAsFloat("Height", ""));
				}
				xml.ReadEndElement();
			}
		}

		private void ReadThrowInfo()
		{
			if (xml.IsStartElement("ThrowSource") && !Utils.SkipEmpty(xml))
			{
				animation.ThrowSource = new ThrowInfo();
				xml.ReadStartElement("ThrowSource");
				xml.ReadStartElement("TargetAdjustment");
				animation.ThrowSource.Position = Oni.Xml.XmlReaderExtensions.ReadElementContentAsVector3(xml, "Position");
				animation.ThrowSource.Angle = xml.ReadElementContentAsFloat("Angle", "");
				xml.ReadEndElement();
				animation.ThrowSource.Distance = xml.ReadElementContentAsFloat("Distance", "");
				animation.ThrowSource.Type = MetaEnum.Parse<AnimationType>(xml.ReadElementContentAsString("TargetType", ""));
				xml.ReadEndElement();
			}
		}

		private void ReadAttackRing()
		{
			if (!xml.IsStartElement("AttackRing") && !xml.IsStartElement("HorizontalExtents"))
			{
				return;
			}
			if (animation.Attacks.Count == 0)
			{
				Console.Error.WriteLine("Warning: AttackRing found but no attacks are present, ignoring");
				xml.Skip();
				return;
			}
			xml.ReadStartElement();
			for (int i = 0; i < 36; i++)
			{
				animation.AttackRing[i] = xml.ReadElementContentAsFloat();
			}
			xml.ReadEndElement();
		}

		private void ReadPosition(Position position)
		{
			xml.ReadStartElement("Position");
			position.Height = xml.ReadElementContentAsFloat("Height", "");
			position.YOffset = xml.ReadElementContentAsFloat("YOffset", "");
			xml.ReadEndElement();
		}

		private void Read(Particle particle)
		{
			xml.ReadStartElement("Particle");
			particle.Start = xml.ReadElementContentAsInt("Start", "");
			particle.End = xml.ReadElementContentAsInt("End", "");
			particle.Bone = MetaEnum.Parse<Bone>(xml.ReadElementContentAsString("Bone", ""));
			particle.Name = xml.ReadElementContentAsString("Name", "");
			xml.ReadEndElement();
		}

		private void Read(Sound sound)
		{
			xml.ReadStartElement("Sound");
			sound.Name = xml.ReadElementContentAsString("Name", "");
			sound.Start = xml.ReadElementContentAsInt("Start", "");
			xml.ReadEndElement();
		}

		private void Read(Shortcut shortcut)
		{
			xml.ReadStartElement("Shortcut");
			shortcut.FromState = MetaEnum.Parse<AnimationState>(xml.ReadElementContentAsString("FromState", ""));
			shortcut.Length = xml.ReadElementContentAsInt("Length", "");
			shortcut.ReplaceAtomic = xml.ReadElementContentAsString("ReplaceAtomic", "") == "yes";
			xml.ReadEndElement();
		}

		private void Read(Footstep footstep)
		{
			xml.ReadStartElement("Footstep");
			string attribute = xml.GetAttribute("Frame");
			if (attribute != null)
			{
				footstep.Frame = XmlConvert.ToInt32(attribute);
				footstep.Type = MetaEnum.Parse<FootstepType>(xml.GetAttribute("Type"));
			}
			else
			{
				footstep.Frame = xml.ReadElementContentAsInt("Frame", "");
				footstep.Type = MetaEnum.Parse<FootstepType>(xml.ReadElementContentAsString("Type", ""));
			}
			xml.ReadEndElement();
		}

		private void Read(Damage damage)
		{
			xml.ReadStartElement("Damage");
			damage.Points = xml.ReadElementContentAsInt("Points", "");
			damage.Frame = xml.ReadElementContentAsInt("Frame", "");
			xml.ReadEndElement();
		}

		private void Read(MotionBlur d)
		{
			xml.ReadStartElement("MotionBlur");
			d.Bones = MetaEnum.Parse<BoneMask>(xml.ReadElementContentAsString("Bones", ""));
			d.Start = xml.ReadElementContentAsInt("Start", "");
			d.End = xml.ReadElementContentAsInt("End", "");
			d.Lifetime = xml.ReadElementContentAsInt("Lifetime", "");
			d.Alpha = xml.ReadElementContentAsInt("Alpha", "");
			d.Interval = xml.ReadElementContentAsInt("Interval", "");
			xml.ReadEndElement();
		}

		private void Read(Attack attack)
		{
			xml.ReadStartElement("Attack");
			attack.Start = xml.ReadElementContentAsInt("Start", "");
			attack.End = xml.ReadElementContentAsInt("End", "");
			attack.Bones = MetaEnum.Parse<BoneMask>(xml.ReadElementContentAsString("Bones", ""));
			attack.Flags = MetaEnum.Parse<AttackFlags>(xml.ReadElementContentAsString("Flags", ""));
			attack.Knockback = xml.ReadElementContentAsFloat("Knockback", "");
			attack.HitPoints = xml.ReadElementContentAsInt("HitPoints", "");
			attack.HitType = MetaEnum.Parse<AnimationType>(xml.ReadElementContentAsString("HitType", ""));
			attack.HitLength = xml.ReadElementContentAsInt("HitLength", "");
			attack.StunLength = xml.ReadElementContentAsInt("StunLength", "");
			attack.StaggerLength = xml.ReadElementContentAsInt("StaggerLength", "");
			if (xml.IsStartElement("Extents"))
			{
				ReadRawArray("Extents", attack.Extents, Read);
				if (attack.Extents.Count != attack.End - attack.Start + 1)
				{
					Console.Error.WriteLine("Error: Attack starting at frame {0} has an incorrect number of extents ({1})", attack.Start, attack.Extents.Count);
				}
			}
			xml.ReadEndElement();
		}

		private void Read(AttackExtent extent)
		{
			xml.ReadStartElement("Extent");
			extent.Angle = xml.ReadElementContentAsFloat("Angle", "");
			extent.Length = xml.ReadElementContentAsFloat("Length", "");
			extent.MinY = xml.ReadElementContentAsFloat("MinY", "");
			extent.MaxY = xml.ReadElementContentAsFloat("MaxY", "");
			xml.ReadEndElement();
		}

		private void ReadRawArray<T>(string name, List<T> list, Action<T> elementReader) where T : new()
		{
			if (!Utils.SkipEmpty(xml))
			{
				xml.ReadStartElement();
				while (xml.IsStartElement())
				{
					T val = new T();
					elementReader(val);
					list.Add(val);
				}
				xml.ReadEndElement();
			}
		}

		private void ImportDaeAnimation()
		{
			string text = xml.GetAttribute("Path");
			bool flag = Utils.SkipEmpty(xml);
			if (!flag)
			{
				xml.ReadStartElement();
				if (text == null)
				{
					text = xml.ReadElementContentAsString("Path", "");
				}
			}
			text = Path.Combine(basePath, text);
			if (!File.Exists(text))
			{
				Console.Error.WriteLine("Could not find animation import source file '{0}'", text);
				return;
			}
			Console.WriteLine("Importing {0}", text);
			daeReader = new AnimationDaeReader();
			daeReader.Scene = Reader.ReadFile(text, false);
			daeReader.StartFrame = int.MinValue;
			daeReader.EndFrame = int.MaxValue;
			if (!flag)
			{
				if (xml.IsStartElement("Start"))
				{
					daeReader.StartFrame = xml.ReadElementContentAsInt("Start", "");
				}
				if (xml.IsStartElement("End"))
				{
					daeReader.EndFrame = xml.ReadElementContentAsInt("End", "");
				}
				xml.ReadEndElement();
			}
			if (daeReader.StartFrame > daeReader.EndFrame)
			{
				int startFrame = daeReader.StartFrame;
				daeReader.StartFrame = daeReader.EndFrame;
				daeReader.EndFrame = startFrame;
				Console.Error.WriteLine("Warning: Start and End frames were in the wrong order; setting range to ({0} - {1})", daeReader.StartFrame, daeReader.EndFrame);
			}
		}
	}
}
