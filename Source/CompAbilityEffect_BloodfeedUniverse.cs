using RimWorld;
using Verse;

namespace EasyMode;

public class CompProperties_AbilityUniversalBloodfeed : CompProperties_AbilityBloodfeederBite
{
    public CompProperties_AbilityUniversalBloodfeed()
    {
        compClass = typeof(CompAbilityEffect_UniversalBloodfeed);
    }
}

public class CompAbilityEffect_UniversalBloodfeed : CompAbilityEffect_BloodfeederBite
{
    public new CompProperties_AbilityBloodfeederBite Props => (CompProperties_AbilityBloodfeederBite)props;

    public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
    {
        return Valid(target);
    }

    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        Pawn pawn = target.Pawn;
        if (pawn == null || pawn.Dead)
        {
            return false;
        }

        if (!pawn.RaceProps.IsFlesh)
        {
            if (throwMessages)
            {
                Messages.Message("MessageCannotUseOnNonBleeder".Translate(parent.def.Named("ABILITY")), pawn, MessageTypeDefOf.RejectInput, historical: false);
            }
            return false;
        }

        if (ModsConfig.AnomalyActive && pawn.IsMutant && !pawn.mutant.Def.canBleed)
        {
            if (throwMessages)
            {
                Messages.Message("MessageCannotUseOnNonBleeder".Translate(parent.def.Named("ABILITY")), pawn, MessageTypeDefOf.RejectInput, historical: false);
            }
            return false;
        }

        return true;
    }

    public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
    {
        Pawn pawn = target.Pawn;
        if (pawn == null)
        {
            return base.ExtraLabelMouseAttachment(target);
        }

        float bloodlossAfterBite = BloodlossAfterBite(pawn);
        if (bloodlossAfterBite >= HediffDefOf.BloodLoss.lethalSeverity)
        {
            return "WillKill".Translate();
        }

        if (HediffDefOf.BloodLoss.stages[HediffDefOf.BloodLoss.StageAtSeverity(bloodlossAfterBite)].lifeThreatening)
        {
            return "WillCauseSeriousBloodloss".Translate();
        }

        return null;
    }

    private float BloodlossAfterBite(Pawn target)
    {
        float value = Props.targetBloodLoss;
        Hediff bloodLoss = target.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
        if (bloodLoss != null)
        {
            value += bloodLoss.Severity;
        }

        return value;
    }
}
