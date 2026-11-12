# Boss and complete animation pack import

Source: `D:/Project/Unity/ResAsset/AssetStoreRes`. Destination: UnityMonster.

The complete Boss, KatanaAnimationPack, Paladin_Anim_Set and Sword and Shield Animation Pack directories, including folder metadata, were copied without pruning. MagicaCloth2 is included for the Samurai cloth components. Input System 1.14.2 is added; input handling uses Both to retain the project legacy controls.

Open `Assets/Boss/BossSkillPreview.unity` (or Tools > Boss > Open Imported Skill Preview) and press Play. The top-right switch selects Darklord or Cyber Samurai; the left panel selects actions. Darklord offers SwordShield and Paladin V2 filtering. Mouse drag orbits, middle drag pans, and wheel zooms. These imported previews inspect animations and cloth; they do not implement combat damage or authored attack-range/skill-effect timelines.

All four source scenes are retained: the unified Boss scene, both individual Boss scenes, and Paladin's ScenePreview. Katana and Sword/Shield contain no additional .unity scenes in the source directories.

The project preloader and editor play-start routing allow these four scenes to run independently. Existing extracted-monster work remains outside this import commit.

## Verification

See copy-report.json for full-directory counts and source dependency audit. Every one of the 6,921 copied source files was SHA-256 verified before Unity import. Runtime smoke results and camera captures, when produced, are alongside this document. Batch command: Unity.exe -batchmode -projectPath D:/Project/Unity/UnityMonster -executeMethod BossPreview.Editor.BossImportValidation.Validate -logFile Logs/boss-import-validation.log.

## Existing source limitation

`Assets/Paladin_Anim_Set/AnimatorContrl/AnimatorContrl_V2/jump02AllZ1.controller` already points to missing GUID `b2331fded68c318448de180a1e2b1dd8` in the source project. The complete pack is preserved as supplied. This old controller's missing motion is not claimed as validated. The Boss viewer reads available clips directly.
