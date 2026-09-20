using System;
using OrbitalRift;
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
        private TMP_Text titleText;
        private TMP_FontAsset font;
        private RectTransform bossAbilityGuide;
        private TMP_Text guideHeaderText;
        private RectTransform resumeRect;
        private RectTransform exitRect;
        private readonly RectTransform[] abilityCards = new RectTransform[3];
        private readonly RectTransform[] abilityIconFrames = new RectTransform[3];
        private readonly Image[] abilityAccentBars = new Image[3];
        private readonly Image[] abilityIcons = new Image[3];
        private readonly TMP_Text[] abilityIndexes = new TMP_Text[3];
        private readonly TMP_Text[] abilityNames = new TMP_Text[3];
        private readonly TMP_Text[] abilityTimings = new TMP_Text[3];
        private readonly TMP_Text[] abilityTexts = new TMP_Text[3];
        private BossArchetype? guideArchetype;

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

            // Keep the blocker as a transparent interaction shield. Pause must not tint,
            // blur, or otherwise post-process the frozen combat field underneath it.
            blocker = EnsureImage("Input Blocker", transform, Vector2.zero, Vector2.one, new Color(0f, .01f, .04f, 0f), true);
            modal = EnsurePanel("Pause Modal", transform, new Vector2(.16f, .16f), new Vector2(.84f, .84f),
                new Color(.012f, .026f, .08f, .98f), new Color(.76f, .42f, 1f));
            modeText = EnsureText("Mode", modal, new Vector2(.08f, .75f), new Vector2(.92f, .83f), 19);
            titleText = EnsureText("Title", modal, new Vector2(.08f, .84f), new Vector2(.92f, .97f), 34);
            SetText(titleText, "ПАУЗА", Color.white);

            bossAbilityGuide = EnsurePanel("Boss Ability Guide", modal, new Vector2(.05f, .245f), new Vector2(.95f, .755f),
                new Color(.004f, .011f, .034f, .96f), new Color(.29f, .48f, .78f, .78f));
            bossAbilityGuide.GetComponent<Image>().raycastTarget = false;
            SetAnchors(bossAbilityGuide, new Vector2(.05f, .38f), new Vector2(.95f, .72f));
            BuildAbilityGuide();

            resumeRect = EnsurePanel("Resume", modal, new Vector2(.08f, .065f), new Vector2(.47f, .195f),
                new Color(.04f, .18f, .22f, .98f), new Color(.35f, 1f, .68f));
            resumeButton = GetOrAdd<Button>(resumeRect.gameObject);
            resumeButton.targetGraphic = resumeRect.GetComponent<Image>();
            resumeButton.onClick.RemoveListener(NotifyResumeRequested);
            resumeButton.onClick.AddListener(NotifyResumeRequested);
            SetText(EnsureText("Label", resumeRect, new Vector2(.03f, .08f), new Vector2(.97f, .92f), 20), "ПРОДОЛЖИТЬ", Color.white);

            exitRect = EnsurePanel("Exit", modal, new Vector2(.53f, .065f), new Vector2(.92f, .195f),
                new Color(.18f, .025f, .07f, .98f), new Color(1f, .32f, .45f));
            exitButton = GetOrAdd<Button>(exitRect.gameObject);
            exitButton.targetGraphic = exitRect.GetComponent<Image>();
            exitButton.onClick.RemoveListener(NotifyExitRequested);
            exitButton.onClick.AddListener(NotifyExitRequested);
            SetText(EnsureText("Label", exitRect, new Vector2(.03f, .08f), new Vector2(.97f, .92f), 20), "ВЫЙТИ", Color.white);
            ApplyLayout();
        }

        private void BuildAbilityGuide()
        {
            if (bossAbilityGuide == null) return;
            guideHeaderText = EnsureText("Guide Header", bossAbilityGuide, new Vector2(.04f, .87f), new Vector2(.96f, .98f), 11);
            guideHeaderText.alignment = TextAlignmentOptions.Center;
            guideHeaderText.textWrappingMode = TextWrappingModes.NoWrap;
            guideHeaderText.overflowMode = TextOverflowModes.Ellipsis;
            SetText(guideHeaderText, "СПОСОБНОСТИ БОССА  //  ТЕЛЕГРАФ · ОКНО · КОНТРПЛЕЙ", new Color(.53f, .78f, 1f));

            for (var i = 0; i < abilityCards.Length; i++)
            {
                var min = new Vector2(.018f + i * .326f, .07f);
                var max = new Vector2(.314f + i * .326f, .83f);
                abilityCards[i] = EnsurePanel("Ability Card " + i, bossAbilityGuide, min, max,
                    new Color(.014f, .024f, .065f, .98f), new Color(.25f, .42f, .65f, .66f));
                abilityCards[i].GetComponent<Image>().raycastTarget = false;

                var accentBar = EnsureImage("Accent Bar", abilityCards[i], new Vector2(.035f, .10f), new Vector2(.055f, .91f), Color.white, false);
                abilityAccentBars[i] = accentBar.GetComponent<Image>();
                abilityIndexes[i] = EnsureText("Index", abilityCards[i], new Vector2(.10f, .885f), new Vector2(.22f, .96f), 8);
                abilityIndexes[i].alignment = TextAlignmentOptions.TopLeft;
                abilityIndexes[i].textWrappingMode = TextWrappingModes.NoWrap;

                abilityNames[i] = EnsureText("Name", abilityCards[i], new Vector2(.10f, .765f), new Vector2(.90f, .88f), 10);
                abilityNames[i].alignment = TextAlignmentOptions.TopLeft;
                abilityNames[i].textWrappingMode = TextWrappingModes.NoWrap;
                abilityNames[i].overflowMode = TextOverflowModes.Ellipsis;
                abilityNames[i].lineSpacing = 0f;

                abilityIconFrames[i] = EnsurePanel("Icon Frame", abilityCards[i], new Vector2(.10f, .31f), new Vector2(.90f, .71f),
                    new Color(.02f, .035f, .09f, .98f), new Color(.40f, .58f, .82f, .8f));
                abilityIconFrames[i].GetComponent<Image>().raycastTarget = false;
                var legacyIcon = abilityCards[i].Find("Icon") as RectTransform;
                if (legacyIcon != null) legacyIcon.gameObject.SetActive(false);
                var iconRect = EnsureRect("Art", abilityIconFrames[i], new Vector2(.08f, .08f), new Vector2(.92f, .92f));
                abilityIcons[i] = GetOrAdd<Image>(iconRect.gameObject);
                abilityIcons[i].raycastTarget = false;
                abilityIcons[i].preserveAspect = true;

                abilityTimings[i] = EnsureText("Timing", abilityCards[i], new Vector2(.10f, .205f), new Vector2(.90f, .285f), 7);
                abilityTimings[i].alignment = TextAlignmentOptions.Center;
                abilityTimings[i].textWrappingMode = TextWrappingModes.NoWrap;
                abilityTimings[i].overflowMode = TextOverflowModes.Ellipsis;
                abilityTimings[i].lineSpacing = 0f;

                abilityTexts[i] = EnsureText("Description", abilityCards[i], new Vector2(.10f, .04f), new Vector2(.90f, .18f), 8);
                abilityTexts[i].alignment = TextAlignmentOptions.TopLeft;
                abilityTexts[i].textWrappingMode = TextWrappingModes.Normal;
                abilityTexts[i].overflowMode = TextOverflowModes.Ellipsis;
                abilityTexts[i].lineSpacing = -8f;
            }
            bossAbilityGuide.gameObject.SetActive(false);
        }

        // Layout is deliberately reapplied instead of trusting serialized rects.
        // The scene used to contain the first compact test layout, so it could
        // survive a script upgrade and place the pause card in the wrong region.
        private void ApplyLayout()
        {
            SetAnchors(transform as RectTransform, Vector2.zero, Vector2.one);
            SetAnchors(pauseTrigger, new Vector2(.464f, .925f), new Vector2(.536f, .986f));
            SetAnchors(blocker, Vector2.zero, Vector2.one);
            // The pause card is an inspection rail, not a modal that hides the arena.
            // Anchor it to the upper-right corner so the frozen combat field remains visible.
            SetAnchors(modal, new Vector2(1f, 1f), new Vector2(1f, 1f));
            modal.pivot = new Vector2(1f, 1f);
            var parentRect = transform as RectTransform;
            var parentWidth = parentRect != null && parentRect.rect.width > 1f ? parentRect.rect.width : 1920f;
            var parentHeight = parentRect != null && parentRect.rect.height > 1f ? parentRect.rect.height : 1080f;
            modal.sizeDelta = new Vector2(Mathf.Clamp(parentWidth * .34f, 320f, 520f),
                Mathf.Clamp(parentHeight * .29f, 250f, 310f));
            modal.anchoredPosition = new Vector2(-24f, -24f);
            SetAnchors(HostOf(titleText), new Vector2(.06f, .875f), new Vector2(.94f, .975f));
            SetAnchors(HostOf(modeText), new Vector2(.06f, .775f), new Vector2(.94f, .855f));
            SetAnchors(bossAbilityGuide, new Vector2(.055f, .245f), new Vector2(.945f, .755f));
            SetAnchors(resumeRect, new Vector2(.08f, .065f), new Vector2(.47f, .195f));
            SetAnchors(exitRect, new Vector2(.53f, .065f), new Vector2(.92f, .195f));
            SetAnchors(HostOf(guideHeaderText), new Vector2(.04f, .87f), new Vector2(.96f, .98f));

            for (var i = 0; i < abilityCards.Length; i++)
            {
                var card = abilityCards[i];
                if (card == null) continue;
                SetAnchors(card, new Vector2(.018f + i * .326f, .07f), new Vector2(.314f + i * .326f, .83f));
                SetAnchors(abilityAccentBars[i] != null ? abilityAccentBars[i].rectTransform : null, new Vector2(.035f, .10f), new Vector2(.055f, .91f));
                SetAnchors(HostOf(abilityIndexes[i]), new Vector2(.10f, .885f), new Vector2(.22f, .96f));
                SetAnchors(HostOf(abilityNames[i]), new Vector2(.10f, .765f), new Vector2(.90f, .88f));
                // The ability art is the primary visual cue during a pause. Give it
                // a real card-sized stage instead of treating it like a tiny HUD glyph.
                SetAnchors(abilityIconFrames[i], new Vector2(.10f, .31f), new Vector2(.90f, .71f));
                var art = abilityIconFrames[i] != null ? abilityIconFrames[i].Find("Art") as RectTransform : null;
                SetAnchors(art, new Vector2(.08f, .08f), new Vector2(.92f, .92f));
                SetAnchors(HostOf(abilityTimings[i]), new Vector2(.10f, .205f), new Vector2(.90f, .285f));
                SetAnchors(HostOf(abilityTexts[i]), new Vector2(.10f, .04f), new Vector2(.90f, .18f));
            }
        }

        public void SetBossAbilityGuide(BossArchetype? archetype, bool visible)
        {
            if (bossAbilityGuide == null) EnsureBuilt();
            if (bossAbilityGuide == null) return;
            var show = visible && archetype.HasValue;
            bossAbilityGuide.gameObject.SetActive(show);
            if (!show) return;
            if (guideArchetype.HasValue && guideArchetype.Value == archetype.Value) return;
            guideArchetype = archetype;
            var abilities = BossAbilityCatalog.For(archetype.Value);
            for (var i = 0; i < abilityCards.Length; i++)
            {
                var info = abilities != null && i < abilities.Length ? abilities[i] : null;
                if (info == null)
                {
                    abilityCards[i].gameObject.SetActive(false);
                    continue;
                }
                abilityCards[i].gameObject.SetActive(true);
                abilityIcons[i].sprite = LoadAbilitySprite(info.IconResource);
                abilityIcons[i].color = Color.white;
                var accent = info.Accent;
                var cardFill = Color.Lerp(new Color(.008f, .015f, .046f, .98f), accent, .075f);
                cardFill.a = .98f;
                abilityCards[i].GetComponent<Image>().color = cardFill;
                var cardOutline = GetOrAdd<Outline>(abilityCards[i].gameObject);
                cardOutline.effectColor = new Color(accent.r, accent.g, accent.b, .76f);
                cardOutline.effectDistance = new Vector2(1.5f, -1.5f);
                abilityAccentBars[i].color = accent;

                abilityIconFrames[i].GetComponent<Image>().color = new Color(accent.r * .09f, accent.g * .09f, accent.b * .09f, .98f);
                var iconOutline = GetOrAdd<Outline>(abilityIconFrames[i].gameObject);
                iconOutline.effectColor = new Color(accent.r, accent.g, accent.b, .9f);
                iconOutline.effectDistance = new Vector2(1f, -1f);

                SetText(abilityIndexes[i], "0" + (i + 1), new Color(.55f, .76f, 1f));
                SetText(abilityNames[i], info.Name, Color.white);
                SetText(abilityTimings[i], PauseTiming(info.Timing), accent);
                SetText(abilityTexts[i], PauseDescription(info), new Color(.68f, .76f, .90f));
            }
        }

        private static string PauseTiming(string value)
        {
            return (value ?? string.Empty)
                .Replace("телеграф ", "Т ")
                .Replace("полёт ", "ПОЛЕТ ")
                .Replace("перезарядка ", "КД ")
                .Replace(" · ", "  •  ");
        }

        private static string PauseDescription(BossAbilityInfo info)
        {
            switch (info.Id)
            {
                case BossAbilityId.FirebirdSolarChicks: return "Три птенца веером идут к пилоту.";
                case BossAbilityId.FirebirdAshenEgg: return "Разбей яйцо до возрождения птицы.";
                case BossAbilityId.FirebirdPhoenixDive: return "Меняет орбиту и выпускает огненное кольцо.";
                case BossAbilityId.HarrierRiftCopies: return "Копии повторяют траекторию охотника.";
                case BossAbilityId.HarrierPhaseDash: return "Рывок через орбиту отбрасывает корабль.";
                case BossAbilityId.HarrierColdFan: return "Веер осколков закрывает ближайший сектор.";
                case BossAbilityId.VoidRiftBeam: return "Луч заранее отмечает безопасный сектор.";
                case BossAbilityId.VoidGravityRoots: return "Метка фиксирует угол орбиты на секунду.";
                case BossAbilityId.VoidBarrage: return "Импульсы летят по текущему направлению.";
                default: return info.ShortDescription;
            }
        }

        private static Sprite LoadAbilitySprite(string resourcePath)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
        }

        private static RectTransform HostOf(TMP_Text text)
        {
            if (text == null || text.transform.parent == null) return null;
            // EnsureText creates a dedicated RectTransform host and places the
            // TMP component on its nested "SDF Text" child. The layout pass
            // must return that direct host; walking one level higher mutates
            // the modal/card itself and collapses it into a thin strip.
            return text.transform.parent as RectTransform;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null) return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void Apply(bool gameplayVisible, bool paused, string modeLabel)
        {
            if (pauseTrigger == null || blocker == null || modal == null || titleText == null || resumeRect == null || exitRect == null)
                EnsureBuilt();
            gameObject.SetActive(gameplayVisible);
            if (!gameplayVisible) return;
            ApplyLayout();
            // The scene may already contain an older serialized child order. Keep
            // the blocker behind the modal so pausing cannot turn the whole view
            // into an opaque dark screen with the controls hidden underneath it.
            blocker.SetAsFirstSibling();
            pauseTrigger.SetAsLastSibling();
            modal.SetAsLastSibling();
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
