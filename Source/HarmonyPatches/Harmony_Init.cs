using HarmonyLib;
using Verse;

namespace EasyMode
{
    // Minimal static init to apply Harmony patches in this assembly
    [StaticConstructorOnStartup]
    public static class EasyModeHarmonyInit
    {
        static EasyModeHarmonyInit()
        {
            var harmony = new Harmony("easyrim.harmony");
            harmony.PatchAll();
        }
    }
}
