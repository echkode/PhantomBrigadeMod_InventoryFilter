// Copyright (c) 2022 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System;
using System.Linq;

using HarmonyLib;
using UnityEngine;

namespace EchKode.PBMods.InventoryFilter
{
	static class InventoryList
	{
		internal static bool cacheInitialized;
		internal static bool isFilterButtonHidden;

		internal static GameObject gameObjectHeaderBackground;
		internal static float shrinkYPosition;

		private static float headerYPosition;
		private static int headerHeight;
		private static int filterHeight;
		private static int sortHeight;
		private static float scrollviewCreepage;

		internal static void CacheHeights()
		{
			if (Harmony.DEBUG)
			{
				FileLog.Log("!!! PBMods caching values");
			}

			headerYPosition = CIViewBaseCustomizationSelector.ins.holderHeaderEquipment.transform.localPosition.y;
			shrinkYPosition = CIViewBaseWorkshopV2.ins.holderHeader.transform.localPosition.y;

			if (Harmony.DEBUG)
			{
				FileLog.Log($"!!! PBMods header y pos: {headerYPosition}");
				FileLog.Log($"!!! PBMods shrink y pos: {shrinkYPosition}");
			}

			var widget = CIViewBaseCustomizationSelector.ins.holderHeaderEquipment.GetComponent<UIWidget>();
			var sortHeights = new int[]
			{
				ExtractHeight(CIViewBaseCustomizationSelector.ins.buttonSortList),
				CIViewBaseCustomizationSelector.ins.labelSortName.height,
				ExtractHeight(CIViewBaseCustomizationSelector.ins.buttonSortOrder),
				CIViewBaseCustomizationSelector.ins.spriteSortOrder.height,
			};

			headerHeight = widget.height;
			filterHeight = ExtractHeight(CIViewBaseCustomizationSelector.ins.buttonFilterList);
			sortHeight = sortHeights.Max();

			if (Harmony.DEBUG)
			{
				FileLog.Log($"!!! PBMods header height: {headerHeight}");
				FileLog.Log($"!!! PBMods filter height: {filterHeight}");
				FileLog.Log($"!!! PBMods sort height: {sortHeight}");
			}

			foreach (var r in widget.mChildren)
			{
				if (r is UISprite background)
				{
					gameObjectHeaderBackground = background.gameObject;
					break;
				}
			}

			cacheInitialized = true;
		}

		private static int ExtractHeight<T>(T ui)
		{
			var widgetField = AccessTools.Field(typeof(T), "widget");
			var widget = (UIWidget)widgetField.GetValue(ui);
			return widget.height;
		}

		internal static void ShrinkHeader()
		{
			Debug.Log($"Mod {ModLink.modId} hiding filter button on inventory list");
			if (Harmony.DEBUG)
			{
				FileLog.Log("!!! PBMods shrink header");
			}
			CIViewBaseCustomizationSelector.ins.buttonFilterList.gameObject.SetActive(false);
			AdjustHeaderHeight();

		}

		internal static void ExpandHeader()
		{
			if (Harmony.DEBUG)
			{
				FileLog.Log("!!! PBMods header restore callback");
			}
			CIViewBaseCustomizationSelector.ins.buttonFilterList.gameObject.SetActive(true);
			isFilterButtonHidden = false;
			AdjustHeaderHeight();
		}

		private static void AdjustHeaderHeight()
		{
			var dy = Mathf.RoundToInt(isFilterButtonHidden ? shrinkYPosition - headerYPosition : headerYPosition - shrinkYPosition);
			if (Harmony.DEBUG)
			{
				FileLog.Log($"!!! PBMods y change: {dy}");
			}
			AdjustLocalPosition(CIViewBaseCustomizationSelector.ins.holderHeaderEquipment, dy);
			AdjustMainBackgroundHeight(CIViewBaseCustomizationSelector.ins.spriteBackgroundMain, dy);
			AdjustMainBackgroundHeight(CIViewBaseCustomizationSelector.ins.spriteBackgroundBlocker, dy);

			AdjustSortRowPosition();

			dy += isFilterButtonHidden ? headerHeight - sortHeight : sortHeight - headerHeight;
			if (Harmony.DEBUG)
			{
				FileLog.Log($"!!! PBMods scrollview change: {dy}");
			}
			AdjustScrollView(dy);

			var height = isFilterButtonHidden ? sortHeight : headerHeight;
			AdjustHeaderBackgroundHeight(height);
			var widget = CIViewBaseCustomizationSelector.ins.holderHeaderEquipment.gameObject.GetComponent<UIWidget>();
			widget.SetDimensions(widget.width, height);
		}

