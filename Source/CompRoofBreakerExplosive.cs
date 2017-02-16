﻿using HugsLib;
using RimWorld;
using Verse;
using Verse.Sound;

namespace RemoteExplosives {
	// A mining explosive that is able to break thick mountain roof
	// TODO: Add postfix to AutoBuildRoofZoneSetter to remove roof orders over collapsed rock, make collapsed rock impassable again
	public class CompRoofBreakerExplosive : CompMiningExplosive {
		private const int RoofFilthAmount = 3;
		private readonly IntRange CollapseDelay = new IntRange(0, 120);
		
		protected override void Detonate() {
			var map = parentMap;
			var position = parentPosition;
			base.Detonate();
			if (map == null) return;
			var explosiveProps = props as CompProperties_Explosive;
			if(explosiveProps == null) return;
			var canAffectThickRoof = RemoteExplosivesUtility.IsEffectiveRoofBreakerPlacement(explosiveProps.explosiveRadius, position, map);
			bool anyThickRoofAffected = false;
			foreach (var cell in GenRadial.RadialCellsAround(position, explosiveProps.explosiveRadius, true)) {
				if(!cell.InBounds(map)) continue;
				var roof = map.roofGrid.RoofAt(cell);
				if(roof == null || (roof.isThickRoof && !canAffectThickRoof)) continue;
				if (roof.filthLeaving != null) {
					for (int j = 0; j < RoofFilthAmount; j++) {
						FilthMaker.MakeFilth(cell, map, roof.filthLeaving);
					}
				}
				if (roof.isThickRoof) {
					anyThickRoofAffected = true;
					map.roofGrid.SetRoof(cell, null);
					var roofCell = cell;
					HugsLibController.Instance.CallbackScheduler.ScheduleCallback(() => { // delay collapse for more interesting visual effect
						CollapseRockOnCell(roofCell, map);
						SoundDefOf.RoofCollapse.PlayOneShot(new TargetInfo(roofCell, map));
					}, CollapseDelay.RandomInRange);
				}
			}
			if (anyThickRoofAffected) {
				CaveInSoundEffect.PlayOneShot(new TargetInfo(position, map));
			}
		}

		private void CollapseRockOnCell(IntVec3 cell, Map map) {
			CrushThingsUnderCollapsingRock(cell, map);
			var rock = GenSpawn.Spawn(RemoteExplosivesDefOf.CollapsedRoofRocks, cell, map);
			if (rock.def.rotatable) {
				rock.Rotation = Rot4.Random;
			}
		}

		private void CrushThingsUnderCollapsingRock(IntVec3 cell, Map map) {
			for (int i = 0; i < 2; i++) {
				var thingList = cell.GetThingList(map);
				for (int j = thingList.Count - 1; j >= 0; j--) {
					var thing = thingList[j];
					map.roofCollapseBuffer.Notify_Crushed(thing);
					var pawn = thing as Pawn;
					DamageInfo dinfo;
					if (pawn != null) {
						var brain = pawn.health.hediffSet.GetBrain();
						dinfo = new DamageInfo(DamageDefOf.Crush, 99999, -1f, null, brain);
					} else {
						dinfo = new DamageInfo(DamageDefOf.Crush, 99999, -1f);
						dinfo.SetBodyRegion(BodyPartHeight.Top, BodyPartDepth.Outside);
					}
					thing.TakeDamage(dinfo);
					if (!thing.Destroyed && thing.def.destroyable) {
						thing.Destroy();
					}
				}
			}
		}
	}
}