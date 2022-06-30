// Copyright (c) 2022 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using HarmonyLib;

namespace EchKode.PBMods.InventoryFilter
{
	static partial class InventoryFilterPatch
	{
		[HarmonyPatch(typeof(CIViewInventory))]
		[HarmonyPatch("OnNavToPartsAll", MethodType.Normal)]
		[HarmonyPrefix]
		static bool Vi_OnNavToPartsAllPrefix()
		{
			if (!InventoryList.isFilterButtonHidden)
			{
				return true;
			}

			FileLog.Log("!!! PBMods nav to all parts");
			if (!InventoryList.cacheInitialized)
			{
				CategoryList.deferredNavAll = true;
				return true;
			}

			CategoryList.ShowAll();
			return false;
		}

		[HarmonyPatch(typeof(CIViewInventory))]
		[HarmonyPatch("OnNavToSubsystemsAll", MethodType.Normal)]
		[HarmonyPrefix]
		static void Vi_OnNavToSubsystemsAllPrefix()
		{
			if (!InventoryList.isFilterButtonHidden)
			{
				return;
			}
			FileLog.Log("!!! PBMods nav to all subsystems");
		}

		[HarmonyPatch(typeof(CIViewInventory))]
		[HarmonyPatch("OnNavToSubsystemsAll", MethodType.Normal)]
		[HarmonyPostfix]
		static void Vi_OnNavToSubsystemsAllPostfix()
		{
			if (!InventoryList.isFilterButtonHidden)
			{
				return;
			}
			CategoryList.SetSelectedCategory("subsystem_internal_aux_");
		}

		[HarmonyPatch(typeof(CIViewInventory))]
		[HarmonyPatch("OnNavToSocket", MethodType.Normal)]
		[HarmonyPrefix]
		static void Vi_OnNavToSocketPrefix(string socket)
		{
			if (!InventoryList.isFilterButtonHidden)
			{
				return;
			}
			FileLog.Log($"!!! PBMods nav to socket: {socket}");
		}

		[HarmonyPatch(typeof(CIViewInventory))]
		[HarmonyPatch("OnNavToSocket", MethodType.Normal)]
		[HarmonyPostfix]
		static void Vi_OnNavToSocketPostfix(string socket)
		{
			if (!InventoryList.isFilterButtonHidden)
			{
				return;
			}

			var kind = socket == "equipment_right" ? "wpn" : "body";
			CategoryList.SetSelectedCategory($"part_{kind}_");
		}

		[HarmonyPatch(typeof(CIViewInventory))]
		[HarmonyPatch("OnNavToHardpoint", MethodType.Normal)]
		[HarmonyPrefix]
		static void Vi_OnNavToHardpointPrefix(string hardpoint)
		{
			if (!InventoryList.isFilterButtonHidden)
			{
				return;
			}
			FileLog.Log($"!!! PBMods nav to hardpoint: {hardpoint}");
		}

		[HarmonyPatch(typeof(CIViewInventory))]
		[HarmonyPatch("OnNavToHardpoint", MethodType.Normal)]
		[HarmonyPostfix]
		static void Vi_OnNavToHardpointPostfix(string hardpoint)
		{
			if (!InventoryList.isFilterButtonHidden)
			{
				return;
			}
			CategoryList.SetSelectedCategory("subsystem_internal_aux_");
		}
	}
}
