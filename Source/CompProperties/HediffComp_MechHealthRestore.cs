using RimWorld;
using Verse;

namespace EasyMode
{
    /// <summary>
    /// Passive repair for a mechanitor's overseen mechs.
    /// Uses vanilla <see cref="MechRepairUtility"/> — the same path as
    /// <see cref="JobDriver_RepairMech"/> / remote repair.
    /// </summary>
    public class HediffCompProperties_MechHealthRestore : HediffCompProperties
    {
        /// <summary>How often to apply repair ticks (ticks). Default 240.</summary>
        public int tickInterval = 240;

        /// <summary>
        /// Minimum implant severity (install count) required before restoration activates.
        /// Default 6 = sixth install.
        /// </summary>
        public float minSeverity = 6f;

        /// <summary>
        /// How many times to call <see cref="MechRepairUtility.RepairTick"/> per pulse
        /// (before optional severity scaling). Each call heals 1 injury HP or restores a missing part.
        /// </summary>
        public int repairsPerPulse = 1;

        /// <summary>Multiply repairsPerPulse by implant severity (install count).</summary>
        public bool scaleWithSeverity = true;

        public HediffCompProperties_MechHealthRestore()
        {
            this.compClass = typeof(HediffComp_MechHealthRestore);
        }
    }

    public class HediffComp_MechHealthRestore : HediffComp
    {
        public HediffCompProperties_MechHealthRestore Props =>
            (HediffCompProperties_MechHealthRestore)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (!ModsConfig.BiotechActive)
                return;

            // Unlock at Nth install (severity). Sixth install by default.
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

            int repairs = Props.repairsPerPulse > 0 ? Props.repairsPerPulse : 1;
            if (Props.scaleWithSeverity)
            {
                repairs = (int)(repairs * parent.Severity);
                if (repairs < 1)
                    repairs = 1;
            }

            var overseen = mechanitor.mechanitor.OverseenPawns;
            for (int i = 0; i < overseen.Count; i++)
            {
                Pawn mech = overseen[i];
                if (mech == null || mech.Dead || mech.Destroyed)
                    continue;

                // Vanilla gate: requires CompMechRepairable + something to heal (or missing weapon).
                if (!MechRepairUtility.CanRepair(mech))
                    continue;

                for (int r = 0; r < repairs; r++)
                {
                    if (!MechRepairUtility.CanRepair(mech))
                        break;

                    // Same call as JobDriver_RepairMech.tickIntervalAction.
                    MechRepairUtility.RepairTick(mech);
                }
            }
        }
    }
}
