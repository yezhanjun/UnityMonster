# Gun and Sword Animation Pack import

Source: `D:/Project/Unity/ResAsset/AssetStoreRes/Assets/Gun and Sword Animation Pack`.
Target: `Assets/AssetStore/Gun and Sword Animation Pack`.

All 4,292 original files (including folder metadata) are retained, plus six external material/texture dependency files under `Dependencies`. Source and destination SHA-256 values are recorded in manifest.json. The import adds the pack and its `Scenes/Demo.unity` to the resource navigator and independent-preview scene routing.

The original pack contains duplicate TextMesh Pro assets. Thirty-four colliding GUIDs are remapped consistently inside the imported files. Its `TextMesh Pro/Resources` folder is named `ImportedResources` in the target, preserving the supplied files without introducing a second global Resources/TMP Settings key. Existing target TMP assets are untouched.

The source material `Materials/Materials/M_9CGMan.mat` already references a nonexistent normal map, GUID `9d763f2f26b89c74aa1b0d0cfccfa714`. The reference is preserved and documented; no replacement texture is invented.

Validation runs in a temporary standalone Unity 6000.2.10f1 project with URP 17.2.0 and UGUI 2.0.0, containing this imported pack, the existing TMP essentials, and the shared `Assets/Game/CharacterSelection/SwordAnimationPack/Model/9CG_Sword.FBX` model already tracked in this repository. `Tools/Validation/GunSwordImportValidation.cs` checks all native animation/FBX imports, loads Demo, and checks all 622 imported controllers/motions plus scene scripts, prefabs, materials and shader references. The original Demo is a static model display without an assigned animation controller. See verification.json for the actual result. This is not exhaustive animation, render or Play-mode acceptance.
