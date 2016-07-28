﻿using Verse;

namespace RemoteExplosives {
	public class HediffComp_SeverityRecovery : HediffComp {

		private static readonly string RecoveringStatusSuffix = "HediffRecovery_status_label".Translate();
		private float lastSeenSeverity;
		private int cooldownTicksLeft;

		public override void CompPostTick() {
			base.CompPostTick();
			var recoProps = props as HediffCompProps_SeverityRecovery;
			if (recoProps != null) {
				if (cooldownTicksLeft > 0) {
					cooldownTicksLeft--;
				}
				if (parent.Severity > lastSeenSeverity + recoProps.severityIncreaseDetectionThreshold) {
					cooldownTicksLeft = recoProps.cooldownAfterSeverityIncrease;
				}
				if (OffCooldown) {
					parent.Severity -= recoProps.severityRecoveryPerTick.RandomInRange;
					if (parent.Severity < 0) parent.Severity = 0;
				}
			}
			if (parent.Severity > props.maxSeverity) {
				parent.Severity = props.maxSeverity;
			}
			lastSeenSeverity = parent.Severity;
		}

		private bool OffCooldown {
			get { return cooldownTicksLeft <= 0; }
		}

		public override string CompLabelInBracketsExtra {
			get {
				return OffCooldown ? RecoveringStatusSuffix : "";
			}
		}
	}
}