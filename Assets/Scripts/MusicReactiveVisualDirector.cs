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

        // Keep this layer deliberately inexpensive: every object is created once and only its
        // cached geometry is updated. It needs to survive a busy Android fight without becoming
        // a visual or CPU tax.
        private const int StarCount = 54;
        private const int RippleDropCount = 16;
        private const int CausticMoteCount = 18;
        // The procedural full-screen nebula is the preferred look. Keep the legacy sprite
        // clouds disabled; the ring seam itself was in non-periodic angular shader formulas.
        private const int NebulaCloudCount = 0;
        // Keep the retired horizontal ribbons disabled alongside the legacy sprite clouds.
        private const int AuroraRibbonCount = 0;
        private const int RingCount = 3;
        // A dense track can launch a beat every .2 seconds. Keeping enough independent rings
        // prevents a still-visible ripple from being recycled and disappearing with a pop.
        private const int PulseRippleCount = 24;
        // Preserve the water look; only the birth region and lifespan change.
        private const float CentralSpawnRadius = .28f;
        private const float WaterLifetimeMultiplier = 1.5f;
        private const float DistantRingOpacity = .38f;
        private readonly System.Random rippleRandom = new System.Random();
        private const int RingSegments = 96;
        private const int PulseRingSegments = 64;
        private const int AuroraSegments = 64;
        // Silence must still look intentional: a dim pond in deep space, rather than a
        // completely empty background waiting for the first loud note.
        private const float MinimumReactiveOpacity = .42f;
        private const float BeatCooldown = .20f;
        private const float MinimumBpm = 58f;
        private const float MaximumBpm = 180f;
        private const float HighTempoBpm = 136f;
        private const float AmbientRippleMinInterval = 2.8f;
        private const float AmbientRippleMaxInterval = 4.6f;

        private Camera targetCamera;
        private MusicSpaceDistortion spaceDistortion;
        private Transform root;
        private Sprite squareSprite;
        private Sprite softSprite;
        private SpriteRenderer nebulaVeil;
        private SpriteRenderer rippleImpact;
        private SpriteRenderer riftCore;
        private SpriteRenderer[] nebulaClouds;
        private SpriteRenderer[] stars;
        private SpriteRenderer[] rays;
        private SpriteRenderer[] rippleDrops;
        private SpriteRenderer[] causticMotes;
        private LineRenderer[] auroraRibbons;
        private Gradient[] auroraGradients;
        private GradientColorKey[][] auroraColorKeys;
        private GradientAlphaKey[][] auroraAlphaKeys;
        private LineRenderer[] rippleGlows;
        private LineRenderer[] rings;
        private LineRenderer[] pulseRings;
        private LineRenderer[] pulseGlows;
        private Vector2[] starSeeds;
        private Vector2[] nebulaSeeds;
        private Vector2[] moteSeeds;
        private float[] rippleDropSeeds;
        private Vector2[] rippleDropTargets;
        private float[] rippleDropLastAges;
        private Vector3[][] ringPoints;
        private Vector3[][] auroraPoints;
        private Vector3[][] pulsePoints;
        private Vector2[] pulseOrigins;
        private float[] pulseBirthTimes;
        private float[] pulseLifetimes;
        private float[] pulseStrengths;
        private float[] pulseSeeds;
        private Material reactiveLineMaterial;
        private Material distantHazeMaterial;
        private float visualTime;
        private float travelTime, travelSpeed = 1f, jumpStrength, waterReach;
        private bool travelActive, travelPaused;
        private bool workshopSuppressed;
        private float beatPulse;
        private float beatFlash;
        private float previousBeat;
        private float lastBeatTime = -10f;
        private float lastTempoBeatTime = -10f;
        private float lastPulseTime = -10f;
        private float nextAmbientRippleTime;
        private float nextTempoPulseTime = float.PositiveInfinity;
        private float estimatedBpm = 100f;
        private int nextPulseIndex;
        private int pulseSequence;
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
        private MusicScene currentScene;
        private float sceneHoldTimer;
        private bool sceneKnown;
        private bool sandboxTimeMode;
        private bool gameplayPaused;
        private Vector2 courseCenter;
        private bool hasCourseCenter;
        public void SetCourseCenter(Vector2 worldCenter)
        {
            courseCenter=worldCenter;hasCourseCenter=true;
            UpdateRoot();
            spaceDistortion?.SetCourseCenter(worldCenter);
        }

        public void Initialize(Camera camera)
        {
            targetCamera = camera;
            if (targetCamera != null && !originalBackgroundKnown)
            {
                originalBackground = targetCamera.backgroundColor;
                originalBackgroundKnown = true;
            }
            EnsureSpaceDistortion();
            BuildIfNeeded();
        }

        /// <summary>Starts the five-shard refraction for the first boss encounter.</summary>
        public void TriggerMirrorBreak()
        {
            EnsureSpaceDistortion();
            spaceDistortion?.TriggerMirrorBreak();
        }

        public void EndMirrorBreak()
        {
            spaceDistortion?.EndMirrorBreak();
        }

        public void SetLivingRegion(bool active, int region, int nextRegion, float blend)
        {
            EnsureSpaceDistortion();
            spaceDistortion?.SetRegion(active, region, nextRegion, blend);
        }

        public void SetSpaceTravel(float speed, float jump, bool activeRun, bool pause, float reach)
        {
            travelSpeed = Mathf.Max(0f, speed);
            jumpStrength = Mathf.Clamp01(jump);
            travelActive = activeRun;
            travelPaused = pause;
            waterReach = Mathf.Max(OrbitSettings.Radius, reach);
        }

        public void SetWorkshopSuppressed(bool suppressed)
        {
            workshopSuppressed = suppressed;
            EnsureSpaceDistortion();
            if (targetCamera != null)
            {
                var distortions = targetCamera.GetComponents<MusicSpaceDistortion>();
                for (var i = 0; i < distortions.Length; i++)
                {
                    if (distortions[i] == null) continue;
                    distortions[i].SetActive(!suppressed);
                    distortions[i].enabled = !suppressed;
                }
            }
            if (suppressed) SetActive(false);
        }

        public void SetSandboxTimeMode(bool enabled)
        {
            sandboxTimeMode = enabled;
        }

        public void SetGameplayPaused(bool paused)
        {
            if (gameplayPaused == paused) return;
            gameplayPaused = paused;
            EnsureSpaceDistortion();
            spaceDistortion?.SetGameplayPaused(paused);
        }

        private void Awake()
        {
            waterReach = OrbitSettings.Radius;
            BuildIfNeeded();
        }

        private void OnDestroy()
        {
            RestoreBackground();
            if (spaceDistortion != null) spaceDistortion.SetActive(false);
            ReleaseRuntimeAssets();
        }

        private void Update()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null && !originalBackgroundKnown)
            {
                originalBackground = targetCamera.backgroundColor;
                originalBackgroundKnown = true;
            }
            EnsureSpaceDistortion();

            if (gameplayPaused) return;

            if (workshopSuppressed)
            {
                SetActive(false);
                return;
            }

            var deltaTime = sandboxTimeMode ? Time.deltaTime : Time.unscaledDeltaTime;
            travelTime += (travelPaused ? 0f : deltaTime) * travelSpeed;
            spaceDistortion?.SetTravel(travelTime, jumpStrength, travelActive, waterReach);
            var frame = ExternalMusicAudioBridge.Poll();
            var visualizerConfigured = MusicReactiveSettings.Enabled && !GameAudioSettings.MusicEnabled;
            SmoothAudioFrame(frame, deltaTime);
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
            visualTime += deltaTime;
            beatPulse = Mathf.MoveTowards(beatPulse, smoothedBeat, deltaTime * 2.2f);
            beatFlash = Mathf.MoveTowards(beatFlash, 0f, deltaTime * .85f);
            var visualFrame = new ExternalMusicFrame(true, smoothedEnergy, smoothedBass, smoothedMid, smoothedTreble, smoothedBeat);
            var scene = ResolveScene(Classify(visualFrame), deltaTime);
            var palette = SmoothPalette(Palette(scene), deltaTime);
            var audibleVolume = Mathf.Clamp01(Mathf.SmoothStep(.008f, .52f, smoothedEnergy) + beatPulse * .14f);
            // Keep a quiet track legible while preserving the loudness response. Without this
            // floor the line width collapsed to a sub-pixel at normal listening volumes.
            var volumeOpacity = Mathf.Lerp(MinimumReactiveOpacity, 1f, audibleVolume);
            var intensity = Mathf.Clamp01(.06f + smoothedEnergy * .80f + beatPulse * .16f);
            UpdateRoot();
            if (spaceDistortion != null)
                spaceDistortion.SetFrame(visualTime, smoothedEnergy, smoothedBass, smoothedMid,
                    smoothedTreble, beatPulse, intensity, volumeOpacity,
                    palette.primary, palette.secondary, palette.spark);
            UpdateVeil(palette.primary, intensity, volumeOpacity);
            UpdateNebulaClouds(palette, visualFrame, intensity, volumeOpacity);
            UpdateStars(palette, visualFrame, intensity, volumeOpacity);
            UpdateRays(palette, visualFrame, intensity, volumeOpacity);
            UpdateAuroraRibbons(palette, visualFrame, intensity, volumeOpacity);
            UpdateWaterRipples(palette, visualFrame, intensity, volumeOpacity);
            UpdatePulseRipples(palette, visualFrame, intensity, volumeOpacity);
            UpdateCausticMotes(palette, visualFrame, intensity, volumeOpacity);
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
            // A script hot-reload can leave the old child Transform alive while the C# arrays
            // have been reset. Treat the visual layer as an all-or-nothing object graph so an
            // Editor recompilation cannot turn a running music session into a NullReference loop.
            if (IsBuilt()) return;
            if (root != null) Destroy(root.gameObject);
            root = null;
            ReleaseRuntimeAssets();
            root = new GameObject("External music reactive space").transform;
            root.SetParent(transform, false);
            root.gameObject.SetActive(false);

            squareSprite = CreateSquareSprite();
            softSprite = CreateSoftSprite();
            var square = squareSprite;
            var soft = softSprite;
            nebulaVeil = CreateRenderer("Reactive nebula veil", square, -92);
            nebulaVeil.transform.SetParent(root, false);
            // Overscan the uniform veil so camera zoom/aspect changes can never expose its edge.
            nebulaVeil.transform.localScale = new Vector3(60f, 60f, 1f);

            // The veil gives the whole screen a coherent black-water base. The clouds are the
            // visible depth: their offset, slow parallax and imperfect overlap stop the music
            // palette from looking like a flat colour filter.
            nebulaClouds = new SpriteRenderer[NebulaCloudCount];
            nebulaSeeds = new Vector2[NebulaCloudCount];
            for (var i = 0; i < nebulaClouds.Length; i++)
            {
                nebulaClouds[i] = CreateRenderer("Reactive nebula cloud " + i.ToString("00"), soft, -91 + i % 2);
                nebulaClouds[i].transform.SetParent(root, false);
                nebulaSeeds[i] = new Vector2(Mathf.Repeat(i * .6180339f + .09f, 1f), Mathf.Repeat(i * .3819660f + .31f, 1f));
            }

            // A soft central impact sells the “drop into a cosmic pool” read before each
            // ripple expands. Two lenses give it a brighter wet core without blinding the arena.
            rippleImpact = CreateRenderer("Reactive water impact", soft, -5);
            rippleImpact.transform.SetParent(root, false);
            rippleImpact.transform.localPosition = Vector3.zero;
            riftCore = CreateRenderer("Reactive rift core", soft, -3);
            riftCore.transform.SetParent(root, false);
            riftCore.transform.localPosition = Vector3.zero;

            stars = new SpriteRenderer[StarCount];
            starSeeds = new Vector2[StarCount];
            for (var i = 0; i < StarCount; i++)
            {
                stars[i] = CreateRenderer("Reactive star " + i.ToString("00"), soft, -7);
                stars[i].transform.SetParent(root, false);
                starSeeds[i] = new Vector2(Mathf.Repeat(i * .6180339f, 1f), Mathf.Repeat(i * .3819660f + .17f, 1f));
            }

            rays = new SpriteRenderer[9];
            for (var i = 0; i < rays.Length; i++)
            {
                rays[i] = CreateRenderer("Reactive distant ray " + i.ToString("00"), soft, -12);
                rays[i].transform.SetParent(root, false);
            }

            rippleDrops = new SpriteRenderer[RippleDropCount];
            rippleDropSeeds = new float[RippleDropCount];
            rippleDropTargets = new Vector2[RippleDropCount];
            rippleDropLastAges = new float[RippleDropCount];
            for (var i = 0; i < RippleDropCount; i++)
            {
                rippleDrops[i] = CreateRenderer("Reactive rain drop " + i.ToString("00"), soft, -4);
                rippleDrops[i].transform.SetParent(root, false);
                rippleDropSeeds[i] = Mathf.Repeat(i * .41421356f + .11f, 1f);
                rippleDropTargets[i] = PulseOrigin(rippleDropSeeds[i], .34f + (i % 5) * .48f);
                rippleDropLastAges[i] = -1f;
            }

            causticMotes = new SpriteRenderer[CausticMoteCount];
            moteSeeds = new Vector2[CausticMoteCount];
            for (var i = 0; i < causticMotes.Length; i++)
            {
                causticMotes[i] = CreateRenderer("Reactive caustic mote " + i.ToString("00"), soft, -4);
                causticMotes[i].transform.SetParent(root, false);
                moteSeeds[i] = new Vector2(Mathf.Repeat(i * .7548777f + .13f, 1f), Mathf.Repeat(i * .5698403f + .29f, 1f));
            }

            var lineShader = Shader.Find("Sprites/Default");
            if (lineShader == null) lineShader = Shader.Find("Unlit/Color");
            if (lineShader == null)
            {
                Debug.LogWarning("[MusicReactive] No compatible line shader was found; using Unity's default line material.");
            }
            else
            {
                reactiveLineMaterial = new Material(lineShader);
            }
            var hazeShader = Resources.Load<Shader>("MusicDistantHaze");
            if (hazeShader != null) distantHazeMaterial = new Material(hazeShader);
            rings = new LineRenderer[RingCount];
            rippleGlows = new LineRenderer[RingCount];
            ringPoints = new Vector3[RingCount][];
            for (var i = 0; i < rings.Length; i++)
            {
                // Periodic geometry allows Unity to join the last and first segment smoothly.
                rings[i] = CreateReactiveLine("Reactive heart ripple " + (i + 1), RingSegments, -60, 2);
                rippleGlows[i] = CreateReactiveLine("Reactive heart glow " + (i + 1), RingSegments, -61, 4);
                if (distantHazeMaterial != null)
                {
                    rings[i].sharedMaterial = distantHazeMaterial;
                    rippleGlows[i].sharedMaterial = distantHazeMaterial;
                }
                ringPoints[i] = new Vector3[RingSegments];
            }

            // These wide, low-opacity curves only move at a glacial pace. They are the distant
            // aurora reflected on the surface, not an equaliser overlay.
            auroraRibbons = new LineRenderer[AuroraRibbonCount];
            auroraPoints = new Vector3[AuroraRibbonCount][];
            auroraGradients = new Gradient[AuroraRibbonCount];
            auroraColorKeys = new GradientColorKey[AuroraRibbonCount][];
            auroraAlphaKeys = new GradientAlphaKey[AuroraRibbonCount][];
            for (var i = 0; i < auroraRibbons.Length; i++)
            {
                auroraRibbons[i] = CreateReactiveLine("Reactive aurora ribbon " + (i + 1), AuroraSegments, -15 + i, 4, false);
                auroraPoints[i] = new Vector3[AuroraSegments];
                auroraGradients[i] = new Gradient();
                auroraColorKeys[i] = new GradientColorKey[3];
                auroraAlphaKeys[i] = new GradientAlphaKey[5];
            }

            // A pool of event-driven ripples is what makes this read as drops falling on a
            // cosmic pond. Each one owns an independent birth time and origin, so no two rings
            // restart in a mechanical lockstep.
            pulseRings = new LineRenderer[PulseRippleCount];
            pulseGlows = new LineRenderer[PulseRippleCount];
            pulsePoints = new Vector3[PulseRippleCount][];
            pulseOrigins = new Vector2[PulseRippleCount];
            pulseBirthTimes = new float[PulseRippleCount];
            pulseLifetimes = new float[PulseRippleCount];
            pulseStrengths = new float[PulseRippleCount];
            pulseSeeds = new float[PulseRippleCount];
            for (var i = 0; i < pulseRings.Length; i++)
            {
                pulseRings[i] = CreateReactiveLine("Reactive rain ripple " + (i + 1), PulseRingSegments, -4, 3);
                pulseGlows[i] = CreateReactiveLine("Reactive rain glow " + (i + 1), PulseRingSegments, -5, 5);
                pulsePoints[i] = new Vector3[PulseRingSegments];
                pulseOrigins[i] = CenteredPulseOrigin();
                pulseBirthTimes[i] = -i * 1.08f;
                pulseLifetimes[i] = 3.4f * WaterLifetimeMultiplier;
                pulseStrengths[i] = .28f + (i % 3) * .13f;
                pulseSeeds[i] = Mathf.Repeat(i * .41421356f + .19f, 1f);
            }

            nextAmbientRippleTime = 1.15f;
        }

        private bool IsBuilt()
        {
            if (root == null || nebulaVeil == null || rippleImpact == null || riftCore == null ||
                nebulaClouds == null || stars == null || rays == null || rippleDrops == null || causticMotes == null ||
                rippleDropTargets == null || rippleDropLastAges == null ||
                rings == null || rippleGlows == null || auroraRibbons == null || pulseRings == null || pulseGlows == null ||
                ringPoints == null || auroraPoints == null || pulsePoints == null ||
                auroraGradients == null || auroraColorKeys == null || auroraAlphaKeys == null)
                return false;

            if (nebulaClouds.Length != NebulaCloudCount || stars.Length != StarCount || rays.Length != 9 ||
                rippleDrops.Length != RippleDropCount || rippleDropTargets.Length != RippleDropCount ||
                rippleDropLastAges.Length != RippleDropCount || causticMotes.Length != CausticMoteCount ||
                rings.Length != RingCount || rippleGlows.Length != RingCount ||
                auroraRibbons.Length != AuroraRibbonCount || pulseRings.Length != PulseRippleCount ||
                pulseGlows.Length != PulseRippleCount || ringPoints.Length != RingCount ||
                auroraPoints.Length != AuroraRibbonCount || pulsePoints.Length != PulseRippleCount ||
                auroraGradients.Length != AuroraRibbonCount || auroraColorKeys.Length != AuroraRibbonCount ||
                auroraAlphaKeys.Length != AuroraRibbonCount)
                return false;

            for (var i = 0; i < AuroraRibbonCount; i++)
            {
                if (auroraRibbons[i] == null || auroraPoints[i] == null || auroraGradients[i] == null ||
                    auroraColorKeys[i] == null || auroraAlphaKeys[i] == null)
                    return false;
            }
            return true;
        }

        private void UpdateRoot()
        {
            if (targetCamera == null || root == null) return;
            var cameraPosition = targetCamera.transform.position;
            root.position = hasCourseCenter?new Vector3(courseCenter.x,courseCenter.y,0):new Vector3(cameraPosition.x,cameraPosition.y,0);
        }

        private void UpdateVeil(Color color, float intensity, float volumeOpacity)
        {
            // This must stay nearly black. The colour comes from soft clouds, otherwise the
            // visualizer reads as a translucent sheet over gameplay instead of a deep sky.
            var alpha = Mathf.Lerp(.008f, .052f, volumeOpacity) * (.78f + intensity * .22f);
            nebulaVeil.color = new Color(color.r * .18f, color.g * .20f, color.b * .30f, alpha);
        }

        private void UpdateNebulaClouds((Color primary, Color secondary, Color spark) palette,
            ExternalMusicFrame frame, float intensity, float volumeOpacity)
        {
            for (var i = 0; i < nebulaClouds.Length; i++)
            {
                var seed = nebulaSeeds[i];
                // These clouds live in side/top/bottom corridors and slide through them over
                // time. They therefore feel like volume the ship passes through, rather than
                // a static coloured vignette over the play field.
                var travel = Mathf.Repeat(seed.x + visualTime * (.025f + frame.Energy * .045f + seed.y * .018f), 1f);
                var lane = i % 4;
                var sway = Mathf.Sin(visualTime * (.24f + seed.y * .18f) + seed.x * 21f) * (.22f + frame.Bass * .42f);
                Vector2 position;
                if (lane == 0 || lane == 1)
                {
                    var side = lane == 0 ? -1f : 1f;
                    position = new Vector2(side * (5.95f + seed.y * 2.15f), Mathf.Lerp(-7.6f, 7.6f, travel) + sway);
                }
                else
                {
                    var vertical = lane == 2 ? 1f : -1f;
                    position = new Vector2(Mathf.Lerp(-9.4f, 9.4f, travel) + sway, vertical * (4.85f + seed.y * 1.18f));
                }
                nebulaClouds[i].transform.localPosition = new Vector3(position.x, position.y, 0f);
                nebulaClouds[i].transform.localRotation = Quaternion.Euler(0f, 0f,
                    seed.x * 360f + visualTime * (2.0f + seed.y * 2.6f));
                var scale = 4.8f + seed.y * 4.5f + frame.Bass * 1.45f + beatFlash * .45f;
                var elongated = lane == 0 || lane == 1 ? 1.52f : .88f;
                nebulaClouds[i].transform.localScale = new Vector3(scale * elongated, scale / elongated, 1f);
                var cloudColor = Color.Lerp(palette.primary, palette.secondary, seed.x);
                cloudColor = Color.Lerp(cloudColor, palette.spark, frame.Treble * .16f * (i % 2));
                var cloudAlpha = (.024f + intensity * .072f) * volumeOpacity * (.72f + seed.y * .46f);
                nebulaClouds[i].color = new Color(cloudColor.r, cloudColor.g, cloudColor.b, cloudAlpha);
            }
        }

        private void UpdateStars((Color primary, Color secondary, Color spark) palette, ExternalMusicFrame frame,
            float intensity, float volumeOpacity)
        {
            for (var i = 0; i < stars.Length; i++)
            {
                var seed = starSeeds[i];
                var angular = seed.x * Mathf.PI * 2f + travelTime * (.08f + frame.Bass * .42f) * (i % 2 == 0 ? 1f : -1f);
                var drift = Mathf.Sin(travelTime * (.55f + seed.y) + seed.x * 19f) * (.13f + frame.Mid * .26f);
                var radius = Mathf.Lerp(2.0f, 10.5f, seed.y) + drift + beatPulse * (i % 3 == 0 ? .85f : .18f);
                stars[i].transform.localPosition = new Vector3(Mathf.Cos(angular) * radius, Mathf.Sin(angular) * radius, 0f);
                var flash = Mathf.Clamp01(.28f + frame.Treble * .62f + beatPulse * (i % 5 == 0 ? .65f : .12f));
                var color = Color.Lerp(palette.primary, palette.spark, seed.x);
                var alpha = flash * (.16f + intensity * .72f) * Mathf.Lerp(.20f, 1f, volumeOpacity);
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
                var alpha = (.012f + intensity * (.052f + frame.Treble * .075f)) * Mathf.Lerp(.20f, 1f, volumeOpacity);
                rays[i].color = new Color(color.r, color.g, color.b, alpha);
            }
        }

        private void UpdateAuroraRibbons((Color primary, Color secondary, Color spark) palette,
            ExternalMusicFrame frame, float intensity, float volumeOpacity)
        {
            for (var ribbonIndex = 0; ribbonIndex < auroraRibbons.Length; ribbonIndex++)
            {
                var line = auroraRibbons[ribbonIndex];
                var points = auroraPoints[ribbonIndex];
                var upper = ribbonIndex != 1;
                var baseY = upper ? 4.8f + ribbonIndex * .46f : -4.35f;
                var direction = upper ? 1f : -1f;
                var waveAmplitude = .20f + frame.Mid * .42f + frame.Bass * .16f;
                var frequency = .48f + frame.Treble * .38f + ribbonIndex * .07f;
                for (var point = 0; point < AuroraSegments; point++)
                {
                    var fraction = point / (float)(AuroraSegments - 1);
                    var x = Mathf.Lerp(-10.8f, 10.8f, fraction);
                    var y = baseY + direction * (
                        Mathf.Sin(x * frequency + visualTime * (.21f + frame.Bass * .32f) + ribbonIndex * 1.9f) * waveAmplitude +
                        Mathf.Sin(x * (frequency * 1.83f) - visualTime * .14f + ribbonIndex) * waveAmplitude * .28f);
                    points[point] = new Vector3(x, y, 0f);
                }

                var color = Color.Lerp(palette.secondary, palette.primary, ribbonIndex / (float)(auroraRibbons.Length - 1));
                var alpha = (.008f + intensity * .052f) * volumeOpacity * (upper ? .72f : .52f);
                var colorKeys = auroraColorKeys[ribbonIndex];
                colorKeys[0] = new GradientColorKey(color, 0f);
                colorKeys[1] = new GradientColorKey(Color.Lerp(color, palette.spark, .36f), .48f);
                colorKeys[2] = new GradientColorKey(palette.spark, 1f);
                var alphaKeys = auroraAlphaKeys[ribbonIndex];
                alphaKeys[0] = new GradientAlphaKey(0f, 0f);
                alphaKeys[1] = new GradientAlphaKey(alpha * .58f, .14f);
                alphaKeys[2] = new GradientAlphaKey(alpha, .50f);
                alphaKeys[3] = new GradientAlphaKey(alpha * .54f, .86f);
                alphaKeys[4] = new GradientAlphaKey(0f, 1f);
                auroraGradients[ribbonIndex].SetKeys(colorKeys, alphaKeys);
                line.colorGradient = auroraGradients[ribbonIndex];
                line.startWidth = line.endWidth = (.020f + frame.Mid * .030f + beatFlash * .010f) * volumeOpacity;
                line.SetPositions(points);
            }
        }

        private void UpdateWaterRipples((Color primary, Color secondary, Color spark) palette,
            ExternalMusicFrame frame, float intensity, float volumeOpacity)
        {
            var waterPulse = Mathf.Clamp01(.09f + frame.Bass * .28f + beatPulse * .36f + beatFlash * .44f);
            rippleImpact.transform.localScale = Vector3.one * (.34f + waterPulse * .82f + frame.Energy * .16f);
            rippleImpact.color = new Color(palette.spark.r, palette.spark.g, palette.spark.b,
                (.016f + waterPulse * .28f) * Mathf.Lerp(.32f, .92f, volumeOpacity));
            riftCore.transform.localScale = Vector3.one * (.075f + waterPulse * .22f + frame.Treble * .035f);
            riftCore.color = new Color(1f, 1f, 1f, (.038f + waterPulse * .42f) * volumeOpacity);

            // Real rain precedes the ripple: every soft drop falls from above the arena onto a
            // different point of the invisible cosmic pond. A quiet landing occasionally owns
            // the next wave, so the image has a readable cause-and-effect even between beats.
            for (var i = 0; i < rippleDrops.Length; i++)
            {
                var seed = rippleDropSeeds[i];
                var age = Mathf.Repeat(visualTime * (.032f + frame.Bass * .040f) + seed, 1f);
                var approach = Mathf.SmoothStep(0f, 1f, age);
                var landing = rippleDropTargets[i];
                var entry = landing + new Vector2(
                    Mathf.Sin(seed * 43f) * (.22f + seed * .48f),
                    4.7f + (i % 4) * .42f);
                var drift = Mathf.Sin(age * Mathf.PI + seed * 17f) * (.08f + frame.Mid * .17f);
                var position = Vector2.Lerp(entry, landing, approach);
                position.x += drift;
                rippleDrops[i].transform.localPosition = position;
                var edgeFade = Mathf.Sin(age * Mathf.PI);
                var alpha = edgeFade * (.038f + volumeOpacity * .31f) * (i % 3 == 0 ? 1.2f : .72f);
                var dropColor = Color.Lerp(palette.spark, Color.white, .46f + frame.Treble * .32f);
                rippleDrops[i].color = new Color(dropColor.r, dropColor.g, dropColor.b, alpha);
                var size = .017f + edgeFade * (.022f + frame.Treble * .020f) + beatFlash * .012f;
                rippleDrops[i].transform.localScale = new Vector3(size * .66f, size * (2.0f + approach * .62f), 1f);
                rippleDrops[i].transform.localRotation = Quaternion.Euler(0f, 0f, drift * -55f);

                // Do not turn every droplet into a drum beat. A landing becomes a gentle
                // ambient wave only if music has not just launched its own stronger pulse.
                if (rippleDropLastAges[i] > .88f && age < .12f && visualTime > 1.2f &&
                    visualTime - lastPulseTime > 1.45f)
                    TriggerPulseAt(landing, .20f + intensity * .12f);
                rippleDropLastAges[i] = age;
            }

            // The rift itself has only a few slow, broad rings. They are a background tide;
            // noticeable drops are spawned below by UpdatePulseRipples.
            for (var ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                var line = rings[ringIndex];
                var travel = Mathf.Repeat(visualTime * (.048f + frame.Bass * .075f) + ringIndex * .337f, 1f);
                var crest = Mathf.Sin(travel * Mathf.PI);
                var expansion = 1f - Mathf.Pow(1f - travel, 2.18f);
                var radius = .58f + expansion * (4.25f + intensity * 1.65f) + beatPulse * .12f;

                // Tonality becomes gentle surface texture, never a saw tooth. Bright vocals or
                // guitars add crystalline detail; ambient music remains nearly circular.
                var tone = Mathf.Clamp01(frame.Treble * .72f + frame.Mid * .28f);
                var scream = Mathf.Clamp01(frame.Treble * .92f + frame.Energy * .70f - frame.Bass * .18f);
                var zigzagCount = Mathf.Lerp(2.15f, 6.65f, tone);
                var textureAmount = Mathf.Lerp(.007f, .052f, tone) * (.72f + scream * .36f);
                var color = Color.Lerp(palette.primary, palette.spark, ringIndex / (float)(rings.Length - 1));
                color = Color.Lerp(color, new Color(1f, .18f, .38f), scream * .18f);
                var alpha = crest * crest * (.045f + volumeOpacity * .42f + scream * .055f) * DistantRingOpacity;
                line.startWidth = line.endWidth = (.014f + crest * (.014f + intensity * .030f)) * Mathf.Lerp(.78f, 1f, volumeOpacity) * 3.2f;
                line.startColor = line.endColor = new Color(color.r, color.g, color.b, alpha);
                var glow = rippleGlows[ringIndex];
                glow.startWidth = glow.endWidth = line.startWidth * 3.2f;
                glow.startColor = glow.endColor = new Color(color.r, color.g, color.b, alpha * .16f * Mathf.Lerp(.42f, 1f, volumeOpacity));
                var points = ringPoints[ringIndex];
                for (var point = 0; point < RingSegments; point++)
                {
                    var angle = point / (float)RingSegments * Mathf.PI * 2f;
                    var wobble = PeriodicWave(angle, zigzagCount + ringIndex * .45f, visualTime * (.31f + frame.Mid * .72f)) * textureAmount;
                    wobble += PeriodicWave(angle, zigzagCount * 1.65f + ringIndex, -visualTime * .39f) * (textureAmount * .28f + frame.Bass * .010f);
                    var pointRadius = radius + wobble * crest;
                    points[point] = new Vector3(Mathf.Cos(angle) * pointRadius, Mathf.Sin(angle) * pointRadius, 0f);
                }
                line.SetPositions(points);
                glow.SetPositions(points);
            }
        }

        private void UpdatePulseRipples((Color primary, Color secondary, Color spark) palette,
            ExternalMusicFrame frame, float intensity, float volumeOpacity)
        {
            var beatRise = smoothedBeat - previousBeat;
            var musicBeat = Mathf.Max(smoothedBeat, frame.Beat);
            var bpm01 = Mathf.InverseLerp(72f, 166f, estimatedBpm);
            // Fast tracks need a shorter debounce so their genuine kicks do not get discarded;
            // slower tracks retain a larger gap that rejects double-trigger noise.
            var beatCooldown = Mathf.Lerp(.36f, .16f, bpm01);
            var hasBeat = frame.HasSignal && musicBeat > .13f && beatRise > .012f &&
                visualTime - lastBeatTime >= Mathf.Max(BeatCooldown, beatCooldown);
            if (hasBeat)
            {
                RegisterTempoBeat();
                bpm01 = Mathf.InverseLerp(72f, 166f, estimatedBpm);
                var strength = Mathf.Clamp01(.42f + musicBeat * .58f + frame.Energy * .18f);
                TriggerPulse(strength, true);
                lastBeatTime = visualTime;
                beatFlash = Mathf.Max(beatFlash, strength);
                // A brisk song gets a restrained off-beat ripple between kicks. It is not an
                // extra FFT pass: only the already-detected beat interval is used.
                nextTempoPulseTime = estimatedBpm >= HighTempoBpm
                    ? visualTime + (60f / estimatedBpm) * .5f
                    : float.PositiveInfinity;
            }
            else if (frame.HasSignal && estimatedBpm >= HighTempoBpm && visualTime >= nextTempoPulseTime &&
                visualTime - lastBeatTime >= .08f)
            {
                TriggerPulse(Mathf.Lerp(.22f, .38f, bpm01), true);
                nextTempoPulseTime = float.PositiveInfinity;
            }
            else if (!frame.HasSignal && visualTime >= nextAmbientRippleTime)
            {
                TriggerPulse(.22f + intensity * .18f, false);
            }
            previousBeat = smoothedBeat;

            var tone = Mathf.Clamp01(frame.Treble * .68f + frame.Mid * .32f);
            for (var rippleIndex = 0; rippleIndex < pulseRings.Length; rippleIndex++)
            {
                var age = visualTime - pulseBirthTimes[rippleIndex];
                var line = pulseRings[rippleIndex];
                var glow = pulseGlows[rippleIndex];
                if (age < 0f || age >= pulseLifetimes[rippleIndex])
                {
                    line.startColor = line.endColor = Color.clear;
                    glow.startColor = glow.endColor = Color.clear;
                    continue;
                }

                var progress = Mathf.Clamp01(age / pulseLifetimes[rippleIndex]);
                var crest = Mathf.Sin(progress * Mathf.PI);
                var strength = pulseStrengths[rippleIndex];
                // Every ring starts as a point deep inside the rift, gathers brightness while
                // approaching, then crosses the arena. The longer birth envelope removes the
                // old sudden pop and makes successive beats read as a travel tunnel.
                // Even quiet notes reach the ship path before their existing gentle fade ends.
                var tunnelTravel = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress / .78f), 1.18f);
                var radius = Mathf.Lerp(.018f, waterReach + CentralSpawnRadius, tunnelTravel);
                var appear = Mathf.SmoothStep(0f, .17f, progress);
                var dissolve = 1f - Mathf.SmoothStep(.74f, 1f, progress);
                var fade = appear * dissolve * Mathf.Lerp(.20f, 1f, volumeOpacity);
                var color = Color.Lerp(palette.primary, palette.spark, .42f + strength * .34f);
                color = Color.Lerp(color, Color.white, Mathf.Clamp01(frame.Treble * .34f + (1f - progress) * .24f));
                // These are the actual rings caused by falling drops. Give them a bright wet
                // contour and a dense bloom, independently from the calm background rings.
                var alpha = fade * (.42f + strength * 1.12f) * Mathf.Lerp(.88f, 1f, crest);
                line.transform.localPosition = pulseOrigins[rippleIndex];
                glow.transform.localPosition = pulseOrigins[rippleIndex];
                line.startWidth = line.endWidth = (.016f + strength * .038f + crest * .015f) *
                    Mathf.Lerp(.34f, 1f, appear) * Mathf.Lerp(.68f, 1f, dissolve) * Mathf.Lerp(.78f, 1f, volumeOpacity);
                line.startColor = line.endColor = new Color(color.r, color.g, color.b, alpha);
                glow.startWidth = glow.endWidth = line.startWidth * (5.5f + strength * 3.6f);
                glow.startColor = glow.endColor = new Color(color.r, color.g, color.b, alpha * .72f);

                var points = pulsePoints[rippleIndex];
                var seed = pulseSeeds[rippleIndex];
                var detail = Mathf.Lerp(2.1f, 5.7f, tone);
                var rippleTexture = (.004f + tone * .022f) * crest * appear * (1f - progress * .48f);
                for (var point = 0; point < PulseRingSegments; point++)
                {
                    var angle = point / (float)PulseRingSegments * Mathf.PI * 2f;
                    var wobble = PeriodicWave(angle, detail, seed * 18f + age * (.48f + frame.Mid * .72f)) * rippleTexture;
                    wobble += PeriodicWave(angle, detail * 1.79f, -seed * 11f) * rippleTexture * .34f;
                    var pointRadius = radius + wobble;
                    points[point] = new Vector3(Mathf.Cos(angle) * pointRadius, Mathf.Sin(angle) * pointRadius, 0f);
                }
                line.SetPositions(points);
                glow.SetPositions(points);
            }
        }

        private static float PeriodicWave(float angle, float frequency, float phase)
        {
            var harmonic = Mathf.Floor(frequency);
            return Mathf.Lerp(Mathf.Sin(angle * harmonic + phase),
                Mathf.Sin(angle * (harmonic + 1f) + phase), frequency - harmonic);
        }

        private void UpdateCausticMotes((Color primary, Color secondary, Color spark) palette,
            ExternalMusicFrame frame, float intensity, float volumeOpacity)
        {
            for (var i = 0; i < causticMotes.Length; i++)
            {
                var seed = moteSeeds[i];
                var direction = i % 2 == 0 ? 1f : -1f;
                var angle = seed.x * Mathf.PI * 2f + visualTime * (.16f + frame.Mid * .38f) * direction;
                var radius = 1.12f + seed.y * 4.45f + Mathf.Sin(visualTime * .42f + seed.x * 22f) * (.13f + frame.Bass * .24f);
                causticMotes[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .76f, 0f);
                var glint = Mathf.Clamp01(.16f + frame.Treble * .68f + beatFlash * (i % 4 == 0 ? .62f : .10f));
                var color = Color.Lerp(palette.secondary, palette.spark, seed.x);
                var alpha = glint * (.025f + intensity * .18f) * volumeOpacity * (i % 3 == 0 ? 1f : .54f);
                causticMotes[i].color = new Color(color.r, color.g, color.b, alpha);
                causticMotes[i].transform.localScale = Vector3.one * (.010f + seed.y * .026f + glint * .022f);
            }
        }

        private void TriggerPulse(float strength, bool musicDriven)
        {
            TriggerPulse(strength, musicDriven, null);
        }

        private void TriggerPulseAt(Vector2 origin, float strength)
        {
            TriggerPulse(strength, false, origin);
        }

        private void TriggerPulse(float strength, bool musicDriven, Vector2? originOverride)
        {
            // Longer lives must not cause a visible ripple to be recycled at a strong beat.
            var index = -1;
            for (var offset = 0; offset < PulseRippleCount; offset++)
            {
                var candidate = (nextPulseIndex + offset) % PulseRippleCount;
                if (visualTime - pulseBirthTimes[candidate] < pulseLifetimes[candidate]) continue;
                index = candidate;
                break;
            }
            if (index < 0) return;
            nextPulseIndex = (nextPulseIndex + 1) % PulseRippleCount;
            var seed = Mathf.Repeat(++pulseSequence * .6180339f + .13f, 1f);
            pulseOrigins[index] = CenteredPulseOrigin();
            pulseBirthTimes[index] = visualTime;
            pulseLifetimes[index] = Mathf.Lerp(4.8f, 3.35f, strength) * WaterLifetimeMultiplier;
            pulseStrengths[index] = strength;
            pulseSeeds[index] = seed;
            lastPulseTime = visualTime;
            if (spaceDistortion != null && root != null)
            {
                var splashColor = Color.Lerp(reactivePrimary, reactiveSpark, .36f + strength * .42f);
                spaceDistortion.PushImpact(root.TransformPoint(pulseOrigins[index]), strength, splashColor, WaterLifetimeMultiplier);
            }
            nextAmbientRippleTime = visualTime + Mathf.Lerp(AmbientRippleMinInterval, AmbientRippleMaxInterval,
                Mathf.Repeat(seed * 1.6180339f + .23f, 1f));
        }

        private Vector2 CenteredPulseOrigin()
        {
            // Local RNG cannot affect enemy spawns, bonuses or a co-op simulation seed.
            var angle = (float)rippleRandom.NextDouble() * Mathf.PI * 2f;
            var radius = Mathf.Sqrt((float)rippleRandom.NextDouble()) * CentralSpawnRadius;
            var worldOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            return worldOffset;
        }

        private void RegisterTempoBeat()
        {
            if (lastTempoBeatTime > -1f)
            {
                var interval = visualTime - lastTempoBeatTime;
                var shortestPlausibleBeat = 60f / MaximumBpm;
                var longestPlausibleBeat = 60f / MinimumBpm;
                if (interval >= shortestPlausibleBeat && interval <= longestPlausibleBeat)
                {
                    var instantBpm = Mathf.Clamp(60f / interval, MinimumBpm, MaximumBpm);
                    // Beat intervals naturally wobble a little; smoothing avoids visual density
                    // jumping from one kick to the next.
                    estimatedBpm = Mathf.Lerp(estimatedBpm, instantBpm, .24f);
                }
            }
            lastTempoBeatTime = visualTime;
        }

        private static Vector2 PulseOrigin(float seed, float radius)
        {
            var angle = seed * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .70f);
        }

        private static MusicScene Classify(ExternalMusicFrame frame)
        {
            if (frame.Energy > .48f && frame.Treble > frame.Bass * .72f) return MusicScene.Overdrive;
            if (frame.Mid > frame.Treble * 1.18f && frame.Bass > .17f) return MusicScene.Verdant;
            return MusicScene.Ambient;
        }

        private MusicScene ResolveScene(MusicScene requested, float deltaTime)
        {
            if (!sceneKnown)
            {
                currentScene = requested;
                sceneKnown = true;
                sceneHoldTimer = 1.25f;
                return currentScene;
            }

            sceneHoldTimer = Mathf.Max(0f, sceneHoldTimer - deltaTime);
            if (requested != currentScene && sceneHoldTimer <= 0f)
            {
                currentScene = requested;
                // Genre feel should evolve over bars, not flicker between frames when a vocal
                // briefly crosses a band threshold.
                sceneHoldTimer = 1.35f;
            }
            return currentScene;
        }

        private static (Color primary, Color secondary, Color spark) Palette(MusicScene scene)
        {
            return scene switch
            {
                MusicScene.Overdrive => (new Color(1f, .16f, .42f), new Color(1f, .48f, .10f), new Color(1f, .86f, .46f)),
                MusicScene.Verdant => (new Color(.10f, .92f, .56f), new Color(.08f, .52f, .64f), new Color(.70f, 1f, .82f)),
                _ => (new Color(.22f, .68f, 1f), new Color(.50f, .28f, 1f), new Color(.84f, .96f, 1f))
            };
        }

        private void SetActive(bool active)
        {
            if (root != null) root.gameObject.SetActive(active);
            if (spaceDistortion != null) spaceDistortion.SetActive(active);
            if (!active) RestoreBackground();
        }

        private void EnsureSpaceDistortion()
        {
            if (targetCamera == null) return;
            if (spaceDistortion == null || spaceDistortion.gameObject != targetCamera.gameObject)
                spaceDistortion = targetCamera.GetComponent<MusicSpaceDistortion>();
            if (spaceDistortion == null) spaceDistortion = targetCamera.gameObject.AddComponent<MusicSpaceDistortion>();
            spaceDistortion.Initialize(targetCamera);
            spaceDistortion.SetGameplayPaused(gameplayPaused);
        }

        private void RestoreBackground()
        {
            if (targetCamera != null && originalBackgroundKnown) targetCamera.backgroundColor = originalBackground;
        }

        private LineRenderer CreateReactiveLine(string name, int segments, int sortingOrder, int roundedVertices, bool loop = true)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(root, false);
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = segments;
            line.sharedMaterial = reactiveLineMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.sortingOrder = sortingOrder;
            line.numCornerVertices = roundedVertices;
            line.numCapVertices = roundedVertices;
            line.alignment = LineAlignment.View;
            return line;
        }

        private static void DestroyOwnedSprite(Sprite sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            Destroy(sprite);
            if (texture != null) Destroy(texture);
        }

        private void ReleaseRuntimeAssets()
        {
            if (reactiveLineMaterial != null) Destroy(reactiveLineMaterial);
            reactiveLineMaterial = null;
            if (distantHazeMaterial != null) Destroy(distantHazeMaterial);
            distantHazeMaterial = null;
            DestroyOwnedSprite(squareSprite);
            DestroyOwnedSprite(softSprite);
            squareSprite = null;
            softSprite = null;
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
