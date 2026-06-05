using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EasyMode
{
    public class CompProperties_ColonyRally : CompProperties_AbilityEffect
    {
        public ThoughtDef thoughtDef;
        public HediffDef hediffDef;

        public CompProperties_ColonyRally()
        {
            compClass = typeof(CompAbilityEffect_ColonyRally);
        }
    }

    public class CompAbilityEffect_ColonyRally : CompAbilityEffect
    {
        private new CompProperties_ColonyRally Props => (CompProperties_ColonyRally)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Map map = parent.pawn.Map;
            if (map == null)
                return;

            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            int count = 0;
            foreach (Pawn pawn in colonists)
            {
                if (Props.thoughtDef != null && pawn.needs?.mood?.thoughts?.memories != null)
                    pawn.needs.mood.thoughts.memories.TryGainMemory(Props.thoughtDef);

                if (Props.hediffDef != null && pawn.health != null)
                {
                    Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
                    if (existing != null)
                        pawn.health.RemoveHediff(existing);
                    pawn.health.AddHediff(Props.hediffDef);
                }

                count++;
            }

            Messages.Message("ColonyRallyApplied".Translate(count), MessageTypeDefOf.PositiveEvent);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (parent.pawn.Map == null)
            {
                if (throwMessages)
                    Messages.Message("EasyMode_RallyNoMap".Translate(),
                        MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            return true;
        }
    }
}
