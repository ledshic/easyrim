using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EasyMode
{
    public class CompProperties_NanoTeleport : CompProperties_AbilityEffect
    {
        public CompProperties_NanoTeleport()
        {
            compClass = typeof(CompAbilityEffect_NanoTeleport);
        }
    }

    public class CompAbilityEffect_NanoTeleport : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn pawn = parent.pawn;

            // Use vanilla position-change approach (same as SkipUtility.SkipTo) instead of
            // DeSpawn + GenSpawn.Spawn, which triggers extra RNG-dependent callbacks and
            // side effects that can desync RWMT Multiplayer clients.
            pawn.Position = target.Cell;
            pawn.Notify_Teleported(false, true);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!target.IsValid || !target.Cell.InBounds(parent.pawn.Map))
                return false;

            // Reject impassable terrain / cells occupied by walls or buildings.
            if (!target.Cell.Walkable(parent.pawn.Map))
            {
                if (throwMessages)
                    Messages.Message("AbilityNotValidUnwalkable".Translate(),
                        MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            return true;
        }

        public override bool Valid(GlobalTargetInfo target, bool throwMessages = false)
        {
            if (!target.IsValid || target.Tile < 0)
                return false;

            Pawn pawn = parent.pawn;
            if (pawn != null)
            {
                int currentTile = -1;
                Caravan c = pawn.GetCaravan();
                if (c != null)
                    currentTile = c.Tile;
                else if (pawn.Spawned)
                    currentTile = pawn.Map.Tile;
                if (target.Tile == currentTile)
                    return false;
            }

            return true;
        }

        public override void Apply(GlobalTargetInfo target)
        {
            int tile = target.Tile;
            Pawn pawn = parent.pawn;
            MapParent mapParent = Find.WorldObjects.MapParentAt(tile);

            if (mapParent?.Faction == Faction.OfPlayer)
            {
                DoEnterOwnMap(pawn, tile, mapParent);
            }
            else
            {
                DoFormCaravan(pawn, tile);
            }
        }

        // Safely extract pawn from any current caravan (when on world map) and/or
        // despawn from current map before performing a world-level teleport.
        // This prevents orphaned caravan references, duplicate caravan membership,
        // and "tried to despawn but not spawned" errors.
        private void ExitCurrentCaravanOrMap(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed)
                return;

            Caravan caravan = pawn.GetCaravan();
            if (caravan != null && !caravan.Destroyed)
            {
                bool shouldDestroy = caravan.PawnsListForReading.Count == 1 &&
                                     caravan.PawnsListForReading[0] == pawn;
                caravan.RemovePawn(pawn);
                if (shouldDestroy && caravan.PawnsListForReading.Count == 0)
                {
                    caravan.Destroy();
                }
            }

            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }

            // Stop any active map pathing or stances (safe when on world/caravan too).
            pawn.pather?.StopDead();
            pawn.stances?.CancelBusyStanceSoft();
        }

        // Enter an owned colony map, generating it first if it hasn't been loaded.
        private void DoEnterOwnMap(Pawn pawn, int tile, MapParent mapParent)
        {
            if (pawn == null || pawn.Destroyed)
                return;

            ExitCurrentCaravanOrMap(pawn);

            Map destMap = GetOrGenerateMapUtility.GetOrGenerateMap(
                tile, mapParent.def, null);

            if (destMap == null)
            {
                Messages.Message("EasyMode_TeleportMapGenFailed".Translate(),
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (!RCellFinder.TryFindRandomCellNearWith(
                destMap.Center,
                c => c.Walkable(destMap),
                destMap,
                out IntVec3 landingCell,
                1,
                10))
                landingCell = destMap.Center;

            GenSpawn.Spawn(pawn, landingCell, destMap);
            pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: false);
        }

        // Leave the current map/caravan and form a solo caravan at the destination tile.
        private void DoFormCaravan(Pawn pawn, int tile)
        {
            if (pawn == null || pawn.Destroyed)
                return;

            ExitCurrentCaravanOrMap(pawn);
            CaravanMaker.MakeCaravan(Gen.YieldSingle(pawn), Faction.OfPlayer, tile,
                addToWorldPawnsIfNotAlready: true);
        }
    }
}
