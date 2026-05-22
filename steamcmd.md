# SteamCMD 发布说明

这份说明对应当前仓库的发布目录和工作流。

## 当前发布目录

GitHub Actions 在打包时会把这些目录放进发布包的根目录 `easyrim/` 下：

- `About`
- `Assemblies`
- `Defs`
- `Languages`
- `Patches`
- 可选：`Textures`
- 可选：`Sounds`

最终用于上传的内容目录应指向模组根目录，也就是包含 `About/About.xml` 的那一层。

## SteamCMD 使用方式

仓库根目录的 [easyrim.vdf](easyrim.vdf) 已经改成相对路径，可以直接在仓库根目录或解压后的发布目录中使用。

典型流程如下：

1. 确认 `About/About.xml` 里已经有正确的 `steamWorkshopId`。
2. 确认 `About/Preview.png` 存在并可用。
3. 在模组根目录执行 SteamCMD 上传，`workshop_build_item` 会读取 [easyrim.vdf](easyrim.vdf)。

如果你要上传的是 GitHub Actions 生成的发布包，只要进入解压后的 `easyrim/` 目录再执行同样的流程即可。

## 当前工作流

仓库里有两个和发布相关的工作流：

- [.github/workflows/build.yml](.github/workflows/build.yml) 用于构建并产出模组目录 artifact。
- [.github/workflows/master-release.yml](.github/workflows/master-release.yml) 用于 master 分支自动发布最新构建。
- [.github/workflows/release.yml](.github/workflows/release.yml) 用于 tag 或手动触发后直接上传 Steam Workshop。

如果你想手动用 SteamCMD，优先使用仓库根目录这份 `easyrim.vdf`；如果你想从别的目录上传，只要保证 `contentfolder` 和 `previewfile` 仍然指向正确位置即可。