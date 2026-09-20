using UnityEngine;

namespace OrbitalRift
{
    // Decorative detail for the authored nebula layer. It is deliberately a small fixed pool:
    // the large cloud cards remain in SpaceDepthLayer while this component adds local pockets,
    // voids, filaments and dust without introducing a full-screen post effect.
    public sealed class SpaceNebulaClusterVisual : MonoBehaviour
    {
        private const int MaxClusters = 4;
        private const int VoidsPerCluster = 2;
        private const int DustPerCluster = 4;
        private const int FilamentsPerCluster = 2;
        private const int FilamentSegments = 20;

        private SpaceDepthLayer sourceLayer;
        private SpaceNebulaSettings settings;
        private SpriteRenderer[] outerGlow;
        private SpriteRenderer[] innerGlow;
        private SpriteRenderer[] voids;
        private SpriteRenderer[] dust;
        private LineRenderer[] filaments;
        private Vector3[][] filamentPoints;
        private Sprite softSprite;
        private Texture2D softTexture;
        private Material glowMaterial;
        private Material flatMaterial;
        private Material filamentMaterial;
        private float visualTime;

        public void Initialize(SpaceDepthLayer layer, SpaceNebulaSettings value)
        {
            sourceLayer = layer;
            settings = value;
            softSprite = CreateSoftSprite();
            var defaultShader = Shader.Find("Sprites/Default");
            var additiveShader = Shader.Find("Particles/Additive");
            flatMaterial = defaultShader != null ? new Material(defaultShader) : null;
            glowMaterial = additiveShader != null ? new Material(additiveShader) : (defaultShader != null ? new Material(defaultShader) : null);
            filamentMaterial = additiveShader != null ? new Material(additiveShader) : (defaultShader != null ? new Material(defaultShader) : null);
            if (flatMaterial != null) flatMaterial.name = "Nebula cluster void material (runtime)";
            if (glowMaterial != null) glowMaterial.name = "Nebula cluster glow material (runtime)";
            if (filamentMaterial != null) filamentMaterial.name = "Nebula filament material (runtime)";

            outerGlow = new SpriteRenderer[MaxClusters];
            innerGlow = new SpriteRenderer[MaxClusters];
            voids = new SpriteRenderer[MaxClusters * VoidsPerCluster];
            dust = new SpriteRenderer[MaxClusters * DustPerCluster];
            filaments = new LineRenderer[MaxClusters * FilamentsPerCluster];
            filamentPoints = new Vector3[filaments.Length][];

            for (var i = 0; i < MaxClusters; i++)
            {
                outerGlow[i] = CreateSprite("cluster " + i.ToString("00") + " glow", glowMaterial, -79);
                innerGlow[i] = CreateSprite("cluster " + i.ToString("00") + " bright pocket", glowMaterial, -78);
                for (var v = 0; v < VoidsPerCluster; v++)
                    voids[i * VoidsPerCluster + v] = CreateSprite("cluster " + i.ToString("00") + " dark void " + v, flatMaterial, -78);
                for (var d = 0; d < DustPerCluster; d++)
                    dust[i * DustPerCluster + d] = CreateSprite("cluster " + i.ToString("00") + " dust " + d, glowMaterial, -77);
                for (var f = 0; f < FilamentsPerCluster; f++)
                {
                    var index = i * FilamentsPerCluster + f;
                    filaments[index] = CreateLine("cluster " + i.ToString("00") + " filament " + f, -77);
                    filamentPoints[index] = new Vector3[FilamentSegments];
                }
            }
            SetVisible(false);
        }

        public void SetSettings(SpaceNebulaSettings value)
        {
            settings = value;
        }

