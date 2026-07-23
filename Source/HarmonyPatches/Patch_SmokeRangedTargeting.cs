using HarmonyLib;
using RimWorld;
using Verse;

namespace EasyMode;

internal static class SmokeTargetingUtility
{
    public static bool BlocksRangedTargetAcquisition(Verb verb, IntVec3 root, LocalTargetInfo target)
    {
        if (verb == null || verb.verbProps == null || verb.caster == null)
        {
            return false;
        }

        if (verb.IsMeleeAttack || !IsRangedWeaponOrTurretVerb(verb))
        {
            return false;
        }

        Map map = verb.caster.Map;
        if (map == null)
        {
            return false;
        }

        if (root.InBounds(map) && root.AnyGas(map, GasType.BlindSmoke))
        {
            return true;
        }

        if (target.IsValid && target.Cell.InBounds(map) && target.Cell.AnyGas(map, GasType.BlindSmoke))
        {
            return true;
        }

        return false;
    }

    private static bool IsRangedWeaponOrTurretVerb(Verb verb)
    {
        if (verb.EquipmentSource != null || verb.ReloadableCompSource != null || verb.VerbOwner_ChargedCompSource != null)
        {
            return true;
        }

        return verb.caster is Building_Turret;
    }
}

[HarmonyPatch(typeof(Verb), nameof(Verb.CanHitTargetFrom))]
internal static class Patch_Verb_CanHitTargetFrom_SmokeLockout
{
    [HarmonyPrefix]
    private static bool Prefix(Verb __instance, IntVec3 root, LocalTargetInfo targ, ref bool __result)
    {
        if (SmokeTargetingUtility.BlocksRangedTargetAcquisition(__instance, root, targ))
        {
            __result = false;
            return false;
        }

        return true;
    }
}