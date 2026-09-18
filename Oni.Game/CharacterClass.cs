using System;
using System.Collections.Generic;
using System.IO;

namespace Oni.Game
{
	internal class CharacterClass
	{
		public InstanceDescriptor Body;

		public InstanceDescriptor[] Textures;

		public IEnumerable<InstanceDescriptor> Animations;

		public InstanceDescriptor Animation;

		public static CharacterClass Read(InstanceDescriptor descriptor)
		{
			return Read(descriptor, null);
		}

		public static CharacterClass Read(InstanceDescriptor descriptor, string animationName)
		{
			if (descriptor.Template.Tag != TemplateTag.ONCC)
			{
				throw new ArgumentException(string.Format("The specified descriptor has a wrong template {0}", descriptor.Template.Tag), "descriptor");
			}
			CharacterClass characterClass = new CharacterClass();
			InstanceDescriptor instanceDescriptor;
			InstanceDescriptor instanceDescriptor2;
			using (BinaryReader binaryReader = descriptor.OpenRead(3124))
			{
				characterClass.Body = binaryReader.ReadInstance();
				instanceDescriptor = binaryReader.ReadInstance();
				binaryReader.Skip(68);
				instanceDescriptor2 = binaryReader.ReadInstance();
			}
			if (instanceDescriptor != null)
			{
				using (BinaryReader binaryReader2 = instanceDescriptor.OpenRead(22))
				{
					characterClass.Textures = binaryReader2.ReadInstanceArray(binaryReader2.ReadUInt16());
				}
			}
			List<InstanceDescriptor> list = new List<InstanceDescriptor>();
			while (instanceDescriptor2 != null)
			{
				InstanceDescriptor instanceDescriptor3;
				using (BinaryReader binaryReader3 = instanceDescriptor2.OpenRead(16))
				{
					instanceDescriptor3 = binaryReader3.ReadInstance();
					binaryReader3.Skip(2);
					int num = binaryReader3.ReadUInt16();
					for (int i = 0; i < num; i++)
					{
						binaryReader3.Skip(8);
						InstanceDescriptor instanceDescriptor4 = binaryReader3.ReadInstance();
						if (instanceDescriptor4 != null)
						{
							list.Add(instanceDescriptor4);
						}
					}
				}
				instanceDescriptor2 = instanceDescriptor3;
			}
			characterClass.Animations = list;
			if (string.Equals(Path.GetExtension(animationName), ".oni", StringComparison.OrdinalIgnoreCase))
			{
				InstanceFile instanceFile = descriptor.File.FileManager.OpenFile(animationName);
				if (instanceFile != null && instanceFile.Descriptors[0].Template.Tag == TemplateTag.TRAM)
				{
					characterClass.Animation = instanceFile.Descriptors[0];
				}
			}
			else
			{
				if (!string.IsNullOrEmpty(animationName) && !animationName.StartsWith("TRAM", StringComparison.Ordinal))
				{
					animationName = "TRAM" + animationName;
				}
				foreach (InstanceDescriptor item in list)
				{
					using (BinaryReader binaryReader4 = item.OpenRead(346))
					{
						int num2 = binaryReader4.ReadInt16();
						binaryReader4.Skip(2);
						int num3 = binaryReader4.ReadInt16();
						int num4 = binaryReader4.ReadInt16();
						binaryReader4.Skip(6);
						int num5 = binaryReader4.ReadInt16();
						if (!string.IsNullOrEmpty(animationName))
						{
							if (item.FullName == animationName)
							{
								characterClass.Animation = item;
								break;
							}
						}
						else if (num2 == 6 && num3 == 7 && num4 == 7 && num5 == 0)
						{
							characterClass.Animation = item;
							break;
						}
					}
				}
				}
				if (!string.IsNullOrEmpty(animationName) && characterClass.Animation == null)
				{
					throw new InvalidOperationException(string.Format("Animation {0} was not found", animationName));
				}
				return characterClass;
		}
	}
}
