# SGV2 霰弹枪与 Albatross 背甲交接（2026-11-19）

本次仅完成原始资产导入、跨工程同步及独立游戏预制体准备。没有把霰弹枪或背甲挂到 Wendy，也没有改变现有角色默认配置。背甲后续开发在另一台电脑进行。

## 资源位置

| 工程 | 作者原始资源根目录 |
|---|---|
| VRCProject | `Assets/ImportedEquipment/SGV2Albatross_20261119/` |
| UnityMonster | `Assets/VRC/ImportedEquipment/SGV2Albatross_20261119/` |

上述根目录下的作者预制体：

- `Albatross/Albatross ver1.1/Albatross2 1.prefab`：背甲。
- `SGV2/SGV2/SGV2 M.prefab`：霰弹枪 M。
- `SGV2/SGV2/SGv2s.prefab`：霰弹枪 S。
- `SGV2/SGV2/SGv2Ssoff.prefab`：作者 S-soff 版本。
- `SGV2/SGV2 Msoff L.prefab`：作者 M-soff 版本。

不根据 M/S/soff 文件名推断具体功能差异，选择时检查作者模型、动画与效果。

UnityMonster 开发优先引用 `Assets/VRC/Equipment/SGV2Albatross_20261119/` 下同名预制体。这些是完全解包后的独立游戏副本，移除了仅用于 VRChat 装配的 Modular Avatar 组件或对应缺失脚本，保留模型、材质、骨架、Animator 与音效。作者原件未裁剪。材质依赖工程已有 lilToon 2.3.2，分别沿用 VRC 的 BRP 和 UnityMonster 的 URP 环境；没有改为 Unity Standard 材质。

## 背甲后续开发，先读这里

1. 当前没有绑定到角色。作者根节点位置约 `(-0.224575, 1.037514, -0.277896)`，旋转约 `(57.952, 0.693, -90)`，缩放约 `6.63`，这是作者原始装配数据，**不能直接当成 Wendy 通用挂点**。按实际选用角色及其体型拟合，保留角色身体比例，不要覆盖原始角色或作者预制体。
2. 使用角色 UpperChest/Chest 下独立背甲挂点。将作者预制体作为子节点，外层挂点负责穿戴偏移，内层骨架负责展开动画；不要随意改名内部骨骼，否则动画路径可能失效。
3. 展开控制器位于资源根的 `Albatross/테스트모델/Albatross/Albatross2.controller`。两个参数均为 Bool：`AT on`、`AT deploy`。动画为 `AT on.anim`、`AT off.anim`、`AT deploy.anim`、`AT deploy 1.anim`。保留作者状态机，但游戏输入、装备状态及启停逻辑尚未接入。
4. 原包使用 Modular Avatar Menu Installer、Parameters、Merge Animator、Bone Proxy 和 Menu Item。它们用于 VRChat 构建，**不是 UnityMonster 运行时装备系统**；不要为了使用背甲给游戏工程安装 VRC SDK。两份 VRC 菜单 ScriptableObject 仅保存在作者资源目录，不作为游戏引用入口。
5. 注意 `AT off.anim` 会动画控制根节点的激活状态及位置。运行时如果直接关闭 Animator 所在对象，可能无法靠 Bool 自行重新开启；接入时由常驻装备管理器负责启用，并检查根节点曲线，必要时复制动画后调整。不要直接修改作者原动画。
6. 音效在同级 `sound/`：`AT deploy.mp3`、`AT on f.mp3`、`AT on 2.mp3`、`SGready.mp3`。音量、音效触发和游戏音频路由尚未接入。
7. 验收重点：背甲与现有鬼人武士刀背鞘、长发及双机械臂避让；待机、跑步、攻击、收掏刀、折叠/展开过程都要检查。当前未做角色挂载与这些动作的视觉验收。

## 霰弹枪后续范围

作者 `SGv2s.controller` 没有参数，保留 `SGV2S.anim` 与 `SGV2 ON.mp3`。后续需手部挂点拟合、枪口/弹壳节点定义、射击/换弹动作、伤害与特效配置；本次没有实现枪械玩法。

## 验证与来源

原文件夹：`D:/本子下载/霰弹和背甲/SGV2`、`D:/本子下载/霰弹和背甲/albatross_V1.1`。导入全部 5 个 unitypackage，合计 44 个不同 GUID，跨包共有资源合并复用，保留原 meta/GUID。完整包哈希及原路径到两工程路径映射见 `import-manifest.json`。

- VRC 证据：`Doc/04-验证与证据/SGV2Albatross-20261119/`。
- UnityMonster 证据：`Docs/Validation/SGV2Albatross-20261119/`。
- UnityMonster 已在 Unity 6000.2.10f1 编辑器中完成本工程检查，5 个预制体通过；背甲游戏副本移除 5 个 VRC 专用装配组件，副本缺失脚本数为 0。检查范围为资产引用与生成，未执行角色穿戴、展开动作或画面验收。
- `unity-import-review.json` 记录实际 Unity 版本、5 个预制体加载、网格/材质/Shader 引用与缺脚本检查结果。批处理不验证实际 URP 画面、Bloom 或角色穿戴效果。
- `AuthorReadme/` 保存作者原说明图片。
- 可重复导入工具：VRC 的 `Tools/ArmorPreview/ImportSgv2Albatross.py`；Unity 检查入口 `Sgv2AlbatrossImportReview.Run`。工具只复用已有 GUID，不覆盖已有资源。

另一台电脑：在 UnityMonster 执行 `git pull --ff-only` 和 `git lfs pull`，使用 Unity 6000.2.10f1 打开。先读本文件，再从游戏预制体入口继续背甲装配；VRC 原工程使用 Unity 2022.3.22f1。
