namespace ExBuddyDataMiner
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Reflection;

	using ff14bot.Enums;
	using ff14bot.Managers;

	using SaintCoinach;
	using SaintCoinach.Ex.Relational;
	using SaintCoinach.Text;
	using SaintCoinach.Xiv;

	using Item = ff14bot.Managers.Item;
	using Language = SaintCoinach.Ex.Language;

	internal class Program
	{
		private static readonly MethodInfo Helper = typeof(Program).GetMethod(
			"ActionMethodHelper",
			BindingFlags.Static | BindingFlags.NonPublic);

		private static readonly Dictionary<Type, Dictionary<string, string>> PropertyMap;

		static Program()
		{
			PropertyMap = new Dictionary<Type, Dictionary<string, string>>
							{
								{
									typeof(Item),
									new Dictionary<string, string>
										{
											{ "StatType1", "BaseParam[0]" },
											{ "StatType2", "BaseParam[1]" },
											{ "StatType3", "BaseParam[2]" },
											{ "StatType4", "BaseParam[3]" },
											{ "StatType5", "BaseParam[4]" },
											{ "StatType6", "BaseParam[5]" },
											{ "Stat1", "BaseParamValue[0]" },
											{ "Stat2", "BaseParamValue[1]" },
											{ "Stat3", "BaseParamValue[2]" },
											{ "Stat4", "BaseParamValue[3]" },
											{ "Stat5", "BaseParamValue[4]" },
											{ "Stat6", "BaseParamValue[5]" },
											{ "SetBonusStatType1", "BaseParam{Special}[0]" },
											{ "SetBonusStatType2", "BaseParam{Special}[1]" },
											{ "SetBonusStatType3", "BaseParam{Special}[2]" },
											{ "SetBonusStatType4", "BaseParam{Special}[3]" },
											{ "SetBonusStatType5", "BaseParam{Special}[4]" },
											{ "SetBonusStatType6", "BaseParam{Special}[5]" },
											{ "SetBonusValue1", "BaseParamValue{Special}[0]" },
											{ "SetBonusValue2", "BaseParamValue{Special}[1]" },
											{ "SetBonusValue3", "BaseParamValue{Special}[2]" },
											{ "SetBonusValue4", "BaseParamValue{Special}[3]" },
											{ "SetBonusValue5", "BaseParamValue{Special}[4]" },
											{ "SetBonusValue6", "BaseParamValue{Special}[5]" },
											{ "MagicDamage", "Damage{Mag}" },
											{ "PhysicalDamage", "Damage{Phys}" },
											{ "MagicDefense", "Defense{Mag}" },
											{ "DesynthesisIndex", "Desynthesize" },
											{ "AetherialReductionIndex", "AetherialReduce" },
											{ "Defense", "Defense{Phys}" },
											{ "BlockStrength", "Block" },
											{ "GilBase", "Price{Low}" },
											{ "GilModifier", "GCTurnIn" },
											{ "RepairItemId", "Item{Repair}" },
											{ "EngName", null },
											{ "FreName", null },
											{ "GerName", null },
											{ "JapName", null },
											{ "Id", null }
										}
								}
							};
		}

		private static void LoadLocalizeable<TRBType, TSCType>(Database database, ARealmReversed realm) where TRBType : LocalizeableResult, new() where TSCType : XivRow, IQuantifiableXivString
		{
			var typeInfo = typeof(TRBType);
			var xivSheetType = typeof(XivSheet<TSCType>);
			var propertyInfos =
				typeInfo.GetProperties(BindingFlags.Instance | BindingFlags.Public)
					.Where(p => p.GetSetMethod() != null)
					.Select(p => new
					{
						Info = p,
						Invocation = (dynamic)Helper.MakeGenericMethod(typeInfo, p.PropertyType).Invoke(null, new object[] { p.SetMethod })
					})
					.ToArray();

			var xivItems = realm.GameData.GetSheet<TSCType>();
			var relationalMultiSheet =
				xivSheetType.GetField("_Source", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(xivItems) as
				IRelationalMultiSheet;
			var items = new List<TRBType>(xivItems.Count);

			Dictionary<string, string> dictionary;
			if (!PropertyMap.TryGetValue(typeof(TRBType), out dictionary))
			{
				dictionary = new Dictionary<string, string>();
			}

			for (var i = 0; i < xivItems.Count; i++)
			{
				var xivItem = xivItems[i];
				var relationalMultiRow = relationalMultiSheet[i];
				var item = new TRBType
				{
					Id = (uint)xivItem.Key,
					EngName = xivItem.Singular,
					FreName = relationalMultiRow.GetRaw("Singular", Language.French) as XivString,
					GerName = relationalMultiRow.GetRaw("Singular", Language.German) as XivString,
					JapName = relationalMultiRow.GetRaw("Singular", Language.Japanese) as XivString
				};

				foreach (var property in propertyInfos)
				{
					var propertyInfo = property.Info;
					string propertyName = dictionary.TryGetValue(propertyInfo.Name, out propertyName)
											? propertyName
											: propertyInfo.Name;

					if (propertyName == null)
					{
						continue;
					}

					try
					{
						var xivValue = ((IRelationalRow)xivItem).GetRaw(propertyName);

						if (xivValue.GetType() != propertyInfo.PropertyType)
						{
							if (propertyInfo.PropertyType.IsEnum)
							{
								xivValue = Enum.ToObject(propertyInfo.PropertyType, xivValue);
							}
							else
							{
								var convertible = xivValue as IConvertible;
								if (convertible != null)
								{
									xivValue = convertible.ToType(propertyInfo.PropertyType, null);
								}
							}
						}

						property.Invocation.Invoke(item, xivValue);
						//propertyInfo.SetValue(item, xivValue);
					}
					catch (Exception ex)
					{
						//Console.WriteLine(propertyName + " does not match");
					}
				}

				items.Add(item);
			}

			database.InsertAll(items);
		}

		// idk if this works for anything...
		private static void Load<TRBType, TSCType>(Database database, ARealmReversed realm) where TRBType : LocalizeableResult, new() where TSCType : IXivRow
		{
			var typeInfo = typeof(TRBType);
			var propertyInfos =
				typeInfo.GetProperties(BindingFlags.Instance | BindingFlags.Public)
					.Where(p => p.GetSetMethod() != null)
					.Select(p => new
					{
						Info = p,
						Invocation = (dynamic)Helper.MakeGenericMethod(typeInfo, p.PropertyType).Invoke(null, new object[] { p.SetMethod })
					})
					.ToArray();

			var xivItems = realm.GameData.GetSheet<TSCType>();
			var items = new List<TRBType>(xivItems.Count);

			Dictionary<string, string> dictionary;
			if (!PropertyMap.TryGetValue(typeof(TRBType), out dictionary))
			{
				dictionary = new Dictionary<string, string>();
			}

			for (var i = 0; i < xivItems.Count; i++)
			{
				var xivItem = xivItems[i];
				var item = new TRBType
				{
					Id = (uint)xivItem.Key,
				};

				foreach (var property in propertyInfos)
				{
					var propertyInfo = property.Info;
					string propertyName = dictionary.TryGetValue(propertyInfo.Name, out propertyName)
											? propertyName
											: propertyInfo.Name;

					if (propertyName == null)
					{
						continue;
					}

					try
					{
						var xivValue = ((IRelationalRow)xivItem).GetRaw(propertyName);

						if (xivValue.GetType() != propertyInfo.PropertyType)
						{
							if (propertyInfo.PropertyType.IsEnum)
							{
								xivValue = Enum.ToObject(propertyInfo.PropertyType, xivValue);
							}
							else
							{
								var convertible = xivValue as IConvertible;
								if (convertible != null)
								{
									xivValue = convertible.ToType(propertyInfo.PropertyType, null);
								}
							}
						}

						property.Invocation.Invoke(item, xivValue);
						//propertyInfo.SetValue(item, xivValue);
					}
					catch (Exception ex)
					{
						//Console.WriteLine(propertyName + " does not match");
					}
				}

				items.Add(item);
			}

			database.InsertAll(items);
		}

		// ReSharper disable once UnusedMember.Local
		static Action<TTarget, object> ActionMethodHelper<TTarget, TParam>(MethodInfo method) where TTarget : class
		{
			var func = (Action<TTarget, TParam>)Delegate.CreateDelegate
				(typeof(Action<TTarget, TParam>), method);

			Action<TTarget, object> ret = (target, param) => func(target, (TParam)param);
			return ret;
		}

		private static void LoadMSD(Database database, Database rbdb, ARealmReversed realm)
		{
			var xivItems =
				realm.GameData.GetSheet<MasterpieceSupplyDuty>().Where(x => x.ClassJob.Key != 0).ToList();
			var items = new List<MasterpieceSupplyDutyResult>(xivItems.Count);
			var requiredItems = new List<RequiredItemResult>(64);
			var rbItems = rbdb.Table<Item>().ToArray();

			for (var i = 0; i < xivItems.Count; i++)
			{
				var xivItem = xivItems[i];

				var item = new MasterpieceSupplyDutyResult
				{
					Id = (uint)xivItem.Key,
					Index = (uint) (xivItems.Count - xivItem.Key)

				};

				item.ClassJob = (ClassJobType)xivItem.ClassJob.Key;
				item.ItemLevel = xivItem.ItemLevel;
				item.RewardItemId = (uint)xivItem.RewardItem.Key;

				foreach (var ci in xivItem.CollectableItems)
				{
					if (ci.RequiredItem.Key == 0)
					{
						continue;
					}

					var rbItem = rbItems[ci.RequiredItem.Key];

					var requiredItem = new RequiredItemResult
					{
						Id = rbItem.Id,
						ChnName = rbItem.ChnName,
						EngName = rbItem.EngName,
						FreName = rbItem.FreName,
						JapName = rbItem.JapName,
						GerName = rbItem.GerName,
						MasterpieceSupplyDutyResultId = item.Id
					};

					requiredItems.Add(requiredItem);
				}

				items.Add(item);
			}

			database.InsertAll(items);
			database.InsertAll(requiredItems);

			var result = database.Query<MasterpieceSupplyDutyResult>(@"
select m.*
from MasterpieceSupplyDutyResult m
join RequiredItemResult r on m.Id = r.MasterpieceSupplyDutyResultId
where r.Id = ?", 5108).SingleOrDefault();

		}

		private static void Main(string[] args)
		{
			var programDir =
				Environment.GetFolderPath(
					Environment.Is64BitProcess ? Environment.SpecialFolder.ProgramFilesX86 : Environment.SpecialFolder.ProgramFiles);

			var dataPath = Path.Combine(programDir, "SquareEnix", "FINAL FANTASY XIV - A Realm Reborn");
			var realm = new ARealmReversed(dataPath, @"SaintCoinach.History.zip", Language.English);
			//realm.Packs.GetPack(new SaintCoinach.IO.PackIdentifier("exd", SaintCoinach.IO.PackIdentifier.DefaultExpansion, 0)).KeepInMemory = true;
			File.Delete("ExBuddy.s3db");

			Database database = null;
			Database rbdb = null;
			try
			{
				database = new Database("ExBuddy.s3db", true);
				rbdb = new Database("db.s3db");

				LoadMSD(database, rbdb, realm);
			}
			finally
			{
				database?.Dispose();
				rbdb?.Dispose();
			}
		}
	}
}
