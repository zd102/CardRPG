# Demo 编译、运行与导出

> 灰烬远征，2026-10-13。[返回概览](../00_游戏概览.md)

## 启动

已导出版本：运行 artifacts/Expedition/CardRPG-Expedition.exe。移动整个 Expedition 目录时，保留旁边的 .pck 和 data_CardRPG_windows_x86_64 文件夹。

从源码运行：双击根目录 Play-Demo.cmd。脚本编译 C#、导入资源并打开默认入口 Scenes/Chapter.tscn，进入第一章外环。需要 Godot 4.7.2 .NET 和 .NET SDK 10.0.401；路径通过 GODOT4 环境变量读取，也检查用户变量和约定安装路径。

## 构建与验证

仓库根目录执行：

```powershell
dotnet build CardRPG.sln --nologo --disable-build-servers
dotnet test tests/CardRPG.Core.Tests/CardRPG.Core.Tests.csproj --nologo
. ./scripts/common.ps1
Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--import')
Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--', '--verify-chapter')
```

界面验证在无显示模式也发送真实 GUI 输入。截图需普通渲染启动，传入 --verify-chapter 与 --capture-dir=绝对目录；正常游玩不产生截图。验证隔离玩家存档，旧 expedition-v1.json 不读取或覆盖。

验证走完七个地点、救援与暗室、战斗、夹具校准、两处撤离、任务交付、台架试用、制作与包裹。检查 12 张新版场景、24 个透明图集区域及卡牌插画映射，确认资源来自 `Art/Print`，并检查任务绘景切换、地图和额外抽牌后的手牌排布；M / Esc、Enter 出征与真实鼠标操作均被执行。装备与任务长详情在覆盖层内滚动，场景不预留固定侧栏或底部操作面板。

补充流程从新远征开始，通过正常移动等待巡逻抵达，在两处巡逻地点与三种窗口尺寸检查交互标记，再实际结束回合触发第三个敌人增援、战败结算、回营和重新出征。验证过程中不读写玩家存档。

插画手册界面还验证装备分类、供牌详情的边界与键盘说明、只读地图点击、窗口内 Tab 焦点，以及“整备 → 装备 → Esc 返回整备”的路径。在 1100×760、1280×800、1600×900 下检查窗口边界与场景标记避让，并比较浏览前后的完整状态，确保不消耗步数或修改进度。不可用手牌仍可聚焦查看原因，按下不会消耗资源。桌面使用原生像素布局，避免小窗口把文字整体缩小。[查看新版界面](../media/hud-preview.html)。

旧原型回归可显式指定 `res://Scenes/Expedition.tscn -- --verify-expedition`；它不是默认入口。第一章外环存档与规则独立，范围见[交付记录](../13_第一章_余火铸造区/08_外环实现与验收.md)。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/export.ps1
```

Windows 导出需要安装与 Godot 版本匹配的导出模板。脚本输出 artifacts/Expedition/CardRPG-Expedition.exe；检查引擎退出码及 ERROR 日志，防止把失败导出当作成功。

验证独立导出包（在仓库根目录执行；PowerShell 管道等待 GUI 程序结束）：

```powershell
& .\artifacts\Expedition\CardRPG-Expedition.exe --headless -- --verify-chapter | Out-Host
```

实际画面验证需移除 `--headless` 并追加 `--capture-dir=绝对目录`。成功日志必须包含 `CHAPTER_VERIFIED` 且进程退出码为 0。当前截图留档见[界面预览](../media/hud-preview.html)。

截图脚本在布局稳定后通过 `RenderingServer.ForceDraw(false)` 主动绘制再读取画面，避免静态或后台窗口因等待自动绘制信号而停顿。导出包截图命令示例（使用默认 ANGLE 渲染）：

```powershell
& .\artifacts\Expedition\CardRPG-Expedition.exe --disable-vsync --max-fps 60 -- --verify-chapter "--capture-dir=$PWD/artifacts/print-hud-verify" | Out-Host
```

2026-10-13 本轮结果：构建 0 警告、0 错误，37 项核心测试通过；源码无界面验证、导出包无界面验证及默认 ANGLE 下的导出包截图流程均返回 `CHAPTER_VERIFIED`。预览页已更新 20 张导出包实机截图。截图验证使用独立测试状态，不读写玩家存档。

## 常见问题

- 找不到 Godot：设置用户环境变量 GODOT4 后重新启动终端或编辑器。
- C# 未加载：先运行 build，再执行资源 import。
- 字体：优先使用 Microsoft YaHei UI，回退系统中文字体。
- 窗口最小为 1100×760；长页面滚动，不要求改变操作系统缩放。
- 存档失败：界面保留错误和内存进度，检查磁盘及用户目录权限后再保存退出。
