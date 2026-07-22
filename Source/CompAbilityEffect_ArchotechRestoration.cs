using RimWorld;
using Verse;

namespace EasyMode;

public class CompProperties_ArchotechRestoration : CompProperties_AbilityEffect
{
    public CompProperties_ArchotechRestoration()
    {
        compClass = typeof(CompAbilityEffect_ArchotechRestoration);
    }
}

public class CompAbilityEffect_ArchotechRestoration : CompAbilityEffect
{
    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        Pawn pawn = target.Pawn;
        if (pawn == null)
        {
            return false;
        }

        bool canHeal = HealthUtility.TryGetWorstHealthCondition(pawn, out _, out _);
        if (!canHeal && throwMessages)
        {
            Messages.Message(string.Format("{0}: {1}", "CannotUseAbility".Translate(parent.def.label), "AbilityCannotCastNoHealableInjury".Translate(pawn.Named("PAWN")).Resolve().StripTags()), pawn, MessageTypeDefOf.RejectInput, historical: false);
        }

        return canHeal;
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);

        Pawn pawn = target.Pawn;
        if (pawn == null)
        {
            return;
        }

        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff.Bleeding)
            {
                hediff.Tended(1f, 1f, 1);
            }
        }

        HealthUtility.FixWorstHealthCondition(pawn);
    }

    public override bool AICanTargetNow(LocalTargetInfo target)
    {
        return false;
    }
}
