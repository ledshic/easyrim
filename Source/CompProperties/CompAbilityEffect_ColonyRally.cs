using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EasyMode
{
    public class CompProperties_ColonyRally : CompProperties_AbilityEffect
    {
        public ThoughtDef thoughtDef;
        public HediffDef hediffDef;
        public float radius = 10f;

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

            Pawn caster = parent.pawn;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            int count = 0;
            foreach (Pawn pawn in colonists)
            {
                if (pawn == null || !pawn.Spawned || !pawn.Position.InHorDistOf(caster.Position, Props.radius))
                    continue;

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

    public class CompProperties_PioneerProclamation : CompProperties_AbilityEffect
    {
        public CompProperties_PioneerProclamation()
        {
            compClass = typeof(CompAbilityEffect_PioneerProclamation);
        }
    }

    public class CompAbilityEffect_PioneerProclamation : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Map map = parent.pawn.Map;
            if (map == null)
                return;

            List<Pawn> pawns = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            int inspiredCount = 0;
            foreach (Pawn pawn in pawns)
            {
                if (TryGrantAnyInspiration(pawn))
                    inspiredCount++;
            }

            Messages.Message("PioneerProclamationApplied".Translate(inspiredCount, pawns.Count), MessageTypeDefOf.PositiveEvent);
        }

        private static bool TryGrantAnyInspiration(Pawn pawn)
        {
            if (pawn?.mindState?.inspirationHandler == null)
                return false;

            if (!pawn.RaceProps.Humanlike)
                return false;

            if (pawn.Inspired)
                return false;

            List<InspirationDef> inspirations = DefDatabase<InspirationDef>.AllDefsListForReading
                .Where(def => def?.Worker != null && def.Worker.InspirationCanOccur(pawn))
                .InRandomOrder()
                .ToList();

            foreach (InspirationDef inspiration in inspirations)
            {
                if (pawn.mindState.inspirationHandler.TryStartInspiration(inspiration))
                    return true;
            }

            return false;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (parent.pawn.Map == null)
            {
                if (throwMessages)
                    Messages.Message("EasyMode_ProclamationNoMap".Translate(),
                        MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            return true;
        }
    }
}
