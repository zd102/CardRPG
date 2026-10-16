# 卡牌动作插画图集

使用内置 imagegen 生成，2026-10-16。文件：`card-actions.png`，3 列 × 2 行，运行时通过 AtlasTexture 切分，不含文字或交互控件。

顺序：锤击、防护、导能、攻防、准备、群攻。每格独立取景，保持工业版画的深绿、暖纸色和锈橙色；图像只表达动作类型，精确效果始终以运行时卡牌文字与预览为准。

生成提示词保存在本文件下方。使用新文件，未覆盖原有场景、装备或卡牌素材。

```text
Use case: stylized-concept. Asset type: a production game illustration sprite atlas, ONE landscape image with exactly 3 columns and 2 rows of equally sized square cells (3:2 overall aspect ratio), no gaps, no border, each scene entirely confined to its own cell. This is for a Chinese industrial-fantasy deckbuilding RPG called CardRPG; do not render any text. Style: refined screen-printed woodcut illustration, visibly tactile fine paper grain and engraved hatching, restrained dark petrol green #203934, warm ivory #e7dcc2, aged brass and muted burnt orange. Not pixel art, not glossy 3D, no neon. All six cells have strong simple silhouettes readable at 140 pixels wide. Each cell is a cropped cinematic action vignette with subject centered and clear margins. TOP LEFT: heavy iron maintenance hammer striking a metal plate, broad downward diagonal and orange impact sparks, conveys direct attack. TOP MIDDLE: dark teal armored forearm bracing a large riveted shield against incoming sparks, conveys defense and stability. TOP RIGHT: a short brass conductor rod releasing a branching pale teal electric arc against dark machinery, conveys magic / electric attack. BOTTOM LEFT: shielded armored arm driving a hammer forward from behind the shield, clear counterattack silhouette, conveys attack plus guard. BOTTOM MIDDLE: gloved hands deftly swapping small brass tools over a workbench, energetic curved motion and two light paper silhouettes, conveys preparation / drawing cards. BOTTOM RIGHT: wide sweeping industrial shovel cleaving across several shadowy mechanical silhouettes with a broad orange arc, conveys multi-target attack. Keep consistent woodcut / editorial game art style across all cells, vary silhouette and light distribution so six actions are visually distinct. No lettering, digits, logos, interface frames, card borders, labels, or watermark. Render only the six artworks at equal exact grid locations.
```
