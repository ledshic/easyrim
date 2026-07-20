using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EasyMode
{
    public enum ConditionalStateRequirement
    {
        Any,
        Required,
        Forbidden
    }

    public class ConditionalHediffRequirement
    {
        public HediffDef hediff;
        public float minSeverity = 0f;
        public float maxSeverity = float.MaxValue;
        public int minStage = 0;
        public int maxStage = int.MaxValue;
        public bool mustBeVisible;

        public bool Matches(Pawn pawn)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediff == null || hediffs == null)
            {
                return false;
            }

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff candidate = hediffs[i];
                if (candidate.def == hediff &&
                    candidate.Severity >= minSeverity &&
                    candidate.Severity <= maxSeverity &&
                    candidate.CurStageIndex >= minStage &&
                    candidate.CurStageIndex <= maxStage &&
                    (!mustBeVisible || candidate.Visible))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class ConditionalHediffRule
    {
        public string key;
        public HediffDef activeHediff;
        public float activeSeverity = -1f;
        public bool attachToParentPart;
        public bool adoptExistingEffect;

        public List<ConditionalHediffRequirement> allHediffs;
        public List<ConditionalHediffRequirement> anyHediffs;
        public List<ConditionalHediffRequirement> noHediffs;

        public List<GeneDef> allGenes;
        public List<GeneDef> anyGenes;
        public List<GeneDef> noGenes;
        public bool requireActiveGenes = true;

        public List<ThingDef> allEquipment;
        public List<ThingDef> anyEquipment;
        public List<ThingDef> noEquipment;
        public List<string> allEquipmentTags;
        public List<string> anyEquipmentTags;
        public List<string> noEquipmentTags;
        public bool includeWeapons = true;
        public bool includeApparel = true;

        public List<JobDef> anyJobs;
        public List<JobDef> noJobs;

        public ConditionalStateRequirement drafted = ConditionalStateRequirement.Any;
        public ConditionalStateRequirement downed = ConditionalStateRequirement.Any;
        public ConditionalStateRequirement inMentalState = ConditionalStateRequirement.Any;
        public ConditionalStateRequirement inCombat = ConditionalStateRequirement.Any;
        public ConditionalStateRequirement moving = ConditionalStateRequirement.Any;
        public ConditionalStateRequirement asleep = ConditionalStateRequirement.Any;
        public ConditionalStateRequirement hasPrimaryEquipment = ConditionalStateRequirement.Any;

        public FloatRange healthPercent = new FloatRange(0f, 1f);

        public bool Matches(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            float currentHealth = pawn.health.summaryHealth.SummaryHealthPercent;
            if (!healthPercent.Includes(currentHealth))
            {
                return false;
            }

            if (!AllHediffsMatch(pawn, allHediffs) ||
                !AnyHediffMatches(pawn, anyHediffs) ||
                AnyHediffMatchesForbidden(pawn, noHediffs))
            {
                return false;
            }

            if (!AllGenesMatch(pawn, allGenes) ||
                !AnyGeneMatches(pawn, anyGenes) ||
                AnyGeneMatchesForbidden(pawn, noGenes))
            {
                return false;
            }

            if (!AllEquipmentMatches(pawn, allEquipment) ||
                !AnyEquipmentMatches(pawn, anyEquipment) ||
                AnyEquipmentMatchesForbidden(pawn, noEquipment) ||
                !AllEquipmentTagsMatch(pawn, allEquipmentTags) ||
                !AnyEquipmentTagMatches(pawn, anyEquipmentTags) ||
                AnyEquipmentTagMatchesForbidden(pawn, noEquipmentTags))
            {
                return false;
            }

            JobDef currentJob = pawn.CurJobDef;
            if (!anyJobs.NullOrEmpty() && !anyJobs.Contains(currentJob))
            {
                return false;
            }

            if (!noJobs.NullOrEmpty() && noJobs.Contains(currentJob))
            {
                return false;
            }

            return StateMatches(drafted, pawn.Drafted) &&
                   StateMatches(downed, pawn.Downed) &&
                   StateMatches(inMentalState, pawn.InMentalState) &&
                   StateMatches(inCombat, IsInCombat(pawn)) &&
                   StateMatches(moving, pawn.pather?.MovingNow == true) &&
                   StateMatches(asleep, !pawn.Awake()) &&
                   StateMatches(hasPrimaryEquipment, pawn.equipment?.Primary != null);
        }

        private bool AllHediffsMatch(Pawn pawn, List<ConditionalHediffRequirement> requirements)
        {
            if (requirements.NullOrEmpty())
            {
                return true;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i] == null || !requirements[i].Matches(pawn))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AnyHediffMatches(Pawn pawn, List<ConditionalHediffRequirement> requirements)
        {
            if (requirements.NullOrEmpty())
            {
                return true;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i]?.Matches(pawn) == true)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AnyHediffMatchesForbidden(Pawn pawn, List<ConditionalHediffRequirement> requirements)
        {
            if (requirements.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i]?.Matches(pawn) == true)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AllGenesMatch(Pawn pawn, List<GeneDef> genes)
        {
            if (genes.NullOrEmpty())
            {
                return true;
            }

            for (int i = 0; i < genes.Count; i++)
            {
                if (!HasGene(pawn, genes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AnyGeneMatches(Pawn pawn, List<GeneDef> genes)
        {
            if (genes.NullOrEmpty())
            {
                return true;
            }

            for (int i = 0; i < genes.Count; i++)
            {
                if (HasGene(pawn, genes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AnyGeneMatchesForbidden(Pawn pawn, List<GeneDef> genes)
        {
            if (genes.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < genes.Count; i++)
            {
                if (HasGene(pawn, genes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasGene(Pawn pawn, GeneDef geneDef)
        {
            if (geneDef == null || pawn.genes == null)
            {
                return false;
            }

            Gene gene = pawn.genes.GetGene(geneDef);
            return gene != null && (!requireActiveGenes || gene.Active);
        }

        private bool AllEquipmentMatches(Pawn pawn, List<ThingDef> defs)
        {
            if (defs.NullOrEmpty())
            {
                return true;
            }

            for (int i = 0; i < defs.Count; i++)
            {
                if (!HasEquipment(pawn, defs[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AnyEquipmentMatches(Pawn pawn, List<ThingDef> defs)
        {
            if (defs.NullOrEmpty())
            {
                return true;
            }

            for (int i = 0; i < defs.Count; i++)
            {
                if (HasEquipment(pawn, defs[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AnyEquipmentMatchesForbidden(Pawn pawn, List<ThingDef> defs)
        {
            if (defs.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < defs.Count; i++)
            {
                if (HasEquipment(pawn, defs[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AllEquipmentTagsMatch(Pawn pawn, List<string> tags)
        {
            if (tags.NullOrEmpty())
            {
                return true;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                if (!HasEquipmentTag(pawn, tags[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AnyEquipmentTagMatches(Pawn pawn, List<string> tags)
        {
            if (tags.NullOrEmpty())
            {
                return true;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                if (HasEquipmentTag(pawn, tags[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AnyEquipmentTagMatchesForbidden(Pawn pawn, List<string> tags)
        {
            if (tags.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                if (HasEquipmentTag(pawn, tags[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasEquipment(Pawn pawn, ThingDef def)
        {
            if (def == null)
            {
                return false;
            }

            if (includeWeapons && pawn.equipment != null)
            {
                List<ThingWithComps> equipment = pawn.equipment.AllEquipmentListForReading;
                for (int i = 0; i < equipment.Count; i++)
                {
                    if (equipment[i].def == def)
                    {
                        return true;
                    }
                }
            }

            if (includeApparel && pawn.apparel != null)
            {
                List<Apparel> apparel = pawn.apparel.WornApparel;
                for (int i = 0; i < apparel.Count; i++)
                {
                    if (apparel[i].def == def)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasEquipmentTag(Pawn pawn, string tag)
        {
            if (tag.NullOrEmpty())
            {
                return false;
            }

            if (includeWeapons && pawn.equipment != null)
            {
                List<ThingWithComps> equipment = pawn.equipment.AllEquipmentListForReading;
                for (int i = 0; i < equipment.Count; i++)
                {
                    if (equipment[i].def.weaponTags?.Contains(tag) == true)
                    {
                        return true;
                    }
                }
            }

            if (includeApparel && pawn.apparel != null)
            {
                List<Apparel> apparel = pawn.apparel.WornApparel;
                for (int i = 0; i < apparel.Count; i++)
                {
                    if (apparel[i].def.apparel?.tags?.Contains(tag) == true)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool StateMatches(ConditionalStateRequirement requirement, bool value)
        {
            switch (requirement)
            {
                case ConditionalStateRequirement.Required:
                    return value;
                case ConditionalStateRequirement.Forbidden:
                    return !value;
                default:
                    return true;
            }
        }

        private static bool IsInCombat(Pawn pawn)
        {
            if (pawn.Drafted || pawn.mindState?.enemyTarget != null)
            {
                return true;
            }

            JobDef job = pawn.CurJobDef;
            return job == JobDefOf.AttackMelee ||
                   job == JobDefOf.AttackStatic ||
                   job == JobDefOf.Wait_Combat;
        }
    }

    public class ConditionalHediffProfileDef : Def
    {
        public List<ConditionalHediffRule> rules = new List<ConditionalHediffRule>();

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (rules == null)
            {
                return;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                ManagedHediffEffectUtility.EnsureManagedEffectDef(rules[i]?.activeHediff);
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (rules.NullOrEmpty())
            {
                yield return $"{defName}: conditional Hediff profile has no rules";
                yield break;
            }

            HashSet<string> keys = new HashSet<string>();
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i]?.key.NullOrEmpty() != false)
                {
                    yield return $"{defName}: conditional rule {i} should define a stable key for save compatibility";
                }
                else if (!keys.Add(rules[i].key))
                {
                    yield return $"{defName}: duplicate conditional rule key '{rules[i].key}'";
                }

                string error = ValidateRule(rules[i], i, defName);
                if (error != null)
                {
                    yield return error;
                }
            }
        }

        internal static string ValidateRule(ConditionalHediffRule rule, int index, string ownerName)
        {
            if (rule?.activeHediff == null)
            {
                return $"{ownerName}: conditional rule {index} has no activeHediff";
            }

            if (!typeof(HediffWithComps).IsAssignableFrom(rule.activeHediff.hediffClass))
            {
                return $"{ownerName}: effect {rule.activeHediff.defName} must use a HediffWithComps-derived class";
            }

            return null;
        }
    }

    public class HediffCompProperties_ConditionalHediffDistributor : HediffCompProperties
    {
        public int checkIntervalTicks = 120;
        public List<ConditionalHediffProfileDef> profiles = new List<ConditionalHediffProfileDef>();
        public List<ConditionalHediffRule> rules = new List<ConditionalHediffRule>();

        private List<ConditionalHediffRule> resolvedRules;

        public List<ConditionalHediffRule> ResolvedRules
        {
            get
            {
                if (resolvedRules == null)
                {
                    BuildResolvedRules();
                }

                return resolvedRules;
            }
        }

        public HediffCompProperties_ConditionalHediffDistributor()
        {
            compClass = typeof(HediffComp_ConditionalHediffDistributor);
        }

        public override void ResolveReferences(HediffDef parent)
        {
            base.ResolveReferences(parent);
            BuildResolvedRules();

            for (int i = 0; i < resolvedRules.Count; i++)
            {
                ManagedHediffEffectUtility.EnsureManagedEffectDef(resolvedRules[i]?.activeHediff);
            }
        }

        public override IEnumerable<string> ConfigErrors(HediffDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (checkIntervalTicks <= 0)
            {
                yield return $"{parentDef.defName}: checkIntervalTicks must be greater than zero";
            }

            List<ConditionalHediffRule> allRules = ResolvedRules;
            if (allRules.NullOrEmpty())
            {
                yield return $"{parentDef.defName}: conditional distributor has no rules";
                yield break;
            }

            for (int i = 0; i < allRules.Count; i++)
            {
                string error = ConditionalHediffProfileDef.ValidateRule(allRules[i], i, parentDef.defName);
                if (error != null)
                {
                    yield return error;
                }
            }

            HashSet<string> keys = new HashSet<string>();
            for (int i = 0; i < allRules.Count; i++)
            {
                string key = allRules[i]?.key;
                if (key.NullOrEmpty())
                {
                    yield return $"{parentDef.defName}: conditional rule {i} should define a stable key for save compatibility";
                }
                else if (!keys.Add(key))
                {
                    yield return $"{parentDef.defName}: duplicate conditional rule key '{key}'";
                }
            }
        }

        private void BuildResolvedRules()
        {
            resolvedRules = new List<ConditionalHediffRule>();
            if (profiles != null)
            {
                for (int i = 0; i < profiles.Count; i++)
                {
                    if (profiles[i]?.rules != null)
                    {
                        resolvedRules.AddRange(profiles[i].rules);
                    }
                }
            }

            if (rules != null)
            {
                resolvedRules.AddRange(rules);
            }
        }
    }

    public class HediffComp_ConditionalHediffDistributor : HediffComp
    {
        private int activeRuleCount = -1;

        private HediffCompProperties_ConditionalHediffDistributor PropsDistributor =>
            (HediffCompProperties_ConditionalHediffDistributor)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            ReconcileRules();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Pawn pawn = parent?.pawn;
            if (pawn != null && pawn.IsHashIntervalTick(Math.Max(1, PropsDistributor.checkIntervalTicks)))
            {
                ReconcileRules();
            }
        }

        public override void CompPostPostRemoved()
        {
            ReleaseAllRules();
            base.CompPostPostRemoved();
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                List<ConditionalHediffRule> rules = PropsDistributor.ResolvedRules;
                if (parent?.pawn == null || rules.NullOrEmpty())
                {
                    return null;
                }

                if (activeRuleCount < 0)
                {
                    activeRuleCount = CountActiveRules(rules, parent.pawn);
                }

                return "EasyMode_AdaptiveModulesActive".Translate(activeRuleCount, rules.Count);
            }
        }

        public override string CompDebugString()
        {
            List<ConditionalHediffRule> rules = PropsDistributor.ResolvedRules;
            if (activeRuleCount < 0 && parent?.pawn != null)
            {
                activeRuleCount = CountActiveRules(rules, parent.pawn);
            }

            return $"Conditional distributor: {Math.Max(0, activeRuleCount)}/{rules?.Count ?? 0} active";
        }

        private void ReconcileRules()
        {
            Pawn pawn = parent?.pawn;
            List<ConditionalHediffRule> rules = PropsDistributor.ResolvedRules;
            if (pawn == null || rules.NullOrEmpty())
            {
                return;
            }

            int active = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                ConditionalHediffRule rule = rules[i];
                if (rule?.activeHediff == null)
                {
                    continue;
                }

                BodyPartRecord targetPart = rule.attachToParentPart ? parent.Part : null;
                string providerId = GetProviderId(i);
                if (rule.Matches(pawn))
                {
                    active++;
                    ManagedHediffEffectUtility.Acquire(
                        pawn,
                        rule.activeHediff,
                        targetPart,
                        providerId,
                        rule.activeSeverity,
                        rule.adoptExistingEffect);
                }
                else
                {
                    ManagedHediffEffectUtility.Release(pawn, rule.activeHediff, targetPart, providerId);
                }
            }

            activeRuleCount = active;
        }

        private void ReleaseAllRules()
        {
            Pawn pawn = parent?.pawn;
            List<ConditionalHediffRule> rules = PropsDistributor.ResolvedRules;
            if (pawn == null || rules.NullOrEmpty())
            {
                return;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                ConditionalHediffRule rule = rules[i];
                if (rule?.activeHediff != null)
                {
                    BodyPartRecord targetPart = rule.attachToParentPart ? parent.Part : null;
                    ManagedHediffEffectUtility.Release(pawn, rule.activeHediff, targetPart, GetProviderId(i));
                }
            }

            activeRuleCount = 0;
        }

        private string GetProviderId(int ruleIndex)
        {
            ConditionalHediffRule rule = PropsDistributor.ResolvedRules[ruleIndex];
            string stableRuleKey = rule?.key;
            if (stableRuleKey.NullOrEmpty())
            {
                stableRuleKey = $"{rule?.activeHediff?.defName ?? "missing"}:{ruleIndex}";
            }

            return $"{parent?.GetUniqueLoadID()}:conditional:{stableRuleKey}";
        }

        private static int CountActiveRules(List<ConditionalHediffRule> rules, Pawn pawn)
        {
            if (rules.NullOrEmpty() || pawn == null)
            {
                return 0;
            }

            int active = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i]?.Matches(pawn) == true)
                {
                    active++;
                }
            }

            return active;
        }
    }
}
