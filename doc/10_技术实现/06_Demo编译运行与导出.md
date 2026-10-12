# Demo 编译、运行与导出

> 灰烬远征，2026-10-12。[返回概览](../00_游戏概览.md)

## 启动

已导出版本：运行 artifacts/Expedition/CardRPG-Expedition.exe。移动整个 Expedition 目录时，保留旁边的 .pck 和 data_CardRPG_windows_x86_64 文件夹。

从源码运行：双击根目录 Play-Demo.cmd。脚本编译 C#、导入 Godot 资源并打开唯一入口 Scenes/Expedition.tscn。首次需要安装 Godot 4.7.2 .NET 和 .NET SDK 10.0.401；Godot 路径通过 GODOT4 环境变量读取，也检查当前用户变量和约定安装路径。

## 构建与验证

仓库根目录执行：

```powershell
dotnet build CardRPG.sln --nologo --disable-build-servers
dotnet test tests/CardRPG.Core.Tests/CardRPG.Core.Tests.csproj --nologo
. ./scripts/common.ps1
Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--import')
Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--', '--verify-expedition')
```

界面验证在无显示模式也发送真实 GUI 输入。截图需要普通渲染启动，并在用户参数传入 --verify-expedition 与 --capture-dir=绝对目录；正常运行不会产生截图。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/export.ps1
```

Windows 导出需要安装与 Godot 版本匹配的导出模板。脚本输出 artifacts/Expedition/CardRPG-Expedition.exe；检查引擎退出码及 ERROR 日志，防止把失败导出当作成功。

## 常见问题

- 找不到 Godot：设置用户环境变量 GODOT4 后重新启动终端或编辑器。
- C# 未加载：先运行 build，再执行资源 import。
- 字体：优先使用 Microsoft YaHei UI，回退系统中文字体。
- 窗口最小为 1100×760；长页面滚动，不要求改变操作系统缩放。
- 存档失败：界面保留错误和内存进度，检查磁盘及用户目录权限后再保存退出。
