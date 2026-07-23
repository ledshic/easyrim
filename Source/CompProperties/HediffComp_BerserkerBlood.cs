using Verse;
using RimWorld;

namespace EasyMode
{
    public class HediffCompProperties_BerserkerBlood : HediffCompProperties_StageByHealth
    {
        public HediffCompProperties_BerserkerBlood()
        {
            this.compClass = typeof(HediffComp_BerserkerBlood);
        }
    }

    public class HediffComp_BerserkerBlood : HediffComp_StageByHealthBase
    {
        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (parent?.pawn == null)
                    return null;
                int hp = (int)(parent.pawn.health.summaryHealth.SummaryHealthPercent * 100f + 0.5f);
                return "BerserkerBlood_Bracket".Translate(hp);
            }
        }
    }
}
