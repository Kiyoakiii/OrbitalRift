using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace OrbitalRift.UI
{
    /// <summary>
    /// Authored uGUI view for Solo Expedition. Every named child is a normal RectTransform,
    /// so layout can be changed in Scene view without touching gameplay code.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ExpeditionHudView : MonoBehaviour
    {
        [Header("Theme")]
        [SerializeField] private Color panelColor = new Color(.008f, .020f, .065f, .88f);
        [SerializeField] private Color cyan = new Color(.42f, .96f, 1f, 1f);
        [SerializeField] private Color pale = new Color(.82f, .93f, 1f, 1f);
        [SerializeField] private Color emptySegment = new Color(.06f, .09f, .16f, .76f);

        [Header("Crisp responsive typography")]
        [SerializeField] private Vector2 portraitTypographyReference = new Vector2(1080f, 1920f);
        [SerializeField] private Vector2 landscapeTypographyReference = new Vector2(1920f, 1080f);
        [SerializeField, Range(.4f, 1f)] private float minimumTypographyScale = .58f;
        [SerializeField, Range(1f, 1.5f)] private float portraitTypographyBoost = 1.18f;

        [Header("Editable layout objects")]
        [SerializeField] private RectTransform headerPanel;
        [SerializeField] private RectTransform sectorMapPanel;
        [SerializeField] private RectTransform objectiveStrip;
        [SerializeField] private RectTransform tickerStrip;
        [SerializeField] private RectTransform trajectoryStrip;
        [SerializeField] private RectTransform threatBar;
        [SerializeField] private RectTransform hullBar;
        [SerializeField] private RectTransform introPanel;
        [SerializeField] private RectTransform pauseButtonRect;

        private TMP_Text roomText;
        private TMP_Text runText;
        private TMP_Text objectiveText;
        private TMP_Text tickerText;
        private TMP_Text trajectoryText;
        private TMP_Text threatTitleText;
        private TMP_Text threatValueText;
        private TMP_Text hullTitleText;
        private TMP_Text hullValueText;
        private TMP_Text introTitleText;
        private TMP_Text introSubtitleText;
        private RectTransform roomRail;
        private RectTransform roomNodes;
        private RectTransform threatSegments;
        private RectTransform hullSegments;
        private CanvasGroup introGroup;
        private Button pauseButton;
        private TMP_FontAsset uiFont;
        private static TMP_FontAsset generatedJuraSdf;
        private readonly List<Image> roomNodeImages = new List<Image>(24);
        private readonly List<Image> threatSegmentImages = new List<Image>(12);
        private readonly List<Image> hullSegmentImages = new List<Image>(12);
        private readonly Dictionary<TMP_Text, int> baseFontSizes = new Dictionary<TMP_Text, int>(16);
        private int lastTypographyWidth = -1;
        private int lastTypographyHeight = -1;

        public bool IsReady => headerPanel != null && roomText != null && threatSegments != null;
        public event Action PauseRequested;

        private void OnEnable()
        {
            EnsureBuilt();
            // The root and this child are enabled in separate Unity callbacks. Re-apply the sample
            // after this view has rebuilt its Graphics so the editable preview cannot fall back to
            // the serialized empty-bar colors.
            if (!Application.isPlaying) ShowEditorPreview();
        }

        private void LateUpdate()
        {
            if (Screen.width == lastTypographyWidth && Screen.height == lastTypographyHeight) return;
            ApplyResponsiveTypography();
        }

        [ContextMenu("Rebuild Missing HUD Objects")]
        public void EnsureBuilt()
        {
            uiFont = LoadJuraSdf();

            headerPanel = EnsurePanel("01 Header", transform, new Vector2(.035f, .934f), new Vector2(.965f, .986f), panelColor, cyan);
            roomText = EnsureText("Room", headerPanel, new Vector2(.018f, .08f), new Vector2(.46f, .92f), TextAnchor.MiddleLeft, 28);
            runText = EnsureText("Run", headerPanel, new Vector2(.54f, .08f), new Vector2(.982f, .92f), TextAnchor.MiddleRight, 25);

            sectorMapPanel = EnsurePanel("02 Sector Map", transform, new Vector2(.16f, .888f), new Vector2(.84f, .922f),
                new Color(.018f, .055f, .13f, .92f), new Color(.28f, .75f, 1f, .9f));
            roomRail = EnsureImageRect("Progress Rail", sectorMapPanel, new Vector2(.055f, .43f), new Vector2(.945f, .57f),
                new Color(.16f, .30f, .48f, .82f));
            roomNodes = EnsureRect("Room Nodes", sectorMapPanel, Vector2.zero, Vector2.one);

            objectiveStrip = EnsureRect("03 Objective", transform, new Vector2(.11f, .853f), new Vector2(.89f, .878f));
            objectiveText = EnsureText("Label", objectiveStrip, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, 24);
            tickerStrip = EnsureRect("04 Event Ticker", transform, new Vector2(.12f, .819f), new Vector2(.88f, .850f));
            tickerText = EnsureText("Label", tickerStrip, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, 25);
            trajectoryStrip = EnsureRect("05 Trajectory", transform, new Vector2(.14f, .773f), new Vector2(.86f, .802f));
            trajectoryText = EnsureText("Label", trajectoryStrip, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, 25);

            pauseButtonRect = EnsurePanel("Pause Button", transform, new Vector2(.466f, .946f), new Vector2(.534f, .986f),
                new Color(.025f, .06f, .15f, .96f), cyan);
            var pauseImage = pauseButtonRect.GetComponent<Image>();
            pauseImage.raycastTarget = true;
            pauseButton = GetOrAdd<Button>(pauseButtonRect.gameObject);
            pauseButton.targetGraphic = pauseImage;
            pauseButton.transition = Selectable.Transition.ColorTint;
            pauseButton.onClick.RemoveListener(NotifyPauseRequested);
            pauseButton.onClick.AddListener(NotifyPauseRequested);
            SetText(EnsureText("Label", pauseButtonRect, new Vector2(.05f, .05f), new Vector2(.95f, .95f),
                TextAnchor.MiddleCenter, 25), "II", pale);

            threatBar = EnsurePanel("06 Threat Bar", transform, new Vector2(.018f, .405f), new Vector2(.056f, .795f),
                new Color(.018f, .028f, .075f, .92f), cyan);
            threatSegments = EnsureRect("Segments", threatBar, new Vector2(.14f, .03f), new Vector2(.86f, .97f));
            threatTitleText = EnsureText("Title", transform, new Vector2(0f, .808f), new Vector2(.105f, .838f), TextAnchor.MiddleCenter, 22);
            threatValueText = EnsureText("Value", transform, new Vector2(0f, .367f), new Vector2(.105f, .397f), TextAnchor.MiddleCenter, 22);

            hullBar = EnsurePanel("07 Hull Bar", transform, new Vector2(.944f, .405f), new Vector2(.982f, .795f),
                new Color(.018f, .028f, .075f, .92f), new Color(.34f, 1f, .68f));
            hullSegments = EnsureRect("Segments", hullBar, new Vector2(.14f, .03f), new Vector2(.86f, .97f));
            hullTitleText = EnsureText("Hull Title", transform, new Vector2(.895f, .808f), new Vector2(1f, .838f), TextAnchor.MiddleCenter, 22);
            hullValueText = EnsureText("Hull Value", transform, new Vector2(.895f, .367f), new Vector2(1f, .397f), TextAnchor.MiddleCenter, 22);

            introPanel = EnsurePanel("08 Room Intro", transform, new Vector2(.17f, .810f), new Vector2(.83f, .880f),
                new Color(.012f, .026f, .075f, .92f), cyan);
            introGroup = GetOrAdd<CanvasGroup>(introPanel.gameObject);
            introTitleText = EnsureText("Title", introPanel, new Vector2(.02f, .50f), new Vector2(.98f, .94f), TextAnchor.MiddleCenter, 27);
            introSubtitleText = EnsureText("Subtitle", introPanel, new Vector2(.02f, .08f), new Vector2(.98f, .48f), TextAnchor.MiddleCenter, 23);

            EnsureSegmentCount(threatSegments, threatSegmentImages, 12, "Threat Segment");
            EnsureSegmentCount(hullSegments, hullSegmentImages, 12, "Hull Segment");
            SetText(hullTitleText, "КОРПУС", new Color(.34f, 1f, .68f));
            ApplyResponsiveTypography();
        }

        public void Apply(ExpeditionHudModel model)
        {
            if (!IsReady) EnsureBuilt();
            gameObject.SetActive(model != null && model.Visible);
            if (model == null || !model.Visible) return;

            SetText(roomText, model.RoomLabel, model.ThreatColor);
            SetText(runText, model.RunLabel, new Color(.35f, 1f, .68f));
            SetText(objectiveText, model.ObjectiveLabel, pale);
            SetText(tickerText, model.TickerLabel, model.TickerColor);
            SetText(trajectoryText, model.TrajectoryLabel, model.TrajectoryColor);
            tickerStrip.gameObject.SetActive(!string.IsNullOrWhiteSpace(model.TickerLabel) && model.IntroAlpha <= .001f);
            objectiveStrip.gameObject.SetActive(model.IntroAlpha <= .001f);

            SetText(threatTitleText, model.ThreatTitle, model.ThreatColor);
            SetText(threatValueText, model.ThreatValue, model.ThreatColor);
            SetText(hullTitleText, "КОРПУС", model.HullColor);
            SetText(hullValueText, model.HullValue, model.HullColor);
            SetPanelBorder(threatBar, model.ThreatColor);
            SetPanelBorder(hullBar, model.HullColor);
            UpdateSegments(threatSegmentImages, model.ThreatSegments, model.TotalHealthSegments,
                model.ThreatColor, model.ThreatEmptyColor);
            UpdateSegments(hullSegmentImages, model.HullSegments, model.TotalHealthSegments,
                model.HullColor, model.HullEmptyColor);
            UpdateRoomMap(model.Rooms);

            introGroup.alpha = Mathf.Clamp01(model.IntroAlpha);
            introGroup.gameObject.SetActive(model.IntroAlpha > .001f);
            SetPanelBorder(introPanel, model.IntroColor);
            SetText(introTitleText, model.IntroTitle, model.IntroColor);
            SetText(introSubtitleText, model.IntroSubtitle, Color.white);
        }

        public void ShowEditorPreview()
        {
            if (!IsReady) EnsureBuilt();
            var previewRooms = new ExpeditionRoomNodeModel[14];
            var roomColors = new[]
            {
                new Color(.38f, 1f, .72f), new Color(1f, .56f, .24f), new Color(.92f, .32f, .72f),
                new Color(.46f, .82f, 1f), new Color(.68f, .48f, 1f), new Color(1f, .32f, .45f)
            };
            for (var i = 0; i < previewRooms.Length; i++)
                previewRooms[i] = new ExpeditionRoomNodeModel(roomColors[i % roomColors.Length], i < 2, i == 2);
            Apply(new ExpeditionHudModel
            {
                Visible = true,
                RoomLabel = "УЗЕЛ 03/14 // БОСС",
                RunLabel = "SOLO // EDITOR PREVIEW",
                ObjectiveLabel = "УНИЧТОЖИТЬ УГРОЗУ // +120 · ОГОНЬ",
                TrajectoryLabel = "ТРАЕКТОРИЯ // ВОСЬМЕРКА // СМЕНА 8 СЕК",
                TickerLabel = "HUD МОЖНО ДВИГАТЬ МЫШКОЙ В SCENE VIEW",
                ThreatTitle = "БОСС",
                ThreatValue = "3/12",
                HullValue = "4/5",
                ThreatColor = new Color(1f, .14f, .22f),
                ThreatEmptyColor = new Color(.18f, .03f, .045f, .92f),
                HullColor = new Color(.34f, 1f, .68f),
                HullEmptyColor = new Color(.18f, .03f, .045f, .92f),
                TrajectoryColor = new Color(.46f, .82f, 1f),
                TickerColor = new Color(.62f, 1f, .78f),
                ThreatSegments = 3,
                HullSegments = 10,
                TotalHealthSegments = 12,
                Rooms = previewRooms
            });
        }

        private void UpdateRoomMap(ExpeditionRoomNodeModel[] rooms)
        {
            rooms ??= System.Array.Empty<ExpeditionRoomNodeModel>();
            while (roomNodeImages.Count < rooms.Length)
            {
                var index = roomNodeImages.Count;
                var node = EnsureImageRect("Room " + (index + 1).ToString("00"), roomNodes, Vector2.zero, Vector2.one, Color.white);
                var outline = GetOrAdd<Outline>(node.gameObject);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                outline.useGraphicAlpha = false;
                roomNodeImages.Add(node.GetComponent<Image>());
            }

            var count = Mathf.Max(1, rooms.Length);
            for (var i = 0; i < roomNodeImages.Count; i++)
            {
                var image = roomNodeImages[i];
                var active = i < rooms.Length;
                image.gameObject.SetActive(active);
                if (!active) continue;
                var step = .88f / Mathf.Max(1, count - 1);
                var centre = count == 1 ? .5f : .06f + i * step;
                var size = rooms[i].IsActive ? .034f : .024f;
                var rect = (RectTransform)image.transform;
                rect.anchorMin = new Vector2(centre - size * .5f, .5f - size * 2.2f);
                rect.anchorMax = new Vector2(centre + size * .5f, .5f + size * 2.2f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var color = rooms[i].Color;
                image.color = rooms[i].IsVisited || rooms[i].IsActive
                    ? color
                    : new Color(color.r * .34f, color.g * .38f, color.b * .44f, .82f);
                var outline = image.GetComponent<Outline>();
                if (outline != null) outline.effectColor = rooms[i].IsActive ? Color.white : color;
            }
        }

        private void EnsureSegmentCount(RectTransform root, List<Image> cache, int count, string prefix)
        {
            if (root == null) return;
            cache.Clear();
            for (var i = 0; i < count; i++)
            {
                var name = prefix + " " + (i + 1).ToString("00");
                var minimumY = i / (float)count + .012f;
                var maximumY = (i + 1f) / count - .012f;
                var rect = EnsureImageRect(name, root, new Vector2(0f, minimumY), new Vector2(1f, maximumY), emptySegment);
                cache.Add(rect.GetComponent<Image>());
            }
        }

        private void UpdateSegments(List<Image> segments, int filled, int total, Color color, Color depletedColor)
        {
            total = Mathf.Clamp(total, 1, segments.Count);
            filled = Mathf.Clamp(filled, 0, total);
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                segment.gameObject.SetActive(i < total);
                if (i >= total) continue;
                segment.enabled = true;
                segment.color = i < filled ? color : depletedColor;
                // ExecuteAlways previews can otherwise retain the old vertex colors until a layout
                // change. Forcing the graphic dirty makes the Scene/Game preview match runtime.
                segment.SetAllDirty();
            }
        }

        private TMP_Text EnsureText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            TextAnchor alignment, int fontSize)
        {
            var rect = EnsureRect(name, parent, anchorMin, anchorMax);
            // Keep the old component disabled for a non-destructive scene migration. The SDF
            // component is now the sole renderer and remains editable in the normal Inspector.
            var legacyText = rect.GetComponent<Text>();
            if (legacyText != null) legacyText.enabled = false;
            // Unity permits only one Graphic on a GameObject, even when the old Text is disabled.
            // Put TMP on a full-stretch child so existing scene references remain intact.
            var sdfRect = EnsureRect("SDF Text", rect, Vector2.zero, Vector2.one);
            var text = GetOrAdd<TextMeshProUGUI>(sdfRect.gameObject);
            text.font = uiFont;
            baseFontSizes[text] = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = ToTmpAlignment(alignment);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = false;
            text.richText = false;
            text.extraPadding = true;
            text.lineSpacing = 1f;
            text.raycastTarget = false;
            text.outlineColor = new Color32(0, 0, 5, 220);
            text.outlineWidth = .08f;
            var legacyOutline = rect.GetComponent<Outline>();
            if (legacyOutline != null) legacyOutline.enabled = false;
            return text;
        }

        private void ApplyResponsiveTypography()
        {
            lastTypographyWidth = Mathf.Max(1, Screen.width);
            lastTypographyHeight = Mathf.Max(1, Screen.height);
            var portrait = lastTypographyHeight > lastTypographyWidth;
            var reference = portrait ? portraitTypographyReference : landscapeTypographyReference;
            var scale = Mathf.Min(lastTypographyWidth / Mathf.Max(1f, reference.x),
                lastTypographyHeight / Mathf.Max(1f, reference.y));
            scale = Mathf.Clamp(scale, minimumTypographyScale, 1.15f);
            if (portrait) scale *= portraitTypographyBoost;

            foreach (var entry in baseFontSizes)
            {
                if (entry.Key == null) continue;
                // Integer point sizes keep Unity's dynamic font atlas pixel-aligned.
                entry.Key.fontSize = Mathf.Max(11, Mathf.RoundToInt(entry.Value * scale));
                entry.Key.SetAllDirty();
            }
        }

        private static TMP_FontAsset LoadJuraSdf()
        {
            var savedAsset = Resources.Load<TMP_FontAsset>("Fonts/Jura SDF");
            if (savedAsset != null) return savedAsset;
            if (generatedJuraSdf != null) return generatedJuraSdf;

            var source = Resources.Load<Font>("Fonts/Jura");
            if (source == null) source = Resources.GetBuiltinResource<Font>("Arial.ttf");
            generatedJuraSdf = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (generatedJuraSdf == null) return null;
            generatedJuraSdf.name = "Jura SDF (Runtime)";
            generatedJuraSdf.hideFlags = HideFlags.DontSave;
            const string glyphs = " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                                  "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ" +
                                  "абвгдежзийклмнопрстуфхцчшщъыьэюя.,:;!?#+-/%()[]<>=";
            if (!generatedJuraSdf.TryAddCharacters(glyphs, out var missing) && !string.IsNullOrEmpty(missing))
                Debug.LogWarning("Jura SDF is missing UI glyphs: " + missing);
            return generatedJuraSdf;
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor alignment)
        {
            return alignment switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
        }

        private RectTransform EnsurePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Color fill, Color border)
        {
            var rect = EnsureRect(name, parent, anchorMin, anchorMax);
            var image = GetOrAdd<Image>(rect.gameObject);
            image.color = fill;
            image.raycastTarget = false;
            var outline = GetOrAdd<Outline>(rect.gameObject);
            outline.effectColor = border;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            return rect;
        }

        private RectTransform EnsureImageRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rect = EnsureRect(name, parent, anchorMin, anchorMax);
            var image = GetOrAdd<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform EnsureRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
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

        private static void SetPanelBorder(RectTransform panel, Color color)
        {
            if (panel == null) return;
            var outline = panel.GetComponent<Outline>();
            if (outline != null) outline.effectColor = color;
        }

        private void NotifyPauseRequested()
        {
            PauseRequested?.Invoke();
        }
    }
}
