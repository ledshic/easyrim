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
            
            // Give hediffs immediately when the component is created if severity is already at target
            if (parent.Severity >= Props.atSeverity)
            {
                GiveHediffs();
            }
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            
            // Check if we should give hediffs after damage
            if (!hediffsGiven && parent.Severity >= Props.atSeverity)
            {
                GiveHediffs();
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (parent.pawn == null || hediffsGiven)
            {
                return;
            }

            // Check if we've reached the required severity
            if (parent.Severity >= Props.atSeverity)
            {
                GiveHediffs();
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

            // Remove this hediff if configured to disappear after giving
            if (Props.disappearsAfterGiving)
            {
                parent.pawn.health.RemoveHediff(parent);
            }
        }

        private void ApplyHediff(HediffToGive hediffToGive)
        {
            Pawn target = parent.pawn;
            if (target == null)
            {
                return;
            }

            // Handle replacing existing hediffs (like the original code)
            bool shouldReplace = hediffToGive.replaceExisting || Props.replaceExisting;
            if (shouldReplace)
            {
                Hediff existingHediff = target.health.hediffSet.GetFirstHediffOfDef(hediffToGive.hediffDef);
                if (existingHediff != null)
                {
                    target.health.RemoveHediff(existingHediff);
                }
            }
            else
            {
                // If not replacing and hediff already exists, skip
                if (target.health.hediffSet.HasHediff(hediffToGive.hediffDef))
                {
                    return;
                }
            }

            // Determine body part (following original pattern)
            BodyPartRecord bodyPart = null;
            if (hediffToGive.onlyBrain || Props.onlyBrain)
            {
                bodyPart = target.health.hediffSet.GetBrain();
            }
            else if (hediffToGive.bodyPart != null)
            {
                bodyPart = target.RaceProps.body.AllParts.FirstOrDefault(x => x.def == hediffToGive.bodyPart);
            }

            // Create and apply the hediff (following original pattern)
            Hediff hediff = HediffMaker.MakeHediff(hediffToGive.hediffDef, target, bodyPart);
            
            // Set severity if specified (following original pattern)
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
