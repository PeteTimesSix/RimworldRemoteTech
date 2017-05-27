﻿using Verse;

namespace RemoteExplosives {
	public class CompProperties_RandomResourceLeaver : CompProperties {
		public ThingDef thingDef;
		public IntRange amountRange;
		public DestroyMode requiredDestroyMode = DestroyMode.KillFinalize;

		public CompProperties_RandomResourceLeaver() {
			compClass = typeof (CompRandomResourceLeaver);
		}
	}
}
