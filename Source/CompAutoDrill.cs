using System;
using System.Reflection;
using RimWorld;
using Verse;

namespace EasyMode
{
    public class CompProperties_AutoDrill : CompProperties
    {
        public int drillTicksPerWork = 120; // Tick batch size used for drill work updates
        public float resourceOutputMultiplier = 1f; // Multiplier for resource output
        public IntRange spawnIntervalRange = new IntRange(600, 1200); // Interval between spawns (in ticks)
        public float detectionRadius = 5f; // Search radius for deep resources

        public CompProperties_AutoDrill()
        {
            compClass = typeof(CompAutoDrill);
        }
    }

    public class CompAutoDrill : ThingComp
    {
        private const bool EnableDebugLog = false;
        private const int ResourceCacheDurationTicks = 30;
        private static readonly FieldInfo CompDeepDrillLastUsedTickField = typeof(CompDeepDrill).GetField("lastUsedTick", BindingFlags.Instance | BindingFlags.NonPublic);

        private int ticksUntilSpawn;
        private int workTickAccumulator;

        private int cachedResourceTick = -1;
        private bool cachedHasResource;
        private ThingDef cachedResDef;
        private int cachedCountPresent;
        private IntVec3 cachedCell;

        public CompProperties_AutoDrill Props => (CompProperties_AutoDrill)props;

        private bool PowerOn
        {
            get
            {
                CompPowerTrader comp = parent.GetComp<CompPowerTrader>();
                return comp?.PowerOn ?? true; // If no power comp, assume powered
            }
        }

        public bool ValuableResourcesPresent()
        {
            return GetNextResource(out _, out _, out _);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            DebugLog("PostSpawnSetup called. Parent: " + parent.def.defName);
            DebugLog("Parent spawned: " + parent.Spawned);
            DebugLog("TickerType: " + parent.def.tickerType);

            if (!respawningAfterLoad)
            {
                ResetTimer();
            }
        }

        private void FlickOffExhaustedDrillsAroundParent(IntVec3 cell)
        {
            for (int i = 0; i < GenRadial.NumCellsInRadius(4f); i++)
            {
                IntVec3 val = cell + GenRadial.RadialPattern[i];
                if (GenGrid.InBounds(val, ((Thing)base.parent).Map))
                {
                    ThingWithComps firstThingWithComp = GridsUtility.GetFirstThingWithComp<CompAutoDrill>(val, ((Thing)base.parent).Map);
                    if (firstThingWithComp != null && !firstThingWithComp.GetComp<CompAutoDrill>().ValuableResourcesPresent())
                    {
                        firstThingWithComp.GetComp<CompFlickable>().SwitchIsOn = false;
                    }
                }
            }
        }

        private void FlickCheckResourceExhaustion(IntVec3 cell)
        {
            if (!ValuableResourcesPresent())
            {
                FlickOffExhaustedDrillsAroundParent(cell);
            }
        }

