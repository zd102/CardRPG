# UI 实施验证记录

本目录对应 [204 项实施记录](../../08_界面与交互/09_UI优化实施记录.md)。本轮是持续实施中的一个版本，未宣称全部条目验收完成。

- `core-tests.trx`：88 项核心测试结果。
- `headless.txt`、`render.txt`：项目完整无头／后台渲染回归。
- `package-headless.txt`：最终 Windows 导出包的完整无头回归。
- `package-render.txt`：最终导出包的管理专项真实输入与截图验证。
- `source-sha256.json`、`capture-manifest.json`、`package-sha256.json`：源码、原始截图与导出包校验。
- `verification-summary.json`：验证范围与结果汇总。真人试用、真实手柄、DPI／多屏测试尚未完成。

所有窗口均在从未激活的隔离桌面运行，未打开前台窗口。验证用例使用测试存档和隔离验证入口，不读写玩家存档。

## 代表截图

| 内容 | 截图 |
| --- | --- |
| 1100×760、150% 阅读字号；图文不重叠 | [大字库存](package-management-large-type.png) |
| 右键与可见更多入口 | [上下文菜单](package-management-context-menu.png) |
| 按指定数量拖动后的结果 | [拖拽结果](package-management-drag-result.png) |
| 预设应用前确认 | [预设比较](package-management-preset-preview.png) |
| 多配方材料汇总与已知来源 | [材料计划](package-management-material-plan.png) |
| 单件装备的改装差异与固定动作 | [改装](package-management-modifications.png) |
| 三种背包同时比较 | [背包规格](package-management-bag-comparison.png) |
| 制作次数与产出分开 | [补给制作](package-management-supply-quantity.png) |
| 按类型与地点分组 | [结构化日志](package-management-structured-log.png) |
| 保存失败不遮部署按钮 | [保存失败](rework-save-failure.png) |
| 致死后果与明确取消 | [撤退确认](rework-lethal-retreat.png) |

`package-` 截图来自最终导出包。其他截图来自完整章节路径；最后一次图块插画留白调整由导出包大字截图及新增几何断言复核。测试夹具与实际完整章节路径的物资数量可能不同，不用于对比真人操作效率。
