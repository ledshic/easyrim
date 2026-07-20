using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EasyMode
{
    [DefOf]
    public static class EasyModeNeuralOverclockDefOf
    {
        public static HediffDef EasyMode_NeuralOverclock;
        public static HediffDef EasyMode_NeuralOverclockStrain;

        static EasyModeNeuralOverclockDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EasyModeNeuralOverclockDefOf));
        }
    }

    public class CompProperties_AbilityNeuralOverclockVisual : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityNeuralOverclockVisual()
        {
            compClass = typeof(CompAbilityEffect_NeuralOverclockVisual);
        }
    }

    public class CompAbilityEffect_NeuralOverclockVisual : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = parent.pawn;
            if (pawn?.Spawned != true)
                return;

            FleckMaker.ThrowLightningGlow(pawn.DrawPos, pawn.Map, 0.45f);
            for (int i = 0; i < 6; i++)
                FleckMaker.ThrowMicroSparks(pawn.DrawPos, pawn.Map);
        }
    }

    public class HediffCompProperties_NeuralOverclock : HediffCompProperties
    {
        public int damageIntervalTicks = 60;
        public IntRange damageRange = new IntRange(1, 2);
        public float internalPartChance = 0.01f;
        public int trailSampleIntervalTicks = 4;
        public int trailLifetimeTicks = 20;
        public int maxTrailSnapshots = 5;

        public HediffCompProperties_NeuralOverclock()
        {
            compClass = typeof(HediffComp_NeuralOverclock);
        }
    }

    public class HediffComp_NeuralOverclock : HediffComp
    {
        private struct TrailSnapshot
        {
            public Vector3 position;
            public Rot4 facing;
            public int createdTick;
        }

        private readonly List<TrailSnapshot> trail = new List<TrailSnapshot>();
        private readonly List<BodyPartRecord> outsideParts = new List<BodyPartRecord>();
        private readonly List<BodyPartRecord> insideParts = new List<BodyPartRecord>();

        private int ticksUntilDamage;
        private int lastTrailSampleTick = -99999;

        public HediffCompProperties_NeuralOverclock Props =>
            (HediffCompProperties_NeuralOverclock)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            ticksUntilDamage = Math.Max(1, Props.damageIntervalTicks);
            trail.Clear();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead)
                return;

            ticksUntilDamage--;
            if (ticksUntilDamage > 0)
                return;

            ticksUntilDamage = Math.Max(1, Props.damageIntervalTicks);
            ApplyOverclockStrain(pawn);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksUntilDamage, "ticksUntilDamage", 60);
        }

        public override void CompPostPostRemoved()
        {
            trail.Clear();
            base.CompPostPostRemoved();
        }

        private void ApplyOverclockStrain(Pawn pawn)
        {
            BodyPartRecord part;
            float amount;

            Rand.PushState(Gen.HashCombineInt(pawn.thingIDNumber, Find.TickManager.TicksGame));
            try
            {
                BuildPartPools(pawn);
                List<BodyPartRecord> pool;
                if (outsideParts.Count == 0)
                    pool = insideParts;
                else if (insideParts.Count > 0 && Rand.Chance(Props.internalPartChance))
                    pool = insideParts;
                else
                    pool = outsideParts;

                if (pool.Count == 0)
                    return;

                part = SelectPart(pool);
                amount = Props.damageRange.RandomInRange;
            }
            finally
            {
                Rand.PopState();
            }

            if (part == null || pawn.health.hediffSet.PartIsMissing(part))
                return;

            Hediff injury = HediffMaker.MakeHediff(
                EasyModeNeuralOverclockDefOf.EasyMode_NeuralOverclockStrain,
                pawn,
                part);
            injury.Severity = amount;
            pawn.health.AddHediff(injury, part);

            if (pawn.Spawned)
                FleckMaker.ThrowMicroSparks(pawn.DrawPos, pawn.Map);
        }

        private void BuildPartPools(Pawn pawn)
        {
            outsideParts.Clear();
            insideParts.Clear();

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (!CanStrainPart(pawn, part))
                    continue;

                if (part.depth == BodyPartDepth.Inside)
                {
                    insideParts.Add(part);
                }
                else if (part.depth == BodyPartDepth.Outside && IsPreferredOutsidePart(part))
                {
                    outsideParts.Add(part);
                }
            }

            // Non-humanoid fallback: retain the same outside/inside split while
            // allowing a race whose limbs do not use vanilla limb tags.
            if (outsideParts.Count == 0)
            {
                foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
                {
                    if (part.depth == BodyPartDepth.Outside && CanStrainPart(pawn, part) &&
                        !part.IsInGroup(BodyPartGroupDefOf.FullHead))
                    {
                        outsideParts.Add(part);
                    }
                }
            }
        }

        private static bool CanStrainPart(Pawn pawn, BodyPartRecord part)
        {
            return part?.def != null &&
                   part.def.alive &&
                   !part.def.conceptual &&
                   part.def.destroyableByDamage &&
                   pawn.health.hediffSet.GetPartHealth(part) > 0f &&
                   !pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part);
        }

        private static bool IsPreferredOutsidePart(BodyPartRecord part)
        {
            if (part.IsCorePart || part.def == BodyPartDefOf.Torso)
                return true;

            List<BodyPartTagDef> tags = part.def.tags;
            return tags.Contains(BodyPartTagDefOf.ManipulationLimbCore) ||
                   tags.Contains(BodyPartTagDefOf.ManipulationLimbSegment) ||
                   tags.Contains(BodyPartTagDefOf.MovingLimbCore) ||
                   tags.Contains(BodyPartTagDefOf.MovingLimbSegment);
        }

        private static BodyPartRecord SelectPart(List<BodyPartRecord> parts)
        {
            float totalWeight = 0f;
            for (int i = 0; i < parts.Count; i++)
                totalWeight += PartWeight(parts[i]);

            float value = Rand.Value * totalWeight;
            for (int i = 0; i < parts.Count; i++)
            {
                value -= PartWeight(parts[i]);
                if (value <= 0f)
                    return parts[i];
            }

            return parts[parts.Count - 1];
        }

        private static float PartWeight(BodyPartRecord part)
        {
            if (part.IsCorePart || part.def == BodyPartDefOf.Torso)
                return 2f;
            if (part.depth == BodyPartDepth.Inside)
                return 1f;
            if (part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore) ||
                part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore))
            {
                return 1.25f;
            }
            return 1f;
        }

        public void DrawAfterimages(Vector3 currentDrawLoc)
        {
            Pawn pawn = Pawn;
            if (pawn?.Spawned != true || pawn.Dead || pawn.GetPosture() != PawnPosture.Standing)
                return;

            int now = Find.TickManager.TicksGame;
            PruneTrail(now);

            PawnRenderTree renderTree = pawn.Drawer.renderer.renderTree;
            PawnRenderFlags flags = PawnRenderFlags.Invisible |
                                    PawnRenderFlags.Clothes |
                                    PawnRenderFlags.Headgear |
                                    PawnRenderFlags.NeverAimWeapon;
            if (!pawn.health.hediffSet.HasHead)
                flags |= PawnRenderFlags.HeadStump;

            renderTree.EnsureInitialized(flags);
            for (int i = 0; i < trail.Count; i++)
            {
                TrailSnapshot snapshot = trail[i];
                float age = now - snapshot.createdTick;
                float alpha = Mathf.Clamp01(1f - age / Math.Max(1f, Props.trailLifetimeTicks)) * 0.9f;
                if (alpha <= 0.01f)
                    continue;

                Vector3 position = snapshot.position;
                position.y = currentDrawLoc.y - 0.004f;

                PawnDrawParms parms = PawnDrawParms.DefaultFor(pawn);
                parms.matrix = Matrix4x4.TRS(
                    position + pawn.ageTracker.CurLifeStage.bodyDrawOffset,
                    Quaternion.identity,
                    Vector3.one);
                parms.facing = snapshot.facing;
                parms.rotDrawMode = pawn.Drawer.renderer.CurRotDrawMode;
                parms.posture = PawnPosture.Standing;
                parms.flags = flags;
                parms.tint = new Color(1f, 1f, 1f, alpha);

                renderTree.ParallelPreDraw(parms);
                renderTree.Draw(parms);
            }
        }

        public void RecordTrailPosition(Vector3 drawLoc)
        {
            Pawn pawn = Pawn;
            if (pawn?.Spawned != true || pawn.Dead || pawn.GetPosture() != PawnPosture.Standing ||
                pawn.pather?.MovingNow != true)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (now < lastTrailSampleTick + Math.Max(1, Props.trailSampleIntervalTicks))
                return;

            if (trail.Count > 0)
            {
                Vector3 delta = drawLoc - trail[trail.Count - 1].position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.01f)
                    return;
            }

            lastTrailSampleTick = now;
            trail.Add(new TrailSnapshot
            {
                position = drawLoc,
                facing = pawn.Rotation,
                createdTick = now
            });

            while (trail.Count > Math.Max(1, Props.maxTrailSnapshots))
                trail.RemoveAt(0);
        }

        private void PruneTrail(int now)
        {
            int lifetime = Math.Max(1, Props.trailLifetimeTicks);
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                if (now - trail[i].createdTick >= lifetime)
                    trail.RemoveAt(i);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt))]
    public static class Patch_Pawn_DynamicDrawPhaseAt_NeuralOverclock
    {
        public static void Postfix(Pawn __instance, DrawPhase phase, Vector3 drawLoc)
        {
            if (phase != DrawPhase.Draw || __instance?.health?.hediffSet == null)
                return;

            Hediff hediff = __instance.health.hediffSet.GetFirstHediffOfDef(
                EasyModeNeuralOverclockDefOf.EasyMode_NeuralOverclock);
            HediffComp_NeuralOverclock comp = hediff?.TryGetComp<HediffComp_NeuralOverclock>();
            if (comp == null)
                return;

            comp.DrawAfterimages(drawLoc);
            comp.RecordTrailPosition(drawLoc);
        }
    }
}
