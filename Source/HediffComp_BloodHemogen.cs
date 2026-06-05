using Verse;
using RimWorld;
using System.Collections.Generic;

namespace EasyMode
{
    public class HediffCompProperties_BloodHemogenStage : HediffCompProperties
    {
        public int tickInterval = 60;
        public List<float> thresholds = new List<float> { 0.8f, 0.6f, 0.4f, 0.2f, 0.0f };

        public HediffCompProperties_BloodHemogenStage()
        {
            this.compClass = typeof(HediffComp_BloodHemogenStage);
        }
    }

    public class HediffComp_BloodHemogenStage : HediffComp
    {
        public HediffCompProperties_BloodHemogenStage Props => (HediffCompProperties_BloodHemogenStage)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            var pawn = parent?.pawn;
            if (pawn == null)
                return;

            int interval = Props.tickInterval > 0 ? Props.tickInterval : 60;
            if (!pawn.IsHashIntervalTick(interval))
                return;

            var hemogenGene = pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (hemogenGene == null)
                return;

            float hemogenPct = hemogenGene.Max > 0f ? hemogenGene.Value / hemogenGene.Max : 0f;
            parent.Severity = CalculateTargetSeverity(hemogenPct);
        }

        private float CalculateTargetSeverity(float hemogenPct)
        {
            var th = Props.thresholds;
            if (th == null || th.Count == 0)
                return 1f;

            for (int i = 0; i < th.Count; i++)
            {
                if (hemogenPct >= th[i])
                    return i + 1;
            }
            return th.Count;
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                var hemogenGene = parent?.pawn?.genes?.GetFirstGeneOfType<Gene_Hemogen>();
                if (hemogenGene == null)
                    return null;
                int pct = (int)(hemogenGene.Value / hemogenGene.Max * 100f + 0.5f);
                return "BloodHemogen_Bracket".Translate(pct);
            }
        }
    }
}
