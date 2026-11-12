# UnityMonster 资源导航

在 Unity 顶部菜单打开 **资源导航 → 打开分类导航**，可直接打开分类文件夹与预览场景。

| 目录 | 内容 |
| --- | --- |
| `VRC` | VRC 角色、服装、材质，以及 AvatarPortability 适配工具 |
| `Monsters/Boss` | Cyber Samurai、Darklord，以及 Boss 技能预览 |
| `Monsters/Jotunnslayer` | 已提取怪物、行为脚本、导入工具与怪物预览 |
| `AssetStore` | 完整商店资源包：三个动画包、武器、TPS KIT、KINEMATION 等 |
| `Game` | 项目场景、地图、角色选择、本地化与编辑器工具 |
| `ThirdParty` | Mirror、MagicaCloth2、后处理插件 |
| `Resources` / `StreamingAssets` | 保留 Unity 运行时加载目录 |
| `TextMesh Pro` | 保留 TMP 标准资源目录 |

## 常用预览

- Boss：`Assets/Monsters/Boss/BossSkillPreview.unity`
- 已提取怪物：`Assets/Monsters/Jotunnslayer/MonsterPreview/MonsterPreview.unity`
- Paladin 原包：`Assets/AssetStore/Paladin_Anim_Set/ScenePreview/Paladin_Anim_Set.unity`

打开场景后进入 Play。Boss 预览可切换角色和动作；怪物预览可选模型、动作、特效并显示范围标尺。范围标尺不代表所有动作已复原原游戏判定。

三个完整动画包位于 `Assets/AssetStore/KatanaAnimationPack`、`Assets/AssetStore/Sword and Shield Animation Pack`、`Assets/AssetStore/Paladin_Anim_Set`，没有删减文件。

目录迁移保留原 `.meta` 与 GUID。历史验收报告保留当时路径；当前查找位置以本导航为准。

Gun and Sword 完整包：`Assets/AssetStore/Gun and Sword Animation Pack`；原包预览：其 `Scenes/Demo.unity`。
