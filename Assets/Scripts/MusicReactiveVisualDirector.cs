using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Converts a privacy-preserving external-audio frame into a reactive space layer. The layer
    /// is behind gameplay, so it adds atmosphere without hiding bullets, trajectories, or HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicReactiveVisualDirector : MonoBehaviour
    {
        private enum MusicScene { Ambient, Verdant, Overdrive }

        private const int StarCount = 42;
        private const int RingCount = 3;
        private const int RingSegments = 72;

        private Camera targetCamera;
        private Transform root;
        private SpriteRenderer nebulaVeil;
        private SpriteRenderer[] stars;
        private SpriteRenderer[] rays;
        private LineRenderer[] rings;
        private Vector2[] starSeeds;
        private float visualTime;
        private float beatPulse;
        private Color originalBackground;
        private bool originalBackgroundKnown;

        public void Initialize(Camera camera)
        {
            targetCamera = camera;
            if (targetCamera != null && !originalBackgroundKnown)
            {
                originalBackground = targetCamera.backgroundColor;
                originalBackgroundKnown = true;
            }
            BuildIfNeeded();
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnDestroy()
        {
            RestoreBackground();
        }

        private void Update()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null && !originalBackgroundKnown)
            {
                originalBackground = targetCamera.backgroundColor;
                originalBackgroundKnown = true;
            }

            var frame = ExternalMusicAudioBridge.Poll();
            var active = MusicReactiveSettings.Enabled && !GameAudioSettings.MusicEnabled && frame.HasSignal;
            if (!active)
            {
                SetActive(false);
                return;
            }

            BuildIfNeeded();
            SetActive(true);
            visualTime += Time.unscaledDeltaTime;
            beatPulse = Mathf.Max(frame.Beat, Mathf.MoveTowards(beatPulse, 0f, Time.unscaledDeltaTime * 2.8f));
            var scene = Classify(frame);
            var palette = Palette(scene);
            var intensity = Mathf.Clamp01(.20f + frame.Energy * .78f + beatPulse * .22f);
            UpdateRoot();
            UpdateVeil(palette.primary, intensity);
            UpdateStars(palette, frame, intensity);
            UpdateRays(palette, frame, intensity);
            UpdateRings(palette, frame, intensity);
        }

        private void BuildIfNeeded()
        {
            if (root != null) return;
            root = new GameObject("External music reactive space").transform;
            root.SetParent(transform, false);
            root.gameObject.SetActive(false);

            var square = CreateSquareSprite();
            var soft = CreateSoftSprite();
            nebulaVeil = CreateRenderer("Reactive nebula veil", square, -92);
            nebulaVeil.transform.SetParent(root, false);
            nebulaVeil.transform.localScale = new Vector3(34f, 24f, 1f);

            stars = new SpriteRenderer[StarCount];
            starSeeds = new Vector2[StarCount];
            for (var i = 0; i < StarCount; i++)
            {
                stars[i] = CreateRenderer("Reactive star " + i.ToString("00"), soft, -7);
                stars[i].transform.SetParent(root, false);
                starSeeds[i] = new Vector2(Mathf.Repeat(i * .6180339f, 1f), Mathf.Repeat(i * .3819660f + .17f, 1f));
            }

            rays = new SpriteRenderer[7];
            for (var i = 0; i < rays.Length; i++)
            {
                rays[i] = CreateRenderer("Reactive distant ray " + i.ToString("00"), soft, -12);
                rays[i].transform.SetParent(root, false);
            }

            rings = new LineRenderer[RingCount];
            for (var i = 0; i < rings.Length; i++)
            {
                var line = new GameObject("Reactive shockwave " + (i + 1)).AddComponent<LineRenderer>();
                line.transform.SetParent(root, false);
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = RingSegments;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.textureMode = LineTextureMode.Stretch;
                line.sortingOrder = -6;
                line.numCornerVertices = 2;
                line.numCapVertices = 2;
                rings[i] = line;
            }
        }

        private void UpdateRoot()
        {
            if (targetCamera == null || root == null) return;
            var cameraPosition = targetCamera.transform.position;
            root.position = new Vector3(cameraPosition.x, cameraPosition.y, 0f);
        }

        private void UpdateVeil(Color color, float intensity)
        {
            nebulaVeil.color = new Color(color.r * .32f, color.g * .32f, color.b * .42f, .055f + intensity * .105f);
        }

        private void UpdateStars((Color primary, Color secondary, Color spark) palette, ExternalMusicFrame frame, float intensity)
        {
            for (var i = 0; i < stars.Length; i++)
            {
                var seed = starSeeds[i];
                var angular = seed.x * Mathf.PI * 2f + visualTime * (.08f + frame.Bass * .42f) * (i % 2 == 0 ? 1f : -1f);
                var drift = Mathf.Sin(visualTime * (.55f + seed.y) + seed.x * 19f) * (.13f + frame.Mid * .26f);
                var radius = Mathf.Lerp(2.0f, 10.5f, seed.y) + drift + beatPulse * (i % 3 == 0 ? .85f : .18f);
                stars[i].transform.localPosition = new Vector3(Mathf.Cos(angular) * radius, Mathf.Sin(angular) * radius, 0f);
                var flash = Mathf.Clamp01(.22f + frame.Treble * .62f + beatPulse * (i % 5 == 0 ? .65f : .12f));
                var color = Color.Lerp(palette.primary, palette.spark, seed.x);
                stars[i].color = new Color(color.r, color.g, color.b, flash * (.30f + intensity * .64f));
                var size = .020f + seed.y * .055f + frame.Treble * .045f + beatPulse * .035f;
                stars[i].transform.localScale = Vector3.one * size;
            }
        }

        private void UpdateRays((Color primary, Color secondary, Color spark) palette, ExternalMusicFrame frame, float intensity)
        {
            for (var i = 0; i < rays.Length; i++)
            {
                var fraction = i / (float)rays.Length;
                var angle = fraction * 360f + visualTime * (5f + frame.Treble * 21f);
                var radius = 4.7f + i * .72f + frame.Bass * 1.8f;
                var direction = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
                rays[i].transform.localPosition = direction * radius;
                rays[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                rays[i].transform.localScale = new Vector3(.025f + frame.Treble * .045f, 1.6f + intensity * 4.2f, 1f);
                var color = Color.Lerp(palette.secondary, palette.spark, i % 2);
                rays[i].color = new Color(color.r, color.g, color.b, .035f + intensity * (.05f + frame.Treble * .08f));
            }
        }

        private void UpdateRings((Color primary, Color secondary, Color spark) palette, ExternalMusicFrame frame, float intensity)
        {
            for (var ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                var line = rings[ringIndex];
                var travel = Mathf.Repeat(visualTime * (.30f + frame.Bass * .78f) + ringIndex / (float)rings.Length, 1f);
                var radius = 1.1f + travel * (4.4f + intensity * 2.8f) + beatPulse * .65f;
                line.startWidth = line.endWidth = .006f + intensity * .020f + (ringIndex == 0 ? beatPulse * .025f : 0f);
                var color = Color.Lerp(palette.primary, palette.spark, ringIndex / (float)(rings.Length - 1));
                line.startColor = line.endColor = new Color(color.r, color.g, color.b, .22f + intensity * .55f);
                for (var point = 0; point < RingSegments; point++)
                {
                    var angle = point / (float)RingSegments * Mathf.PI * 2f;
                    var wobble = Mathf.Sin(angle * (3f + ringIndex) + visualTime * (1.4f + frame.Mid * 4f)) * (.035f + frame.Treble * .14f);
                    line.SetPosition(point, new Vector3(Mathf.Cos(angle) * (radius + wobble), Mathf.Sin(angle) * (radius + wobble), 0f));
                }
            }
        }

        private static MusicScene Classify(ExternalMusicFrame frame)
        {
            if (frame.Energy > .48f && frame.Treble > frame.Bass * .72f) return MusicScene.Overdrive;
            if (frame.Mid > frame.Treble * 1.18f && frame.Bass > .17f) return MusicScene.Verdant;
            return MusicScene.Ambient;
        }

        private static (Color primary, Color secondary, Color spark) Palette(MusicScene scene)
        {
            return scene switch
            {
                MusicScene.Overdrive => (new Color(1f, .12f, .24f), new Color(1f, .33f, .06f), new Color(1f, .76f, .30f)),
                MusicScene.Verdant => (new Color(.18f, 1f, .48f), new Color(.06f, .62f, .42f), new Color(.68f, 1f, .78f)),
                _ => (new Color(.28f, .74f, 1f), new Color(.42f, .34f, 1f), new Color(.78f, .94f, 1f))
            };
        }

        private void SetActive(bool active)
        {
            if (root != null) root.gameObject.SetActive(active);
            if (!active) RestoreBackground();
        }

        private void RestoreBackground()
        {
            if (targetCamera != null && originalBackgroundKnown) targetCamera.backgroundColor = originalBackground;
        }

        private static SpriteRenderer CreateRenderer(string name, Sprite sprite, int sortingOrder)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Sprite CreateSquareSprite()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(.5f, .5f), 2f);
        }

        private static Sprite CreateSoftSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + .5f) / size * 2f - 1f;
                var dy = (y + .5f) / size * 2f - 1f;
                var alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
        }
    }
}
