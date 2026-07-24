# ConditionalHediffDistributor 中的武器分类改进

**更新时间:** 2026-07-24
**改进内容:** 使用 `weaponClasses` 作为武器分类的权威标准

## 概述

`HediffCompProperties_ConditionalHediffDistributor` 中的武器判断逻辑已改进，现在**直接使用 RimWorld XML 定义的 `weaponClasses`** 作为判断标准，而不依赖 C# API 属性 (`IsRangedWeapon`/`IsMeleeWeapon`)。

这确保了分类的**严谨性、准确性和可维护性**。

---

## 改进前后对比

### 改进前（弃用）
```csharp
// 依赖 IsRangedWeapon/IsMeleeWeapon 属性
if (string.Equals(normalizedTag, "Ranged", StringComparison.OrdinalIgnoreCase)
    && def.IsRangedWeapon)
{
    return true;
}
```

**问题：**
- 依赖 API 属性，不够直接
- 语义不清楚

### 改进后（现在）
```csharp
// 直接检查 weaponClasses（权威标准）
if (MatchesWeaponClass(def, normalizedTag))
{
    return true;
}
```

**优势：**
- 使用 XML 定义的 `weaponClasses` 作为权威标准
- 支持精细分类（`RangedLight` vs `RangedHeavy` 等）
- 完全基于 XML 配置，不需要代码改动

---

## 判断优先级（Priority Chain）

当在 XML 中指定 `<anyEquipmentTags>` 时，系统按以下优先级判断：

### Priority 1: weaponClasses（最权威）
```csharp
// 检查武器的 weaponClasses 定义
if (MatchesWeaponClass(def, normalizedTag))
{
    return true;
}
```

**支持的分类名称：**

| 类别 | weaponClass 名称 | 说明 |
|------|-----------------|------|
| **近战总类** | `Melee` | 所有近战武器（语义别名） |
| **近战细分** | `MeleePiercer` | 穿刺类（如剑） |
| | `MeleeBlunt` | 钝击类（如棒槌） |
| | `Neolithic` | 新石器时代武器 |
| **远程总类** | `Ranged` | 所有远程武器（语义别名） |
| **远程细分** | `RangedLight` | 轻型远程（手枪等） |
| | `RangedHeavy` | 重型远程（狙击枪等） |
| | `LongShots` | 长距离射击 |
| | `ShortShots` | 短距离射击 |
| | `Ultratech` | 超科技武器 |

### Priority 2: weaponTags（灵活标签）
```csharp
// 如果 weaponClasses 未匹配，检查 weaponTags
if (def.weaponTags?.Contains(normalizedTag) == true)
{
    return true;
}
```

这支持**任何自定义标签**，如：
- `SimpleGun`
- `MedievalMeleeDecent`
- `NeolithicRangedBasic`
- 任何用户定义的标签

### Priority 3: Apparel Tags（服装标签）
对穿着的衣物检查标签（如果 `includeApparel=true`）。

---

## XML 使用示例

### 示例 1: 远程武器条件
```xml
<li>
    <key>pioneer-vanguard-fire</key>
    <activeHediff>pioneerspirit_vanguard_fire</activeHediff>
    <activeSeverity>1.0</activeSeverity>
    <anyEquipmentTags>
        <li>Ranged</li>  <!-- 匹配所有远程武器类别 -->
    </anyEquipmentTags>
    <inCombat>Required</inCombat>
</li>
```

**效果：** 装备任何 `Ranged`、`RangedLight`、`RangedHeavy`、`LongShots`、`ShortShots` 或 `Ultratech` 分类的武器时，激活此 Hediff。

### 示例 2: 精细分类（轻型远程）
```xml
<li>
    <key>specialist-light-ranged</key>
    <activeHediff>specialistLightRanged</activeHediff>
    <activeSeverity>1.0</activeSeverity>
    <anyEquipmentTags>
        <li>RangedLight</li>  <!-- 仅匹配轻型远程 -->
    </anyEquipmentTags>
</li>
```

**效果：** 仅当装备 `RangedLight` 分类的武器（如手枪）时，激活此 Hediff。

### 示例 3: 近战武器条件
```xml
<li>
    <key>warrior-melee</key>
    <activeHediff>warriorMeleeBonus</activeHediff>
    <activeSeverity>1.0</activeSeverity>
    <anyEquipmentTags>
        <li>Melee</li>  <!-- 匹配所有近战武器类别 -->
    </anyEquipmentTags>
</li>
```

**效果：** 装备任何 `Melee`、`MeleePiercer`、`MeleeBlunt` 或 `Neolithic` 分类的武器时，激活此 Hediff。

