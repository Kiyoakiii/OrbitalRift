using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalRift.UI
{
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class ExpeditionModeChoiceView : MonoBehaviour
    {
        public event Action<bool> Selected;
        public event Action Cancelled;
        public event Action ResumeLivingRequested;
        private Button resumeLiving;
        public bool IsOpen => gameObject.activeSelf;

        public void Show() { EnsureBuilt(); gameObject.SetActive(true); transform.SetAsLastSibling(); }
        public void Hide() => gameObject.SetActive(false);
        private void OnEnable() => EnsureBuilt();

        [ContextMenu("Build editable expedition selector")]
        public void EnsureBuilt()
        {
            var blocker = Rect("Blocker", transform, Vector2.zero, Vector2.one);
            var background = blocker.GetComponent<Image>() ?? blocker.gameObject.AddComponent<Image>();
            background.color = new Color(.009f, .017f, .043f, .99f);
            Label("Title", blocker, new Vector2(.08f,.78f), new Vector2(.92f,.88f), "ВЫБЕРИ ЭКСПЕДИЦИЮ", 38);
            Choice("Classic Expedition", blocker, .52f, "ОБЫЧНАЯ ЭКСПЕДИЦИЯ\nКомнаты, магазин и рейтинг", new Color(.05f,.09f,.12f,.35f), false);
            Choice("Living Cosmos", blocker, .30f, "ЖИВОЙ КОСМОС · ПРОТОТИП\nКарта · выбор курса · без рейтинга", new Color(.05f,.09f,.12f,.35f), true);
            var resume = Rect("Resume living cosmos", blocker, new Vector2(.10f,.145f), new Vector2(.90f,.25f));
            var resumeFill = resume.GetComponent<Image>() ?? resume.gameObject.AddComponent<Image>();
            resumeFill.color = new Color(.055f,.13f,.16f,.78f);
            resumeLiving = resume.GetComponent<Button>() ?? resume.gameObject.AddComponent<Button>();
            resumeLiving.targetGraphic = resumeFill;
            resumeLiving.onClick.RemoveListener(ResumeLiving); resumeLiving.onClick.AddListener(ResumeLiving);
            Label("Label", resume, new Vector2(.06f,.08f), new Vector2(.94f,.92f), "ПРОДОЛЖИТЬ ИМПУЛЬ · БЕЗ РЕЙТИНГА", 24);
            var back = Rect("Back", blocker, new Vector2(.30f,.045f), new Vector2(.70f,.115f));
            var fill = back.GetComponent<Image>() ?? back.gameObject.AddComponent<Image>();
            fill.color = new Color(.07f,.085f,.14f);
            var button = back.GetComponent<Button>() ?? back.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.onClick.RemoveListener(Back); button.onClick.AddListener(Back);
            Label("Label", back, new Vector2(.06f,.08f), new Vector2(.94f,.92f), "НАЗАД", 26);
            if (resumeLiving != null) resumeLiving.gameObject.SetActive(false);
        }

        public void SetLivingCheckpointAvailable(bool available)
        {
            EnsureBuilt();
            resumeLiving.gameObject.SetActive(available);
        }

        private void Choice(string name, Transform parent, float bottom, string text, Color color, bool experimental)
        {
            var rect = Rect(name, parent, new Vector2(.10f,bottom), new Vector2(.90f,bottom+.18f));
            var fill = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>(); fill.color = color;
            var button = rect.GetComponent<Button>() ?? rect.gameObject.AddComponent<Button>(); button.targetGraphic = fill;
            if (experimental) { button.onClick.RemoveListener(Living); button.onClick.AddListener(Living); }
            else { button.onClick.RemoveListener(Classic); button.onClick.AddListener(Classic); }
            Label("Label", rect, new Vector2(.06f,.10f), new Vector2(.94f,.90f), text, 30);
        }
        private void Living() { Hide(); Selected?.Invoke(true); }
        private void Classic() { Hide(); Selected?.Invoke(false); }
        private void Back() { Hide(); Cancelled?.Invoke(); }
        private void ResumeLiving() { Hide(); ResumeLivingRequested?.Invoke(); }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var found = parent.Find(name) as RectTransform;
            if (found != null) return found;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin=min; rect.anchorMax=max;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            return rect;
        }
        private static void Label(string name, Transform parent, Vector2 min, Vector2 max, string value, float size)
        {
            var rect = Rect(name,parent,min,max);
            var text = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=Resources.Load<TMP_FontAsset>("Fonts/Jura SDF") ?? TMP_Settings.defaultFontAsset;
            text.text=value; text.color=new Color(.85f,.92f,1f); text.fontSize=size;
            text.enableAutoSizing=true; text.fontSizeMin=18; text.fontSizeMax=size;
            text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false;
        }
    }
}
