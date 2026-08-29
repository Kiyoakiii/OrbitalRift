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
            if (GetComponent<MultiplayerSessionController>() == null) gameObject.AddComponent<MultiplayerSessionController>();
            manager.CreateEditorPreview();
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
#if UNITY_ANDROID
            // Keep rendering predictable on high-DPI phones. This pixel-art game
            // uses unlit 2D sprites, so MSAA only adds GPU cost here.
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
#endif
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            DontDestroyOnLoad(gameObject);
            if (GetComponent<GameManager>() == null) gameObject.AddComponent<GameManager>();
            if (GetComponent<FirebaseScoreService>() == null) gameObject.AddComponent<FirebaseScoreService>();
            if (GetComponent<MultiplayerSessionController>() == null) gameObject.AddComponent<MultiplayerSessionController>();
            if (GetComponent<CoopSimulationBridge>() == null) gameObject.AddComponent<CoopSimulationBridge>();
        }
    }
}
