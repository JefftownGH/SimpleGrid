# SimpleGrid 开发总览

## 1. 项目状态

截至 2026-09-10，`source/` 是当前维护版的唯一实现来源。当前源码已在维护环境完成 Release 构建和部署验证；公开仓库尚未建立正式 Release、安装器或 CI，因此不能把本地部署状态等同于公开发行状态。

公开品牌和仓库名为 `SimpleGrid`，中文名为“简单格子”。程序集、进程、命名空间、项目文件、安装目录和用户数据目录现已统一为 `SimpleGrid`；`NoFences` 仅在上游署名、来源说明和旧版迁移文档中出现。

本项目并非直接使用上游发行版。当前源码由既有定制版程序恢复并继续维护，包含高清图标、高 DPI、Windows 11 桌面集成、诊断日志、拖拽排序、快速菜单和自有项目仓库等扩展。上游项目用于来源和行为对照，不应覆盖 `source/`。

## 2. 技术栈与构建

- UI：Windows Forms
- 目标框架：.NET Framework 4.8
- 平台：x64
- 项目格式：SDK-style Windows Desktop project
- 资源兼容包：`System.Resources.Extensions 10.0.0`

构建命令：

```powershell
dotnet restore .\source\SimpleGrid.csproj
dotnet build .\source\SimpleGrid.csproj -c Release --no-restore
```

主要输出：

```text
source\bin\Release\net48\SimpleGrid.exe
source\bin\Release\net48\SimpleGrid.exe.config
source\bin\Release\net48\System.Resources.Extensions.dll
```

输出目录中的其他 `System.*.dll` 和 `Microsoft.Bcl.HashCode.dll` 是资源兼容包的运行依赖。部署时应与 EXE 一并复制，不要只替换单个文件后删除既有依赖。

编译目前有少量来自反编译结构体字段的 `CS0649` 警告，不影响生成；当前构建结果为 0 个错误。

## 3. 代码结构

| 模块 | 职责 |
| --- | --- |
| `SimpleGrid/Program.cs` | 单实例互斥体、应用启动、全局异常日志和消息循环。 |
| `SimpleGrid/FenceWindow.cs` | 围栏窗口、绘制、命中测试、滚动、拖放、菜单和 DPI 处理。 |
| `SimpleGrid.Model/FenceManager.cs` | 围栏创建、删除、加载及 XML 持久化。 |
| `SimpleGrid.Model/FenceInfo.cs` | 围栏位置、大小、锁定、折叠和文件顺序的数据模型。 |
| `SimpleGrid.Model/FenceEntry.cs` | 文件类型判断、程序与快捷方式启动。 |
| `SimpleGrid.Win32/HiResIcons.cs` | Shell 高清图标读取、缩放及异步缓存。 |
| `SimpleGrid.Util/ThumbnailProvider.cs` | 图片缩略图后台生成。 |
| `SimpleGrid.Win32/*` | 桌面贴合、模糊、阴影、窗口层级和 Win32 互操作。 |
| `Peter/*` | Windows Shell 右键菜单实现。 |

详细的上游差异见 `docs/UPSTREAM-DIFFERENCES.md`。

## 4. 用户数据

用户数据根目录：

```text
%LOCALAPPDATA%\SimpleGrid
```

每个围栏对应一个 GUID 子目录，并包含 `__fence_metadata.xml`。核心字段如下：

- `Id`：围栏唯一标识。
- `Name`：围栏标题。
- `PosX`、`PosY`：逻辑坐标。
- `Width`、`Height`：逻辑尺寸。
- `Locked`：是否锁定编辑和拖放。
- `CanMinify`：是否允许折叠。
- `TitleHeight`：标题栏高度。
- `Files`：图标路径列表，列表顺序就是格子显示顺序。

根目录的 `fences.index` 每行保存一个围栏 GUID。程序优先按索引直接加载元数据，创建、保存和删除围栏时同步维护索引；没有索引的旧数据仍可通过目录扫描加载。首次启动且 SimpleGrid 目录没有围栏时，程序会从 `%LOCALAPPDATA%\NoFences` 复制旧数据并改写 `Items` 绝对路径，不删除旧目录。

