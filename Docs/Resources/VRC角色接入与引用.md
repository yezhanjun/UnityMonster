# VRC 整装角色接入与引用

更新日期：2026-11-17。源工程：`E:/Project/VRCTestRes`，当前四个已同步角色为 Wendy、Shinano、Sapphy、Milltina FC。按整体角色使用，保留骨骼、衣物与便携布料组件，不要求调用换装界面。

## 游戏引用入口

| 角色 | 选择 ID | Resources.Load<GameObject> 路径 | 整装资源 |
|---|---:|---|---|
| Wendy | 12 | `PortableCharacters/Wendy` | `Assets/Resources/PortableCharacters/Wendy.prefab` |
| Shinano | 13 | `PortableCharacters/Shinano` | `Assets/Resources/PortableCharacters/Shinano.prefab` |
| Sapphy | 14 | `PortableCharacters/Sapphy` | `Assets/Resources/PortableCharacters/Sapphy.prefab` |
| Milltina FC | 15 | `PortableCharacters/Milltina` | `Assets/Resources/PortableCharacters/Milltina.prefab` |

代码映射位于 `Assets/Game/CharacterSelection/RangeCharacterSelection.cs`。Inspector 也可以直接引用表中的 prefab。头像沿用 `Resources/CharacterPortraits/{角色英文名}_Icon`。

```csharp
var prefab = Resources.Load<GameObject>("PortableCharacters/Sapphy");
var character = Object.Instantiate(prefab, visualParent);
character.transform.localPosition = Vector3.zero;
character.transform.localRotation = Quaternion.identity;
character.transform.localScale = Vector3.one;
var animator = character.GetComponent<Animator>();
animator.runtimeAnimatorController = gameplayController;
```

这些是视觉模型，不单独承担玩家输入、网络移动或碰撞控制。现有游戏角色选择系统仍负责对应职责。不要把模型内部衣服拆到另一个根节点，也不要把蒙皮骨骼重绑到别的角色。

## 来源与生成

| 角色 | 本工程源预制体（位于 Assets/VRC/Characters） | 默认配置 |
|---|---|---|
| Wendy | `Wendy/Character/Wendy Current Default.prefab` | 预览工程当前默认整装、原始比例 |
| Shinano | `Shinano/Character/Shinano Current Default.prefab` | 预览工程保存的当前默认整装 |
| Sapphy | `Sapphy/Character/Sapphy.prefab` | 作者原装；不再以 Rin N008 套装作为默认 |
| Milltina | `Milltina/Character/Milltina FC Portable.prefab` | FC 体型与原装衣服 |

编辑器菜单 `TPSShoot/Characters/Prepare and Validate Portable Characters` 重新生成上述四个 Resources 入口。实现：`Assets/Game/CharacterSelection/Editor/PortableCharacterSetup.cs`。生成时根缩放为 1，主 Animator 使用角色自身 Humanoid Avatar，关闭 Root Motion，关闭预览表情 UI 的自动构建；游戏控制器由游戏侧指定。

独立比例研究版本不替换 Wendy 当前默认，避免本次同步改变角色比例。

## 布料和材质依赖

- `Assets/VRC/AvatarPortability`：PortableBonePhysics、PortableBoneCollider、WardrobePoseFollower，以及预制体实际引用的体型/丝袜修正组件。
- 保留 BakedWardrobeSlot、WardrobeController、WardrobeItem 及其引用；整装模式下它们仍可能负责衣物绑定与初始化。
- 物理根与碰撞体必须在同一个角色实例内。实例化完整 prefab，不共享另一个实例的骨骼。
- 保留当前工程的 `Packages/jp.lilxyzw.liltoon-2.3.2` 与 URP 配置。未把 VRC SDK/PhysBone SDK 作为游戏运行依赖。
- 这批角色使用已转换的便携骨骼物理；不能仅因工程有 MagicaCloth 就删除这些组件或重复添加另一套解算器。

## 同步与验证

依赖清单由源 Unity 的 `AssetDatabase.GetDependencies(..., true)` 导出，并补齐代码依赖。`Tools/SyncVrcCharacters.py` 按 GUID 找到目标已有位置后更新，保留已有 meta；新资产放到 `Assets/VRC`。逐文件来源、目标、GUID、SHA256 见 `VRC角色同步清单-20261117.json`。本机覆盖前备份放在 `Logs/VrcSync20261117/backup`，不作为跨机器必需依赖。

生成工具检查 Humanoid Avatar、Missing Script、材质/Shader、蒙皮网格与带权骨骼，以及布料碰撞引用。运行态还应在正常游戏角色选择流程检查移动、跳跃和停下后的布料表现；编译和静态引用检查不等同于视觉验收。

2026-11-17 验证：Unity 6000.2.10f1 批处理生成四个入口并完成上述引用检查，正常退出（0）。详细计数见 ../Validation/vrc-characters-20261117.json。导入期间 lilToon 报告了 Shader keyword space 断言；本次未完成渲染画面及游戏动作视觉验收，因此不宣称已验证布料动态效果。
