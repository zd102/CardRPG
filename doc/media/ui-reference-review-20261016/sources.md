# 审查资料与来源

查阅日期：2026-10-16。资料用于当前项目的界面审查；竞品截图的权利归各自权利人。没有启动两款竞品客户端，也没有把静态素材推断为已验证的快捷键或操作性能。

## 本项目的 23 张截图

源目录：`artifacts/Expedition-UI-20261016-Redesign/preview/`。

`captures/` 中保存相同文件名的原始 PNG 副本，未裁切、重绘或添加标记；复制前后逐一核对 SHA-256。`capture-manifest.json` 记录名称、来源路径、尺寸与原有哈希。编号与上一版预览页的展示顺序一致。

逐图审查只证明所列截图中的可见布局和文字。战斗预测条件与结算行布局还对照了当前源码；未重新运行游戏或推导真人耗时。

## 主要竞品参考

| 文件 | 来源 | 用途 |
| --- | --- | --- |
| `duckov-loot.jpg` | [Steam 商店原图](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/3167020/ss_ffb859764420f62c612287f011361749a06b6432.1920x1080.jpg) | 携带区与容器的同时呈现、物品图形和容量的位置关系 |
| `delta-stash.png` | [Garena 官方仓库图](https://web.df.garena.com/03_news/news_banner/SMP/202602_NewToOperations/2.png) | 穿戴区、库存区、整理动作的分工 |
| `delta-loadout.png` | [Garena 官方配装图](https://web.df.garena.com/03_news/news_banner/SMP/202602_NewToOperations/8.png) | 出战装备与补给检查集中呈现 |
| `delta-gunsmith.png` | [Garena 官方属性图](https://web.df.garena.com/03_news/news_banner/SMP/202602_NewToOperations/9.png) | 属性详情按维度组织 |

鸭科夫媒体来源由[Steam 官方应用数据](https://store.steampowered.com/api/appdetails?appids=3167020&l=english)核对，拾取图为媒体列表 id 1。由于本机访问 Akamai 资源连接中断，图片通过 Steam 的 `shared.st.dl.eccdnx.com` CDN 下载，路径与图片标识相同。

三角洲图片来自 Garena 官方于 **2026-02-20** 发布的 [Operations Mode 101](https://deltaforce.garena.com/en/news/system/RMDWVM)。红框、绿底文字为该指南的教学标注，不是对游戏界面的独立观察。该资料也不构成对中国区当日客户端每项功能和键位的验证。

## 已查看但不作为当前客户端功能证据的素材

以下图片仍列在当前 Steam 商店媒体列表中，但其中两张可见 `trial version`，其余画面也缺少可核验的客户端版本。它们仅保留为本次阅读过的历史素材，不承担当前界面行为的证据：

| 文件 | 官方媒体标识 |
| --- | --- |
| `duckov-world.jpg` | `ss_f89c284a734b7307df490813df82d4f85a981299`，可见 trial version |
| `duckov-screen-2.jpg` | `ss_b822619832ab8d2337b7e31a77c4311a5d168a97`，可见 trial version |
| `duckov-screen-3.jpg` | `ss_0f96530f53f858cbaf4de595cb1a95ec6d016b26` |
| `duckov-screen-4.jpg` | `ss_67e534105478e4fe28ef84043f4a5db5ebb8bda5` |

本次未将搜索结果中的模组页面、玩家攻略或媒体转述作为原生功能依据。没有依据游戏流行程度给 UI 打分。

`review-preview.png` 与 `layout-preview.png` 是本审查 HTML 页面在无头 Edge 中的预览图；后者为建议布局草图，不是游戏实际界面。`review-check.json` 记录审查页的图片加载、筛选、草图切换和本地链接检查，不代表游戏功能重新通过测试。