        private bool GetNextResource(out ThingDef resDef, out int countPresent, out IntVec3 cell)
        {
            if (parent.Map == null)
            {
                resDef = null;
                countPresent = 0;
                cell = IntVec3.Invalid;
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            bool cacheValid = cachedResourceTick >= 0 && currentTick - cachedResourceTick <= ResourceCacheDurationTicks;

            if (!cacheValid)
            {
                cachedHasResource = ScanNextResource(out cachedResDef, out cachedCountPresent, out cachedCell);
                cachedResourceTick = currentTick;
            }

            resDef = cachedResDef;
            countPresent = cachedCountPresent;
            cell = cachedCell;
            return cachedHasResource;
        }

        private bool ScanNextResource(out ThingDef resDef, out int countPresent, out IntVec3 cell)
        {
            float radius = Props.detectionRadius > 0f
                ? Props.detectionRadius
                : Math.Max(0f, ((BuildableDef)parent.def).specialDisplayRadius);

            for (int i = 0; i < GenRadial.NumCellsInRadius(radius); i++)
            {
                IntVec3 val = parent.Position + GenRadial.RadialPattern[i];
                if (GenGrid.InBounds(val, parent.Map))
                {
                    ThingDef val2 = parent.Map.deepResourceGrid.ThingDefAt(val);
                    if (val2 != null && val2.thingCategories.Contains(ThingCategoryDefOf.ResourcesRaw))
                    {
                        resDef = val2;
                        countPresent = parent.Map.deepResourceGrid.CountAt(val);
                        cell = val;
                        return true;
                    }
                }
            }

            resDef = DeepDrillUtility.GetBaseResource(parent.Map, parent.Position);
            countPresent = int.MaxValue;
            cell = parent.Position;
            return false;
        }

        private void InvalidateResourceCache()
        {
            cachedResourceTick = -1;
        }

        private void SpawnResource(ThingDef resDef, int countPresent, IntVec3 cell)
        {
            int num = Math.Min(countPresent, resDef.deepCountPerPortion);
            parent.Map.deepResourceGrid.SetAt(cell, resDef, Math.Max(0, countPresent - num));

            float adjustedYield = (float)num * Props.resourceOutputMultiplier * Find.Storyteller.difficulty.mineYieldFactor;
            Thing obj = ThingMaker.MakeThing(resDef, (ThingDef)null);
            obj.stackCount = Math.Max(1, GenMath.RoundRandom(adjustedYield));
            GenPlace.TryPlaceThing(obj, parent.Position, parent.Map, ThingPlaceMode.Near, null, null, null, 1);

            InvalidateResourceCache();
        }

        public void TrySpawn()
        {
            ThingDef resDef;
            int countPresent;
            IntVec3 cell;
            bool nextResource = GetNextResource(out resDef, out countPresent, out cell);
            if (resDef != null)
            {
                if (nextResource)
                {
                    SpawnResource(resDef, countPresent, cell);
                    FlickCheckResourceExhaustion(cell);
                }
            }
        }

        public void ResetTimer()
        {
            ticksUntilSpawn = Props.spawnIntervalRange.RandomInRange;
            DebugLog("Timer reset. Next spawn in " + ticksUntilSpawn + " ticks.");
        }

        private static void DebugLog(string message)
        {
            if (EnableDebugLog)
            {
                Log.Message("[AutoDrill] " + message);
            }
        }

        private bool CanDrillNow()
        {
            if (!parent.Spawned || !PowerOn)
            {
                // Log.Message("[AutoDrill] Cannot work: No power.");
                return false;
            }

            CompFlickable flickable = parent.GetComp<CompFlickable>();
            if (flickable != null && !flickable.SwitchIsOn)
            {
                // Log.Message("[AutoDrill] Cannot work: Switch is off.");
                return false;
            }

            CompForbiddable forbiddable = parent.GetComp<CompForbiddable>();
            if (forbiddable != null && forbiddable.Forbidden)
            {
                // Log.Message("[AutoDrill] Cannot work: Forbidden.");
                return false;
            }

            if (!GetNextResource(out _, out _, out _))
            {
                // Log.Message("[AutoDrill] Cannot work: No resources found.");
                return false;
            }

            return true;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!CanDrillNow())
            {
                workTickAccumulator = 0;
                return;
            }

            // Keep vanilla deep drill infestation eligibility in sync with auto-drill activity.
            MarkVanillaDeepDrillUsedThisTick();

            workTickAccumulator++;
            int workTicks = Math.Max(1, Props.drillTicksPerWork);
            if (workTickAccumulator < workTicks)
                return;

            // Reduce by elapsed batched ticks to preserve the same overall timing as per-tick updates.
            ticksUntilSpawn -= workTickAccumulator;
            workTickAccumulator = 0;

            if (ticksUntilSpawn <= 0)
            {
                TrySpawn();
                ResetTimer();
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilSpawn, "ticksUntilSpawn", 0);
            Scribe_Values.Look(ref workTickAccumulator, "workTickAccumulator", 0);
        }

        private void MarkVanillaDeepDrillUsedThisTick()
        {
            if (CompDeepDrillLastUsedTickField == null)
            {
                return;
            }

            CompDeepDrill deepDrill = parent.GetComp<CompDeepDrill>();
            if (deepDrill == null)
            {
                return;
            }

            CompDeepDrillLastUsedTickField.SetValue(deepDrill, Find.TickManager.TicksGame);
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned)
                return null;

            if (!PowerOn)
                return "AutoDrillStopped".Translate();

            CompFlickable flickable = parent.GetComp<CompFlickable>();
            if (flickable != null && !flickable.SwitchIsOn)
                return "AutoDrillStopped".Translate();

            CompForbiddable forbiddable = parent.GetComp<CompForbiddable>();
            if (forbiddable != null && forbiddable.Forbidden)
                return "AutoDrillStopped".Translate();

            if (GetNextResource(out ThingDef resDef, out _, out _))
            {
                return "AutoDrillWorking".Translate() + "\n" +
                       "NextSpawnedItemIn".Translate(resDef.label) + " " +
                       GenDate.ToStringTicksToPeriod(ticksUntilSpawn, allowSeconds: true, shortForm: false);
            }

            return "AutoDrillStopped".Translate();
        }
    }
}
