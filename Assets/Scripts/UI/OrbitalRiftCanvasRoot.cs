using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace OrbitalRift.UI
{
    /// <summary>
    /// Persistent uGUI composition root. It creates only missing objects and never resets an existing
    /// RectTransform, so designers can rearrange the generated hierarchy directly in the editor.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler))]
    public sealed class OrbitalRiftCanvasRoot : MonoBehaviour
    {
        [Header("Generated once, editable afterwards")]
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private UiScreenManager screenManager;
        [SerializeField] private ExpeditionHudView expeditionHud;
        [SerializeField] private PauseOverlayView pauseOverlay;

        public bool ExpeditionHudAvailable => expeditionHud != null && expeditionHud.IsReady;
        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action ExitRequested;

        private void OnEnable()
        {
            EnsureStructure();
            if (!Application.isPlaying) ShowEditorPreview();
        }

        private void Awake()
        {
            EnsureStructure();
            if (Application.isPlaying) screenManager.ShowOnly(UiScreenId.None);
        }

        private void OnValidate()
        {
            ConfigureCanvas();
        }

        [ContextMenu("Ensure UI Hierarchy")]
        public void EnsureStructure()
        {
            ConfigureCanvas();
            screenManager = GetOrAdd<UiScreenManager>(gameObject);
            safeArea = EnsureRect("Safe Area", transform);
            GetOrAdd<SafeAreaFitter>(safeArea.gameObject);

            var mainMenu = EnsureRect("01 Main Menu [migration pending]", safeArea);
            var settings = EnsureRect("02 Settings [migration pending]", safeArea);
            var coopLobby = EnsureRect("03 Coop Lobby [migration pending]", safeArea);
            var classicHud = EnsureRect("04 Classic HUD [migration pending]", safeArea);
            var expeditionRoot = EnsureRect("05 Expedition HUD", safeArea);
            var defenseHud = EnsureRect("06 Defense HUD [migration pending]", safeArea);
            var results = EnsureRect("07 Results [migration pending]", safeArea);
            var modalLayer = EnsureRect("90 Modal Layer", safeArea);

            expeditionHud = GetOrAdd<ExpeditionHudView>(expeditionRoot.gameObject);
            expeditionHud.EnsureBuilt();
            pauseOverlay = GetOrAdd<PauseOverlayView>(EnsureRect("Pause Overlay", modalLayer).gameObject);
            pauseOverlay.EnsureBuilt();
            pauseOverlay.PauseRequested -= ForwardPause;
            pauseOverlay.PauseRequested += ForwardPause;
            pauseOverlay.ResumeRequested -= ForwardResume;
            pauseOverlay.ResumeRequested += ForwardResume;
            pauseOverlay.ExitRequested -= ForwardExit;
            pauseOverlay.ExitRequested += ForwardExit;
            screenManager.Configure(
                (UiScreenId.MainMenu, mainMenu),
                (UiScreenId.Settings, settings),
                (UiScreenId.CoopLobby, coopLobby),
                (UiScreenId.ClassicHud, classicHud),
                (UiScreenId.ExpeditionHud, expeditionRoot),
                (UiScreenId.DefenseHud, defenseHud),
                (UiScreenId.Results, results));

            EnsureEventSystem();
        }

        public void SetExpeditionHud(ExpeditionHudModel model)
        {
            if (!ExpeditionHudAvailable || screenManager == null) EnsureStructure();
            if (model != null && model.Visible)
            {
                screenManager.ShowOnly(UiScreenId.ExpeditionHud);
                expeditionHud.Apply(model);
            }
            else
            {
                screenManager.ShowOnly(UiScreenId.None);
            }
        }

        public void SetPauseOverlay(bool gameplayVisible, bool paused, string modeLabel)
        {
            if (pauseOverlay == null) EnsureStructure();
            pauseOverlay?.Apply(gameplayVisible, paused, modeLabel);
        }

        [ContextMenu("Show Expedition HUD Editor Preview")]
        public void ShowEditorPreview()
        {
            if (!ExpeditionHudAvailable || screenManager == null) EnsureStructure();
            screenManager.ShowOnly(UiScreenId.ExpeditionHud);
            expeditionHud.ShowEditorPreview();
        }

        private void ConfigureCanvas()
        {
            var canvas = GetOrAdd<Canvas>(gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 20;

            var scaler = GetOrAdd<CanvasScaler>(gameObject);
            // Keep UI geometry on whole screen pixels. Scaling the legacy dynamic Font atlas by a
            // fractional Canvas factor made Jura noticeably blurry in a small 16:9 Game View.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

            GetOrAdd<GraphicRaycaster>(gameObject);
        }

        private void EnsureEventSystem()
        {
            var existing = FindFirstObjectByType<EventSystem>();
            if (existing != null) return;
            var eventObject = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            eventObject.transform.SetParent(transform, false);
        }

        private void ForwardPause() => PauseRequested?.Invoke();
        private void ForwardResume() => ResumeRequested?.Invoke();
        private void ForwardExit() => ExitRequested?.Invoke();

        private static RectTransform EnsureRect(string name, Transform parent)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
