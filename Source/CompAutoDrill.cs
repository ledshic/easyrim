using System;
using RimWorld;
using Verse;

namespace EasyMode
{
    public class CompProperties_AutoDrill : CompProperties
    {
        public int drillTicksPerWork = 120; // Time between drill attempts (in ticks)
        public float resourceOutputMultiplier = 1f; // Multiplier for resource output
        public IntRange spawnIntervalRange = new IntRange(600, 1200); // Interval between spawns (in ticks)
        public float detectionRadius = 5f; // 检测半径

        public CompProperties_AutoDrill()
        {
            compClass = typeof(CompAutoDrill);
        }
    }

    public class CompAutoDrill : ThingComp
    {
        private int ticksUntilSpawn;

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
            ThingDef resDef;
            int countPresent;
            IntVec3 cell;
            return GetNextResource(out resDef, out countPresent, out cell);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            Log.Message("[AutoDrill] PostSpawnSetup called. Parent: " + parent.def.defName);
            Log.Message("[AutoDrill] Parent spawned: " + parent.Spawned);
            Log.Message("[AutoDrill] TickerType: " + parent.def.tickerType);

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
            for (int i = 0; i < GenRadial.NumCellsInRadius(Math.Max(0f, ((BuildableDef)((Thing)base.parent).def).specialDisplayRadius)); i++)
            {
                IntVec3 val = ((Thing)base.parent).Position + GenRadial.RadialPattern[i];
                if (GenGrid.InBounds(val, ((Thing)base.parent).Map))
                {
                    ThingDef val2 = ((Thing)base.parent).Map.deepResourceGrid.ThingDefAt(val);
                    if (val2 != null && val2.thingCategories.Contains(ThingCategoryDefOf.ResourcesRaw))
                    {
                        resDef = val2;
                        countPresent = ((Thing)base.parent).Map.deepResourceGrid.CountAt(val);
                        cell = val;
                        return true;
                    }
                }
            }
            resDef = DeepDrillUtility.GetBaseResource(((Thing)base.parent).Map, ((Thing)base.parent).Position);
            countPresent = int.MaxValue;
            cell = ((Thing)base.parent).Position;
            return false;
        }

        private void SpawnResource(ThingDef resDef, int countPresent, IntVec3 cell)
        {
            int num = Math.Min(countPresent, resDef.deepCountPerPortion);

            ((Thing)base.parent).Map.deepResourceGrid.SetAt(cell, resDef, Math.Max(0, countPresent - GenMath.RoundRandom((float)num)));

            Thing obj = ThingMaker.MakeThing(resDef, (ThingDef)null);
            obj.stackCount = Math.Max(1, GenMath.RoundRandom((float)num * Find.Storyteller.difficulty.mineYieldFactor));
            GenPlace.TryPlaceThing(obj, ((Thing)base.parent).Position, ((Thing)base.parent).Map, (ThingPlaceMode)1, (Action<Thing, int>)null, (Predicate<IntVec3>)null, (Rot4?)null, 1);

        }

        public void TrySpawn()
        {
            //IL_0017: Unknown result type (might be due to invalid IL or missing references)
            //IL_001e: Unknown result type (might be due to invalid IL or missing references)
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
            Log.Message("[AutoDrill] Timer reset. Next spawn in " + ticksUntilSpawn + " ticks.");
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

            // Debugging log to ensure this function is called every tick
            // Log.Message("[AutoDrill] CompTick called.");

            if (!CanDrillNow())
                return;

            ticksUntilSpawn--;
            // Log.Message("[AutoDrill] ticksUntilSpawn: " + ticksUntilSpawn);  // Added log to track ticks until spawn

            if (ticksUntilSpawn <= 0)
            {
                // Log.Message("[AutoDrill] Spawning resource...");
                TrySpawn();
                ResetTimer();
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilSpawn, "ticksUntilSpawn", 0);
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned)
                return null;

            if (!PowerOn)
                return "NoPower".Translate();

            CompFlickable flickable = parent.GetComp<CompFlickable>();
            if (flickable != null && !flickable.SwitchIsOn)
                return "SwitchedOff".Translate();

            CompForbiddable forbiddable = parent.GetComp<CompForbiddable>();
            if (forbiddable != null && forbiddable.Forbidden)
                return "Forbidden".Translate();

            if (GetNextResource(out ThingDef resDef, out _, out _))
            {
                return "ResourceBelow".Translate() + ": " + resDef.LabelCap + "\n" +
                       "NextSpawnedItemIn".Translate(resDef.label) + " " +
                       GenDate.ToStringTicksToPeriod(ticksUntilSpawn, allowSeconds: true, shortForm: false);
            }

            return "DeepDrillNoResources".Translate();
        }
    }
}
