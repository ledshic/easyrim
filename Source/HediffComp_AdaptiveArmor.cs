using Verse;
using RimWorld;
using System;

namespace EasyMode
{
    public class HediffCompProperties_AdaptiveArmor : HediffCompProperties_StageByHealth
    {
        public HediffCompProperties_AdaptiveArmor()
        {
            this.compClass = typeof(HediffComp_AdaptiveArmor);
        }
    }

    public class HediffComp_AdaptiveArmor : HediffComp_StageByHealthBase
    {
        public HediffCompProperties_AdaptiveArmor PropsAA => (HediffCompProperties_AdaptiveArmor)this.props;
    }
}
