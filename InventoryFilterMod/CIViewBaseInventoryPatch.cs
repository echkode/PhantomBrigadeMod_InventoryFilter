using System;

using HarmonyLib;
using UnityEngine;

namespace EchKode.PBMods.InventoryFilter
{
	[HarmonyPatch]
	static partial class InventoryFilterPatch
	{
		[HarmonyPatch(typeof(CIViewBaseInventory))]
		[HarmonyPatch("TryEntry", MethodType.Normal)]
		[HarmonyPrefix]
		static void Bvi_TryEntryPrefix()
		{
			InventoryList.isFilterButtonHidden = true;
		}

		[HarmonyPatch(typeof(CIViewBaseInventory))]
		[HarmonyPatch("TryEntry", MethodType.Normal)]
		[HarmonyPostfix]
		static void Bvi_TryEntryPostfix()
		{
			if (!InventoryList.isFilterButtonHidden)
			{
				return;
			}

			if (!InventoryList.cacheInitialized)
			{
				InventoryList.CacheHeights();
				CategoryList.AddCategories();
			}

			CIViewOverworldRoot.ins.hideableMainHolder.SetVisible(true);
			InventoryList.ShrinkHeader();

			if (CategoryList.deferredNavAll)
			{
				CategoryList.ShowAll();
				CategoryList.deferredNavAll = false;
			}
			CategoryList.Enable();
		}

		[HarmonyPatch(typeof(CIViewBaseInventory))]
		[HarmonyPatch("TryExit", MethodType.Normal)]
		[HarmonyPrefix]
		static void Bvi_TryExitPrefix(ref Action callbackOnComplete)
		{
			if (!InventoryList.isFilterButtonHidden)
			{
				return;
			}

			Debug.Log($"Mod {ModLink.modId} restoring filter button on inventory list");
			if (Harmony.DEBUG)
			{
				FileLog.Log("!!! PBMods restore header");
			}
			callbackOnComplete = InventoryList.ExpandHeader;
		}

		[HarmonyPatch(typeof(CIViewBaseInventory))]
		[HarmonyPatch("TryExit", MethodType.Normal)]
		[HarmonyPostfix]
		static void Bvi_TryExitPostfix()
		{
			if (InventoryList.isFilterButtonHidden)
			{
				CategoryList.Disable();
			}
		}
	}
}
