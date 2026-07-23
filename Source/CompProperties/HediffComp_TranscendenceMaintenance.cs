using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EasyMode
{
    public class HediffCompProperties_TranscendenceMaintenance : HediffCompProperties
    {
        public int tickInterval = 240;
        public int apparelRepairPerTick = 5;
        public int equipmentRepairPerTick = 5;
        public float psyfocusRestore = 0.40f;
        public int removeBadHediffCount = 1;

        public HediffCompProperties_TranscendenceMaintenance()
        {
            compClass = typeof(HediffComp_TranscendenceMaintenance);
        }
    }

    public class HediffComp_TranscendenceMaintenance : HediffComp
    {
        private HediffCompProperties_TranscendenceMaintenance Props =>
            (HediffCompProperties_TranscendenceMaintenance)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = parent?.pawn;
            if (pawn == null)
            {
                return;
            }

            int interval = Math.Max(1, Props.tickInterval);
            if (!pawn.IsHashIntervalTick(interval))
            {
                return;
            }

            RepairApparel(pawn, Math.Max(0, Props.apparelRepairPerTick));
            RepairEquipment(pawn, Math.Max(0, Props.equipmentRepairPerTick));
            RestorePsyfocus(pawn, Props.psyfocusRestore);
            RemoveRandomBadHediffs(pawn, Math.Max(0, Props.removeBadHediffCount));
        }

        private static void RepairApparel(Pawn pawn, int repairAmount)
        {
            if (repairAmount <= 0 || pawn.apparel == null)
            {
                return;
            }

            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                RepairThingByAmount(worn[i], repairAmount);
            }
        }

        private static void RepairEquipment(Pawn pawn, int repairAmount)
        {
            if (repairAmount <= 0 || pawn.equipment == null)
            {
                return;
            }

            List<ThingWithComps> equipment = pawn.equipment.AllEquipmentListForReading;
            for (int i = 0; i < equipment.Count; i++)
            {
                RepairThingByAmount(equipment[i], repairAmount);
            }
        }

        private static void RepairThingByAmount(Thing thing, int repairAmount)
        {
            if (thing == null || repairAmount <= 0)
            {
                return;
            }

            int maxHitPoints = thing.MaxHitPoints;
            if (maxHitPoints <= 0 || thing.HitPoints >= maxHitPoints)
            {
                return;
            }

            thing.HitPoints = Math.Min(maxHitPoints, thing.HitPoints + repairAmount);
        }

        private static void RestorePsyfocus(Pawn pawn, float psyfocusRestore)
        {
            if (psyfocusRestore <= 0f)
            {
                return;
            }

            pawn.psychicEntropy?.OffsetPsyfocusDirectly(psyfocusRestore);
        }

        private static void RemoveRandomBadHediffs(Pawn pawn, int removeCount)
        {
            if (removeCount <= 0)
            {
                return;
            }

            List<Hediff> hediffs = pawn.health?.hediffSet?.hediffs;
            if (hediffs == null || hediffs.Count == 0)
            {
                return;
            }

            List<Hediff> candidates = new List<Hediff>();
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff?.def == null || !hediff.def.isBad)
                {
                    continue;
                }

                if (hediff is Hediff_AddedPart || hediff is Hediff_Implant)
                {
                    continue;
                }

                candidates.Add(hediff);
            }

            int removeLimit = Math.Min(removeCount, candidates.Count);
            for (int i = 0; i < removeLimit; i++)
            {
                int index = Rand.Range(0, candidates.Count);
                Hediff selected = candidates[index];
                candidates.RemoveAt(index);
                pawn.health.RemoveHediff(selected);
            }
        }
    }
}
