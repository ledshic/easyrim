# ConditionalHediffDistributor 武器分类改进 - 快速参考

## 改进内容总结

✅ **编译验证:** Build succeeded with 0 Warnings, 0 Errors
📅 **更新时间:** 2026-07-24
📁 **修改文件:** `Source/CompProperties/HediffComp_ConditionalHediffDistributor.cs`

---

## 关键改进

### 从 API 属性 → XML 权威标准
```
旧方法：def.IsRangedWeapon / def.IsMeleeWeapon (API 属性，不够直接)
新方法：def.weaponClasses (XML 定义，权威准确)
```

### 新增方法
- `MatchesWeaponClass()` - 核心判断逻辑，支持精细分类
- `IsInMeleeFamily()` - 识别所有近战族系
- `IsInRangedFamily()` - 识别所有远程族系

### 判断优先级
```
Priority 1: weaponClasses (权威标准，最严谨)
   ↓
Priority 2: weaponTags (灵活标签，向后兼容)
   ↓
Priority 3: apparel tags (服装标签)
```

---

## XML 使用示例

### 远程武器条件
```xml
<anyEquipmentTags>
    <li>Ranged</li>  <!-- 匹配所有远程武器 -->
</anyEquipmentTags>
```

### 精细分类（轻型远程）
```xml
<anyEquipmentTags>
    <li>RangedLight</li>  <!-- 仅轻型远程 -->
</anyEquipmentTags>
```

### 近战武器条件
```xml
<anyEquipmentTags>
    <li>Melee</li>  <!-- 匹配所有近战武器 -->
</anyEquipmentTags>
```

---

## 支持的武器分类

| 分类 | 对应 weaponClass | 包含的具体类型 |
|------|-----------------|----------------|
| **Melee** (别名) | - | MeleePiercer, MeleeBlunt, Neolithic |
| **Ranged** (别名) | - | RangedLight, RangedHeavy, LongShots, ShortShots, Ultratech |
| RangedLight | RangedLight | 手枪、轻步枪 |
| RangedHeavy | RangedHeavy | 重狙、火箭筒 |
| MeleePiercer | MeleePiercer | 剑、矛 |
| MeleeBlunt | MeleeBlunt | 棒槌、锤子 |

---

## 完全向后兼容

✅ 现有 XML 配置无需改动
✅ 旧的 weaponTags 标签仍然生效
✅ "Melee" 和 "Ranged" 别名继续工作
✅ 仅改进底层判断逻辑，表面行为不变

---

## 相关文档

- 📖 详细分析: [武器分类方案分析.md](../武器分类方案分析.md)
- 📖 使用指南: [Docs/ConditionalHediffWeaponClassification.md](../Docs/ConditionalHediffWeaponClassification.md)
- 📖 框架文档: [Docs/AdaptiveHediffFramework.md](../Docs/AdaptiveHediffFramework.md)

---

## 代码改动统计

```
Files modified: 1
  - Source/CompProperties/HediffComp_ConditionalHediffDistributor.cs

Methods refactored: 1
  - HasEquipmentTag() - 从依赖 API 属性改为使用 weaponClasses

New methods: 3
  - MatchesWeaponClass(ThingDef, string) - 核心判断逻辑
  - IsInMeleeFamily(string) - 近战族系识别
  - IsInRangedFamily(string) - 远程族系识别

Compilation: ✅ Success (0 warnings, 0 errors)
```

---

## 使用场景示例

### 场景1: 远程武器加成
```xml
<!-- 装备远程武器时激活额外伤害加成 -->
<li>
    <key>ranged-bonus</key>
    <activeHediff>RangedDamageBonus</activeHediff>
    <activeSeverity>1.0</activeSeverity>
    <anyEquipmentTags>
        <li>Ranged</li>
    </anyEquipmentTags>
</li>
```

### 场景2: 轻型远程专家
```xml
<!-- 仅装备轻型远程武器时激活（精细分类） -->
<li>
    <key>light-ranged-specialist</key>
    <activeHediff>LightRangedMastery</activeHediff>
    <activeSeverity>1.0</activeSeverity>
    <anyEquipmentTags>
        <li>RangedLight</li>
    </anyEquipmentTags>
</li>
```

### 场景3: 近战战士
```xml
<!-- 装备近战武器时激活 -->
<li>
    <key>melee-warrior</key>
    <activeHediff>MeleeMastery</activeHediff>
    <activeSeverity>1.0</activeSeverity>
    <anyEquipmentTags>
        <li>Melee</li>
    </anyEquipmentTags>
</li>
```

---

**下一步:** 查看 [Docs/ConditionalHediffWeaponClassification.md](../Docs/ConditionalHediffWeaponClassification.md) 获取详细的实现和使用指南。
