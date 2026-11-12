# Continue UnityMonster on another PC

Repository: https://github.com/yezhanjun/UnityMonster.git, branch main. Unity version: 6000.2.10f1.

Install Git LFS before pulling/cloning. Run `git pull --ff-only` and `git lfs pull`, then open the repository root in Unity Hub. Allow package resolution and first asset import to complete.

Open the Unity menu **资源导航 → 打开分类导航**. The directory guide is `Assets/README.md`. Boss preview: `Assets/Monsters/Boss/BossSkillPreview.unity`. Extracted monster preview: `Assets/Monsters/Jotunnslayer/MonsterPreview/MonsterPreview.unity`.

This checkpoint includes the categorized assets, preview tools, and previously unpublished monster WIP/evidence so work can continue on another computer. WIP is not a claim of completed monster acceptance. The bulk goal remains paused; refer to `Docs/Acceptance/CURRENT-PROGRESS.json` for current counts and outstanding work. Historical reports retain their original paths; use `Docs/Validation/AssetLayout/migration.json` to translate those paths.

The scene previews and their assets are in this repository. Source extraction/research scripts may still require the original external source-game files, AssetStoreRes project, or local analysis tools; those external projects and Unity Library/Logs caches are not part of this checkpoint.

Known source limitation: Paladin jump02AllZ1.controller already references a missing source motion; no replacement was invented. Layout validation passed for 11,564 moved GUIDs and all 65 monster gallery entries, and the Boss runtime smoke passed. This does not validate every WIP monster or every animation pose.
