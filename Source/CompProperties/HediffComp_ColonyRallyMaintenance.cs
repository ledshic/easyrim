using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EasyMode
{
    public class HediffCompProperties_ColonyRallyMaintenance : HediffCompProperties
    {
        public int tickInterval = 240;
        public HediffDef followupHediffDef;

        public HediffCompProperties_ColonyRallyMaintenance()
        {
            compClass = typeof(HediffComp_ColonyRallyMaintenance);
        }
    }

    public class HediffComp_ColonyRallyMaintenance : HediffComp
    {
        private HediffCompProperties_ColonyRallyMaintenance Props => (HediffCompProperties_ColonyRallyMaintenance)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = parent?.pawn;
            if (pawn == null)
                return;

            int interval = Props.tickInterval > 0 ? Props.tickInterval : 240;
            if (!pawn.IsHashIntervalTick(interval))
                return;

            if (pawn.needs?.AllNeeds == null)
                return;

            List<Need> needs = pawn.needs.AllNeeds;
            for (int i = 0; i < needs.Count; i++)
            {
                Need need = needs[i];
                if (need != null)
                    need.CurLevel = need.MaxLevel;
            }
        }

        public override void CompPostPostRemoved()
        {
            try
            {
                Pawn pawn = parent?.pawn;
                if (pawn == null || Props.followupHediffDef == null || pawn.health == null)
                    return;

                if (pawn.health.hediffSet.HasHediff(Props.followupHediffDef))
                    return;

                pawn.health.AddHediff(Props.followupHediffDef);
            }
            catch
            {
            }

            base.CompPostPostRemoved();
        }
    }
}
