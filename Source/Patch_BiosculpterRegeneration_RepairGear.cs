using HarmonyLib;
using RimWorld;
using Verse;

namespace EasyMode
{
    [HarmonyPatch(typeof(CompBiosculpterPod_HealingCycle), nameof(CompBiosculpterPod_HealingCycle.CycleCompleted))]
    public static class Patch_BiosculpterRegeneration_RepairGear
    {
        static void Postfix(CompBiosculpterPod_HealingCycle __instance, Pawn pawn)
        {
            try
            {
                if (pawn == null) return;
                if (!(__instance is CompBiosculpterPod_RegenerationCycle)) return;

                if (pawn.apparel != null)
                {
                    foreach (Apparel apparel in pawn.apparel.WornApparel)
                    {
                        RepairThingToMax(apparel);
                    }
                }

                if (pawn.equipment != null)
                {
                    foreach (ThingWithComps equipment in pawn.equipment.AllEquipmentListForReading)
                    {
                        RepairThingToMax(equipment);
                    }
                }
            }
            catch
            {
                // Keep vanilla cycle completion stable even if repair logic fails.
            }
        }

        private static void RepairThingToMax(Thing thing)
        {
            if (thing == null) return;
            if (thing.MaxHitPoints <= 0) return;
            if (thing.HitPoints >= thing.MaxHitPoints) return;

            thing.HitPoints = thing.MaxHitPoints;
        }
    }
}
