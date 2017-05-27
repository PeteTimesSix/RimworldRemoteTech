﻿using System;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RemoteExplosives {
	// A FloatMenuOption that shows a tooltip when hovered over.
	public class FloatMenuOptionWithTooltip : FloatMenuOption {
		public TipSignal Tooltip;

		public FloatMenuOptionWithTooltip(string label, Action action, TipSignal tooltip = new TipSignal(), MenuOptionPriority priority = MenuOptionPriority.Default, Action mouseoverGuiAction = null, Thing revalidateClickTarget = null, float extraPartWidth = 0, Func<Rect, bool> extraPartOnGUI = null, WorldObject revalidateWorldClickTarget = null) : base(label, action, priority, mouseoverGuiAction, revalidateClickTarget, extraPartWidth, extraPartOnGUI, revalidateWorldClickTarget) {
			Tooltip = tooltip;
		}

		public override bool DoGUI(Rect rect, bool colonistOrdering) {
			var result = base.DoGUI(rect, colonistOrdering);
			if (!Tooltip.text.NullOrEmpty() || Tooltip.textGetter != null) {
				TooltipHandler.TipRegion(rect, Tooltip);
			}
			return result;
		}
	}
}