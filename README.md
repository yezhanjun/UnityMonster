# UnityMonster

巨人杀手与雨中冒险怪物的独立 Unity 白盒验收工程。使用 Unity **6000.2.10f1**、URP 17.2、C#，保留现有战斗框架、GM 图鉴和怪物资源的依赖闭包。

## 获取与启动

安装 Git LFS，然后执行：

```sh
git lfs install
git clone https://github.com/yezhanjun/UnityMonster.git
cd UnityMonster
git lfs pull
```

在 Unity Hub 中添加仓库根目录，使用 6000.2.10f1 打开。首次打开需要还原 Packages 并导入资源。打开 `Assets/Baseline/Scenes/Startup.unity`，进入 Play，选择「白盒训练场」。使用训练场的 GM 图鉴召唤怪物，关闭 GM 后点击战斗开始。已记录通过的怪物排在 GM 列表前面。

本工程只配置启动场景与白盒训练场的构建入口；正式训练场和其他玩法不属于此仓库的验收入口。

## 验收记录

打开 [实机截图验收表](Docs/Acceptance/怪物实机截图验收汇总.html)，可搜索全部 283 项并点击放大截图。

| 状态 | 数量 |
| --- | ---: |
| 已记录通过 | 91 / 283 |
| 待验证或待实现 | 191 |
| 跳过：不攻击的蝙蝠 | 1 |

以上是迁移前保留的验收记录，不表示在本次仓库迁移中重新验收了全部怪物。声音证据包含采样记录，尚不等于逐项人工听辨。已知 lilToon keyword space 渲染报错仍保留；部分怪物动作是简化实现，Bell、ClayBruiser、Grenadier 的原版动画持久化仍需后续处理。Goal 仍暂停，等待老师验收。

## 工程约定

- `Assets/`、`Packages/`、`ProjectSettings/` 是完整工程入口；保留 `.meta` 与资源 GUID。
- `Docs/Acceptance/` 保存进度与截图证据；旧来源绝对路径仅作历史溯源。HTML 的截图已内嵌，不依赖旧工程目录。
- `Library/`、`Temp/`、`Logs/`、`UserSettings/`、构建结果与 IDE 文件不提交。
- 大型二进制资源及大型 Unity 序列化资源由 Git LFS 管理。不要只下载 ZIP 后直接运行缺少 LFS 内容的工程。
- 已导入的怪物模型不需要本机安装原游戏。`Editor` 下的旧资源重导入工具仍包含原机器路径，仅用于可选的再次提取，不是启动依赖。
- 当前仓库独立维护，不自动回写原 TPSBattle/TPSShoot 主工程。

## 工程检查

可用 Unity 批处理执行 `UnityMonsterProjectValidation.Run` 检查脚本编译后的场景与资源引用，输出 `Logs/project-validation.json`。它不替代怪物移动、攻击、声音及画面的逐项验收。
