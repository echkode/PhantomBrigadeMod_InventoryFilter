// Copyright (c) 2022 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using HarmonyLib;
using PhantomBrigade.Data;
using UnityEngine;

namespace EchKode.PBMods.InventoryFilter
{
	using PB_IDUtility = PhantomBrigade.IDUtility;
	using EntryIdMap = Dictionary<int, CIHelperEquipmentSelector>;
	using EntryKeyMap = Dictionary<string, CIHelperEquipmentSelector>;
	using EquipmentSelectorList = List<CIHelperEquipmentSelector>;
	using LiveryList = List<CIHelperEquipmentLivery>;

	static class CategoryList
	{
		private enum ItemEntryKind
		{
			Part,
			Subsystem,
		}

		private enum SortMode
		{
			Level,
			Quality,
			Group,
		}

		private enum SortOrder
		{
			Ascending,
			Descending,
		}

		private class Accessors
		{
			public MethodInfo SpriteMethod;
			public FieldInfo PartsList;
			public FieldInfo SubsystemsList;
			public FieldInfo EntitiesFiltered;
			public FieldInfo SocketLast;
			public FieldInfo HardpointLast;
			public MethodInfo ResetEquipmentSelection;
			public MethodInfo RemoveLastSelectionIndex;
			public FieldInfo EntriesEquipmentByID;
			public FieldInfo EntriesEquipmentByKey;
			public MethodInfo OnPartHoverStart;
			public MethodInfo OnPartHoverEnd;
			public MethodInfo OnSubsystemHoverStart;
			public MethodInfo OnSubsystemHoverEnd;
			public MethodInfo ConfigurePartEntry;
			public MethodInfo ConfigureSubsystemEntry;
			public MethodInfo OnPartSelectionIsolated;
			public MethodInfo OnSubsystemSelectionIsolated;
			public FieldInfo EquipmentSelectorPool;
			public FieldInfo SortingModeLast;
			public FieldInfo SortOrderAscending;
			public FieldInfo LiverySelectorPool;
		}

		private class CategoryInfo
		{
			public DataContainerWorkshopCategory DataContainer;
			public bool Display;
		}

		private class ItemEntryInfo
		{
			public System.Func<EquipmentEntity, int, string> MakeName;
			public System.Action<EquipmentEntity, CIHelperEquipmentSelector> ConfigureEntry;
			public System.Action<int> OnHoverStart;
			public System.Action<int> OnHoverEnd;
			public System.Action<int> OnSelectionIsolated;
		}

		private const string categoryKeyAll = "all";

		internal static bool deferredNavAll;

		private static Accessors access;

		private static GameObject categoryHolder;
		private static Dictionary<string, CIButton> categoryButtons;
		private static bool isCategoryHolderPositioned;

		private static Dictionary<string, CategoryInfo> categories;

		private static int buttonHeight;
		private static int buttonWidth;

		private static List<EquipmentEntity> partsList;
		private static List<EquipmentEntity> subsystemsList;
		private static List<EquipmentEntity> entitiesFiltered;

		private static bool selectedByNav;