外部项目拖入后由 `FenceManager.ImportItem()` 复制到：

```text
%LOCALAPPDATA%\SimpleGrid\Items\<随机标识>\<原始名称>
```

每个导入项目使用独立容器，避免同名冲突。已经位于 `Items` 内的路径会直接复用；复制失败时不会加入围栏，并尝试清理未完成容器。运行日志为 `%LOCALAPPDATA%\SimpleGrid\app_diag.log`。源码归档、重新构建和程序部署都不应覆盖用户数据目录。

## 5. 快捷方式与响应性修复

此前从围栏启动网易云音乐时，Windows 事件日志记录到 `explorer.exe` 的 `Application Hang`。旧路径通过 Shell 同步打开 `.lnk`，同时界面线程可能触发 Shell 图标读取和高频刷新，容易形成阻塞。

当前实现：

1. `FenceEntry.Open()` 在独立任务中执行启动。
2. `.lnk` 先通过 `WScript.Shell` 解析目标、参数和工作目录。
3. 指向现存 `.exe` 的快捷方式使用 `UseShellExecute=false` 直接启动。
4. 无法解析、非 EXE 或特殊协议仍回退到 Windows Shell。
5. 高清图标缓存未命中时立即返回占位图，真实 Shell 图标由最多两个后台任务加载。
6. 后台图标完成后通过 `BeginInvoke` 请求重绘，鼠标移动重绘约以 32 ms 为最小间隔。

正式版本曾用网易云音乐快捷方式走同一启动代码验证：调用快速返回，SimpleGrid 与 Explorer 保持响应，且没有新增 Explorer 挂起事件。

## 6. 图标拖拽排序

当前版本支持所有围栏项目，而不仅限于图片文件：

- 左键按住图标，移动超过系统拖拽阈值后开始拖动。
- 拖到同一围栏中的目标格子，可调整顺序。
- 拖到另一个未锁定围栏，可从源围栏移到目标围栏。
- 蓝色竖线显示即将插入的位置。
- 拖到空白格或末尾可追加到对应位置。
- 外部文件拖入围栏时，会先复制到中央 `Items` 仓库，再按鼠标位置插入。
- 围栏内部和跨围栏移动只修改 `Files` 路径列表，不再次移动或删除真实文件。
- 源围栏或目标围栏被锁定时拒绝内部移动。

拖动命中、绘制换行和插入线现在共用同一列数算法。列数严格按“首项起点 + 单元宽度 + 间距”计算，避免 125%—200% DPI 缩放时因逐项取整而出现绘制为四列、命中却按三列的偏差。窗口还会保存本帧实际可见项目及其原始 `Files` 索引；失效路径被跳过时，点击和拖动仍对应屏幕上的真实图标，插入后再映射回持久化列表位置。

移动完成后，同围栏只保存一次；跨围栏分别保存源和目标，然后刷新两个窗口。跨围栏目标已包含相同路径时会去重。

排序与布局核心已覆盖以下程序化验证：

1. 同围栏从首位移动到末位。
2. 同围栏从中间移动到首位。
3. 跨围栏插入中间位置。
4. 跨围栏目标已有相同路径时去重并移动。
5. 100%、125%、150%、175%、200% 缩放下，对 40—800 像素窗口宽度逐一比较绘制换行和命中列数，结果无不一致。
6. 列表中间存在失效路径时，可见位置与持久化插入索引双向映射正确。

桌面围栏窗口未被当前自动化界面接口枚举，因此最终的手动验收应再检查一次真实鼠标拖动、蓝色插入线及重启后的顺序持久化。

## 7. 部署与回滚

正式安装目录：

```text
%LOCALAPPDATA%\Programs\SimpleGrid
```

部署前应确认正在运行的 `SimpleGrid.exe` 确实来自该目录，备份现有程序和 `%LOCALAPPDATA%\SimpleGrid` 数据，终止进程并等待文件锁释放，再复制完整构建输出并重新启动。旧版升级时，保留 `%LOCALAPPDATA%\Programs\NoFences` 和 `%LOCALAPPDATA%\NoFences` 作为回滚副本；首次启动迁移会复制旧数据、生成 `fences.index` 并把 XML 中的 `NoFences\Items` 绝对路径改写到 `SimpleGrid\Items`。

