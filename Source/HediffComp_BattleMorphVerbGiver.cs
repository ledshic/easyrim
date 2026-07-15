using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EasyMode
{
    public class HediffCompProperties_BattleMorphVerbGiver : HediffCompProperties_VerbGiver
    {
        public float minSeverityForUse = 1f;

        public HediffCompProperties_BattleMorphVerbGiver()
        {
            compClass = typeof(HediffComp_BattleMorphVerbGiver);
        }
    }

    public class HediffComp_BattleMorphVerbGiver : HediffComp_VerbGiver, IVerbOwner
    {
        private HediffCompProperties_BattleMorphVerbGiver PropsBattleMorphVerbGiver =>
            (HediffCompProperties_BattleMorphVerbGiver)props;

        bool IVerbOwner.VerbsStillUsableBy(Pawn p)
        {
            if (p?.health?.hediffSet == null)
            {
                return false;
            }

            return p.health.hediffSet.hediffs.Contains(parent) &&
                   parent.Severity >= PropsBattleMorphVerbGiver.minSeverityForUse;
        }
    }
}
