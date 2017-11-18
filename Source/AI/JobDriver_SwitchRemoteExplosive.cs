﻿using System.Collections.Generic;
using Verse.AI;

namespace RemoteExplosives {
	/* 
	 * Calls a colonist to flick a remote explosive.
	 * This includes both setting the armed state and the channel.
	 */
	public class JobDriver_SwitchRemoteExplosive : JobDriver {
		public override bool TryMakePreToilReservations() {
			return pawn.Reserve(job.targetA, job);
		}

		protected override IEnumerable<Toil> MakeNewToils() {
			AddFailCondition(JobHasFailed);
			var explosive = TargetThingA as Building_RemoteExplosive;
			if (explosive == null) yield break;
			yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
			yield return new Toil {
				initAction = () => explosive.DoSwitch(),
				defaultCompleteMode = ToilCompleteMode.Instant
			};
		}

		private bool JobHasFailed() {
			var explosive = TargetThingA as Building_RemoteExplosive;
			return explosive == null || !explosive.Spawned || !explosive.WantsSwitch();
		}
	}
}
