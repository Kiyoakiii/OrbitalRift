#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace OrbitalRift.Editor
{
    /// <summary>Creates the persistent SDF asset used by the editable Canvas HUD.</summary>
    [InitializeOnLoad]
    public static class GenerateJuraSdfFont
    {
        private const string SourcePath = "Assets/Resources/Fonts/Jura.ttf";
        private const string AssetPath = "Assets/Resources/Fonts/Jura SDF.asset";
        private const string Glyphs = " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                                      "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ" +
                                      "абвгдежзийклмнопрстуфхцчшщъыьэюя.,:;!?#+-/%()[]<>=";

        static GenerateJuraSdfFont()
        {
            EditorApplication.delayCall += Ensure;
        }

        [MenuItem("Tools/Orbital Rift/UI/Generate or Repair Jura SDF Font")]
        public static void Ensure()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (existing != null)
            {
                // The full HUD alphabet is baked below. Locking the asset prevents Play Mode
                // from dirtying the font atlas with a stray glyph in the Editor.
                if (existing.atlasPopulationMode != AtlasPopulationMode.Static)
                {
                    existing.atlasPopulationMode = AtlasPopulationMode.Static;
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                }
                return;
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null)
            {
                Debug.LogError("Cannot create Jura SDF: source font is missing at " + SourcePath);
                return;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                Debug.LogError("TextMeshPro failed to create Jura SDF.");
                return;
            }

            fontAsset.name = "Jura SDF";
            AssetDatabase.CreateAsset(fontAsset, AssetPath);
            var atlases = fontAsset.atlasTextures;
            for (var i = 0; i < atlases.Length; i++)
            {
                if (atlases[i] == null) continue;
                atlases[i].name = "Jura SDF Atlas " + i;
                AssetDatabase.AddObjectToAsset(atlases[i], fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "Jura SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (!fontAsset.TryAddCharacters(Glyphs, out var missing) && !string.IsNullOrEmpty(missing))
                Debug.LogWarning("Jura SDF is missing UI glyphs: " + missing);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("Generated persistent Jura SDF font at " + AssetPath);
        }
    }
}
#endif
