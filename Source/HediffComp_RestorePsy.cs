using Verse;
using RimWorld;
using System;

namespace EasyMode
{
    public class HediffCompProperties_RestorePsy : HediffCompProperties
    {
        public float restorePercent = 0.01f;
        public int tickInterval = 10;

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
            if (parent.pawn.IsHashIntervalTick(Props.tickInterval))
            {
                // Log.Message($"HediffComp_RestorePsy: Checking pawn {parent.pawn.Name} for psyfocus restoration");
                
                if (parent.pawn.psychicEntropy == null)
                {
                    // Log.Warning($"HediffComp_RestorePsy: Pawn {parent.pawn.Name} has no psychic entropy");
                    return;
                }

                var entropy = parent.pawn.psychicEntropy;
                
                // Use the direct OffsetPsyfocusDirectly method (like WeaponTraitWorker_PsyfocusOnKill)
                // For now, use a base value calculation instead of accessing max psyfocus property
                float restore = 1.0f * Props.restorePercent; // Assuming max psyfocus is 1.0
                entropy.OffsetPsyfocusDirectly(restore);
                // Log.Message($"HediffComp_RestorePsy: Restored {restore:F3} psyfocus to {parent.pawn.Name}");
            }
        }
    }
}
