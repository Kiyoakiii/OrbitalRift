using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    // Небольшой 5x7 bitmap-шрифт для игрового HUD и меню. Он рисуется квадратами,
    // поэтому остаётся пиксельным на любом DPI и не зависит от шрифтов устройства.
    public static class PixelUi
    {
        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;
        private const int GlyphStep = 6;
        private static Texture2D pixel;
        private static Font uiFont;
        private static readonly string[] Unknown = { "11111", "10001", "00110", "01100", "11000", "10001", "11111" };
        private static readonly string[] CoreRing = { "0011100", "0111110", "1100011", "1100011", "1100011", "0111110", "0011100" };
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = G("01110","10001","10001","11111","10001","10001","10001"), ['B'] = G("11110","10001","10001","11110","10001","10001","11110"),
            ['C'] = G("01111","10000","10000","10000","10000","10000","01111"), ['D'] = G("11110","10001","10001","10001","10001","10001","11110"),
            ['E'] = G("11111","10000","10000","11110","10000","10000","11111"), ['F'] = G("11111","10000","10000","11110","10000","10000","10000"),
            ['G'] = G("01111","10000","10000","10111","10001","10001","01111"), ['H'] = G("10001","10001","10001","11111","10001","10001","10001"),
            ['I'] = G("11111","00100","00100","00100","00100","00100","11111"), ['J'] = G("00111","00010","00010","00010","10010","10010","01100"),
            ['K'] = G("10001","10010","10100","11000","10100","10010","10001"), ['L'] = G("10000","10000","10000","10000","10000","10000","11111"),
            ['M'] = G("10001","11011","10101","10101","10001","10001","10001"), ['N'] = G("10001","11001","10101","10011","10001","10001","10001"),
            ['O'] = G("01110","10001","10001","10001","10001","10001","01110"), ['P'] = G("11110","10001","10001","11110","10000","10000","10000"),
            ['Q'] = G("01110","10001","10001","10001","10101","10010","01101"), ['R'] = G("11110","10001","10001","11110","10100","10010","10001"),
            ['S'] = G("01111","10000","10000","01110","00001","00001","11110"), ['T'] = G("11111","00100","00100","00100","00100","00100","00100"),
            ['U'] = G("10001","10001","10001","10001","10001","10001","01110"), ['V'] = G("10001","10001","10001","10001","10001","01010","00100"),
            ['W'] = G("10001","10001","10001","10101","10101","10101","01010"), ['X'] = G("10001","10001","01010","00100","01010","10001","10001"),
            ['Y'] = G("10001","10001","01010","00100","00100","00100","00100"), ['Z'] = G("11111","00001","00010","00100","01000","10000","11111"),
            ['0'] = G("01110","10001","10011","10101","11001","10001","01110"), ['1'] = G("00100","01100","00100","00100","00100","00100","01110"),
            ['2'] = G("01110","10001","00001","00010","00100","01000","11111"), ['3'] = G("11110","00001","00001","01110","00001","00001","11110"),
            ['4'] = G("00010","00110","01010","10010","11111","00010","00010"), ['5'] = G("11111","10000","10000","11110","00001","00001","11110"),
            ['6'] = G("01110","10000","10000","11110","10001","10001","01110"), ['7'] = G("11111","00001","00010","00100","01000","01000","01000"),
            ['8'] = G("01110","10001","10001","01110","10001","10001","01110"), ['9'] = G("01110","10001","10001","01111","00001","00001","01110"),
            ['А'] = G("01110","10001","10001","11111","10001","10001","10001"), ['Б'] = G("11111","10000","10000","11110","10001","10001","11110"),
            ['В'] = G("11110","10001","10001","11110","10001","10001","11110"), ['Г'] = G("11111","10000","10000","10000","10000","10000","10000"),
            ['Д'] = G("00110","01001","01001","01001","11111","10001","10001"), ['Е'] = G("11111","10000","10000","11110","10000","10000","11111"),
            ['Ж'] = G("10101","10101","01010","00100","01010","10101","10101"), ['З'] = G("01110","10001","00001","00110","00001","10001","01110"),
            ['И'] = G("10001","11001","10101","10011","10001","10001","10001"), ['Й'] = G("00100","10001","11001","10101","10011","10001","10001"), ['К'] = G("10001","10010","10100","11000","10100","10010","10001"),
            ['Л'] = G("00111","01001","10001","10001","10001","10001","10001"), ['М'] = G("10001","11011","10101","10101","10001","10001","10001"),
            ['Н'] = G("10001","10001","10001","11111","10001","10001","10001"), ['О'] = G("01110","10001","10001","10001","10001","10001","01110"),
            ['П'] = G("11111","10001","10001","10001","10001","10001","10001"), ['Р'] = G("11110","10001","10001","11110","10000","10000","10000"),
            ['С'] = G("01111","10000","10000","10000","10000","10000","01111"), ['Т'] = G("11111","00100","00100","00100","00100","00100","00100"),
            ['У'] = G("10001","10001","01010","00100","01000","10000","10000"), ['Ф'] = G("00100","01110","10101","10101","01110","00100","00100"),
            ['Х'] = G("10001","10001","01010","00100","01010","10001","10001"), ['Ц'] = G("10001","10001","10001","10001","10001","11111","00001"),
            ['Ч'] = G("10001","10001","10001","01111","00001","00001","00001"), ['Ш'] = G("10101","10101","10101","10101","10101","10101","11111"),
            ['Щ'] = G("10101","10101","10101","10101","10101","11111","00001"), ['Ъ'] = G("11000","01000","01110","01001","01001","01001","01110"),
            ['Ы'] = G("10001","10001","10111","11001","10101","10101","10111"), ['Ь'] = G("10000","10000","11110","10001","10001","10001","11110"),
            ['Э'] = G("01110","10001","00001","00111","00001","10001","01110"), ['Ю'] = G("10011","10101","10101","11101","10101","10101","10011"),
            ['Я'] = G("01111","10001","10001","01111","00101","01001","10001"),
            ['-'] = G("00000","00000","00000","11111","00000","00000","00000"), ['+'] = G("00000","00100","00100","11111","00100","00100","00000"),
            ['/'] = G("00001","00010","00100","01000","10000","00000","00000"), ['.'] = G("00000","00000","00000","00000","00000","00110","00110"),
            [':'] = G("00000","00110","00110","00000","00110","00110","00000"), ['!'] = G("00100","00100","00100","00100","00100","00000","00100"),
            ['?'] = G("01110","10001","00001","00010","00100","00000","00100"), ['>'] = G("10000","01000","00100","00010","00100","01000","10000"),
            ['<'] = G("00001","00010","00100","01000","00100","00010","00001"), ['='] = G("00000","11111","00000","11111","00000","00000","00000")
        };

        private static string[] G(params string[] rows) => rows;
        private static Texture2D Pixel
        {
            get
            {
                if (pixel != null) return pixel;
                pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
                return pixel;
            }
        }

        public static void DrawPanel(Rect rect, Color fill, Color border, float thickness = 3f)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = fill;
            GUI.DrawTexture(rect, Pixel);
            GUI.color = border;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Pixel);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Pixel);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Pixel);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Pixel);
            GUI.color = Color.white;
        }

        public static void DrawSegmentBar(Rect rect, int value, int max, Color fill, Color empty, Color border)
        {
            if (Event.current.type != EventType.Repaint || max <= 0) return;
            DrawPanel(rect, new Color(0f, 0f, 0f, 0f), border, 2f);
            var inner = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f);
            var gap = Mathf.Max(2f, inner.height * .16f);
            var segmentWidth = (inner.width - gap * (max - 1)) / max;
            for (var i = 0; i < max; i++)
            {
                var segment = new Rect(inner.x + i * (segmentWidth + gap), inner.y, segmentWidth, inner.height);
                GUI.color = i < value ? fill : empty;
                GUI.DrawTexture(segment, Pixel);
                if (i < value)
                {
                    GUI.color = new Color(1f, 1f, 1f, .22f);
                    GUI.DrawTexture(new Rect(segment.x + 2f, segment.y + 2f, segment.width - 4f, Mathf.Max(1f, segment.height * .18f)), Pixel);
                }
            }
            GUI.color = Color.white;
        }

        public static void DrawCoreIcon(Rect rect, bool active, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var cell = Mathf.Max(1f, Mathf.Min(rect.width, rect.height) / 7f);
            var originX = rect.x + (rect.width - cell * 7f) * .5f;
            var originY = rect.y + (rect.height - cell * 7f) * .5f;
            var ringColor = active ? color : new Color(color.r * .28f, color.g * .32f, color.b * .38f, .9f);
            GUI.color = ringColor;
            for (var row = 0; row < 7; row++)
            for (var column = 0; column < 7; column++)
                if (CoreRing[row][column] == '1') GUI.DrawTexture(new Rect(originX + column * cell, originY + row * cell, cell, cell), Pixel);
            if (active)
            {
                // Три коротких луча внутри кольца дают узнаваемый «мерседес»-силуэт.
                GUI.DrawTexture(new Rect(originX + cell * 3f, originY + cell * 2f, cell, cell * 3f), Pixel);
                GUI.DrawTexture(new Rect(originX + cell * 2f, originY + cell * 4f, cell * 3f, cell), Pixel);
                GUI.DrawTexture(new Rect(originX + cell * 3f, originY + cell * 3f, cell, cell), Pixel);
            }
            GUI.color = Color.white;
        }

        private static Font UiFont
        {
            get
            {
                if (uiFont != null) return uiFont;
                // Jura is a clean sci-fi display face with full Cyrillic support.
                // The Arial fallback keeps the editor preview usable if Unity is
                // still importing the bundled font on a first launch.
                uiFont = Resources.Load<Font>("Fonts/Jura");
                if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return uiFont;
            }
        }

        private static GUIStyle MakeTextStyle(int fontSize, Color color, TextAnchor alignment)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = UiFont,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = alignment,
                wordWrap = false,
                richText = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                normal = { textColor = color },
                hover = { textColor = color },
                active = { textColor = color },
                focused = { textColor = color }
            };
        }

        public static void DrawText(Rect rect, string value, int requestedPixelSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter, bool shadow = true)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(value)) return;
            var text = value.ToUpperInvariant().Replace('Ё', 'Е').Replace('•', '.').Replace('—', '-');
            // The old bitmap renderer used a 7px glyph grid. Jura is a real
            // outline font, so use a larger base multiplier to preserve the
            // same visual weight in panels and on high-DPI phones.
            var fontSize = Mathf.Clamp(requestedPixelSize * 6, 10, 72);
            var content = new GUIContent(text);
            var style = MakeTextStyle(fontSize, color, alignment);

            // Fit each label to its own panel while keeping a readable minimum.
            // This avoids the old bitmap glyphs becoming oversized on desktop.
            var lines = text.Split('\n');
            while (fontSize > 9)
            {
                style.fontSize = fontSize;
                var fits = style.CalcHeight(content, rect.width) <= rect.height + 1f;
                for (var i = 0; i < lines.Length && fits; i++)
                    fits = style.CalcSize(new GUIContent(lines[i])).x <= rect.width + 1f;
                if (fits) break;
                fontSize--;
            }

            if (shadow)
            {
                var shadowStyle = MakeTextStyle(fontSize, new Color(0f, 0f, .02f, color.a * .8f), alignment);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), content, shadowStyle);
            }
            style.fontSize = fontSize;
            GUI.Label(rect, content, style);
            GUI.color = Color.white;
        }

        private static void DrawLines(Rect rect, string[] lines, int pixelSize, Color color, TextAnchor alignment, float startY, float shadowOffset)
        {
            GUI.color = color;
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var lineWidth = line.Length * GlyphStep * pixelSize - pixelSize;
                var x = alignment == TextAnchor.UpperLeft || alignment == TextAnchor.MiddleLeft || alignment == TextAnchor.LowerLeft
                    ? rect.x : alignment == TextAnchor.UpperRight || alignment == TextAnchor.MiddleRight || alignment == TextAnchor.LowerRight
                        ? rect.xMax - lineWidth : rect.x + (rect.width - lineWidth) * .5f;
                var y = startY + lineIndex * (GlyphHeight + 1) * pixelSize;
                for (var charIndex = 0; charIndex < line.Length; charIndex++)
                {
                    var character = line[charIndex];
                    if (character == ' ') continue;
                    if (!Glyphs.TryGetValue(character, out var glyph)) glyph = Unknown;
                    for (var row = 0; row < GlyphHeight; row++)
                    for (var column = 0; column < GlyphWidth; column++)
                        if (glyph[row][column] == '1') GUI.DrawTexture(new Rect(x + charIndex * GlyphStep * pixelSize + column * pixelSize + shadowOffset, y + row * pixelSize + shadowOffset, pixelSize, pixelSize), Pixel);
                }
            }
        }
    }
}
