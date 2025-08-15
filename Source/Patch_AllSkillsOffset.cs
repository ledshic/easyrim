using HarmonyLib;
using RimWorld;
using Verse;

namespace EasyMode
{
    [HarmonyPatch(typeof(SkillRecord), "get_Level")]
    public static class Patch_AllSkills_Level_WithGuidance
    {
        static void Postfix(SkillRecord __instance, ref int __result)
        {
            try
            {
                Pawn pawn = __instance?.Pawn;
                if (pawn == null) return;
                if (!pawn.health?.hediffSet?.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail("WorkGuidance")) ?? true) return;
                int boosted = __result + 10;
                if (boosted > 20) boosted = 20;
                __result = boosted;
            }
            catch { }
        }
    }
}
