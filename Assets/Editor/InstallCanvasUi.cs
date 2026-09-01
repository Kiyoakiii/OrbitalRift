#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OrbitalRift.Editor
{
    /// <summary>Materialises the editable Canvas hierarchy in Boot.unity exactly once.</summary>
    public static class InstallCanvasUi
    {
        private const string ScenePath = "Assets/Scenes/Boot.unity";

        [MenuItem("Tools/Orbital Rift/UI/Install or Repair Editable Canvas")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var bootstrap = Object.FindFirstObjectByType<OrbitalRiftBootstrap>();
            if (bootstrap == null)
            {
                var root = new GameObject("Orbital Rift Bootstrap");
                bootstrap = root.AddComponent<OrbitalRiftBootstrap>();
            }

            bootstrap.gameObject.name = "Orbital Rift Bootstrap";
            bootstrap.EnsureUiRoot();
            RemoveGeneratedWorldPreview();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Orbital Rift editable Canvas installed in " + ScenePath);
        }

        private static void RemoveGeneratedWorldPreview()
        {
            var generatedNames = new[]
            {
                "Editor Preview Camera", "Editor Preview Background", "Arena (Editor Preview)",
                "Main Camera", "Deep space background"
            };
            for (var i = 0; i < generatedNames.Length; i++)
            {
                var generated = GameObject.Find(generatedNames[i]);
                if (generated != null) Object.DestroyImmediate(generated);
            }
        }
    }
}
#endif
