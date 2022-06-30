using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace EchKode.PBMods.InventoryFilter.ConfigBuilder
{
	internal class Program
	{
		private enum PartCategory
		{
			Unknown,
			Body,
			Equip,
			Aux,
			All,
		}

		private class FileFormat
		{
			public string textName;
			public string textDesc;
			public string icon;
			public int priority;
			public List<string> tags;
		}

		private static readonly Dictionary<string, (PartCategory, string)> workshopCategoryMap = new Dictionary<string, (PartCategory, string)>()
		{
			["all"] = (PartCategory.All, "Display all parts"),
			["body"] = (PartCategory.Body, "Display only mech body parts"),
			["item"] = (PartCategory.Equip, "Display only weapons and shields"),
			["utility"] = (PartCategory.Aux, "Display only auxiliary subsystems"),
		};

		static void Main(string[] args)
		{
			if (args.Length < 2)
			{
				Console.WriteLine("Supply the path to the game's Equipment Groups configuration directory and an output director as command line arguments");
				Environment.Exit(1);
			}

			var excludes = new List<string>();
			var specials = new Dictionary<string, string>();

			foreach (var key in ConfigurationManager.AppSettings.AllKeys)
			{
				var value = ConfigurationManager.AppSettings[key];
				if (value == "exclude")
				{
					excludes.Add(key);
				}
				else
				{
					specials.Add(key, value);
				}
			}

			var tags = BuildPartTags(excludes, specials, args[0]);
			BorrowWorkshopCategories(args[0])
				.Select(kvp =>
				{
					if (!tags.TryGetValue(kvp.Key, out var t))
					{
						t = new List<string>();
					}

					return (kvp.Key, new FileFormat()
					{
						textName = kvp.Value.textName,
						textDesc = kvp.Value.textDesc,
						icon = kvp.Value.icon,
						priority = kvp.Value.priority,
						tags = t,
					});
				})
				.ToList()
				.ForEach(x =>
				{
					var pathname = Path.Combine(args[1], $"inventory_{x.Item1.ToString().ToLowerInvariant()}.yaml");
					UtilitiesYAML.SaveToFile(pathname, x.Item2);
				});
		}

		private static Dictionary<PartCategory, List<string>> BuildPartTags(
			List<string> excludes,
			Dictionary<string, string> specials,
			string equipmentGroupPath) =>
				Directory.EnumerateFiles(equipmentGroupPath, "*.yaml")
					.Select(pathname => Path.GetFileNameWithoutExtension(pathname).ToLowerInvariant())
					.Where(key => !excludes.Contains(key))
					.Select(key =>
					{
						if (specials.TryGetValue(key, out var v))
						{
							if (!Enum.TryParse<PartCategory>(v, true, out var category))
							{
								Console.WriteLine("Unknown special value: {0}", v);
								return new
								{
									Category = PartCategory.Unknown,
									Key = string.Empty,
								};
							}

							return new
							{
								Category = category,
								Key = key,
							};
						}

						var parts = key.Split('_');

						if (parts[0] == "subsystem")
						{
							if (parts.Length > 3 && parts[2] == "aux")
							{
								return new
								{
									Category = PartCategory.Aux,
									Key = key,
								};
							}
						}

						if (parts[1] == "spec")
						{
							return new
							{
								Category = PartCategory.Equip,
								Key = key,
							};
						}

						if (parts[1] == "wpn")
						{
							return new
							{
								Category = PartCategory.Equip,
								Key = key,
							};
						}

						if (parts[1] == "body")
						{
							return new
							{
								Category = PartCategory.Body,
								Key = key,
							};
						}

						return new
						{
							Category = PartCategory.Unknown,
							Key = string.Empty,
						};
					})
					.Where(x => x.Category != PartCategory.Unknown)
					.GroupBy(x => x.Category)
					.ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());

		private static string WorkshopCategoriesPath(string equipmentGroupPath) => Path.Combine(equipmentGroupPath, @"..\..\Workshop\Categories");
		private static Dictionary<PartCategory, FileFormat> BorrowWorkshopCategories(string equipmentGroupPath) =>
			Directory.EnumerateFiles(WorkshopCategoriesPath(equipmentGroupPath), "*.yaml")
				.Select(pathname => new
				{
					Key = Path.GetFileNameWithoutExtension(pathname),
					Path = pathname,
				})
				.Select(x =>
				{
					if (workshopCategoryMap.TryGetValue(x.Key, out var category))
					{
						var data = UtilitiesYAML.ReadFromFile<FileFormat>(x.Path, false);
						data.textDesc = category.Item2;
						return new
						{
							Category = category.Item1,
							Data = data,
						};
					}

					return new
					{
						Category = PartCategory.Unknown,
						Data = new FileFormat(),
					};
				})
				.Where(x => x.Category != PartCategory.Unknown)
				.ToDictionary(x => x.Category, x => x.Data);
	}
}
