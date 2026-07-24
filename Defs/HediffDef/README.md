# Hediff classification and naming guide

This directory contains all EasyRim Hediff definitions. To keep future content maintainable and compatible, use the following classification and naming rules.

## Compatibility first

- Do not rename released `defName` values unless the migration impact is explicitly accepted.
- Do not mass-rename historical XML files only for style.
- Apply naming rules to new files and incremental additions.

## Category model

All Hediff defs in this directory should be classified into one of the categories below.

### 1) Gameplay combo trees

Purpose: long-lived enhancement suites composed of a parent Hediff plus multiple sub-Hediffs.

Current files:

- `hediffs_transcendence.xml`
- `hediffs_bravesoul.xml`
- `hediffs_corsair.xml`
- `hediffs_pioneer.xml`
- `hediffs_frontier_warden.xml`
- `hediffs_vampire.xml`

`hediffs_vampire.xml` also contains `bloodhemogen_resonance`.
`hediffs_vampire.xml` also contains `EM_CrimsonDynastySpine`.
`hediffs_transcendence.xml` also contains `TranscendenceBattleMorphToolset`.
`hediffs_transcendence.xml` also contains `EM_AdaptiveNexusSpine`.
`hediffs_pioneer.xml` also contains `EM_PioneerSpine` and `WorkMotivation`.
`hediffs_corsair.xml` also contains `EM_CorsairSpine`.
`hediffs_bravesoul.xml` also contains `EM_BraveSpine`.
`hediffs_frontier_warden.xml` also contains `EM_FrontierWardenSpine`.

Typical shape:

- One parent status (`transcendence`, `bravesoul`, `corsaircreed`, ...)
- Child modules named `parent_module` (`transcendence_physical`, `bravesoul_combat`, ...)
- Persistent distribution via `HediffCompProperties_GiveMultipleHediffs`

### 2) Spine bridge Hediffs

Purpose: implant-style bridge defs that conditionally distribute combo tree modules.

Current files:

- none (all spine bridge defs are now co-located with their owning combo-tree files)

Typical shape:

- Primary implant defName pattern: `EM_<Theme>Spine`
- Rule/profile/helper defs may use `EM_` prefixed internal names
- Conditional distribution via `HediffCompProperties_ConditionalHediffDistributor`

### 3) Utility standalone Hediffs

Purpose: single-purpose tool, buff, proc, or helper not modeled as a full combo tree.

Current files:

- none (utility effects are currently co-located in combo-tree files)

Typical shape:

- Focused behavior with no multi-branch status tree contract
- May be granted by abilities, jobs, or conditional distributors

## Naming conventions for new content

Use these conventions for new defs while keeping existing names stable.

### File names

- Combo tree file: `hediffs_<theme>.xml`
- Spine bridge file: `hediffs_<theme>_spine.xml`
- Utility standalone file: `hediffs_<theme>_<purpose>.xml` when practical

### DefName patterns

- Combo parent: `<theme>` (lowercase compact or snake style consistent with its suite)
- Combo child: `<parent>_<module>`
- Spine implant: `EM_<Theme>Spine`
- Internal helper/rule output: `EM_<Domain>_<Purpose>`

Notes:

- Existing legacy names such as `WorkMotivation` and `EasyMode_NeuralOverclock` are valid and remain unchanged.
- For new utility/internal defs, prefer `EM_` for easier grep and tooling classification.

## Add/change checklist

When adding or changing a Hediff in this directory:

1. Classify it as combo tree, spine bridge, or utility standalone.
2. Keep `defName` compatibility unless a migration is explicitly approved.
3. Update EN/SCH DefInjected text in parallel.
4. Ensure references stay consistent across XML and C# (`Source/`).
5. If introducing bridge rules, use stable unique `key` values for distributor rules.

## Quick mapping reference

- Combo tree owner status: player-facing persistent enhancement group
- Spine bridge: implant controller that grants or revokes grouped effects
- Utility standalone: narrow, task-specific effect without full tree semantics

## Chinese summary

- 本目录 Hediff 分三类：玩法组合树、脊柱桥接、工具性单体。
- 已发布 `defName` 默认不改，优先兼容旧存档与外部补丁。
- 新增内容按规范命名；历史命名不强制回溯重构。
- 新增/修改时务必同步英文与简中 DefInjected 文本。
