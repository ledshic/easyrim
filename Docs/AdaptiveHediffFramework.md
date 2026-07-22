# Adaptive Hediff framework

The adaptive Hediff framework lets a persistent controller Hediff distribute and revoke functional Hediffs according to declarative XML conditions. Controllers can safely share the same output because every framework-created effect contains persisted provider leases.

## Components

- `HediffCompProperties_ConditionalHediffDistributor` evaluates dynamic rules.
- `ConditionalHediffProfileDef` packages reusable rule sets shared by multiple controllers.
- `HediffCompProperties_GiveMultipleHediffs` handles unconditional persistent bundles and now uses the same ownership model.
- `HediffComp_ManagedEffect` is automatically injected into output defs during reference resolution. Content XML should not add it manually.
- `ManagedHediffEffectUtility` creates, leases, updates, and removes managed effects.

An output created by the framework is removed only when its final provider lease is released. An output that already existed independently is not removed unless the distributor explicitly sets `adoptExistingEffect` (conditional rules) or `adoptExistingHediffs` (multi-effect bundles).

## Conditional rule example

```xml
<Def Class="EasyMode.ConditionalHediffProfileDef">
  <defName>MySharedProfile</defName>
  <rules>
    <li>
      <key>persistent-core</key>
      <activeHediff>MyPersistentEffect</activeHediff>
    </li>
  </rules>
</Def>

<li Class="EasyMode.HediffCompProperties_ConditionalHediffDistributor">
  <checkIntervalTicks>120</checkIntervalTicks>
  <profiles>
    <li>MySharedProfile</li>
  </profiles>
  <rules>
    <li>
      <key>combat-response</key>
      <activeHediff>MyCombatEffect</activeHediff>
      <inCombat>Required</inCombat>
      <healthPercent>0.25~1</healthPercent>
    </li>
  </rules>
</li>
```

Rules are ANDed across condition families. Within a family:

- `all*` requires every entry.
- `any*` requires at least one entry.
- `no*` forbids every matching entry.
- Empty or omitted lists do not restrict the rule.

An entirely conditionless rule is persistent while its controller exists.

Rules can also nest other rules to express grouped logic. Use `allRules`, `anyRules`, and `noRules` when you need parentheses-style combinations such as `A and (B or C)`.

Example:

```xml
<rules>
  <li>
    <key>drafted-ranged-or-combat-melee</key>
    <activeHediff>MyEffect</activeHediff>
    <anyRules>
      <li>
        <drafted>Required</drafted>
        <hasPrimaryEquipment>Required</hasPrimaryEquipment>
        <anyEquipmentTags>
          <li>Ranged</li>
        </anyEquipmentTags>
      </li>
      <li>
        <inCombat>Required</inCombat>
        <hasPrimaryEquipment>Required</hasPrimaryEquipment>
        <anyEquipmentTags>
          <li>Melee</li>
        </anyEquipmentTags>
      </li>
    </anyRules>
  </li>
</rules>
```

Every rule should define a unique, stable `key`. Provider leases use this key in save data, so keys must not be renamed after release. Rules without a key receive a deterministic fallback, but emit a configuration error because list reordering could then change ownership identity.

## Supported conditions

### Hediffs

```xml
<allHediffs>
  <li>
    <hediff>transcendence</hediff>
    <minSeverity>0.5</minSeverity>
    <maxSeverity>1</maxSeverity>
    <minStage>0</minStage>
    <maxStage>2</maxStage>
    <mustBeVisible>false</mustBeVisible>
  </li>
</allHediffs>
```

The same entry form is accepted by `anyHediffs` and `noHediffs`.

### Genes

```xml
<allGenes>
  <li>MeleeDamage_Strong</li>
</allGenes>
<requireActiveGenes>true</requireActiveGenes>
```

Use `MayRequire` on the entire rule when it refers to DLC content. Do not put `MayRequire` only on the final gene list entry: stripping that entry would leave an empty list, which intentionally means “no restriction.”

