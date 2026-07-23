using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EasyMode;

[HarmonyPatch(typeof(GenRecipe), nameof(GenRecipe.MakeRecipeProducts))]
internal static class Patch_GenRecipe_MakeRecipeProducts_UniqueWeaponConversion
{
    private const string ConversionRecipeDefName = "EasyMode_ConvertToUniqueRangedWeapon";
    private const string NoUniqueFallbackMessageKey = "EasyMode_UniqueWeaponForge_NoUnique_Returned";
    private const string UniqueSuffix = "_Unique";

    [HarmonyPrefix]
    private static bool Prefix(
        RecipeDef recipeDef,
        Pawn worker,
        List<Thing> ingredients,
        Thing dominantIngredient,
        IBillGiver billGiver,
        Precept_ThingStyle precept,
        ThingStyleDef style,
        int? overrideGraphicIndex,
        ref IEnumerable<Thing> __result)
    {
        if (recipeDef?.defName != ConversionRecipeDefName)
        {
            return true;
        }

        __result = GenerateUniqueConversionProduct(recipeDef, worker, ingredients, dominantIngredient, precept, style, overrideGraphicIndex);
        return false;
    }

    private static IEnumerable<Thing> GenerateUniqueConversionProduct(
        RecipeDef recipeDef,
        Pawn worker,
        List<Thing> ingredients,
        Thing dominantIngredient,
        Precept_ThingStyle precept,
        ThingStyleDef style,
        int? overrideGraphicIndex)
    {
        Thing sourceWeapon = FindSourceWeapon(ingredients, dominantIngredient);
        if (sourceWeapon == null)
        {
            yield break;
        }

        ThingDef targetDef = ResolveTargetDefinition(sourceWeapon.def);
        bool canConvert = sourceWeapon.def.IsRangedWeapon && targetDef != null;

        Thing product;
        if (canConvert)
        {
            product = CreateConvertedWeapon(targetDef, sourceWeapon, worker, recipeDef, precept, style, overrideGraphicIndex);
        }
        else
        {
            product = CreateReturnedWeapon(sourceWeapon);
            if (worker?.Faction == Faction.OfPlayer)
            {
                Messages.Message(NoUniqueFallbackMessageKey.Translate(sourceWeapon.LabelCap), worker, MessageTypeDefOf.NeutralEvent);
            }
        }

        if (product != null)
        {
            yield return product;
        }
    }

    private static Thing FindSourceWeapon(List<Thing> ingredients, Thing dominantIngredient)
    {
        if (ingredients != null)
        {
            for (int i = 0; i < ingredients.Count; i++)
            {
                Thing ingredient = ingredients[i];
                if (ingredient?.def?.IsRangedWeapon == true)
                {
                    return ingredient;
                }
            }

            for (int i = 0; i < ingredients.Count; i++)
            {
                Thing ingredient = ingredients[i];
                if (ingredient?.def?.IsWeapon == true)
                {
                    return ingredient;
                }
            }
        }

        return dominantIngredient?.def?.IsWeapon == true ? dominantIngredient : null;
    }

    private static ThingDef ResolveTargetDefinition(ThingDef sourceDef)
    {
        if (sourceDef == null)
        {
            return null;
        }

        if (sourceDef.defName.EndsWith(UniqueSuffix, StringComparison.Ordinal))
        {
            return sourceDef;
        }

        return DefDatabase<ThingDef>.GetNamedSilentFail(sourceDef.defName + UniqueSuffix);
    }

    private static Thing CreateConvertedWeapon(
        ThingDef targetDef,
        Thing sourceWeapon,
        Pawn worker,
        RecipeDef recipeDef,
        Precept_ThingStyle precept,
        ThingStyleDef style,
        int? overrideGraphicIndex)
    {
        ThingDef stuff = targetDef.MadeFromStuff ? sourceWeapon.Stuff : null;
        Thing product = ThingMaker.MakeThing(targetDef, stuff);
        if (product.HitPoints > 0)
        {
            product.HitPoints = product.MaxHitPoints;
        }

        product.stackCount = 1;
        product.Notify_RecipeProduced(worker);
        return PostProcessProduct(product, recipeDef, worker, precept, style, overrideGraphicIndex);
    }

    private static Thing CreateReturnedWeapon(Thing sourceWeapon)
    {
        ThingDef stuff = sourceWeapon.def.MadeFromStuff ? sourceWeapon.Stuff : null;
        Thing returned = ThingMaker.MakeThing(sourceWeapon.def, stuff);
        returned.stackCount = 1;

        if (returned.HitPoints > 0 && sourceWeapon.HitPoints > 0)
        {
            returned.HitPoints = Mathf.Clamp(sourceWeapon.HitPoints, 1, returned.MaxHitPoints);
        }

        CompQuality sourceQuality = sourceWeapon.TryGetComp<CompQuality>();
        CompQuality returnedQuality = returned.TryGetComp<CompQuality>();
        if (sourceQuality != null && returnedQuality != null)
        {
            returnedQuality.SetQuality(sourceQuality.Quality, ArtGenerationContext.Colony);
        }

        returned.StyleDef = sourceWeapon.StyleDef;
        returned.StyleSourcePrecept = sourceWeapon.StyleSourcePrecept;
        return returned;
    }

    // Keep product handling aligned with vanilla recipe output behavior.
    private static Thing PostProcessProduct(
        Thing product,
        RecipeDef recipeDef,
        Pawn worker,
        Precept_ThingStyle precept,
        ThingStyleDef style,
        int? overrideGraphicIndex)
    {
        CompQuality compQuality = product.TryGetComp<CompQuality>();
        if (compQuality != null)
        {
            if (recipeDef.workSkill == null)
            {
                Log.Error(recipeDef + " needs workSkill because it creates a product with a quality.");
            }

            QualityCategory quality = QualityUtility.GenerateQualityCreatedByPawn(worker, recipeDef.workSkill);
            compQuality.SetQuality(quality, ArtGenerationContext.Colony);
        }

        CompArt compArt = product.TryGetComp<CompArt>();
        if (compArt != null)
        {
            compArt.JustCreatedBy(worker);
            if (compQuality != null && (int)compQuality.Quality >= 4)
            {
                TaleRecorder.RecordTale(TaleDefOf.CraftedArt, worker, product);
            }
        }

        if (compQuality != null)
        {
            QualityUtility.SendCraftNotification(product, worker);
        }

        if (worker?.Ideo != null)
        {
            product.StyleDef = worker.Ideo.GetStyleFor(product.def);
        }

        if (precept != null)
        {
            product.StyleSourcePrecept = precept;
        }
        else if (style != null)
        {
            product.StyleDef = style;
        }
        else if (!product.def.randomStyle.NullOrEmpty() && Rand.Chance(product.def.randomStyleChance))
        {
            product.SetStyleDef(product.def.randomStyle.RandomElementByWeight(x => x.Chance).StyleDef);
        }

        product.overrideGraphicIndex = overrideGraphicIndex;
        if (product.def.Minifiable)
        {
            product = product.MakeMinified();
        }

        return product;
    }
}
