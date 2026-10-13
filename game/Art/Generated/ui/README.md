# 余火站 · 界面切图

> 早期方案留档。当前第一章使用 [木刻插画资源](../../Print/README.md) 与原生纸张控件，已不加载本目录切图；以下保留当时的生成和接入说明。

2026-10-13，使用内置 `image_gen` 生成并接入第一章界面。风格为深墨绿、旧黄铜与纸质标签；完整生成词保存在 [prompts.json](prompts.json)。

| 图片 | 用途 | 导入尺寸上限 | 九宫格边距 |
| --- | --- | --- | --- |
| [field-panel.png](field-panel.png) | HUD、窗口外框、次要按钮、场景标签、敌人状态与手牌边框 | 256 px | 四边 14 px |
| [brass-button.png](brass-button.png) | 主操作、当前导航与分类选中状态 | 384 px | 四边 16 px |
| [ledger-paper.png](ledger-paper.png) | 当前目标、装备供牌详情、任务记录、地图、整备清单与结算 | 256 px | 四边 14 px |

原始 PNG 保留生成分辨率。Godot 的 `.png.import` 在导入时缩小纹理，`ChapterSkin.cs` 用 `StyleBoxTexture` 切分四角、边缘与中心，保持边框厚度，不拉伸整张图片。使用最近邻采样，图片中不包含文字；标题、数值、状态和说明均由控件渲染。

普通、悬停、按下与禁用共用切图，通过调色区分；选中使用黄铜底，键盘焦点另有明确描边。未覆盖原有场景及卡牌插画。

实际效果见[界面预览](../../../../doc/media/hud-preview.html)。
