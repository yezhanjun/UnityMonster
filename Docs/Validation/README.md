# 独立工程迁移验证

Unity 6000.2.10f1，工程目录 `D:/Project/Unity/UnityMonster`。

- 已在该独立目录完成脚本编译。
- 已打开 Startup 与 TrainingWhitebox 两个场景检查，缺失脚本 0，缺失场景资源依赖 0，见 `project-validation.json`。
- 用已有通过记录的 `CHILD_BODY` 做一次迁移后运行检查；从白盒 GM 正常召唤，记录移动、攻击、伤害 12、原版攻击音效采样与回收。已人工查看该次生成的攻击截图，怪物与玩家均可见，见 `ChildFrames/CHILD_BODY-attack.png`。
- 怪物单项行为检查通过，但整体 smoke **FAILED**：14 条已知 `State comes from an incompatible keyword space` 渲染错误仍存在。不能将本次检查称为无错误完整通过。
- 声音以 source/listener DSP 峰值确认，未逐项人工听辨。
- 使用原隔离工程的 Library 缓存加速本地检查；Library 不提交。本次没有另做完全无缓存的克隆导入测试。
- 本次只验证迁移后的运行兼容性，验收统计仍为 91/283，Goal 保持暂停。

这些检查不替代全部 283 项的逐项画面与行为验收。
