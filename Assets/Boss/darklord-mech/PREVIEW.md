# Darklord / Sword and Shield 动作预览

直接打开 `Assets/Scenes/SampleScene.unity` 或 `SwordShieldPreview/Darklord_SwordShieldPreview.unity`，进入 Play Mode。

- 左侧共 464 个 Humanoid 动作：Sword and Shield 362 个，Paladin V2 102 个。使用 All / SwordShield / Paladin V2 筛选来源，并可按名称搜索。
- 常用动作排在前面：战斗待机、前走、前跑、四套完整连招、防御、防御受击、闪避、翻滚、受击、转身、普通待机。
- 支持暂停、播放速度、时间拖动、上一个/下一个、重新播放与自动轮播。非循环动作在预览中重复播放。
- 在左侧面板外，左键/右键拖拽旋转镜头，中键平移，滚轮缩放；Reset view 恢复初始视角。
- 预览使用现有模型、剑盾、材质和原动画。通过 Playables 播放，关闭预览中的 Root Motion 与动画事件；禁用预览组件时销毁播放图并恢复 Animator 的相关设置。
- 当前场景预览组件位于独立的 `Darklord Animation Preview Controls` 对象上。运行时创建的 PlayableGraph 不保存到模型资源。

## 头部映射修复

模型已有 Humanoid Avatar，但原先自动识别了错误的头部骨骼。修复仅调整 FBX 导入设置中的五个映射，原 FBX、骨骼层级、蒙皮和材质未重新制作：

| 人形骨骼 | 原映射 | 修正映射 |
|---|---|---|
| Head | core spine upper | head face |
| Neck | core spine lower | head neck lower |
| Jaw | head neck middle | head jaw |
| LeftEye | core arm left 2 | head eyeball left |
| RightEye | core arm right 2 | head eyeball right |

原导入设置已备份到 `_tools/darklord-preview/original-head-mapping.fbx.meta`。

## 验证

在 Unity 6000.2.10f1 独立验证工程中，确认 362 个列入列表的动作均为 Humanoid，实际播放了前 14 个常用动作并检查蒙皮顶点，未记录运行错误。通过 Input System 模拟输入检查镜头旋转、平移、缩放及面板屏蔽，并检查暂停和播放图销毁。修复后的 Head、Neck、Jaw、LeftEye、RightEye 在运行时均解析到预期骨骼。

截图与报告在 `_tools/darklord-preview/`，修复前的截图位于其 `before_head_fix/` 目录。未逐个对全部 362 个动作做美术验收；厚重装甲、握持与局部穿插需要按实际选用动作另行评估。预览没有接入战斗命中、移动控制或足部 IK。

Unity 菜单：`Tools > Darklord > Build Sword and Shield Preview` 可重建独立场景；`Add Preview to Current Scene` 为当前场景的 Darklord 实例添加预览。`Fix Avatar Head Mapping` 可重新应用上述五项映射。

`Refresh Preview Animation Lists` 同步三个预览场景的动作列表。编辑器中启用预览时会直接读取当前资源，避免已打开场景保留旧列表而导致筛选为空。Paladin 仅引用 Humanoid_V2 中的 102 个 FBX 内嵌 AnimationClip；V1 不列入预览，其此前修改的 73 项导入设置已恢复。Darklord 的 Avatar 不变。Unity Play Mode 验证旧列表自动补全、全部 102 个 V2 动作的双时间点骨骼采样及三个来源筛选通过，报告 `_tools/paladin-preview/validation.json`；尚未逐动作做穿插、握持等美术验收。
