using UnityEngine;
using OrbitalRift.UI;

namespace OrbitalRift
{
    /// <summary>Single scene entry point. The game is generated from primitives, so it has no borrowed art assets.</summary>
    [ExecuteAlways]
    public sealed class OrbitalRiftBootstrap : MonoBehaviour
    {
        private void OnEnable()
        {
            EnsureUiRoot();
            if (Application.isPlaying) return;
            var manager = GetComponent<GameManager>();
            if (manager == null) manager = gameObject.AddComponent<GameManager>();
            if (GetComponent<MultiplayerSessionController>() == null) gameObject.AddComponent<MultiplayerSessionController>();
            manager.CreateEditorPreview();
        }

        private void Awake()
        {
            EnsureUiRoot();
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

        [ContextMenu("Ensure Editable Canvas UI")]
        public OrbitalRiftCanvasRoot EnsureUiRoot()
        {
            const string rootName = "UI Root [Canvas]";
            var uiTransform = transform.Find(rootName);
            if (uiTransform == null)
            {
                var uiObject = new GameObject(rootName, typeof(RectTransform));
                uiTransform = uiObject.transform;
                uiTransform.SetParent(transform, false);
            }

            var uiRoot = uiTransform.GetComponent<OrbitalRiftCanvasRoot>();
            if (uiRoot == null) uiRoot = uiTransform.gameObject.AddComponent<OrbitalRiftCanvasRoot>();
            uiRoot.EnsureStructure();
            return uiRoot;
        }
    }
}
