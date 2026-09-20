using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// A visual-only singularity for the ability sandbox.  The gameplay pull is
    /// owned by GameManager; this component only makes the danger readable.
    /// </summary>
    public sealed class SandboxBlackHolePresentation : MonoBehaviour
    {
        private const int ArcCount = 4;
        private const int ArcPoints = 18;
        private const int MoteCount = 16;

        private Transform visualRoot;
        private SpriteRenderer voidLens;
        private SpriteRenderer innerGlow;
        private SpriteRenderer outerHalo;
        private LineRenderer[] arcs;
        private SpriteRenderer[] motes;
        private Sprite glowSprite;
        private bool visible;

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

        public void Render(Vector2 center, float remaining01)
        {
            if (!visible || visualRoot == null) return;
            var time = Time.time;
            var style = BossAssetRegistry.Ability(BossAbilityId.VoidBlackHole).Style;
            var intensity = Mathf.SmoothStep(.30f, 1f, Mathf.Clamp01(remaining01));
            var breath = .5f + .5f * Mathf.Sin(time * 5.7f);

            voidLens.transform.position = center;
            voidLens.transform.localScale = Vector3.one * (.47f + breath * .055f);
            voidLens.color = new Color(.002f, .001f, .012f, .98f);
            innerGlow.transform.position = center;
            innerGlow.transform.localScale = Vector3.one * (.74f + breath * .13f);
            innerGlow.color = style.Tint(style.Secondary, .34f * intensity + breath * .10f);
            outerHalo.transform.position = center;
            outerHalo.transform.localScale = Vector3.one * (1.42f + breath * .20f);
            outerHalo.color = style.Tint(style.Primary, (.12f + breath * .10f) * intensity);

            for (var arcIndex = 0; arcIndex < arcs.Length; arcIndex++)
            {
                var arc = arcs[arcIndex];
                var rotation = time * (arcIndex % 2 == 0 ? 2.2f : -1.7f) + arcIndex * 1.57f;
                for (var pointIndex = 0; pointIndex < ArcPoints; pointIndex++)
                {
                    var t = pointIndex / (float)(ArcPoints - 1);
                    var radius = .30f + t * (.82f + arcIndex * .055f);
                    var angle = rotation + t * Mathf.PI * (1.45f + arcIndex * .12f);
                    var wobble = Mathf.Sin(time * 11f + pointIndex * .8f + arcIndex) * .025f;
                    arc.SetPosition(pointIndex, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + wobble));
                }
                var color = arcIndex % 2 == 0 ? new Color(.86f, .26f, 1f, .68f * intensity) : new Color(.20f, .86f, 1f, .54f * intensity);
                style.ApplyLine(arc, intensity * style.GlowOverLife.Evaluate(1-remaining01));
                arc.startWidth = .040f + breath * .015f;
                arc.endWidth = .003f;
            }

            for (var moteIndex = 0; moteIndex < motes.Length; moteIndex++)
            {
                var mote = motes[moteIndex];
                var seed = moteIndex * .618f;
                var radius = .42f + Mathf.Repeat(seed * .73f - time * (.11f + moteIndex % 3 * .018f), 1f) * 1.18f;
                var angle = time * (2.8f + moteIndex % 4 * .24f) + seed * 7.8f;
                mote.transform.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .64f) * radius;
                mote.transform.localScale = Vector3.one * (.020f + (moteIndex % 3) * .012f + breath * .007f);
                mote.color = moteIndex % 2 == 0
                    ? style.Tint(style.ParticleGradient.Evaluate(1-remaining01), .62f * intensity)
                    : style.Tint(style.ParticleGradient.Evaluate(1-remaining01), .52f * intensity);
            }
        }

        private void EnsureVisuals(Transform arena)
        {
            if (visualRoot != null) return;
            visualRoot = new GameObject("Sandbox black hole FX").transform;
            visualRoot.SetParent(arena, false);
            voidLens = CreateGlow("Black hole event horizon", 18);
            innerGlow = CreateGlow("Black hole inner lens", 17);
            outerHalo = CreateGlow("Black hole blue halo", 15);
            arcs = new LineRenderer[ArcCount];
            for (var i = 0; i < arcs.Length; i++) arcs[i] = CreateArc("Black hole accretion arc " + (i + 1), 16 + i);
            motes = new SpriteRenderer[MoteCount];
            for (var i = 0; i < motes.Length; i++) motes[i] = CreateGlow("Black hole mote " + (i + 1), 17);
            SetVisible(false);
        }

        private SpriteRenderer CreateGlow(string name, int sortingOrder)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(visualRoot, false);
            renderer.sprite = glowSprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private LineRenderer CreateArc(string name, int sortingOrder)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(visualRoot, false);
            line.positionCount = ArcPoints;
            line.useWorldSpace = true;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = sortingOrder;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            return line;
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
