# EasyRim project structure convention

This document defines the directory and naming standard for EasyRim. The structure is aligned with the reference style in [references/CustomizeWeapon](../references/CustomizeWeapon) and adapted to this repository's maintenance needs.

## Scope

Production content belongs only to:

- `About/`
- `Defs/`
- `Languages/`
- `Patches/`
- `Source/`
- `Textures/`

Reference-only content belongs only to:

- `references/`

Do not move reference content into production folders unless explicitly requested.

## Def directory layout

Use singular Def type names under `Defs/`:

- `Defs/AbilityDef/`
- `Defs/HediffDef/`
- `Defs/RecipeDef/`
- `Defs/ThingDef/`
- `Defs/ThoughtDef/`

Rule:

- New Def folders must follow singular naming.
- Existing files should be placed under the matching singular Def type folder.

## Def file naming

Use stable, explicit prefixes by Def type:

- Ability files: `Abilities_*.xml` or `abilities_*.xml`
- Hediff files: `hediffs_*.xml`
- Recipe files: `Recipes_*.xml`
- Thing files: `BodyParts_*.xml`, `Buildings_*.xml`, `Items_*.xml`, or other clear Thing-group prefixes
- Thought files: `thoughts_*.xml`

Rules:

- Use `Recipes` spelling only. Do not use `Receipes`.
- Keep released `defName` values stable unless migration impact is accepted.

## Source layout

Classify C# files under `Source/` with CustomWeapon-style role folders:

- `Source/CompProperties/` for `CompProperties_*`, `CompAbilityEffect_*`, and Hediff comp implementation clusters
- `Source/ThingComps/` for Thing-level comps (`ThingComp`)
- `Source/HarmonyPatches/` for Harmony patch and patch entry files
- `Source/Controllers/` for long-lived game/world lifecycle controllers
- `Source/Defs/` for custom Def and DefOf declarations
- `Source/Data/` for debug actions and data-centric utility definitions

Rules:

- New C# source files should be placed in one of the folders above rather than directly under `Source/`.
- Keep namespace compatibility stable; folder moves should not change runtime behavior.

## Localization layout

Keep both English and ChineseSimplified in dual-track localization:

- `Languages/English/Keyed/`
- `Languages/English/DefInjected/`
- `Languages/ChineseSimplified/Keyed/`
- `Languages/ChineseSimplified/DefInjected/`

Use singular Def type names inside each `DefInjected/` directory:

- `AbilityDef/`
- `HediffDef/`
- `RecipeDef/`
- `ThingDef/`
- `ThoughtDef/`
- `TraitDef/`

Rules:

- Add EN and SCH DefInjected entries in parallel for new gameplay-facing defs.
- Avoid splitting the same Def type across singular and plural subfolders.

## Patches layout

Use Def type subfolders under `Patches/`:

- `Patches/ThingDef/`
- `Patches/HediffDef/`
- `Patches/AbilityDef/`

Current state:

- Existing vanilla-balance patch files are under `Patches/ThingDef/`.

Rules:

- Place new patch files in the folder matching the primary patched Def type.
- If a patch touches multiple Def types, choose the dominant target type and add a brief comment header in the XML file.

## References boundary

The repository tracks only `references/README.md` under `references/`.
All pulled or generated reference files remain untracked local material.

## Change process

When changing structure:

1. Apply low-risk renames first (spelling and folder normalization).
2. Keep path references in docs synchronized.
3. Run build and smoke checks after each migration batch.
4. Record important structural decisions in repo memory.

## Validation command

Run this command after structural changes:

- ./scripts/check-structure.sh
