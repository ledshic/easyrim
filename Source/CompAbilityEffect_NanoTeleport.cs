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
            IntVec3 destPos = target.Cell;

            pawn.DeSpawn(DestroyMode.Vanish);
            GenSpawn.Spawn(pawn, destPos, map);
            pawn.Notify_Teleported(false, true);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return target.IsValid && target.Cell.InBounds(parent.pawn.Map);
        }
    }
}
