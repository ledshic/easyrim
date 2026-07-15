using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

namespace EasyMode
{
    public class HediffCompProperties_GiveMultipleHediffs : HediffCompProperties
    {
        public List<HediffToGive> hediffsToGive = new List<HediffToGive>();
        public float atSeverity = 1.0f;
        public bool disappearsAfterGiving = false;
        public bool replaceExisting = false;
        public bool onlyBrain = false;
        public bool maintainGivenHediffs = true;
        public int maintainCheckIntervalTicks = 120;

        public HediffCompProperties_GiveMultipleHediffs()
        {
            this.compClass = typeof(HediffComp_GiveMultipleHediffs);
        }
    }

    public class HediffToGive
    {
        public HediffDef hediffDef;
        public float severity = -1f; // -1 means use default severity
        public BodyPartDef bodyPart;
        public bool applyToAllMatchingBodyParts = false;
        public bool onlyBrain = false;
        public bool replaceExisting = false;
    }

    public class HediffComp_GiveMultipleHediffs : HediffComp
    {
        public HediffCompProperties_GiveMultipleHediffs Props => (HediffCompProperties_GiveMultipleHediffs)this.props;
        private bool hediffsGiven = false;

        public override void CompPostMake()
        {
            base.CompPostMake();

            if (parent.Severity >= Props.atSeverity)
            {
                GiveHediffs();
            }
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);

            if (!hediffsGiven && parent.Severity >= Props.atSeverity)
            {
                GiveHediffs();
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (parent.pawn == null)
            {
                return;
            }

            if (parent.Severity >= Props.atSeverity)
            {
                if (!hediffsGiven)
                {
                    GiveHediffs();
                }
                else if (!Props.disappearsAfterGiving && Props.maintainGivenHediffs &&
                         parent.pawn.IsHashIntervalTick(Props.maintainCheckIntervalTicks))
                {
                    EnsureHediffsPresent();
                }
            }
        }

        private void GiveHediffs()
        {
            if (hediffsGiven || Props.hediffsToGive == null || Props.hediffsToGive.Count == 0)
            {
                return;
            }

            foreach (var hediffToGive in Props.hediffsToGive)
            {
                if (hediffToGive.hediffDef == null)
                {
                    continue;
                }

                ApplyHediff(hediffToGive);
            }

            hediffsGiven = true;

            if (Props.disappearsAfterGiving)
            {
                parent.pawn.health.RemoveHediff(parent);
            }
        }

        private void EnsureHediffsPresent()
        {
            if (Props.hediffsToGive == null || Props.hediffsToGive.Count == 0)
            {
                return;
            }

            foreach (var hediffToGive in Props.hediffsToGive)
            {
                if (hediffToGive?.hediffDef == null)
                {
                    continue;
                }

                if (IsHediffEntryMissing(hediffToGive))
                {
                    ApplyHediff(hediffToGive);
                }
            }
        }

        private bool IsHediffEntryMissing(HediffToGive hediffToGive)
        {
            Pawn target = parent.pawn;
            if (target == null)
            {
                return false;
            }

            if (hediffToGive.onlyBrain || Props.onlyBrain)
            {
                BodyPartRecord brain = target.health.hediffSet.GetBrain();
                if (brain == null)
                {
                    return false;
                }

                return !target.health.hediffSet.hediffs.Any(h => h.def == hediffToGive.hediffDef && h.Part == brain);
            }

            if (hediffToGive.bodyPart != null)
            {
                var matchingParts = target.health.hediffSet.GetNotMissingParts()
                    .Where(x => x.def == hediffToGive.bodyPart)
                    .ToList();
                if (!matchingParts.Any())
                {
                    return false;
                }

                if (hediffToGive.applyToAllMatchingBodyParts)
                {
                    return matchingParts.Any(part =>
                        !target.health.hediffSet.hediffs.Any(h => h.def == hediffToGive.hediffDef && h.Part == part));
                }

                BodyPartRecord first = matchingParts[0];
                return !target.health.hediffSet.hediffs.Any(h => h.def == hediffToGive.hediffDef && h.Part == first);
            }

            return !target.health.hediffSet.HasHediff(hediffToGive.hediffDef);
        }

        private void ApplyHediff(HediffToGive hediffToGive)
        {
            Pawn target = parent.pawn;
            if (target == null)
            {
                return;
            }

            bool shouldReplace = hediffToGive.replaceExisting || Props.replaceExisting;
            if (hediffToGive.onlyBrain || Props.onlyBrain)
            {
                BodyPartRecord brain = target.health.hediffSet.GetBrain();
                ApplyHediffToPart(target, hediffToGive, brain, shouldReplace);
                return;
            }

            if (hediffToGive.bodyPart != null)
            {
                var matchingParts = target.health.hediffSet.GetNotMissingParts()
                    .Where(x => x.def == hediffToGive.bodyPart)
                    .ToList();

                if (!matchingParts.Any())
                {
                    return;
                }

                if (hediffToGive.applyToAllMatchingBodyParts)
                {
                    foreach (BodyPartRecord part in matchingParts)
                    {
                        ApplyHediffToPart(target, hediffToGive, part, shouldReplace);
                    }
                }
                else
                {
                    ApplyHediffToPart(target, hediffToGive, matchingParts[0], shouldReplace);
                }

                return;
            }

            ApplyHediffToPart(target, hediffToGive, null, shouldReplace);
        }

        private void ApplyHediffToPart(Pawn target, HediffToGive hediffToGive, BodyPartRecord bodyPart, bool shouldReplace)
        {
            if (shouldReplace)
            {
                var existing = target.health.hediffSet.hediffs
                    .Where(h => h.def == hediffToGive.hediffDef && h.Part == bodyPart)
                    .ToList();

                foreach (Hediff existingHediff in existing)
                {
                    target.health.RemoveHediff(existingHediff);
                }
            }
            else
            {
                bool alreadyExists = target.health.hediffSet.hediffs
                    .Any(h => h.def == hediffToGive.hediffDef && h.Part == bodyPart);
                if (alreadyExists)
                {
                    return;
                }
            }

            Hediff hediff = HediffMaker.MakeHediff(hediffToGive.hediffDef, target, bodyPart);

            if (hediffToGive.severity >= 0f)
            {
                hediff.Severity = hediffToGive.severity;
            }

            target.health.AddHediff(hediff, bodyPart);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref hediffsGiven, "hediffsGiven", false);
        }
    }
}
