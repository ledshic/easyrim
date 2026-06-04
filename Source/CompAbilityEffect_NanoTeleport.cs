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
        // ── Local-map teleport ────────────────────────────────────────────────

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

        // ── World-tile teleport ───────────────────────────────────────────────
        //
        // Called by Verb_CastAbility for world targets when
        // verbProperties.targetWorldCell == true.
        //
        // Decision matrix (no FloatMenu — avoids potential MP desync):
        //   • Own colony tile  → enter map, spawn near centre
        //   • Everything else  → form a solo caravan at that tile

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

        // Enter an owned colony map, generating it first if it hasn't been loaded.
        private void DoEnterOwnMap(Pawn pawn, int tile, MapParent mapParent)
        {
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

            pawn.DeSpawn(DestroyMode.Vanish);
            GenSpawn.Spawn(pawn, landingCell, destMap);
            pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: false);
        }

        // Leave the current map and form a solo caravan at the destination tile.
        private void DoFormCaravan(Pawn pawn, int tile)
        {
            // Only despawn if the pawn is currently in a map. If already on world map
            // (e.g., in a caravan), they won't be spawned in any map.
            if (pawn.Map != null)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }
            CaravanMaker.MakeCaravan(Gen.YieldSingle(pawn), Faction.OfPlayer, tile,
                addToWorldPawnsIfNotAlready: true);
        }
    }
}
