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
        private const int RippleDropCount = 12;
        private const int RingCount = 4;
        private const int RingSegments = 96;
        private const float MinimumReactiveOpacity = .34f;

        private Camera targetCamera;
        private Transform root;
        private SpriteRenderer nebulaVeil;
        private SpriteRenderer rippleImpact;
        private SpriteRenderer[] stars;
        private SpriteRenderer[] rays;
        private SpriteRenderer[] rippleDrops;
        private LineRenderer[] rippleGlows;
        private LineRenderer[] rings;
        private Vector2[] starSeeds;
        private float[] rippleDropSeeds;
        private float visualTime;
        private float beatPulse;
        private float smoothedEnergy;
        private float smoothedBass;
        private float smoothedMid;
        private float smoothedTreble;
        private float smoothedBeat;
        private bool paletteKnown;
        private Color reactivePrimary;
        private Color reactiveSecondary;
        private Color reactiveSpark;
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
            var visualizerConfigured = MusicReactiveSettings.Enabled && !GameAudioSettings.MusicEnabled;
            SmoothAudioFrame(frame, Time.unscaledDeltaTime);
            // Keep a calm idle layer alive while external capture is silent or waiting for
            // permission. Previously the whole hierarchy was disabled in that state, so a
            // paused track (or a temporary WASAPI/Android hand-off) made every ring disappear.
            // The ripple floor remains deliberately subtle and still follows volume when a
            // signal arrives.
            var active = visualizerConfigured;
            if (!active)
            {
                SetActive(false);
                return;
            }

            BuildIfNeeded();
            SetActive(true);
            visualTime += Time.unscaledDeltaTime;
            beatPulse = Mathf.MoveTowards(beatPulse, smoothedBeat, Time.unscaledDeltaTime * 2.2f);
            var visualFrame = new ExternalMusicFrame(true, smoothedEnergy, smoothedBass, smoothedMid, smoothedTreble, smoothedBeat);
            var scene = Classify(visualFrame);
            var palette = SmoothPalette(Palette(scene), Time.unscaledDeltaTime);
            var audibleVolume = Mathf.Clamp01(Mathf.SmoothStep(.008f, .52f, smoothedEnergy) + beatPulse * .14f);
            // Keep a quiet track legible while preserving the loudness response. Without this
            // floor the line width collapsed to a sub-pixel at normal listening volumes.
            var volumeOpacity = Mathf.Lerp(MinimumReactiveOpacity, 1f, audibleVolume);
            var intensity = Mathf.Clamp01(.06f + smoothedEnergy * .80f + beatPulse * .16f);
            UpdateRoot();
            UpdateVeil(palette.primary, intensity, volumeOpacity);
            UpdateStars(palette, visualFrame, intensity, volumeOpacity);
            UpdateRays(palette, visualFrame, intensity, volumeOpacity);
            UpdateWaterRipples(palette, visualFrame, intensity, volumeOpacity);
        }

        private void SmoothAudioFrame(ExternalMusicFrame frame, float deltaTime)
        {
            var targetEnergy = frame.HasSignal ? frame.Energy : 0f;
            var targetBass = frame.HasSignal ? frame.Bass : 0f;
            var targetMid = frame.HasSignal ? frame.Mid : 0f;
            var targetTreble = frame.HasSignal ? frame.Treble : 0f;
            var targetBeat = frame.HasSignal ? frame.Beat : 0f;
            smoothedEnergy = SmoothBand(smoothedEnergy, targetEnergy, deltaTime, 1.15f, .72f);
            smoothedBass = SmoothBand(smoothedBass, targetBass, deltaTime, .95f, .62f);
            smoothedMid = SmoothBand(smoothedMid, targetMid, deltaTime, .95f, .62f);
            smoothedTreble = SmoothBand(smoothedTreble, targetTreble, deltaTime, 1.05f, .66f);
            smoothedBeat = SmoothBand(smoothedBeat, targetBeat, deltaTime, 4.0f, 1.75f);
        }

        private static float SmoothBand(float current, float target, float deltaTime, float rise, float fall)
        {
            var rate = target > current ? rise : fall;
            return Mathf.MoveTowards(current, target, deltaTime * rate);
        }

        private (Color primary, Color secondary, Color spark) SmoothPalette(
            (Color primary, Color secondary, Color spark) target, float deltaTime)
        {
            if (!paletteKnown)
            {
                reactivePrimary = target.primary;
                reactiveSecondary = target.secondary;
                reactiveSpark = target.spark;
                paletteKnown = true;
            }

            var blend = 1f - Mathf.Exp(-deltaTime * 2.2f);
            reactivePrimary = Color.Lerp(reactivePrimary, target.primary, blend);
            reactiveSecondary = Color.Lerp(reactiveSecondary, target.secondary, blend);
            reactiveSpark = Color.Lerp(reactiveSpark, target.spark, blend);
            return (reactivePrimary, reactiveSecondary, reactiveSpark);
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

            // A soft central impact sells the “drop into a cosmic pool” read before each
            // ripple expands. The same soft sprite is reused for stars and droplets.
            rippleImpact = CreateRenderer("Reactive water impact", soft, -5);
            rippleImpact.transform.SetParent(root, false);
            rippleImpact.transform.localPosition = Vector3.zero;

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

            rippleDrops = new SpriteRenderer[RippleDropCount];
            rippleDropSeeds = new float[RippleDropCount];
            for (var i = 0; i < RippleDropCount; i++)
            {
                rippleDrops[i] = CreateRenderer("Reactive rain drop " + i.ToString("00"), soft, -4);
                rippleDrops[i].transform.SetParent(root, false);
                rippleDropSeeds[i] = Mathf.Repeat(i * .41421356f + .11f, 1f);
            }

            rings = new LineRenderer[RingCount];
            rippleGlows = new LineRenderer[RingCount];
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

                var glow = new GameObject("Reactive ripple glow " + (i + 1)).AddComponent<LineRenderer>();
                glow.transform.SetParent(root, false);
                glow.useWorldSpace = false;
                glow.loop = true;
                glow.positionCount = RingSegments;
                glow.material = new Material(Shader.Find("Sprites/Default"));
                glow.textureMode = LineTextureMode.Stretch;
                glow.sortingOrder = -7;
                glow.numCornerVertices = 3;
                glow.numCapVertices = 3;
                rippleGlows[i] = glow;
            }
        }

        private void UpdateRoot()
        {
            if (targetCamera == null || root == null) return;
            var cameraPosition = targetCamera.transform.position;
            root.position = new Vector3(cameraPosition.x, cameraPosition.y, 0f);
        }

        private void UpdateVeil(Color color, float intensity, float volumeOpacity)
        {
            var alpha = Mathf.Lerp(.012f, .19f, volumeOpacity) * (.72f + intensity * .28f);
            nebulaVeil.color = new Color(color.r * .32f, color.g * .32f, color.b * .42f, alpha);
        }

        private void UpdateStars((Color primary, Color secondary, Color spark) palette, ExternalMusicFrame frame,
            float intensity, float volumeOpacity)
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
                var alpha = flash * (.12f + intensity * .72f) * Mathf.Lerp(.16f, 1f, volumeOpacity);
                stars[i].color = new Color(color.r, color.g, color.b, alpha);
                var size = .020f + seed.y * .055f + frame.Treble * .045f + beatPulse * .035f;
                stars[i].transform.localScale = Vector3.one * size;
            }
        }

        private void UpdateRays((Color primary, Color secondary, Color spark) palette, ExternalMusicFrame frame,
            float intensity, float volumeOpacity)
        {
            for (var i = 0; i < rays.Length; i++)
            {
                var fraction = i / (float)rays.Length;
                var angle = fraction * 360f + visualTime * (.9f + frame.Treble * 4.8f);
                var radius = 4.7f + i * .72f + frame.Bass * 1.8f;
                var direction = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
                rays[i].transform.localPosition = direction * radius;
                rays[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                rays[i].transform.localScale = new Vector3(.025f + frame.Treble * .045f, 1.6f + intensity * 4.2f, 1f);
                var color = Color.Lerp(palette.secondary, palette.spark, i % 2);
                var alpha = (.008f + intensity * (.045f + frame.Treble * .075f)) * Mathf.Lerp(.18f, 1f, volumeOpacity);
                rays[i].color = new Color(color.r, color.g, color.b, alpha);
            }
        }

        private void UpdateWaterRipples((Color primary, Color secondary, Color spark) palette,
            ExternalMusicFrame frame, float intensity, float volumeOpacity)
        {
            var waterPulse = Mathf.Clamp01(.05f + frame.Bass * .30f + beatPulse * .58f);
            rippleImpact.transform.localScale = Vector3.one * (.18f + waterPulse * .56f + frame.Energy * .12f);
            rippleImpact.color = new Color(palette.spark.r, palette.spark.g, palette.spark.b,
                (.012f + waterPulse * .34f) * Mathf.Lerp(.20f, 1f, volumeOpacity));

            // Small luminous droplets fall toward the rift. They are deliberately dimmer than
            // the gameplay layer, but their parabolic approach makes the next ripple feel caused.
            for (var i = 0; i < rippleDrops.Length; i++)
            {
                var seed = rippleDropSeeds[i];
                var age = Mathf.Repeat(visualTime * (.06f + frame.Bass * .18f) + seed, 1f);
                var approach = Mathf.SmoothStep(0f, 1f, age);
                var angle = seed * Mathf.PI * 2f + visualTime * (.12f + frame.Mid * .34f);
                var outerRadius = 4.2f + (i % 3) * .72f + frame.Treble * .55f;
                var radius = Mathf.Lerp(outerRadius, .20f, approach);
                var x = Mathf.Cos(angle) * radius;
                var y = Mathf.Sin(angle) * radius - approach * approach * .20f;
                rippleDrops[i].transform.localPosition = new Vector3(x, y, 0f);
                var edgeFade = Mathf.Sin(age * Mathf.PI);
                var alpha = edgeFade * (.06f + volumeOpacity * .66f) * (i % 3 == 0 ? 1.2f : .72f);
                var dropColor = Color.Lerp(palette.spark, Color.white, .46f + frame.Treble * .32f);
                rippleDrops[i].color = new Color(dropColor.r, dropColor.g, dropColor.b, alpha);
                var size = .018f + edgeFade * (.018f + frame.Treble * .025f) + beatPulse * .012f;
                rippleDrops[i].transform.localScale = Vector3.one * size;
            }

            for (var ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                var line = rings[ringIndex];
                // The further a ripple travels, the more it loses energy. The power curve
                // deliberately gives distant lines a long, calm tail instead of a frantic loop.
                var travel = Mathf.Repeat(visualTime * (.10f + frame.Bass * .30f) + ringIndex / (float)rings.Length, 1f);
                var crest = Mathf.Sin(travel * Mathf.PI);
                var expansion = 1f - Mathf.Pow(1f - travel, 2.45f);
                var radius = .48f + expansion * (4.65f + intensity * 2.2f) + beatPulse * .35f;

                // Tonality becomes the “texture” of the ring: a soft ambient pad has a few
                // broad waves, while a bright vocal/guitar peak grows tighter zigzags and heat.
                var tone = Mathf.Clamp01(frame.Treble * .72f + frame.Mid * .28f);
                var scream = Mathf.Clamp01(frame.Treble * .92f + frame.Energy * .70f - frame.Bass * .18f);
                var zigzagCount = Mathf.Lerp(2.5f, 11.5f, tone);
                var textureAmount = Mathf.Lerp(.014f, .105f, tone) * (.72f + scream * .52f);
                var color = Color.Lerp(palette.primary, palette.spark, ringIndex / (float)(rings.Length - 1));
                color = Color.Lerp(color, new Color(1f, .10f, .06f), scream * .42f);
                var alpha = crest * crest * (.07f + volumeOpacity * .82f + scream * .10f);
                line.startWidth = line.endWidth = (.010f + crest * (.018f + intensity * .052f)) * Mathf.Lerp(.72f, 1f, volumeOpacity);
                line.startColor = line.endColor = new Color(color.r, color.g, color.b, alpha);
                var glow = rippleGlows[ringIndex];
                glow.startWidth = glow.endWidth = line.startWidth * (4.6f + waterPulse * 2.8f);
                glow.startColor = glow.endColor = new Color(color.r, color.g, color.b, alpha * .32f * Mathf.Lerp(.40f, 1f, volumeOpacity));
                for (var point = 0; point < RingSegments; point++)
                {
                    var angle = point / (float)RingSegments * Mathf.PI * 2f;
                    // Harmonics keep the ring organic like a puddle ripple. The main frequency
                    // is smoothed audio-driven tonality, so its zigzag count changes musically
                    // without the eye-jarring frame-to-frame texture flicker.
                    var wobble = Mathf.Sin(angle * (zigzagCount + ringIndex * .45f) + visualTime * (.45f + frame.Mid * 1.45f)) * textureAmount;
                    wobble += Mathf.Sin(angle * (zigzagCount * 1.65f + ringIndex) - visualTime * .62f) * (textureAmount * .34f + frame.Bass * .018f);
                    var pointRadius = radius + wobble * crest;
                    var pointPosition = new Vector3(Mathf.Cos(angle) * pointRadius, Mathf.Sin(angle) * pointRadius, 0f);
                    line.SetPosition(point, pointPosition);
                    glow.SetPosition(point, pointPosition);
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