本机部署形成的 `backup-*` 目录属于维护现场，不进入公开仓库。回滚时应同时恢复对应版本的程序文件；涉及用户数据迁移时，还要恢复同一批次的数据备份，避免代码与 XML 路径语义不一致。

## 8. 响应性与可靠性

本轮检查已完成：

1. 绘制过程创建的 `Brush`、`Pen`、字体族和临时缩略图均明确释放；窗口关闭时释放字体、缩略图缓存和节流器。
2. 图片缩略图缓存增加锁和大小写不敏感键；所有窗口共享两个后台加载名额，替换及销毁图标时释放旧资源。
3. 缩略图改为共享读取文件流，不再用 `ReadAllBytes` 为整张图片额外复制一份内存。
4. Shell 高清图标占位图被真实图标替换时释放旧位图，缓存路径按 Windows 的大小写不敏感规则处理。
5. 窗口移动和缩放保存改为 UI 定时器节流，取消会跨线程修改窗口状态的 `async void` 循环；缩放时只请求异步重绘。
6. `FenceEntry.Open()` 改用线程池任务，不再为每次启动强制建立独立长生命周期线程。
7. 围栏元数据先写同目录临时文件，再原子替换正式 XML；单个目录数据损坏时记录错误并继续加载其他围栏。
8. XML 反序列化完成后先关闭读句柄，再构造窗口；窗口初始化阶段禁止位置/尺寸事件保存，避免启动时覆盖既有布局。

仍可继续优化但不影响本次拖拽正确性：

1. 绘制时仍会通过 `File.Exists`、`Directory.Exists` 检查路径；慢盘、移动盘或失联盘符可能影响一帧的响应时间。
2. 图标与缩略图缓存尚无 LRU 上限，也未根据文件修改时间统一失效。
3. 静态图标完成事件会让多个围栏都收到刷新通知，可改为带路径的定向、合并刷新。
4. 图片解码仍由 GDI+ 打开原图后生成缩略图，可进一步使用按目标尺寸解码的实现。

推荐实施顺序：先修复 GDI 生命周期和绘制时磁盘访问，再增加有限缓存及定向刷新。不要仅为了降低任务管理器数字迁移到 WPF、WinUI 或自包含新版 .NET；这会扩大风险，并可能增加基础内存。

## 9. 验证清单

每次修改后至少执行：

1. Release 构建为 0 个错误。
2. 启动正式 EXE，确认五个围栏全部出现且进程可响应。
3. 双击普通 EXE 快捷方式，确认目标启动且 Explorer 不被拖挂。
4. 在同一围栏拖动首、中、末位置并重启检查顺序。
5. 跨两个未锁定围栏移动图标，确认源删除引用、目标插入且真实文件未移动。
6. 从外部拖入一个测试文件，确认围栏引用指向 `Items`，副本内容一致且外部原件仍存在。
7. 右键项目检查快速删除和资源管理器定位；用 `Shift + 右键` 检查完整 Shell 菜单。
8. 检查 `%LOCALAPPDATA%\SimpleGrid\app_diag.log` 没有新的未处理异常。
9. 高 DPI 机器上确认按住哪个图标就拖动哪个图标，尤其检查每行最后一列。

## 10. 来源与维护原则

- 上游项目：`https://github.com/Twometer/NoFences`
- 上游许可：MIT，原始版权声明保存在仓库根目录 `LICENSE`。
- `source/` 是维护版实现来源；上游链接和差异说明用于追溯来源。
- 不将 `%LOCALAPPDATA%\SimpleGrid` 用户布局、个人文件路径或运行日志提交到共享仓库。
- 旧版迁移来源目录为 `%LOCALAPPDATA%\NoFences`；只用于兼容说明，不再作为当前程序的数据根目录。
- 修改数据模型前要确保旧 XML 可以继续反序列化；修改部署依赖时要同步更新本文。
