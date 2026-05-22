using System.Collections.Generic;
using Verse;

namespace EasyMode
{
    // Persist per-pawn teleport target mode in save data.
    public class GameComponent_TeleportModeMemory : GameComponent
    {
        private Dictionary<int, int> pawnModes = new Dictionary<int, int>();

        public GameComponent_TeleportModeMemory(Game game)
        {
        }

        internal TeleportTargetMode GetModeForPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return TeleportTargetMode.CurrentMap;
            }

            if (pawnModes.TryGetValue(pawn.thingIDNumber, out int mode) && mode >= 0 && mode <= 1)
            {
                return (TeleportTargetMode)mode;
            }

            return TeleportTargetMode.CurrentMap;
        }

        internal void SetModeForPawn(Pawn pawn, TeleportTargetMode mode)
        {
            if (pawn == null)
            {
                return;
            }

            pawnModes[pawn.thingIDNumber] = (int)mode;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pawnModes, "EasyModeTeleportPawnModes", LookMode.Value, LookMode.Value);

            if (pawnModes == null)
            {
                pawnModes = new Dictionary<int, int>();
            }
        }
    }
}
