using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalRift.UI
{
    /// <summary>
    /// One reusable Canvas pause surface. Gameplay modes only provide state and react to events;
    /// the pause affordance and modal layout are owned here in one place.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PauseOverlayView : MonoBehaviour
    {
        [SerializeField] private RectTransform pauseTrigger;
        [SerializeField] private RectTransform blocker;
        [SerializeField] private RectTransform modal;

        private Button pauseButton;
        private Button resumeButton;
        private Button exitButton;
        private TMP_Text modeText;
        private TMP_FontAsset font;

        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action ExitRequested;

        private void OnEnable()
        {
            EnsureBuilt();
            if (!Application.isPlaying) Apply(false, false, string.Empty);
        }

        [ContextMenu("Rebuild Pause Overlay")]
        public void EnsureBuilt()
        {
            font = Resources.Load<TMP_FontAsset>("Fonts/Jura SDF") ?? TMP_Settings.defaultFontAsset;

            pauseTrigger = EnsurePanel("Pause Trigger", transform, new Vector2(.463f, .938f), new Vector2(.537f, .986f),
                new Color(.025f, .06f, .15f, .96f), new Color(.42f, .96f, 1f));
            pauseButton = GetOrAdd<Button>(pauseTrigger.gameObject);
            pauseButton.targetGraphic = pauseTrigger.GetComponent<Image>();
            pauseButton.onClick.RemoveListener(NotifyPauseRequested);
            pauseButton.onClick.AddListener(NotifyPauseRequested);
            SetText(EnsureText("Label", pauseTrigger, new Vector2(.05f, .04f), new Vector2(.95f, .96f), 30), "II", new Color(.82f, .93f, 1f));

            blocker = EnsureImage("Input Blocker", transform, Vector2.zero, Vector2.one, new Color(0f, .01f, .04f, .78f), true);
            modal = EnsurePanel("Pause Modal", transform, new Vector2(.25f, .355f), new Vector2(.75f, .645f),
                new Color(.012f, .026f, .08f, .98f), new Color(.76f, .42f, 1f));
            modeText = EnsureText("Mode", modal, new Vector2(.08f, .66f), new Vector2(.92f, .84f), 23);
            SetText(EnsureText("Title", modal, new Vector2(.08f, .78f), new Vector2(.92f, .96f), 34), "ПАУЗА", Color.white);

            var resumeRect = EnsurePanel("Resume", modal, new Vector2(.08f, .16f), new Vector2(.47f, .52f),
                new Color(.04f, .18f, .22f, .98f), new Color(.35f, 1f, .68f));
            resumeButton = GetOrAdd<Button>(resumeRect.gameObject);
            resumeButton.targetGraphic = resumeRect.GetComponent<Image>();
            resumeButton.onClick.RemoveListener(NotifyResumeRequested);
            resumeButton.onClick.AddListener(NotifyResumeRequested);
            SetText(EnsureText("Label", resumeRect, new Vector2(.03f, .08f), new Vector2(.97f, .92f), 22), "ПРОДОЛЖИТЬ", Color.white);

            var exitRect = EnsurePanel("Exit", modal, new Vector2(.53f, .16f), new Vector2(.92f, .52f),
                new Color(.18f, .025f, .07f, .98f), new Color(1f, .32f, .45f));
            exitButton = GetOrAdd<Button>(exitRect.gameObject);
            exitButton.targetGraphic = exitRect.GetComponent<Image>();
            exitButton.onClick.RemoveListener(NotifyExitRequested);
            exitButton.onClick.AddListener(NotifyExitRequested);
            SetText(EnsureText("Label", exitRect, new Vector2(.03f, .08f), new Vector2(.97f, .92f), 22), "ВЫЙТИ", Color.white);
        }

        public void Apply(bool gameplayVisible, bool paused, string modeLabel)
        {
            if (pauseTrigger == null || blocker == null || modal == null) EnsureBuilt();
            gameObject.SetActive(gameplayVisible);
            if (!gameplayVisible) return;
            pauseTrigger.gameObject.SetActive(!paused);
            blocker.gameObject.SetActive(paused);
            modal.gameObject.SetActive(paused);
            if (modeText != null) SetText(modeText, modeLabel, new Color(.56f, .90f, 1f));
        }

        private RectTransform EnsurePanel(string name, Transform parent, Vector2 min, Vector2 max, Color fill, Color border)
        {
            var rect = EnsureRect(name, parent, min, max);
            var image = GetOrAdd<Image>(rect.gameObject);
            image.color = fill;
            image.raycastTarget = true;
            var outline = GetOrAdd<Outline>(rect.gameObject);
            outline.effectColor = border;
            outline.effectDistance = new Vector2(2f, -2f);
            return rect;
        }

        private RectTransform EnsureImage(string name, Transform parent, Vector2 min, Vector2 max, Color color, bool raycastTarget)
        {
            var rect = EnsureRect(name, parent, min, max);
            var image = GetOrAdd<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = raycastTarget;
            return rect;
        }

        private TMP_Text EnsureText(string name, Transform parent, Vector2 min, Vector2 max, int size)
        {
            var host = EnsureRect(name, parent, min, max);
            var content = EnsureRect("SDF Text", host, Vector2.zero, Vector2.one);
            var text = GetOrAdd<TextMeshProUGUI>(content.gameObject);
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.extraPadding = true;
            text.outlineColor = new Color(0f, 0f, .02f, .9f);
            text.outlineWidth = .08f;
            return text;
        }

        private static RectTransform EnsureRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
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

        private static void SetText(TMP_Text target, string value, Color color)
        {
            if (target == null) return;
            target.text = (value ?? string.Empty).ToUpperInvariant().Replace('Ё', 'Е');
            target.color = color;
        }

        private void NotifyPauseRequested() => PauseRequested?.Invoke();
        private void NotifyResumeRequested() => ResumeRequested?.Invoke();
        private void NotifyExitRequested() => ExitRequested?.Invoke();
    }
}
