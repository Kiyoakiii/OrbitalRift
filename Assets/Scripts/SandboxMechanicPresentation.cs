using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Bounded, reused geometry for sandbox spells. Owns and releases one shared material.</summary>
    public sealed class SandboxMechanicPresentation : MonoBehaviour
    {
        private readonly List<LineRenderer> lines = new List<LineRenderer>(256);
        private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>(48);
        private Transform root;
        private Material material;
        private int lineCursor, spriteCursor;

        public void Configure(Transform parent)
        {
            if (root != null) return;
            root = new GameObject("Sandbox mechanic FX").transform;
            root.SetParent(parent, false);
            material = new Material(Shader.Find("Sprites/Default")) { name = "Sandbox shared energy" };
        }

        public void BeginFrame() { if (root != null) root.gameObject.SetActive(true); lineCursor = spriteCursor = 0; }
        public void EndFrame()
        {
            for (var i = lineCursor; i < lines.Count; i++) lines[i].enabled = false;
            for (var i = spriteCursor; i < sprites.Count; i++) sprites[i].enabled = false;
        }
        public void Hide() { if (root != null) root.gameObject.SetActive(false); }

        private LineRenderer TakeLine(int count, Color color, float width)
        {
            if (root == null || lineCursor >= 256) return null;
            if (lineCursor == lines.Count)
            {
                var line = new GameObject("Energy stroke " + lineCursor).AddComponent<LineRenderer>();
                line.transform.SetParent(root, false);
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.numCapVertices = 3;
                line.numCornerVertices = 3;
                line.sortingOrder = 12;
                lines.Add(line);
            }
            var result = lines[lineCursor++];
            result.enabled = true;
            result.positionCount = count;
            result.startColor = result.endColor = color;
            result.startWidth = result.endWidth = width;
            return result;
        }

        public void Line(Vector2 a, Vector2 b, Color color, float width = .025f)
        {
            var line = TakeLine(2, color, width);
            if (line == null) return;
            line.SetPosition(0, a); line.SetPosition(1, b);
        }

        public void Arc(Vector2 center, float radius, float angle, float span, Color color, float width = .025f)
        {
            var count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(span) * radius * 12f), 8, 100);
            var line = TakeLine(count, color, width);
            if (line == null) return;
            for (var i = 0; i < count; i++)
            {
                var a = angle + span * i / (count - 1f);
                line.SetPosition(i, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        public void Ring(Vector2 center, float radius, Color color, float width = .025f)
        { Arc(center, radius, 0f, Mathf.PI * 2f, color, width); }

        public void Tether(Vector2 a, Vector2 b, Color color, float phase)
        {
            var line = TakeLine(32, color, .022f);
            if (line == null) return;
            var d = b - a;
            var side = new Vector2(-d.y, d.x).normalized;
            for (var i = 0; i < 32; i++)
            {
                var t = i / 31f;
                line.SetPosition(i, Vector2.Lerp(a, b, t) + side * (Mathf.Sin(t * 35f - phase * 9f) * .05f * Mathf.Sin(t * Mathf.PI)));
            }
        }

        public void Chevron(Vector2 center, float angle, float size, Color color)
        {
            var forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var side = new Vector2(-forward.y, forward.x);
            Line(center - forward * size + side * size * .65f, center, color, .036f);
            Line(center - forward * size - side * size * .65f, center, color, .036f);
        }

        public void Diamond(Vector2 center, float radius, float angle, Color color)
        {
            var line = TakeLine(5, color, .028f);
            if (line == null) return;
            for (var i = 0; i < 5; i++)
            {
                var a = angle + i * Mathf.PI * .5f;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        public void Sprite(Sprite sprite, Vector2 position, float angleDegrees, float worldSize, Color tint)
        {
            if (sprite == null || root == null || spriteCursor >= 48) return;
            if (spriteCursor == sprites.Count)
            {
                var sr = new GameObject("Energy silhouette " + spriteCursor).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root, false);
                sr.sortingOrder = 11;
                sprites.Add(sr);
            }
            var renderer = sprites[spriteCursor++];
            renderer.enabled = true;
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.transform.position = position;
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
            var bounds = sprite.bounds.size;
            renderer.transform.localScale = Vector3.one * (worldSize / Mathf.Max(.001f, Mathf.Max(bounds.x, bounds.y)));
        }

        private void OnDisable() { Hide(); }
        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            if (material != null) Destroy(material);
        }
    }
}
