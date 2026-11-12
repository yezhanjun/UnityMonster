# Cyber Samurai / Katana + Magica Cloth 初版

## 打开与使用

1. 打开 `KatanaPreview/CyberSamurai_KatanaPreview.unity`，进入 Play Mode。
2. 等待布料构建完成。默认收刀待机，左上角可切换 11 个动作、开关自动轮播、调整速度、开关布料和重置布料。
   在 Game 画面按钮面板之外，左键或右键拖拽可绕角色旋转，中键拖拽平移，滚轮缩放；点击 `Reset view` 恢复初始视角。镜头操作不改变角色位置或布料状态。
3. 可复用角色为 `KatanaPreview/CyberSamurai_KatanaBoss.prefab`。演示 UI 位于场景独立对象上，不在角色 prefab 内。
4. 修正版模型是 `model/CyberSamurai_Humanoid_Corrected.fbx`。它使用自身的 Humanoid Avatar，身体与衣服分别导出；旧 `CyberSamurai_Humanoid.fbx` 仍保留用于对照。

## 动画重定向

- 来源：`Assets/AssetStore/KatanaAnimationPack/Animations/Humanoid`，资源库共 538 个 `.anim`。
- 当前接入收刀待机、收刀前走/前跑、拔刀、战斗待机、五组完整 Version 1 连招及收刀过渡。攻击来源严格限定为 `02_Attack/01_Vertion_01`（资源包目录实际拼写为 Vertion）。
- Version 1 连招保留原有拔刀/收刀节奏，播放结束回到收刀待机；单独切换战斗待机时使用拔刀过渡，离开战斗待机时先收刀。
- `KatanaPreview/Animations` 中是这些动作的派生副本，原动画不修改。人体使用 Humanoid muscle 曲线，刀和刀鞘的额外 Transform 曲线重映射到新角色的手部挂点。
- 武器网格来自该动画包的 `9CG_Katana.fbx`。挂点通过两个 Avatar 的同一人形参考姿势校正骨骼轴差异。
- 武器普通 MeshRenderer 也参与材质设置：刀刃为钢材、握柄为深色非金属、护手为暖色金属、刀鞘为黑漆；不再套用同一个灰色材质。这些是本项目新建的 URP 材质，并非原作者的专用刀具贴图。
- `SamuraiWeaponSheathing` 使用从原动画采样的入鞘权重，在 LateUpdate 校正目标角色的刀/鞘相对姿态，避免手臂比例差异导致待机刀身露在刀鞘外。
- 新 FBX 移除了雕像原来固定在腰部的刀与刀鞘，避免与动画武器重复；原 OBJ 和旧 FBX 保留完整原件。
- 预览关闭 `Animator.applyRootMotion`，方便在镜头内观看；正式 Boss 的位移、转向、动作状态机、命中和取消窗口尚未接入。预览控制器不能代替战斗逻辑。
- 其余 Humanoid 动画的人体曲线可使用这个 Avatar，但武器 Transform 曲线也需要做同样的路径重映射，不能直接依赖原角色路径。

## 布料配置

- 插件复制自 `DemonHunterNewU6/Assets/ThirdParty/Plugins/MagicaCloth2`，保留插件 GUID。
- 当前工程显式依赖已有兼容版本：Burst 1.8.25、Collections 2.6.2、Mathematics 1.3.2。
- 上衣 `CyberSamurai_Robe`、左裤腿 `CyberSamurai_Trousers_L`、右裤腿 `CyberSamurai_Trousers_R` 分别使用独立 Mesh Cloth，避免内侧布面映射到另一条腿的代理网格。金属身体不参与布料模拟。
- 领口、胸肩和腰部固定，衣袖下方与裤腿可移动。裤装按左右腿分别分配大腿/小腿权重，修正裤筒中立形状并为机械腿护甲留出包覆空间；不再使用裙摆式低比例大腿跟随。固定点选择数据已保存，可在 Magica Cloth Inspector 中继续 Vertex Paint。
- 每份布料使用两段躯干、左右上下臂、左右大小腿，共 10 个胶囊碰撞体。
- 配置了网格简化、动画姿势跟随、0.07 米最大位移曲线、较低惯性、瞬移重置与启动稳定时间。三份布料合计 49,482 个渲染顶点、11,825 个模拟顶点。为避免错误跨面合并关闭了距离简化，模拟成本高于旧版；尚未做目标设备性能验收。
- `SamuraiClothBootstrap` 在三份布料全部构建完成之前保持 Animator 暂停，防止以动作中间姿势构建初始布料；使用插件自动构建回调并在销毁时退订。

## 验证范围与限制

在 Unity 6000.2.10f1 的独立验证工程中编译、生成 prefab/场景并进入 Play Mode，运行上述 11 个动画，检查 Avatar、人形骨骼、布料构建、网格有限值以及实际布料位移。通过 Input System 注入鼠标事件检查旋转、平移、缩放及 UI 区域屏蔽；另外检查连招结束回到待机和刀鞘对齐。当前报告与截图归档在 `_tools/cyber-samurai/revision3/`，验证工程仍位于 `revision2/UnityValidation`。

这是布料和重定向初版，不能视为成品美术验收：

- 原模型是弯肘、握指的雕像，衣服褶皱也是雕刻姿态。展开到 T-Pose 后，袖子的褶皱与左右轮廓仍不完全对称。
- 当前是 22 根身体人形骨骼，没有独立手指骨；手掌可重定向，手指不会按动画张合。握刀手型需要补手指骨、权重与握持校正。
- 宽裤腿在深屈膝、翻身、交叉腿等大动作中仍可能穿插或拉伸；需要按最终选用的攻击动作继续细调 Vertex Paint、碰撞胶囊与动作修正形状。
- 未配置布料自碰撞，也未完成目标设备性能分析；已测数值稳定不等于没有视觉穿插。
- 已按作者公开参数核对并配置 5 套 URP PBR 材质，包括底色、法线、金属度、粗糙度转换与发光，详见 `SOURCE_MATERIALS.md`。标准 URP Lit 未复现作者的各向异性与自定义 SpecularF0，环境和后处理也有差异。

## 重建

模型重建脚本：`_tools/cyber-samurai/revision4/build_corrected.py`，使用 Blender 后台运行。可编辑源文件为同目录 `CyberSamurai_Corrected.blend`。头部参考姿态已校正原雕像的转头/侧倾，实际播放时仍保留动画本身的头部动作。

Unity 菜单：`Tools > Cyber Samurai > Build Katana and Cloth Preview`。该操作重建生成目录内的预览控制器、派生动画、材质、武器网格、prefab 与预览场景；修改这些生成资源前请另存副本。菜单会询问是否保存当前未保存的场景。

## 裤腿连接修正与行走检查

revision4 按裆部以下的实际网格连通关系识别左右裤筒，修正越过模型中线的内侧褶皱被绑定到另一条腿的问题。张腿待机截图已检查跨腿横带消失。Walk_Loop_F_0 的实际派生动画 isHumanMotion=True、legacy=False，周期 1.066667 秒，来源为 Humanoid/03_Walk/01_Walk/01_Walk/01_Walk_F_0。检查了一周期正侧面截图及左右脚位置；膝部布料变形和足底贴地仍需精修，预览关闭 Root Motion 和足部 IK。报告及截图位于 _tools/cyber-samurai/revision4。
