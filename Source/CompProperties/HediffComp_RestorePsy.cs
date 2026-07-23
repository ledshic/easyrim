using Verse;
using RimWorld;
using System;

namespace EasyMode
{
    public class HediffCompProperties_RestorePsy : HediffCompProperties
    {
        public const int BaseTickInterval = 10;
        public float restorePercent = 0.01f;
        public int tickInterval = 60;

        public HediffCompProperties_RestorePsy()
        {
            this.compClass = typeof(HediffComp_RestorePsy);
        }
    }

    public class HediffComp_RestorePsy : HediffComp
    {
        public HediffCompProperties_RestorePsy Props => (HediffCompProperties_RestorePsy)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (parent.pawn == null)
                return;

            int tickInterval = Math.Max(1, Props.tickInterval);
            if (parent.pawn.IsHashIntervalTick(tickInterval))
            {
                if (parent.pawn.psychicEntropy == null)
                    return;

                var entropy = parent.pawn.psychicEntropy;
                float restore = Props.restorePercent * (tickInterval / (float)HediffCompProperties_RestorePsy.BaseTickInterval);
                entropy.OffsetPsyfocusDirectly(restore);
            }
        }
    }
}
