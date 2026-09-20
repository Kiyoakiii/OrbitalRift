using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Visual language for Rift Echo: not a second static sprite, but a live
    /// electrical trace of the pilot. It is intentionally presentation-only;
    /// the combat rules stay in GameManager and remain deterministic.
    /// </summary>
    public sealed class RiftEchoPresentation : MonoBehaviour
    {
        private const int ArcPoints = 9;

        private Transform visualRoot;
        private LineRenderer outerArc;
        private LineRenderer coreArc;
        private SpriteRenderer halo;
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

        public void Render(Vector2 echoPosition, Vector2 playerPosition, float life01)
        {
            if (!visible || visualRoot == null) return;
            var time = Time.time;
            var delta = playerPosition - echoPosition;
            var distance = delta.magnitude;
            if (distance < .01f) return;

            var direction = delta / distance;
            var normal = new Vector2(-direction.y, direction.x);
            for (var i = 0; i < ArcPoints; i++)
            {
                var t = i / (ArcPoints - 1f);
                var envelope = Mathf.Sin(t * Mathf.PI);
                var noise = Mathf.Sin(time * 21f + i * 5.17f) * .12f + Mathf.Sin(time * 10f + i * 2.4f) * .06f;
                var point = Vector2.Lerp(echoPosition, playerPosition, t) + normal * noise * envelope;
                outerArc.SetPosition(i, point);
                coreArc.SetPosition(i, point);
            }

            var pulse = .5f + .5f * Mathf.Sin(time * 13f);
            var alpha = Mathf.Clamp01(life01);
            outerArc.startColor = new Color(.22f, .84f, 1f, .04f + alpha * (.15f + pulse * .12f));
            outerArc.endColor = new Color(.54f, .28f, 1f, .02f + alpha * .08f);
            outerArc.startWidth = .095f + pulse * .025f;
            outerArc.endWidth = .018f;
            coreArc.startColor = new Color(.88f, 1f, 1f, alpha * (.28f + pulse * .22f));
            coreArc.endColor = new Color(.64f, .52f, 1f, alpha * .10f);
            coreArc.startWidth = .018f + pulse * .007f;
            coreArc.endWidth = .006f;

            halo.transform.position = echoPosition;
            halo.transform.localScale = Vector3.one * (.85f + pulse * .18f);
            halo.color = new Color(.26f, .90f, 1f, alpha * (.09f + pulse * .12f));
        }

        private void EnsureVisuals(Transform arena)
        {
            if (visualRoot != null) return;
            visualRoot = new GameObject("Rift Echo electric trace").transform;
            visualRoot.SetParent(arena, false);
            outerArc = CreateLine("Rift Echo outer voltage", 2, ArcPoints);
            coreArc = CreateLine("Rift Echo core voltage", 5, ArcPoints);
            halo = new GameObject("Rift Echo voltage halo").AddComponent<SpriteRenderer>();
            halo.transform.SetParent(visualRoot, false);
            halo.sprite = glowSprite;
            halo.sortingOrder = 2;
            SetVisible(false);
        }

        private LineRenderer CreateLine(string name, int sortingOrder, int points)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(visualRoot, false);
            line.useWorldSpace = true;
            line.positionCount = points;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = sortingOrder;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 3;
            line.numCapVertices = 4;
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
