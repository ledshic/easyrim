using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EasyMode
{
    /// <summary>
    /// Tracks pawns removed by nano emergency disassembly and respawns them after a delay.
    /// </summary>
    public class GameComponent_NanoRespawn : GameComponent
    {
        private List<NanoRespawnEntry> pending = new List<NanoRespawnEntry>();

        public GameComponent_NanoRespawn(Game game)
        {
        }

        public static GameComponent_NanoRespawn Instance =>
            Current.Game?.GetComponent<GameComponent_NanoRespawn>();

        public bool IsPending(Pawn pawn)
        {
            return FindEntry(pawn) != null;
        }

        /// <summary>Pending reconstruction entries (for debug UI).</summary>
        public IReadOnlyList<NanoRespawnEntry> PendingEntries =>
            (IReadOnlyList<NanoRespawnEntry>)pending ?? Array.Empty<NanoRespawnEntry>();

        public NanoRespawnEntry FindEntry(Pawn pawn)
        {
            if (pawn == null || pending == null)
                return null;
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i]?.pawn == pawn)
                    return pending[i];
            }
            return null;
        }

        /// <summary>
        /// Immediately despawns a critically wounded nano host and schedules full-HP respawn.
        /// </summary>
        public bool TryBeginEmergency(Pawn pawn, int minDays, int maxDays)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead)
                return false;
            if (IsPending(pawn))
                return false;

            if (minDays < 1)
                minDays = 1;
            if (maxDays < minDays)
                maxDays = minDays;

            int days = Rand.RangeInclusive(minDays, maxDays);
            return BeginEmergencyInternal(pawn, days);
        }

        /// <summary>
        /// Debug: force nano despawn for any living pawn (no critical-wound check).
        /// Schedules the normal 3–5 day reconstruction timer.
        /// </summary>
        public bool ForceDespawnNow(Pawn pawn, int minDays = 3, int maxDays = 5)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead)
                return false;
            if (IsPending(pawn))
            {
                Messages.Message(
                    "EasyMode_NanoDebugAlreadyPending".Translate(pawn.Named("PAWN")),
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return false;
            }

            if (minDays < 1)
                minDays = 1;
            if (maxDays < minDays)
                maxDays = minDays;

            int days = Rand.RangeInclusive(minDays, maxDays);
            return BeginEmergencyInternal(pawn, days);
        }

        /// <summary>
        /// Debug: immediately complete reconstruction for a pending pawn.
        /// </summary>
        public bool ForceRespawnNow(Pawn pawn)
        {
            if (pawn == null)
                return false;

            NanoRespawnEntry entry = FindEntry(pawn);
            if (entry == null)
            {
                Messages.Message(
                    "EasyMode_NanoDebugNotPending".Translate(pawn.Named("PAWN")),
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return false;
            }

            if (!TryRespawn(entry))
                return false;

            pending.Remove(entry);
            return true;
        }

        /// <summary>
        /// Debug: immediately complete reconstruction for every pending pawn.
        /// </summary>
        public int ForceRespawnAll()
        {
            if (pending == null || pending.Count == 0)
                return 0;

            int count = 0;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                NanoRespawnEntry entry = pending[i];
                if (entry?.pawn == null || entry.pawn.Destroyed)
                {
                    pending.RemoveAt(i);
                    continue;
                }

                if (TryRespawn(entry))
                {
                    pending.RemoveAt(i);
                    count++;
                }
            }
            return count;
        }

        private bool BeginEmergencyInternal(Pawn pawn, int days)
        {
            int respawnTick = Find.TickManager.TicksGame + days * GenDate.TicksPerDay;
            bool shouldNotify = PawnUtility.ShouldSendNotificationAbout(pawn);

            Map mapHeld = pawn.MapHeld;
            // PlanetTile implicitly converts to int (surface tile id).
            int tileId = mapHeld != null ? (int)mapHeld.Tile : (int)pawn.Tile;
            IntVec3 cell = pawn.PositionHeld;

            var entry = new NanoRespawnEntry
            {
                pawn = pawn,
                respawnTick = respawnTick,
                tileId = tileId,
                preferredCell = cell,
                originalFaction = pawn.Faction,
                sendNotification = shouldNotify,
                days = days
            };
            pending.Add(entry);

            if (shouldNotify)
            {
                Messages.Message(
                    "EasyMode_NanoEmergencyDespawn".Translate(days, pawn.Named("PAWN")),
                    new LookTargets(pawn),
                    MessageTypeDefOf.NegativeEvent);
            }

            DespawnForReconstruction(pawn);

            return true;
        }

        public override void GameComponentTick()
        {
            if (pending == null || pending.Count == 0)
                return;

            int now = Find.TickManager.TicksGame;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                NanoRespawnEntry entry = pending[i];
                if (entry?.pawn == null || entry.pawn.Destroyed)
                {
                    pending.RemoveAt(i);
                    continue;
                }

                if (now < entry.respawnTick)
                    continue;

                if (TryRespawn(entry))
                    pending.RemoveAt(i);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pending, "pending", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pending == null)
                pending = new List<NanoRespawnEntry>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                pending.RemoveAll(e => e == null || e.pawn == null || e.pawn.Destroyed);
        }

        private static void DespawnForReconstruction(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed)
                return;

            try
            {
                pawn.jobs?.StopAll(true, true);
            }
            catch
            {
                // Job system can throw if already torn down; safe to ignore.
            }

            pawn.pather?.StopDead();
            pawn.stances?.CancelBusyStanceSoft();

            if (pawn.Drafted && pawn.drafter != null)
                pawn.drafter.Drafted = false;

            Caravan caravan = pawn.GetCaravan();
            if (caravan != null && !caravan.Destroyed)
            {
                bool shouldDestroy = caravan.PawnsListForReading.Count == 1 &&
                                     caravan.PawnsListForReading[0] == pawn;
                caravan.RemovePawn(pawn);
                if (shouldDestroy && caravan.PawnsListForReading.Count == 0)
                    caravan.Destroy();
            }

            if (pawn.Spawned)
                pawn.DeSpawn(DestroyMode.Vanish);

            if (!Find.WorldPawns.Contains(pawn))
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            else
                Find.WorldPawns.ForcefullyKeptPawns.Add(pawn);
        }

        private static bool TryRespawn(NanoRespawnEntry entry)
        {
            Pawn pawn = entry.pawn;
            if (pawn == null || pawn.Destroyed || pawn.Dead)
                return true; // drop dead/destroyed entries

            // Keep reconstructed pawn aligned with its original faction.
            Faction targetFaction = entry.originalFaction ?? pawn.Faction;
            if (targetFaction != null && pawn.Faction != targetFaction)
                pawn.SetFaction(targetFaction);

            FullyHeal(pawn);

            Map map = ResolveRespawnMap(entry);
            if (map == null)
            {
                // Keep waiting until a player map is available.
                entry.respawnTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
                return false;
            }

            IntVec3 cell = ResolveRespawnCell(map, entry.preferredCell);

            // Leave world pawn tracking before spawning onto a map.
            if (Find.WorldPawns.Contains(pawn))
                Find.WorldPawns.RemovePawn(pawn);

            GenSpawn.Spawn(pawn, cell, map);
            if (targetFaction != null && pawn.Faction != targetFaction)
                pawn.SetFaction(targetFaction);
            pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);

            if (entry.sendNotification)
            {
                Messages.Message(
                    "EasyMode_NanoEmergencyRespawn".Translate(pawn.Named("PAWN")),
                    new LookTargets(pawn),
                    MessageTypeDefOf.PositiveEvent);
            }

            return true;
        }

        private static Map ResolveRespawnMap(NanoRespawnEntry entry)
        {
            // Prefer the original tile if that map is still loaded and player-owned.
            if (entry.tileId >= 0)
            {
                Map byTile = Find.Maps.FirstOrDefault(m => (int)m.Tile == entry.tileId);
                if (byTile != null && (byTile.ParentFaction == Faction.OfPlayer || byTile.IsPlayerHome))
                    return byTile;
            }

            Map home = Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
            if (home != null)
                return home;

            return Find.CurrentMap ?? Find.Maps.FirstOrDefault();
        }

        private static IntVec3 ResolveRespawnCell(Map map, IntVec3 preferred)
        {
            if (preferred.IsValid && preferred.InBounds(map) && preferred.Walkable(map))
                return preferred;

            if (RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(
                    c => c.Walkable(map) && !c.Fogged(map), map, out IntVec3 cell))
                return cell;

            if (RCellFinder.TryFindRandomCellNearWith(
                    map.Center, c => c.Walkable(map), map, out cell, 1, 50))
                return cell;

            return map.Center;
        }

        /// <summary>
        /// Restore full HP: missing parts, injuries, other bad hediffs; clear downed/mental state; fill needs.
        /// Good hediffs (transcendence set, implants, etc.) are preserved.
        /// </summary>
        public static void FullyHeal(Pawn pawn)
        {
            if (pawn?.health == null)
                return;

            // Restore missing body parts (ancestors first).
            int safety = 64;
            while (safety-- > 0)
            {
                List<Hediff_MissingPart> missing = pawn.health.hediffSet.GetMissingPartsCommonAncestors();
                if (missing == null || missing.Count == 0)
                    break;
                pawn.health.RestorePart(missing[0].Part);
            }

            // Remove remaining bad hediffs (injuries, blood loss, diseases, etc.).
            List<Hediff> snapshot = pawn.health.hediffSet.hediffs.ToList();
            for (int i = 0; i < snapshot.Count; i++)
            {
                Hediff h = snapshot[i];
                if (h == null || h.def == null)
                    continue;
                if (!h.def.isBad)
                    continue;
                // Keep artificial parts / implants even if marked bad by some defs.
                if (h is Hediff_AddedPart || h is Hediff_Implant)
                    continue;
                pawn.health.RemoveHediff(h);
            }

            // Removing bad hediffs calls CheckForStateChange, which undowns when appropriate.

            if (pawn.InMentalState)
                pawn.mindState?.mentalStateHandler?.CurState?.RecoverFromState();

            if (pawn.needs != null)
            {
                List<Need> needs = pawn.needs.AllNeeds;
                for (int i = 0; i < needs.Count; i++)
                {
                    Need n = needs[i];
                    if (n != null)
                        n.CurLevel = n.MaxLevel;
                }
            }
        }
    }

    public class NanoRespawnEntry : IExposable
    {
        public Pawn pawn;
        public Faction originalFaction;
        public bool sendNotification;
        public int respawnTick;
        public int tileId = -1;
        public IntVec3 preferredCell = IntVec3.Invalid;
        public int days;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref originalFaction, "originalFaction");
            Scribe_Values.Look(ref sendNotification, "sendNotification", false);
            Scribe_Values.Look(ref respawnTick, "respawnTick", 0);
            Scribe_Values.Look(ref tileId, "tileId", -1);
            Scribe_Values.Look(ref preferredCell, "preferredCell", IntVec3.Invalid);
            Scribe_Values.Look(ref days, "days", 0);
        }
    }
}
