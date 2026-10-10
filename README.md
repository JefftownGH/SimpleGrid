# SimpleGrid（简单格子）

[中文](#中文) | [English](#english)

<a id="中文"></a>

## 中文

一个轻量、开源的 Windows 桌面围栏工具，基于 [Twometer/NoFences](https://github.com/Twometer/NoFences) 二次开发。

SimpleGrid（简单格子）用半透明格子整理桌面上的程序快捷方式、文件和文件夹。它在 NoFences 的轻量基础上，重点改善了高 DPI、图标加载、快捷方式启动、拖拽排序、数据可靠性和长期维护体验。

> 本项目是社区维护的非官方版本，与上游作者没有隶属或背书关系。项目沿用 MIT 许可证，并保留上游版权声明。

维护者：**DD**

## 写在前面

我没有任何代码基础，只是一个从零开始的普通人。SimpleGrid 的全部开发工作，都是我借助 Codex 一步一步完成的。感谢这个时代，让一个不懂编程的人也能把心中的想法变成真实可用的程序；感谢所有无私分享成果的开源项目，也感谢每一位用代码为后来者照亮道路的开源程序员。谨以这个小小的项目，向伟大的开源精神致敬。

## 为什么叫“简单格子”

这个名字表达了项目最直接的目标：用简单、轻量、没有多余负担的格子整理 Windows 桌面。英文仓库名使用 `SimpleGrid`，方便搜索、输入和分享。

项目现已完成全量品牌统一：可执行文件、进程、程序集、代码命名空间、项目文件、默认安装目录和用户数据目录均使用 `SimpleGrid`。首次启动且新目录还没有围栏时，程序会从 `%LOCALAPPDATA%\NoFences` 复制旧数据，并将 XML 中指向旧 `NoFences\Items` 的绝对路径改为新目录；旧目录不会被删除，可用于回滚。

## 为什么做这个版本

上游 NoFences 提供了简洁可用的桌面围栏基础，但长期使用时仍有一些明显限制：图标不能自由排序，跨围栏移动不方便，高 DPI 下可能点错或拖错项目，Shell 图标和快捷方式可能阻塞界面，配置保存缺少故障保护，而且围栏内容长期依赖外部整理目录。

本版本在保留轻量 Windows Forms 架构的前提下，针对这些日常问题做了持续修复，并把恢复出的可维护源码、技术说明和上游快照一起整理为可公开协作的项目。

## 与原版相比有什么不同

| 能力 | 上游版本 | SimpleGrid |
| --- | --- | --- |
| 围栏内图标排序 | 不支持自由插入排序 | 支持拖拽排序和插入位置提示 |
| 跨围栏移动 | 依赖外部拖放 | 支持直接拖到另一个未锁定围栏 |
| 高 DPI 命中 | 绘制和命中可能使用不同列数 | 100%—200% DPI 共用同一布局算法 |
| 失效路径 | 可能导致可见位置与数据索引错位 | 使用可见项目到模型索引的明确映射 |
| 快捷方式启动 | 主要依赖 Windows Shell | 后台解析 `.lnk`，可直接启动目标 EXE |
| 图标和缩略图 | 可能在界面线程阻塞 | 后台加载、共享限流、线程安全缓存 |
| 右键删除 | 依赖 Shell 菜单，目标可能丢失 | 快速菜单、锁定目标、清理重复记录 |
| 文件定位 | 无专用入口 | 可在资源管理器中打开并选中项目 |
| 外部项目存储 | 长期引用原始外部路径 | 拖入时复制到 SimpleGrid 自有 `Items` 仓库 |
| 配置保存 | 直接写入风险较高 | 同目录临时文件写入后原子替换 |
| 异常隔离 | 单个损坏配置可能影响启动 | 单围栏失败被记录，其余围栏继续加载 |
| 资源管理 | 存在 GDI/缓存释放风险 | 明确释放图标、位图、字体和缩略图资源 |

完整实现差异与证据见 [docs/UPSTREAM-DIFFERENCES.md](docs/UPSTREAM-DIFFERENCES.md)。

## 主要功能

- 创建、删除、重命名、移动和缩放桌面围栏。
- 锁定围栏，避免误拖动或误修改内容。
- 自动折叠围栏，只保留标题栏。
- 显示程序图标、文件夹图标和图片缩略图。
- 围栏内部拖拽排序，蓝色插入线显示目标位置。
- 在未锁定围栏之间直接移动项目。
- 从资源管理器或桌面拖入文件、文件夹和快捷方式。
- 外部项目自动复制到 `%LOCALAPPDATA%\SimpleGrid\Items`，不再依赖原始整理目录。
- 快速右键删除围栏记录；`Shift + 右键` 可打开完整 Windows Shell 菜单。
- 右键选择“在资源管理器中打开”，定位并选中对应项目。
- 布局、尺寸、锁定状态和项目顺序自动持久化。
- 单实例运行，Windows Explorer 重建后重新恢复桌面层级。

## 系统要求

- Windows 10 或 Windows 11（x64）。
- 运行预编译版本时需要 .NET Framework 4.8。
- 从源码构建时需要 .NET SDK 和 .NET Framework 4.8 Developer Pack。

预编译版本可从 [GitHub Releases](https://github.com/JefftownGH/SimpleGrid/releases) 下载；当前稳定版本为 [v1.0.0](https://github.com/JefftownGH/SimpleGrid/releases/tag/v1.0.0)。也可以按下方步骤从源码构建。

## 从源码构建

```powershell
dotnet restore .\source\SimpleGrid.csproj
dotnet build .\source\SimpleGrid.csproj -c Release --no-restore
```

构建结果位于：

```text
source\bin\Release\net48\
```

运行时应保留输出目录中的依赖 DLL，不要只复制 `SimpleGrid.exe`。更详细的构建、部署和验证说明见 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)。

## 基本使用

1. 启动 `SimpleGrid.exe`。第一次运行且没有现有配置时，程序会创建一个默认围栏。
2. 右键围栏空白区域，可以新建围栏、重命名、锁定、折叠或删除围栏。
3. 将文件、文件夹或快捷方式拖入围栏。程序会创建自有副本并保存到 `Items` 仓库。
4. 按住图标拖动，可在当前围栏中排序，也可移动到另一个未锁定围栏。
5. 右键具体图标可快速删除围栏记录，或在资源管理器中定位它。
6. 如需 Windows 原生完整右键菜单，对图标使用 `Shift + 右键`。

“Delete item”删除的是围栏中的项目记录，不会主动删除 `Items` 仓库里的物理文件。这种设计优先保证数据可恢复；长期未引用项目的自动清理尚未实现。

## 数据存放位置

SimpleGrid 的用户数据不保存在 Git 仓库或安装目录，而位于：

```text
%LOCALAPPDATA%\SimpleGrid
```

其中：

- 每个 GUID 子目录代表一个围栏，配置文件为 `__fence_metadata.xml`。
- `fences.index` 保存围栏 GUID 列表，确保受限登录任务也能稳定加载全部围栏。
- `Items\<随机标识>\` 保存拖入项目的自有副本。
- `app_diag.log` 保存启动、关闭和异常诊断信息。

卸载、升级或自行部署时不要直接覆盖该目录。备份整个目录即可同时保存围栏布局和自有项目。

## 项目结构

| 目录 | 用途 |
| --- | --- |
| `source/` | 当前维护版源码，是实现事实来源 |
| `docs/` | 架构、差异、构建、部署和变更记录 |

本地恢复工具、构建产物、运行日志、用户布局和个人文件路径均已通过 `.gitignore` 排除，不会提交到公开仓库。

## 已知限制

- 目前只面向 Windows x64 和 .NET Framework 4.8。
- 绘制时仍会检查项目路径；失联磁盘或很慢的网络路径可能影响单帧响应。
- 图标和缩略图缓存尚未设置 LRU 容量上限。
- 删除围栏记录后，`Items` 中的无引用副本不会自动清理。
- 项目尚未建立自动化 CI、安装器和签名发布流程。
- 桌面层窗口不容易被通用 UI 自动化工具枚举，拖拽手感仍需要真实桌面人工验收。

## 开发与贡献

欢迎提交 Issue 或 Pull Request。报告问题时建议提供：

- Windows 版本与显示缩放比例。
- 可以复现问题的最短步骤。
- `%LOCALAPPDATA%\SimpleGrid\app_diag.log` 中相关时间段的日志；上传前请检查并删除个人路径等隐私信息。
- 如果涉及布局问题，可提供打码后的截图。

架构、数据格式、关键实现和验证清单见 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)，版本历史见 [docs/CHANGELOG.md](docs/CHANGELOG.md)。

## 许可证与署名

本项目使用 [MIT License](LICENSE)。原始项目版权归 Twometer 所有；2026 年以来的增强修改由 DD 维护，并在 MIT 许可证允许的范围内发布。分发本项目或其重要部分时，请保留许可证和原始版权声明。

<a id="english"></a>

## English

SimpleGrid is a lightweight, open-source desktop organizer for Windows, developed from [Twometer/NoFences](https://github.com/Twometer/NoFences).

It organizes application shortcuts, files, and folders into translucent desktop grids. Building on the lightweight foundation of NoFences, SimpleGrid focuses on better high-DPI behavior, icon loading, shortcut launching, drag-and-drop ordering, data reliability, and long-term maintainability.

> This is an unofficial, community-maintained project. It is not affiliated with or endorsed by the upstream author. The project remains under the MIT License and retains the upstream copyright notice.

Maintainer: **DD**

### A personal note

I started with no coding background—just an ordinary person beginning from zero. Every part of SimpleGrid was built step by step with the help of Codex. I am grateful to live in an era when someone who does not know programming can still turn an idea into a real, usable application. I am equally grateful to every open-source project that shares its work freely, and to every programmer whose code lights the way for those who follow. This small project is my tribute to the spirit of open source.

### Why “SimpleGrid”?

The name states the goal plainly: organize the Windows desktop with grids that are simple, lightweight, and free of unnecessary overhead. The repository uses the English name `SimpleGrid` to make it easy to search, type, and share worldwide.

The project has completed a full branding migration. The executable, process, assembly, code namespace, project file, default installation directory, and user-data directory all use `SimpleGrid`. On first launch, if the new directory has no fences, the application copies legacy data from `%LOCALAPPDATA%\NoFences` and rewrites absolute XML paths that point to the old `NoFences\Items` directory. The old directory is kept for rollback.

### Why this version exists

The upstream NoFences project provides a clean and practical foundation for desktop fences, but extended use reveals several limitations: icons cannot be freely reordered, moving items between fences is awkward, high-DPI scaling can cause incorrect clicks or drops, Shell icons and shortcuts can block the interface, configuration saving lacks failure protection, and fence contents remain dependent on external source folders.

SimpleGrid keeps the lightweight Windows Forms architecture while addressing these everyday issues. It also packages the recovered, maintainable source code, technical documentation, and upstream snapshot into a project ready for public collaboration.

### Differences from upstream

| Capability | Upstream | SimpleGrid |
| --- | --- | --- |
| Icon ordering within a fence | No free insertion ordering | Drag-to-reorder with insertion-position feedback |
| Moving between fences | Relies on external drag and drop | Drag directly into another unlocked fence |
| High-DPI hit testing | Drawing and hit testing may use different column counts | One shared layout algorithm from 100% to 200% DPI |
| Missing paths | Visible positions and data indices can become misaligned | Explicit mapping from visible items to model indices |
| Shortcut launching | Primarily relies on Windows Shell | Resolves `.lnk` files in the background and can launch target EXEs directly |
| Icons and thumbnails | May block the UI thread | Background loading, shared throttling, and a thread-safe cache |
| Right-click removal | Relies on the Shell menu and may lose the target | Fast menu, stable target capture, and duplicate-record cleanup |
| File location | No dedicated action | Opens File Explorer with the item selected |
| External item storage | Keeps long-term references to original paths | Copies dropped items into SimpleGrid's own `Items` repository |
| Configuration saving | Direct writes have higher corruption risk | Writes a temporary file in the same directory, then replaces atomically |
| Fault isolation | One damaged configuration may prevent startup | Logs a failed fence and continues loading the others |
| Resource management | Potential GDI/cache disposal issues | Explicitly disposes icons, bitmaps, fonts, and thumbnail resources |

See [docs/UPSTREAM-DIFFERENCES.md](docs/UPSTREAM-DIFFERENCES.md) for the complete implementation comparison and supporting evidence.

### Features

- Create, delete, rename, move, and resize desktop fences.
- Lock fences to prevent accidental movement or content changes.
- Auto-collapse fences so that only the title bar remains visible.
- Display application icons, folder icons, and image thumbnails.
- Reorder items within a fence, with a blue insertion line showing the destination.
- Move items directly between unlocked fences.
- Drag files, folders, and shortcuts from File Explorer or the desktop.
- Automatically copy external items to `%LOCALAPPDATA%\SimpleGrid\Items`, removing the dependency on the original organization folder.
- Quickly remove fence records from the context menu; use `Shift + right-click` for the complete Windows Shell menu.
- Choose “Open in File Explorer” to locate and select an item.
- Automatically persist layout, size, lock state, and item order.
- Run as a single instance and restore the desktop layer after Windows Explorer restarts.

### System requirements

- Windows 10 or Windows 11 (x64).
- .NET Framework 4.8 for the prebuilt version.
- .NET SDK and the .NET Framework 4.8 Developer Pack when building from source.

Download the prebuilt application from [GitHub Releases](https://github.com/JefftownGH/SimpleGrid/releases). The current stable release is [v1.0.0](https://github.com/JefftownGH/SimpleGrid/releases/tag/v1.0.0). You can also build it from source as described below.

### Build from source

```powershell
dotnet restore .\source\SimpleGrid.csproj
dotnet build .\source\SimpleGrid.csproj -c Release --no-restore
```

Build output:

```text
source\bin\Release\net48\
```

Keep the dependency DLLs in the output directory with `SimpleGrid.exe`; do not copy the executable alone. See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for detailed build, deployment, and verification instructions.

### Basic usage

1. Launch `SimpleGrid.exe`. On the first run, if no configuration exists, the application creates a default fence.
2. Right-click an empty area of a fence to create, rename, lock, collapse, or delete a fence.
3. Drag a file, folder, or shortcut into a fence. SimpleGrid creates and stores its own copy in the `Items` repository.
4. Drag an icon to reorder it within the current fence or move it to another unlocked fence.
5. Right-click an icon to remove its fence record quickly or locate it in File Explorer.
6. Use `Shift + right-click` on an icon to open the complete native Windows context menu.

“Delete item” removes the item record from the fence; it does not delete the physical file stored in the `Items` repository. This favors recoverability. Automatic cleanup of unreferenced items has not yet been implemented.

### Data location

SimpleGrid stores user data outside the Git repository and installation directory:

```text
%LOCALAPPDATA%\SimpleGrid
```

Inside this directory:

- Each GUID-named subdirectory represents a fence; its configuration file is `__fence_metadata.xml`.
- `fences.index` stores the list of fence GUIDs so all fences can load reliably even in restricted sign-in tasks.
- `Items\<random-id>\` stores SimpleGrid-owned copies of dropped items.
- `app_diag.log` records startup, shutdown, and exception diagnostics.

Do not overwrite this directory when uninstalling, upgrading, or deploying manually. Backing up the whole directory preserves both the fence layout and stored items.

### Project structure

| Directory | Purpose |
| --- | --- |
| `source/` | Maintained source code and the source of truth for implementation |
| `docs/` | Architecture, upstream differences, build and deployment guidance, and change history |

Local recovery tools, build output, runtime logs, user layouts, and personal file paths are excluded from the public repository through `.gitignore`.

### Known limitations

- Windows x64 and .NET Framework 4.8 only.
- Item paths are still checked during drawing; disconnected drives or slow network paths may affect an individual frame.
- The icon and thumbnail cache does not yet have an LRU size limit.
- Removing a fence record does not automatically clean up unreferenced copies from `Items`.
- Automated CI, an installer, and a signed release pipeline have not yet been established.
- Desktop-layer windows are difficult for general UI automation tools to enumerate, so drag-and-drop feel still requires manual testing on a real desktop.

### Development and contributions

Issues and pull requests are welcome. When reporting a problem, please include:

- Your Windows version and display scaling percentage.
- The shortest steps that reproduce the issue.
- Relevant lines from `%LOCALAPPDATA%\SimpleGrid\app_diag.log`; remove personal paths and other private information before uploading.
- A redacted screenshot if the issue involves layout.

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for the architecture, data format, key implementation details, and verification checklist. See [docs/CHANGELOG.md](docs/CHANGELOG.md) for version history.

### License and attribution

SimpleGrid is distributed under the [MIT License](LICENSE). The original project is copyright Twometer. Enhancements made since 2026 are maintained by DD and distributed under the terms of the MIT License. When redistributing this project or a substantial part of it, retain the license and original copyright notice.
