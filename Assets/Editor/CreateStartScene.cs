#if UNITY_EDITOR
using OrbitalRift;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CreateStartScene
{
    [MenuItem("Tools/Orbital Rift/Create or Repair Start Scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Orbital Rift Bootstrap");
        root.AddComponent<OrbitalRiftBootstrap>();
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");
        Selection.activeObject = root;
    }
}
#endif
