using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    public static class RainAntlerTextureImport
    {
        [Serializable] sealed class Document { public MaterialSource[] materials; }
        [Serializable] sealed class MaterialSource { public Binding[] textureBindings; }
        [Serializable] sealed class Binding { public TextureSource texture; }
        [Serializable] sealed class TextureSource { public string file; public Sampling sampling; }
        [Serializable] sealed class Sampling
        {
            public int m_ColorSpace, m_MipCount, m_Width, m_Height;
            public Settings m_TextureSettings;
        }
        [Serializable] sealed class Settings
        {
            public int m_FilterMode, m_Aniso, m_WrapU, m_WrapV, m_WrapW;
            public float m_MipBias;
        }

        // Preserve decoded source channels. Shader-specific normal decoding is separate work.
        public static void ImportAndValidate()
        {
            var source = JsonUtility.FromJson<Document>(File.ReadAllText("Docs/FrontEnd/antlers-particle-material-sources.json"));
            var visited = new HashSet<string>();
            foreach (var material in source.materials)
            foreach (var binding in material.textureBindings)
            {
                var texture = binding.texture;
                if (!visited.Add(texture.file)) continue;
                var sampling = texture.sampling;
                var settings = sampling.m_TextureSettings;
                Require(sampling.m_ColorSpace == 0 || sampling.m_ColorSpace == 1, "color space");
                var importer = AssetImporter.GetAtPath(texture.file) as TextureImporter;
                Require(importer, "texture importer " + texture.file);
                importer.textureType = TextureImporterType.Default;
                // Serialized TextureColorSpace is Linear=0/sRGB=1, unlike ColorSpace.Gamma=0.
                bool expectedSrgb = sampling.m_ColorSpace == 1;
                importer.sRGBTexture = expectedSrgb;
                importer.mipmapEnabled = sampling.m_MipCount > 1;
                importer.filterMode = (FilterMode)settings.m_FilterMode;
                importer.anisoLevel = settings.m_Aniso;
                importer.mipMapBias = settings.m_MipBias;
                importer.wrapModeU = (TextureWrapMode)settings.m_WrapU;
                importer.wrapModeV = (TextureWrapMode)settings.m_WrapV;
                importer.wrapModeW = (TextureWrapMode)settings.m_WrapW;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(sampling.m_Width, sampling.m_Height));
                importer.isReadable = false;
                importer.SaveAndReimport();
                var actual = AssetDatabase.LoadAssetAtPath<Texture2D>(texture.file);
                Require(actual && actual.width == sampling.m_Width && actual.height == sampling.m_Height &&
                    actual.mipmapCount == sampling.m_MipCount &&
                    actual.isDataSRGB == expectedSrgb &&
                    actual.filterMode == (FilterMode)settings.m_FilterMode &&
                    actual.wrapModeU == (TextureWrapMode)settings.m_WrapU &&
                    actual.wrapModeV == (TextureWrapMode)settings.m_WrapV &&
                    actual.wrapModeW == (TextureWrapMode)settings.m_WrapW &&
                    actual.anisoLevel == settings.m_Aniso &&
                    Mathf.Approximately(actual.mipMapBias, settings.m_MipBias), texture.file);
                Debug.Log($"RAIN_ANTLER_TEXTURE_SAMPLING_CHECKED {texture.file} sourceTextureColorSpace={sampling.m_ColorSpace} actualSrgb={actual.isDataSRGB}");
            }
            Require(visited.Count == 10, "unique textures");
            AssetDatabase.SaveAssets();
            Debug.Log("RAIN_ANTLER_TEXTURE_SAMPLING_PASSED shader_and_rendered_fidelity_pending=True");
        }

        static void Require(bool value, string detail)
        {
            if (!value) throw new InvalidOperationException("RAIN_ANTLER_TEXTURE_SAMPLING_FAILED " + detail);
        }
    }
}
