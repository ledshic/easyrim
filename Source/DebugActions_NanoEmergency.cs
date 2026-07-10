using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using Verse;

namespace EasyMode
{
    /// <summary>
    /// Developer console actions for nano emergency despawn / respawn.
    /// Auto-discovered via <see cref="DebugActionAttribute"/> (GenTypes.AllTypes scan).
    /// </summary>
    public static class DebugActions_NanoEmergency
    {
        /// <summary>
        /// Map tool: click a pawn to force nano emergency despawn (no critical-wound check).
        /// </summary>
        [DebugAction("EasyMode", "Nano: force despawn (click pawn)", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void NanoForceDespawn(Pawn p)
        {
            if (p == null || p.Destroyed || p.Dead)
            {
                Messages.Message("EasyMode_NanoDebugInvalidPawn".Translate(),
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            GameComponent_NanoRespawn comp = GameComponent_NanoRespawn.Instance;
            if (comp == null)
            {
                Messages.Message("EasyMode_NanoDebugNoComponent".Translate(),
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (comp.ForceDespawnNow(p))
            {
                Log.Message($"[EasyMode] Nano force despawn: {p.LabelShortCap}");
            }
        }

        /// <summary>
        /// Immediately respawn every pawn currently waiting for nano reconstruction.
        /// </summary>
        [DebugAction("EasyMode", "Nano: force respawn all pending",
            allowedGameStates = AllowedGameStates.Playing)]
        private static void NanoForceRespawnAll()
        {
            GameComponent_NanoRespawn comp = GameComponent_NanoRespawn.Instance;
            if (comp == null)
            {
                Messages.Message("EasyMode_NanoDebugNoComponent".Translate(),
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            int count = comp.ForceRespawnAll();
            Messages.Message(
                "EasyMode_NanoDebugRespawnAll".Translate(count),
                MessageTypeDefOf.NeutralEvent,
                historical: false);
            Log.Message($"[EasyMode] Nano force respawn all: {count}");
        }

        /// <summary>
        /// Submenu listing each pending reconstruction target for individual force-respawn.
        /// </summary>
        [DebugAction("EasyMode", "Nano: force respawn",
            allowedGameStates = AllowedGameStates.Playing)]
        private static List<DebugActionNode> NanoForceRespawnMenu()
        {
            var nodes = new List<DebugActionNode>();
            GameComponent_NanoRespawn comp = GameComponent_NanoRespawn.Instance;
            if (comp == null)
            {
                nodes.Add(new DebugActionNode("(GameComponent missing)", DebugActionType.Action, () => { }));
                return nodes;
            }

            IReadOnlyList<NanoRespawnEntry> entries = comp.PendingEntries;
            if (entries == null || entries.Count == 0)
            {
                nodes.Add(new DebugActionNode("(no pending nano respawns)", DebugActionType.Action, () => { }));
                return nodes;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                NanoRespawnEntry entry = entries[i];
                Pawn pawn = entry?.pawn;
                if (pawn == null || pawn.Destroyed)
                    continue;

                int remainingTicks = entry.respawnTick - Find.TickManager.TicksGame;
                float remainingDays = remainingTicks / (float)GenDate.TicksPerDay;
                string label = $"{pawn.LabelShortCap} ({remainingDays:0.0}d left)";

                Pawn captured = pawn;
                nodes.Add(new DebugActionNode(label, DebugActionType.Action, () =>
                {
                    GameComponent_NanoRespawn.Instance?.ForceRespawnNow(captured);
                    Log.Message($"[EasyMode] Nano force respawn: {captured.LabelShortCap}");
                }));
            }

            if (nodes.Count == 0)
                nodes.Add(new DebugActionNode("(no valid pending pawns)", DebugActionType.Action, () => { }));

            return nodes;
        }
    }
}
