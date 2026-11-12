# lilToon 关键字空间错误验证

2026-11-12，独立工程 UnityMonster，Unity 6000.2.10f1 / D3D11。

## 问题与修复范围

此前怪物实机测试重复出现14条“State comes from an incompatible keyword space”，即使行为专项通过，整轮仍FAILED。日志显示lilToon及透明/轮廓变体的关键字空间与Hidden/ltspass_opaque、Hidden/ltspass_transparent不一致。

Unity官方UUM-122608记录了UsePass/Fallback同类回归，修复版本包括6000.2.14f1：
https://issuetracker.unity.com/issues/19628/state-comes-from-an-incompatible-keyword-space-error-when-clicking-on-a-shader
本项目表现与该记录一致，但未升级引擎验证，因此不把官方记录当作本项目根因的唯一证据。

四个实际报错wrapper（lts、lts_o、lts_trans、lts_trans_o）已将源UsePass原样展开到自身。保留原属性、标签、fallback、UV保留pass，以及源高低两套SubShader。未替换角色材质、隐藏Renderer或过滤测试错误。

Simple Lit fallback尝试曾使错误增至28，已撤回；最终仍使用原Unlit fallback。源Pass正文比较60/60匹配（Logs/verify-liltoon-pass-bodies.py）。内联增加约3746行生成代码，代价是这些副本需要随源Pass更新。

## 已取得证据

- Logs/liltoon-expanded-test.log：远程哥布林整轮MONSTER_PLAYTEST_PASSED，tested1 failed0；无关键字空间错误。原声音DSP .067377、反射/陷阱专项和停战清理通过。
- Docs/Validation/LilToonExpandedFrames：idle与cast已实际查看，角色、怪物及紫色特效正常可见；没有做全角色、多平台或构建包画面验证。
- 新进程Logs/liltoon-regression.log：LILTOON_PASS_AUDIT确认四个shader supported=True，实际FORWARD索引0，轮廓变体FORWARD_OUTLINE索引1，passCount为8/9，编译错误0；不是通过退回URP材质来消除错误。近战实机回归终态MONSTER_PLAYTEST_PASSED，tested1 failed0，实伤5、停战清理通过；signature截图已实际复核。

## 维护与限制

Tools/InlineLilToonPasses.py是幂等生成工具，已执行幂等检查（四项ALREADY_INLINED）。在Unity关闭时从工程根运行。修改lilToon设置或升级包导致shader重新生成后，需要再执行该工具；修改共享Pass后，应先用lilToon重新生成wrapper，再执行工具，以同步源代码。升级到已修复引擎后，可重新生成四个wrapper撤销此兼容处理，然后重跑实机验证。

本修复不代表所有283项目已验收。远程哥布林仍有材质一致性和音源跟随/返程语义待核，当前112通过、170待验、1跳过不变。

