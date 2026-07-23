using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EasyMode
{
    public class HediffCompProperties_GiveMultipleHediffs : HediffCompProperties
    {
        public List<HediffToGive> hediffsToGive = new List<HediffToGive>();
        public float atSeverity = 1.0f;
        public bool disappearsAfterGiving;
        public bool replaceExisting;
        public bool onlyBrain;
        public bool maintainGivenHediffs = true;
        public int maintainCheckIntervalTicks = 120;

        // Managed mode gives persistent effects proper ownership and cleanup.
        public bool removeGivenHediffsOnRemoval = true;
        public bool adoptExistingHediffs = true;

        public HediffCompProperties_GiveMultipleHediffs()
        {
            compClass = typeof(HediffComp_GiveMultipleHediffs);
        }

        public override void ResolveReferences(HediffDef parent)
        {
            base.ResolveReferences(parent);
            if (hediffsToGive == null)
            {
                return;
            }

            for (int i = 0; i < hediffsToGive.Count; i++)
            {
                ManagedHediffEffectUtility.EnsureManagedEffectDef(hediffsToGive[i]?.hediffDef);
            }
        }

        public override IEnumerable<string> ConfigErrors(HediffDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (maintainCheckIntervalTicks <= 0)
            {
                yield return $"{parentDef.defName}: maintainCheckIntervalTicks must be greater than zero";
            }

            if (hediffsToGive.NullOrEmpty())
            {
                yield return $"{parentDef.defName}: GiveMultipleHediffs has no effects";
                yield break;
            }

            for (int i = 0; i < hediffsToGive.Count; i++)
            {
                HediffDef effectDef = hediffsToGive[i]?.hediffDef;
                if (effectDef == null)
                {
                    yield return $"{parentDef.defName}: effect entry {i} has no hediffDef";
                }
                else if (!typeof(HediffWithComps).IsAssignableFrom(effectDef.hediffClass))
                {
                    yield return $"{parentDef.defName}: effect {effectDef.defName} must use a HediffWithComps-derived class";
                }
            }
        }
    }

    public class HediffToGive
    {
        public string key;
        public HediffDef hediffDef;
        public float severity = -1f;
        public BodyPartDef bodyPart;
        public bool applyToAllMatchingBodyParts;
        public bool onlyBrain;
        public bool replaceExisting;
        public bool attachToDistributorPart;
    }

    /// <summary>
    /// Persistent multi-effect distributor. Effects are leased through the shared
    /// managed-effect layer, allowing multiple controllers to safely share them.
    /// </summary>
    public class HediffComp_GiveMultipleHediffs : HediffComp
    {
        private HediffCompProperties_GiveMultipleHediffs Props =>
            (HediffCompProperties_GiveMultipleHediffs)props;

        // Kept for backward-compatible loading of existing saves.
        private bool hediffsGiven;

        private bool PersistentManagedMode => Props.maintainGivenHediffs && !Props.disappearsAfterGiving;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            Reconcile();
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            Reconcile();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Pawn pawn = parent?.pawn;
            if (pawn != null && pawn.IsHashIntervalTick(Math.Max(1, Props.maintainCheckIntervalTicks)))
            {
                Reconcile();
            }
        }

        public override void CompPostPostRemoved()
        {
            if (PersistentManagedMode && Props.removeGivenHediffsOnRemoval)
            {
                ReleaseAll();
            }

            base.CompPostPostRemoved();
        }

        private void Reconcile()
        {
            Pawn pawn = parent?.pawn;
            if (pawn == null || Props.hediffsToGive.NullOrEmpty())
            {
                return;
            }

            if (parent.Severity < Props.atSeverity)
            {
                if (PersistentManagedMode && Props.removeGivenHediffsOnRemoval)
                {
                    ReleaseAll();
                }

                return;
            }

            if (Props.disappearsAfterGiving)
            {
                if (!hediffsGiven)
                {
                    GiveOneShotEffects();
                    hediffsGiven = true;
                    pawn.health.RemoveHediff(parent);
                }

                return;
            }

            if (!Props.maintainGivenHediffs && hediffsGiven)
            {
                return;
            }

            for (int i = 0; i < Props.hediffsToGive.Count; i++)
            {
                HediffToGive entry = Props.hediffsToGive[i];
                if (entry?.hediffDef == null)
                {
                    continue;
                }

                List<BodyPartRecord> parts = ResolveTargetParts(entry);
                if (parts.Count == 0)
                {
                    continue;
                }

                for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                {
                    BodyPartRecord part = parts[partIndex];
                    ManagedHediffEffectUtility.Acquire(
                        pawn,
                        entry.hediffDef,
                        part,
                        GetProviderId(i, part),
                        entry.severity,
                        Props.adoptExistingHediffs || Props.replaceExisting || entry.replaceExisting);
                }
            }

            hediffsGiven = true;
        }

        private void GiveOneShotEffects()
        {
            Pawn pawn = parent.pawn;
            for (int i = 0; i < Props.hediffsToGive.Count; i++)
            {
                HediffToGive entry = Props.hediffsToGive[i];
                if (entry?.hediffDef == null)
                {
                    continue;
                }

                List<BodyPartRecord> parts = ResolveTargetParts(entry);
                for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                {
                    BodyPartRecord part = parts[partIndex];
                    if (!(Props.replaceExisting || entry.replaceExisting) &&
                        ManagedHediffEffectUtility.FindEffect(pawn, entry.hediffDef, part) != null)
                    {
                        continue;
                    }

                    Hediff effect = HediffMaker.MakeHediff(entry.hediffDef, pawn, part);
                    if (entry.severity >= 0f)
                    {
                        effect.Severity = entry.severity;
                    }

                    pawn.health.AddHediff(effect, part);
                }
            }
        }

        private void ReleaseAll()
        {
            Pawn pawn = parent?.pawn;
            if (pawn == null || Props.hediffsToGive.NullOrEmpty())
            {
                return;
            }

            for (int i = 0; i < Props.hediffsToGive.Count; i++)
            {
                HediffToGive entry = Props.hediffsToGive[i];
                if (entry?.hediffDef == null)
                {
                    continue;
                }

                List<BodyPartRecord> parts = ResolveTargetParts(entry, includeMissingParts: true);
                for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                {
                    BodyPartRecord part = parts[partIndex];
                    ManagedHediffEffectUtility.Release(
                        pawn,
                        entry.hediffDef,
                        part,
                        GetProviderId(i, part));
                }
            }
        }

        private List<BodyPartRecord> ResolveTargetParts(HediffToGive entry, bool includeMissingParts = false)
        {
            Pawn pawn = parent?.pawn;
            List<BodyPartRecord> result = new List<BodyPartRecord>();
            if (pawn?.health?.hediffSet == null)
            {
                return result;
            }

            if (entry.attachToDistributorPart)
            {
                result.Add(parent.Part);
                return result;
            }

            if (entry.onlyBrain || Props.onlyBrain)
            {
                BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
                if (brain != null)
                {
                    result.Add(brain);
                }

                return result;
            }

            if (entry.bodyPart == null)
            {
                result.Add(null);
                return result;
            }

            IEnumerable<BodyPartRecord> parts = includeMissingParts
                ? pawn.RaceProps.body.AllParts
                : pawn.health.hediffSet.GetNotMissingParts();

            foreach (BodyPartRecord part in parts.Where(part => part.def == entry.bodyPart))
            {
                result.Add(part);
                if (!entry.applyToAllMatchingBodyParts)
                {
                    break;
                }
            }

            return result;
        }

        private string GetProviderId(int entryIndex, BodyPartRecord part)
        {
            HediffToGive entry = Props.hediffsToGive[entryIndex];
            int partIndex = part?.Index ?? -1;
            string stableEntryKey = entry?.key;
            if (stableEntryKey.NullOrEmpty())
            {
                stableEntryKey = $"{entry?.hediffDef?.defName ?? "missing"}:{entry?.bodyPart?.defName ?? "global"}:{partIndex}";
            }

            return $"{parent?.GetUniqueLoadID()}:multiple:{stableEntryKey}";
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref hediffsGiven, "hediffsGiven", false);
        }
    }
}
