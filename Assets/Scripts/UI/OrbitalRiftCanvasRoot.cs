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
        [Header("Canvas reference resolution")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);
        [SerializeField, Range(0f, 1f)] private float widthHeightMatch = .5f;

        [Header("Generated once, editable afterwards")]
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private UiScreenManager screenManager;
        [SerializeField] private ExpeditionHudView expeditionHud;

        public bool ExpeditionHudAvailable => expeditionHud != null && expeditionHud.IsReady;
        public event Action ExpeditionPauseRequested;

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
            EnsureRect("90 Modal Layer [migration pending]", safeArea);

            expeditionHud = GetOrAdd<ExpeditionHudView>(expeditionRoot.gameObject);
            expeditionHud.EnsureBuilt();
            expeditionHud.PauseRequested -= ForwardExpeditionPause;
            expeditionHud.PauseRequested += ForwardExpeditionPause;
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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = widthHeightMatch;
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

        private void ForwardExpeditionPause()
        {
            ExpeditionPauseRequested?.Invoke();
        }

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
