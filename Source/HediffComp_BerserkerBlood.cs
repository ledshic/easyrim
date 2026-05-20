using Verse;
using RimWorld;

namespace EasyMode
{
    // 狂战士之血：根据生命值损失提升恢复相关效果的阶段用组件
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
                // Keyed: BerserkerBlood_Bracket, e.g., "Berserker's Blood {0}% health" / "狂战士之血 {0}% 生命"
                return "BerserkerBlood_Bracket".Translate(hp);
            }
        }
    }
}
