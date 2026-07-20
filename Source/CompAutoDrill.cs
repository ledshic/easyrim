using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace EasyMode
{
    public enum AutoDrillPowerMode
    {
        ExternalPower,
        Solar
    }

    public enum AutoDrillOperatingMode
    {
        HighSpeed,
        HighPrecision,
        Safe
    }

    public class CompProperties_AutoDrill : CompProperties
    {
        public int drillTicksPerWork = 120;
        public float resourceOutputMultiplier = 1f;
        public float externalPowerConsumption = 1000f;
        public float highPrecisionEfficiency = 1f / 3f;
        public float safeEfficiency = 0.5f;
        public IntRange spawnIntervalRange = new IntRange(600, 1200);
        public float detectionRadius = 5f;

        public CompProperties_AutoDrill()
        {
            compClass = typeof(CompAutoDrill);
        }
    }

    public class CompAutoDrill : ThingComp
    {
        private const int ResourceCacheDurationTicks = 30;
        private static readonly FieldInfo CompDeepDrillLastUsedTickField = typeof(CompDeepDrill).GetField("lastUsedTick", BindingFlags.Instance | BindingFlags.NonPublic);

        private int ticksUntilSpawn;
        private int workTickAccumulator;
        private AutoDrillPowerMode powerMode = AutoDrillPowerMode.ExternalPower;
        private AutoDrillOperatingMode operatingMode = AutoDrillOperatingMode.HighSpeed;

        private int cachedResourceTick = -1;
        private bool cachedHasResource;
        private ThingDef cachedResDef;
        private int cachedCountPresent;
        private IntVec3 cachedCell;

        public CompProperties_AutoDrill Props => (CompProperties_AutoDrill)props;

        private float DrillingEfficiency
        {
            get
            {
                switch (operatingMode)
                {
                    case AutoDrillOperatingMode.HighPrecision:
                        return Mathf.Clamp01(Props.highPrecisionEfficiency);
                    case AutoDrillOperatingMode.Safe:
                        return Mathf.Clamp01(Props.safeEfficiency);
                    default:
                        return 1f;
                }
            }
        }

        private float RoofedPowerOutputFactor
        {
            get
            {
                int total = 0;
                int roofed = 0;
                foreach (IntVec3 cell in parent.OccupiedRect())
                {
                    total++;
                    if (parent.Map.roofGrid.Roofed(cell))
                    {
                        roofed++;
                    }
                }

                return total > 0 ? (float)(total - roofed) / total : 0f;
            }
        }

        private bool HasSolarPowerNow()
        {
            if (parent.Map == null)
            {
                return false;
            }

            // Match vanilla solar plant detection: CurSkyGlow multiplied by unroofed coverage.
            float solarFactor = parent.Map.skyManager.CurSkyGlow * RoofedPowerOutputFactor;
            return solarFactor > 0f;
        }

        public bool ValuableResourcesPresent()
        {
            return GetNextResource(out _, out _, out _);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // Recompute deep resource state for the new position after reinstall/redeploy.
            InvalidateResourceCache();

            if (!respawningAfterLoad)
            {
                ResetTimer();
                TryRearmAfterRedeploy();
            }

            UpdatePowerConsumption();
            if (operatingMode == AutoDrillOperatingMode.Safe)
            {
                ClearVanillaDeepDrillUsage();
            }
        }

        private void TryRearmAfterRedeploy()
        {
            CompFlickable flickable = parent.GetComp<CompFlickable>();
            if (flickable == null || flickable.SwitchIsOn)
            {
                return;
            }

            if (ValuableResourcesPresent())
            {
                flickable.SwitchIsOn = true;
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

            float adjustedYield = operatingMode == AutoDrillOperatingMode.HighPrecision
                ? num
                : (float)num * Props.resourceOutputMultiplier * Find.Storyteller.difficulty.mineYieldFactor;
            Thing obj = ThingMaker.MakeThing(resDef, (ThingDef)null);
            obj.stackCount = Math.Max(1, GenMath.RoundRandom(adjustedYield));
            GenPlace.TryPlaceThing(obj, parent.Position, parent.Map, ThingPlaceMode.Near, null, null, null, 1);

            InvalidateResourceCache();
        }

        private void TryMarkUninstallDesignation()
        {
            if (parent?.Map == null || !parent.Spawned)
            {
                return;
            }

            if (parent.Map.designationManager.DesignationOn(parent, DesignationDefOf.Uninstall) != null)
            {
                return;
            }

            if (parent.Map.designationManager.DesignationOn(parent, DesignationDefOf.Deconstruct) != null)
            {
                return;
            }

            Building building = parent as Building;
            if (building == null || building.def.category != ThingCategory.Building || !building.def.Minifiable)
            {
                return;
            }

            if (building.Faction != Faction.OfPlayer && (building.def.building == null || !building.def.building.alwaysUninstallable))
            {
                return;
            }

            parent.Map.designationManager.AddDesignation(new Designation(parent, DesignationDefOf.Uninstall));
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

                    if (!ValuableResourcesPresent())
                    {
                        TryMarkUninstallDesignation();
                    }
                }
            }
        }

        public void ResetTimer()
        {
            ticksUntilSpawn = Props.spawnIntervalRange.RandomInRange;
        }

        private bool CanDrillNow()
        {
            if (!parent.Spawned || !HasRequiredPower())
                return false;

            CompFlickable flickable = parent.GetComp<CompFlickable>();
            if (flickable != null && !flickable.SwitchIsOn)
                return false;

            CompForbiddable forbiddable = parent.GetComp<CompForbiddable>();
            if (forbiddable != null && forbiddable.Forbidden)
                return false;

            if (!GetNextResource(out _, out _, out _))
                return false;

            return true;
        }

        private bool HasRequiredPower()
        {
            if (powerMode == AutoDrillPowerMode.Solar)
            {
                return HasSolarPowerNow();
            }

            CompPowerTrader power = parent.GetComp<CompPowerTrader>();
            return power != null && power.PowerOn;
        }

        private void UpdatePowerConsumption()
        {
            CompPowerTrader power = parent.GetComp<CompPowerTrader>();
            if (power == null)
            {
                return;
            }

            power.PowerOutput = powerMode == AutoDrillPowerMode.ExternalPower
                ? -Math.Abs(Props.externalPowerConsumption)
                : 0f;
        }

        public override void CompTick()
        {
            base.CompTick();

            UpdatePowerConsumption();

            if (!CanDrillNow())
            {
                workTickAccumulator = 0;
                return;
            }

            // Safe mode deliberately stays outside vanilla deep-drill infestation eligibility.
            if (operatingMode != AutoDrillOperatingMode.Safe)
            {
                MarkVanillaDeepDrillUsedThisTick();
            }

            workTickAccumulator++;
            int workTicks = Math.Max(1, Props.drillTicksPerWork);
            if (workTickAccumulator < workTicks)
                return;

            // Reduce by elapsed batched ticks to preserve the same overall timing as per-tick updates.
            ticksUntilSpawn -= Math.Max(1, GenMath.RoundRandom(workTickAccumulator * DrillingEfficiency));
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
            Scribe_Values.Look(ref powerMode, "powerMode", AutoDrillPowerMode.ExternalPower);
            Scribe_Values.Look(ref operatingMode, "operatingMode", AutoDrillOperatingMode.HighSpeed);
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

        private void ClearVanillaDeepDrillUsage()
        {
            if (CompDeepDrillLastUsedTickField == null)
            {
                return;
            }

            CompDeepDrill deepDrill = parent.GetComp<CompDeepDrill>();
            if (deepDrill != null)
            {
                CompDeepDrillLastUsedTickField.SetValue(deepDrill, Find.TickManager.TicksGame - GenDate.TicksPerDay);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            yield return new Command_Action
            {
                defaultLabel = (powerMode == AutoDrillPowerMode.ExternalPower
                    ? "AutoDrillPowerExternal"
                    : "AutoDrillPowerSolar").Translate(),
                defaultDesc = "AutoDrillPowerModeDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/TogglePower"),
                action = delegate
                {
                    powerMode = powerMode == AutoDrillPowerMode.ExternalPower
                        ? AutoDrillPowerMode.Solar
                        : AutoDrillPowerMode.ExternalPower;
                    UpdatePowerConsumption();
                }
            };

            yield return new Command_Action
            {
                defaultLabel = OperatingModeLabel,
                defaultDesc = "AutoDrillOperatingModeDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/ChangeStyle"),
                action = delegate
                {
                    operatingMode = (AutoDrillOperatingMode)(((int)operatingMode + 1) % 3);
                    if (operatingMode == AutoDrillOperatingMode.Safe)
                    {
                        ClearVanillaDeepDrillUsage();
                    }
                }
            };
        }

        private string OperatingModeLabel
        {
            get
            {
                switch (operatingMode)
                {
                    case AutoDrillOperatingMode.HighPrecision:
                        return "AutoDrillModeHighPrecision".Translate();
                    case AutoDrillOperatingMode.Safe:
                        return "AutoDrillModeSafe".Translate();
                    default:
                        return "AutoDrillModeHighSpeed".Translate();
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned)
                return null;

            if (!HasRequiredPower())
                return "AutoDrillStopped".Translate();

            CompFlickable flickable = parent.GetComp<CompFlickable>();
            if (flickable != null && !flickable.SwitchIsOn)
                return "AutoDrillStopped".Translate();

            CompForbiddable forbiddable = parent.GetComp<CompForbiddable>();
            if (forbiddable != null && forbiddable.Forbidden)
                return "AutoDrillStopped".Translate();

            if (GetNextResource(out ThingDef resDef, out _, out _))
            {
                int remainingTicks = Mathf.CeilToInt(ticksUntilSpawn / Math.Max(0.01f, DrillingEfficiency));
                string powerLabel = (powerMode == AutoDrillPowerMode.ExternalPower
                    ? "AutoDrillPowerExternal"
                    : "AutoDrillPowerSolar").Translate();
                return "AutoDrillWorking".Translate() + "\n" +
                       powerLabel + " / " + OperatingModeLabel + "\n" +
                       "NextSpawnedItemIn".Translate(resDef.label) + " " +
                       GenDate.ToStringTicksToPeriod(remainingTicks, allowSeconds: true, shortForm: false);
            }

            return "AutoDrillStopped".Translate();
        }
    }
}
