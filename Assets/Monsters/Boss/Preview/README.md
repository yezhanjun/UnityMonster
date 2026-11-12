# 怪物技能动作统一预览

打开 `Assets/Monsters/Boss/BossSkillPreview.unity`，进入 Play Mode。右上角切换 Darklord 与 Cyber Samurai，左侧选择当前角色的动作。

- Darklord：原 Sword and Shield 的 362 个动作，加上 Paladin_Anim_Set 的 102 个 Humanoid V2 动作，共 464 个。左侧 All / SwordShield / Paladin V2 按来源筛选，支持搜索、暂停、速度调整、时间拖动和自动轮播，常用剑盾动作在列表前面。
- Cyber Samurai：已配置的 11 个 Katana 动作，含五套 Version 1 连招、收刀/拔刀及走跑，保留上衣和左右裤腿三份 Magica Cloth 模拟。首次选择时等待布料构建完成。
- 左键/右键拖动旋转镜头，中键平移，滚轮缩放；Reset view 恢复对应角色的初始视角。左侧操作面板和右上角色切换区域会屏蔽镜头输入。
- 切换角色会停用另一名角色，释放其临时播放图，暂停其动画和布料；切回武士时重置布料。相机按不同角色的尺寸恢复视角。

这是技能动作查看场景，复用各角色已有的模型、Avatar、材质和动作配置。角色原有的独立预览场景仍可使用。

生成菜单：`Tools > Boss > Build Unified Skill Preview`。它读取两个角色的独立预览场景，重新生成 `Assets/Monsters/Boss/BossSkillPreview.unity`；编辑生成场景前请按需另存副本。

编辑器内每次启用预览时直接读取动作资源，避免已打开场景保留旧的 362 项列表，造成 Paladin 筛选为空。`Tools > Darklord > Refresh Preview Animation Lists` 可将列表同步保存到统一场景、Darklord 独立场景及 SampleScene。Paladin 仅使用 Humanoid_V2 文件夹；V1 的 73 项导入设置已恢复原样。

Paladin V2 验证：在独立 Unity Play Mode 中，将场景内存列表刻意截断为旧的 362 项，启用后自动恢复为 464 项。对 102 个 V2 动作逐一使用 Darklord 原 Avatar 采样两个时间点，检查头、手、脚坐标有效，全部右手位置发生变化，三个来源筛选数量正确且未记录运行错误。报告 `_tools/paladin-preview/validation.json`。这不代表逐动作的装甲穿插与武器握持已完成美术验收。

验证：Unity 6000.2.10f1 独立验证工程中，往返切换六次，检查当前角色独占启用、相机恢复、两名角色攻击动作实际运动、三份布料构建、非当前角色播放图释放及角色按钮区域屏蔽镜头输入，均通过且未记录运行错误。记录位于 `_tools/boss-preview/validation.json`。
