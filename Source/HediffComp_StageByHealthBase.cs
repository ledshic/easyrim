using Verse;
using RimWorld;
using System.Collections.Generic;

namespace EasyMode
{
    // Shared properties for comps that change stages based on pawn health percent
    public class HediffCompProperties_StageByHealth : HediffCompProperties
    {
        public int tickInterval = 60;

        // Backward-compat fields (optional) — used when 'thresholds' is null or empty
        public float stage1Threshold = 1.0f;   // >= 100%
        public float stage2Threshold = 0.85f;  // >= 85%
        public float stage3Threshold = 0.70f;  // >= 70%
        public float stage4Threshold = 0.55f;  // >= 55%

        // New: variable-length thresholds. Interpret as descending lower-bounds for each stage.
        // Example default (5 stages): [1.0, 0.85, 0.70, 0.55, 0.0]
        // Severity is 1-based index of the first threshold that health >= threshold[i];
        // if none matched, severity == thresholds.Count (last stage).
        public List<float> thresholds = new List<float> { 1.0f, 0.85f, 0.70f, 0.55f, 0.0f };
    }

    // Base comp that updates severity according to health percent and thresholds
    public abstract class HediffComp_StageByHealthBase : HediffComp
    {
        protected HediffCompProperties_StageByHealth PropsBase => (HediffCompProperties_StageByHealth)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            var pawn = parent?.pawn;
            if (pawn == null)
                return;

            // Ensure we have a sane interval
            int interval = PropsBase?.tickInterval > 0 ? PropsBase.tickInterval : 60;
            if (pawn.IsHashIntervalTick(interval))
            {
                float healthPercent = pawn.health.summaryHealth.SummaryHealthPercent;
                parent.Severity = CalculateTargetSeverity(healthPercent);
            }
        }

        protected virtual float CalculateTargetSeverity(float healthPercent)
        {
            var p = PropsBase;
            if (p == null)
                return parent?.Severity ?? 1f;

            // Prefer the new variable-length thresholds if present; otherwise build from legacy fields
            List<float> th = (p.thresholds != null && p.thresholds.Count > 0)
                ? p.thresholds
                : new List<float> { p.stage1Threshold, p.stage2Threshold, p.stage3Threshold, p.stage4Threshold, 0.0f };

            // Ensure we have at least 1 stage to avoid division-by-zero or empty returns
            if (th.Count == 0)
                return 1.0f;

            // Loop over thresholds (descending). Severity is 1-based.
            for (int i = 0; i < th.Count; i++)
            {
                if (healthPercent >= th[i])
                    return i + 1;
            }

            // If health is below the smallest threshold, return the last stage
            return th.Count;
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                var pawn = parent?.pawn;
                if (pawn == null)
                    return null;

                float pct = pawn.health.summaryHealth.SummaryHealthPercent;
                return FormatLabel(pct);
            }
        }

        // Derived comps can override for custom translation/wording
        protected virtual string FormatLabel(float healthPercent)
        {
            return $"{(healthPercent * 100f):F0}% health";
        }
    }
}