		private static void AdjustLocalPosition(GameObject o, int dy)
		{
			var localPosition = o.transform.localPosition;
			o.transform.localPosition = new Vector3(localPosition.x, localPosition.y + dy, localPosition.z);
			if (Harmony.DEBUG)
			{
				FileLog.Log($"!!! PBMods local position change: name={o.name}; current={localPosition.y}; change={dy}; new={o.transform.localPosition.y}");
			}
		}

		private static void AdjustMainBackgroundHeight(UISprite background, int dy)
		{
			var height = background.height + dy;
			if (Harmony.DEBUG)
			{
				FileLog.Log($"!!! PBMods main background height: current={background.height}; change={dy}; new={height}");
			}
			background.SetDimensions(background.width, height);
		}

		private static void AdjustSortRowPosition()
		{
			var sortRow = new[] {
				CIViewBaseCustomizationSelector.ins.buttonSortList.gameObject,
				CIViewBaseCustomizationSelector.ins.buttonSortOrder.gameObject
			};
			var direction = isFilterButtonHidden ? 1 : -1;
			var offset = direction * filterHeight;
			Array.ForEach(sortRow, o => AdjustLocalPosition(o, offset));
		}

		private static void AdjustScrollView(int dy)
		{
			// A lot of this code is cribbed from CIViewBaseCustomizationSelector.RefreshScaling().

			// Some magic number pulled from the disassembly.
			const float leftPos = -156f;

			if (Harmony.DEBUG)
			{
				FileLog.Log($"!!! PBMods adjust scrollview: change={dy}; creepage={scrollviewCreepage}");
			}

			var h = CIViewBaseCustomizationSelector.ins.scrollView.widgetMain.height + dy + scrollviewCreepage;
			var num = Mathf.FloorToInt(h / CIViewBaseCustomizationSelector.ins.scrollView.entryHeight);
			var y = CIViewBaseCustomizationSelector.ins.scrollView.transform.localPosition.y;

			if (Harmony.DEBUG)
			{
				FileLog.Log($"!!! PBMods adjust scrollview height: current={CIViewBaseCustomizationSelector.ins.scrollView.widgetMain.height}; new={h}");
				FileLog.Log($"!!! PBMods adjust scrollview cells: current={CIViewBaseCustomizationSelector.ins.scrollView.cells}; new={num}");
			}

			if (num == CIViewBaseCustomizationSelector.ins.scrollView.cells)
			{
				if (Harmony.DEBUG)
				{
					FileLog.Log($"!!! PBMods adjust scrollview pos: current={y}; change={dy}; new={y + dy}");
				}

				y += dy;
				scrollviewCreepage += dy;
				CIViewBaseCustomizationSelector.ins.scrollView.transform.localPosition = new Vector3(leftPos, y, 0.0f);
				return;
			}

			y = Mathf.RoundToInt((float)(num * 48.0 * 0.5 + 24.0));
			if (Harmony.DEBUG)
			{
				FileLog.Log($"!!! PBMods adjust scrollview pos: current={CIViewBaseCustomizationSelector.ins.scrollView.transform.localPosition.y}; new={y}");
			}
			CIViewBaseCustomizationSelector.ins.scrollView.cells = num;
			CIViewBaseCustomizationSelector.ins.scrollView.UpdatePanel();
			CIViewBaseCustomizationSelector.ins.scrollView.transform.localPosition = new Vector3(leftPos, y, 0.0f);
		}

		private static void AdjustHeaderBackgroundHeight(int headerHeight)
		{
			var background = gameObjectHeaderBackground.GetComponent<UISprite>();
			if (Harmony.DEBUG)
			{
				FileLog.Log($"!!! PBMods background height: current={background.height}; new={headerHeight}");
			}
			background.SetDimensions(background.width, headerHeight);
		}
	}
}
