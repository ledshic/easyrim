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
            {
                // Log.Warning("HediffComp_RestorePsy: Parent pawn is null");
                return;
            }

            // Log that we're checking this pawn
            int tickInterval = Math.Max(1, Props.tickInterval);
            if (parent.pawn.IsHashIntervalTick(tickInterval))
            {
                // Log.Message($"HediffComp_RestorePsy: Checking pawn {parent.pawn.Name} for psyfocus restoration");
                
                if (parent.pawn.psychicEntropy == null)
                {
                    // Log.Warning($"HediffComp_RestorePsy: Pawn {parent.pawn.Name} has no psychic entropy");
                    return;
                }

                var entropy = parent.pawn.psychicEntropy;
                
                // Use the direct OffsetPsyfocusDirectly method (like WeaponTraitWorker_PsyfocusOnKill)
                // Scale the per-trigger restore amount to preserve the same average restore rate.
                float restore = Props.restorePercent * (tickInterval / (float)HediffCompProperties_RestorePsy.BaseTickInterval);
                entropy.OffsetPsyfocusDirectly(restore);
                // Log.Message($"HediffComp_RestorePsy: Restored {restore:F3} psyfocus to {parent.pawn.Name}");
            }
        }
    }
}
