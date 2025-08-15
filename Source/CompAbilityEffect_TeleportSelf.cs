using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace EasyMode
{
    public class CompProperties_AbilityTeleportSelf : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityTeleportSelf()
        {
            compClass = typeof(CompAbilityEffect_TeleportSelf);
        }
    }

    // Single-pawn long-range teleport within the current map.
    // Validation mimics transport pod landing (via DropCellFinder.SkyfallerCanLandAt),
    // with additional standability check for pawn spawn.
    public class CompAbilityEffect_TeleportSelf : CompAbilityEffect
    {
        public new CompProperties_AbilityTeleportSelf Props => (CompProperties_AbilityTeleportSelf)props;

        // Removed invalid override of GetWornGizmosExtra(); CompAbilityEffect 没有该可重写方法。

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            var pawn = parent.pawn;
            if (pawn == null || pawn.Map == null)
            {
                return false;
            }

            if (!target.IsValid || !target.Cell.InBounds(pawn.Map))
            {
                if (throwMessages)
                {
                    Messages.Message("AbilityInvalidTarget".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            var map = pawn.Map;
            IntVec3 cell = target.Cell;

            // Transport pod-like landing permission.
            bool canSkyfall = DropCellFinder.SkyfallerCanLandAt(cell, map, new IntVec2(1, 1));
            if (!canSkyfall)
            {
                if (throwMessages)
                {
                    Messages.Message("CannotTeleportHere".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            // Ensure pawn can be placed.
            if (!cell.Standable(map))
            {
                if (throwMessages)
                {
                    Messages.Message("MustPlaceOnStandable".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return true;
        }

    // Multiplayer: this method is deterministic and side-effect free beyond map state; if MP is loaded,
    // they can SyncMethod by patching, but we avoid hard attribute to keep no-dep.
    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            var pawn = parent.pawn;
            if (pawn == null || pawn.Map == null || !target.IsValid)
            {
                return;
            }

            Map map = pawn.Map;
            IntVec3 from = pawn.Position;
            IntVec3 to = target.Cell;

            // Re-validate at apply time to be safe.
            if (!DropCellFinder.SkyfallerCanLandAt(to, map, new IntVec2(1, 1)) || !to.Standable(map))
            {
                Messages.Message("CannotTeleportHere".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // VFX/SFX: reuse skip entry/exit if available.
            TryPlaySkipEffects(from, to, map);

            // Interrupt current actions and relocate pawn.
            pawn.pather?.StopDead();
            pawn.stances?.CancelBusyStanceSoft();

            Rot4 rot = pawn.Rotation;
            // Despawn and respawn to ensure all tracking lists are updated properly.
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }
            GenSpawn.Spawn(pawn, to, map, rot, WipeMode.Vanish, respawningAfterLoad: false);

            // Small entry effect again to emphasize arrival.
            TrySpawnEffecter(DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_EntryNoDelay"), to, map);
            TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry"), to, map);
        }

        private static void TryPlaySkipEffects(IntVec3 from, IntVec3 to, Map map)
        {
            // exit at origin
            TrySpawnEffecter(DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_Exit"), from, map);
            TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Exit"), from, map);

            // entry at destination
            TrySpawnEffecter(DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_Entry"), to, map);
            // Entry sound is present in Core; exit may be Royalty-only, so we already used silent-fail.
            TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry"), to, map);
        }

        private static void TrySpawnEffecter(EffecterDef def, IntVec3 cell, Map map)
        {
            if (def == null || map == null)
                return;
            var effecter = def.Spawn(cell, map, 1f);
            effecter?.Cleanup();
        }

        private static void TryPlaySound(SoundDef sound, IntVec3 cell, Map map)
        {
            if (sound == null)
                return;
            SoundInfo info;
            if (map != null)
            {
                info = SoundInfo.InMap(new TargetInfo(cell, map), MaintenanceType.None);
            }
            else
            {
                info = SoundInfo.OnCamera(MaintenanceType.None);
            }
            sound.PlayOneShot(info);
        }
    }
}
