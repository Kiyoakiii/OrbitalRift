using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Shared nine-layer scene treatment for every sandbox ability and seven-phase treatment
    /// for every sandbox shot.  The pools are fixed so visual polish never becomes a source of
    /// per-frame GameObject churn.  Low quality keeps the gameplay core and telegraph, while
    /// dropping the expensive background, particles and post-process proxy.
    /// </summary>
    public sealed class SandboxLayeredVfx : MonoBehaviour
    {
        private const int MaxLines = 240;
        private const int MaxSprites = 72;
        private const int MaxShots = 36;
        // Keep the animation plate on a private world layer. OnGUI controls do not use a
        // camera layer, so the workshop camera can render this layer exclusively and exclude
        // every star, arena ring, projectile, and editor preview sprite.
        private const int WorkshopLayer = 31;

        // These are the same isolated Phoenix layer images shown in the workshop cards.
        // The runtime preview must use this set too; the old LineRenderer-only treatment
        // was a separate approximation and could never match the evidence thumbnails.
        private static readonly string[] SolarPhotoLayerResources =
        {
            "SandboxVfxLayers/SolarChicks/Runtime/layer0-core",
            "SandboxVfxLayers/SolarChicks/Runtime/layer1-ribbons",
            "SandboxVfxLayers/SolarChicks/Runtime/layer2-trail",
            "SandboxVfxLayers/SolarChicks/Runtime/layer3-particles",
            "SandboxVfxLayers/SolarChicks/Runtime/layer4-noise",
            "SandboxVfxLayers/SolarChicks/Runtime/layer5-glow",
            "SandboxVfxLayers/SolarChicks/Runtime/layer6-impact",
            "SandboxVfxLayers/SolarChicks/Runtime/layer7-decal"
        };

        private sealed class ShotFx
        {
            public Vector2 Origin;
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public Sprite Core;
            public AbilitySandboxAbilityId Profile;
            public float Age;
            public float FlightTime;
            public float Seed;
        }

        private struct VfxOverride
        {
            public int LayerMask;
            public float Scale;
            public float Brightness;
            public float Glow;
        }

        private readonly List<LineRenderer> lines = new List<LineRenderer>(MaxLines);
        private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>(MaxSprites);
        private readonly List<ShotFx> shots = new List<ShotFx>(MaxShots);
        private readonly Dictionary<AbilitySandboxAbilityId, VfxOverride> overrides =
            new Dictionary<AbilitySandboxAbilityId, VfxOverride>();
        private Transform root;
        private SpriteRenderer workshopBackdrop;
        private Sprite workshopBackdropSprite;
        private Material material;
        private Material photoMaterial;
        private Sprite circleSprite;
        private Sprite fallbackSprite;
        private readonly Sprite[] solarPhotoLayers = new Sprite[SandboxVfxLayerEditor.LayerCount];
        private readonly Texture2D[] solarPhotoTextures = new Texture2D[SandboxVfxLayerEditor.LayerCount];
        private bool solarPhotoLayersLoaded;
        private int lineCursor;
        private int spriteCursor;
        private float clock;
        private float abilityAge;
        private float abilityDuration;
        private Vector2 abilityOrigin;
        private Vector2 abilityTarget;
        private Color abilityColor;
        private Sprite abilityCore;
        private AbilitySandboxAbilityId abilityId;
        private bool abilityActive;
        private bool lowSettings;
        private bool editorPreview;
        private AbilitySandboxAbilityId editorAbilityId;
        private Vector2 editorOrigin;
        private Vector2 editorTarget;
        private Color editorColor;
        private Sprite editorCore;
        private int editorLayerMask = SandboxVfxLayerEditor.AllLayers;
        private float editorScale = 1f;
        private float editorBrightness = 1f;
        private float editorGlow = 1f;
        private bool styleOverrideActive;
        private bool committedPreviewActive;
        private SpellVfxPreview solarPrefabPreview;

        /// <summary>Bit mask accumulated across the last sandbox session: bits 0..6 are the seven shot phases.</summary>
        public int LifetimeShotPhaseMask { get; private set; }

        public int ActiveShotCount => shots.Count;

        public void CommitEditorPreview(AbilitySandboxAbilityId id, int layerMask, float scale,
            float brightness, float glow)
        {
            overrides[id] = new VfxOverride
            {
                LayerMask = layerMask,
                Scale = Mathf.Clamp(scale, .55f, 2.5f),
                Brightness = Mathf.Clamp(brightness, .20f, 2.5f),
                Glow = Mathf.Clamp(glow, 0f, 2.5f)
            };
            editorAbilityId = id;
            editorLayerMask = layerMask;
            editorScale = scale;
            editorBrightness = brightness;
            editorGlow = glow;
            committedPreviewActive = true;
        }

        public void SetEditorPreview(AbilitySandboxAbilityId id, Vector2 origin, Vector2 target, Color color,
            Sprite core, int layerMask, float scale, float brightness, float glow)
        {
            if (!editorPreview)
            {
                shots.Clear();
                abilityActive = false;
            }
            editorPreview = true;
            editorAbilityId = id;
            editorOrigin = origin;
            editorTarget = target;
            editorColor = color;
            editorCore = core != null ? core : fallbackSprite;
            editorLayerMask = layerMask;
            editorScale = Mathf.Clamp(scale, .55f, 2.5f);
            editorBrightness = Mathf.Clamp(brightness, .20f, 2.5f);
            editorGlow = Mathf.Clamp(glow, 0f, 2.5f);
            committedPreviewActive = false;
        }

        public void ClearEditorPreview()
        {
            editorPreview = false;
            if(workshopBackdrop!=null)workshopBackdrop.enabled=false;
            styleOverrideActive = false;
            shots.Clear();
            abilityActive = false;
        }

        public void Configure(Transform arena, Sprite circle, Sprite fallback)
        {
            circleSprite = circle;
            fallbackSprite = fallback;
            if (root != null)
            {
                // Domain reloads can preserve the runtime root while the
                // workshop is rebuilt. Re-assert the isolated UI layer for
                // every existing child so a stale child cannot leak back into
                // the animation plate or the classic arena.
                root.gameObject.layer = WorkshopLayer;
                SetLayerRecursively(root, WorkshopLayer);
                return;
            }
            root = new GameObject("Sandbox layered VFX").transform;
            root.SetParent(arena, false);
            root.gameObject.layer = WorkshopLayer;
            workshopBackdrop = new GameObject("Workshop clean animation plate").AddComponent<SpriteRenderer>();
            workshopBackdrop.transform.SetParent(root, false);
            workshopBackdrop.gameObject.layer = WorkshopLayer;
            workshopBackdropSprite=UnityEngine.Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f,Texture2D.whiteTexture.width);
            workshopBackdrop.sprite = workshopBackdropSprite;
            workshopBackdrop.enabled = false;
            workshopBackdrop.color = Color.black;
            workshopBackdrop.sortingOrder = 0;
            workshopBackdrop.transform.localPosition = new Vector3(0f, 0f, .12f);
            workshopBackdrop.transform.localScale = new Vector3(30f, 30f, 1f);
            material = new Material(Shader.Find("Sprites/Default")) { name = "Sandbox layered energy" };
            var transparentShader = Shader.Find("OrbitalRift/Transparent VFX Key") ?? Shader.Find("Sprites/Default");
            photoMaterial = new Material(transparentShader) { name = "Sandbox transparent VFX photos" };
            if (photoMaterial.HasProperty("_Cutoff")) photoMaterial.SetFloat("_Cutoff", .075f);
        }

        public void BeginAbility(AbilitySandboxAbilityId id, Vector2 origin, Vector2 target, Color color, Sprite core)
        {
            abilityId = id;
            abilityOrigin = origin;
            abilityTarget = target;
            abilityColor = color;
            abilityCore = core != null ? core : fallbackSprite;
            committedPreviewActive = false;
            abilityAge = 0f;
            abilityDuration = id == AbilitySandboxAbilityId.AshenEgg ? 2.4f : 1.85f;
            abilityActive = true;
        }

        public void EmitShot(Vector2 origin, Vector2 velocity, Color color, Sprite core)
        {
            if (root == null || velocity.sqrMagnitude < .0001f) return;
            if (shots.Count >= MaxShots) shots.RemoveAt(0);
            var speed = Mathf.Max(.1f, velocity.magnitude);
            shots.Add(new ShotFx
            {
                Origin = origin,
                Position = origin,
                Velocity = velocity,
                Color = color,
                Core = core != null ? core : fallbackSprite,
                // Only tag shots that use the active ability's core. Persistent boss/clone
                // fire that happens between casts keeps the generic seven-phase profile.
                Profile = core != null && core == abilityCore ? abilityId : AbilitySandboxAbilityId.VectorSnap,
                Age = 0f,
                FlightTime = Mathf.Clamp(2.2f / speed, .44f, .94f),
                Seed = Random.value * 50f
            });
        }

        public void Tick(float dt, Vector2 player, Vector2 boss, float orbitRadius,
            bool solarPlume, bool aegisOrbit, bool echoResonator, bool emberCore, bool kineticOverdrive,
            bool ghostTrail)
        {
            if (root == null) return;
            clock += dt;
            lowSettings = QualitySettings.GetQualityLevel() <= 1;
            root.gameObject.SetActive(true);
            // The opaque plate belongs only to the isolated editor preview.
            workshopBackdrop.enabled = editorPreview;
            lineCursor = 0;
            spriteCursor = 0;

            if (editorPreview || committedPreviewActive)
            {
                styleOverrideActive = true;
                abilityColor = editorColor;
                // The workshop is an isolated effect plate.  Do not leak the
                // classic arena's orbit rings into the layer comparison view.
                if(editorAbilityId == AbilitySandboxAbilityId.SolarChicks)
                {
                    if(solarPrefabPreview == null)
                    {
                        var prefab=Resources.Load<SpellProjectileVfx>("Spells/SolarChicks/Prefabs/SolarChick");
                        if(prefab != null)
                        {
                            solarPrefabPreview=new GameObject("Solar Chicks prefab preview").AddComponent<SpellVfxPreview>();
                            solarPrefabPreview.transform.SetParent(root,false);solarPrefabPreview.Configure(prefab);
                        }
                    }
                    if(solarPrefabPreview != null) solarPrefabPreview.Show(dt,editorScale,editorBrightness,editorGlow,editorLayerMask);
                    else RenderEditorPreview();
                }
                else
                {
                    if(solarPrefabPreview != null)solarPrefabPreview.Hide();
                    RenderEditorPreview();
                }
            }
            else
            {
                styleOverrideActive = false;
                RenderBackground(orbitRadius);
                if(solarPrefabPreview != null)solarPrefabPreview.Hide();
                RenderPassives(player, boss, solarPlume, aegisOrbit, echoResonator, emberCore, kineticOverdrive, ghostTrail);
                if (abilityActive)
                {
                    abilityAge += dt;
                    BeginStyleOverride(abilityId, abilityOrigin, abilityTarget);
                    RenderAbility(player, boss, orbitRadius);
                    styleOverrideActive = false;
                    if (abilityAge >= abilityDuration) abilityActive = false;
                }
                TickShots(dt);
            }

            for (var i = lineCursor; i < lines.Count; i++) lines[i].enabled = false;
            for (var i = spriteCursor; i < sprites.Count; i++) sprites[i].enabled = false;
            if (!editorPreview && !committedPreviewActive && !abilityActive && shots.Count == 0 && !HasPassives(solarPlume, aegisOrbit, echoResonator, emberCore, kineticOverdrive, ghostTrail))
                root.gameObject.SetActive(false);
        }

        public void Hide()
        {
            if(solarPrefabPreview != null)solarPrefabPreview.Hide();
            abilityActive = false;
            editorPreview = false;
            committedPreviewActive = false;
            styleOverrideActive = false;
            shots.Clear();
            LifetimeShotPhaseMask = 0;
            if (root != null) root.gameObject.SetActive(false);
        }

        private void RenderBackground(float orbitRadius)
        {
            if (lowSettings) return;
            // Background — low alpha, single broad ring.
            Ring(Vector2.zero, orbitRadius * 1.42f, new Color(abilityColor.r, abilityColor.g, abilityColor.b, .045f), .018f, 2);
            // Parallax layers — two offset, slowly drifting arcs.
            Arc(Vector2.zero, orbitRadius * 1.14f, clock * .05f, 2.2f, new Color(.18f, .56f, 1f, .075f), .018f, 3);
            Arc(Vector2.zero, orbitRadius * 1.27f, -clock * .033f + 2.1f, 1.8f, new Color(.72f, .30f, 1f, .06f), .014f, 3);
        }

        private void RenderPassives(Vector2 player, Vector2 boss, bool solarPlume, bool aegisOrbit,
            bool echoResonator, bool emberCore, bool kineticOverdrive, bool ghostTrail)
        {
            if (solarPlume)
            {
                var ability = BossAssetRegistry.Ability(BossAbilityId.FirebirdSolarPlume);
                var vfx = ability.Vfx; var style = ability.Style;
                Halo(boss, vfx.GlowSize.x + Mathf.Sin(clock * vfx.PulseSpeed) * vfx.PulseAmount, style.Tint(vfx.GlowColor, style.AlphaOverLife.Evaluate(Mathf.Repeat(clock / ability.Duration, 1))));
                Arc(boss, .91f, clock * .55f, 2.4f, style.RibbonGradient.Evaluate(Mathf.Repeat(clock / ability.Duration, 1)), .032f, 15);
                Arc(boss, .91f, clock * .55f + Mathf.PI, 2.4f, style.RibbonGradient.Evaluate(Mathf.Repeat(clock / ability.Duration + .5f, 1)), .032f, 15);
            }
            if (aegisOrbit)
            {
                // Gameplay layer — the passive protection boundary.
                Ring(player, .52f + Mathf.Sin(clock * 3f) * .035f, new Color(.24f, .94f, 1f, .80f), .035f, 15);
                Arc(player, .69f, -clock * 1.8f, Mathf.PI * .86f, new Color(.56f, 1f, 1f, .68f), .025f, 16);
            }
            if (echoResonator)
            {
                Arc(player, .39f, clock * 2.4f, Mathf.PI * 1.4f, new Color(.34f, .62f, 1f, .48f), .025f, 15);
                Diamond(player + Vector2.up * .36f, .07f, clock, new Color(.64f, .86f, 1f, .78f), 16);
            }
            if (emberCore)
            {
                var ability = BossAssetRegistry.Ability(BossAbilityId.FirebirdEmberCore);
                var vfx = ability.Vfx;
                Halo(boss, vfx.GlowSize.x + Mathf.Sin(clock * vfx.PulseSpeed) * vfx.PulseAmount, ability.Style.Tint(vfx.GlowColor, ability.Style.AlphaOverLife.Evaluate(Mathf.Repeat(clock / ability.Duration, 1))));
                Diamond(boss, vfx.CoreSize.x + Mathf.Sin(clock * vfx.PulseSpeed) * vfx.PulseAmount, clock * vfx.SecondaryMotionSpeed, vfx.CoreColor * ability.Style.RibbonGradient.Evaluate(Mathf.Repeat(clock / ability.Duration,1)), 16);
            }
            if (kineticOverdrive)
            {
                Arc(player, .48f, clock * 3.4f, 2.6f, new Color(.64f, .90f, 1f, .68f), .026f, 15);
                Arc(player, .56f, clock * 3.4f + Mathf.PI, 2.6f, new Color(.32f, .68f, 1f, .46f), .018f, 15);
            }
            if (ghostTrail)
            {
                // The existing ghost trail owns its segments; this is its readable passive badge.
                Diamond(player + Vector2.left * .26f, .055f, -clock * 2f, new Color(.48f, 1f, .68f, .58f), 15);
            }
        }

        private void RenderAbility(Vector2 player, Vector2 boss, float orbitRadius)
        {
            var progress = Mathf.Clamp01(abilityAge / abilityDuration);
            var pulse = .5f + .5f * Mathf.Sin(clock * 8.5f + abilityId.GetHashCode() * .17f);
            var fade = Mathf.Clamp01(Mathf.Min(progress / .12f, (1f - progress) / .18f));
            var origin = abilityOrigin;
            var target = abilityTarget;
            var direction = (target - origin).sqrMagnitude > .001f ? (target - origin).normalized : Vector2.up;
            var angle = Mathf.Atan2(direction.y, direction.x);

            // Midground — profile geometry tied to the ability rather than a recolored ball.
            var midRadius = .34f + pulse * .10f;
            if (EditorLayer(4))
                Ring(origin, midRadius, Alpha(abilityColor, .30f + fade * .28f), .025f, 9);
            if (EditorLayer(1) || EditorLayer(2) || EditorLayer(4)) switch (abilityId)
            {
                case AbilitySandboxAbilityId.BlackHole:
                    Ring(origin, .18f + pulse * .04f, new Color(.002f, .001f, .012f, .96f), .06f, 14);
                    Arc(origin, .58f, clock * 2.4f, Mathf.PI * 1.6f, new Color(.86f, .24f, 1f, .85f * fade), .045f, 14);
                    Arc(origin, .78f, -clock * 1.8f + 1.2f, Mathf.PI * 1.35f, new Color(.25f, .86f, 1f, .72f * fade), .032f, 14);
                    break;
                case AbilitySandboxAbilityId.MineRing:
                    for (var i = 0; i < 8; i++)
                    {
                        var mineAngle = i * Mathf.PI * .25f + clock * .06f;
                        Diamond(OrbitPoint(mineAngle, orbitRadius * .73f), .10f + pulse * .018f, clock + i, Alpha(abilityColor, fade), 14);
                    }
                    break;
                case AbilitySandboxAbilityId.Polarity:
                    Arc(player, .62f, clock * 1.2f, Mathf.PI, new Color(.26f, .90f, 1f, .86f * fade), .035f, 14);
                    Arc(player, .62f, clock * 1.2f + Mathf.PI, Mathf.PI, new Color(1f, .30f, .62f, .82f * fade), .035f, 14);
                    break;
                case AbilitySandboxAbilityId.OrbitAnchor:
                    Tether(player, target, Alpha(abilityColor, .68f * fade), clock, 13);
                    Diamond(target, .15f + pulse * .03f, clock, Color.white, 15);
                    Arc(target, .30f, clock * 2f, Mathf.PI * 1.5f, Alpha(abilityColor, fade), .034f, 14);
                    break;
                case AbilitySandboxAbilityId.TrajectoryReplay:
                    Arc(target, .52f, -clock * 1.5f, Mathf.PI * 1.4f, Alpha(abilityColor, fade), .032f, 14);
                    Tether(target, player, Alpha(abilityColor, .24f * fade), clock, 12);
                    break;
                case AbilitySandboxAbilityId.GravityWave:
                    Ring(Vector2.zero, Mathf.Lerp(.25f, orbitRadius * 1.5f, progress), Alpha(abilityColor, .66f * fade), .05f, 14);
                    Ring(Vector2.zero, Mathf.Lerp(.42f, orbitRadius * 1.42f, progress), Alpha(abilityColor, .18f * fade), .14f, 13);
                    break;
                case AbilitySandboxAbilityId.CourseRupture:
                    for (var i = 0; i < 6; i++)
                        Chevron(OrbitPoint(i * Mathf.PI / 3f, orbitRadius * .82f), i * Mathf.PI / 3f + Mathf.PI * .5f - clock, .15f, Alpha(abilityColor, fade), 15);
                    break;
                case AbilitySandboxAbilityId.AshenEgg:
                    Arc(origin, .48f, clock * .7f, Mathf.PI * 1.9f, Alpha(abilityColor, fade), .05f, 14);
                    Arc(origin, .62f, -clock * .5f + 1f, Mathf.PI * 1.5f, new Color(1f, .96f, .68f, .70f * fade), .026f, 14);
                    break;
                default:
                    for (var i = 0; i < 4; i++)
                    {
                        var a = angle + (i - 1.5f) * .48f + Mathf.Sin(clock * 2f + i) * .035f;
                        Chevron(origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * midRadius, a, .12f, Alpha(abilityColor, fade), 14);
                    }
                    Tether(origin, target, Alpha(abilityColor, .20f * fade), clock, 11);
                    break;
            }

            // Gameplay layer — unmistakable core and direction.
            if (EditorLayer(0))
            {
                Sprite(abilityCore, origin, angle * Mathf.Rad2Deg, .22f + pulse * .045f, Color.white, 16);
                Sprite(abilityCore, target, angle * Mathf.Rad2Deg, .08f + pulse * .025f, Alpha(abilityColor, .75f * fade), 15);
            }

            if (!lowSettings)
            {
                // Lighting — two cheap halo sprites instead of a post stack.
                if (EditorLayer(5))
                {
                    Halo(origin, .75f + pulse * .12f, new Color(abilityColor.r, abilityColor.g, abilityColor.b, .12f * fade));
                    Halo(target, .34f + pulse * .08f, new Color(abilityColor.r, abilityColor.g, abilityColor.b, .08f * fade));
                }
                // Particles — varied, state-bound micro shards.
                if (EditorLayer(3)) for (var i = 0; i < 6; i++)
                {
                    var a = clock * (i % 2 == 0 ? 1.4f : -1.1f) + i * 1.047f;
                    var p = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (.24f + pulse * .25f);
                    Diamond(p, .022f + (i % 3) * .009f, a, Alpha(abilityColor, fade * (.44f + (i % 2) * .2f)), 17);
                }
                // Foreground — a short, brighter contour remains in front of gameplay.
                if (EditorLayer(7))
                    Arc(player, .36f + pulse * .04f, clock * 2.1f, .72f, new Color(1f, 1f, 1f, .25f * fade), .018f, 18);
                // Post-processing proxy — bounded full-world tint, omitted on low settings.
                if (EditorLayer(5))
                    Halo(Vector2.zero, 6.8f + pulse * .15f, new Color(abilityColor.r, abilityColor.g, abilityColor.b, .018f * fade));
            }
        }

        private void RenderEditorPreview()
        {
            var savedOrigin = editorOrigin;
            var savedTarget = editorTarget;
            var delta = editorTarget - editorOrigin;
            var direction = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.right;
            if (editorAbilityId == AbilitySandboxAbilityId.SolarChicks)
            {
                // The workshop preview is a presentation strip rather than the combat
                // arena: show the three photo-assembled chicks travelling horizontally
                // through the enlarged polygon so the layer relationship is readable.
                // Keep the full left-to-right pass inside the enlarged workshop
                // polygon.  The world camera is wider than the UI stage, so using
                // the combat-space endpoints here made the chicks disappear before
                // the loop reached its midpoint.
                // Span the whole effect plate instead of stopping near its
                // centre.  The final point is intentionally beyond the boss
                // mannequin position so the terminal flash reaches the far
                // edge of the preview polygon.
                editorOrigin = new Vector2(-2.10f, -.82f);
                editorTarget = new Vector2(6.65f, -.82f);
                direction = Vector2.right;
                var cycle = Mathf.Repeat(clock, 1.85f);
                // Keep the fan inside the workshop stage.  The combat shot can
                // travel the full orbit, but the visual reference is a compact,
                // readable three-bird composition.
                var travelDistance = editorTarget.x - editorOrigin.x;
                var side = new Vector2(-direction.y, direction.x);
                for (var i = 0; i < 3; i++)
                {
                    // Keep the three chicks visibly separated from the first frame;
                    // the reference sheet reads as a fan, not as one stacked sprite.
                    // Keep every chick travelling left to right.  A small vertical
                    // separation reads as a three-bird formation without turning the
                    // editor preview into a diagonal fan.
                    var shotDirection = direction;
                    var shotOrigin = editorOrigin + side * ((i - 1) * .42f) -
                        direction * ((i - 1) * .20f);
                    // The reference shows all three chicks in one readable burst;
                    // a tiny offset keeps silhouettes from z-fighting without
                    // turning the fan into three unrelated moments.
                    var age = Mathf.Repeat(cycle + i * .035f, 1.85f);
                    var flight = Mathf.Clamp01(age / 1.0f);
                    var aftermath = age < 1f ? 1f : Mathf.Clamp01(1f - (age - 1f) / .34f);
                    var shot = new ShotFx
                    {
                        Origin = shotOrigin,
                        Position = shotOrigin + shotDirection * (travelDistance * flight),
                        Velocity = shotDirection * 1.35f,
                        Color = editorColor,
                        Core = editorCore,
                        Profile = AbilitySandboxAbilityId.SolarChicks,
                        Age = age,
                        FlightTime = 1f,
                        Seed = 1.4f + i * 1.8f
                    };
                    RenderPhoenixChickLayers(shot, shotDirection, age < 1f, aftermath);
                }
                editorOrigin = savedOrigin;
                editorTarget = savedTarget;
                return;
            }

            RenderEditorGenericPreview(direction);
        }

        private void RenderEditorGenericPreview(Vector2 direction)
        {
            var angle = Mathf.Atan2(direction.y, direction.x);
            var pulse = .5f + .5f * Mathf.Sin(clock * 6f);
            var color = EditorColor(editorColor);
            var cycle = Mathf.Repeat(clock, 1.65f);
            var flight = Mathf.Clamp01(cycle / .95f);
            var projectile = Vector2.Lerp(editorOrigin, editorTarget, flight);
            var aftermath = cycle < .95f ? 0f : Mathf.Clamp01(1f - (cycle - .95f) / .34f);
            if (EditorLayer(1))
            {
                Arc(editorOrigin, .27f + pulse * .04f, clock * 2.4f, 4.1f, Alpha(color, .82f), .040f, 21);
                Arc(editorOrigin, .35f + pulse * .03f, -clock * 1.8f, -3.0f, Alpha(Color.white, .46f), .022f, 21);
            }
            if (EditorLayer(2))
            {
                Ribbon(projectile, projectile - direction * .78f, Alpha(color, .78f), clock, .070f, 20);
                Tether(editorOrigin, projectile, Alpha(color, .34f), clock, 19);
            }
            if (EditorLayer(3))
            {
                for (var i = 0; i < 7; i++)
                {
                    var a = clock * (i % 2 == 0 ? 1.5f : -1.2f) + i * .9f;
                    var p = editorOrigin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (.22f + (i % 3) * .07f);
                    Diamond(p, .026f + (i % 2) * .009f, a, Alpha(color, .62f), 22);
                }
            }
            if (EditorLayer(4))
                Arc(editorOrigin, .48f + pulse * .06f, -clock * .9f, 5.4f, Alpha(color, .26f), .020f, 19);
            if (EditorLayer(5))
            {
                Halo(editorOrigin, .72f + pulse * .10f, new Color(color.r, color.g, color.b, .16f * editorGlow));
                Halo(editorTarget, .34f + pulse * .06f, new Color(color.r, color.g, color.b, .11f * editorGlow));
            }
            if (EditorLayer(6))
            {
                Ring(editorTarget, .20f + pulse * .10f, Alpha(Color.white, aftermath > 0f ? aftermath : .10f), .042f, 24);
                Ring(editorTarget, .38f + pulse * .08f, Alpha(color, aftermath > 0f ? aftermath * .6f : .04f), .022f, 23);
            }
            if (EditorLayer(7))
                Arc(editorTarget, .54f, clock * .8f, 5.5f, Alpha(color, .54f), .024f, 18);
            if (EditorLayer(0))
            {
                Sprite(editorCore, editorOrigin, angle * Mathf.Rad2Deg, .26f + pulse * .04f, Color.white, 25);
                Sprite(editorCore, projectile, angle * Mathf.Rad2Deg, .12f + pulse * .02f, Alpha(color, .86f), 24);
            }
        }

        private bool EditorLayer(int index)
        { return !styleOverrideActive || (editorLayerMask & (1 << index)) != 0; }

        private Vector2 EditorPoint(Vector2 point)
        { return styleOverrideActive ? editorOrigin + (point - editorOrigin) * editorScale : point; }

        private Color EditorColor(Color color)
        {
            if (!styleOverrideActive) return color;
            color.r *= editorBrightness;
            color.g *= editorBrightness;
            color.b *= editorBrightness;
            color.a = Mathf.Clamp01(color.a * Mathf.Lerp(.70f, 1.25f, Mathf.InverseLerp(.20f, 2.50f, editorBrightness)));
            return color;
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var c = Mathf.Cos(radians);
            var s = Mathf.Sin(radians);
            return new Vector2(vector.x * c - vector.y * s, vector.x * s + vector.y * c);
        }

        private void TickShots(float dt)
        {
            for (var i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i];
                shot.Age += dt;
                shot.Position += shot.Velocity * dt;
                var direction = shot.Velocity.normalized;
                var angle = Mathf.Atan2(direction.y, direction.x);
                if (shot.Age > shot.FlightTime + .34f)
                {
                    shots.RemoveAt(i);
                    continue;
                }

                BeginStyleOverride(shot.Profile, shot.Origin, shot.Position);
                if (shot.Profile == AbilitySandboxAbilityId.SolarChicks)
                    RenderPhoenixChickLayers(shot, direction, shot.Age < shot.FlightTime,
                        shot.Age < shot.FlightTime ? 1f : 1f - (shot.Age - shot.FlightTime) / .34f);

                // 1. anticipation / muzzle flash.
                if (shot.Age < .12f)
                {
                    LifetimeShotPhaseMask |= 1 << 0;
                    var anticipation = 1f - shot.Age / .12f;
                    if (EditorLayer(6))
                        Ring(shot.Origin, .08f + (1f - anticipation) * .16f, Alpha(shot.Color, anticipation * .88f), .035f, 19);
                    if (EditorLayer(0))
                        Sprite(shot.Core, shot.Origin, angle * Mathf.Rad2Deg, .12f + anticipation * .04f, Color.white, 20);
                }

                if (shot.Age < shot.FlightTime)
                {
                    LifetimeShotPhaseMask |= 1 << 1;
                    LifetimeShotPhaseMask |= 1 << 2;
                    // 2. projectile core.
                    var corePulse = 1f + Mathf.Sin(clock * 14f + shot.Seed) * .08f;
                    if (EditorLayer(0))
                        Sprite(shot.Core, shot.Position, angle * Mathf.Rad2Deg, .15f * corePulse, Color.white, 20);
                    // 3. trail / ribbon.
                    var trailEnd = shot.Position - direction * (.35f + shot.Velocity.magnitude * .055f);
                    if (EditorLayer(2))
                        Ribbon(shot.Position, trailEnd, shot.Color, shot.Seed + clock, .036f, 19);
                    // 4. secondary particles.
                    if (!lowSettings && EditorLayer(3))
                    {
                        LifetimeShotPhaseMask |= 1 << 3;
                        for (var p = 0; p < 3; p++)
                        {
                            var offset = new Vector2(-direction.y, direction.x) * Mathf.Sin(clock * 9f + p + shot.Seed) * (.05f + p * .025f);
                            Diamond(shot.Position - direction * (.11f + p * .06f) + offset, .018f + p * .006f,
                                clock * (p % 2 == 0 ? 2f : -2f), Alpha(shot.Color, .58f - p * .12f), 20);
                        }
                    }
                }
                else
                {
                    LifetimeShotPhaseMask |= 1 << 4;
                    // Safe terminal point in the sandbox; the real hit test remains disabled.
                    var aftermath = 1f - (shot.Age - shot.FlightTime) / .34f;
                    // 5. impact flash.
                    if (EditorLayer(6))
                    {
                        Ring(shot.Position, .10f + (1f - aftermath) * .30f, Alpha(Color.white, aftermath * .92f), .04f, 21);
                        Ring(shot.Position, .18f + (1f - aftermath) * .48f, Alpha(shot.Color, aftermath * .48f), .025f, 20);
                    }
                    if (!lowSettings && EditorLayer(3))
                    {
                        LifetimeShotPhaseMask |= 1 << 5;
                        // 6. debris / sparks.
                        for (var p = 0; p < 5; p++)
                        {
                            var sparkAngle = shot.Seed + p * 1.256f + clock * .4f;
                            var spark = shot.Position + new Vector2(Mathf.Cos(sparkAngle), Mathf.Sin(sparkAngle)) * (.14f + p * .035f);
                            Line(shot.Position, spark, Alpha(shot.Color, aftermath * .58f), .019f, 21);
                        }
                    }
                    // 7. short aftermath.
                    LifetimeShotPhaseMask |= 1 << 6;
                    if (EditorLayer(7))
                        Arc(shot.Position, .32f + (1f - aftermath) * .22f, shot.Seed + clock, 1.55f, Alpha(shot.Color, aftermath * .42f), .018f, 19);
                }
                styleOverrideActive = false;
            }
        }

        private void BeginStyleOverride(AbilitySandboxAbilityId id, Vector2 origin, Vector2 target)
        {
            styleOverrideActive = false;
            if (editorPreview)
            {
                styleOverrideActive = true;
                return;
            }
            if (!overrides.TryGetValue(id, out var preset)) return;
            editorOrigin = origin;
            editorTarget = target;
            editorAbilityId = id;
            editorLayerMask = preset.LayerMask;
            editorScale = preset.Scale;
            editorBrightness = preset.Brightness;
            editorGlow = preset.Glow;
            styleOverrideActive = true;
        }

        /// <summary>
        /// Phoenix chick shot assembled from the reference guide's eight layers:
        /// core, energy ribbons, trail, particles, noise proxy, glow, impact and decal.
        /// The shot remains one pooled presentation, while each layer has its own timing.
        /// </summary>
        private void RenderPhoenixChickLayers(ShotFx shot, Vector2 direction, bool flying, float aftermath)
        {
            // The workshop is an assembly tool: use the very same photo assets as the
            // eight cards on the left.  Procedural rendering remains only as a fallback
            // for a build where the runtime PNGs have not finished importing yet.
            if (EnsureSolarPhotoLayers())
            {
                RenderPhoenixChickPhotoLayers(shot, direction, flying, aftermath);
                return;
            }

            var position = shot.Position;
            var side = new Vector2(-direction.y, direction.x);
            var pulse = .5f + .5f * Mathf.Sin(clock * 15f + shot.Seed);
            var coreColor = new Color(1f, .42f, .05f, 1f);
            var goldColor = new Color(1f, .88f, .32f, 1f);
            var emberColor = new Color(1f, .20f, .035f, .94f);

            // 1. Core — readable chick silhouette with a hot-white center.
            if (EditorLayer(0))
            {
                // The isolated core card is also present in the assembled shot:
                // a compact hot point gives the pixel-art chick the same white
                // center and warm bloom as the reference composite.
                Sprite(circleSprite, position, 0f, .18f + pulse * .025f,
                    new Color(1f, .72f, .18f, .82f), 21);
                Sprite(shot.Core, position, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
                    .52f + pulse * .060f, Color.white, 22);
            }

            // 2. Energy ribbons — two open, wing-like arcs.  They deliberately
            // stay below a full circle: the reference reads as feathers around
            // a flying bird, not as a target reticle.
            if (EditorLayer(1))
            {
                var heading = Mathf.Atan2(direction.y, direction.x);
                var wingCenter = position - direction * .045f;
                Arc(wingCenter + side * .045f, .44f + pulse * .055f, heading + 2.45f, 1.32f,
                    Alpha(goldColor, flying ? .92f : aftermath), .046f, 22);
                Arc(wingCenter - side * .045f, .50f + pulse * .045f, heading - 2.35f, -1.22f,
                    Alpha(emberColor, flying ? .82f : aftermath * .8f), .032f, 22);
            }

            // 3. Trail — a main ribbon plus three branching feather filaments.
            if (EditorLayer(2))
            {
                var trailLength = 1.55f + shot.Velocity.magnitude * .16f;
                var trailStart = position - direction * trailLength;
                Ribbon(position, trailStart, Alpha(coreColor, flying ? 1f : aftermath * .72f),
                    shot.Seed + clock * 1.7f, .20f, 21);
                Ribbon(position - direction * .02f, trailStart + side * .015f,
                    Alpha(Color.white, flying ? 1f : aftermath * .52f),
                    shot.Seed - clock * 1.3f, .075f, 22);
                // A stable hot filament keeps the long trail readable even on
                // displays where the animated ribbon falls below one pixel.
                Line(position, trailStart, Alpha(Color.white, flying ? .86f : aftermath * .45f), .055f, 22);
                Line(position + side * .035f, trailStart + side * .085f,
                    Alpha(goldColor, flying ? .82f : aftermath * .36f), .032f, 21);
                for (var branch = 0; branch < 3; branch++)
                {
                    var t = .16f + branch * .20f;
                    var branchStart = Vector2.Lerp(position, trailStart, t);
                    var branchEnd = branchStart - direction * (.42f + branch * .075f) +
                        side * Mathf.Sin(clock * 5f + shot.Seed + branch) * (.28f + branch * .065f);
                    Ribbon(branchStart, branchEnd, Alpha(goldColor, flying ? .80f : aftermath * .44f),
                        shot.Seed - clock + branch, .060f, 21);
                }
            }

            if (!lowSettings && (EditorLayer(3) || EditorLayer(4) || EditorLayer(5)))
            {
                // 4. Particles — ember feathers emitted from the moving silhouette.
                if (EditorLayer(3)) for (var particle = 0; particle < 6; particle++)
                {
                    var trailLength = 1.55f + shot.Velocity.magnitude * .16f;
                    var trailStart = position - direction * trailLength;
                    var particleT = .12f + particle * .13f;
                    var particlePoint = Vector2.Lerp(position, trailStart, particleT) +
                        side * Mathf.Sin(clock * 7f + particle + shot.Seed) * (.045f + particle * .015f);
                    Diamond(particlePoint, .045f + (particle % 2) * .016f, clock * (particle % 2 == 0 ? 2f : -2f),
                        Alpha(particle % 2 == 0 ? goldColor : emberColor, flying ? .68f : aftermath * .35f), 23);
                }

                // 5. Noise / distortion proxy — a broken, breathing heat ring.
                if (EditorLayer(4))
                {
                    var heading = Mathf.Atan2(direction.y, direction.x);
                    Arc(position - direction * .10f, .56f + pulse * .07f, heading + 2.4f, 1.65f,
                        Alpha(new Color(1f, .44f, .08f, 1f), flying ? .36f : aftermath * .16f), .022f, 21);
                }

                // 6. Glow / bloom — layered halos replace a costly full-screen stack.
                if (EditorLayer(5))
                {
                    // The glow card is a soft aura, never a second reticle.
                    // Keep it below the chick silhouette so the three open
                    // feather trails remain the dominant read.
                    Halo(position, .34f + pulse * .06f, new Color(1f, .20f, .025f, flying ? .07f : aftermath * .04f));
                    Halo(position, .20f + pulse * .035f, new Color(1f, .72f, .16f, flying ? .14f : aftermath * .06f));
                }
            }

            if (!flying && (EditorLayer(6) || EditorLayer(7)))
            {
                // 7. Impact — a hot radial flash and short feather sparks.
                if (EditorLayer(6))
                {
                    Ring(position, .22f + (1f - aftermath) * .34f, Alpha(Color.white, aftermath * .88f), .060f, 24);
                }
                if (EditorLayer(6) && !lowSettings)
                    for (var spark = 0; spark < 6; spark++)
                    {
                        var sparkAngle = shot.Seed + spark * 1.047f + clock * .5f;
                        var sparkEnd = position + new Vector2(Mathf.Cos(sparkAngle), Mathf.Sin(sparkAngle)) *
                            (.24f + spark % 3 * .065f);
                        Line(position, sparkEnd, Alpha(spark % 2 == 0 ? goldColor : emberColor, aftermath * .70f), .028f, 24);
                    }

                // 8. Decal — a brief rune-like landing mark at the safe demo point.
                if (EditorLayer(7))
                {
                    Ring(position, .44f + (1f - aftermath) * .16f,
                        Alpha(new Color(1f, .35f, .06f, 1f), aftermath * .34f), .018f, 20);
                    Arc(position, .58f + (1f - aftermath) * .20f, shot.Seed + clock, 5.1f,
                        Alpha(new Color(1f, .52f, .10f, 1f), aftermath * .58f), .028f, 20);
                }
            }
        }

        private bool EnsureSolarPhotoLayers()
        {
            if (solarPhotoLayersLoaded) return true;
            var ready = true;
            for (var i = 0; i < SolarPhotoLayerResources.Length; i++)
            {
                var texture = Resources.Load<Texture2D>(SolarPhotoLayerResources[i]);
                if (texture == null)
                {
                    ready = false;
                    continue;
                }

                solarPhotoTextures[i] = texture;
                if (solarPhotoLayers[i] == null)
                {
                    solarPhotoLayers[i] = UnityEngine.Sprite.Create(texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(.5f, .5f), 100f);
                    solarPhotoLayers[i].name = "Solar chicks photo layer " + i;
                }
            }

            solarPhotoLayersLoaded = ready;
            return solarPhotoLayersLoaded;
        }

        private void RenderPhoenixChickPhotoLayers(ShotFx shot, Vector2 direction, bool flying, float aftermath)
        {
            var position = shot.Position;
            var side = new Vector2(-direction.y, direction.x);
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var pulse = .5f + .5f * Mathf.Sin(clock * 11f + shot.Seed);
            var flap = Mathf.Sin(clock * 7.2f + shot.Seed) * 8f;
            var fade = flying ? 1f : Mathf.Clamp01(aftermath);

            // 5. Glow and 4. noise sit behind the readable gameplay silhouette.
            if (!lowSettings && EditorLayer(5))
                Sprite(solarPhotoLayers[5], position - direction * .03f, angle,
                    1.02f + pulse * .10f, Alpha(Color.white, (.42f + pulse * .12f) * fade), 8);
            if (!lowSettings && EditorLayer(4))
                Sprite(solarPhotoLayers[4], position + side * Mathf.Sin(clock * 1.7f + shot.Seed) * .025f,
                    angle - 10f + pulse * 12f, .94f + pulse * .055f,
                    Alpha(Color.white, (.48f + pulse * .10f) * fade), 9);

            // 3. Trail is the long photo ribbon, placed behind the moving head.
            if (EditorLayer(2))
            {
                Sprite(solarPhotoLayers[2], position - direction * (.24f + pulse * .035f),
                    angle + Mathf.Sin(clock * 2.2f + shot.Seed) * 3f,
                    1.24f + pulse * .07f, Alpha(Color.white, .84f * fade), 12);
                // Keep the source photo readable, then bend a second copy through depth.
                // The z-arc makes the 2D trail a real 3D ribbon instead of a flat stamp.
                Ribbon3D(position - direction * .08f, position - direction * 1.62f,
                    Alpha(new Color(1f, .42f, .06f, 1f), .68f * fade), shot.Seed + clock * 1.7f, .085f, 13);
            }

            // 1. Ribbons are the animated feather fan; its small rotation is the wing beat.
            if (EditorLayer(1))
                Sprite(solarPhotoLayers[1], position + side * (.018f + flap * .0015f),
                    angle + flap, 1.16f + pulse * .065f, Alpha(Color.white, .98f * fade), 17);

            // 4. Particles are a second, slightly delayed photo pass so the source art
            // reads as emitted embers instead of a procedural diamond field.
            if (EditorLayer(3))
                Sprite(solarPhotoLayers[3], position - direction * (.16f + pulse * .05f),
                    angle - flap * .55f, 1.02f + pulse * .07f, Alpha(Color.white, .92f * fade), 18);

            // 1. Core remains on top and is the same star image shown in the first card.
            if (EditorLayer(0))
                Sprite(solarPhotoLayers[0], position, 0f, 1.02f + pulse * .055f,
                    Alpha(Color.white, fade), 22);

            // 7. Impact and 8. decal only appear in the terminal part of the loop.
            if (!flying && fade > .001f)
            {
                if (EditorLayer(6))
                    Sprite(solarPhotoLayers[6], position, angle + clock * 12f,
                        (.78f + (1f - fade) * .30f) * (1f + pulse * .05f),
                        Alpha(Color.white, fade), 24);
                if (EditorLayer(7))
                    Sprite(solarPhotoLayers[7], position + side * .02f, clock * 8f,
                        .96f + (1f - fade) * .22f, Alpha(Color.white, fade * .86f), 20);
            }
        }

        private bool HasPassives(bool solarPlume, bool aegisOrbit, bool echoResonator, bool emberCore, bool kineticOverdrive, bool ghostTrail)
        { return solarPlume || aegisOrbit || echoResonator || emberCore || kineticOverdrive || ghostTrail; }

        private void EnsureLine(int index)
        {
            if (index < lines.Count) return;
            var line = new GameObject("Layered energy line " + index).AddComponent<LineRenderer>();
            line.transform.SetParent(root, false);
            line.gameObject.layer = WorkshopLayer;
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            lines.Add(line);
        }

        private LineRenderer TakeLine(int count, Color color, float width, int sortingOrder)
        {
            if (lineCursor >= MaxLines) return null;
            color = EditorColor(color);
            width *= styleOverrideActive ? editorScale : 1f;
            EnsureLine(lineCursor);
            var line = lines[lineCursor++];
            line.enabled = true;
            line.positionCount = count;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private void Line(Vector2 a, Vector2 b, Color color, float width, int sortingOrder)
        {
            a = EditorPoint(a);
            b = EditorPoint(b);
            var line = TakeLine(2, color, width, sortingOrder);
            if (line == null) return;
            line.SetPosition(0, a); line.SetPosition(1, b);
        }

        private void Ribbon(Vector2 a, Vector2 b, Color color, float phase, float width, int sortingOrder)
        {
            a = EditorPoint(a);
            b = EditorPoint(b);
            var line = TakeLine(18, color, width, sortingOrder);
            if (line == null) return;
            var d = b - a;
            var side = new Vector2(-d.y, d.x).normalized;
            for (var i = 0; i < 18; i++)
            {
                var t = i / 17f;
                // Wide Phoenix trails get a readable feather curve; narrow
                // generic ribbons keep the old restrained motion.
                var waveAmplitude = width >= .10f ? .085f : .025f;
                var wave = Mathf.Sin(phase * 12f + t * 8f) * waveAmplitude * Mathf.Sin(t * Mathf.PI);
                line.SetPosition(i, Vector2.Lerp(a, b, t) + side * wave);
            }
        }

        private void Ribbon3D(Vector2 a, Vector2 b, Color color, float phase, float width, int sortingOrder)
        {
            a = EditorPoint(a);
            b = EditorPoint(b);
            var line = TakeLine(24, color, width, sortingOrder);
            if (line == null) return;
            var d = b - a;
            var side = new Vector2(-d.y, d.x).normalized;
            for (var i = 0; i < 24; i++)
            {
                var t = i / 23f;
                var wave = Mathf.Sin(phase * 10f + t * 8.5f) * .075f * Mathf.Sin(t * Mathf.PI);
                var point = Vector2.Lerp(a, b, t) + side * wave;
                // The centre bows toward the camera, while the ends settle back.
                var depth = -.12f - Mathf.Sin(t * Mathf.PI) * .24f +
                    Mathf.Sin(phase * 1.7f + t * 5.5f) * .025f;
                line.SetPosition(i, new Vector3(point.x, point.y, depth));
            }
        }

        private void Arc(Vector2 center, float radius, float angle, float span, Color color, float width, int sortingOrder)
        {
            center = EditorPoint(center);
            radius *= styleOverrideActive ? editorScale : 1f;
            var count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(span) * radius * 10f), 8, 48);
            var line = TakeLine(count, color, width, sortingOrder);
            if (line == null) return;
            for (var i = 0; i < count; i++)
            {
                var a = angle + span * i / (count - 1f);
                line.SetPosition(i, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        private void Ring(Vector2 center, float radius, Color color, float width, int sortingOrder)
        { Arc(center, radius, 0f, Mathf.PI * 2f, color, width, sortingOrder); }

        private void Tether(Vector2 a, Vector2 b, Color color, float phase, int sortingOrder)
        {
            a = EditorPoint(a);
            b = EditorPoint(b);
            var line = TakeLine(24, color, .022f, sortingOrder);
            if (line == null) return;
            var d = b - a;
            var side = new Vector2(-d.y, d.x).normalized;
            for (var i = 0; i < 24; i++)
            {
                var t = i / 23f;
                line.SetPosition(i, Vector2.Lerp(a, b, t) + side * Mathf.Sin(t * 30f - phase * 7f) * .035f * Mathf.Sin(t * Mathf.PI));
            }
        }

        private void Chevron(Vector2 center, float angle, float size, Color color, int sortingOrder)
        {
            var f = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var s = new Vector2(-f.y, f.x);
            Line(center - f * size + s * size * .62f, center, color, .03f, sortingOrder);
            Line(center - f * size - s * size * .62f, center, color, .03f, sortingOrder);
        }

        private void Diamond(Vector2 center, float radius, float angle, Color color, int sortingOrder)
        {
            center = EditorPoint(center);
            radius *= styleOverrideActive ? editorScale : 1f;
            var line = TakeLine(5, color, .024f, sortingOrder);
            if (line == null) return;
            for (var i = 0; i < 5; i++)
            {
                var a = angle + i * Mathf.PI * .5f;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        private void Halo(Vector2 center, float size, Color color)
        {
            if (styleOverrideActive) color.a = Mathf.Clamp01(color.a * editorGlow);
            Sprite(circleSprite, center, 0f, size, color, 1);
        }

        private void Sprite(Sprite sprite, Vector2 position, float angleDegrees, float worldSize, Color tint, int sortingOrder)
        {
            if (sprite == null || spriteCursor >= MaxSprites) return;
            position = EditorPoint(position);
            worldSize *= styleOverrideActive ? editorScale : 1f;
            tint = EditorColor(tint);
            if (spriteCursor >= sprites.Count)
            {
                var renderer = new GameObject("Layered energy sprite " + spriteCursor).AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(root, false);
                renderer.gameObject.layer = WorkshopLayer;
                renderer.sharedMaterial = photoMaterial != null ? photoMaterial : material;
                sprites.Add(renderer);
            }
            var sr = sprites[spriteCursor++];
            sr.enabled = true;
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = sortingOrder;
            sr.transform.position = position;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
            var bounds = sprite.bounds.size;
            sr.transform.localScale = Vector3.one * (worldSize / Mathf.Max(.001f, Mathf.Max(bounds.x, bounds.y)));
        }

        private static Color Alpha(Color color, float alpha)
        { color.a *= Mathf.Clamp01(alpha); return color; }

        private static void SetLayerRecursively(Transform node, int layer)
        {
            if (node == null) return;
            node.gameObject.layer = layer;
            for (var i = 0; i < node.childCount; i++)
                SetLayerRecursively(node.GetChild(i), layer);
        }

        private static Vector2 OrbitPoint(float angle, float radius)
        { return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius; }

        private void OnDisable() { Hide(); }
        private void OnDestroy()
        {
            if(workshopBackdropSprite != null) Destroy(workshopBackdropSprite);
            if (root != null) Destroy(root.gameObject);
            if (material != null) Destroy(material);
            for (var i = 0; i < solarPhotoLayers.Length; i++)
                if (solarPhotoLayers[i] != null) Destroy(solarPhotoLayers[i]);
        }
    }
}
