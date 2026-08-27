#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OrbitalRift
{
    // Rebuilds the visual preview whenever Boot.unity is opened in the editor.
    [InitializeOnLoad]
    internal static class EditorPreviewBootstrap
    {
        static EditorPreviewBootstrap()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.delayCall += RefreshActiveScene;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += RefreshActiveScene;
        }

        private static void RefreshActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var manager = Object.FindObjectOfType<GameManager>();
            if (manager == null) return;
            manager.CreateEditorPreview();
            SceneView.RepaintAll();
        }
    }
}
#endif
