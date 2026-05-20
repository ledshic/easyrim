using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
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

        // =========== World targeting (long-range jump) ===========
        // Allow picking any world tile; no traveling world object is created.
        // Behavior:
        // - If there is a player caravan at the tile, join it immediately.
        // - Else if there is a MapParent capable of having a map: get or generate the map and drop the pawn at a safe cell (pod-like selection).
        // - Else (empty tile): form a new player caravan at that tile consisting of the caster.
        public override bool Valid(GlobalTargetInfo target, bool throwMessages = false)
        {
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
            var pawn = parent.pawn;
            if (pawn == null || !target.IsValid)
            {
                return;
            }

            int tile = target.Tile;
            // Origin context (for VFX) — do NOT despawn yet if we will open manual landing picker.
            Map originMap = pawn.Map;
            IntVec3 originCell = pawn.PositionHeld;
            Rot4 rot = pawn.Rotation;

            // Track origin caravan if any; we'll remove the pawn and destroy the caravan if it becomes empty.
            Caravan originCaravan = pawn.GetCaravan();
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

            // Prefer joining an existing player caravan on the tile (not the same as origin caravan).
            var caravan = Find.WorldObjects.Caravans?
                .FirstOrDefault(c => c.Tile == tile && c.Faction == Faction.OfPlayer && c != originCaravan);
            if (caravan != null)
            {
                if (multiplayerActive)
                {
                    SyncedTeleportToPlayerCaravan(pawn, tile);
                    return;
                }

                // Commit teleport immediately to caravan: play exit SFX (camera) then move pawn.
                if (originMap != null)
                {
                    TrySpawnEffecter(DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_Exit"), originCell, originMap);
                    TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Exit"), originCell, originMap);
                }

                // If pawn comes from another caravan, remove it and destroy if empty.
                if (originCaravan != null && !originCaravan.Destroyed)
                {
                    bool destroy = originCaravan.PawnsListForReading.Count == 1 && originCaravan.PawnsListForReading[0] == pawn;
                    originCaravan.RemovePawn(pawn);
                    if (destroy && originCaravan.PawnsListForReading.Count == 0)
                    {
                        originCaravan.Destroy();
                    }
                }

                if (pawn.Spawned)
                {
                    pawn.DeSpawn(DestroyMode.Vanish);
                }

                caravan.AddPawn(pawn, true);
                TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry"), IntVec3.Zero, null);
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
                        if (MultiplayerCompatibility.MultiplayerActive)
                        {
                            SyncedTeleportVisitHostile(pawn, tile);
                            return;
                        }

                        if (originMap != null)
                        {
                            TrySpawnEffecter(DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_Exit"), originCell, originMap);
                            TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Exit"), originCell, originMap);
                        }

                        if (originCaravan != null && !originCaravan.Destroyed)
                        {
                            bool destroy = originCaravan.PawnsListForReading.Count == 1 && originCaravan.PawnsListForReading[0] == pawn;
                            originCaravan.RemovePawn(pawn);
                            if (destroy && originCaravan.PawnsListForReading.Count == 0)
                            {
                                originCaravan.Destroy();
                            }
                        }

                        if (pawn.Spawned)
                        {
                            pawn.DeSpawn(DestroyMode.Vanish);
                        }

                        CaravanMaker.MakeCaravan(new List<Pawn> { pawn }, Faction.OfPlayer, tile, addToWorldPawnsIfNotAlready: true);
                        TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry"), IntVec3.Zero, null);
                    }));
                    options.Add(new FloatMenuOption("TeleportAttack".Translate(), () =>
                    {
                        if (MultiplayerCompatibility.MultiplayerActive)
                        {
                            SyncedTeleportAttackMapParent(pawn, tile);
                            return;
                        }

                        Map targetMap = mapParent.Map;
                        if (targetMap == null)
                        {
                            targetMap = MapGenerator.GenerateMap(new IntVec3(200, 1, 200), mapParent, mapParent.MapGeneratorDef, null);
                        }
                        BeginManualLandingTargeting(targetMap, pawn, rot, originMap, originCell, mapParent, originCaravan);
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

                if (multiplayerActive)
                {
                    SyncedTeleportAttackMapParent(pawn, tile);
                    return;
                }

                // Open a second-stage targeting on the destination map so the player can pick an exact landing cell,
                // emulating transport pods' "choose landing spot" behavior.
                BeginManualLandingTargeting(targetMap, pawn, rot, originMap, originCell, mapParent, originCaravan);
                return;
            }

            if (multiplayerActive)
            {
                SyncedTeleportToEmptyTile(pawn, tile);
                return;
            }

            // Otherwise, form a new player caravan at this tile.
            if (originMap != null)
            {
                TrySpawnEffecter(DefDatabase<EffecterDef>.GetNamedSilentFail("Skip_Exit"), originCell, originMap);
                TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Exit"), originCell, originMap);
            }

            // If coming from a caravan, detach and destroy if it becomes empty.
            if (originCaravan != null && !originCaravan.Destroyed)
            {
                bool destroy = originCaravan.PawnsListForReading.Count == 1 && originCaravan.PawnsListForReading[0] == pawn;
                originCaravan.RemovePawn(pawn);
                if (destroy && originCaravan.PawnsListForReading.Count == 0)
                {
                    originCaravan.Destroy();
                }
            }

            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }
            var pawns = new List<Pawn> { pawn };
            CaravanMaker.MakeCaravan(pawns, Faction.OfPlayer, tile, addToWorldPawnsIfNotAlready: true);
            TryPlaySound(DefDatabase<SoundDef>.GetNamedSilentFail("Psycast_Skip_Entry"), IntVec3.Zero, null);
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

            if (Find.WorldObjects.MapParents?.Any(mp => mp.Tile == tile && mp.def.canHaveMap) != true)
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

            if (Find.WorldObjects.MapParents?.Any(mp => mp.Tile == tile && mp.def.canHaveMap) == true)
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

            MapParent mapParent = Find.WorldObjects.MapParents?.FirstOrDefault(mp => mp.Tile == tile && mp.def.canHaveMap);
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

        private void BeginManualLandingTargeting(Map targetMap, Pawn pawn, Rot4 rot, Map originMap, IntVec3 originCell, MapParent mapParent, Caravan originCaravan)
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
                DoTeleportSpawnAt(pawn, rot, originMap, originCell, targetMap, fallback, mapParent, originCaravan);
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
                DoTeleportSpawnAt(pawn, rot, originMap, originCell, targetMap, drop, mapParent, originCaravan);
            });
        }

        private static void DoTeleportSpawnAt(Pawn pawn, Rot4 rot, Map originMap, IntVec3 originCell, Map targetMap, IntVec3 dropCell, MapParent mapParent, Caravan originCaravan)
        {
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
            get
            {
                try
                {
                    Type multiplayerType = Type.GetType("Multiplayer.Client.Multiplayer, Multiplayer");
                    object client = multiplayerType?.GetProperty("Client")?.GetValue(null, null);
                    return client != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal static void RegisterSyncMethods()
        {
            if (syncMethodsRegistered)
                return;

            Type mpType = FindType("Multiplayer.API.MP");
            if (mpType == null)
                return;

            MethodInfo registerSyncMethod = mpType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "RegisterSyncMethod")
                        return false;

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(MethodInfo);
                });

            if (registerSyncMethod == null)
            {
                Log.Warning("[EasyMode] Multiplayer API loaded, but RegisterSyncMethod(MethodInfo) was not found. World teleport will not be synchronized in Multiplayer.");
                return;
            }

            Type teleportType = typeof(CompAbilityEffect_TeleportSelf);
            string[] methodNames =
            {
                "SyncedTeleportToPlayerCaravan",
                "SyncedTeleportVisitHostile",
                "SyncedTeleportToEmptyTile",
                "SyncedTeleportAttackMapParent"
            };

            foreach (string methodName in methodNames)
            {
                MethodInfo method = teleportType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
                if (method == null)
                {
                    Log.Warning("[EasyMode] Could not find teleport sync method: " + methodName);
                    return;
                }

                registerSyncMethod.Invoke(null, new object[] { method });
            }

            syncMethodsRegistered = true;
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }
    }
}