		internal static void AddCategories()
		{
			if (null != categoryHolder)
			{
				return;
			}

			access = new Accessors()
			{
				SpriteMethod = AccessTools.Method(typeof(CIBase), "TryGetSprite"),
				PartsList = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "partsLast"),
				SubsystemsList = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "subsystemsLast"),
				EntitiesFiltered = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "entitiesFiltered"),
				SocketLast = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "socketLast"),
				HardpointLast = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "hardpointLast"),
				ResetEquipmentSelection = AccessTools.Method(typeof(CIViewBaseCustomizationSelector), "ResetEquipmentSelection"),
				RemoveLastSelectionIndex = AccessTools.Method(typeof(CIViewBaseCustomizationSelector), "RemoveLastSelectionIndex"),
				EntriesEquipmentByID = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "entriesEquipmentByID"),
				EntriesEquipmentByKey = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "entriesEquipmentByKey"),
				OnPartHoverStart = AccessTools.Method(typeof(CIViewBaseCustomizationSelector), "OnPartHoverStart"),
				OnPartHoverEnd = AccessTools.Method(typeof(CIViewBaseCustomizationSelector), "OnPartHoverEnd"),
				OnSubsystemHoverStart = AccessTools.Method(typeof(CIViewBaseCustomizationSelector), "OnSubsystemHoverStart"),
				OnSubsystemHoverEnd = AccessTools.Method(typeof(CIViewBaseCustomizationSelector), "OnSubsystemHoverEnd"),
				ConfigurePartEntry = AccessTools.Method(typeof(CIViewBaseCustomizationSelector), "ConfigurePartEntry"),
				ConfigureSubsystemEntry = AccessTools.Method(typeof(CIViewBaseCustomizationSelector), "ConfigureSubsystemEntry"),
				OnPartSelectionIsolated = AccessTools.Method(typeof(CIViewBaseCustomizationSelector), "OnPartSelectionIsolated"),
				OnSubsystemSelectionIsolated = AccessTools.Method(typeof(CIViewBaseCustomizationSelector), "OnSubsystemSelectionIsolated"),
				EquipmentSelectorPool = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "equipmentSelectorPool"),
				SortingModeLast = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "sortingModeLast"),
				SortOrderAscending = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "sortOrderAscending"),
				LiverySelectorPool = AccessTools.Field(typeof(CIViewBaseCustomizationSelector), "liverySelectorPool"),
			};

			InitializeCategoryFields();

			var modConfigsPath = Path.Combine(ModLink.modPath, "Configs", "Inventory");
			foreach (var pathname in Directory.EnumerateFiles(modConfigsPath, "*.yaml"))
			{
				LoadButton(pathname);
			}

			GetReferencesToEquipmentLists();
		}

		private static void InitializeCategoryFields()
		{
			categoryHolder = Object.Instantiate(
				CIViewBaseWorkshopV2.ins.categoryHolder.gameObject,
				CIViewBaseInventory.ins.transform);
			UtilityGameObjects.ClearChildren(categoryHolder);
			categoryButtons = new Dictionary<string, CIButton>();
			categories = new Dictionary<string, CategoryInfo>();
		}

		private static void LoadButton(string pathname)
		{
			// Cribbed with small modifications from CIViewBaseWorkshopV2.Start().

			var key = Path.GetFileNameWithoutExtension(pathname).ToLowerInvariant().Substring("inventory_".Length);
			var inventoryCategory = UtilitiesYAML.ReadFromFile<DataContainerWorkshopCategory>(pathname, false);
			inventoryCategory.OnAfterDeserialization(key);
			categories.Add(key, new CategoryInfo() { DataContainer = inventoryCategory });

			var uiObject = UIHelper.CreateUIObject(CIViewBaseWorkshopV2.ins.categoryButtonPrefab, categoryHolder.transform);
			uiObject.name = inventoryCategory.key;
			uiObject.callbackOnClick = new UICallback(OnCategorySelected, key);
			uiObject.callbackOnClickSecondary = new UICallback(OnSwitchFilterUI, key);
			uiObject.tooltipUsed = true;
			uiObject.tooltipHeader = inventoryCategory.textName;
			uiObject.tooltipContent = inventoryCategory.textDesc;

			var sprite = (UISprite)access.SpriteMethod.Invoke(uiObject, new object[] { 0 });
			if (sprite != null)
			{
				sprite.enabled = true;
				sprite.spriteName = inventoryCategory.icon;
				sprite.MakePixelPerfect();
			}

			var (ww, wh) = uiObject.elements
				.Where(el => el.widget != null)
				.Select(el => el.widget)
				.Aggregate((0, 0), (acc, w) => (Mathf.Max(acc.Item1, w.width), Mathf.Max(acc.Item2, w.height)));
			buttonHeight = Mathf.Max(buttonHeight, wh);
			buttonWidth = Mathf.Max(buttonWidth, ww);

			categoryButtons.Add(key, uiObject);
		}

		private static void GetReferencesToEquipmentLists()
		{
			partsList = (List<EquipmentEntity>)access.PartsList.GetValue(CIViewBaseCustomizationSelector.ins);
			subsystemsList = (List<EquipmentEntity>)access.SubsystemsList.GetValue(CIViewBaseCustomizationSelector.ins);
			entitiesFiltered = (List<EquipmentEntity>)access.EntitiesFiltered.GetValue(CIViewBaseCustomizationSelector.ins);
		}

		private static void OnCategorySelected(object arg)
		{
			if (!(arg is string nameInternal))
			{
				return;
			}

			if (!categories.TryGetValue(nameInternal, out var category))
			{
				return;
			}

			foreach (var kvp in categoryButtons)
			{
				if (kvp.Value.gameObject.activeSelf)
				{
					kvp.Value.available = kvp.Key != nameInternal;
				}
			}

			if (selectedByNav)
			{
				selectedByNav = false;
				return;
			}

			FilterItemsByCategory(category.DataContainer);
		}

		private static void OnSwitchFilterUI(object arg)
		{
			OnCategorySelected(arg);
			// XXX swap out category list for filter list
		}

		internal static void Enable()
		{
			if (null == categoryHolder)
			{
				return;
			}

			FileLog.Log("!!! PBMods enabling inventory category list");

			SetHolderPosition();
			UpdateCategoryDisplay();
			if (partsList.Any() || subsystemsList.Any())
			{
				ShowList();
				categoryButtons[categoryKeyAll].available = false;
			}
			else
			{
				HideList();
			}
		}

		private static void ShowList()
		{
			FileLog.Log("!!! PBMods showing category list");
			categoryHolder.SetActive(true);
			ShowButtons();
		}

		private static void SetHolderPosition()
		{
			if (isCategoryHolderPositioned)
			{
				// This code has a timing bug where the category list will get positioned under
				// the item list if the inventory screen is entered directly from the unit socket
				// edit screen. The CIViewBaseCustomizationSelector UI is repositioned during an
				// animation on exiting the unit socket edit screen. However, this function is
				// entered before the end of the animation so it looks like the category list has
				// disappeared. What's happened is that it's been positioned behind the item list.
				// These are fairly static screens so it's good enough to just cache the first
				// calculated position.

				return;
			}

			var o = CIViewBaseCustomizationSelector.ins.spriteBackgroundMain;
			var t = o.transform;
			var p = t.localPosition + new Vector3(-o.width, o.height, 0);
			var w = t.parent.TransformPoint(p);
			var pos = categoryHolder.transform.parent.InverseTransformPoint(w);
			categoryHolder.transform.localPosition = new Vector3(
				pos.x + buttonWidth * 3 / 4,
				pos.y - buttonHeight / 2,
				categoryHolder.transform.localPosition.z);

			isCategoryHolderPositioned = true;
		}

		private static void UpdateCategoryDisplay()
		{
			var equipmentTags = GetEquipmentTags();
			foreach (var kvp in categories)
			{
				if (kvp.Value.DataContainer.priority == 0)
				{
					kvp.Value.Display = equipmentTags.Any();
				}
				else
				{
					kvp.Value.Display = kvp.Value.DataContainer.tags.Overlaps(equipmentTags);
				}
				FileLog.Log($"!!! PBMods category: key={kvp.Key}; display={kvp.Value.Display}");
			}
		}

		private static void ShowButtons()
		{
			var orderedCategories = categories.OrderBy(kvp => kvp.Value.DataContainer.priority).ToList();
			var candidates = orderedCategories
				.Skip(1)
				.Select(kvp => new
				{
					kvp.Key,
					Active = kvp.Value.Display,
					Button = categoryButtons[kvp.Key],
				})
				.Prepend(new
				{
					orderedCategories.First().Key,
					Active = true,
					Button = categoryButtons[orderedCategories.First().Key],
				})
				.ToList();
			var i = 0;

			foreach (var candidate in candidates)
			{
				if (candidate.Active)
				{
					candidate.Button.transform.localPosition = new Vector3(0.0f, i * -buttonHeight, 0.0f);
					candidate.Button.available = true;
					i += 1;
				}
				candidate.Button.gameObject.SetActive(candidate.Active);
				categories[candidate.Key].Display = candidate.Active;
			}
		}

		private static HashSet<string> GetEquipmentTags() =>
			new HashSet<string>(partsList
				.Where(CanPartBeDisplayed)
				.Where(HasGroupTags)
				.SelectMany(part => part.dataLinkPartPreset.data.groupFilterKeys)
				.Concat(subsystemsList
					.Where(CanSubsystemBeDisplayed)
					.Where(HasGroupTags)
					.SelectMany(subsystem => subsystem.dataLinkSubsystem.data.groupFilterKeys)));

		private static bool CanPartBeDisplayed(EquipmentEntity equipmentEntity)
		{
			if (!equipmentEntity.isEnabled)
			{
				return false;
			}
			
			if (!equipmentEntity.isPart)
			{
				return false;
			}
			
			if (!equipmentEntity.hasDataLinkPartPreset)
			{
				return false;
			}

			return true;
		}

		private static bool HasGroupTags(EquipmentEntity equipmentEntity)
		{
			if (equipmentEntity.hasDataLinkPartPreset)
			{
				var data = equipmentEntity.dataLinkPartPreset.data;
				return data.groupFilterKeys != null && data.groupFilterKeys.Count != 0;
			}

			if (equipmentEntity.hasDataLinkSubsystem)
			{
				var data = equipmentEntity.dataLinkSubsystem.data;
				return data.groupFilterKeys != null && data.groupFilterKeys.Count != 0;
			}

			return false;
		}

		private static bool CanSubsystemBeDisplayed(EquipmentEntity equipmentEntity)
		{
			if (!equipmentEntity.isEnabled)
			{
				return false;
			}

			if (!equipmentEntity.isSubsystem)
			{
				return false;
			}

			if (!equipmentEntity.hasDataLinkSubsystem)
			{
				return false;
			}

			return true;
		}

		private static void HideList()
		{
			FileLog.Log("!!! PBMods hiding category list");
			foreach (var kvp in categoryButtons)
			{
				kvp.Value.available = false;
				kvp.Value.gameObject.SetActive(false);
			}
			categoryHolder.SetActive(false);
		}

		private static void FilterItemsByCategory(DataContainerWorkshopCategory category)
		{
			var (byId, byKey) = PrepareItemList();
			entitiesFiltered.Clear();
			if (category.key == categoryKeyAll)
			{
				AddAllEntities();
			}
			else
			{
				AddCategoryEntities(category);
			}
			FillItemList(byId, byKey);
		}

		private static void AddAllEntities()
		{
			foreach (var equipmentEntity in partsList.Concat(subsystemsList))
			{
				if (!equipmentEntity.isEnabled)
				{
					continue;
				}

				if (equipmentEntity.isPart && equipmentEntity.hasDataLinkPartPreset)
				{
					entitiesFiltered.Add(equipmentEntity);
				}
				else if (equipmentEntity.isSubsystem && equipmentEntity.hasDataLinkSubsystem)
				{
					entitiesFiltered.Add(equipmentEntity);
				}
			}
		}

		private static void AddCategoryEntities(DataContainerWorkshopCategory category)
		{
			foreach (var equipmentEntity in partsList)
			{
				if (CanPartBeDisplayed(equipmentEntity))
				{
					var data = equipmentEntity.dataLinkPartPreset.data;
					if (data.groupFilterKeys != null && data.groupFilterKeys.Count != 0 && category.tags.Overlaps(data.groupFilterKeys))
					{
						entitiesFiltered.Add(equipmentEntity);
					}
				}
			}

			foreach (var equipmentEntity in subsystemsList)
			{
				if (CanSubsystemBeDisplayed(equipmentEntity))
				{
					var data = equipmentEntity.dataLinkSubsystem.data;
					if (data.groupFilterKeys != null && data.groupFilterKeys.Count != 0 && category.tags.Overlaps(data.groupFilterKeys))
					{
						entitiesFiltered.Add(equipmentEntity);
					}
				}
			}
		}

		private static (EntryIdMap, EntryKeyMap) PrepareItemList()
		{
			var entriesEquipmentByID = (EntryIdMap)access.EntriesEquipmentByID.GetValue(CIViewBaseCustomizationSelector.ins);
			var entriesEquipmentByKey = (EntryKeyMap)access.EntriesEquipmentByKey.GetValue(CIViewBaseCustomizationSelector.ins);

			access.RemoveLastSelectionIndex.Invoke(CIViewBaseCustomizationSelector.ins, null);
			entriesEquipmentByID.Clear();
			entriesEquipmentByKey.Clear();
			CIViewBaseCustomizationSelector.ins.scrollView.ClearEntries(false);

			return (entriesEquipmentByID, entriesEquipmentByKey);
		}

		private static void FillItemList(EntryIdMap entriesEquipmentByID, EntryKeyMap entriesEquipmentByKey)
		{
			var selector = CIViewBaseCustomizationSelector.ins;
			var equipmentSelectorPool = (EquipmentSelectorList)access.EquipmentSelectorPool.GetValue(selector);
			var liverySelectorPool = (LiveryList)access.LiverySelectorPool.GetValue(selector);
			var sortingMode = ((CIViewBaseCustomizationSelector.SortingMode)access.SortingModeLast.GetValue(selector)).key;
			var sortAscending = (bool)access.SortOrderAscending.GetValue(selector);
			var sorter = GetSorter(sortingMode, sortAscending);

			entitiesFiltered.Sort(new System.Comparison<EquipmentEntity>(sorter));
			var poolIndex = 0;
			var poolCount = equipmentSelectorPool.Count;
			for (var i = 0; i < entitiesFiltered.Count; i += 1)
			{
				var equipmentEntity = entitiesFiltered[i];
				int id = equipmentEntity.id.id;
				CIHelperEquipmentSelector component;
				CIScrollViewElementSimple viewElementSimple;
				if (poolIndex < poolCount)
				{
					component = equipmentSelectorPool[poolIndex];
					viewElementSimple = component.scrollElement;
					viewElementSimple.gameObject.SetActive(true);
					poolIndex += 1;
				}
				else
				{
					viewElementSimple = (CIScrollViewElementSimple)UIHelper.CreateUIObject(selector.scrollView.entryPrefab, selector.scrollView.panel.transform);
					component = viewElementSimple.GetComponent<CIHelperEquipmentSelector>();
					equipmentSelectorPool.Add(component);
				}
				selector.scrollView.entries.Add(viewElementSimple);
				entriesEquipmentByID.Add(id, component);
				entriesEquipmentByKey.Add(equipmentEntity.nameInternal.s, component);
				var entryInfo = GetEntryInfo(equipmentEntity);
				entryInfo.ConfigureEntry(equipmentEntity, component);
				viewElementSimple.gameObject.name = entryInfo.MakeName(equipmentEntity, i);
				viewElementSimple.callbackOnHoverStart = new UICallback(entryInfo.OnHoverStart, id);
				viewElementSimple.callbackOnHoverEnd = new UICallback(entryInfo.OnHoverEnd, id);
				if (selector.inventory.intelLevel == CIViewBaseCustomizationInfo.IntelLevel.Full)
				{
					viewElementSimple.callbackOnConfirm = new UICallback(entryInfo.OnSelectionIsolated, id);
				}
				viewElementSimple.callbackOnConfirmLong = null;
				viewElementSimple.buttonBody.longPressUsed = false;
			}

			var entryCount = selector.scrollView.entries.Count;
			for (var i = equipmentSelectorPool.Count - 1; i >= entryCount; i -= 1)
			{
				equipmentSelectorPool[i].scrollElement.gameObject.SetActive(false);
			}
			for (var i = 0; i < liverySelectorPool.Count; i += 1)
			{
				liverySelectorPool[i].scrollElement.gameObject.SetActive(false);
			}

			var added = entriesEquipmentByID.Any();
			selector.holderList.SetActive(added);
			selector.holderEmpty.SetActive(!added);
			if (added)
			{
				selector.scrollView.OnEntryListChanged();
			}
			CIViewBaseCustomizationInfo.ins.SetLocation(added ? CIViewBaseCustomizationInfo.Location.LeftRaised : CIViewBaseCustomizationInfo.Location.CornerRaised);
			access.ResetEquipmentSelection.Invoke(selector, null);
		}

		private static System.Func<EquipmentEntity, EquipmentEntity, int> GetSorter(string sortMode, bool sortAscending)
		{
			var modeMap = new Dictionary<string, SortMode>()
			{
				[CIViewBaseCustomizationSelector.SortingKeys.level] = SortMode.Level,
				[CIViewBaseCustomizationSelector.SortingKeys.quality] = SortMode.Quality,
				[CIViewBaseCustomizationSelector.SortingKeys.group] = SortMode.Group,
			};

			if (!modeMap.TryGetValue(sortMode ?? string.Empty, out var mode))
			{
				return (entity1, entity2) => 0;
			}

			var order = sortAscending ? SortOrder.Ascending : SortOrder.Descending;
			return (entity1, entity2) => CompareItemForSorting(mode, order, entity1, entity2);
		}

		private static int CompareItemForSorting(SortMode mode, SortOrder order, EquipmentEntity entity1, EquipmentEntity entity2)
		{
			var orderModifier = order == SortOrder.Ascending ? 1 : -1;

			if (entity1 == null)
			{
				return entity2 == null ? 0 : -1 * orderModifier;
			}

			if (entity2 == null)
			{
				return orderModifier;
			}

			var cmp = 0;

			if (mode == SortMode.Quality)
			{
				cmp = CompareByQuality(entity1, entity2);
			}
			else if (mode == SortMode.Group)
			{
				cmp = CompareByGroup(entity1, entity2);
			}

			if (cmp != 0)
			{
				return cmp * orderModifier;
			}

			cmp = CompareByLevel(entity1, entity2);
			if (cmp != 0)
			{
				return cmp * orderModifier;
			}

			return CompareDefault(orderModifier, entity1, entity2);
		}

		private static int CompareByQuality(EquipmentEntity entity1, EquipmentEntity entity2) =>
			CompareBy(GetQuality, entity1, entity2);

		private static int GetQuality(EquipmentEntity entity)
		{
			if (entity.isPart)
			{
				return entity.hasRating ? entity.rating.i : 1;
			}

			if (entity.isSubsystem)
			{
				return entity.dataLinkSubsystem.data.rating;
			}

			return 1;
		}

		private static int CompareByGroup(EquipmentEntity entity1, EquipmentEntity entity2) =>
			CompareBy(GetGroup, entity1, entity2);

		private static string GetGroup(EquipmentEntity entity)
		{
			if (entity.isPart)
			{
				return entity.hasDataLinkPartPreset? entity.dataLinkPartPreset.data.groupMainKey : string.Empty;
			}

			if (entity.isSubsystem)
			{
				return entity.dataLinkSubsystem.data.hardpointsProcessed.FirstOrDefault();
			}

			return string.Empty;
		}

		private static int CompareByLevel(EquipmentEntity entity1, EquipmentEntity entity2) =>
			CompareBy(GetLevel, entity1, entity2);

		private static float GetLevel(EquipmentEntity entity)
		{
			if (entity.isPart)
			{
				return DataHelperStats.GetAveragePartLevel(entity);
			}

			if (entity.isSubsystem)
			{
				return entity.level.i;
			}

			return 0f;
		}

		private static int CompareBy<T>(System.Func<EquipmentEntity, T> value, EquipmentEntity entity1, EquipmentEntity entity2)
			 where T : System.IComparable
		{
			var v1 = value(entity1);
			var v2 = value(entity2);
			return v1.CompareTo(v2);
		}

		private static int CompareBy(System.Func<EquipmentEntity, string> value, EquipmentEntity entity1, EquipmentEntity entity2)
		{
			var v1 = value(entity1);
			var v2 = value(entity2);
			return string.Compare(v1, v2, System.StringComparison.InvariantCulture);
		}

		private static int CompareDefault(int orderModifier, EquipmentEntity entity1, EquipmentEntity entity2)
		{
			if (entity1.isPart && entity2.isSubsystem)
			{
				return -1 * orderModifier;
			}

			if (entity1.isSubsystem && entity2.isPart)
			{
				return 1 * orderModifier;
			}

			if (entity1.isPart)
			{
				return ComparePartDefault(orderModifier, entity1, entity2);
			}

			if (entity1.isSubsystem)
			{
				return CompareSubsystemDefault(orderModifier, entity1, entity2);
			}

			return 0;
		}

		private static int ComparePartDefault(int orderModifier, EquipmentEntity entity1, EquipmentEntity entity2)
		{
			int cmp = string.Compare(
				entity1.partBlueprint.sockets.FirstOrDefault(),
				entity2.partBlueprint.sockets.FirstOrDefault(),
				System.StringComparison.InvariantCulture);

			if (cmp != 0)
				return cmp * orderModifier;

			cmp = string.Compare(
				entity1.hasDataKeyPartPreset ? entity1.dataKeyPartPreset.s : string.Empty,
				entity2.hasDataKeyPartPreset ? entity2.dataKeyPartPreset.s : string.Empty,
				System.StringComparison.InvariantCulture);

			return cmp != 0
				? cmp * orderModifier
				: string.Compare(
					entity1.nameInternal.s,
					entity2.nameInternal.s,
					System.StringComparison.InvariantCultureIgnoreCase) * orderModifier;
		}

		private static int CompareSubsystemDefault(int orderModifier, EquipmentEntity entity1, EquipmentEntity entity2)
		{
			var data1 = entity1.dataLinkSubsystem.data;
			var data2 = entity2.dataLinkSubsystem.data;
			int cmp = string.Compare(data1.parent, data2.parent, System.StringComparison.InvariantCulture);

			if (cmp != 0)
				return cmp * orderModifier;

			return string.Compare(
				data1.textNameProcessed?.s,
				data2.textNameProcessed?.s,
				System.StringComparison.InvariantCultureIgnoreCase) * orderModifier;
		}

		private static ItemEntryInfo GetEntryInfo(EquipmentEntity entity)
		{
			if (entity.isPart)
			{
				return GetPartEntryInfo();
			}

			if (entity.isSubsystem)
			{
				return GetSubsystemEntryInfo();
			}

			return null;
		}

			private static ItemEntryInfo GetPartEntryInfo() =>
			new ItemEntryInfo()
			{
				MakeName = (entity, id) => string.Format("Part_{0}_{1}", id, PB_IDUtility.ToLog(entity)),
				ConfigureEntry = ConfigurePartEntry,
				OnHoverStart = id => OnHoverStart(ItemEntryKind.Part, id),
				OnHoverEnd = id => OnHoverEnd(ItemEntryKind.Part, id),
				OnSelectionIsolated = id => OnSelectionIsolated(ItemEntryKind.Part, id),
			};

		private static ItemEntryInfo GetSubsystemEntryInfo() =>
			new ItemEntryInfo()
			{
				MakeName = (entity, id) => string.Format("Subsystem_{0}_{1}", id, entity.dataKeySubsystem.s),
				ConfigureEntry = ConfigureSubsystemEntry,
				OnHoverStart = id => OnHoverStart(ItemEntryKind.Subsystem, id),
				OnHoverEnd = id => OnHoverEnd(ItemEntryKind.Subsystem, id),
				OnSelectionIsolated = id => OnSelectionIsolated(ItemEntryKind.Subsystem, id),
			};

		private static void ConfigurePartEntry(EquipmentEntity equipmentEntity, CIHelperEquipmentSelector component) =>
			access.ConfigurePartEntry.Invoke(CIViewBaseCustomizationSelector.ins, new object[] { equipmentEntity, component, true });

		private static void ConfigureSubsystemEntry(EquipmentEntity equipmentEntity, CIHelperEquipmentSelector component) =>
			access.ConfigureSubsystemEntry.Invoke(CIViewBaseCustomizationSelector.ins, new object[] { equipmentEntity, component, true });

		private static void OnHoverStart(ItemEntryKind kind, int id)
		{
			if (kind == ItemEntryKind.Part)
			{
				access.OnPartHoverStart.Invoke(CIViewBaseCustomizationSelector.ins, new object[] { id });
			}
			else if (kind == ItemEntryKind.Subsystem)
			{
				access.OnSubsystemHoverStart.Invoke(CIViewBaseCustomizationSelector.ins, new object[] { id });
			}
		}

		private static void OnHoverEnd(ItemEntryKind kind, int id)
		{
			if (kind == ItemEntryKind.Part)
			{
				access.OnPartHoverEnd.Invoke(CIViewBaseCustomizationSelector.ins, new object[] { id });
			}
			else if (kind == ItemEntryKind.Subsystem)
			{
				access.OnSubsystemHoverEnd.Invoke(CIViewBaseCustomizationSelector.ins, new object[] { id });
			}
		}

		private static void OnSelectionIsolated(ItemEntryKind kind, int id)
		{
			if (kind == ItemEntryKind.Part)
			{
				access.OnPartSelectionIsolated.Invoke(CIViewBaseCustomizationSelector.ins, new object[] { id });
			}
			else if (kind == ItemEntryKind.Subsystem)
			{
				access.OnSubsystemSelectionIsolated.Invoke(CIViewBaseCustomizationSelector.ins, new object[] { id });
			}
		}

		internal static void Disable()
		{
			if (null == categoryHolder)
			{
				return;
			}

			FileLog.Log("!!! PBMods disabling inventory category list");

			entitiesFiltered.Clear();
			CIViewBaseCustomizationSelector.ins.hideableFilterList.SetVisible(false);
			HideList();
			foreach (var kvp in categories)
			{
				kvp.Value.Display = false;
			}
		}

		internal static void ShowAll()
		{
			var persistentEntity = PB_IDUtility.GetPersistentEntity(CIViewBaseInventory.ins.persistentIDOwner);
			access.SocketLast.SetValue(CIViewBaseCustomizationSelector.ins, null);
			access.HardpointLast.SetValue(CIViewBaseCustomizationSelector.ins, null);
			PrepareHeader(persistentEntity);
			PopulateEquipmentLists(persistentEntity);
			if (partsList.Any() || subsystemsList.Any())
			{
				OnCategorySelected(categoryKeyAll);
			}
			else
			{
				HideList();
				ShowEmptyItemList();
			}
		}

		private static void PrepareHeader(PersistentEntity inventoryOwner)
		{
			CIViewBaseCustomizationSelector.ins.holderSelection.SetActive(inventoryOwner == PB_IDUtility.playerBasePersistent);
			CIViewBaseCustomizationSelector.ins.holderEquipped.SetActive(false);
			CIViewBaseCustomizationSelector.ins.holderHeaderEquipment.SetActive(true);
			CIViewBaseCustomizationSelector.ins.holderHeaderLivery.SetActive(false);
		}

		private static void PopulateEquipmentLists(PersistentEntity inventoryOwner)
		{
			PopulatePartsList(inventoryOwner);
			PopulateSubsystemsList(inventoryOwner);
		}

		private static void PopulatePartsList(PersistentEntity inventoryOwner)
		{
			partsList.Clear();
			var partsInInventory = EquipmentUtility.GetPartsInInventory(inventoryOwner);
			if (partsInInventory == null)
			{
				return;
			}

			foreach (EquipmentEntity equipmentEntity in partsInInventory)
			{
				partsList.Add(equipmentEntity);
			}
		}

		private static void PopulateSubsystemsList(PersistentEntity inventoryOwner)
		{
			subsystemsList.Clear();
			var subsystemsInInventory = EquipmentUtility.GetSubsystemsInInventory(inventoryOwner);
			if (subsystemsInInventory == null)
			{
				return;
			}

			foreach (EquipmentEntity equipmentEntity in subsystemsInInventory)
			{
				subsystemsList.Add(equipmentEntity);
			}
		}

		private static void ShowEmptyItemList()
		{
			CIViewBaseCustomizationSelector.ins.holderList.SetActive(false);
			CIViewBaseCustomizationSelector.ins.holderEmpty.SetActive(true);
			access.ResetEquipmentSelection.Invoke(CIViewBaseCustomizationSelector.ins, null);
		}

		internal static void SetSelectedCategory(string tagPrefix)
		{
			FileLog.Log($"!!! PBMods select category: {tagPrefix}");
			var keyMatch = categories.OrderBy(kvp => kvp.Value.DataContainer.priority)
				.Where(kvp => kvp.Value.DataContainer.tags.Any(t => t.StartsWith(tagPrefix)))
				.Select(kvp => kvp.Key)
				.FirstOrDefault();
			if (keyMatch == null)
			{
				return;
			}

			var display = categories[keyMatch].Display;

			if (display)
			{
				if (!categoryHolder.activeSelf)
				{
					ShowList();
				}
				selectedByNav = true;
				OnCategorySelected(keyMatch);
			}
			else
			{
				HideList();
			}
		}
	}
}
