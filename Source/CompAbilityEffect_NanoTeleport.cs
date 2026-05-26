using RimWorld;
using Verse;

namespace EasyMode
{
    public class CompProperties_NanoTeleport : CompProperties_AbilityEffect
    {
        public CompProperties_NanoTeleport()
        {
            compClass = typeof(CompAbilityEffect_NanoTeleport);
        }
    }

    public class CompAbilityEffect_NanoTeleport : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn pawn = parent.pawn;
            Map map = pawn.Map;
            IntVec3 cell = target.Cell;

            ActiveDropPodInfo podInfo = new ActiveDropPodInfo();
            podInfo.innerContainer.TryAdd(pawn);
            DropPodUtility.MakeDropPodAt(cell, map, podInfo);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return target.IsValid && target.Cell.InBounds(parent.pawn.Map);
        }
    }
}
