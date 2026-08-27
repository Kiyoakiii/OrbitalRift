using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Single scene entry point. The game is generated from primitives, so it has no borrowed art assets.</summary>
    [ExecuteAlways]
    public sealed class OrbitalRiftBootstrap : MonoBehaviour
    {
        private void OnEnable()
        {
            if (Application.isPlaying) return;
            var manager = GetComponent<GameManager>();
            if (manager == null) manager = gameObject.AddComponent<GameManager>();
            manager.CreateEditorPreview();
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            DontDestroyOnLoad(gameObject);
            if (GetComponent<GameManager>() == null) gameObject.AddComponent<GameManager>();
            if (GetComponent<FirebaseScoreService>() == null) gameObject.AddComponent<FirebaseScoreService>();
        }
    }
}
