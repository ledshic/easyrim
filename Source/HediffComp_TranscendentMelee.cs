using Verse;

namespace EasyMode
{
    public class HediffCompProperties_TranscendentMelee : HediffCompProperties
    {
        public HediffCompProperties_TranscendentMelee()
        {
            this.compClass = typeof(HediffComp_TranscendentMelee);
        }

        public float armorPenetration = 0.4f;
        public float damageFactor = 0.35f;
        public float cooldownFactor = 0.5f;
    }

    public class HediffComp_TranscendentMelee : HediffComp
    {
        public HediffCompProperties_TranscendentMelee PropsTranscendentMelee => (HediffCompProperties_TranscendentMelee)props;
    }
}
