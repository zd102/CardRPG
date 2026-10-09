# Demo 编译、运行与导出

本指南适用于当前「灰烬回廊」微型迷宫 Demo，已在 Windows x64 上验证。完成环境配置后，双击仓库根目录的 `Play-Demo.cmd` 即可编译并游玩；生成可分发游戏包使用 `scripts/export.ps1`。

## 1. 准备开发环境

| 组件 | 本项目版本与要求 | 用途 |
| --- | --- | --- |
| Godot .NET x64 | **4.7.2 stable .NET**，完整解压并保留旁边的 `GodotSharp` 目录 | 编辑场景、运行 C# 游戏与导出 |
| .NET SDK x64 | **10.0.401**；`global.json` 允许同一功能版本带的后续补丁，禁用预览版 | 编译 `net10.0` 项目；仅安装 Runtime 不够 |
| Godot .NET 导出模板 | **4.7.2 stable .NET**，与引擎完全匹配 | Windows 游戏包导出；源码运行时不需要 |
| Git | 可执行 `git clone` | 获取源码 |
| VS Code + 微软 C# 扩展 | 可选；扩展 ID 为 `ms-dotnettools.csharp` | 编写代码与 F5 调试 |

安装来源与本机记录见[性能平台与发布构建](05_性能平台与发布构建.md)。Godot 要选择带 **.NET / C#** 支持的版本。首次编译需联网还原 NuGet 包；依赖版本已写入各项目文件。

在 Godot 的“编辑器 → 管理导出模板”中安装同版本 .NET 模板。本机模板目录为 `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\`，其中包括 `windows_release_x86_64.exe`。模板只用于导出，不需要复制进仓库。

## 2. 获取源码并配置引擎路径

首次获取当前 Demo 分支：

```powershell
git clone --branch docs/game-design https://github.com/zd102/CardRPG.git
Set-Location CardRPG
```

已有仓库时进入其根目录即可。下文命令均从包含 `CardRPG.sln`、`game/` 和 `scripts/` 的仓库根目录执行。

将以下路径替换为本机实际解压位置，指向 Godot 的 `.exe` 文件：

```powershell
$env:GODOT4 = 'C:\Tools\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe'
if (-not (Test-Path -LiteralPath $env:GODOT4 -PathType Leaf)) {
    throw '请将 GODOT4 改为本机 Godot .NET 可执行文件的路径。'
}
[Environment]::SetEnvironmentVariable('GODOT4', $env:GODOT4, 'User')

dotnet --version
& $env:GODOT4 --version
```

本次验证分别输出 `10.0.401` 和 `4.7.2.stable.mono.official.ed1daf0bf`。设置后重新打开 VS Code，使 F5 调试继承环境变量。构建脚本也会读取用户级 `GODOT4`，不要求将引擎加入 `PATH`。

## 3. 编译与运行

双击 `Play-Demo.cmd`，或执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/play.ps1
```

脚本依次编译 `game/CardRPG.csproj` 及其核心库、调用 Godot 导入资源，然后启动游戏。首次运行会自动生成 `game/.godot/` 和 C# 构建缓存。

只编译全部项目（包含测试程序集）：

```powershell
dotnet build CardRPG.sln --nologo --disable-build-servers
```

打开 Godot 编辑器：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/play.ps1 -Editor
```

工程入口为 `game/project.godot`，主场景为 `game/Scenes/MicroMaze.tscn`。VS Code 打开仓库根目录、安装推荐 C# 扩展后，可按 F5 使用已有调试配置；该配置会先执行编译任务。

## 4. 验证规则与完整流程

先完成上一节的全部项目编译，再执行规则测试：

```powershell
dotnet test tests/CardRPG.Core.Tests/CardRPG.Core.Tests.csproj --no-build --nologo
```

当前应通过 **42 项测试**，覆盖连续移动、碰撞、视野、门锁、物件、寻路和精确点击落点。

通过仓库提供的进程封装运行引擎内验证，等待引擎退出并检查错误：

```powershell
. .\scripts\common.ps1
Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--import')
Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--', '--verify-demo')
Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--', '--verify-mouse')
```

键盘验证成功输出 `MICRO_MAZE_VERIFIED`；纯鼠标验证成功输出 `MICRO_MAZE_MOUSE_VERIFIED`，二者退出码均应为 0。`--` 后的参数由 Demo 读取；平时游玩不传入这些验证参数。

`--headless` 不显示画面。需要图形检查或截图时，去掉它，并在 `--` 后增加 `--capture-dir=<绝对目录>`；截图目录建议放在 `artifacts/` 下。画面、窗口尺寸与完整验收记录见[微型迷宫 Demo 验收](../11_开发管理与验收/04_微型迷宫Demo验收.md)。

## 5. 导出 Windows 游戏包

确认已安装第 1 节的 .NET 导出模板，然后执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/export.ps1
```

脚本自动编译、导入资源，并通过 `game/export_presets.cfg` 中的 **Windows Desktop** 预设执行 Release 导出。导出目标为 Windows x86_64；仅执行 `dotnet build` 不会生成完整游戏包。

成功后输出目录包含：

```text
artifacts/MicroMaze/
├─ CardRPG-MicroMaze.exe
├─ CardRPG-MicroMaze.pck
└─ data_CardRPG_windows_x86_64/
```

双击 `CardRPG-MicroMaze.exe` 游玩。分发时复制整个 `MicroMaze` 文件夹，保留 `.pck` 和运行时目录。本机导出包包含 .NET 10.0.12 运行时；无开发环境的另一台设备仍需单独验收。

仓库提交源码、地图、场景、构建脚本、测试与文档；`.gitignore` 排除 `artifacts/`、`game/.godot/` 和 C# `bin/obj`。新克隆的仓库通过上述命令重新生成这些内容，现成本地导出包不会随 `git clone` 下载。

## 6. 常见问题

| 现象 | 处理方法 |
| --- | --- |
| 找不到 `dotnet` 或要求的 SDK | 安装 x64 .NET SDK 10.0.401，重开终端，在仓库根目录运行 `dotnet --version`；核对 `global.json` |
| 提示找不到 Godot .NET | 检查 `GODOT4` 是否指向存在的 `.exe`，并确认完整保留了引擎目录 |
| NuGet 还原失败 | 检查网络及 NuGet 源，然后执行 `dotnet restore CardRPG.sln`；Godot SDK 与 xUnit 包由还原获取 |
| 场景提示无法加载 C# 脚本 | 使用 .NET 版 Godot；先运行 `scripts/play.ps1` 完成编译与资源导入 |
| 导出提示模板或解决方案缺失 | 安装 4.7.2 .NET 模板；保留已提交的 `game/CardRPG.sln`，不要用 `.slnx` 替代 |
| F5 找不到引擎 | 配置用户级 `GODOT4` 后完全重启 VS Code；检查微软 C# 扩展是否启用 |
| 原生 OpenGL 退出崩溃 | 本工程已配置 Compatibility + ANGLE / Direct3D 11；保留 `game/project.godot` 中对应渲染设置 |

关闭游戏和编辑器后，`game/.godot/`、`src/CardRPG.Core/bin/obj` 和 `tests/CardRPG.Core.Tests/bin/obj` 可作为构建缓存清理；再次编译或运行脚本会重建。保留源码、场景、`.uid` 文件和需要使用的导出包。
