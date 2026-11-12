# 作者材质核对（2026-11-01）

模型：[Cyber Samurai](https://sketchfab.com/3d-models/cyber-samurai-26ccafaddb2745ceb56ae5cfc65bfed5)，作者 [KhoaMinh](https://sketchfab.com/duongminhkhoa231)。公开许可：CC Attribution；使用时保留作者署名、来源与许可信息。本工程修改包括重新绑定骨骼、T-Pose、拆分布料、替换动画武器与 Unity 材质转换。

核对依据是作者公开 Sketchfab Viewer 的材质参数，不是凭缩略图猜测。作者模型标记为未绑定、无动画，包含 5 个材质、21 张贴图。本地 21 张贴图的文件名和字节数均与公开元数据一致；这不是文件哈希一致性验证。

| 材质 | Base Color | Roughness | Normal | Metallic | Emission |
|---|---|---|---|---|---|
| Robe | fabric13_basecolor.jpg | fabric13_roughnessf.jpg | robe_normals.png | 0 | 关闭 |
| Robe2 | circleknitfabric_basecolor.jpg | circleknitfabric_roughness.jpg | robe_norma1ls.jpg | 0 | 关闭 |
| LowSet1 | LowSet1_Base_Color.png | LowSet1_Roughness.png | LowSet1_Normal_OpenGL.png | LowSet1_Metallic.png | LowSet1_Emissive.png × 4.4143113 |
| LowSet2 | LowSet2_Base_Color.png | LowSet2_Roughness.png | LowSet2_Normal_OpenGL.png | LowSet2_Metallic.png | LowSet2_Emissive.png × 1 |
| LowSet3 | LowSet3_Base_Color.png | LowSet3_Roughness.png | LowSet3_Normal_OpenGL.png | LowSet3_Metallic.png | 关闭 |

`robe_vcolor.jpg` 只在作者已关闭的 AlphaMask 通道中出现，不是 Robe 的底色。旧转换流程把它作为底色，现已修正。全部法线强度为 1，不翻转绿色通道；贴图缩放为 1、偏移为 0；不透明、关闭背面剔除。

Unity 使用 URP Lit：生成线性 MetallicSmoothness 图，R 为 Metallic（布料为 0），A 为 1 − Roughness。Base Color 和 Emission 使用 sRGB；法线使用 NormalMap 导入。生成的材质位于 `KatanaPreview/Materials`，由 `SamuraiKatanaSetup` 创建。

作者 LowSet1 还开启了各向异性及自定义 SpecularF0 通道，当前标准 URP Lit 未复现这些通道。Sketchfab 的环境光、曝光和后处理与 Unity 预览不同，因此不保证逐像素相同。

公开参数与核对记录保存在 `_tools/cyber-samurai/revision2/`：`sketchfab_options.json`、`material_audit.json`、`texture_size_audit.json`。作者其他渲染图：https://www.artstation.com/artwork/nYmz0K 。
