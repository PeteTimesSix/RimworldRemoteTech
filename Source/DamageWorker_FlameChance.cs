﻿using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RemoteExplosives {
	/**
	 * Like the Flame damage worker, but makes a roll against the chance defined in the def, to see if it should crate fire.
	 */
	public class DamageWorker_FlameChance : DamageWorker_AddInjury {
		public override float Apply(DamageInfo dinfo, Thing victim) {
			if (ShouldCreateFire()) {
				if (!dinfo.InstantOldInjury) {
					victim.TryAttachFire(Rand.Range(0.15f, 0.25f));
				}
				var pawn = victim as Pawn;
				if (pawn != null && pawn.Faction == Faction.OfPlayer) {
					Find.TickManager.slower.SignalForceNormalSpeedShort();
				}
			}
			return base.Apply(dinfo, victim);
		}

		public override void ExplosionAffectCell(Explosion explosion, IntVec3 c, List<Thing> damagedThings, bool canThrowMotes) {
			base.ExplosionAffectCell(explosion, c, damagedThings, canThrowMotes);
			if (ShouldCreateFire()) {
				FireUtility.TryStartFireIn(c, Rand.Range(0.2f, 0.6f));
			}
		}

		private bool ShouldCreateFire() {
			const float defaultFlameChance = .5f;
			var chanceDef = def as FlameChanceDamageDef;
			return Rand.Range(0f, 1f) < (chanceDef != null ? chanceDef.flameChance : defaultFlameChance);
		}
	}
}