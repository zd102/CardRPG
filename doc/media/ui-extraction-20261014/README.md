# 搜打撤布局实机留档

2026-10-14，Windows x86_64 独立导出包。配装与库存参考《三角洲行动》的[官方仓库示例](https://deltaforce.garena.com/en/news/system/RMDWVM)组织为穿戴、随身、仓库／地面三栏；[修改依据与范围](../../08_界面与交互/05_搜打撤布局修正.md)说明了借鉴关系和本作的差异。

## 看实际画面

| 场景 | 原尺寸截图 |
| --- | --- |
| 三栏装备仓库 | [1100](1100-cargo.png) · [1280](1280-cargo.png) · [1600](1600-cargo.png) |
| 最小窗口、大阅读字号 | [库存](rework-1100-150-storage.png) · [装备比较](rework-1100-150-comparison.png) · [整备](rework-1100-150-prepare.png) |
| 首次部署与满包 | [整备与部署](01-preparation.png) · [满包与地面](inventory-full-pockets.png) |
| 战斗 | [普通战斗](04-battle.png) · [三敌人](1100-three-enemies.png) · [出牌预览](rework-card-preview.png) |
| 其他管理页 | [工坊](1280-crafting.png) · [任务](1280-journal.png) · [地图](1280-route-map.png) |
| 保存失败验证 | [模拟失败后撤回](rework-save-failure.png) |

## 验证与复现

构建 0 警告、0 错误，77 项核心测试通过。独立导出包无头交互与后台实际渲染均返回 `UI_REWORK_VERIFIED` 和 `CHAPTER_VERIFIED`，进程退出码 0。渲染使用未激活的 Windows 隔离桌面，未打开前台窗口，也未读取或修改玩家存档。

共生成 116 张截图，覆盖 1100×760、1280×800、1600×900 和三档阅读字号。全量缩略检查后，对库存、装备、整备、满包与三敌人等重点页面复核原图。本目录保留 15 张原始 PNG、10 张全量总览；`manifest.json` 提供全部原图尺寸与 SHA-256，`source-sha256.json` 对应受检源码，`render.txt`／`headless.txt` 是导出包验证日志，`export-sha256.json` 对应交付 ZIP 与其文件。

```powershell
./scripts/export.ps1
./scripts/verify-ui.ps1 -Executable "$PWD/artifacts/Expedition/CardRPG-Expedition.exe" -Headless -OutputDirectory "$PWD/artifacts/ui-extraction-export-headless"
./scripts/verify-ui.ps1 -Executable "$PWD/artifacts/Expedition/CardRPG-Expedition.exe" -OutputDirectory "$PWD/artifacts/ui-extraction-export-captures"
```

交付压缩包为 `artifacts/CardRPG-Expedition-20261014-ui.zip`，解压后运行 `Expedition/CardRPG-Expedition.exe`，保留 `.pck` 和 `data_CardRPG_windows_x86_64`。ZIP 包含 224 个文件，已检查归档 CRC 与文件清单。忽略目录中的旧临时 exe 文件没有纳入分发包。

原有 `ui-rework-20261014` 保存首轮布局，不能混作当前版本。截图证明的是这些实际状态下的渲染和操作；不等同于真人可用性、全系统 DPI 或手柄验证。单击选物、按钮／Shift+Enter 取放已实现，拖拽整理不在本轮范围内。

清理时已核实本轮中间截图目录 `artifacts/ui-extraction-layout-captures` 的绝对路径、创建时间和文件范围：117 个截图／日志文件、77,073,028 字节、无重解析点。自动审批拒绝删除，仅返回“blocked by policy”，因此目录保留，未改用其他方式删除。该目录不参与 Git 提交；正式证据与分发包不依赖它。
