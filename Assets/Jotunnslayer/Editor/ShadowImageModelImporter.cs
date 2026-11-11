using UnityEditor;

namespace Jotunnslayer
{
    internal sealed class ShadowImageModelImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Jotunnslayer/Resources/Jotunnslayer/ShadowImages/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.optimizeGameObjects = false;
            importer.preserveHierarchy = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("_nm.png", System.StringComparison.Ordinal))
                importer.textureType = TextureImporterType.NormalMap;
            else if (assetPath.EndsWith("_op.png", System.StringComparison.Ordinal) ||
                     assetPath.EndsWith("_rm2.png", System.StringComparison.Ordinal) ||
                     assetPath.EndsWith("_sm.png", System.StringComparison.Ordinal))
                importer.sRGBTexture = false;
        }
    }
}
