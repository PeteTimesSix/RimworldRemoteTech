﻿using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RemoteExplosives {
	/**
	 * A designator that selects only detonation wire
	 */
	public class Designator_SelectDetonatorWire : Designator {
		
		public Designator_SelectDetonatorWire() {
			hotKey = KeyBindingDefOf.Misc10;
			icon = Resources.Textures.UISelectWire;
			useMouseIcon = true;
			defaultLabel = "WireDesignator_label".Translate();
			defaultDesc = "WireDesignator_desc".Translate();
			soundDragSustain = SoundDefOf.DesignateDragStandard;
			soundDragChanged = SoundDefOf.DesignateDragStandardChanged;
			soundSucceeded = SoundDefOf.ThingSelected;
		}

		public override string Label {
			get { return "WireDesignator_label".Translate(); }
		}

		public override string Desc {
			get { return "WireDesignator_desc".Translate(); }
		}

		public override int DraggableDimensions {
			get { return 2; }
		}

		public override bool DragDrawMeasurements {
			get { return true; }
		}

		public override AcceptanceReport CanDesignateCell(IntVec3 loc) {
			var contents = Map.thingGrid.ThingsListAt(loc);
			if (contents != null) {
				for (int i = 0; i < contents.Count; i++) {
					if (contents[i] is Building_DetonatorWire) return true;
				}
			}
			return false;
		}
		
		public override void DesignateSingleCell(IntVec3 c) {
			if(!ShiftIsHeld()) Find.Selector.ClearSelection();
			CellDesignate(c);
			TryCloseArchitectMenu();
		}

		public override void DesignateMultiCell(IEnumerable<IntVec3> cells) {
			if (!ShiftIsHeld()) Find.Selector.ClearSelection();
			foreach (var cell in cells) {
				CellDesignate(cell);
			}
			TryCloseArchitectMenu();
		}

		private void CellDesignate(IntVec3 cell) {
			var contents = Map.thingGrid.ThingsListAt(cell);
			var selector = Find.Selector;
			if (contents != null) {
				for (int i = 0; i < contents.Count; i++) {
					var thing = contents[i];
					if (thing is Building_DetonatorWire && !selector.SelectedObjects.Contains(thing)) {
						selector.SelectedObjects.Add(thing);
						SelectionDrawer.Notify_Selected(thing);
					}
				}
			}
		}

		private void TryCloseArchitectMenu() {
			if (Find.Selector.NumSelected == 0) return;
			if (Find.MainTabsRoot.OpenTab != MainButtonDefOf.Architect) return;
			Find.MainTabsRoot.EscapeCurrentTab();
		}

		private bool ShiftIsHeld() {
			return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		}
	}
}