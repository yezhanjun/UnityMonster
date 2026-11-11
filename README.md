# UnityMonster

巨人杀手与雨中冒险怪物的独立 Unity 白盒验收工程。使用 Unity **6000.2.10f1**、URP 17.2、C#，保留现有战斗框架、GM 图鉴和怪物资源的依赖闭包。

老师验收与新对话交接请先读 [快速验收与交接](Docs/Acceptance/快速验收与交接.md)，区分稳定功能基线、已记录通过项和本地未收尾内容。当前先验收，确认后再准备下一阶段。

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
| 已记录通过 | 103 / 283 |
| 本地在修失败（未提交） | 2 |
| 待完成验收 | 177 |
| 跳过：不攻击的蝙蝠 | 1 |

以上为累计逐项验收记录，不表示每次变更都重新验收全部怪物。最新单手和弓箭邪教徒的画面及远程追击验证见 Docs/Validation/单手与弓箭邪教徒验收.md；此前蝙蝠和爆炸甲虫记录见同目录。声音证据包含采样记录，尚不等于逐项人工听辨。已知 lilToon keyword space 渲染报错仍保留；部分怪物动作是简化实现，Bell、ClayBruiser、Grenadier 已补全采样曲线并重启复验，动作仍为白盒近似表现。Goal 已由老师恢复，继续在独立工程验收。

## 资源清单

查看 [分类资源清单与整理建议](Docs/Resources/资源清单.md)、[可搜索的逐文件清单](Docs/Resources/资源清单.html) 或 [完整 JSON](Docs/Resources/资源完整清单.json)。清单按用途与目录列出体积、资源类型、怪物资源组、包版本、重复候选及 meta 检查结果；保留现有资源路径和 GUID。

## 工程约定

- `Assets/`、`Packages/`、`ProjectSettings/` 是完整工程入口；保留 `.meta` 与资源 GUID。
- `Docs/Acceptance/` 保存进度与截图证据；旧来源绝对路径仅作历史溯源。HTML 的截图已内嵌，不依赖旧工程目录。
- `Library/`、`Temp/`、`Logs/`、`UserSettings/`、构建结果与 IDE 文件不提交。
- 大型二进制资源及大型 Unity 序列化资源由 Git LFS 管理。不要只下载 ZIP 后直接运行缺少 LFS 内容的工程。
- 已导入的怪物模型不需要本机安装原游戏。`Editor` 下的旧资源重导入工具仍包含原机器路径，仅用于可选的再次提取，不是启动依赖。
- 当前仓库独立维护，不自动回写原 TPSBattle/TPSShoot 主工程。

## 工程检查

可用 Unity 批处理执行 `UnityMonsterProjectValidation.Run` 检查脚本编译后的场景与资源引用，输出 `Logs/project-validation.json`。它不替代怪物移动、攻击、声音及画面的逐项验收。
