# 2026-10-14 UI 审查证据

[完整评审](../../08_界面与交互/02_UI交互与视觉全面评审.md)

源码基线：`6c1e05f`。以源码新构建的第一章入口验证，未验证旧导出包。环境：Windows、Godot 4.7.2 .NET、ANGLE／D3D11、Intel UHD Graphics 770。验证参数包含 `--verify-chapter`，不读取或覆盖玩家存档。

## 验证结果

| 检查 | 结果 |
| --- | --- |
| `dotnet build CardRPG.sln --nologo --disable-build-servers` | 通过，0 警告、0 错误 |
| Godot `--headless --path game -- --verify-chapter` | 退出 0，`CHAPTER_VERIFIED` |
| Godot 真实图形渲染，同一自动验证加 `--capture-dir` | 退出 0，`CHAPTER_VERIFIED`，73 张截图 |
| 截图尺寸 | 1100×760、1280×800、1600×900 |
| 人工视觉审查 | 全部截图总览＋关键页面原尺寸检查 |
| 专项补充 | 名义前景／背景对比度；部分截图绿色盲近似模拟 |

真实渲染进程由 `CreateDesktop`／`CreateProcess` 启动在隔离桌面，启动信息指定隐藏窗口，不调用 `SwitchDesktop`，不向用户桌面注入输入。引擎通过 `RenderingServer.ForceDraw(false)` 渲染并从视口纹理存图；因此本轮截图不是无头占位图。

通过标记：

```text
CHAPTER_VERIFIED | real input, 7 places, print palette, graphic HUD, focus inspection, modal focus, nested return, filters, three window sizes, rescue, hidden room, combat, calibration, exits, quests, trial, crafting, parcel, save restore, three enemies, defeat recovery, physical inventory, full pockets, ground pickup, backpack crafting
```

本次没有新增游戏代码或测试代码。没有另跑完整 Core 单元测试套件；本任务结论基于上述 UI 验证与源码检查。未将自动验证通过当作体验、性能或可访问性全面通过。

临时文件清理：已检查本次新建的 `artifacts/ui-audit-20261014` 的绝对路径、项目内范围、链接属性与删除预览，但递归删除命令被自动审批策略拦截，未提供进一步原因，因此该目录仍保留在本机，包含完整截图和验证辅助文件。此前已有的构建缓存与导出包未删除。

## 截图索引

联系表保留所有 73 张截图的缩略图与原文件名；用于覆盖检查，文字细节以原图为准。

- [01：初始据点、整备、指引、入口、废料场、战斗、工坊、暗室](contact-01.jpg)
- [02：广场、结算、回营、货站、试用、制作、装备、列车](contact-02.jpg)
- [03：1100 窗口的手册、战斗和巡逻](contact-03.jpg)
- [04：1100 三敌人及 1280 窗口各页](contact-04.jpg)
- [05：失败、不可用牌及 1600 窗口](contact-05.jpg)
- [06：1600 后续页面及物品边界状态](contact-06.jpg)
- [07：扩仓与补给取放](contact-07.jpg)

保留原图：[据点](01-camp.png)、[战斗读牌](04-card-inspection.png)、[三敌人](1100-three-enemies.png)、[仓储](1100-cargo.png)、[装备](1100-equipment-detail.png)、[制作](1280-crafting.png)、[改装](1280-mods.png)、[列车选择](11-train-choices.png)、[满包](inventory-full-pockets.png)、[成功结算](06-extraction.png)、[失败结算](14-defeated.png)。其余原图未重复加入仓库；尺寸与 SHA-256 留在 [manifest.json](manifest.json)，可按原自动流程重建。截图状态与焦点可能因渲染时机而不同，哈希不作为跨运行验收条件。

## 色觉与对比度检查方法

[colour-check.png](colour-check.png) 左侧为原始截图裁剪，右侧为绿色盲近似模拟。使用 [Machado、Oliveira、Fernandes 2009 模型](https://pubmed.ncbi.nlm.nih.gov/19834201/) 的完整 deuteranopia 变换矩阵，在 sRGB 线性化后计算，截断到显示色域，再编码回 sRGB；Pillow 33 级三维查找表实现。它是视觉筛查辅助，不是用户视觉重建或医学判断。

```text
 0.367322   0.860646  -0.227968
 0.280085   0.672501   0.047413
-0.011820   0.042940   0.968881
```

名义对比度以 `ChapterGame` 中前景色和 `#e7dcc2` 底色，用 sRGB 相对亮度及 `(L亮 + 0.05)/(L暗 + 0.05)` 计算。Ink 8.53、Muted 4.47、Accent 4.11、Danger 4.69、Success 4.99。未逐像素测量全部纸张纹理背景；这组数值只用于定位风险，不应当作实机全状态合规报告。

## 外部参照

参考来源与设计含义见评审第二节。实际查看的图片包括 Mega Crit 媒体包 `ss8.jpg`、`ss4.jpg`，Klei `3-fight1.jpg`、`5-convo1.jpg`，Pretty Soon `backpack_1.jpg` 至 `backpack_4.jpg`；《Into the Breach》参考开发者 GDC 复盘。没有把这些商业游戏图片复制进项目美术资源，也没有把截图观察写成亲自游玩结果。
