using RimWorld;
using Verse;

namespace EasyMode
{
    public class HediffCompProperties_BattleMorphVisibility : HediffCompProperties
    {
        public HediffCompProperties_BattleMorphVisibility()
        {
            compClass = typeof(HediffComp_BattleMorphVisibility);
        }

        public int tickInterval = 30;
        public float hiddenSeverity = 0f;
        public float activeSeverity = 1f;
    }

    public class HediffComp_BattleMorphVisibility : HediffComp
    {
        private const float HiddenSeverityEpsilon = 0.0001f;

        private HediffCompProperties_BattleMorphVisibility PropsBattleMorphVisibility =>
            (HediffCompProperties_BattleMorphVisibility)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = parent.pawn;
            if (pawn == null || pawn.IsHashIntervalTick(PropsBattleMorphVisibility.tickInterval) == false)
            {
                return;
            }

            float hiddenSeverity = PropsBattleMorphVisibility.hiddenSeverity <= 0f
                ? HiddenSeverityEpsilon
                : PropsBattleMorphVisibility.hiddenSeverity;

            float targetSeverity = PawnConditionUtility.IsInCombat(pawn)
                ? PropsBattleMorphVisibility.activeSeverity
                : hiddenSeverity;

            if (parent.Severity != targetSeverity)
            {
                parent.Severity = targetSeverity;
            }
        }

    }
}
