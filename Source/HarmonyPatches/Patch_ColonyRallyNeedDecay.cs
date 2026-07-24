using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EasyMode
{
    [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.NeedInterval))]
    public static class Patch_ColonyRallyFoodDecay
    {
        private static HediffDef FatigueDef => DefDatabase<HediffDef>.GetNamedSilentFail("ColonyRallyFatigue");

        static void Postfix(Need_Food __instance)
        {
            try
            {
                Pawn pawn = GetPawn(__instance);
                if (!HasFatigue(pawn) || IsFoodNeedFrozen(pawn, __instance))
                    return;

                __instance.CurLevel -= __instance.FoodFallPerTick * 150f;
            }
            catch
            {
            }
        }

        private static bool HasFatigue(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.HasHediff(FatigueDef) ?? false;
        }

        private static Pawn GetPawn(Need need)
        {
            return need == null ? null : Traverse.Create(need).Field("pawn").GetValue<Pawn>();
        }

        private static bool IsFoodNeedFrozen(Pawn pawn, Need_Food need)
        {
            if (pawn == null || need == null)
                return true;

            if (pawn.Suspended || pawn.Deathresting)
                return true;

            if (need.def.freezeWhileSleeping && !pawn.Awake())
                return true;

            if (need.def.freezeInMentalState && pawn.InMentalState)
                return true;

            if (!pawn.SpawnedOrAnyParentSpawned && !pawn.IsCaravanMember() && !PawnUtility.IsTravelingInTransportPodWorldObject(pawn))
                return true;

            CompHoldingPlatformTarget platformTarget = pawn.TryGetComp<CompHoldingPlatformTarget>();
            if (platformTarget?.CurrentlyHeldOnPlatform ?? false)
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(Need_Rest), nameof(Need_Rest.NeedInterval))]
    public static class Patch_ColonyRallyRestDecay
    {
        private static HediffDef FatigueDef => DefDatabase<HediffDef>.GetNamedSilentFail("ColonyRallyFatigue");

        static void Postfix(Need_Rest __instance)
        {
            try
            {
                Pawn pawn = GetPawn(__instance);
                if (!HasFatigue(pawn) || __instance.Resting || IsNeedFrozen(pawn, __instance))
                    return;

                float extraDecay = __instance.RestFallPerTick * 150f * pawn.GetStatValue(StatDefOf.RestFallRateFactor);
                __instance.CurLevel -= extraDecay;
            }
            catch
            {
            }
        }

        private static bool HasFatigue(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.HasHediff(FatigueDef) ?? false;
        }

        private static Pawn GetPawn(Need need)
        {
            return need == null ? null : Traverse.Create(need).Field("pawn").GetValue<Pawn>();
        }

        private static bool IsNeedFrozen(Pawn pawn, Need need)
        {
            if (pawn == null || need == null)
                return true;

            if (pawn.Suspended)
                return true;

            if (need.def.freezeWhileSleeping && !pawn.Awake())
                return true;

            if (need.def.freezeInMentalState && pawn.InMentalState)
                return true;

            if (!pawn.SpawnedOrAnyParentSpawned && !pawn.IsCaravanMember() && !PawnUtility.IsTravelingInTransportPodWorldObject(pawn))
                return true;

            return false;
        }
    }
}
