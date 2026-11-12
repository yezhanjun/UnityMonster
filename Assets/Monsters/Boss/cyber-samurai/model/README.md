# Cyber Samurai —— 模型版本说明

当前初版请使用 `CyberSamurai_Humanoid_Corrected.fbx`，配套可播放角色为 `../KatanaPreview/CyberSamurai_KatanaBoss.prefab`，预览场景为 `../KatanaPreview/CyberSamurai_KatanaPreview.unity`。它使用自身 Humanoid Avatar，并已分离身体和衣服以接入 Magica Cloth 2。完整操作、验证范围与限制见 `../KATANA_SETUP.md`。

下面保留的是旧版 `CyberSamurai_Humanoid.fbx` 的历史说明，不能作为修正版验收依据。旧版骨骼位置和权重存在问题，已备份留作对照。

## 旧版记录

源资源：`../source/RoninFinalS/RoninFinalS.obj`（Blender 导出，105,029 顶点 / 208,944 三角面，5 个材质槽：`LowSet1` `LowSet2` `LowSet3` `Robe` `Robe2`，贴图在 `../textures/`）。

## 文件

| 文件 | 说明 |
| --- | --- |
| `CyberSamurai_Static.fbx` | 静态模型：单 Mesh + 5 个材质槽，贴图按文件名自动关联到 `../textures/`（BaseColor / Normal / Roughness / Metallic / Emissive）。 |
| `CyberSamurai_Humanoid.fbx` | 蒙皮模型：22 根人形骨骼 + SkinnedMeshRenderer，`Rig = Humanoid`，Avatar 已自动生成（`isValid = true`）。绑定姿势 = 模型原始雕像姿态。 |
| `CyberSamurai_preview.jpg` | 预览图（贴图渲染 / 绑定姿势 / 骨骼 / 蒙皮测试）。 |

两个 FBX 的 `.meta` 已设置好导入参数，Unity 打开工程后直接可像普通角色一样使用（无需再手动点 Rig → Humanoid）。

## 骨骼（Unity Humanoid 标准命名，22 根）

```
Hips
├─ Spine → Chest → UpperChest → Neck → Head
│           ├─ Shoulder.L → UpperArm.L → LowerArm.L → Hand.L
│           └─ Shoulder.R → UpperArm.R → LowerArm.R → Hand.R
├─ UpperLeg.L → LowerLeg.L → Foot.L → Toes.L
└─ UpperLeg.R → LowerLeg.R → Foot.R → Toes.R
```

Unity 自动映射结果：`Hips / Spine / Chest / UpperChest / Neck / Head / Left+RightShoulder / UpperArm / LowerArm / Hand / UpperLeg / LowerLeg / Foot / Toes` 共 22 个 human bone 全部命中（无手指骨）。

## 蒙皮

- 权重：到骨骼线段的距离 `1/d^6` 加权 → 取前 4 根骨 → 3 次网格邻接平滑 → 小型连通块刚性化（避免螺丝/钢片撕开）。
- 裙摆：`Hips` + 左右 `UpperLeg` 混合（越靠下腿的权重越大），整体像一整条裙子摆动。
- 刀、刀鞘、背后挂件：刚性绑到 `Hips`。
- 头盔、面罩、颈部液压件：刚性绑到 `Head`。

## 关于 T-Pose（重要）

模型是**雕像姿势**（双臂下垂、肘部约 90°、双手在腹前），不是 T-Pose。Unity 生成 Avatar 时会把这个姿势当成 T-Pose 参考，**直接播放 T-Pose 制作的人形动画时手臂角度会有偏差**。

需要精确重定向时，在 Unity 里做一次：

1. 选中 `CyberSamurai_Humanoid.fbx` → 右侧 **Rig** 页 → `Animation Type = Humanoid`（已设置）→ 点 **Configure...**
2. 在 Avatar 窗口左上 **Pose** 下拉 → **Enforce T-Pose**
3. 点 **Done** 保存（姿态数据会写进 `.meta` 的 `humanDescription`，之后不用再点）

如果不做这一步，模型外观 100% 保持原样，但手臂/肘部的动画重定向会偏。

## 已知限制

- 绑定姿势不是 T-Pose，手臂系动画的形变不如标准 T-Pose 角色自然；宽大和服袖子在极限姿势下会有拉伸。
- 裙摆是刚性跟随 Hips/双腿的近似，快速大跨步时脚可能穿出裙摆。
- 无手指骨（手部是一整块）。

## 重新生成

管线脚本在工程根目录 `_tools/cyber-samurai/`：

```
blender -b -P build_static_fbx.py                 # OBJ → 静态 FBX
blender -b -P build_rig.py -- joints_v1.json <out.fbx> <out.blend> apose distance
```

`joints_v1.json` 是骨骼关节坐标表，改完重跑即可重新生成蒙皮 FBX。
