using System;
using System.Collections.Generic;
using Verse;

namespace EasyMode
{
    /// <summary>
    /// A persisted claim on a dynamically distributed Hediff. Multiple providers can
    /// safely share one effect; the effect is removed only after the final claim ends.
    /// </summary>
    public class ManagedHediffLease : IExposable
    {
        public string providerId;
        public float severity = -1f;

        public ManagedHediffLease()
        {
        }

        public ManagedHediffLease(string providerId, float severity)
        {
            this.providerId = providerId;
            this.severity = severity;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref providerId, "providerId");
            Scribe_Values.Look(ref severity, "severity", -1f);
        }
    }

    public class HediffCompProperties_ManagedEffect : HediffCompProperties
    {
        public HediffCompProperties_ManagedEffect()
        {
            compClass = typeof(HediffComp_ManagedEffect);
        }
    }

    /// <summary>
    /// Marks an effect Hediff as lease-managed and stores its active providers.
    /// This component is injected into effect defs by distributor properties during
    /// def resolution, so content authors do not need to add it manually.
    /// </summary>
    public class HediffComp_ManagedEffect : HediffComp
    {
        private List<ManagedHediffLease> leases = new List<ManagedHediffLease>();
        private bool frameworkCreated;

        public bool FrameworkCreated => frameworkCreated;
        public int LeaseCount => leases?.Count ?? 0;

        public void Acquire(string providerId, float severity, bool adoptExisting)
        {
            if (providerId.NullOrEmpty())
            {
                return;
            }

            if (leases == null)
            {
                leases = new List<ManagedHediffLease>();
            }

            ManagedHediffLease existing = null;
            for (int i = 0; i < leases.Count; i++)
            {
                if (leases[i]?.providerId == providerId)
                {
                    existing = leases[i];
                    break;
                }
            }

            if (existing == null)
            {
                leases.Add(new ManagedHediffLease(providerId, severity));
            }
            else
            {
                existing.severity = severity;
            }

            if (adoptExisting)
            {
                frameworkCreated = true;
            }

            RefreshManagedSeverity();
        }

        public void MarkFrameworkCreated()
        {
            frameworkCreated = true;
        }

        public bool Release(string providerId)
        {
            if (leases != null && !providerId.NullOrEmpty())
            {
                for (int i = leases.Count - 1; i >= 0; i--)
                {
                    if (leases[i]?.providerId == providerId)
                    {
                        leases.RemoveAt(i);
                    }
                }
            }

            RefreshManagedSeverity();
            return frameworkCreated && (leases == null || leases.Count == 0);
        }

        private void RefreshManagedSeverity()
        {
            if (!frameworkCreated || parent == null || leases == null || leases.Count == 0)
            {
                return;
            }

            float requestedSeverity = -1f;
            for (int i = 0; i < leases.Count; i++)
            {
                if (leases[i] != null && leases[i].severity >= 0f)
                {
                    requestedSeverity = Math.Max(requestedSeverity, leases[i].severity);
                }
            }

            if (requestedSeverity >= 0f && Math.Abs(parent.Severity - requestedSeverity) > 0.0001f)
            {
                parent.Severity = requestedSeverity;
            }
        }

        public override string CompDebugString()
        {
            return $"Managed effect: created={frameworkCreated}, leases={LeaseCount}";
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref frameworkCreated, "frameworkCreated", false);
            Scribe_Collections.Look(ref leases, "leases", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && leases == null)
            {
                leases = new List<ManagedHediffLease>();
            }
        }
    }

    public static class ManagedHediffEffectUtility
    {
        private static readonly HashSet<string> ReportedInvalidEffects = new HashSet<string>();

        public static void EnsureManagedEffectDef(HediffDef effectDef)
        {
            if (effectDef == null)
            {
                return;
            }

            if (effectDef.comps == null)
            {
                effectDef.comps = new List<HediffCompProperties>();
            }

            if (effectDef.CompProps<HediffCompProperties_ManagedEffect>() == null)
            {
                effectDef.comps.Add(new HediffCompProperties_ManagedEffect());
            }
        }

        public static bool Acquire(
            Pawn pawn,
            HediffDef effectDef,
            BodyPartRecord part,
            string providerId,
            float severity = -1f,
            bool adoptExisting = false)
        {
            if (pawn?.health?.hediffSet == null || effectDef == null || providerId.NullOrEmpty())
            {
                return false;
            }

            Hediff effect = FindEffect(pawn, effectDef, part);
            bool created = false;
            if (effect == null)
            {
                effect = HediffMaker.MakeHediff(effectDef, pawn, part);
                HediffComp_ManagedEffect newMarker = effect.TryGetComp<HediffComp_ManagedEffect>();
                if (newMarker == null)
                {
                    ReportInvalidEffect(effectDef, "does not instantiate as HediffWithComps");
                    return false;
                }

                newMarker.MarkFrameworkCreated();
                if (severity >= 0f)
                {
                    effect.Severity = severity;
                }

                pawn.health.AddHediff(effect, part);
                created = true;
            }

            HediffComp_ManagedEffect marker = effect.TryGetComp<HediffComp_ManagedEffect>();
            if (marker == null)
            {
                ReportInvalidEffect(effectDef, "is missing HediffComp_ManagedEffect");
                return false;
            }

            marker.Acquire(providerId, severity, created || adoptExisting);
            return true;
        }

        public static void Release(Pawn pawn, HediffDef effectDef, BodyPartRecord part, string providerId)
        {
            if (pawn?.health?.hediffSet == null || effectDef == null || providerId.NullOrEmpty())
            {
                return;
            }

            Hediff effect = FindEffect(pawn, effectDef, part);
            HediffComp_ManagedEffect marker = effect?.TryGetComp<HediffComp_ManagedEffect>();
            if (marker != null && marker.Release(providerId))
            {
                pawn.health.RemoveHediff(effect);
            }
        }

        public static Hediff FindEffect(Pawn pawn, HediffDef effectDef, BodyPartRecord part)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null)
            {
                return null;
            }

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff candidate = hediffs[i];
                if (candidate.def == effectDef && candidate.Part == part)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void ReportInvalidEffect(HediffDef effectDef, string reason)
        {
            string key = effectDef.defName + ":" + reason;
            if (ReportedInvalidEffects.Add(key))
            {
                Log.Error($"[EasyMode] Managed effect {effectDef.defName} {reason}. The effect was not distributed.");
            }
        }
    }
}
