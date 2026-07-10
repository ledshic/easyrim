using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace EasyMode
{
    public class CompProperties_TunnelerDeepScan : CompProperties
    {
        public IntRange scanIntervalTicks = new IntRange(45000, 90000);
        public IntRange lumpCellCountRange = new IntRange(8, 18);
        public int minDistanceFromEdge = 10;
        public float deepCountMultiplier = 1f;
        public bool playerFactionOnly = true;

        public CompProperties_TunnelerDeepScan()
        {
            compClass = typeof(CompTunnelerDeepScan);
        }
    }

    public class CompTunnelerDeepScan : ThingComp
    {
        private static List<ThingDef> cachedDeepMineables;

        private int ticksUntilNextScan;
        private CompCanBeDormant dormantComp;

        public CompProperties_TunnelerDeepScan Props => (CompProperties_TunnelerDeepScan)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            dormantComp = parent.GetComp<CompCanBeDormant>();

            if (!respawningAfterLoad || ticksUntilNextScan <= 0)
            {
                ResetTimer();
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!ShouldScanNow())
            {
                return;
            }

            ticksUntilNextScan--;
            if (ticksUntilNextScan > 0)
            {
                return;
            }

            TrySpawnDeepResourceLump();
            ResetTimer();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilNextScan, "ticksUntilNextScan", 0);
        }

        public override string CompInspectStringExtra()
        {
            if (!ShouldScanNow())
            {
                return null;
            }

            return "EasyMode_TunnelerDeepScanNext".Translate(
                GenDate.ToStringTicksToPeriod(Math.Max(0, ticksUntilNextScan), allowSeconds: true, shortForm: false));
        }

        private void ResetTimer()
        {
            ticksUntilNextScan = Math.Max(1, Props.scanIntervalTicks.RandomInRange);
        }

        private bool ShouldScanNow()
        {
            if (!parent.Spawned || parent.Map == null)
            {
                return false;
            }

            if (!parent.Map.Biome.hasBedrock)
            {
                return false;
            }

            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || pawn.Downed)
            {
                return false;
            }

            if (Props.playerFactionOnly && pawn.Faction != Faction.OfPlayer)
            {
                return false;
            }

            if (dormantComp == null)
            {
                dormantComp = parent.GetComp<CompCanBeDormant>();
            }

            if (dormantComp != null && !dormantComp.Awake)
            {
                return false;
            }

            return true;
        }

        private void TrySpawnDeepResourceLump()
        {
            Map map = parent.Map;
            if (!TryFindSpawnCenter(map, out IntVec3 center))
            {
                return;
            }

            ThingDef resourceDef = ChooseDeepResourceDef();
            if (resourceDef == null)
            {
                return;
            }

            int numCells = Math.Max(1, Props.lumpCellCountRange.RandomInRange);
            int deepCount = Math.Max(1, Mathf.RoundToInt(resourceDef.deepCountPerCell * Mathf.Max(0.01f, Props.deepCountMultiplier)));

            foreach (IntVec3 cell in GridShapeMaker.IrregularLump(center, map, numCells))
            {
                if (CanPlaceDeepResourceAt(cell, map))
                {
                    map.deepResourceGrid.SetAt(cell, resourceDef, deepCount);
                }
            }
        }

        private bool TryFindSpawnCenter(Map map, out IntVec3 center)
        {
            int minEdgeDist = Math.Max(0, Props.minDistanceFromEdge);
            return CellFinderLoose.TryFindRandomNotEdgeCellWith(
                minEdgeDist,
                cell => CanPlaceDeepResourceAt(cell, map) && !cell.InNoBuildEdgeArea(map),
                map,
                out center);
        }

        private static bool CanPlaceDeepResourceAt(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map))
            {
                return false;
            }

            TerrainDef terrain = map.terrainGrid.BaseTerrainAt(cell);
            if (terrain != null && terrain.IsWater && terrain.passability == Traversability.Impassable)
            {
                return false;
            }

            if (!cell.GetAffordances(map).Contains(ThingDefOf.DeepDrill.terrainAffordanceNeeded))
            {
                return false;
            }

            return map.deepResourceGrid.ThingDefAt(cell) == null;
        }

        private static ThingDef ChooseDeepResourceDef()
        {
            if (cachedDeepMineables == null)
            {
                cachedDeepMineables = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(IsDeepMineableResource)
                    .ToList();
            }

            if (cachedDeepMineables.Count == 0)
            {
                return null;
            }

            return cachedDeepMineables.RandomElementByWeight(def => def.deepCommonality);
        }

        private static bool IsDeepMineableResource(ThingDef def)
        {
            if (def == null || def.deepCommonality <= 0f)
            {
                return false;
            }

            if (def.deepCountPerCell <= 0 || def.deepCountPerPortion <= 0)
            {
                return false;
            }

            if (def.thingCategories == null)
            {
                return false;
            }

            return def.thingCategories.Contains(ThingCategoryDefOf.ResourcesRaw);
        }
    }
}