        public void Tick(float dt, float speed, Vector2 center, Vector2 cameraOffset, bool visible)
        {
            if (sourceLayer == null || settings == null)
            {
                SetVisible(false);
                return;
            }

            var count = Mathf.Clamp(settings.ClusterCount, 0, MaxClusters);
            count = Mathf.Min(count, sourceLayer.ActiveCount);
            var active = visible && count > 0 && settings.Opacity > .001f;
            visualTime += Mathf.Max(0f, dt) * Mathf.Max(0f, speed);

            for (var i = 0; i < MaxClusters; i++)
            {
                var clusterVisible = active && i < count;
                SetClusterVisible(i, clusterVisible);
                if (!clusterVisible) continue;

                var position = sourceLayer.GetRenderPosition(i, center, cameraOffset);
                var spin = sourceLayer.GetItemSpin(i) * Mathf.Deg2Rad;
                var sway = new Vector2(
                    Mathf.Sin(visualTime * (.33f + i * .04f) + i * 2.1f),
                    Mathf.Cos(visualTime * (.27f + i * .03f) + i * 1.7f)) * settings.Drift * .34f;
                position += sway;
                var size = settings.ClusterSize * (i == 0 ? 1.02f : i == 1 ? .91f : i == 2 ? .78f : .68f);
                var tint = Color.Lerp(settings.Tint, settings.HighlightTint, .16f + (i % 2) * .18f);
                tint.r *= settings.Brightness;tint.g *= settings.Brightness;tint.b *= settings.Brightness;
                var glowAlpha = Mathf.Clamp01(settings.Opacity * (.16f + settings.Glow * .16f));
                SetSprite(outerGlow[i], position, size * 1.08f, tint, glowAlpha);
                var pocketColor = Color.Lerp(tint, settings.HighlightTint, .58f);
                SetSprite(innerGlow[i], position + new Vector2(Mathf.Cos(spin), Mathf.Sin(spin)) * size * .055f,
                    size * (.25f + settings.Glow * .045f), pocketColor,
                    Mathf.Clamp01(settings.Opacity * (.34f + settings.Glow * .22f)));

                for (var v = 0; v < VoidsPerCluster; v++)
                {
                    var voidAngle = spin + (v == 0 ? 1.2f : -1.85f) + i * .31f;
                    var voidOffset = new Vector2(Mathf.Cos(voidAngle), Mathf.Sin(voidAngle)) * size * (v == 0 ? .18f : .28f);
                    var voidColor = new Color(.004f, .008f, .028f, Mathf.Clamp01(settings.DarkVoids * settings.Opacity * (.42f - v * .11f)));
                    SetSprite(voids[i * VoidsPerCluster + v], position + voidOffset,
                        size * (v == 0 ? .31f : .20f), voidColor, voidColor.a);
                }

                for (var d = 0; d < DustPerCluster; d++)
                {
                    var dustAngle = spin + d * Mathf.PI * .5f + i * .63f;
                    var dustRadius = size * (.27f + (d % 2) * .14f);
                    var dustPosition = position + new Vector2(Mathf.Cos(dustAngle), Mathf.Sin(dustAngle)) * dustRadius;
                    dustPosition += new Vector2(
                        Mathf.Sin(visualTime * (.42f + d * .05f) + d),
                        Mathf.Cos(visualTime * (.37f + d * .06f) + i)) * settings.Drift * .22f;
                    var dustColor = Color.Lerp(settings.HighlightTint, Color.white, d == 0 ? .45f : .08f);
                    SetSprite(dust[i * DustPerCluster + d], dustPosition,
                        size * (.018f + (d % 3) * .010f), dustColor,
                        Mathf.Clamp01(settings.Opacity * settings.Dust * (d == 0 ? .42f : .19f)));
                }

                for (var f = 0; f < FilamentsPerCluster; f++)
                {
                    var index = i * FilamentsPerCluster + f;
                    var points = filamentPoints[index];
                    var direction = spin + (f == 0 ? -.38f : .72f);
                    var tangent = new Vector2(-Mathf.Sin(direction), Mathf.Cos(direction));
                    var normal = new Vector2(Mathf.Cos(direction), Mathf.Sin(direction));
                    var length = size * (f == 0 ? .96f : .72f);
                    var amplitude = size * (.075f + settings.Filament * .055f) * (f == 0 ? 1f : .72f);
                    var color = Color.Lerp(settings.HighlightTint, tint, f == 0 ? .28f : .52f);
                    var alpha = Mathf.Clamp01(settings.Opacity * settings.Filament * (f == 0 ? .38f : .24f));
                    for (var p = 0; p < FilamentSegments; p++)
                    {
                        var u = p / (float)(FilamentSegments - 1);
                        var along = Mathf.Lerp(-length, length, u);
                        var wave = Mathf.Sin(u * Mathf.PI * (f == 0 ? 2.2f : 1.7f) + visualTime * (.21f + i * .02f) + i * 1.4f) * amplitude;
                        var falloff = Mathf.Sin(u * Mathf.PI);
                        points[p] = position + normal * (along * .12f) + tangent * (wave * falloff + along);
                    }
                    filaments[index].startColor = new Color(color.r, color.g, color.b, alpha);
                    filaments[index].endColor = new Color(color.r, color.g, color.b, 0f);
                    filaments[index].startWidth = filaments[index].endWidth = (.014f + settings.Filament * .018f) * (f == 0 ? 1f : .7f);
                    filaments[index].SetPositions(points);
                }
            }
        }

        private SpriteRenderer CreateSprite(string childName, Material material, int sortingOrder)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = softSprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            return renderer;
        }

        private LineRenderer CreateLine(string childName, int sortingOrder)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = FilamentSegments;
            line.sharedMaterial = filamentMaterial;
            line.sortingOrder = sortingOrder;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.enabled = false;
            return line;
        }

        private static void SetSprite(SpriteRenderer renderer, Vector2 position, float size, Color color, float alpha)
        {
            renderer.transform.position = new Vector3(position.x, position.y, 0f);
            renderer.transform.localScale = Vector3.one * Mathf.Max(.001f, size);
            renderer.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        private void SetClusterVisible(int index, bool visible)
        {
            outerGlow[index].enabled = visible;
            innerGlow[index].enabled = visible;
            for (var v = 0; v < VoidsPerCluster; v++) voids[index * VoidsPerCluster + v].enabled = visible;
            for (var d = 0; d < DustPerCluster; d++) dust[index * DustPerCluster + d].enabled = visible;
            for (var f = 0; f < FilamentsPerCluster; f++) filaments[index * FilamentsPerCluster + f].enabled = visible;
        }

        private void SetVisible(bool visible)
        {
            for (var i = 0; i < MaxClusters; i++) SetClusterVisible(i, visible);
        }

        private Sprite CreateSoftSprite()
        {
            const int size = 64;
            softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            softTexture.name = "Nebula detail soft mask (runtime)";
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var p = new Vector2((x + .5f) / size, (y + .5f) / size) * 2f - Vector2.one;
                var radius = p.magnitude;
                var alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 1.55f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            softTexture.SetPixels(pixels);
            softTexture.Apply(false, false);
            return Sprite.Create(softTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        private void OnDestroy()
        {
            if (glowMaterial != null) Destroy(glowMaterial);
            if (flatMaterial != null) Destroy(flatMaterial);
            if (filamentMaterial != null) Destroy(filamentMaterial);
            if (softSprite != null) Destroy(softSprite);
            if (softTexture != null) Destroy(softTexture);
        }
    }
}
