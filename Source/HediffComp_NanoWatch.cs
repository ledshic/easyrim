using Verse;

namespace EasyMode
{
    public class HediffCompProperties_NanoWatch : HediffCompProperties
    {
        /// <summary>How often to check the host for critical wounds (ticks).</summary>
        public int tickInterval = 60;

        /// <summary>Minimum days before nano reconstruction completes.</summary>
        public int minRespawnDays = 3;

        /// <summary>Maximum days before nano reconstruction completes.</summary>
        public int maxRespawnDays = 5;

        public HediffCompProperties_NanoWatch()
        {
            this.compClass = typeof(HediffComp_NanoWatch);
        }
    }

    /// <summary>
    /// Watches the nano hediff owner. On critical wounds, triggers emergency disassembly
    /// (despawn) and schedules a full-HP respawn after a random delay.
    /// </summary>
    public class HediffComp_NanoWatch : HediffComp
    {
        public HediffCompProperties_NanoWatch Props => (HediffCompProperties_NanoWatch)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = parent?.pawn;
            if (pawn == null || pawn.Dead || pawn.Destroyed)
                return;

            int interval = Props.tickInterval > 0 ? Props.tickInterval : 60;
            if (!pawn.IsHashIntervalTick(interval))
                return;

            GameComponent_NanoRespawn comp = GameComponent_NanoRespawn.Instance;
            if (comp == null || comp.IsPending(pawn))
                return;

            if (!IsCriticallyWounded(pawn))
                return;

            comp.TryBeginEmergency(pawn, Props.minRespawnDays, Props.maxRespawnDays);
        }

        /// <summary>
        /// Same criteria as <see cref="Pawn_HealthTracker.ShouldBeDead"/> but ignores
        /// <c>preventsDeath</c> (transcendence_biological would otherwise always return false).
        /// </summary>
        public static bool IsCriticallyWounded(Pawn pawn)
        {
            if (pawn?.health == null)
                return false;

            Pawn_HealthTracker health = pawn.health;
            if (health.Dead)
                return true;

            foreach (Hediff hediff in health.hediffSet.hediffs)
            {
                if (hediff.CauseDeathNow())
                    return true;
            }

            if (health.ShouldBeDeadFromRequiredCapacity() != null)
                return true;

            if (PawnCapacityUtility.CalculatePartEfficiency(
                    health.hediffSet, pawn.RaceProps.body.corePart) <= 0.0001f)
                return true;

            if (health.ShouldBeDeadFromLethalDamageThreshold())
                return true;

            return false;
        }
    }
}
