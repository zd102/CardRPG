# 204 项 UI 优化：实现与后台验证

2026-10-15。A001–A204 代码已落地；[逐项状态](../../ui-implementation.json)记录实现位置，[实现索引](implementation-map.json)说明每组行为。`implemented` 不等于全部验收通过，未使用 `verified` 批量覆盖验收状态。

本机程序位于 `artifacts/Expedition-UI-20261015-R3/CardRPG-Expedition.exe`；运行或复制时保留同目录 `.pck` 与 `data_CardRPG_windows_x86_64`。[实现和操作说明](../../08_界面与交互/09_UI优化实施记录.md) · [实机截图浏览](index.html) · [机器可读验证摘要](verification-summary.json)。

## 已验证的范围

| 层次 | 证据 | 能说明什么 |
| --- | --- | --- |
| 核心规则 | [102 项测试](core-tests/core-tests.trx) | 批次、保护、数量、原子批量、材料计划、预设、奖励、备份恢复等规则；0 失败、0 跳过 |
| 构建与导出 | [导出日志](export.log)、[文件哈希](source-hashes.json) | Debug 构建与 Windows Release 导出完成；文案 JSON 随包可用 |
| 导出包无头 | [完整流程日志](package-headless/render.log) | 从开局到探索、战斗、交付、制作、拾取、保存回滚及全部专项的功能断言 |
| 导出包真实渲染 | [完整流程日志](package-render/render.log)、[截图](index.html) | 同一包在未激活的隔离桌面运行，实际 GPU 渲染；1100×760、1280×800、1600×900及三档字号 |
| 原始输入专项 | [验证实现](../../../game/Scripts/ChapterVerification.Completion.cs)、`completion-*` 截图 | 启动取消、自然 Tab、更多与详情、跨对象快照、模拟手柄、免费拾取退出边界、拖牌取消／执行、手牌显示重排、720、长文案、剧情暂停和队列失败恢复 |
| 真实背景对比 | [逐点样本](package-render/contrast-samples.json)、[文字图](package-render/contrast-with-text.png)、[同位置背景](package-render/contrast-background.png) | 管理页可见文字与其真实背景比值、像素位置；普通文字目标 4.5，较大文字目标 3；不把禁用态算入正文 |

`Chapter` 全流程中的 `Click` 是带定位和滚入视野的功能辅助工具。`Completion` 的 `RawClick`、`RawTabTo`、`RawDragCard` 仅发原始输入，目标被裁切会直接失败；初始化测试夹具与实际操作区段分开。两种结果不能互相冒充。

全部功能、截图及导出运行均在后台，无前台窗口或焦点切换。真实渲染使用 `scripts/verify-ui.ps1` 创建不激活的 Windows 桌面；无头检查不替代截图。

## 代表截图

| 关注点 | 图片 |
| --- | --- |
| 真实批次与固定操作区 | [标准物资页](package-render/management-storage.png)、[1100 大字](package-render/rework-1100-150-storage.png) |
| 数量对照和容量预测 | [成对数量表](package-render/management-paired-table.png)、[大字数量表](package-render/management-paired-large-type.png) |
| 右键和拖拽 | [对象菜单](package-render/management-context-menu.png)、[装备落点比较](package-render/management-equipment-drop.png) |
| 规划 | [材料计划](package-render/management-material-plan.png)、[预设检查](package-render/management-preset-preview.png)、[背包规格](package-render/management-bag-comparison.png) |
| 角色与装备 | [角色卡表](package-render/1280-character.png)、[来源矩阵](package-render/management-deck-sources.png)、[装备比较](package-render/rework-1100-150-comparison.png) |
| 固定阅读 | [跨对象快照](package-render/completion-pinned-comparison.png)、[增长约三成文案](package-render/completion-expanded-detail.png) |
| 小窗口与大字 | [1280×720](package-render/completion-720.png)、[1100×760／150%／长文案](package-render/completion-expanded-copy.png) |
| 战斗 | [三敌人](package-render/1100-three-enemies.png)、[六张手牌](package-render/completion-six-cards.png)、[手牌旁预览](package-render/rework-card-preview.png) |
| 任务队列 | [剧情暂停](package-render/completion-batch-story.png)、[继续交付](package-render/completion-batch-complete.png) |
| 场景与恢复 | [标签避让](package-render/rework-1100-labels.png)、[启动页](package-render/completion-startup.png)、[保存失败](package-render/rework-save-failure.png) |

## 未完成的验收

**A140：真实设备验收未做。** 已实现手柄按键／摇杆阈值、输入模式保持、720 布局、Windows `GetDpiForWindow` 查询与屏幕变化轮询。模拟输入、缩放策略断言和 720 截图只覆盖软件路径，不能替代真实手柄、混合 DPI、多显示器移动检查。

**A188：真人试用未做。** 已有原始输入自动化和本地匿名记录功能；没有填写真人效率、理解程度、满意度或点击节省比例。悬浮延迟和音量舒适度仍需真人体验反馈。

长文案模式用附加字符扩大名称与规则约三成，不是完整翻译测试；734 个已提取显示模板也不是所有内容的多语言交付。对比度仅证明样本所在管理页文字，不能外推为所有纹理、按钮和状态都逐像素认证。

旧批次保留在 [首批](../ui-implementation-20261015/) 与 [第二批](../ui-continuation-20261015/)；这些目录的旧计数不表示当前进度。当前目录中的构建基线用于限定清理范围，不是本轮完成快照。

临时构建和重复验证目录的清理被自动审批审查拒绝，原因仅返回 `blocked by policy`；已保留，未尝试其他删除方式，详见 [清理记录](cleanup.json)。`completion/`、`management/` 中含调试期旧图和失败图，请以 `package-render/` 和本页链接作为最终截图证据。
