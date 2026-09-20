using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// One inexpensive full-screen pass for the external-music mode. It does not draw another
    /// ring over the game: it bends the already rendered scene around every music impact, then
    /// lets coloured storm clouds travel along the safe side corridors of the arena.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class MusicSpaceDistortion : MonoBehaviour
    {
        private const int MaxImpacts = 6;
        private static readonly int ImpactsId = Shader.PropertyToID("_Impacts");
        private static readonly int ImpactColorsId = Shader.PropertyToID("_ImpactColors");
        private static readonly int MusicId = Shader.PropertyToID("_Music");
        private static readonly int PrimaryId = Shader.PropertyToID("_NebulaPrimary");
        private static readonly int SecondaryId = Shader.PropertyToID("_NebulaSecondary");
        private static readonly int SparkId = Shader.PropertyToID("_NebulaSpark");
        private static readonly int TimePhaseId = Shader.PropertyToID("_TimePhase");
        private static readonly int WarpStrengthId = Shader.PropertyToID("_WarpStrength");
        private static readonly int MirrorId = Shader.PropertyToID("_MirrorBreak");
        private static readonly int FlightId = Shader.PropertyToID("_Flight");
        private static readonly int RippleReachId = Shader.PropertyToID("_RippleReach");
        private static readonly int MusicCenterId = Shader.PropertyToID("_MusicCenter");

        private readonly Vector4[] impacts = new Vector4[MaxImpacts];
        private readonly Vector4[] impactColors = new Vector4[MaxImpacts];
        private readonly Vector2[] impactCenterOffsets = new Vector2[MaxImpacts];
        private Vector2 courseCenter;
        private bool hasCourseCenter;
        public void SetCourseCenter(Vector2 worldCenter){courseCenter=worldCenter;hasCourseCenter=true;}
        public Vector2 CourseViewportCenter => hasCourseCenter&&targetCamera!=null?(Vector2)targetCamera.WorldToViewportPoint(courseCenter):Vector2.one*.5f;
        private readonly float[] impactBirthTimes = new float[MaxImpacts];
        private readonly float[] impactLifetimes = new float[MaxImpacts];
        private Camera targetCamera;
        private Material material;
        private bool active;
        private bool travelActive;
        private void Awake() { waterReach = OrbitSettings.Radius; }
        private float travelTime, jumpStrength, waterReach;
        private float visualTime;
        private float energy;
        private float bass;
        private float mid;
        private float treble;
        private float beat;
        private float warpStrength;
        private Color primary;
        private Color secondary;
        private Color spark;
        private float mirrorBreakStartedAt = -10f;
        private float mirrorBreakSeed;
        private bool mirrorBreakActive;
        private SpaceVisualProfile blueRegion, amberRegion;
        private bool regionActive;
        private Color regionPrimary, regionSecondary, regionSpark;
        private Vector4 regionShape;
        private bool gameplayPaused;
        private float gameplayPauseStartedAt = -10f;
        private float gameplayPausedDuration;

        public void SetRegion(bool enabledRegion, int region, int nextRegion, float blend)
        {
            regionActive = enabledRegion;
            if (!regionActive) return;
            if (blueRegion == null) blueRegion = Resources.Load<SpaceVisualProfile>("SpaceBlueFrontier");
            if (amberRegion == null) amberRegion = Resources.Load<SpaceVisualProfile>("SpaceAmberFront");
            if (blueRegion == null || amberRegion == null) { regionActive = false; return; }
            var from = region == 0 ? blueRegion : amberRegion;
            var to = nextRegion == 0 ? blueRegion : amberRegion;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));
            regionPrimary = Color.Lerp(from.primary, to.primary, t);
            regionSecondary = Color.Lerp(from.secondary, to.secondary, t);
            regionSpark = Color.Lerp(from.spark, to.spark, t);
            regionShape = new Vector4(Mathf.Lerp(from.cloudScale, to.cloudScale, t),
                Mathf.Lerp(from.density, to.density, t), Mathf.Lerp(from.shear, to.shear, t), 1f);
        }

        public void Initialize(Camera camera)
        {
            targetCamera = camera != null ? camera : GetComponent<Camera>();
            EnsureMaterial();
        }

        public void SetActive(bool value)
        {
            active = value;
        }

        public void SetGameplayPaused(bool paused)
        {
            if (gameplayPaused == paused) return;
            if (paused)
                gameplayPauseStartedAt = Time.unscaledTime;
            else if (gameplayPauseStartedAt >= 0f)
            {
                gameplayPausedDuration += Time.unscaledTime - gameplayPauseStartedAt;
                gameplayPauseStartedAt = -10f;
            }
            gameplayPaused = paused;
        }

        public void SetTravel(float time, float jump, bool activeRun, float reach)
        {
            travelTime = time;
            jumpStrength = jump;
            travelActive = activeRun;
            waterReach = reach;
        }

        public void SetFrame(float time, float energyValue, float bassValue, float midValue,
            float trebleValue, float beatValue, float intensity, float opacity,
            Color primaryColor, Color secondaryColor, Color sparkColor)
        {
            active = true;
            visualTime = time;
            energy = Mathf.Clamp01(energyValue);
            bass = Mathf.Clamp01(bassValue);
            mid = Mathf.Clamp01(midValue);
            treble = Mathf.Clamp01(trebleValue);
            beat = Mathf.Clamp01(beatValue);
            // Impacts must remain readable even on a quiet ambient track. Loudness scales the
            // spectacle upward, but never collapses the actual refraction to a sub-pixel.
            var musicalDrive = Mathf.Clamp01(intensity * opacity + bass * .16f + beat * .20f);
            warpStrength = Mathf.Lerp(.34f, 1f, musicalDrive);
            primary = primaryColor;
            secondary = secondaryColor;
            spark = sparkColor;
        }

        public void PushImpact(Vector2 worldPosition, float strength, Color color, float lifetimeMultiplier = 1f)
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) return;

            var viewport = targetCamera.WorldToViewportPoint(new Vector3(worldPosition.x, worldPosition.y, 0f));
            if (viewport.z <= 0f || viewport.x < -.1f || viewport.x > 1.1f || viewport.y < -.1f || viewport.y > 1.1f)
                return;

            var slot = -1;
            for (var i = 0; i < MaxImpacts; i++)
            {
                var progress = impactLifetimes[i] <= 0f ? 1f : (visualTime - impactBirthTimes[i]) / impactLifetimes[i];
                if (progress >= 1f)
                {
                    slot = i;
                    break;
                }
            }
            // A full refraction pool can skip one beat without cutting an existing wave.
            if (slot < 0) return;

            var clampedStrength = Mathf.Clamp01(strength);
            impacts[slot] = new Vector4(viewport.x, viewport.y, 1f, clampedStrength);
            impactCenterOffsets[slot]=worldPosition-(hasCourseCenter?courseCenter:(Vector2)targetCamera.transform.position);
            impactColors[slot] = color;
            impactBirthTimes[slot] = visualTime;
            impactLifetimes[slot] = Mathf.Lerp(2.7f, 1.75f, clampedStrength) * Mathf.Max(1f, lifetimeMultiplier);
        }

        /// <summary>
        /// A cosmetic, non-damaging five-shard screen refraction. It runs independently from
        /// external music and remains active for the full first-boss encounter.
        /// </summary>
        public void TriggerMirrorBreak()
        {
            mirrorBreakActive = true;
            mirrorBreakStartedAt = GameplayVisualTime();
            mirrorBreakSeed = Random.value * 97.31f;
        }

        public void EndMirrorBreak()
        {
            mirrorBreakActive = false;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var mirrorElapsed = GameplayVisualTime() - mirrorBreakStartedAt;
            var mirrorActive = mirrorBreakActive && mirrorElapsed >= 0f;
            if ((!active && !mirrorActive && !travelActive) || !EnsureMaterial() || material == null || material.shader == null || !material.shader.isSupported)
            {
                Graphics.Blit(source, destination);
                return;
            }

            for (var i = 0; i < MaxImpacts; i++)
            {
                var world=(hasCourseCenter?courseCenter:(Vector2)targetCamera.transform.position)+impactCenterOffsets[i];
                var viewport=targetCamera.WorldToViewportPoint(world);
                impacts[i].x=viewport.x;impacts[i].y=viewport.y;
                var life = impactLifetimes[i];
                var progress = life <= 0f ? 2f : (visualTime - impactBirthTimes[i]) / life;
                impacts[i].z = active && progress >= 0f && progress < 1f ? progress : -1f;
            }

            material.SetVectorArray(ImpactsId, impacts);
            var musicCenter=CourseViewportCenter;
            material.SetVector(MusicCenterId,new Vector4(musicCenter.x,musicCenter.y,0,0));
            material.SetVectorArray(ImpactColorsId, impactColors);
            material.SetVector(MusicId, active ? new Vector4(energy, bass, mid, treble) : Vector4.zero);
            var primaryColor = active ? primary : new Color(.16f, .48f, .85f);
            var secondaryColor = active ? secondary : new Color(.32f, .16f, .62f);
            var sparkColor = active ? spark : new Color(.52f, .88f, 1f);
            material.SetColor(PrimaryId, regionActive ? Color.Lerp(regionPrimary, primaryColor, active ? .3f : 0f) : primaryColor);
            material.SetColor(SecondaryId, regionActive ? Color.Lerp(regionSecondary, secondaryColor, active ? .3f : 0f) : secondaryColor);
            material.SetColor(SparkId, regionActive ? Color.Lerp(regionSpark, sparkColor, active ? .3f : 0f) : sparkColor);
            material.SetVector("_RegionShape", regionActive ? regionShape : Vector4.zero);
            material.SetFloat(TimePhaseId, active ? visualTime : travelTime);
            material.SetFloat(WarpStrengthId, active ? warpStrength + beat * .15f : 0f);
            material.SetVector(FlightId, new Vector4(jumpStrength, travelTime, travelActive ? 1f : 0f, 0f));
            var reachInViewport = targetCamera != null && targetCamera.orthographic
                ? waterReach / (2f * targetCamera.orthographicSize) : .4f;
            material.SetFloat(RippleReachId, reachInViewport);
            material.SetVector(MirrorId, new Vector4(mirrorActive ? mirrorElapsed : -1f, mirrorActive ? .94f : 0f, mirrorBreakSeed, 0f));
            Graphics.Blit(source, destination, material);
        }

        private float GameplayVisualTime()
        {
            var now = gameplayPaused ? gameplayPauseStartedAt : Time.unscaledTime;
            return now - gameplayPausedDuration;
        }

        private bool EnsureMaterial()
        {
            if (material != null) return true;
            var shader = Resources.Load<Shader>("MusicSpaceWaterDistortion");
            if (shader == null)
            {
                enabled = false;
                return false;
            }
            material = new Material(shader) { hideFlags = HideFlags.DontSave };
            return true;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
