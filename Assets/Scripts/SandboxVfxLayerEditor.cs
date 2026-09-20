using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Small in-game VFX workshop. It owns only authoring controls; the actual preview
    /// is rendered by SandboxLayeredVfx so the inspected result is the same runtime path
    /// used by the ability sandbox.
    /// </summary>
    public sealed class SandboxVfxLayerEditor
    {
        public const int LayerCount = 8;
        public const int AllLayers = (1 << LayerCount) - 1;

        private static readonly string[] LayerNames =
        {
            "CORE / ЯДРО", "RIBBONS / ЛЕНТЫ", "TRAIL / ШЛЕЙФ", "PARTICLES / ЧАСТИЦЫ",
            "NOISE / ШУМ", "GLOW / СВЕЧЕНИЕ", "IMPACT / ПОПАДАНИЕ", "DECAL / ДЕКАЛЬ"
        };

        private static readonly string[] LayerDescriptions =
        {
            "Яркий центр\nспела.", "Закрученные\nэнергокольца.", "Хвост движения\nи скорость.", "Искры, осколки,\nмикро-вспышки.",
            "Искажение,\nвихрь воздуха.", "Мягкое ореольное\nсвечение.", "Вспышка удара\nпри попадании.", "Руна / метка\nна траектории."
        };

        private static readonly Dictionary<int, Texture2D> LayerPreviewCache =
            new Dictionary<int, Texture2D>();

        private static readonly string[] SolarLayerNames =
        {
            "ПТЕНЕЦ", "ПЛАМЯ", "ШЛЕЙФ", "ИСКРЫ",
            "ПОКАЧИВАНИЕ", "ОРЕОЛ", "УДАР", "ЗАТУХАНИЕ"
        };
        private static readonly string[] SolarLayerDescriptions =
        {
            "PNG-птица.\nВзмах крыльев.", "Два живых\nхвоста огня.", "След полёта.\nУзкий конец.", "Части PNG\nи микроискры.",
            "Движение тела.\nБез новой PNG.", "Мягкий свет\nвокруг птицы.", "Вспышка,\nкольцо, искры.", "Туман и свет\nпосле удара."
        };

        private static readonly string[] SolarChicksLayerResources =
        {
            // The card thumbnails and the live preview intentionally share the same
            // transparent runtime-ready files, so the assembly shown on the left is
            // the assembly rendered on the right.
            "Spells/SolarChicks/Textures/Chick", "Spells/SolarChicks/Textures/FlameRibbons",
            "Spells/SolarChicks/Textures/TrailMask", "Spells/SolarChicks/Textures/EmberAtlas",
            "Spells/SolarChicks/Textures/FlameRibbons", "Spells/SolarChicks/Textures/Glow",
            "Spells/SolarChicks/Textures/ImpactFlare", "Spells/SolarChicks/Textures/Afterglow"
        };

        private static readonly Dictionary<string, Texture2D> LayerPhotoCache =
            new Dictionary<string, Texture2D>();

        private readonly bool[] enabled = new bool[LayerCount];
        private readonly Dictionary<AbilitySandboxAbilityId, Preset> presets =
            new Dictionary<AbilitySandboxAbilityId, Preset>();
        private int selectedCatalogIndex;
        private AbilitySandboxAbilityId selectedAbility;
        private bool commitRequested;
        private bool spellSelectorOpen;

        private struct Preset
        {
            public int LayerMask;
            public float Scale;
            public float Brightness;
            public float Glow;
        }

        public bool IsOpen { get; private set; }
        public AbilitySandboxAbilityId SelectedAbility => selectedAbility;
        public int LayerMask
        {
            get
            {
                var mask = 0;
                for (var i = 0; i < LayerCount; i++) if (enabled[i]) mask |= 1 << i;
                return mask;
            }
        }
        public float Scale { get; private set; } = 1f;
        public float Brightness { get; private set; } = 1f;
        public float Glow { get; private set; } = 1f;

        public bool ConsumeCommitRequest()
        {
            if (!commitRequested) return false;
            commitRequested = false;
            return true;
        }

        public void Open(IReadOnlyList<AbilitySandboxSession.Definition> definitions, AbilitySandboxAbilityId preferred)
        {
            IsOpen = true;
            commitRequested = false;
            spellSelectorOpen = false;
            selectedCatalogIndex = 0;
            if (definitions != null)
                for (var i = 0; i < definitions.Count; i++)
                    if (definitions[i].Id == preferred) { selectedCatalogIndex = i; break; }
            SelectDefinition(definitions, selectedCatalogIndex);
        }

        public void Close()
        {
            IsOpen = false;
            commitRequested = false;
            spellSelectorOpen = false;
        }

        private void SelectDefinition(IReadOnlyList<AbilitySandboxSession.Definition> definitions, int index)
        {
            if (definitions == null || definitions.Count == 0) return;
            selectedCatalogIndex = Mathf.Clamp(index, 0, definitions.Count - 1);
            selectedAbility = definitions[selectedCatalogIndex].Id;
            if (presets.TryGetValue(selectedAbility, out var preset)) LoadPreset(preset);
            else ResetControls();
        }

        private void LoadPreset(Preset preset)
        {
            for (var i = 0; i < LayerCount; i++) enabled[i] = (preset.LayerMask & (1 << i)) != 0;
            Scale = preset.Scale;
            Brightness = preset.Brightness;
            Glow = preset.Glow;
        }

        private void SavePreset()
        {
            presets[selectedAbility] = new Preset
            {
                LayerMask = LayerMask,
                Scale = Scale,
                Brightness = Brightness,
                Glow = Glow
            };
        }

        private void ResetControls()
        {
            for (var i = 0; i < LayerCount; i++) enabled[i] = true;
            Scale = 1f;
            Brightness = 1f;
            Glow = 1f;
        }

        public void Draw(float left, float top, float width, float height, int pixel, int smallPixel,
            Color pale, Color cyan, float previewTime, IReadOnlyList<AbilitySandboxSession.Definition> definitions)
        {
            if (!IsOpen) return;
            var shade = new Rect(left, top, width, height);
            PixelUi.DrawPanel(shade, new Color(0f, .003f, .018f, .06f), Color.clear, 0f);

            var modal = new Rect(left + width * .025f, top + height * .03f, width * .95f, height * .94f);
            PixelUi.DrawPanel(modal, new Color(.008f, .022f, .072f, .035f), new Color(.50f, .88f, 1f, .98f), 4f);
            PixelUi.DrawText(new Rect(modal.x + 16f, modal.y + modal.height * .025f, modal.width - 32f, modal.height * .065f),
                "VFX WORKSHOP // СЛОИ", Mathf.RoundToInt(pixel * 1.08f), Color.white, TextAnchor.MiddleLeft);
            PixelUi.DrawText(new Rect(modal.x + 16f, modal.y + modal.height * .087f, modal.width - 32f, modal.height * .035f),
                "LIVE PREVIEW // LOOP  ·  ИЗМЕНЕНИЯ ПРИМЕНЯЮТСЯ КАЖДЫЙ КАДР.",
                Mathf.Max(3, smallPixel - 1), new Color(.58f, .80f, 1f), TextAnchor.MiddleLeft);

            if (definitions == null || definitions.Count == 0) return;
            selectedCatalogIndex = Mathf.Clamp(selectedCatalogIndex, 0, definitions.Count - 1);
            var pageSize = 8;
            var pageCount = Mathf.Max(1, Mathf.CeilToInt(definitions.Count / (float)pageSize));
            var page = selectedCatalogIndex / pageSize;
            var compactSelector = new Rect(modal.x + modal.width * .045f, modal.y + modal.height * .145f,
                modal.width * .48f, modal.height * .065f);
            var currentDefinition = definitions[selectedCatalogIndex];
            DrawButton(compactSelector,
                "1. СПЕЛЛ: " + currentDefinition.ShortName + "  //  " + (page + 1) + "/" + pageCount + (spellSelectorOpen ? "  ▲" : "  ▼"),
                smallPixel, new Color(.012f, .045f, .105f, .58f), new Color(.32f, .72f, .92f, .84f), Color.white);
            if (GUI.Button(compactSelector, GUIContent.none, GUIStyle.none)) spellSelectorOpen = !spellSelectorOpen;

            if (spellSelectorOpen)
            {
                var selector = new Rect(modal.x + modal.width * .045f, compactSelector.yMax + modal.height * .012f,
                    modal.width * .91f, modal.height * .205f);
                PixelUi.DrawPanel(selector, new Color(.012f, .045f, .105f, .72f), new Color(.22f, .52f, .74f, .78f), 2f);
                PixelUi.DrawText(new Rect(selector.x + 8f, selector.y + 4f, selector.width - 16f, selector.height * .16f),
                    "ВЫБОР СПЕЛЛА  //  СТРАНИЦА " + (page + 1) + "/" + pageCount, smallPixel,
                    new Color(.60f, .82f, 1f), TextAnchor.MiddleLeft);

                var gridTop = selector.y + selector.height * .22f;
                var cellGap = selector.width * .012f;
                var cellWidth = (selector.width - cellGap * 3f) / 4f;
                var cellHeight = selector.height * .31f;
                var first = page * pageSize;
                for (var i = 0; i < pageSize; i++)
                {
                    var catalogIndex = first + i;
                    if (catalogIndex >= definitions.Count) break;
                    var row = i / 4;
                    var column = i % 4;
                    var cell = new Rect(selector.x + column * (cellWidth + cellGap), gridTop + row * (cellHeight + cellGap), cellWidth, cellHeight);
                    var definition = definitions[catalogIndex];
                    var selected = catalogIndex == selectedCatalogIndex;
                    DrawButton(cell, definition.ShortName, smallPixel,
                        selected ? new Color(definition.Accent.r * .25f, definition.Accent.g * .25f, definition.Accent.b * .25f, .92f) : new Color(.018f, .035f, .078f, .76f),
                        selected ? definition.Accent : new Color(.22f, .38f, .58f), selected ? Color.white : pale);
                    if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) SelectDefinition(definitions, catalogIndex);
                }
                var pageButtonY = selector.y + selector.height * .035f;
                var previousPage = new Rect(selector.xMax - selector.width * .245f, pageButtonY, selector.width * .10f, selector.height * .12f);
                var nextPage = new Rect(selector.xMax - selector.width * .125f, pageButtonY, selector.width * .10f, selector.height * .12f);
                DrawButton(previousPage, "◀", smallPixel, new Color(.025f, .07f, .14f, .72f), cyan, Color.white);
                DrawButton(nextPage, "▶", smallPixel, new Color(.025f, .07f, .14f, .72f), cyan, Color.white);
                if (GUI.Button(previousPage, GUIContent.none, GUIStyle.none) && page > 0)
                    SelectDefinition(definitions, (page - 1) * pageSize);
                if (GUI.Button(nextPage, GUIContent.none, GUIStyle.none) && page + 1 < pageCount)
                    SelectDefinition(definitions, (page + 1) * pageSize);
            }

            var bodyTop = modal.y + modal.height * (spellSelectorOpen ? .445f : .255f);
            var bodyHeight = modal.height * (spellSelectorOpen ? .40f : .60f);
            var layerPanel = new Rect(modal.x + modal.width * .045f, bodyTop, modal.width * .29f, bodyHeight);
            // The live polygon gets the space previously occupied by the parameter
            // panel.  Layer cards remain the controls; the preview is the focus.
            var previewPanel = new Rect(layerPanel.xMax + modal.width * .018f, bodyTop, modal.width * .602f, bodyHeight);
            DrawLayerPanel(layerPanel, pixel, smallPixel, pale, cyan, currentDefinition.Id);
            DrawPreviewPanel(previewPanel, pixel, smallPixel, pale, cyan, previewTime);

            var footer = new Rect(modal.x + modal.width * .045f, modal.y + modal.height * .865f, modal.width * .91f, modal.height * .075f);
            PixelUi.DrawText(new Rect(footer.x, footer.y, footer.width * .48f, footer.height),
                "КАДР СБОРКИ: " + EnabledLayerCount() + "/" + LayerCount + " СЛОЁВ", smallPixel,
                new Color(.60f, .82f, 1f), TextAnchor.MiddleLeft);
            var reset = new Rect(footer.x + footer.width * .53f, footer.y, footer.width * .19f, footer.height);
            var close = new Rect(footer.x + footer.width * .75f, footer.y, footer.width * .25f, footer.height);
            DrawButton(reset, "СБРОС", smallPixel, new Color(.05f, .10f, .20f, .98f), cyan, Color.white);
            DrawButton(close, "ГОТОВО\nПРИМЕНИТЬ", smallPixel, new Color(.05f, .19f, .20f, .98f), new Color(.38f, 1f, .70f), Color.white);
            if (GUI.Button(reset, GUIContent.none, GUIStyle.none)) ResetControls();
            if (GUI.Button(close, GUIContent.none, GUIStyle.none))
            {
                SavePreset();
                commitRequested = true;
                IsOpen = false;
            }
        }

        // The preview marker follows the sandbox session clock, so scrubbing speed affects it too.
        private void DrawPreviewPanel(Rect panel, int pixel, int smallPixel, Color pale, Color cyan, float previewTime)
        {
            PixelUi.DrawPanel(panel, new Color(.004f, .018f, .035f, .025f), new Color(.28f, .86f, 1f, .90f), 2f);
            PixelUi.DrawText(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, panel.height * .10f),
                "4. LIVE RUNTIME PREVIEW", smallPixel, new Color(.62f, .90f, 1f), TextAnchor.MiddleLeft);
            PixelUi.DrawText(new Rect(panel.x + 10f, panel.y + panel.height * .105f, panel.width - 20f, panel.height * .065f),
                selectedAbility == AbilitySandboxAbilityId.SolarChicks ? "ИГРОВОЙ PREFAB: SOLARCHICK · ПОЛЁТ И ПОПАДАНИЕ" : "АНИМАЦИЯ ИДЁТ В ЦИКЛЕ  ·  ИСТОЧНИК: SANDBOXLAYEREDVFX", Mathf.Max(3, smallPixel - 1),
                new Color(.48f, .72f, .86f), TextAnchor.MiddleLeft);

            var stage = new Rect(panel.x + panel.width * .03f, panel.y + panel.height * .17f,
                panel.width * .94f, panel.height * .70f);
            // The old world-space polygon and reticle competed with the spell itself.
            // Leave this area as a clean animation plate; only the runtime VFX is drawn here.

            var solar = selectedAbility == AbilitySandboxAbilityId.SolarChicks;
            var loop = solar ? 1f : Mathf.Repeat(previewTime, 1.85f) / 1.85f;
            var timeline = new Rect(panel.x + panel.width * .06f, panel.y + panel.height * .89f,
                panel.width * .88f, panel.height * .035f);
            PixelUi.DrawPanel(timeline, new Color(.01f, .04f, .08f, .78f), new Color(.18f, .42f, .58f), 1f);
            GUI.color = cyan;
            GUI.DrawTexture(new Rect(timeline.x + 2f, timeline.y + 2f,
                Mathf.Max(2f, (timeline.width - 4f) * loop), Mathf.Max(1f, timeline.height - 4f)), Texture2D.whiteTexture);
            GUI.color = Color.white;
            PixelUi.DrawText(new Rect(panel.x + panel.width * .06f, panel.y + panel.height * .925f,
                panel.width * .88f, panel.height * .055f),
                solar ? "ЦИКЛ 3.5 С · ПОЛЁТ 2 С · УДАР И ЗАТУХАНИЕ" : "LOOP 0.00 — 1.85 S  ·  КАДР " + Mathf.RoundToInt(loop * 100f) + "%",
                Mathf.Max(3, smallPixel - 1), new Color(.60f, .82f, 1f), TextAnchor.MiddleCenter);
        }

        private void DrawLayerPanel(Rect panel, int pixel, int smallPixel, Color pale, Color cyan, AbilitySandboxAbilityId ability)
        {
            PixelUi.DrawPanel(panel, new Color(.012f, .036f, .088f, .98f), new Color(.24f, .52f, .76f, .84f), 2f);
            PixelUi.DrawText(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, panel.height * .10f),
                "2. СЛОИ ЭФФЕКТА // КАРТКИ", smallPixel, new Color(.62f, .84f, 1f), TextAnchor.MiddleLeft);
            PixelUi.DrawText(new Rect(panel.x + 10f, panel.y + panel.height * .105f, panel.width - 20f, panel.height * .065f),
                "КЛИК = ВКЛ / ВЫКЛ  ·  КАРТКА = ОТДЕЛЬНЫЙ СЛОЙ", Mathf.Max(3, smallPixel - 1), new Color(.46f, .64f, .84f), TextAnchor.MiddleLeft);
            var top = panel.y + panel.height * .19f;
            var gridHeight = panel.height * .77f;
            var gapX = panel.width * .025f;
            var gapY = panel.height * .018f;
            var cardWidth = (panel.width - 20f - gapX) * .5f;
            var cardHeight = (gridHeight - gapY * 3f) * .25f;
            for (var i = 0; i < LayerCount; i++)
            {
                var row = i / 2;
                var column = i % 2;
                var card = new Rect(panel.x + 10f + column * (cardWidth + gapX), top + row * (cardHeight + gapY), cardWidth, cardHeight);
                var accent = LayerAccent(i);
                var state = enabled[i] ? "ON" : "OFF";
                var fill = enabled[i]
                    ? new Color(accent.r * .12f, accent.g * .12f, accent.b * .12f, .96f)
                    : new Color(.018f, .025f, .06f, .92f);
                var border = enabled[i] ? accent : new Color(.20f, .28f, .44f);
                PixelUi.DrawPanel(card, fill, border, 2f);
                var thumbSize = Mathf.Min(card.height - 10f, card.width * .36f);
                var thumb = new Rect(card.x + 5f, card.y + (card.height - thumbSize) * .5f, thumbSize, thumbSize);
                var layerPhoto = LayerPhotoTexture(i, ability);
                DrawTexture(thumb, layerPhoto != null ? layerPhoto : LayerPreviewTexture(i),
                    enabled[i] ? Color.white : new Color(.42f, .48f, .60f, .50f));
                var textX = thumb.xMax + 6f;
                PixelUi.DrawText(new Rect(textX, card.y + card.height * .08f, card.xMax - textX - 4f, card.height * .27f),
                    (selectedAbility == AbilitySandboxAbilityId.SolarChicks ? SolarLayerNames : LayerNames)[i], Mathf.Max(3, smallPixel - 1), enabled[i] ? pale : new Color(.46f, .54f, .68f), TextAnchor.MiddleLeft);
                PixelUi.DrawText(new Rect(textX, card.y + card.height * .36f, card.xMax - textX - 4f, card.height * .46f),
                    (selectedAbility == AbilitySandboxAbilityId.SolarChicks ? SolarLayerDescriptions : LayerDescriptions)[i], Mathf.Max(2, smallPixel - 2), enabled[i] ? new Color(.62f, .76f, .90f) : new Color(.34f, .40f, .52f), TextAnchor.MiddleLeft);
                PixelUi.DrawText(new Rect(textX, card.y + card.height * .80f, card.xMax - textX - 4f, card.height * .15f),
                    state, Mathf.Max(2, smallPixel - 2), enabled[i] ? accent : new Color(.34f, .42f, .56f), TextAnchor.LowerLeft);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) enabled[i] = !enabled[i];
            }
        }

        private void DrawControlPanel(Rect panel, int pixel, int smallPixel, Color pale, Color cyan, AbilitySandboxSession.Definition definition)
        {
            PixelUi.DrawPanel(panel, new Color(.016f, .032f, .080f, .98f), new Color(.58f, .38f, .82f, .88f), 2f);
            PixelUi.DrawText(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, panel.height * .10f),
                "3. ПАРАМЕТРЫ", smallPixel, new Color(.86f, .66f, 1f), TextAnchor.MiddleLeft);
            PixelUi.DrawText(new Rect(panel.x + 10f, panel.y + panel.height * .105f, panel.width - 20f, panel.height * .085f),
                definition.Source + " // " + definition.Name, smallPixel, definition.Accent, TextAnchor.MiddleLeft);
            var scale = Scale;
            var brightness = Brightness;
            var glow = Glow;
            DrawParameter(panel, "РАЗМЕР", ref scale, .55f, 2.5f, panel.y + panel.height * .25f, smallPixel, pale, cyan);
            DrawParameter(panel, "ЯРКОСТЬ", ref brightness, .20f, 2.5f, panel.y + panel.height * .40f, smallPixel, pale, cyan);
            DrawParameter(panel, "GLOW", ref glow, 0f, 2.5f, panel.y + panel.height * .55f, smallPixel, pale, cyan);
            Scale = scale;
            Brightness = brightness;
            Glow = glow;
            PixelUi.DrawText(new Rect(panel.x + 10f, panel.y + panel.height * .72f, panel.width - 20f, panel.height * .20f),
                "ЭТО НЕ КОНЦЕПТ-КАРТИНКА.\nПАНЕЛЬ УПРАВЛЯЕТ ТЕМ ЖЕ RUNTIME-ДИРЕКТОРОМ, ЧТО И СПЕЛЛ В ИГРЕ.",
                Mathf.Max(3, smallPixel - 1), new Color(.60f, .76f, .92f), TextAnchor.MiddleLeft);
        }

        private static void DrawParameter(Rect panel, string label, ref float value, float min, float max, float y,
            int smallPixel, Color pale, Color cyan)
        {
            var labelRect = new Rect(panel.x + 10f, y, panel.width * .35f, panel.height * .09f);
            var minus = new Rect(panel.x + panel.width * .49f, y, panel.width * .12f, panel.height * .09f);
            var plus = new Rect(panel.x + panel.width * .85f, y, panel.width * .12f, panel.height * .09f);
            PixelUi.DrawText(labelRect, label, smallPixel, pale, TextAnchor.MiddleLeft);
            PixelUi.DrawText(new Rect(panel.x + panel.width * .61f, y, panel.width * .22f, panel.height * .09f), value.ToString("0.00"), smallPixel, cyan);
            DrawButton(minus, "-", smallPixel, new Color(.04f, .08f, .17f, .98f), cyan, Color.white);
            DrawButton(plus, "+", smallPixel, new Color(.04f, .08f, .17f, .98f), cyan, Color.white);
            if (GUI.Button(minus, GUIContent.none, GUIStyle.none)) value = Mathf.Clamp(value - .10f, min, max);
            if (GUI.Button(plus, GUIContent.none, GUIStyle.none)) value = Mathf.Clamp(value + .10f, min, max);
            var bar = new Rect(panel.x + panel.width * .08f, y + panel.height * .105f, panel.width * .84f, panel.height * .025f);
            PixelUi.DrawPanel(bar, new Color(.02f, .05f, .10f, .96f), new Color(.20f, .36f, .58f), 1f);
            GUI.color = cyan;
            GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * Mathf.InverseLerp(min, max, value), Mathf.Max(1f, bar.height - 4f)), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static Texture2D LayerPreviewTexture(int index)
        {
            if (LayerPreviewCache.TryGetValue(index, out var cached)) return cached;
            const int textureWidth = 96;
            const int textureHeight = 64;
            var texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
            {
                name = "Sandbox VFX layer preview " + index,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            for (var y = 0; y < textureHeight; y++)
            {
                for (var x = 0; x < textureWidth; x++)
                {
                    var u = (x + .5f) / textureWidth * 2f - 1f;
                    var v = (y + .5f) / textureHeight * 2f - 1f;
                    var radius = Mathf.Sqrt(u * u + v * v);
                    var angle = Mathf.Atan2(v, u);
                    var color = new Color(0f, 0f, 0f, 0f);
                    switch (index)
                    {
                        case 0:
                            AddPreviewColor(ref color, new Color(1f, .42f, .06f), Mathf.Exp(-radius * radius * 13f));
                            AddPreviewColor(ref color, new Color(1f, .96f, .62f), Mathf.Exp(-radius * radius * 52f));
                            AddPreviewColor(ref color, new Color(1f, .18f, .02f), Mathf.Exp(-Mathf.Pow(radius - .48f, 2f) * 180f));
                            break;
                        case 1:
                            var ribbonA = Mathf.Exp(-Mathf.Pow(v - Mathf.Sin(u * 6.5f) * .22f, 2f) * 150f) * Mathf.Clamp01(1.1f - Mathf.Abs(u));
                            var ribbonB = Mathf.Exp(-Mathf.Pow(v + Mathf.Sin(u * 5.2f + .7f) * .19f, 2f) * 170f) * Mathf.Clamp01(1.1f - Mathf.Abs(u));
                            AddPreviewColor(ref color, new Color(1f, .36f, .04f), ribbonA);
                            AddPreviewColor(ref color, new Color(1f, .88f, .24f), ribbonB);
                            break;
                        case 2:
                            var taper = Mathf.Clamp01((u + 1f) * .5f);
                            var trail = Mathf.Exp(-Mathf.Pow(v - Mathf.Sin(u * 8f) * .09f, 2f) * 105f) * taper * taper;
                            var trailEdge = Mathf.Exp(-Mathf.Pow(v + .10f - Mathf.Sin(u * 5f) * .06f, 2f) * 260f) * taper;
                            AddPreviewColor(ref color, new Color(1f, .24f, .02f), trail);
                            AddPreviewColor(ref color, new Color(1f, .82f, .18f), trailEdge);
                            break;
                        case 3:
                            var dotSeed = Mathf.Abs(Mathf.Sin((x * 12.9898f + y * 78.233f + 13.17f) * .1234f));
                            var dot = dotSeed > .975f ? Mathf.Clamp01(1f - radius) * 1.2f : 0f;
                            var star = Mathf.Exp(-Mathf.Pow(radius - .48f, 2f) * 190f) * Mathf.Clamp01(dotSeed * 3f - 1.8f);
                            AddPreviewColor(ref color, new Color(1f, .72f, .18f), dot);
                            AddPreviewColor(ref color, new Color(1f, .98f, .74f), star);
                            break;
                        case 4:
                            var vortex = Mathf.Abs(Mathf.Sin(angle * 3f + radius * 19f)) * Mathf.Exp(-Mathf.Pow(radius - .45f, 2f) * 24f);
                            var vortexCore = Mathf.Exp(-radius * radius * 9f) * .35f;
                            AddPreviewColor(ref color, new Color(.92f, .18f, .04f), vortex);
                            AddPreviewColor(ref color, new Color(1f, .56f, .10f), vortexCore);
                            break;
                        case 5:
                            var glow = Mathf.Exp(-radius * radius * 3.2f);
                            var glowRing = Mathf.Exp(-Mathf.Pow(radius - .54f, 2f) * 55f) * .35f;
                            AddPreviewColor(ref color, new Color(1f, .24f, .03f), glow * .55f);
                            AddPreviewColor(ref color, new Color(1f, .78f, .20f), glowRing);
                            break;
                        case 6:
                            var burst = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 8f)), 14f) * Mathf.Exp(-radius * 2.2f);
                            var burstCore = Mathf.Exp(-radius * radius * 36f);
                            AddPreviewColor(ref color, Color.white, burst);
                            AddPreviewColor(ref color, new Color(1f, .38f, .04f), burst * .62f);
                            AddPreviewColor(ref color, new Color(1f, .86f, .26f), burstCore);
                            break;
                        default:
                            var decalRing = Mathf.Exp(-Mathf.Pow(radius - .53f, 2f) * 240f);
                            var decalInner = Mathf.Exp(-Mathf.Pow(radius - .27f, 2f) * 180f);
                            var spokes = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f)), 28f) * Mathf.Clamp01(1f - radius);
                            AddPreviewColor(ref color, new Color(1f, .30f, .04f), decalRing);
                            AddPreviewColor(ref color, new Color(1f, .78f, .18f), decalInner * .8f);
                            AddPreviewColor(ref color, new Color(1f, .95f, .62f), spokes);
                            break;
                    }
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply(false, true);
            LayerPreviewCache[index] = texture;
            return texture;
        }

        private static Texture2D LayerPhotoTexture(int index, AbilitySandboxAbilityId ability)
        {
            if (ability != AbilitySandboxAbilityId.SolarChicks || index < 0 || index >= SolarChicksLayerResources.Length)
                return null;
            var key = SolarChicksLayerResources[index];
            if (LayerPhotoCache.TryGetValue(key, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(key);
            LayerPhotoCache[key] = texture;
            return texture;
        }

        private static void AddPreviewColor(ref Color target, Color tint, float amount)
        {
            amount = Mathf.Clamp01(amount);
            target.r = Mathf.Clamp01(target.r + tint.r * amount);
            target.g = Mathf.Clamp01(target.g + tint.g * amount);
            target.b = Mathf.Clamp01(target.b + tint.b * amount);
            target.a = Mathf.Clamp01(Mathf.Max(target.a, amount));
        }

        private int EnabledLayerCount()
        {
            var count = 0;
            for (var i = 0; i < enabled.Length; i++) if (enabled[i]) count++;
            return count;
        }

        private static Color LayerAccent(int index)
        {
            switch (index)
            {
                case 0: return new Color(1f, .90f, .45f);
                case 1: return new Color(1f, .48f, .12f);
                case 2: return new Color(1f, .25f, .05f);
                case 3: return new Color(1f, .72f, .22f);
                case 4: return new Color(.98f, .35f, .12f);
                case 5: return new Color(1f, .78f, .30f);
                case 6: return Color.white;
                default: return new Color(1f, .38f, .08f);
            }
        }

        private static void DrawButton(Rect rect, string label, int smallPixel, Color fill, Color border, Color text)
        {
            PixelUi.DrawPanel(rect, fill, border, 2f);
            PixelUi.DrawText(rect, label, smallPixel, text);
        }

        private static void DrawTexture(Rect rect, Texture2D texture, Color color)
        {
            if (texture == null || Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }
    }
}
