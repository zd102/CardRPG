# UI 优化第二批验证

本批新增成对库存数量表、容量／超重预测、材料反查、装备比较与槽位拖拽、地图目标与路线成本、紧凑整备、输入释放边界，以及卡组费用条和来源表。逐项状态以 [实施记录](../../08_界面与交互/09_UI优化实施记录.md) 和 [JSON 清单](../../ui-implementation.json) 为准。

当前可运行包：`artifacts/Expedition-UI-20261015-R2/CardRPG-Expedition.exe`。需保留同目录 PCK 与 .NET 数据目录；旧版未覆盖。

## 检查范围

- 核心测试 96 项通过，新增已知路线与隐藏节点、路线预览无副作用、任务物从地面到撤离目标、19.9／20／20.1 kg 边界、精确装备空间阻塞测试。见 [TRX](core-tests.trx)。
- 源码完整第一章后台渲染通过：七地点、战斗、任务、背包、制作、存档、回滚；三种窗口尺寸与三档字号回归。见 [完整日志](chapter-render.txt)。
- 管理专项直接发送鼠标拖拽、右键和键盘事件，检查无效落点、取消、保留物确认、指定数量、跨区焦点、按住确认键跨弹窗、装备槽只打开比较、成对表批次选择、材料反查和地图标记只读性。测试会辅助定位及滚动到对象，不作为真人操作效率测量。
- 导出包的最终检查记录在 [完整无头日志](package-headless/render.log) 与 [管理页渲染日志](package-management/render.log)；最终结果以 [汇总](verification-summary.json) 为准。
- 所有 Godot 渲染进程均在未激活的隔离桌面启动，未打开前台窗口。图片为实际渲染，不是设计稿。

源码完整截图与最终导出源码仅相差“配方预览”的对话框标题；导出包专项再次检查最终界面。源码与包的指纹分别见 [source-sha256.json](source-sha256.json)、[package-sha256.json](package-sha256.json)。完整截图清单见 [capture-manifest.json](capture-manifest.json)，代表截图见下表。

## 代表截图

| 变化 | 实际截图 |
| --- | --- |
| 成对数量表与真实批次 | [1280 宽度](management-paired-table.png)、[1100／150%](management-paired-large-type.png) |
| 容量与负重预测 | [1100／150%](management-large-type.png) |
| 装备槽拖拽后比较 | [装备比较](management-equipment-drop.png) |
| 材料反查已知配方 | [配方预览](package-management/management-material-recipe.png) |
| 地图临时目标和基础成本 | [1280 宽度](management-map-target.png)、[窄布局／150%](management-map-large-type.png) |
| 紧凑整备 | [1100／150%](management-prepare-large-type.png) |
| 卡组来源表与费用条 | [来源表](management-deck-sources.png) |

尚未进行手柄、系统 DPI、多屏、720 高度与真人效率测试。来源矩阵联动、完整焦点分区、野外拖拽和拾取多层边界仍作为部分实现保留。

构建前状态与文件清单见 [build-baseline.json](build-baseline.json)。清理只针对本轮新增的临时截图和发布元数据，之前被拒绝清理的目录不在本轮范围内；实际处理结果见 [cleanup-result.json](cleanup-result.json)。
