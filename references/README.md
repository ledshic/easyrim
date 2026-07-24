# RimWorld 参考资料

此目录用于集中存放开发 EasyRim 时使用的参考资料（原版资料与第三方模组源码）。仓库只追踪本说明文件；目录内生成或拉取的内容均由根目录 `.gitignore` 排除，不进入 Git。

本地目录结构：

```text
references/
├── README.md
├── RimWorld-Data/        # 原版游戏数据目录快照，用于本地比对
├── RimWorld-XMLs/        # 原版 XML 资料快照，用于本地检索
├── CustomizeWeapon/      # 第三方模组源码参考（realloon/CustomizeWeapon）
├── chinese-simplified/  # 原版及 DLC 的简体中文语言包
└── decompiled/          # Assembly-CSharp.dll 的反编译结果
```

当前资料来自 RimWorld `1.6.4871 rev595` 的 macOS Steam 安装：

- `chinese-simplified/<package>/`：对应 `Data/<package>/Languages/ChineseSimplified (简体中文).tar`，其中 `<package>` 包括 `Core` 以及本机已安装的 DLC。
- `decompiled/`：由 `Contents/Resources/Data/Managed/Assembly-CSharp.dll` 反编译得到。
- `decompiled/_selected/HediffDef.cs`：迁移自仓库原有的 `_decompile/HediffDef.cs`，仅作为历史选取参考。

这些内容属于 RimWorld 原版游戏资料，仅用于本地检索和兼容性研究。更新游戏后应重新生成，不能把目录内部文件加入提交。

第三方参考资料：

- `CustomizeWeapon/`：来自 `https://github.com/realloon/CustomizeWeapon` 的本地克隆副本，用于实现对照与兼容性参考。
- 建议更新方式：在仓库根目录执行 `gh repo clone realloon/CustomizeWeapon` 后移动到 `references/CustomizeWeapon`，或在已有目录内执行 `git pull`。
- 同样不应将该目录内容加入提交（保留本地参考用途）。

可使用以下方式刷新语言文件：

```sh
RIMWORLD_APP="/path/to/RimWorldMac.app"
for archive in "$RIMWORLD_APP"/Data/*/Languages/'ChineseSimplified (简体中文).tar'; do
  package=$(basename "$(dirname "$(dirname "$archive")")")
  mkdir -p "references/chinese-simplified/$package"
  tar -xf "$archive" -C "references/chinese-simplified/$package"
done
```

反编译目录应使用 ILSpy 的项目输出模式生成：

```sh
ilspycmd -p -o references/decompiled \
  "$RIMWORLD_APP/Contents/Resources/Data/Managed/Assembly-CSharp.dll"
```
