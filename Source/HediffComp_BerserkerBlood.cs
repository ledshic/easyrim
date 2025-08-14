using Verse;
using RimWorld;

namespace EasyMode
{
    // 狂战士之血：根据生命值损失提升恢复相关效果的阶段用组件
    public class HediffCompProperties_BerserkerBlood : HediffCompProperties
    {
        public int tickInterval = 60;
        // 与 AdaptiveArmor 相同的分段阈值（按生命值百分比），便于在 XML 中用 minSeverity 做多阶段数值
        public float stage1Threshold = 1.0f;   // >=100% → 阶段1
        public float stage2Threshold = 0.85f;  // >=85%  → 阶段2
        public float stage3Threshold = 0.70f;  // >=70%  → 阶段3
        public float stage4Threshold = 0.55f;  // >=55%  → 阶段4
        // <55% → 阶段5

        public HediffCompProperties_BerserkerBlood()
        {
            this.compClass = typeof(HediffComp_BerserkerBlood);
        }
    }

    public class HediffComp_BerserkerBlood : HediffComp
    {
        public HediffCompProperties_BerserkerBlood Props => (HediffCompProperties_BerserkerBlood)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (parent?.pawn == null)
                return;

            if (parent.pawn.IsHashIntervalTick(Props.tickInterval))
            {
                float healthPercent = parent.pawn.health.summaryHealth.SummaryHealthPercent;
                float targetSeverity = CalculateTargetSeverity(healthPercent);
                parent.Severity = targetSeverity;
            }
        }

        private float CalculateTargetSeverity(float healthPercent)
        {
            if (healthPercent >= Props.stage1Threshold) return 1.0f;
            if (healthPercent >= Props.stage2Threshold) return 2.0f;
            if (healthPercent >= Props.stage3Threshold) return 3.0f;
            if (healthPercent >= Props.stage4Threshold) return 4.0f;
            return 5.0f;
        }

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
