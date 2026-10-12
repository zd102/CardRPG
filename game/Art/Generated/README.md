# 生图素材接入

2026-10-13：默认第一章已接入 **23 张卡通像素素材**，包括余火站、7 个探索地点、4 张任务状态变体、角色、徽章、卡牌和居民衣架。均通过内置 `image_gen` 生成或编辑，未调用 CLI/API 后备方案。资源清单、最终提示词与接入方式见 [chapter/README.md](chapter/README.md)。

## 旧原型尝试记录

2026-10-12 最初尝试生图时，foundry 请求返回 HTTP 502：`支持模型 gpt-image-2 的供应商今日已自动禁用`。以下为通道恢复前的旧 Expedition 原型记录与写实提示词，不代表当前第一章素材状态。

旧原型中，foundry.png 和 echo.png 可作为据点等页面的全屏背景，文件不存在时保持程序绘图。ExpeditionLocalScene、ExpeditionRegion 和 ExpeditionActor 继续保留供原型回归；默认入口已使用 Chapter 系列组件。标题、描述和按钮始终由原生控件渲染。

| 文件 | 用途 | 状态 |
| --- | --- | --- |
| foundry.png | 据点、战场环境、结算背景 | 已尝试生成，服务失败 |
| armory.png | 装备插画备选；当前覆盖层保留原场景，不使用此图 | 提示词已准备，未调用 |
| sentinel.png | 敌人插画备选；当前使用可点击角色，不使用此图 | 提示词已准备，未调用 |
| echo.png | 祝福插画 | 提示词已准备，未调用 |

实际调用的提示词与待生成提示词如下。统一使用内置生图工具，透明背景为 false。生成后须检查构图、文字区对比度和各窗口尺寸的裁切，再放入以上路径。图标与路线图继续使用可缩放代码图形。

## foundry

```text
Use case: stylized-concept
Asset type: production environment background for a Chinese desktop card RPG called Ashen Expedition, no UI.
Primary request: A richly detailed abandoned industrial foundry district at blue hour, cinematic panoramic landscape, weathered railway tracks curving from foreground toward a monumental smelting hall, warm amber furnace light through tall windows, gantry cranes, pipes, iron walkways, crates, distant smokestacks fading into cool mist. No characters. Painterly realistic game concept art with fine architectural detail and atmospheric depth, premium dark fantasy industrial exploration mood. Charcoal and desaturated petrol teal palette, restrained brass and amber light. Wide 3:2 composition. Left foreground relatively dark and quiet to support separately rendered text; focal foundry on right half. Edge to edge illustration, no border, no lettering, no words, no logos, no watermark, no UI, no collage.
```

## armory

```text
Use case: stylized-concept
Asset type: production equipment illustration for a dark industrial card RPG.
Primary request: Close cinematic still life inside an abandoned foundry workshop: heavy forged steel war hammer, short copper induction staff with a muted cyan coil, riveted iron chest armor, and a pair of brass mechanical bracers laid carefully on a scarred wooden workbench. The hammer and bracers are clearly legible hero objects, with tiny warm amber lights in the bracer mechanisms. Cold teal shadows, charcoal iron, warm brass highlights, airborne dust in side light. Painterly realistic game concept art, refined material texture, collectible equipment atmosphere matching a misty industrial foundry game. Landscape 3:2, objects centered with breathing room, table in foreground, dark defocused workshop behind. Edge to edge full illustration. No text, no labels, no numbers, no logos, no UI, no panels, no watermark.
```

## sentinel

```text
Use case: stylized-concept
Asset type: production battle encounter illustration for a dark industrial card RPG.
Primary request: A massive armored foundry sentinel, an inhuman industrial automaton of riveted black iron and aged brass, glowing amber furnace heart and narrow illuminated visor, carrying a heavy iron hammer, standing guard on a factory catwalk. One main sentinel right of center, readable upper body and weapon with large silhouette; behind it shadowy iron pillars, pipes, sparks and a smelting furnace. Painterly realistic premium game concept art with cinematic depth, charcoal and petrol teal shadows, restrained copper gold highlights. Ominous, grounded mechanical design, no gore. Wide 3:2 landscape composition, left third relatively quiet dark architecture for live interface overlays. Edge to edge image, no writing, no numbers, no logo, no UI, no frame, no watermark.
```

## echo

```text
Use case: stylized-concept
Asset type: production blessing / upgrade illustration for a dark industrial card RPG.
Primary request: A mysterious mechanical resonance device on an iron pedestal in a silent abandoned foundry workshop. Concentric aged brass rings suspended around a small radiant turquoise energy crystal, fine copper coils and ceramic insulators, restrained amber indicator lights. Entire device clearly framed at center-right, occupying half the scene; dark workshop on left with room for separately rendered typography. Painterly realistic game concept art, beautiful tactile brass and charcoal metal, misty teal ambient light, a few floating dust motes, mystical technology yet grounded industrial construction. Cinematic 3:2 landscape, premium cohesive collectible RPG art. Edge to edge illustration, no text, no lettering, no logos, no UI, no collage, no watermark.
```
