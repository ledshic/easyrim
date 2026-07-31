using RimWorld;
using Verse;

namespace EasyMode
{
    /// <summary>
    /// Passive recharge for a mechanitor's overseen mechs.
    /// Mirrors <see cref="Building_MechCharger"/> energy gain:
    /// <c>mech.needs.energy.CurLevel += amount</c> on <see cref="Need_MechEnergy"/>.
    /// </summary>
    public class HediffCompProperties_MechPowerRestore : HediffCompProperties
    {
        /// <summary>How often to restore energy (ticks). Default 240.</summary>
        public int tickInterval = 240;

        /// <summary>
        /// Minimum implant severity (install count) required before recharge activates.
        /// Default 3 = third install.
        /// </summary>
        public float minSeverity = 3f;

        /// <summary>
        /// Energy added each pulse when <see cref="fullRestore"/> is false.
        /// Same unit as <see cref="Building_MechCharger"/> (absolute need level).
        /// </summary>
        public float energyPerPulse = 5f;

        /// <summary>If true, set energy to MaxLevel each pulse instead of adding energyPerPulse.</summary>
        public bool fullRestore = true;

        /// <summary>
        /// When not full-restoring, multiply energyPerPulse by implant severity (install count).
        /// </summary>
        public bool scaleWithSeverity = true;

        public HediffCompProperties_MechPowerRestore()
        {
            this.compClass = typeof(HediffComp_MechPowerRestore);
        }
    }

    public class HediffComp_MechPowerRestore : HediffComp
    {
        public HediffCompProperties_MechPowerRestore Props =>
            (HediffCompProperties_MechPowerRestore)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (!ModsConfig.BiotechActive)
                return;

            // Unlock at Nth install (severity). Third install by default.
            if (parent.Severity < Props.minSeverity)
                return;

            Pawn mechanitor = parent?.pawn;
            if (mechanitor == null || mechanitor.Dead || !mechanitor.Spawned)
                return;

            if (!MechanitorUtility.IsMechanitor(mechanitor) || mechanitor.mechanitor == null)
                return;

            int interval = Props.tickInterval > 0 ? Props.tickInterval : 240;
            if (!mechanitor.IsHashIntervalTick(interval))
                return;

            float amount = Props.energyPerPulse;
            if (Props.scaleWithSeverity && !Props.fullRestore)
            {
                amount *= parent.Severity;
            }

            // OverseenPawns: every mech this mechanitor oversees (vanilla relation list).
            var overseen = mechanitor.mechanitor.OverseenPawns;
            for (int i = 0; i < overseen.Count; i++)
            {
                Pawn mech = overseen[i];
                if (mech == null || mech.Dead || mech.Destroyed)
                    continue;

                // Same need used by Building_MechCharger / Need_MechEnergy.
                Need_MechEnergy energy = mech.needs?.energy;
                if (energy == null)
                    continue;

                if (Props.fullRestore)
                {
                    energy.CurLevel = energy.MaxLevel;
                }
                else if (amount > 0f && energy.CurLevel < energy.MaxLevel)
                {
                    energy.CurLevel += amount;
                }
            }
        }
    }
}