### Equipment

Rules can inspect both equipped weapons and worn apparel:

```xml
<anyEquipment>
  <li>Gun_ChargeRifle</li>
</anyEquipment>
<anyEquipmentTags>
  <li>Ranged</li>
</anyEquipmentTags>
<includeWeapons>true</includeWeapons>
<includeApparel>true</includeApparel>
```

`allEquipment`, `noEquipment`, `allEquipmentTags`, and `noEquipmentTags` are also supported.

### Jobs and pawn state

```xml
<anyJobs>
  <li>AttackMelee</li>
</anyJobs>
<noJobs>
  <li>Flee</li>
</noJobs>
<drafted>Required</drafted>
<downed>Forbidden</downed>
<inMentalState>Forbidden</inMentalState>
<inCombat>Required</inCombat>
<moving>Any</moving>
<asleep>Forbidden</asleep>
<hasPrimaryEquipment>Required</hasPrimaryEquipment>
<indoors>Any</indoors>
<outdoors>Any</outdoors>
<inLight>Any</inLight>
<inDarkness>Any</inDarkness>
```

State values are `Any`, `Required`, and `Forbidden`.

`drafted` is the raw drafted state only. `inCombat` means an active combat situation, driven by hostile targets or combat jobs, and is no longer a proxy for drafted idle behavior.

`hasPrimaryEquipment` now checks whether the pawn is actually carrying a weapon in equipment, not just whether the primary slot is non-empty.

`indoors` and `outdoors` use roof coverage at the pawn position. `inLight` and `inDarkness` use the map glow level at the pawn position.

### Health

`healthPercent` is a `FloatRange` and defaults to `0~1`.

## Output behavior

- `activeHediff` is required and must use a `HediffWithComps`-derived class.
- `activeSeverity=-1` preserves the output's default severity.
- When several leases request different non-negative severities, the greatest requested severity wins.
- `attachToParentPart=true` attaches the output to the controller's body part. The default is a global Hediff.
- Checks use hash intervals, spreading work across ticks. The default 120-tick interval gives approximately two-second response latency at normal simulation speed.

## Persistent bundles

Existing master Hediffs use `HediffCompProperties_GiveMultipleHediffs`. Persistent bundles now acquire managed leases and clean them up when the master is removed or falls below `atSeverity`.

Relevant properties:

```xml
<maintainGivenHediffs>true</maintainGivenHediffs>
<removeGivenHediffsOnRemoval>true</removeGivenHediffsOnRemoval>
<adoptExistingHediffs>true</adoptExistingHediffs>
```

One-shot distributors (`disappearsAfterGiving=true`) retain one-shot semantics and do not lease their outputs.

## Extension boundary

The current framework evaluates sustained state. Instant events such as “after landing a hit,” “on kill,” or “after using an ability” should be implemented as event-triggered, duration-limited Hediffs rather than by shortening the polling interval. Those event adapters can still use `ManagedHediffEffectUtility` when several sources need to share an output.

## Hediff classification and naming strategy

EasyRim uses a compatibility-first classification model for defs under `Defs/HediffDefs`:

- Gameplay combo trees: parent + module suite status groups.
- Spine bridge Hediffs: implant controllers that distribute combo effects.
- Utility standalone Hediffs: focused one-off tools, buffs, or helper effects.

Reference guide:

- [../Defs/HediffDefs/README.md](../Defs/HediffDefs/README.md)

Compatibility rule:

- Existing released `defName` values are stable by default and should not be renamed for style-only cleanup.
- Apply naming conventions to new or incremental content.

Localization and maintenance rule:

- Any new Hediff in these categories should keep EN/SCH DefInjected entries in sync.
- When adding conditional rules, every rule key should be unique and stable for save compatibility.
- When a helper Def is owned by one Hediff suite (for example `VerbToolProfileDef` used only by one Hediff), prefer co-locating it in the same XML file as the owning suite to reduce split-file drift.
