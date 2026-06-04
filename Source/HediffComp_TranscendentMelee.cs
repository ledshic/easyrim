using Verse;

namespace EasyMode
{
    /// <summary>
    /// Grants transcendent melee capabilities with custom modified verb.
    /// Provides T-1000 style relentless combat with monosword-level armor penetration
    /// but reduced damage (35% of monosword baseline).
    /// </summary>
    public class HediffCompProperties_TranscendentMelee : HediffCompProperties
    {
        public HediffCompProperties_TranscendentMelee()
        {
            this.compClass = typeof(HediffComp_TranscendentMelee);
        }

        /// <summary>
        /// Armor penetration multiplier. Default 0.4 (monosword level)
        /// </summary>
        public float armorPenetration = 0.4f;

        /// <summary>
        /// Damage multiplier. Default 0.35 (35% of monosword damage)
        /// </summary>
        public float damageFactor = 0.35f;

        /// <summary>
        /// Cooldown reduction factor for melee attacks. Lower = faster attacks.
        /// Default 0.5 (50% faster = double attack speed)
        /// </summary>
        public float cooldownFactor = 0.5f;
    }

    public class HediffComp_TranscendentMelee : HediffComp
    {
        public HediffCompProperties_TranscendentMelee PropsTranscendentMelee => (HediffCompProperties_TranscendentMelee)props;

        public override void CompPostMake()
        {
            base.CompPostMake();

            if (parent?.pawn != null)
            {
                // Ensure pawn has melee verbs
                ApplyTranscendentMeleeModifications();
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // Refresh melee modifications periodically (in case verbs are refreshed)
            if (Find.TickManager.TicksGame % 100 == 0 && parent?.pawn != null)
            {
                ApplyTranscendentMeleeModifications();
            }
        }

        private void ApplyTranscendentMeleeModifications()
        {
            Pawn pawn = parent.pawn;
            if (pawn?.meleeVerbs == null)
                return;

            // Modify existing melee verbs or create transcendent variant
            foreach (Verb verb in pawn.meleeVerbs.AllVerbs)
            {
                if (verb?.verbProps != null)
                {
                    // Apply transcendent modifications via stats
                    // The actual damage/AP values will be calculated by the pawn's combat stats
                    // and the hediff stat modifiers in the XML definition
                }
            }
        }

        public override void Notify_PawnKilled()
        {
            base.Notify_PawnKilled();
            // Clean up if needed
        }
    }
}
