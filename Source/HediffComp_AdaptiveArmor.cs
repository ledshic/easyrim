using Verse;
using RimWorld;
using System;

namespace EasyMode
{
    public class HediffCompProperties_AdaptiveArmor : HediffCompProperties
    {
        public int tickInterval = 60;
        
        // Health thresholds for each stage (values between 0.0 and 1.0)
        public float stage1Threshold = 1.0f;    // 100% health - Stage 1 (active)
        public float stage2Threshold = 0.85f;   // 85% health - Stage 2 (alert)
        public float stage3Threshold = 0.70f;   // 70% health - Stage 3 (defensive)
        public float stage4Threshold = 0.55f;   // 55% health - Stage 4 (emergency)
        // Stage 5 (fortress) is anything below stage4Threshold

        public HediffCompProperties_AdaptiveArmor()
        {
            this.compClass = typeof(HediffComp_AdaptiveArmor);
        }
    }

    public class HediffComp_AdaptiveArmor : HediffComp
    {
        public HediffCompProperties_AdaptiveArmor Props => (HediffCompProperties_AdaptiveArmor)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (parent.pawn == null)
            {
                return;
            }

            if (parent.pawn.IsHashIntervalTick(Props.tickInterval))
            {
                // Calculate health percentage
                float healthPercent = parent.pawn.health.summaryHealth.SummaryHealthPercent;
                
                // Determine target stage based on health percentage
                float targetSeverity = CalculateTargetSeverity(healthPercent);
                
                // Update severity to match target stage
                parent.Severity = targetSeverity;
            }
        }

        private float CalculateTargetSeverity(float healthPercent)
        {
            // Stage 1: Above stage1Threshold (default 100% health)
            if (healthPercent >= Props.stage1Threshold)
                return 1.0f;
            
            // Stage 2: Above stage2Threshold (default 85% health)
            if (healthPercent >= Props.stage2Threshold)
                return 2.0f;
            
            // Stage 3: Above stage3Threshold (default 70% health)
            if (healthPercent >= Props.stage3Threshold)
                return 3.0f;
            
            // Stage 4: Above stage4Threshold (default 55% health)
            if (healthPercent >= Props.stage4Threshold)
                return 4.0f;
            
            // Stage 5: Below stage4Threshold
            return 5.0f;
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (parent.pawn == null)
                    return null;
                
                float healthPercent = parent.pawn.health.summaryHealth.SummaryHealthPercent;
                return $"{(healthPercent * 100f):F0}% health";
            }
        }
    }
}
