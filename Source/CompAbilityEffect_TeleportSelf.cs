using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace EasyMode
{
    internal enum TeleportTargetMode
    {
        CurrentMap = 0,
        LongRangeWorld = 1
    }

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

        private static GameComponent_TeleportModeMemory ModeMemory => Current.Game?.GetComponent<GameComponent_TeleportModeMemory>();

        private TeleportTargetMode CurrentMode
        {
            get
            {
                Pawn pawn = parent?.pawn;
                if (pawn == null)
                {
                    return TeleportTargetMode.CurrentMap;
                }

                GameComponent_TeleportModeMemory memory = ModeMemory;
                if (memory != null)
                {
                    return memory.GetModeForPawn(pawn);
                }

                return TeleportTargetMode.CurrentMap;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Pawn pawn = parent?.pawn;
            if (pawn == null)
            {
                yield break;
            }

            string modeKey = CurrentMode == TeleportTargetMode.CurrentMap
                ? "TeleportModeCurrentMap"
                : "TeleportModeLongRange";

            yield return new Command_Action
            {
                defaultLabel = "TeleportModeGizmoLabel".Translate(modeKey.Translate()),
                defaultDesc = "TeleportModeGizmoDesc".Translate(),
                action = () =>
                {
                    var options = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("TeleportModeCurrentMap".Translate(), () => SetModeWithSync(TeleportTargetMode.CurrentMap)),
                        new FloatMenuOption("TeleportModeLongRange".Translate(), () => SetModeWithSync(TeleportTargetMode.LongRangeWorld))
                    };

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }

        private void SetModeWithSync(TeleportTargetMode mode)
        {
            Pawn pawn = parent?.pawn;
            if (pawn == null)
            {
                return;
            }

            if (MultiplayerCompatibility.MultiplayerActive)
            {
                SyncedSetTeleportMode(pawn, (int)mode);
            }
            else
            {
                SetTeleportMode(pawn, mode);
            }
        }

        private static void SyncedSetTeleportMode(Pawn pawn, int mode)
        {
            SetTeleportMode(pawn, (TeleportTargetMode)mode);
        }

        private static void SetTeleportMode(Pawn pawn, TeleportTargetMode mode)
        {
            if (pawn == null)
            {
                return;
            }

            ModeMemory?.SetModeForPawn(pawn, mode);
            string messageKey = mode == TeleportTargetMode.CurrentMap
                ? "TeleportSwitchedCurrentMap"
                : "TeleportSwitchedLongRange";
            Messages.Message(messageKey.Translate(), pawn, MessageTypeDefOf.TaskCompletion, historical: false);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (CurrentMode != TeleportTargetMode.CurrentMap)
            {
                if (throwMessages)
                {
                    Messages.Message("TeleportModeRequiresWorldTarget".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

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

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (CurrentMode != TeleportTargetMode.CurrentMap)
            {
                Messages.Message("TeleportModeRequiresWorldTarget".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

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

        // =========== World targeting (long-range jump) ===========
        // Allow picking any world tile; no traveling world object is created.
        // Behavior:
        // - If there is a player caravan at the tile, join it immediately.
        // - Else if there is a MapParent capable of having a map: get or generate the map and drop the pawn at a safe cell (pod-like selection).
        // - Else (empty tile): form a new player caravan at that tile consisting of the caster.
        public override bool Valid(GlobalTargetInfo target, bool throwMessages = false)
        {
            if (CurrentMode != TeleportTargetMode.LongRangeWorld)
            {
                if (throwMessages)
                {
                    Messages.Message("TeleportModeRequiresMapTarget".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            var pawn = parent.pawn;
            if (pawn == null || !target.IsValid)
            {
                if (throwMessages)
                {
                    Messages.Message("AbilityInvalidTarget".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            int tile = target.Tile;
            if (tile < 0 || tile >= Find.WorldGrid.TilesCount)
            {
                if (throwMessages)
                {
                    Messages.Message("AbilityInvalidTarget".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            // Disallow impassable world tiles (oceans/void/mountains etc.). Use WorldPathGrid to be compatible with 1.5 SurfaceTile changes.
            var grid = Find.WorldGrid;
            if (tile < 0 || tile >= grid.TilesCount)
            {
                if (throwMessages)
                {
                    Messages.Message("MessageCantLandInImpassable".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }
            if (!Find.WorldPathGrid.Passable(tile))
            {
                if (throwMessages)
                {
                    Messages.Message("MessageCantLandInImpassable".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return true;
        }

        public override void Apply(GlobalTargetInfo target)
        {
            if (CurrentMode != TeleportTargetMode.LongRangeWorld)
            {
                Messages.Message("TeleportModeRequiresMapTarget".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            var pawn = parent.pawn;
            if (pawn == null || !target.IsValid)
            {
                return;
            }

            int tile = target.Tile;
            bool multiplayerActive = MultiplayerCompatibility.MultiplayerActive;
            if (multiplayerActive && !MultiplayerCompatibility.SyncMethodsRegistered)
            {
                Messages.Message("TeleportMultiplayerSyncUnavailable".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            if (!multiplayerActive)
            {
                pawn.pather?.StopDead();
                pawn.stances?.CancelBusyStanceSoft();
            }

            Caravan originCaravan = pawn.GetCaravan();

            // Prefer joining an existing player caravan on the tile (not the same as origin caravan).
            var caravan = Find.WorldObjects.Caravans?
                .FirstOrDefault(c => c.Tile == tile && c.Faction == Faction.OfPlayer && c != originCaravan);
            if (caravan != null)
            {
                // Keep singleplayer/multiplayer behavior identical by reusing the synced commit path.
                SyncedTeleportToPlayerCaravan(pawn, tile);
                return;
            }

            // Try find a map parent on the tile.
            var mapParent = Find.WorldObjects.MapParents?.FirstOrDefault(mp => mp.Tile == tile && mp.def.canHaveMap);
            if (mapParent != null)
            {
                // If occupied by another faction, ask for intent first.
                if (mapParent.Faction != null && mapParent.Faction != Faction.OfPlayer)
                {
                    var options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption("TeleportVisit".Translate(), () =>
                    {
                        // Resolve context at click time to avoid stale captured origin state.
                        SyncedTeleportVisitHostile(pawn, tile);
                    }));
                    options.Add(new FloatMenuOption("TeleportAttack".Translate(), () =>
                    {
                        MapParent attackMapParent = FindMapParentAtTile(tile);
                        if (attackMapParent == null)
                        {
                            Messages.Message("AbilityInvalidTarget".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                            return;
                        }

                        Map targetMap = attackMapParent.Map;
                        if (targetMap == null)
                        {
                            if (MultiplayerCompatibility.MultiplayerActive)
                            {
                                SyncedTeleportAttackMapParent(pawn, tile);
                                return;
                            }

                            targetMap = MapGenerator.GenerateMap(new IntVec3(200, 1, 200), attackMapParent, attackMapParent.MapGeneratorDef, null);
                        }

                        BeginManualLandingTargeting(targetMap, pawn, tile);
                    }));

                    Find.WindowStack.Add(new FloatMenu(options));
                    return;
                }

                // Get or generate the map for this parent.
                Map targetMap = mapParent.Map;
                if (targetMap == null)
                {
                    if (multiplayerActive)
                    {
                        SyncedTeleportAttackMapParent(pawn, tile);
                        return;
                    }

                    // Generate map instantly (no traveling object), mirroring pod/shuttle arrival behavior.
                    targetMap = MapGenerator.GenerateMap(new IntVec3(200, 1, 200), mapParent, mapParent.MapGeneratorDef, null);
                }

                // Open a second-stage targeting on the destination map so the player can pick an exact landing cell,
                // emulating transport pods' "choose landing spot" behavior.
                BeginManualLandingTargeting(targetMap, pawn, tile);
                return;
            }

            // Keep singleplayer/multiplayer behavior identical by reusing the synced commit path.
            SyncedTeleportToEmptyTile(pawn, tile);
        }

        private static void SyncedTeleportToPlayerCaravan(Pawn pawn, int tile)
        {
            if (!TryGetTeleportContext(pawn, out Map originMap, out IntVec3 originCell, out Caravan originCaravan, out _))
                return;

            Caravan caravan = Find.WorldObjects.Caravans?
                .FirstOrDefault(c => c.Tile == tile && c.Faction == Faction.OfPlayer && c != originCaravan);
            if (caravan == null)
                return;

            PlayExitEffects(originMap, originCell);
            RemoveFromOriginCaravanIfNeeded(pawn, originCaravan);
            DespawnIfSpawned(pawn);
            caravan.AddPawn(pawn, true);
            TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry"), IntVec3.Zero, null);
        }

        private static void SyncedTeleportVisitHostile(Pawn pawn, int tile)
        {
            if (!TryGetTeleportContext(pawn, out Map originMap, out IntVec3 originCell, out Caravan originCaravan, out _))
                return;

            if (FindMapParentAtTile(tile) == null)
                return;

            PlayExitEffects(originMap, originCell);
            RemoveFromOriginCaravanIfNeeded(pawn, originCaravan);
            DespawnIfSpawned(pawn);
            CaravanMaker.MakeCaravan(new List<Pawn> { pawn }, Faction.OfPlayer, tile, addToWorldPawnsIfNotAlready: true);
            TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry"), IntVec3.Zero, null);
        }

        private static void SyncedTeleportToEmptyTile(Pawn pawn, int tile)
        {
            if (!TryGetTeleportContext(pawn, out Map originMap, out IntVec3 originCell, out Caravan originCaravan, out _))
                return;

            if (FindMapParentAtTile(tile) != null)
                return;

            PlayExitEffects(originMap, originCell);
            RemoveFromOriginCaravanIfNeeded(pawn, originCaravan);
            DespawnIfSpawned(pawn);
            CaravanMaker.MakeCaravan(new List<Pawn> { pawn }, Faction.OfPlayer, tile, addToWorldPawnsIfNotAlready: true);
            TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry"), IntVec3.Zero, null);
        }

        private static void SyncedTeleportAttackMapParent(Pawn pawn, int tile)
        {
            if (!TryGetTeleportContext(pawn, out Map originMap, out IntVec3 originCell, out Caravan originCaravan, out Rot4 rot))
                return;

            MapParent mapParent = FindMapParentAtTile(tile);
            if (mapParent == null)
                return;

            Map targetMap = mapParent.Map;
            if (targetMap == null)
            {
                targetMap = MapGenerator.GenerateMap(new IntVec3(200, 1, 200), mapParent, mapParent.MapGeneratorDef, null);
            }

            if (!TryFindDeterministicTeleportDropCell(targetMap, out IntVec3 dropCell))
            {
                Messages.Message("CannotTeleportHere".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            DoTeleportSpawnAt(pawn, rot, originMap, originCell, targetMap, dropCell, mapParent, originCaravan);
        }

        private static void SyncedTeleportAttackMapParentAtCell(Pawn pawn, int tile, IntVec3 dropCell)
        {
            if (!TryGetTeleportContext(pawn, out Map originMap, out IntVec3 originCell, out Caravan originCaravan, out Rot4 rot))
                return;

            MapParent mapParent = FindMapParentAtTile(tile);
            if (mapParent == null)
                return;

            Map targetMap = mapParent.Map;
            if (targetMap == null)
            {
                targetMap = MapGenerator.GenerateMap(new IntVec3(200, 1, 200), mapParent, mapParent.MapGeneratorDef, null);
            }

            if (!IsValidTeleportDropCell(dropCell, targetMap) && !TryFindDeterministicTeleportDropCell(targetMap, out dropCell))
            {
                Messages.Message("CannotTeleportHere".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            DoTeleportSpawnAt(pawn, rot, originMap, originCell, targetMap, dropCell, mapParent, originCaravan);
        }

        private static bool TryGetTeleportContext(Pawn pawn, out Map originMap, out IntVec3 originCell, out Caravan originCaravan, out Rot4 rot)
        {
            originMap = pawn?.Map;
            originCell = pawn?.PositionHeld ?? IntVec3.Invalid;
            originCaravan = pawn?.GetCaravan();
            rot = pawn?.Rotation ?? Rot4.South;

            if (pawn == null)
                return false;

            pawn.pather?.StopDead();
            pawn.stances?.CancelBusyStanceSoft();
            return true;
        }

        private static void PlayExitEffects(Map originMap, IntVec3 originCell)
        {
            if (originMap == null || !originCell.IsValid)
                return;

            TrySpawnEffecter(DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_Exit"), originCell, originMap);
            TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Exit"), originCell, originMap);
        }

        private static void RemoveFromOriginCaravanIfNeeded(Pawn pawn, Caravan originCaravan)
        {
            if (originCaravan == null || originCaravan.Destroyed)
                return;

            bool destroy = originCaravan.PawnsListForReading.Count == 1 && originCaravan.PawnsListForReading[0] == pawn;
            originCaravan.RemovePawn(pawn);
            if (destroy && originCaravan.PawnsListForReading.Count == 0)
            {
                originCaravan.Destroy();
            }
        }

        private static void DespawnIfSpawned(Pawn pawn)
        {
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }
        }

        private void BeginManualLandingTargeting(Map targetMap, Pawn pawn, int worldTile)
        {
            if (targetMap == null || pawn == null)
                return;

            // Jump camera to destination map for picking.
            try
            {
                CameraJumper.TryJump(targetMap.Center, targetMap);
            }
            catch
            {
                // If camera jump fails for any reason, fallback to auto-drop.
                IntVec3 fallback;
                if (!TryFindTeleportDropCell(targetMap, out fallback))
                {
                    fallback = targetMap.Center;
                }

                SyncedTeleportAttackMapParentAtCell(pawn, worldTile, fallback);
                return;
            }

            var parms = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                validator = (TargetInfo ti) =>
                {
                    if (!ti.IsValid || ti.Map != targetMap) return false;
                    var c = ti.Cell;
                    if (!c.InBounds(targetMap)) return false;
                    return DropCellFinder.SkyfallerCanLandAt(c, targetMap, new IntVec2(1, 1)) && c.Standable(targetMap);
                }
            };

            // Start targeting: when the player clicks a valid cell, we perform the actual teleport.
            Find.Targeter.BeginTargeting(parms, (LocalTargetInfo lt) =>
            {
                var drop = lt.Cell;
                SyncedTeleportAttackMapParentAtCell(pawn, worldTile, drop);
            });
        }

        private static void DoTeleportSpawnAt(Pawn pawn, Rot4 rot, Map originMap, IntVec3 originCell, Map targetMap, IntVec3 dropCell, MapParent mapParent, Caravan originCaravan)
        {
            if (pawn == null || pawn.Destroyed)
                return;

            // Re-validate destination just in case (validator should have ensured this already).
            if (targetMap == null)
                return;
            if (!dropCell.InBounds(targetMap) || !DropCellFinder.SkyfallerCanLandAt(dropCell, targetMap, new IntVec2(1, 1)) || !dropCell.Standable(targetMap))
            {
                // Fallback to a safe cell.
                if (!TryFindTeleportDropCell(targetMap, out dropCell))
                {
                    dropCell = targetMap.Center;
                }
            }

            // If coming from a caravan, detach here (commit time) and destroy if empty to avoid leaving empty caravans behind.
            if (originCaravan != null && !originCaravan.Destroyed)
            {
                bool destroy = originCaravan.PawnsListForReading.Count == 1 && originCaravan.PawnsListForReading[0] == pawn;
                originCaravan.RemovePawn(pawn);
                if (destroy && originCaravan.PawnsListForReading.Count == 0)
                {
                    originCaravan.Destroy();
                }
            }

            if (originMap != null)
            {
                TrySpawnEffecter(DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_Exit"), originCell, originMap);
                TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Exit"), originCell, originMap);
            }
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }

            GenSpawn.Spawn(pawn, dropCell, targetMap, rot, WipeMode.Vanish, respawningAfterLoad: false);

            TrySpawnEffecter(DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_EntryNoDelay"), dropCell, targetMap);
            TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry"), dropCell, targetMap);

            if (mapParent != null && mapParent.Faction == Faction.OfPlayer && pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }
        }

        private static bool TryFindTeleportDropCell(Map map, out IntVec3 cell)
        {
            // Prefer safe drop near colony if player home, else a general random drop spot.
            // Use DropCellFinder helpers where available; fallback to standable near center.
            cell = IntVec3.Invalid;
            try
            {
                // Try common helper used by incidents/pods
                cell = DropCellFinder.RandomDropSpot(map);
            }
            catch
            {
                // ignore and try fallback
            }
            if (!cell.IsValid || !cell.Standable(map))
            {
                if (!CellFinder.TryFindRandomCellNear(map.Center, map, 20, c => c.Standable(map) && map.reachability.CanReachColony(c), out cell))
                {
                    if (!CellFinder.TryFindRandomCell(map, c => c.Standable(map), out cell))
                    {
                        cell = map.Center;
                    }
                }
            }
            return cell.IsValid;
        }

        private static bool TryFindDeterministicTeleportDropCell(Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map == null)
                return false;

            IntVec3 center = map.Center;
            int maxRadius = Mathf.Max(map.Size.x, map.Size.z);
            int numCells = GenRadial.NumCellsInRadius(maxRadius);
            for (int i = 0; i < numCells; i++)
            {
                IntVec3 candidate = center + GenRadial.RadialPattern[i];
                if (IsValidTeleportDropCell(candidate, map))
                {
                    cell = candidate;
                    return true;
                }
            }

            foreach (IntVec3 candidate in map.AllCells)
            {
                if (IsValidTeleportDropCell(candidate, map))
                {
                    cell = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidTeleportDropCell(IntVec3 cell, Map map)
        {
            return cell.IsValid
                && cell.InBounds(map)
                && cell.Standable(map)
                && DropCellFinder.SkyfallerCanLandAt(cell, map, new IntVec2(1, 1));
        }

        private static MapParent FindMapParentAtTile(int tile)
        {
            return Find.WorldObjects.MapParents?.FirstOrDefault(mp => mp.Tile == tile && mp.def.canHaveMap);
        }
    }

    [StaticConstructorOnStartup]
    internal static class EasyModeMultiplayerSyncInit
    {
        static EasyModeMultiplayerSyncInit()
        {
            LongEventHandler.ExecuteWhenFinished(MultiplayerCompatibility.RegisterSyncMethods);
        }
    }

    internal static class MultiplayerCompatibility
    {
        private static bool syncMethodsRegistered;

        internal static bool SyncMethodsRegistered
        {
            get
            {
                if (!syncMethodsRegistered)
                {
                    RegisterSyncMethods();
                }

                return syncMethodsRegistered;
            }
        }

        internal static bool MultiplayerActive
        {
            get => MP.IsInMultiplayer;
        }

        internal static void RegisterSyncMethods()
        {
            if (syncMethodsRegistered)
                return;

            try
            {
                Type teleportType = typeof(CompAbilityEffect_TeleportSelf);
                string[] methodNames =
                {
                    "SyncedSetTeleportMode",
                    "SyncedTeleportToPlayerCaravan",
                    "SyncedTeleportVisitHostile",
                    "SyncedTeleportToEmptyTile",
                    "SyncedTeleportAttackMapParent",
                    "SyncedTeleportAttackMapParentAtCell"
                };

                foreach (string methodName in methodNames)
                {
                    MethodInfo method = teleportType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
                    if (method == null)
                    {
                        Log.Warning("[EasyMode] Could not find teleport sync method: " + methodName);
                        return;
                    }

                    MP.RegisterSyncMethod(method);
                }

                syncMethodsRegistered = true;
            }
            catch (Exception ex)
            {
                Log.Error("[EasyMode] Failed to register teleport sync methods: " + ex);
            }
        }
    }
}