### 示例 4: 组合条件（多标签）
```xml
<li>
    <key>advanced-light-ranged-in-combat</key>
    <activeHediff>advancedLightRanged</activeHediff>
    <activeSeverity>1.0</activeSeverity>
    <allEquipmentTags>
        <li>RangedLight</li>  <!-- AND: 必须是轻型远程 -->
    </allEquipmentTags>
    <inCombat>Required</inCombat>  <!-- AND: 必须在战斗中 -->
</li>
```

**效果：** 同时满足"装备轻型远程武器"AND"在战斗中"时激活。

### 示例 5: 排除条件
```xml
<li>
    <key>non-melee-bonus</key>
    <activeHediff>nonMeleeBonus</activeHediff>
    <activeSeverity>1.0</activeSeverity>
    <noEquipmentTags>
        <li>Melee</li>  <!-- NOT: 不是近战武器 -->
    </noEquipmentTags>
</li>
```

**效果：** 不装备任何近战武器时激活（即装备远程武器或无武器）。

---

## 语义别名说明

### "Melee" 别名的匹配规则
当在 XML 中使用 `<li>Melee</li>` 时，会匹配以下所有 `weaponClass`：
- `Melee` - 基础近战
- `MeleePiercer` - 穿刺类
- `MeleeBlunt` - 钝击类
- `Neolithic` - 新石器时代

### "Ranged" 别名的匹配规则
当在 XML 中使用 `<li>Ranged</li>` 时，会匹配以下所有 `weaponClass`：
- `Ranged` - 基础远程
- `RangedLight` - 轻型远程
- `RangedHeavy` - 重型远程
- `LongShots` - 长距离射击
- `ShortShots` - 短距离射击
- `Ultratech` - 超科技武器

---

## 代码实现细节

### MatchesWeaponClass 方法
```csharp
private bool MatchesWeaponClass(ThingDef weaponDef, string className)
{
    // 1. 直接匹配 weaponClasses
    for (int i = 0; i < weaponDef.weaponClasses.Count; i++)
    {
        WeaponClassDef wc = weaponDef.weaponClasses[i];
        if (wc != null && string.Equals(wc.defName, className, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    // 2. 支持 "Melee" 语义别名（匹配所有近战族系）
    if (string.Equals(className, "Melee", StringComparison.OrdinalIgnoreCase))
    {
        // 检查是否在 IsInMeleeFamily() 中
    }

    // 3. 支持 "Ranged" 语义别名（匹配所有远程族系）
    if (string.Equals(className, "Ranged", StringComparison.OrdinalIgnoreCase))
    {
        // 检查是否在 IsInRangedFamily() 中
    }
}
```

### 优先级链（HasEquipmentTag）
```csharp
private bool HasEquipmentTag(Pawn pawn, string tag)
{
    // Priority 1: 检查 weaponClasses（最权威）
    if (MatchesWeaponClass(def, normalizedTag))
        return true;

    // Priority 2: 检查 weaponTags（向后兼容）
    if (def.weaponTags?.Contains(normalizedTag) == true)
        return true;

    // Priority 3: 检查 apparel 标签
    if (apparel[i].def.apparel?.tags?.Contains(normalizedTag) == true)
        return true;
}
```

---

## 迁移指南（如果使用了旧方法）

如果您在 XML 中依赖了旧的 `IsRangedWeapon` 属性逻辑，现在可以直接使用以下替换：

| 旧方法 | 新方法 | 说明 |
|-------|--------|------|
| `<li>Ranged</li>` (via IsRangedWeapon) | `<li>Ranged</li>` (via weaponClasses) | 相同语法，更严谨的实现 |
| `<li>Melee</li>` (via IsMeleeWeapon) | `<li>Melee</li>` (via weaponClasses) | 相同语法，更严谨的实现 |
| 自定义 `weaponTags` | 仍然支持 | 通过 Priority 2 继续工作 |

**无需改动现有 XML** - 旧的 XML 定义仍然可以工作，只是现在使用了更严谨的底层判断机制。

---

## 设计原则

### 为什么使用 weaponClasses？

1. **权威性：** 在 XML 中明确定义，不依赖代码推导
2. **准确性：** 支持精细分类（RangedLight vs RangedHeavy）
3. **可维护性：** 改进武器分类只需改 XML，无需改代码
4. **扩展性：** 新的 WeaponClassDef 自动被支持
5. **一致性：** 与 RimWorld 官方的 WeaponClassDefs.xml 保持同步

### 为什么保留 weaponTags？

1. **向后兼容性：** 支持现有的自定义标签
2. **灵活性：** 允许更细粒度的自定义分类
3. **平滑迁移：** 不破坏现有 mod 配置

---

## 相关文档

- [武器分类方案分析.md](../武器分类方案分析.md) - 详细的分类方案分析
- [AdaptiveHediffFramework.md](AdaptiveHediffFramework.md) - 条件 Hediff 框架文档
- [ProjectStructureConvention.md](ProjectStructureConvention.md) - 项目结构规范
