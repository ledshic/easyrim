using Verse;
using RimWorld;
using System.Collections.Generic;

namespace EasyMode
{
    // Shared properties for comps that change stages based on pawn health percent
    public class HediffCompProperties_StageByHealth : HediffCompProperties
    {
        public int tickInterval = 60;

        public float stage1Threshold = 1.0f;
        public float stage2Threshold = 0.85f;
        public float stage3Threshold = 0.70f;
        public float stage4Threshold = 0.55f;

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

            List<float> th = (p.thresholds != null && p.thresholds.Count > 0)
                ? p.thresholds
                : new List<float> { p.stage1Threshold, p.stage2Threshold, p.stage3Threshold, p.stage4Threshold, 0.0f };

            if (th.Count == 0)
                return 1.0f;

            for (int i = 0; i < th.Count; i++)
            {
                if (healthPercent >= th[i])
                    return i + 1;
            }

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
