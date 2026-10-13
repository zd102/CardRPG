# 第一章卡通像素素材

> 初版像素风留档。当前默认章节已改用 [木刻插画资源](../../Print/README.md)。以下为本目录最初的生成、接入与验证记录。

2026-10-13。当前目录的 23 张 PNG 均已接入默认 `Chapter.tscn`。使用内置 `image_gen` 生成或编辑，没有使用 CLI/API 后备方案。所有项目引用均位于此目录，运行与导出不依赖用户目录中的生成缓存。

| 素材 | 数量 | 用途与状态 |
| --- | --- | --- |
| camp、A01～A07 | 8 | 全屏据点与外环地点，统一像素卡通风 |
| camp-lit | 1 | Q01 交付后，工作灯和旧日城市投影 |
| A03-rescued | 1 | 救援后废料台上的维修偶离开 |
| A04-dark | 1 | 关闭检修灯后，荧光暗门显现 |
| A05-powered | 1 | 永久修复货站后，配电柜与撤离信号通电 |
| enemy-E01、enemy-E02、enemy-E03、enemy-E13、enemy-dummy | 5 | 四种敌人及试用练习靶，透明背景 |
| repair-robot、clothes-rack | 2 | S07、S09 交付后据点变化，透明背景 |
| crest | 1 | 生命 HUD 徽章，透明背景 |
| card-strike、card-ward、card-arc | 3 | 攻击、防御与电弧类牌面插画，牌名和效果由原生控件显示 |

## 提示词与生成记录

- [pixel-prompts.json](pixel-prompts.json)：18 张基础素材的最终像素风重绘提示词。
- [state-prompts.json](state-prompts.json)：4 张状态变体，以对应基础场景为编辑目标，保持镜头与物件位置。
- [home-props-prompts.json](home-props-prompts.json)：透明居民衣架，以据点作为风格参考。
- [prompts.json](prompts.json)、[character-ui-prompts.json](character-ui-prompts.json)：初始写实方向留档，已由像素版本替换。

这次续作新增 `camp-lit.png`、`A03-rescued.png`、`A04-dark.png`、`A05-powered.png`、`clothes-rack.png`。原始输出仍保留在内置工具生成缓存中，以上文件是项目使用的副本。

## 接入与验证

ChapterArt 缓存导入纹理。ChapterScene 按任务状态选择绘景，并共享覆盖裁切坐标给 ChapterHotspot 和据点道具；缩放采用最近邻采样。ChapterGame 在窗口调整后刷新排布，保持手牌与操作可见。装备、任务与整备详情为居中覆盖层，场景不预留固定侧栏或底部操作面板。

`--verify-chapter` 检查所有 23 张纹理可加载、8 张透明素材的背景 alpha、任务状态切换、交互标记不重叠、手牌可见和真实鼠标键盘流程。截图模式另检查 1280×800、1100×760 和 1600×900 的实际渲染。玩家存档不受验证流程影响。

实际界面见[预览页](../../../../doc/media/ui-preview.html)，已使用 Windows 导出包的最新截图替换旧原型画面，并提供任务变化前后的对照。另覆盖巡逻预警、三敌人同屏与战败回营；验证说明见[外环验收](../../../../doc/13_第一章_余火铸造区/08_外环实现与验收.md)。
