using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Ghost wakes make the harrier's dash legible; actual clone enemies are
    /// regular gameplay objects managed by GameManager and can be destroyed.
    /// </summary>
    public sealed class HarrierBossPresentation : MonoBehaviour
    {
        private const int WakeCount = 4;
        private Transform visualRoot;
        private SpriteRenderer aura;
        private SpriteRenderer[] wakes;
        private Sprite glowSprite;
        private bool visible;
        private Vector3 spriteBaseScale = Vector3.one;
        private Sprite lastBossSprite;

        public void Configure(Transform arena, Sprite glow)
        {
            glowSprite = glow;
            EnsureVisuals(arena);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (visualRoot != null) visualRoot.gameObject.SetActive(value);
        }

        public void Render(Enemy boss, bool dashing)
        {
            if (!visible || boss == null || visualRoot == null) return;
            var center = (Vector2)boss.transform.position;
            var time = Time.time;
            var theme = boss.Definition.Theme;
            var style = boss.ActiveAbility != null ? boss.ActiveAbility.Style : theme;
            if (lastBossSprite != boss.Renderer.sprite)
            {
                lastBossSprite = boss.Renderer.sprite;
                spriteBaseScale = boss.Renderer.transform.localScale;
            }
            var pulse = .5f + .5f * Mathf.Sin(time * (dashing ? 11f : 4f));
            boss.Renderer.transform.localScale = spriteBaseScale * (1f + Mathf.Sin(time * (dashing ? 12f : 3.4f)) * (dashing ? .07f : .035f));
            boss.Renderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * (dashing ? 9f : 2.3f)) * (dashing ? 9f : 3.5f));
            aura.transform.position = center;
            aura.transform.localScale = Vector3.one * (.95f + pulse * .18f);
            aura.color = theme.Tint(theme.Primary, dashing ? .26f + pulse * .20f : .10f + pulse * .08f);
            var move = new Vector2(Mathf.Cos(boss.Angle), Mathf.Sin(boss.Angle));
            for (var i = 0; i < wakes.Length; i++)
            {
                var wake = wakes[i];
                wake.gameObject.SetActive(dashing || i == 0);
                wake.transform.position = center - move * (.18f + i * .18f);
                wake.transform.localScale = Vector3.one * (.50f - i * .07f);
                wake.color = style.Tint(style.Fade(i / (float)wakes.Length, true), dashing ? .34f : .08f);
            }
        }

        private void EnsureVisuals(Transform arena)
        {
            if (visualRoot != null) return;
            visualRoot = new GameObject("Umbral harrier FX").transform;
            visualRoot.SetParent(arena, false);
            aura = CreateGlow("Harrier rift aura", 1);
            wakes = new SpriteRenderer[WakeCount];
            for (var i = 0; i < wakes.Length; i++) wakes[i] = CreateGlow("Harrier echo wake " + (i + 1), 2);
            SetVisible(false);
        }

        private SpriteRenderer CreateGlow(string name, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(visualRoot, false);
            renderer.sprite = glowSprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void OnDisable()
        {
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (visualRoot != null) Destroy(visualRoot.gameObject);
        }
    }
}
