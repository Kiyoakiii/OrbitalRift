using System.Collections.Generic;
using Guid = System.Guid;
using UnityEngine;

namespace OrbitalRift
{
    [ExecuteAlways]
    public sealed class GameManager : MonoBehaviour
    {
        private readonly List<Enemy> enemies = new List<Enemy>(32);
        private readonly List<Projectile> projectiles = new List<Projectile>(128);
        private readonly List<StarParticle> stars = new List<StarParticle>(128);
        private readonly List<DamageShard> damageShards = new List<DamageShard>(16);
        private readonly List<LineRenderer> orbitRenderers = new List<LineRenderer>(OrbitSettings.DashCount);
        private readonly List<CoopPlayerShotState> coopPreviewPlayerShots = new List<CoopPlayerShotState>(48);
        private readonly List<SpriteRenderer> coopThreatMineMarkers = new List<SpriteRenderer>(3);
        private ObjectPool<Enemy> enemyPool;
        private ObjectPool<Projectile> projectilePool;
        private ObjectPool<StarParticle> starPool;
        private ObjectPool<DamageShard> damageShardPool;
        private Camera gameCamera;
        private Transform arena, player, core, splitPickup, menuEmblem, warpBadge;
        private Transform coopGuest, coopHostMarker, coopGuestMarker, coopRelayCore, coopRelayCoreGlow;
        private LineRenderer coopTrajectoryRenderer, coopTetherRenderer, coopThreatCleaveRenderer,
            coopThreatRingLeftRenderer, coopThreatRingRightRenderer;
        private TrailRenderer coopRelayCoreTrail;
        private Transform coopRoomEnvironment;
        private SpriteRenderer coopRoomWash;
        private readonly List<SpriteRenderer> coopRoomMotifs = new List<SpriteRenderer>(18);
        private Sprite whiteSprite, circleSprite, shipSprite, projectileSprite, bonusSprite, orangeEnemySprite, pinkCanEnemySprite, bossSprite, menuEmblemSprite, warpBadgeSprite;
        private Sprite navigatorRankSprite, guardianRankSprite, legendRankSprite, overlordRankSprite, divinityRankSprite;
        private AudioSource musicSource, effectsSource;
        private AudioClip enemyDeathSound, playerDamageSound, coopBumpSound, coopTetherOverloadSound, coopRicochetSound;
        private float playerAngle = -Mathf.PI * .5f, targetAngle, fireTimer, spawnTimer, starTimer, invincible, coreAngle;
        private int score, bestScore, mmr, lastMmrDelta, shields = 3, phase = 1, cores, spawnsLeft;
        private ShipArchetype selectedShip;
        private bool playing, showMenu = true, showSettings, showCoop, showResults, autoFire = true, coreActive, splitShot, paused, bossSpawnPending, showRankGuide, showRoomGuide;
        private string playerNickname;
        private string currentRunId;
        private string nicknameError;
        private string partyJoinCode = string.Empty;
        private IReadOnlyList<LeaderboardEntry> scoreLeaderboardEntries;
        private IReadOnlyList<LeaderboardEntry> mmrLeaderboardEntries;
        private FirebaseScoreService firebaseScores;
        private MultiplayerSessionController multiplayerSessions;
        private CoopSimulationBridge coopSimulation;
        private IPlayerCommandSource playerCommandSource;
        private FirebaseConnectionState firebaseConnectionState = FirebaseConnectionState.Connecting;
        private float splitShotTimer, tripleShotTimer, warpTimer, splitLifetime;
        private int starShields;
        private Vector2 splitVelocity;
        private float touchHintTimer;
        private float phaseUpgradeBannerTimer;
        private string phaseUpgradeLabel;
        private float screenShakeTimer, screenShakeStrength;
        private float hpFlashTimer;
        private float bossSpawnTimer;
        private float mmrResultTimer;
        private float uiFadeTimer;
        private int activeControlDirection;
        private int framedScreenWidth = -1, framedScreenHeight = -1;
        private bool coopPlaying, coopLocalPreview, soloExpeditionPlaying;
        private float coopPreviewHostAngle = 210f, coopPreviewGuestAngle = 330f;
        private float coopPreviewTrajectoryTime;
        private float coopPreviewHostFireTimer, coopPreviewGuestFireTimer;
        private uint coopPreviewHostShots, coopPreviewGuestShots, lastCoopHostShots, lastCoopGuestShots;
        private SectorLayout coopPreviewSector;
        private int coopPreviewRunSeed = 27082026;
        private int coopPreviewRoomIndex;
        private float coopPreviewRoomTimer;
        private Transform coopEnemy;
        private float coopPreviewEnemyAngle = 90f, coopPreviewEnemyRadius = CoopTrajectorySettings.ThreatSpawnRadius;
        private int coopPreviewEnemyHealth, coopPreviewEnemyMaxHealth;
        private byte coopPreviewEnemyKind;
        private ElementalReaction coopPreviewResonance;
        private float coopPreviewResonanceTimer;
        private uint coopPreviewResonanceSequence;
        private bool coopPreviewHasLastElement;
        private DamageElement coopPreviewLastElement;
        private float coopPreviewLastElementAge;
        private float coopPreviewThreatAttackTimer;
        private float coopPreviewThreatWindupTimer;
        private float coopPreviewThreatTargetHostAngle;
        private float coopPreviewThreatTargetGuestAngle;
        private float coopPreviewRoomEntryGraceTimer;
        private float coopPreviewTeamDamageCooldown;
        private float coopPreviewThreatPulseTimer;
        private uint coopPreviewThreatPulseSequence;
        private DamageElement coopPreviewThreatPulseElement;
        private CoopThreatPattern coopPreviewThreatPattern;
        private bool coopPreviewThreatTargetsHost = true;
        private float coopPreviewThreatPatternAngle;
        private uint lastCoopThreatPulseSequence;
        private bool coopPreviewCompleted;
        private uint coopPreviewCompletionSequence;
        private uint lastCoopCompletionSequence;
        private int coopPreviewTeamHealth;
        private int coopPreviewTeamMaxHealth = CoopRoomRules.TeamMaxHealth;
        private bool coopPreviewFailed;
        private uint coopPreviewFailureSequence;
        private uint lastCoopFailureSequence;
        private float coopPreviewCollisionCooldown;
        private uint coopPreviewCollisionSequence;
        private uint lastCoopCollisionSequence;
        private Vector2 coopPreviewCollisionPosition;
        private float coopCollisionBannerTimer;
        private bool coopPreviewRelayCoreActive;
        private Vector2 coopPreviewRelayCorePosition, coopPreviewRelayCoreVelocity;
        private byte coopPreviewRelayCoreCharge;
        private DamageElement coopPreviewRelayCoreElement;
        private bool coopPreviewRelayCoreDangerous;
        private float coopPreviewRelayCoreContactCooldown;
        private uint coopPreviewRelayCoreEventSequence, lastCoopRelayCoreEventSequence;
        private byte coopPreviewRelayCoreEventKind;
        private Vector2 coopPreviewRelayCoreEventPosition;
        private float coopRelayCoreBannerTimer;
        private string coopRelayCoreBanner = string.Empty;
        private bool coopPreviewTetherActive;
        private float coopPreviewTetherHeat, coopPreviewTetherOverloadTimer;
        private float coopPreviewTetherDamageTimer, coopPreviewTetherCoreTimer, coopPreviewTetherReconnectCooldown;
        private uint coopPreviewTetherEventSequence, lastCoopTetherEventSequence;
        private byte coopPreviewTetherEventKind;
        private Vector2 coopPreviewTetherEventPosition;
        private float coopTetherBannerTimer;
        private string coopTetherBanner = string.Empty;
        private uint coopPreviewRedirectSequence, lastCoopRedirectSequence;
        private byte coopPreviewRedirectKind;
        private Vector2 coopPreviewRedirectPosition;
        private bool coopPreviewRedirectFromHost;
        private DamageElement coopPreviewRedirectElement;
        private float coopPreviewRedirectCooldown;
        private float coopRedirectBannerTimer;
        private string coopRedirectBanner = string.Empty;
        private float coopRoomIntroTimer;
        private int coopObservedRoomIndex = -1;
        private int coopPreviousEnemyHealth = -1;
        private float coopThreatDefeatedBannerTimer;
        private int coopPreviousTeamHealth = -1;
        private float coopHullHitBannerTimer;
        private int coopLastHullDamage;
        private float coopThreatPatternVisualTimer;
        private float coopThreatPatternVisualDuration;
        private CoopThreatPattern coopThreatVisualPattern;
        private Vector2 coopThreatVisualOrigin;
        private Vector2 coopThreatVisualTarget;
        private float coopThreatVisualPatternAngle;
        private float coopThreatVisualTargetAngle;
        private Color coopThreatVisualColor;
        private float enemyDeathSfxCooldown;
        private int coopRoomEnvironmentSignature = int.MinValue;
        private float coopRoomEnvironmentRotationSpeed;
        private int coopResultScore;
        private int coopResultMmrDelta;
        private string coopResultRunId = string.Empty;
        private string coopResultFingerprint = string.Empty;
        private bool coopResultSubmitted;

        [Header("Editor preview / visual tuning")]
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Color distantStarColor = new Color(.55f, .66f, 1f, .5f);
        [SerializeField] private Color shipTint = Color.white;

        // Скорость движения по единственной орбите при удержании сенсорной зоны.
        private const float TouchOrbitSpeed = 3.4f;
        private const float UiFadeDuration = .28f;

        private void OnEnable()
        {
            if (!Application.isPlaying) CreateEditorPreview();
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                CreateEditorPreview();
                return;
            }
            // Всегда начинаем новый запуск со стартового меню. Это также
            // сбрасывает состояние, которое Unity может сохранить при
            // отключенном Domain Reload в настройках Enter Play Mode.
            playing = false;
            showMenu = true;
            showSettings = false;
            showCoop = false;
            showResults = false;
            showRoomGuide = false;
            coopPlaying = false;
            coopLocalPreview = false;
            soloExpeditionPlaying = false;
            paused = false;
            coreActive = false;
            splitShot = false;
            phaseUpgradeBannerTimer = 0f;
            screenShakeTimer = 0f;
            hpFlashTimer = 0f;
            bestScore = PlayerPrefs.GetInt("orbital_rift_best", 0);
            if (PlayerPrefs.GetInt("orbital_rift_mmr_revision", 0) < MmrSettings.RatingRevision)
            {
                mmr = MmrSettings.StartingMmr;
                PlayerPrefs.SetInt("orbital_rift_mmr", mmr);
                PlayerPrefs.SetInt("orbital_rift_mmr_revision", MmrSettings.RatingRevision);
                PlayerPrefs.Save();
            }
            else mmr = Mathf.Max(MmrSettings.MinimumMmr, PlayerPrefs.GetInt("orbital_rift_mmr", MmrSettings.StartingMmr));
            playerNickname = PlayerPrefs.GetString("orbital_rift_nickname", string.Empty);
            selectedShip = ShipLoadoutSettings.Clamp(PlayerPrefs.GetInt(ShipLoadoutSettings.PlayerPrefsKey, 0));
            GameAudioSettings.Load();
            HapticFeedback.Load();
            GameVisualSettings.Load();
            playerCommandSource = new LocalPlayerCommandSource();
            uiFadeTimer = .45f;
            firebaseScores = GetComponent<FirebaseScoreService>();
            multiplayerSessions = GetComponent<MultiplayerSessionController>();
            coopSimulation = GetComponent<CoopSimulationBridge>();
            if (firebaseScores != null)
            {
                firebaseScores.PersonalBestLoaded += ApplyCloudBestScore;
                firebaseScores.PersonalMmrLoaded += ApplyCloudMmr;
                firebaseScores.ScoreLeaderboardLoaded += ApplyScoreLeaderboard;
                firebaseScores.MmrLeaderboardLoaded += ApplyMmrLeaderboard;
                firebaseScores.ConnectionStateChanged += ApplyFirebaseConnectionState;
                firebaseConnectionState = firebaseScores.ConnectionState;
            }
            RemoveEditorPreviewObjects();
            CreateCamera();
            whiteSprite = CreateWhiteSprite();
            circleSprite = CreateCircleSprite();
            shipSprite = LoadResourceSprite("ship", 1024f);
            projectileSprite = LoadResourceSprite("projectile", 1024f);
            bonusSprite = LoadResourceSprite("bonus_pickup", 1024f);
            orangeEnemySprite = LoadResourceSprite("enemy_orange", 1024f);
            pinkCanEnemySprite = LoadResourceSprite("enemy_pink_can", 1024f);
            bossSprite = LoadResourceSprite("boss_dreadnought", 1024f);
            menuEmblemSprite = LoadResourceSprite("menu_emblem", 1024f);
            warpBadgeSprite = LoadResourceSprite("warp_badge", 1024f);
            navigatorRankSprite = LoadResourceSprite("Ranks/rank_navigator", 1024f);
            guardianRankSprite = LoadResourceSprite("Ranks/rank_guardian", 1024f);
            legendRankSprite = LoadResourceSprite("Ranks/rank_legend", 1024f);
            overlordRankSprite = LoadResourceSprite("Ranks/rank_overlord", 1024f);
            divinityRankSprite = LoadResourceSprite("Ranks/rank_divinity", 1024f);
            CreateAudio();
            CreateSpaceBackdrop();
            arena = new GameObject("Arena").transform;
            CreateArena();
            CreatePools();
            CreatePlayer();
        }

        private void Update()
        {
            UpdateCameraFraming();
            if (!Application.isPlaying) return;
            var dt = Time.deltaTime;
            hpFlashTimer = Mathf.Max(0f, hpFlashTimer - dt);
            enemyDeathSfxCooldown = Mathf.Max(0f, enemyDeathSfxCooldown - Time.unscaledDeltaTime);
            mmrResultTimer = Mathf.Max(0f, mmrResultTimer - dt);
            uiFadeTimer = Mathf.Max(0f, uiFadeTimer - Time.unscaledDeltaTime);
            if (!paused) { UpdateStars(dt); UpdateDamageShards(dt); UpdateScreenShake(dt); }
            UpdatePresentation();
            if (!coopPlaying && coopSimulation != null && coopSimulation.RunStarted)
                BeginCoopRun(false);
            var command = playerCommandSource != null ? playerCommandSource.ReadFrame() : PlayerCommandFrame.None;
            if (coopPlaying)
            {
                if (!coopLocalPreview && (multiplayerSessions == null || multiplayerSessions.CurrentSession == null || multiplayerSessions.PlayerCount < 2))
                {
                    FinishCoopRunToMenu();
                    return;
                }
                if (command.BackPressed)
                {
                    ExitCoopRun();
                    return;
                }
                UpdateCoopRun(dt, command.OrbitDirection);
                return;
            }
            if (showRoomGuide && command.BackPressed)
            {
                showRoomGuide = false;
                BeginUiFade();
                return;
            }
            if (showCoop && command.BackPressed)
            {
                showCoop = false;
                BeginUiFade();
                return;
            }
            if (showSettings && command.BackPressed)
            {
                CloseSettings();
                return;
            }
            if (playing && command.BackPressed)
            {
                paused = !paused;
                activeControlDirection = 0;
                return;
            }
            if (!playing || paused) return;
            UpdateInput(dt, command.OrbitDirection);
            UpdatePlayer(dt);
            UpdateSpawning(dt);
            UpdateEnemies(dt);
            UpdateProjectiles(dt);
            UpdateCore(dt);
            if (command.ToggleAutoFire) autoFire = !autoFire;
        }

        private void LateUpdate()
        {
            if (gameCamera == null) return;
            gameCamera.clearFlags = CameraClearFlags.Color;
            gameCamera.backgroundColor = backgroundColor;
        }

        private void OnDestroy()
        {
            if (firebaseScores == null) return;
            firebaseScores.PersonalBestLoaded -= ApplyCloudBestScore;
            firebaseScores.PersonalMmrLoaded -= ApplyCloudMmr;
            firebaseScores.ScoreLeaderboardLoaded -= ApplyScoreLeaderboard;
            firebaseScores.MmrLeaderboardLoaded -= ApplyMmrLeaderboard;
            firebaseScores.ConnectionStateChanged -= ApplyFirebaseConnectionState;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                if (playing) paused = true;
                if (musicSource != null) musicSource.Pause();
                return;
            }
            ResumeMusicAfterBackground();
        }

        private void OnApplicationPause(bool backgrounded)
        {
            if (backgrounded)
            {
                if (playing) paused = true;
                if (musicSource != null) musicSource.Pause();
                return;
            }
            ResumeMusicAfterBackground();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (gameCamera != null) gameCamera.backgroundColor = backgroundColor;
            if (player != null)
            {
                var renderer = player.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.color = shipTint;
            }
        }

        private void CreateCamera()
        {
            RenderSettings.skybox = null;
            gameCamera = new GameObject("Main Camera").AddComponent<Camera>();
            gameCamera.orthographic = true; gameCamera.clearFlags = CameraClearFlags.Color; gameCamera.backgroundColor = backgroundColor; gameCamera.transform.position = new Vector3(0,0,-10); gameCamera.tag = "MainCamera";
            gameCamera.gameObject.AddComponent<AudioListener>();
            UpdateCameraFraming(true);
        }

        private static void RemoveEditorPreviewObjects()
        {
            var previewNames = new[] { "Editor Preview Camera", "Editor Preview Background", "Arena (Editor Preview)", "Main Camera", "Deep space background" };
            for (var i = 0; i < previewNames.Length; i++)
            {
                var preview = GameObject.Find(previewNames[i]);
                if (preview != null) Destroy(preview);
            }
        }

        private void UpdateCameraFraming(bool force = false)
        {
            if (gameCamera == null) return;
            if (!force && !coopPlaying && framedScreenWidth == Screen.width && framedScreenHeight == Screen.height) return;
            framedScreenWidth = Screen.width;
            framedScreenHeight = Screen.height;
            var aspect = Mathf.Max(.01f, Screen.width / (float)Mathf.Max(1, Screen.height));
            var halfWidthWithMargin = OrbitSettings.Radius + .55f;
            var halfHeightWithMargin = 5.1f;
            if (coopPlaying)
            {
                var trajectoryTime = coopLocalPreview
                    ? coopPreviewTrajectoryTime
                    : coopSimulation == null ? CoopTrajectorySettings.InitialElapsedSeconds : coopSimulation.TrajectoryTimeSeconds;
                var state = CoopTrajectorySettings.Evaluate(trajectoryTime);
                halfWidthWithMargin = Mathf.Lerp(CoopTrajectorySettings.HorizontalExtent(state.From),
                    CoopTrajectorySettings.HorizontalExtent(state.To), state.Blend) + CoopTrajectorySettings.CameraMargin;
                halfHeightWithMargin = Mathf.Lerp(CoopTrajectorySettings.VerticalExtent(state.From),
                    CoopTrajectorySettings.VerticalExtent(state.To), state.Blend) + CoopTrajectorySettings.CameraMargin;
            }
            // На узком портретном экране размер берётся по ширине; на ПК сохраняется обычный масштаб.
            var targetSize = Mathf.Max(5.1f, halfHeightWithMargin, halfWidthWithMargin / aspect);
            gameCamera.orthographicSize = force
                ? targetSize
                : Mathf.Lerp(gameCamera.orthographicSize, targetSize,
                    1f - Mathf.Exp(-4f * Mathf.Max(0f, Time.unscaledDeltaTime)));
        }

        [ContextMenu("Create Editor Preview")]
        public void CreateEditorPreview()
        {
            if (arena == null)
            {
                var existingArena = GameObject.Find("Arena (Editor Preview)");
                if (existingArena != null) arena = existingArena.transform;
            }
            if (arena != null && player == null) player = arena.Find("Player");
            if (arena != null && player != null) return;
            gameCamera = FindFirstObjectByType<Camera>();
            if (gameCamera == null) CreateCamera();
            else UpdateCameraFraming(true);
            gameCamera.gameObject.name = "Editor Preview Camera";
            whiteSprite = CreateWhiteSprite();
            circleSprite = CreateCircleSprite();
            shipSprite = LoadResourceSprite("ship", 1024f);
            bonusSprite = LoadResourceSprite("bonus_pickup", 1024f);
            menuEmblemSprite = LoadResourceSprite("menu_emblem", 1024f);
            warpBadgeSprite = LoadResourceSprite("warp_badge", 1024f);
            CreateSpaceBackdrop("Editor Preview Background");
            arena = new GameObject("Arena (Editor Preview)").transform;
            CreateArena();
            CreatePlayer();
        }

        private Sprite CreateWhiteSprite()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); texture.Apply();
            return Sprite.Create(texture, new Rect(0,0,2,2), new Vector2(.5f,.5f), 2);
        }

        private Sprite CreateCircleSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var dx = (x + .5f) / size * 2f - 1f;
                var dy = (y + .5f) / size * 2f - 1f;
                var alpha = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 7f);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f,.5f), size);
        }

        private Sprite LoadResourceSprite(string resourceName, float fallbackPixelsPerUnit)
        {
            var sprite = Resources.Load<Sprite>(resourceName);
            if (sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>(resourceName);
            if (texture == null) return null;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), fallbackPixelsPerUnit);
        }

        private void CreateSpaceBackdrop(string rootName = "Deep space background")
        {
            var backdrop = new GameObject(rootName).transform;
            MakeSprite("Black space", backdrop, Color.black, new Vector3(20f, 20f, 1f), -100);
            for (var i = 0; i < StarStreamSettings.BackgroundStarCount; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var radius = Random.Range(1.2f, 8f);
                var star = MakeSprite("Distant star", backdrop, new Color(distantStarColor.r, distantStarColor.g, distantStarColor.b, Random.Range(.18f, distantStarColor.a)), Vector3.one * Random.Range(.012f,.04f), -5);
                star.sprite = circleSprite;
                star.transform.position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }

        private void CreateAudio()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.name = "Music source";
            musicSource.clip = Resources.Load<AudioClip>("deep_space_drift");
            musicSource.loop = true;
            musicSource.volume = GameAudioSettings.MusicVolume;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.name = "Effects source";
            effectsSource.volume = GameAudioSettings.EffectsVolume;
            effectsSource.playOnAwake = false;
            effectsSource.spatialBlend = 0f;
            enemyDeathSound = SoundEffects.CreateEnemyDeath();
            playerDamageSound = SoundEffects.CreatePlayerDamage();
            coopBumpSound = SoundEffects.CreateCoopBump();
            coopTetherOverloadSound = SoundEffects.CreateTetherOverload();
            coopRicochetSound = SoundEffects.CreateFriendlyRicochet();
        }

        private SpriteRenderer MakeSprite(string name, Transform parent, Color color, Vector3 scale, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent); go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = whiteSprite; sr.color = color; sr.sortingOrder = order; return sr;
        }

        private LineRenderer CreateThreatLine(string name, int sortingOrder, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(arena);
            line.useWorldSpace = true;
            line.loop = false;
            line.startWidth = line.endWidth = width;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = sortingOrder;
            line.numCapVertices = 4;
            line.gameObject.SetActive(false);
            return line;
        }

        private void CreateArena()
        {
            CreateRing(OrbitSettings.Radius, OrbitSettings.LineColor, OrbitSettings.LineWidth);
            core = MakeSprite("Warp core", arena, new Color(.75f,1f,1f,.95f), new Vector3(.26f,.26f,1), 2).transform;
            core.gameObject.SetActive(false);
            splitPickup = MakeSprite("Split shot pickup", arena, new Color(1f,.83f,.2f,.95f), new Vector3(.2f,.2f,1), 2).transform;
            if (bonusSprite != null)
            {
                var bonusRenderer = splitPickup.GetComponent<SpriteRenderer>();
                bonusRenderer.sprite = bonusSprite;
                bonusRenderer.color = Color.white;
                SetSpriteWorldSize(bonusRenderer, .48f);
            }
            splitPickup.gameObject.SetActive(false);
            if (menuEmblemSprite != null)
            {
                menuEmblem = MakeSprite("Menu orbital emblem", arena, Color.white, Vector3.one, 1).transform;
                var emblemRenderer = menuEmblem.GetComponent<SpriteRenderer>();
                emblemRenderer.sprite = menuEmblemSprite;
                SetSpriteWorldSize(emblemRenderer, .88f);
                menuEmblem.position = new Vector2(0f, .7f);
            }
            if (warpBadgeSprite != null)
            {
                warpBadge = MakeSprite("Warp gate badge", arena, Color.white, Vector3.one, 12).transform;
                var warpRenderer = warpBadge.GetComponent<SpriteRenderer>();
                warpRenderer.sprite = warpBadgeSprite;
                SetSpriteWorldSize(warpRenderer, 1.15f);
                warpBadge.position = new Vector2(0f, .2f);
                warpBadge.gameObject.SetActive(false);
            }
        }

        private void CreateRing(float radius, Color color, float width)
        {
            var count = OrbitSettings.LineType == OrbitLineType.Solid ? 1 : OrbitSettings.DashCount;
            for (var segment = 0; segment < count; segment++)
            {
                var ring = new GameObject(count == 1 ? "Orbit ring" : "Orbit ring segment").AddComponent<LineRenderer>();
                ring.transform.SetParent(arena);
                ring.useWorldSpace = false;
                ring.loop = count == 1;
                ring.positionCount = OrbitSettings.Segments + (ring.loop ? 1 : 0);
                ring.startWidth = ring.endWidth = width;
                ring.material = new Material(Shader.Find("Sprites/Default"));
                ring.startColor = ring.endColor = color;
                ring.sortingOrder = -3;
                orbitRenderers.Add(ring);
                var start = count == 1 ? 0f : segment * Mathf.PI * 2f / count;
                var duty = OrbitSettings.LineType == OrbitLineType.Dotted ? .08f : OrbitSettings.DashDuty;
                var span = count == 1 ? Mathf.PI * 2f : Mathf.PI * 2f / count * duty;
                for (var i = 0; i < ring.positionCount; i++)
                {
                    var angle = start + i * span / OrbitSettings.Segments;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
                }
            }
        }

        private void CreatePools()
        {
            var poolRoot = new GameObject("Pools").transform;
            var enemyPrefab = MakeSprite("Enemy", poolRoot, Color.white, Vector3.one, 3).gameObject.AddComponent<Enemy>();
            var projectilePrefab = MakeSprite("Projectile", poolRoot, Color.white, new Vector3(.09f,.22f,1), 4).gameObject.AddComponent<Projectile>();
            if (projectileSprite != null)
            {
                var projectileRenderer = projectilePrefab.GetComponent<SpriteRenderer>();
                projectileRenderer.sprite = projectileSprite;
                // Не тонируем пользовательский PNG: сохраняем его исходные цвета.
                projectileRenderer.color = Color.white;
                projectilePrefab.PreserveSpriteColor = true;
                SetSpriteWorldSize(projectileRenderer, .22f);
            }
            var starPrefab = MakeSprite("Warp star", poolRoot, Color.white, Vector3.one, -1);
            starPrefab.sprite = circleSprite;
            var starParticle = starPrefab.gameObject.AddComponent<StarParticle>();
            var damagePrefab = MakeSprite("Damage shard", poolRoot, Color.red, Vector3.one, 6).gameObject.AddComponent<DamageShard>();
            enemyPool = new ObjectPool<Enemy>(enemyPrefab, poolRoot, 24);
            projectilePool = new ObjectPool<Projectile>(projectilePrefab, poolRoot, 90);
            starPool = new ObjectPool<StarParticle>(starParticle, poolRoot, 80);
            damageShardPool = new ObjectPool<DamageShard>(damagePrefab, poolRoot, 12);
            enemyPrefab.gameObject.SetActive(false);
            projectilePrefab.gameObject.SetActive(false);
            starParticle.gameObject.SetActive(false);
            damagePrefab.gameObject.SetActive(false);
        }

        private void CreatePlayer()
        {
            var sr = MakeSprite("Player", arena, shipTint, Vector3.one, 5);
            if (shipSprite != null) sr.sprite = shipSprite;
            SetSpriteWorldSize(sr, .95f);
            player = sr.transform;
            PositionOnOrbit();
        }

        private void EnsureCoopVisuals()
        {
            if (coopGuest == null)
            {
                var guestRenderer = MakeSprite("Coop guest ship", arena, Color.white, Vector3.one, 5);
                if (shipSprite != null) guestRenderer.sprite = shipSprite;
                SetSpriteWorldSize(guestRenderer, .95f);
                coopGuest = guestRenderer.transform;
            }
            if (coopHostMarker == null)
            {
                var marker = MakeSprite("Coop host marker", arena, Color.cyan, Vector3.one, 4);
                marker.sprite = circleSprite;
                SetSpriteWorldSize(marker, 1.18f);
                coopHostMarker = marker.transform;
            }
            if (coopGuestMarker == null)
            {
                var marker = MakeSprite("Coop guest marker", arena, new Color(.9f, .35f, 1f, .48f), Vector3.one, 4);
                marker.sprite = circleSprite;
                SetSpriteWorldSize(marker, 1.18f);
                coopGuestMarker = marker.transform;
            }
            if (coopEnemy == null)
            {
                var renderer = MakeSprite("Coop sector threat", arena, Color.white, Vector3.one, 3);
                renderer.sprite = pinkCanEnemySprite != null ? pinkCanEnemySprite : whiteSprite;
                SetSpriteWorldSize(renderer, .42f);
                coopEnemy = renderer.transform;
            }
            if (coopRelayCore == null)
            {
                var relayRenderer = MakeSprite("Unstable relay core", arena, new Color(.35f, .92f, 1f), Vector3.one, 4);
                relayRenderer.sprite = circleSprite;
                SetSpriteWorldSize(relayRenderer, .52f);
                coopRelayCore = relayRenderer.transform;

                var glowRenderer = MakeSprite("Relay core glow", coopRelayCore,
                    new Color(.25f, .82f, 1f, .24f), Vector3.one, 3);
                glowRenderer.sprite = circleSprite;
                SetSpriteWorldSize(glowRenderer, .82f);
                coopRelayCoreGlow = glowRenderer.transform;

                coopRelayCoreTrail = coopRelayCore.gameObject.AddComponent<TrailRenderer>();
                coopRelayCoreTrail.time = .52f;
                coopRelayCoreTrail.minVertexDistance = .035f;
                coopRelayCoreTrail.startWidth = .18f;
                coopRelayCoreTrail.endWidth = .015f;
                coopRelayCoreTrail.sortingOrder = 2;
                coopRelayCoreTrail.material = new Material(Shader.Find("Sprites/Default"));
                coopRelayCoreTrail.startColor = new Color(.48f, .94f, 1f, .82f);
                coopRelayCoreTrail.endColor = new Color(.20f, .66f, 1f, 0f);
            }
            if (coopTrajectoryRenderer == null)
            {
                coopTrajectoryRenderer = new GameObject("Coop morph trajectory").AddComponent<LineRenderer>();
                coopTrajectoryRenderer.transform.SetParent(arena);
                coopTrajectoryRenderer.useWorldSpace = false;
                coopTrajectoryRenderer.loop = true;
                coopTrajectoryRenderer.positionCount = CoopTrajectorySettings.LineSegments;
                coopTrajectoryRenderer.startWidth = coopTrajectoryRenderer.endWidth = CoopTrajectorySettings.LineWidth;
                coopTrajectoryRenderer.material = new Material(Shader.Find("Sprites/Default"));
                coopTrajectoryRenderer.sortingOrder = -2;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(.18f, .88f, 1f), 0f),
                        new GradientColorKey(new Color(.86f, .30f, 1f), .5f),
                        new GradientColorKey(new Color(.18f, .88f, 1f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(.46f, 0f),
                        new GradientAlphaKey(.82f, .5f),
                        new GradientAlphaKey(.46f, 1f)
                    });
                coopTrajectoryRenderer.colorGradient = gradient;
            }
            if (coopTetherRenderer == null)
            {
                coopTetherRenderer = new GameObject("Coop energy tether").AddComponent<LineRenderer>();
                coopTetherRenderer.transform.SetParent(arena);
                coopTetherRenderer.useWorldSpace = false;
                coopTetherRenderer.positionCount = 2;
                coopTetherRenderer.startWidth = coopTetherRenderer.endWidth = .075f;
                coopTetherRenderer.material = new Material(Shader.Find("Sprites/Default"));
                coopTetherRenderer.sortingOrder = 2;
                coopTetherRenderer.numCapVertices = 3;
                coopTetherRenderer.gameObject.SetActive(false);
            }
            if (coopThreatCleaveRenderer == null)
            {
                coopThreatCleaveRenderer = CreateThreatLine("Threat cleave wave", 15, .16f);
                coopThreatCleaveRenderer.positionCount = 13;
            }
            if (coopThreatRingLeftRenderer == null)
            {
                coopThreatRingLeftRenderer = CreateThreatLine("Threat ring left", 14, .075f);
                coopThreatRingLeftRenderer.positionCount = 25;
            }
            if (coopThreatRingRightRenderer == null)
            {
                coopThreatRingRightRenderer = CreateThreatLine("Threat ring right", 14, .075f);
                coopThreatRingRightRenderer.positionCount = 25;
            }
            if (coopThreatMineMarkers.Count == 0)
            {
                for (var i = 0; i < 3; i++)
                {
                    var marker = MakeSprite("Threat mine marker " + i.ToString("00"), arena,
                        new Color(1f, .28f, .58f, .92f), Vector3.one, 13);
                    marker.sprite = circleSprite != null ? circleSprite : whiteSprite;
                    SetSpriteWorldSize(marker, .20f);
                    marker.gameObject.SetActive(false);
                    coopThreatMineMarkers.Add(marker);
                }
            }
            if (coopRoomEnvironment == null)
            {
                coopRoomEnvironment = new GameObject("Coop procedural room environment").transform;
                coopRoomEnvironment.SetParent(arena);
                coopRoomWash = MakeSprite("Room color wash", coopRoomEnvironment,
                    new Color(.04f, .18f, .28f, .06f), new Vector3(14f, 18f, 1f), -90);
                for (var i = 0; i < 18; i++)
                {
                    var motif = MakeSprite("Room motif " + i.ToString("00"), coopRoomEnvironment,
                        Color.clear, Vector3.one, -4);
                    motif.sprite = i % 3 == 0 ? circleSprite : whiteSprite;
                    coopRoomMotifs.Add(motif);
                }
            }
            SetCoopVisualsActive(false);
        }

        private void SetCoopVisualsActive(bool active)
        {
            if (coopGuest != null) coopGuest.gameObject.SetActive(active);
            if (coopHostMarker != null) coopHostMarker.gameObject.SetActive(active);
            if (coopGuestMarker != null) coopGuestMarker.gameObject.SetActive(active);
            if (coopEnemy != null) coopEnemy.gameObject.SetActive(active);
            if (coopRelayCore != null)
            {
                coopRelayCore.gameObject.SetActive(active);
                if (!active && coopRelayCoreTrail != null) coopRelayCoreTrail.Clear();
            }
            if (coopTrajectoryRenderer != null) coopTrajectoryRenderer.gameObject.SetActive(active);
            if (coopTetherRenderer != null) coopTetherRenderer.gameObject.SetActive(false);
            if (coopThreatCleaveRenderer != null) coopThreatCleaveRenderer.gameObject.SetActive(false);
            if (coopThreatRingLeftRenderer != null) coopThreatRingLeftRenderer.gameObject.SetActive(false);
            if (coopThreatRingRightRenderer != null) coopThreatRingRightRenderer.gameObject.SetActive(false);
            for (var i = 0; i < coopThreatMineMarkers.Count; i++)
                if (coopThreatMineMarkers[i] != null) coopThreatMineMarkers[i].gameObject.SetActive(false);
            if (coopRoomEnvironment != null) coopRoomEnvironment.gameObject.SetActive(active);
            for (var i = 0; i < orbitRenderers.Count; i++)
                if (orbitRenderers[i] != null) orbitRenderers[i].enabled = !active;
        }

        private void BeginCoopRun(bool localPreview, bool soloExpedition = false)
        {
            coopLocalPreview = localPreview;
            soloExpeditionPlaying = soloExpedition;
            coopPlaying = true;
            UpdateCameraFraming(true);
            playing = false;
            paused = false;
            showMenu = false;
            showSettings = false;
            showCoop = false;
            showResults = false;
            Cleanup();
            EnsureCoopVisuals();
            SetCoopVisualsActive(true);
            if (soloExpeditionPlaying)
            {
                if (coopGuest != null) coopGuest.gameObject.SetActive(false);
                if (coopGuestMarker != null) coopGuestMarker.gameObject.SetActive(false);
            }
            player.gameObject.SetActive(true);
            playerCommandSource?.Reset();
            activeControlDirection = 0;
            lastCoopHostShots = localPreview ? 0u : coopSimulation.HostShotSequence;
            lastCoopGuestShots = localPreview ? 0u : coopSimulation.GuestShotSequence;
            coopPreviewHostAngle = 210f;
            coopPreviewGuestAngle = 330f;
            coopPreviewTrajectoryTime = CoopTrajectorySettings.InitialElapsedSeconds;
            coopPreviewHostShots = coopPreviewGuestShots = 0;
            coopPreviewHostFireTimer = .25f;
            coopPreviewGuestFireTimer = .48f;
            coopPreviewRunSeed = soloExpeditionPlaying
                ? Mathf.Max(1, Guid.NewGuid().GetHashCode() & int.MaxValue)
                : 27082026;
            coopPreviewSector = SectorGenerator.Generate(coopPreviewRunSeed);
            coopPreviewRoomIndex = 0;
            coopPreviewRoomTimer = 0f;
            coopPreviewEnemyAngle = 91f;
            coopPreviewEnemyRadius = CoopTrajectorySettings.ThreatSpawnRadius;
            coopPreviewEnemyMaxHealth = 0;
            coopPreviewEnemyHealth = 0;
            coopPreviewEnemyKind = (byte)SectorRoomType.Start;
            coopPreviewResonance = ElementalReaction.None;
            coopPreviewResonanceTimer = 0f;
            coopPreviewResonanceSequence = 0;
            coopPreviewHasLastElement = false;
            coopPreviewLastElement = DamageElement.Kinetic;
            coopPreviewLastElementAge = 0f;
            coopPreviewThreatAttackTimer = 0f;
            coopPreviewThreatWindupTimer = 0f;
            coopPreviewThreatTargetHostAngle = coopPreviewHostAngle;
            coopPreviewThreatTargetGuestAngle = coopPreviewGuestAngle;
            coopPreviewRoomEntryGraceTimer = 0f;
            coopPreviewTeamDamageCooldown = 0f;
            coopPreviewThreatPulseTimer = 0f;
            coopPreviewThreatPulseSequence = 0;
            coopPreviewThreatPulseElement = DamageElement.Kinetic;
            coopPreviewThreatPattern = CoopThreatPattern.Bolt;
            coopPreviewThreatTargetsHost = true;
            coopPreviewThreatPatternAngle = 0f;
            coopPreviewPlayerShots.Clear();
            coopThreatPatternVisualTimer = 0f;
            coopThreatPatternVisualDuration = 0f;
            lastCoopThreatPulseSequence = 0;
            coopPreviewCompleted = false;
            coopPreviewCompletionSequence = 0;
            lastCoopCompletionSequence = 0;
            coopPreviewTeamMaxHealth = soloExpeditionPlaying
                ? CoopRoomRules.SoloExpeditionMaxHealth
                : CoopRoomRules.TeamMaxHealth;
            coopPreviewTeamHealth = coopPreviewTeamMaxHealth;
            coopPreviewFailed = false;
            coopPreviewFailureSequence = 0;
            lastCoopFailureSequence = 0;
            coopPreviewCollisionCooldown = 0f;
            coopPreviewCollisionSequence = 0;
            lastCoopCollisionSequence = localPreview || coopSimulation == null ? 0u : coopSimulation.ShipCollisionSequence;
            coopPreviewCollisionPosition = Vector2.zero;
            coopCollisionBannerTimer = 0f;
            coopPreviewRelayCoreEventSequence = 0;
            lastCoopRelayCoreEventSequence = localPreview || coopSimulation == null ? 0u : coopSimulation.RelayCoreEventSequence;
            coopRelayCoreBannerTimer = 0f;
            coopRelayCoreBanner = string.Empty;
            ResetCoopPreviewTetherState();
            lastCoopTetherEventSequence = localPreview || coopSimulation == null ? 0u : coopSimulation.TetherEventSequence;
            coopTetherBannerTimer = 0f;
            coopTetherBanner = string.Empty;
            ResetCoopPreviewRedirectState();
            lastCoopRedirectSequence = localPreview || coopSimulation == null ? 0u : coopSimulation.FriendlyRedirectSequence;
            coopRedirectBannerTimer = 0f;
            coopRedirectBanner = string.Empty;
            coopRoomEnvironmentSignature = int.MinValue;
            coopObservedRoomIndex = -1;
            coopRoomIntroTimer = 0f;
            coopPreviousEnemyHealth = -1;
            coopThreatDefeatedBannerTimer = 0f;
            coopPreviousTeamHealth = -1;
            coopHullHitBannerTimer = 0f;
            coopLastHullDamage = 0;
            coopResultScore = 0;
            coopResultMmrDelta = 0;
            coopResultFingerprint = string.Empty;
            coopResultRunId = soloExpeditionPlaying
                ? "solo-expedition-" + Guid.NewGuid().ToString("N")
                : localPreview
                ? "preview-" + coopPreviewRunSeed
                : (multiplayerSessions == null ? string.Empty : multiplayerSessions.RunId);
            if (string.IsNullOrWhiteSpace(coopResultRunId))
                coopResultRunId = "coop-" + (multiplayerSessions == null ? coopPreviewRunSeed : multiplayerSessions.RunSeed);
            coopResultSubmitted = false;
            ResetCoopPreviewEnemy();
            ConfigureCoopMarkers();
            ConfigureCoopEnemyVisual(coopPreviewEnemyKind);
            if (GameAudioSettings.MusicEnabled && musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
                musicSource.Play();
            SpawnWarpBurst(54, 1.6f);
            BeginUiFade();
        }

        private void ConfigureCoopMarkers()
        {
            var host = ShipLoadoutSettings.Get(CoopHostShip());
            var guest = ShipLoadoutSettings.Get(CoopGuestShip());
            if (coopHostMarker != null)
            {
                var color = host.ProjectileColor;
                color.a = .42f;
                coopHostMarker.GetComponent<SpriteRenderer>().color = color;
            }
            if (coopGuestMarker != null)
            {
                var color = guest.ProjectileColor;
                color.a = .42f;
                coopGuestMarker.GetComponent<SpriteRenderer>().color = color;
            }
        }

        private void UpdateCoopRun(float dt, int localDirection)
        {
            activeControlDirection = localDirection;
            coopCollisionBannerTimer = Mathf.Max(0f, coopCollisionBannerTimer - Mathf.Max(0f, dt));
            coopRelayCoreBannerTimer = Mathf.Max(0f, coopRelayCoreBannerTimer - Mathf.Max(0f, dt));
            coopTetherBannerTimer = Mathf.Max(0f, coopTetherBannerTimer - Mathf.Max(0f, dt));
            coopRedirectBannerTimer = Mathf.Max(0f, coopRedirectBannerTimer - Mathf.Max(0f, dt));
            coopThreatDefeatedBannerTimer = Mathf.Max(0f, coopThreatDefeatedBannerTimer - Mathf.Max(0f, dt));
            coopHullHitBannerTimer = Mathf.Max(0f, coopHullHitBannerTimer - Mathf.Max(0f, dt));
            float hostAngle;
            float guestAngle;
            uint hostShots;
            uint guestShots;
            float enemyAngle;
            float enemyRadius;
            int enemyHealth;
            int enemyMaxHealth;
            byte enemyKind;
            bool runCompleted;
            uint runCompletionSequence;
            bool runFailed;
            uint runFailureSequence;
            uint collisionSequence;
            Vector2 collisionPosition;
            bool relayCoreActive;
            Vector2 relayCorePosition;
            byte relayCoreCharge;
            DamageElement relayCoreElement;
            bool relayCoreDangerous;
            uint relayCoreEventSequence;
            byte relayCoreEventKind;
            Vector2 relayCoreEventPosition;
            bool tetherActive;
            float tetherHeat;
            float tetherOverloadTimer;
            uint tetherEventSequence;
            byte tetherEventKind;
            Vector2 tetherEventPosition;
            uint redirectSequence;
            byte redirectKind;
            Vector2 redirectPosition;
            bool redirectFromHost;
            DamageElement redirectElement;
            int roomIndex;
            int runSeed;
            if (coopLocalPreview)
            {
                if (!coopPreviewCompleted && !coopPreviewFailed)
                {
                    coopPreviewTrajectoryTime += Mathf.Max(0f, dt);
                    coopPreviewLastElementAge += Mathf.Max(0f, dt);
                    if (coopPreviewLastElementAge > 1.2f) coopPreviewHasLastElement = false;
                    coopPreviewResonanceTimer = Mathf.Max(0f, coopPreviewResonanceTimer - Mathf.Max(0f, dt));
                    coopPreviewThreatPulseTimer = Mathf.Max(0f, coopPreviewThreatPulseTimer - Mathf.Max(0f, dt));
                    coopPreviewRoomEntryGraceTimer = Mathf.Max(0f,
                        coopPreviewRoomEntryGraceTimer - Mathf.Max(0f, dt));
                    coopPreviewHostAngle = CoopSimulationRules.StepAngle(coopPreviewHostAngle, localDirection, dt);
                    if (!soloExpeditionPlaying)
                    {
                        var hostPosition = CoopTrajectorySettings.Position(coopPreviewHostAngle, coopPreviewTrajectoryTime);
                        var guestPosition = CoopTrajectorySettings.Position(coopPreviewGuestAngle, coopPreviewTrajectoryTime);
                        int guestDirection;
                        if (Vector2.Distance(hostPosition, guestPosition) > CoopTetherRules.ActivationDistance * .82f)
                            guestDirection = CoopTetherRules.DirectionToward(coopPreviewGuestAngle, hostPosition,
                                coopPreviewTrajectoryTime);
                        else if (coopPreviewTetherActive && coopPreviewEnemyHealth > 0)
                        {
                            var enemyRadians = coopPreviewEnemyAngle * Mathf.Deg2Rad;
                            var enemyPosition = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) *
                                                coopPreviewEnemyRadius;
                            var interceptionPoint = Vector2.Lerp(hostPosition, enemyPosition, .52f);
                            guestDirection = CoopTetherRules.DirectionToward(coopPreviewGuestAngle,
                                interceptionPoint, coopPreviewTrajectoryTime);
                        }
                        else
                            guestDirection = Mathf.Sin(Time.unscaledTime * .85f) >= 0f ? 1 : -1;
                        coopPreviewGuestAngle = CoopSimulationRules.StepAngle(coopPreviewGuestAngle, guestDirection, dt);
                        UpdateLocalCoopShipCollision(dt);
                    }
                    UpdateCoopPreviewFire(dt);
                    if (coopPreviewEnemyHealth > 0)
                        coopPreviewRoomTimer = 0f;
                    else
                        coopPreviewRoomTimer += Mathf.Max(0f, dt);
                    if (coopPreviewRoomTimer >= CoopRoomRules.RoomClearDelay && coopPreviewSector != null && coopPreviewEnemyHealth <= 0)
                    {
                        coopPreviewRoomTimer = 0f;
                        if (coopPreviewRoomIndex >= coopPreviewSector.Rooms.Count - 1)
                        {
                            coopPreviewCompleted = true;
                            coopPreviewCompletionSequence++;
                        }
                        else
                        {
                            coopPreviewRoomIndex++;
                            ResetCoopPreviewEnemy();
                        }
                    }
                    var previewRoomType = (SectorRoomType)Mathf.Clamp(coopPreviewEnemyKind, 0, (int)SectorRoomType.Boss);
                    coopPreviewEnemyAngle = Mathf.Repeat(coopPreviewEnemyAngle + Mathf.Max(0f, dt) * CoopRoomRules.EnemyOrbitSpeed(previewRoomType), 360f);
                    coopPreviewEnemyRadius = Mathf.MoveTowards(coopPreviewEnemyRadius,
                        CoopTrajectorySettings.ThreatOrbitRadius, Mathf.Max(0f, dt) * .44f);
                    UpdateCoopPreviewTether(dt);
                }
                hostAngle = coopPreviewHostAngle;
                guestAngle = coopPreviewGuestAngle;
                hostShots = coopPreviewHostShots;
                guestShots = soloExpeditionPlaying ? 0u : coopPreviewGuestShots;
                var hostDelta = hostShots > lastCoopHostShots ? hostShots - lastCoopHostShots : 0u;
                var guestDelta = guestShots > lastCoopGuestShots ? guestShots - lastCoopGuestShots : 0u;
                ApplyCoopPreviewDamage(hostDelta, guestDelta);
                if (coopLocalPreview) UpdateCoopPreviewPlayerShots(dt);
                UpdateCoopPreviewRelayCore(dt);
                if (!coopPreviewCompleted && !coopPreviewFailed)
                    UpdateCoopPreviewThreatPulse(dt);
                enemyAngle = coopPreviewEnemyAngle;
                enemyRadius = coopPreviewEnemyRadius;
                enemyHealth = coopPreviewEnemyHealth;
                enemyMaxHealth = coopPreviewEnemyMaxHealth;
                enemyKind = coopPreviewEnemyKind;
                runCompleted = coopPreviewCompleted;
                runCompletionSequence = coopPreviewCompletionSequence;
                runFailed = coopPreviewFailed;
                runFailureSequence = coopPreviewFailureSequence;
                collisionSequence = coopPreviewCollisionSequence;
                collisionPosition = coopPreviewCollisionPosition;
                relayCoreActive = coopPreviewRelayCoreActive;
                relayCorePosition = coopPreviewRelayCorePosition;
                relayCoreCharge = coopPreviewRelayCoreCharge;
                relayCoreElement = coopPreviewRelayCoreElement;
                relayCoreDangerous = coopPreviewRelayCoreDangerous;
                relayCoreEventSequence = coopPreviewRelayCoreEventSequence;
                relayCoreEventKind = coopPreviewRelayCoreEventKind;
                relayCoreEventPosition = coopPreviewRelayCoreEventPosition;
                tetherActive = coopPreviewTetherActive;
                tetherHeat = coopPreviewTetherHeat;
                tetherOverloadTimer = coopPreviewTetherOverloadTimer;
                tetherEventSequence = coopPreviewTetherEventSequence;
                tetherEventKind = coopPreviewTetherEventKind;
                tetherEventPosition = coopPreviewTetherEventPosition;
                redirectSequence = coopPreviewRedirectSequence;
                redirectKind = coopPreviewRedirectKind;
                redirectPosition = coopPreviewRedirectPosition;
                redirectFromHost = coopPreviewRedirectFromHost;
                redirectElement = coopPreviewRedirectElement;
                roomIndex = coopPreviewRoomIndex;
                runSeed = coopPreviewRunSeed;
            }
            else
            {
                hostAngle = coopSimulation.HostAngleDegrees;
                guestAngle = coopSimulation.GuestAngleDegrees;
                hostShots = coopSimulation.HostShotSequence;
                guestShots = coopSimulation.GuestShotSequence;
                enemyAngle = coopSimulation.CoopEnemyAngle;
                enemyRadius = coopSimulation.CoopEnemyRadius;
                enemyHealth = coopSimulation.CoopEnemyHealth;
                enemyMaxHealth = coopSimulation.CoopEnemyMaxHealth;
                enemyKind = coopSimulation.CoopEnemyKind;
                runCompleted = coopSimulation.RunCompleted;
                runCompletionSequence = coopSimulation.RunCompletionSequence;
                runFailed = coopSimulation.RunFailed;
                runFailureSequence = coopSimulation.RunFailureSequence;
                collisionSequence = coopSimulation.ShipCollisionSequence;
                collisionPosition = coopSimulation.ShipCollisionPosition;
                relayCoreActive = coopSimulation.RelayCoreActive;
                relayCorePosition = coopSimulation.RelayCorePosition;
                relayCoreCharge = coopSimulation.RelayCoreCharge;
                relayCoreElement = coopSimulation.RelayCoreElement;
                relayCoreDangerous = coopSimulation.RelayCoreDangerous;
                relayCoreEventSequence = coopSimulation.RelayCoreEventSequence;
                relayCoreEventKind = coopSimulation.RelayCoreEventKind;
                relayCoreEventPosition = coopSimulation.RelayCoreEventPosition;
                tetherActive = coopSimulation.TetherActive;
                tetherHeat = coopSimulation.TetherHeat;
                tetherOverloadTimer = coopSimulation.TetherOverloadTimer;
                tetherEventSequence = coopSimulation.TetherEventSequence;
                tetherEventKind = coopSimulation.TetherEventKind;
                tetherEventPosition = coopSimulation.TetherEventPosition;
                redirectSequence = coopSimulation.FriendlyRedirectSequence;
                redirectKind = coopSimulation.FriendlyRedirectKind;
                redirectPosition = coopSimulation.FriendlyRedirectPosition;
                redirectFromHost = coopSimulation.FriendlyRedirectFromHost;
                redirectElement = coopSimulation.FriendlyRedirectElement;
                roomIndex = coopSimulation.ActiveRoomIndex;
                runSeed = coopSimulation.ActiveRunSeed;
            }

            if (roomIndex != coopObservedRoomIndex)
            {
                coopObservedRoomIndex = roomIndex;
                coopRoomIntroTimer = 3.4f;
            }
            else
                coopRoomIntroTimer = Mathf.Max(0f, coopRoomIntroTimer - Mathf.Max(0f, dt));

            var trajectoryTime = coopLocalPreview ? coopPreviewTrajectoryTime : coopSimulation.TrajectoryTimeSeconds;
            UpdateCoopTrajectory(trajectoryTime);
            UpdateCoopRoomEnvironment(runSeed, roomIndex,
                (SectorRoomType)Mathf.Clamp(enemyKind, 0, (int)SectorRoomType.Boss), dt);
            PositionCoopShip(player, coopHostMarker, hostAngle, trajectoryTime);
            if (!soloExpeditionPlaying)
                PositionCoopShip(coopGuest, coopGuestMarker, guestAngle, trajectoryTime);
            UpdateCoopTetherVisual(!soloExpeditionPlaying && tetherActive, tetherHeat,
                tetherOverloadTimer, hostAngle, guestAngle, trajectoryTime);
            if (collisionSequence != lastCoopCollisionSequence)
            {
                lastCoopCollisionSequence = collisionSequence;
                SpawnImpactBurst(collisionPosition, new Color(.55f, .92f, 1f, 1f), 24, 3.4f, .42f);
                SpawnImpactBurst(collisionPosition, new Color(1f, .40f, .86f, 1f), 14, 2.2f, .34f);
                PlayEffect(coopBumpSound, .9f);
                AddScreenShake(.18f, .11f);
                HapticFeedback.Pulse(45);
                coopCollisionBannerTimer = .8f;
            }
            EmitCoopShots(player, CoopHostShip(), ref lastCoopHostShots, hostShots);
            if (!soloExpeditionPlaying)
                EmitCoopShots(coopGuest, CoopGuestShip(), ref lastCoopGuestShots, guestShots);
            ConfigureCoopEnemyVisual(enemyKind);
            if (coopPreviousEnemyHealth > 0 && enemyHealth <= 0)
            {
                PositionCoopEnemy(enemyAngle, enemyRadius, true);
                var deathColor = SectorRoomColor((SectorRoomType)Mathf.Clamp(enemyKind, 0, (int)SectorRoomType.Boss));
                SpawnImpactBurst(coopEnemy.position, deathColor,
                    enemyKind == (byte)SectorRoomType.Boss ? 54 : 28,
                    enemyKind == (byte)SectorRoomType.Boss ? 4.8f : 3.2f, .72f);
                PlayEnemyDeathEffect(enemyKind == (byte)SectorRoomType.Boss ? .95f : .72f);
                AddScreenShake(enemyKind == (byte)SectorRoomType.Boss ? .28f : .14f,
                    enemyKind == (byte)SectorRoomType.Boss ? .16f : .08f);
                coopThreatDefeatedBannerTimer = CoopRoomRules.RoomClearDelay;
            }
            PositionCoopEnemy(enemyAngle, enemyRadius, enemyHealth > 0 && !runCompleted && !runFailed);
            coopPreviousEnemyHealth = enemyHealth;
            var currentTeamHealth = coopLocalPreview
                ? coopPreviewTeamHealth
                : (coopSimulation == null ? 0 : coopSimulation.CoopTeamHealth);
            if (coopPreviousTeamHealth >= 0 && currentTeamHealth < coopPreviousTeamHealth)
            {
                coopLastHullDamage = coopPreviousTeamHealth - currentTeamHealth;
                coopHullHitBannerTimer = 1.05f;
                var hitColor = CoopElementColor(coopLocalPreview
                    ? coopPreviewThreatPulseElement
                    : (coopSimulation == null ? DamageElement.Kinetic : coopSimulation.CoopThreatPulseElement));
                SpawnImpactBurst(player.position, hitColor, 16, 2.1f, .34f);
                PlayEffect(playerDamageSound, .52f);
                AddScreenShake(.13f, .08f);
                HapticFeedback.Pulse(32);
            }
            coopPreviousTeamHealth = currentTeamHealth;
            PositionCoopRelayCore(relayCoreActive, relayCorePosition, relayCoreCharge,
                relayCoreElement, relayCoreDangerous);
            if (relayCoreEventSequence != lastCoopRelayCoreEventSequence)
            {
                lastCoopRelayCoreEventSequence = relayCoreEventSequence;
                var eventColor = relayCoreEventKind == 4
                    ? new Color(1f, .25f, .32f)
                    : relayCoreEventKind == 3
                        ? new Color(1f, .32f, .72f)
                        : CoopElementColor(relayCoreElement);
                SpawnImpactBurst(relayCoreEventPosition, eventColor,
                    relayCoreEventKind == 2 ? 28 : 20, relayCoreEventKind == 2 ? 3.6f : 2.7f, .42f);
                AddScreenShake(relayCoreEventKind == 2 ? .18f : .12f, relayCoreEventKind == 2 ? .11f : .07f);
                coopRelayCoreBanner = relayCoreEventKind == 4 ? "ОБРАТКА // -1 КОРПУС" :
                    relayCoreEventKind == 3 ? "БОСС ОТБИЛ ЯДРО!" : "ЯДРО ПРОБИЛО УГРОЗУ";
                coopRelayCoreBannerTimer = 1.15f;
            }
            if (tetherEventSequence != lastCoopTetherEventSequence)
            {
                lastCoopTetherEventSequence = tetherEventSequence;
                var tetherColor = tetherEventKind == 4 ? new Color(1f, .24f, .48f) :
                    tetherEventKind == 3 ? new Color(1f, .82f, .24f) : new Color(.28f, .92f, 1f);
                SpawnImpactBurst(tetherEventPosition, tetherColor,
                    tetherEventKind == 3 || tetherEventKind == 4 ? 30 : 12,
                    tetherEventKind == 3 || tetherEventKind == 4 ? 3.8f : 2.1f, .40f);
                if (tetherEventKind == 3 || tetherEventKind == 4)
                {
                    PlayEffect(coopTetherOverloadSound, .92f);
                    AddScreenShake(.20f, .12f);
                    HapticFeedback.Pulse(55);
                }
                coopTetherBanner = tetherEventKind == 1 ? "СЦЕПКА // КОНТАКТ" :
                    tetherEventKind == 2 ? "СЦЕПКА РЕЖЕТ УГРОЗУ" :
                    tetherEventKind == 3 ? "ПЕРЕГРУЗКА // УДАР ПО УГРОЗЕ" :
                    tetherEventKind == 4 ? "ПЕРЕГРУЗКА // ОБРАТКА КОМАНДЕ" :
                    "СЦЕПКА ЗАРЯДИЛА ЯДРО";
                coopTetherBannerTimer = tetherEventKind == 2 ? .48f : 1.05f;
            }
            if (redirectSequence != lastCoopRedirectSequence)
            {
                lastCoopRedirectSequence = redirectSequence;
                var color = CoopElementColor(redirectElement);
                SpawnImpactBurst(redirectPosition, color, redirectKind == 1 ? 24 : 16,
                    redirectKind == 1 ? 3.4f : 2.6f, redirectKind == 1 ? .44f : .34f);
                PlayEffect(coopRicochetSound, redirectKind == 1 ? .88f : .68f);
                AddScreenShake(redirectKind == 1 ? .12f : .08f, redirectKind == 1 ? .075f : .05f);
                HapticFeedback.Pulse(redirectKind == 1 ? 35 : 22);
                if (redirectKind == 1 && coopEnemy != null)
                {
                    var speed = BalanceSettings.PlayerProjectileSpeed(1) * 1.28f;
                    var aim = (Vector2)(coopEnemy.position - (Vector3)redirectPosition);
                    if (aim.sqrMagnitude < .001f) aim = Vector2.up;
                    Shoot(redirectPosition, aim.normalized * speed, true, color, redirectElement, 0f);
                }
                coopRedirectBanner = redirectKind == 1
                    ? (redirectFromHost ? "РИКОШЕТ P1 > P2 // УРОН x1.65" : "РИКОШЕТ P2 > P1 // УРОН x1.65")
                    : "ДРУЖЕСКИЙ БУМ // ПИНБОЛ";
                coopRedirectBannerTimer = 1.05f;
            }
            var threatPulseSequence = coopLocalPreview ? coopPreviewThreatPulseSequence : (coopSimulation == null ? 0u : coopSimulation.CoopThreatPulseSequence);
            var threatPulseTimer = coopLocalPreview ? coopPreviewThreatPulseTimer : (coopSimulation == null ? 0f : coopSimulation.CoopThreatPulseTimer);
            var threatPulseElement = coopLocalPreview ? coopPreviewThreatPulseElement : (coopSimulation == null ? DamageElement.Kinetic : coopSimulation.CoopThreatPulseElement);
            var threatPattern = coopLocalPreview ? coopPreviewThreatPattern : (coopSimulation == null ? CoopThreatPattern.Bolt : coopSimulation.CoopThreatPattern);
            var threatTargetsHost = coopLocalPreview ? coopPreviewThreatTargetsHost : (coopSimulation == null || coopSimulation.CoopThreatTargetsHost);
            var threatPatternAngle = coopLocalPreview ? coopPreviewThreatPatternAngle : (coopSimulation == null ? 0f : coopSimulation.CoopThreatPatternAngle);
            if (coopEnemy != null && threatPulseSequence != lastCoopThreatPulseSequence)
            {
                lastCoopThreatPulseSequence = threatPulseSequence;
                var pulseColor = CoopElementColor(threatPulseElement);
                SpawnImpactBurst(coopEnemy.position, pulseColor, 22, 3.2f, .48f);
                var pulseRoom = (SectorRoomType)Mathf.Clamp(enemyKind, 0, (int)SectorRoomType.Boss);
                var threatDamage = CoopRoomRules.ThreatDamage(pulseRoom);
                if (threatDamage > 0)
                {
                    coopThreatPatternVisualTimer = CoopThreatAttackRules.Windup(threatPattern);
                    coopThreatPatternVisualDuration = coopThreatPatternVisualTimer;
                    coopThreatVisualPattern = threatPattern;
                    coopThreatVisualOrigin = coopEnemy.position;
                    coopThreatVisualTarget = threatTargetsHost || soloExpeditionPlaying || coopGuest == null
                        ? player.position : coopGuest.position;
                    coopThreatVisualTargetAngle = coopLocalPreview
                        ? (coopPreviewThreatTargetsHost ? coopPreviewThreatTargetHostAngle : coopPreviewThreatTargetGuestAngle)
                        : (threatTargetsHost ? coopSimulation.HostAngleDegrees : coopSimulation.GuestAngleDegrees);
                    coopThreatVisualPatternAngle = threatPatternAngle;
                    coopThreatVisualColor = pulseColor;
                    if (threatPattern == CoopThreatPattern.Bolt)
                    {
                        EmitCoopThreatBolts(coopEnemy.position, coopThreatVisualTarget, pulseRoom, pulseColor, threatPulseElement);
                    }
                }
                else
                {
                    coopThreatPatternVisualTimer = 0f;
                }
                AddScreenShake(.08f, .045f);
            }
            UpdateCoopThreatPatternVisual(dt, trajectoryTime);
            if (runFailed && runFailureSequence != lastCoopFailureSequence)
            {
                lastCoopFailureSequence = runFailureSequence;
                FailCoopRun(layoutForFailure: coopLocalPreview ? coopPreviewSector : multiplayerSessions?.CurrentSector);
                SpawnImpactBurst(player.position, new Color(1f, .16f, .20f), 28, 2.5f, .46f);
                AddScreenShake(.30f, .20f);
                PlayEffect(playerDamageSound, .95f);
            }
            if (!runFailed && runCompleted && runCompletionSequence != lastCoopCompletionSequence)
            {
                lastCoopCompletionSequence = runCompletionSequence;
                CompleteCoopRun(layoutForCompletion: coopLocalPreview ? coopPreviewSector : multiplayerSessions?.CurrentSector);
                SpawnWarpBurst(100, 2.8f);
                AddScreenShake(.24f, .16f);
                PlayEnemyDeathEffect(.9f);
            }
            UpdateProjectiles(dt);
        }

        private void UpdateLocalCoopShipCollision(float deltaTime)
        {
            coopPreviewCollisionCooldown = Mathf.Max(0f,
                coopPreviewCollisionCooldown - Mathf.Max(0f, deltaTime));
            if (coopPreviewCollisionCooldown > 0f) return;
            if (!CoopSimulationRules.TryBounceShips(ref coopPreviewHostAngle, ref coopPreviewGuestAngle,
                    coopPreviewTrajectoryTime, out var impactPosition)) return;
            coopPreviewCollisionPosition = impactPosition;
            coopPreviewCollisionSequence++;
            coopPreviewCollisionCooldown = CoopSimulationRules.ShipCollisionCooldown;
        }

        private void ResetCoopPreviewTetherState()
        {
            coopPreviewTetherActive = false;
            coopPreviewTetherHeat = 0f;
            coopPreviewTetherOverloadTimer = 0f;
            coopPreviewTetherDamageTimer = 0f;
            coopPreviewTetherCoreTimer = 0f;
            coopPreviewTetherReconnectCooldown = .8f;
            coopPreviewTetherEventSequence = 0;
            coopPreviewTetherEventKind = 0;
            coopPreviewTetherEventPosition = Vector2.zero;
        }

        private void ResetCoopPreviewRedirectState()
        {
            coopPreviewRedirectSequence = 0;
            coopPreviewRedirectKind = 0;
            coopPreviewRedirectPosition = Vector2.zero;
            coopPreviewRedirectFromHost = false;
            coopPreviewRedirectElement = DamageElement.Kinetic;
            coopPreviewRedirectCooldown = 0f;
        }

        private void UpdateCoopPreviewTether(float dt)
        {
            if (soloExpeditionPlaying || coopPreviewCompleted || coopPreviewFailed)
            {
                coopPreviewTetherActive = false;
                coopPreviewTetherOverloadTimer = 0f;
                return;
            }
            dt = Mathf.Max(0f, dt);
            coopPreviewTetherReconnectCooldown = Mathf.Max(0f, coopPreviewTetherReconnectCooldown - dt);
            coopPreviewTetherDamageTimer = Mathf.Max(0f, coopPreviewTetherDamageTimer - dt);
            coopPreviewTetherCoreTimer = Mathf.Max(0f, coopPreviewTetherCoreTimer - dt);
            coopPreviewTetherOverloadTimer = Mathf.Max(0f, coopPreviewTetherOverloadTimer - dt);

            var hostPosition = CoopTrajectorySettings.Position(coopPreviewHostAngle, coopPreviewTrajectoryTime);
            var guestPosition = CoopTrajectorySettings.Position(coopPreviewGuestAngle, coopPreviewTrajectoryTime);
            var distance = Vector2.Distance(hostPosition, guestPosition);
            if (!coopPreviewTetherActive)
            {
                coopPreviewTetherHeat = Mathf.MoveTowards(coopPreviewTetherHeat, 0f, dt * .52f);
                if (!CoopTetherRules.CanConnect(distance, coopPreviewTetherReconnectCooldown)) return;
                coopPreviewTetherActive = true;
                coopPreviewTetherHeat = Mathf.Min(coopPreviewTetherHeat, .18f);
                coopPreviewTetherEventKind = 1;
                coopPreviewTetherEventPosition = (hostPosition + guestPosition) * .5f;
                coopPreviewTetherEventSequence++;
            }

            CoopTetherRules.PullAngles(ref coopPreviewHostAngle, ref coopPreviewGuestAngle,
                coopPreviewTrajectoryTime, dt);
            hostPosition = CoopTrajectorySettings.Position(coopPreviewHostAngle, coopPreviewTrajectoryTime);
            guestPosition = CoopTrajectorySettings.Position(coopPreviewGuestAngle, coopPreviewTrajectoryTime);
            distance = Vector2.Distance(hostPosition, guestPosition);
            var tension = CoopTetherRules.Tension(distance);
            if (tension < .14f)
                coopPreviewTetherHeat = Mathf.MoveTowards(coopPreviewTetherHeat, 0f, dt * .20f);
            else
                coopPreviewTetherHeat = Mathf.Clamp01(coopPreviewTetherHeat + dt * (.07f + tension * .62f));

            var enemyRadians = coopPreviewEnemyAngle * Mathf.Deg2Rad;
            var enemyPosition = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) * coopPreviewEnemyRadius;
            if (coopPreviewEnemyHealth > 0 && coopPreviewTetherDamageTimer <= 0f &&
                CoopTetherRules.DistanceToSegment(enemyPosition, hostPosition, guestPosition) <= CoopTetherRules.CutRadius)
            {
                coopPreviewEnemyHealth = Mathf.Max(0, coopPreviewEnemyHealth - 1);
                coopPreviewTetherEventKind = 2;
                coopPreviewTetherEventPosition = enemyPosition;
                coopPreviewTetherEventSequence++;
                coopPreviewTetherDamageTimer = CoopTetherRules.DamageInterval;
            }

            if (coopPreviewRelayCoreActive && coopPreviewTetherCoreTimer <= 0f &&
                CoopTetherRules.DistanceToSegment(coopPreviewRelayCorePosition, hostPosition, guestPosition) <=
                CoopTetherRules.CutRadius + .14f)
            {
                var direction = (enemyPosition - coopPreviewRelayCorePosition).normalized;
                if (direction.sqrMagnitude < .001f) direction = (guestPosition - hostPosition).normalized;
                coopPreviewRelayCoreVelocity = Vector2.ClampMagnitude(
                    coopPreviewRelayCoreVelocity + direction * 1.45f, CoopRelayCoreRules.MaxSpeed);
                coopPreviewRelayCoreCharge = (byte)Mathf.Min(CoopRelayCoreRules.MaxCharge,
                    coopPreviewRelayCoreCharge + 1);
                coopPreviewRelayCoreElement = DamageElement.Kinetic;
                coopPreviewRelayCoreDangerous = false;
                coopPreviewTetherEventKind = 5;
                coopPreviewTetherEventPosition = coopPreviewRelayCorePosition;
                coopPreviewTetherEventSequence++;
                coopPreviewTetherCoreTimer = CoopTetherRules.CoreChargeInterval;
            }

            if (distance > CoopTetherRules.BreakDistance || coopPreviewTetherHeat >= 1f)
                DischargeCoopPreviewTether(hostPosition, guestPosition, enemyPosition);
        }

        private void DischargeCoopPreviewTether(Vector2 hostPosition, Vector2 guestPosition, Vector2 enemyPosition)
        {
            var hitsEnemy = coopPreviewEnemyHealth > 0 &&
                            CoopTetherRules.DistanceToSegment(enemyPosition, hostPosition, guestPosition) <=
                            CoopTetherRules.CutRadius + .32f;
            if (hitsEnemy)
            {
                coopPreviewEnemyHealth = Mathf.Max(0, coopPreviewEnemyHealth - CoopTetherRules.OverloadDamage);
                coopPreviewTetherEventKind = 3;
                coopPreviewTetherEventPosition = enemyPosition;
            }
            else
            {
                coopPreviewTeamHealth = CoopTetherRules.ApplySafeBacklash(coopPreviewTeamHealth);
                coopPreviewTetherEventKind = 4;
                coopPreviewTetherEventPosition = (hostPosition + guestPosition) * .5f;
            }
            coopPreviewTetherActive = false;
            coopPreviewTetherHeat = 1f;
            coopPreviewTetherOverloadTimer = CoopTetherRules.OverloadDuration;
            coopPreviewTetherEventSequence++;
            coopPreviewTetherReconnectCooldown = CoopTetherRules.ReconnectCooldown;
            coopPreviewTetherDamageTimer = CoopTetherRules.DamageInterval;
            coopPreviewTetherCoreTimer = CoopTetherRules.CoreChargeInterval;
        }

        /// <summary>
        /// Finalises a deterministic shared result on both clients. Firebase uses
        /// the party run id as an idempotency key, so reconnects cannot duplicate
        /// the same completion upload.
        /// </summary>
        private void CompleteCoopRun(SectorLayout layoutForCompletion)
        {
            RecordCoopOutcome(layoutForCompletion, true);
        }

        private void FailCoopRun(SectorLayout layoutForFailure)
        {
            RecordCoopOutcome(layoutForFailure, false);
        }

        private void RecordCoopOutcome(SectorLayout layout, bool success)
        {
            coopResultScore = success ? ComputeCoopScore(layout) : 0;
            coopResultFingerprint = ComputeCoopResultFingerprint(layout, coopResultRunId, coopResultScore);
            coopResultMmrDelta = MmrSettings.CalculateChange(coopResultScore, mmr);
            lastMmrDelta = coopResultMmrDelta;
            mmrResultTimer = 2.25f;

            // The deterministic two-pilot preview is QA-only. Solo Expedition is
            // an actual ranked mode and must persist its result like network co-op.
            if ((coopLocalPreview && !soloExpeditionPlaying) || coopResultSubmitted) return;
            coopResultSubmitted = true;
            bestScore = Mathf.Max(bestScore, coopResultScore);
            mmr = Mathf.Max(MmrSettings.MinimumMmr, mmr + coopResultMmrDelta);
            PlayerPrefs.SetInt("orbital_rift_best", bestScore);
            PlayerPrefs.SetInt("orbital_rift_mmr", mmr);
            PlayerPrefs.Save();

            var nickname = string.IsNullOrWhiteSpace(playerNickname) ? "PILOT" : playerNickname;
            var runId = string.IsNullOrWhiteSpace(coopResultRunId)
                ? (soloExpeditionPlaying ? "solo-expedition-" : "coop-") +
                  (multiplayerSessions == null ? coopPreviewRunSeed : multiplayerSessions.RunSeed)
                : coopResultRunId;
            currentRunId = runId;
            if (firebaseScores != null)
                firebaseScores.SubmitProgress(bestScore, mmr, nickname, runId, coopResultFingerprint);
        }

        private static int ComputeCoopScore(SectorLayout layout)
        {
            if (layout == null || layout.Rooms == null || layout.Rooms.Count == 0) return 0;
            var total = 0;
            for (var i = 0; i < layout.Rooms.Count; i++)
            {
                var room = layout.Rooms[i];
                var roomValue = 180 + Mathf.Clamp(room.Threat, 1, 20) * 35 + CoopRoomRules.RewardAmount(room.Type);
                total += roomValue;
            }
            return Mathf.Clamp(total, 0, 100000000);
        }

        private static string ComputeCoopResultFingerprint(SectorLayout layout, string runId, int resultScore)
        {
            var payload = (runId ?? string.Empty) + "|" + resultScore + "|" + (layout == null ? string.Empty : layout.Signature());
            unchecked
            {
                uint hash = 2166136261u;
                for (var i = 0; i < payload.Length; i++)
                {
                    hash ^= payload[i];
                    hash *= 16777619u;
                }
                return hash.ToString("X8");
            }
        }

        private void ResetCoopPreviewEnemy()
        {
            if (coopPreviewSector == null || coopPreviewSector.Rooms.Count == 0) return;
            var room = coopPreviewSector.Rooms[Mathf.Clamp(coopPreviewRoomIndex, 0, coopPreviewSector.Rooms.Count - 1)];
            coopPreviewEnemyKind = (byte)room.Type;
            coopPreviewEnemyMaxHealth = CoopRoomRules.EnemyHealth(room);
            coopPreviewEnemyHealth = coopPreviewEnemyMaxHealth;
            coopPreviewEnemyAngle = Mathf.Repeat(91f + coopPreviewRoomIndex * 47f, 360f);
            coopPreviewEnemyRadius = CoopTrajectorySettings.ThreatSpawnRadius;
            coopPreviewRoomTimer = 0f;
            coopPreviewResonance = ElementalReaction.None;
            coopPreviewResonanceTimer = 0f;
            coopPreviewHasLastElement = false;
            coopPreviewLastElementAge = 0f;
            coopPreviewThreatAttackTimer = 0f;
            coopPreviewThreatWindupTimer = 0f;
            coopPreviewThreatTargetHostAngle = coopPreviewHostAngle;
            coopPreviewThreatTargetGuestAngle = coopPreviewGuestAngle;
            coopPreviewThreatPattern = CoopThreatPattern.Bolt;
            coopPreviewThreatTargetsHost = true;
            coopPreviewThreatPatternAngle = 0f;
            coopPreviewPlayerShots.Clear();
            coopPreviewRoomEntryGraceTimer = CoopRoomRules.RoomEntryGraceDuration;
            coopPreviewTeamDamageCooldown = 0f;
            coopPreviewThreatPulseTimer = 0f;
            coopPreviewThreatPulseElement = DamageElement.Kinetic;
            coopPreviewRelayCoreActive = CoopRelayCoreRules.ShouldSpawn(
                coopPreviewRunSeed, coopPreviewRoomIndex, room.Type);
            coopPreviewRelayCorePosition = CoopRelayCoreRules.SpawnPosition(
                coopPreviewRunSeed, coopPreviewRoomIndex);
            coopPreviewRelayCoreVelocity = Vector2.zero;
            coopPreviewRelayCoreCharge = 0;
            coopPreviewRelayCoreElement = DamageElement.Kinetic;
            coopPreviewRelayCoreDangerous = false;
            coopPreviewRelayCoreContactCooldown = .35f;
            coopPreviewRelayCoreEventKind = 0;
            coopPreviewRelayCoreEventPosition = coopPreviewRelayCorePosition;
        }

        private void UpdateCoopPreviewRelayCore(float dt)
        {
            if (!coopPreviewRelayCoreActive || coopPreviewCompleted || coopPreviewFailed) return;
            coopPreviewRelayCoreContactCooldown = Mathf.Max(0f,
                coopPreviewRelayCoreContactCooldown - Mathf.Max(0f, dt));
            CoopRelayCoreRules.Step(ref coopPreviewRelayCorePosition, ref coopPreviewRelayCoreVelocity, dt);

            var hostPosition = CoopTrajectorySettings.Position(coopPreviewHostAngle, coopPreviewTrajectoryTime);
            var guestPosition = CoopTrajectorySettings.Position(coopPreviewGuestAngle, coopPreviewTrajectoryTime);
            if (coopPreviewRelayCoreContactCooldown <= 0f &&
                (TryHandlePreviewRelayCoreShipContact(hostPosition) ||
                 (!soloExpeditionPlaying && TryHandlePreviewRelayCoreShipContact(guestPosition))))
                coopPreviewRelayCoreContactCooldown = .28f;

            if (coopPreviewRelayCoreContactCooldown > 0f || coopPreviewEnemyHealth <= 0) return;
            var enemyRadians = coopPreviewEnemyAngle * Mathf.Deg2Rad;
            var enemyPosition = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) * coopPreviewEnemyRadius;
            if ((coopPreviewRelayCorePosition - enemyPosition).sqrMagnitude >
                CoopRelayCoreRules.EnemyContactRadius * CoopRelayCoreRules.EnemyContactRadius) return;
            var speed = coopPreviewRelayCoreVelocity.magnitude;
            var damage = CoopRelayCoreRules.ImpactDamage(coopPreviewRelayCoreCharge, speed);
            if (damage <= 0) return;
            coopPreviewEnemyHealth = Mathf.Max(0, coopPreviewEnemyHealth - damage);
            coopPreviewRelayCoreEventPosition = coopPreviewRelayCorePosition;
            var roomType = (SectorRoomType)Mathf.Clamp(coopPreviewEnemyKind, 0, (int)SectorRoomType.Boss);
            if (roomType == SectorRoomType.Boss && coopPreviewEnemyHealth > 0)
            {
                var target = soloExpeditionPlaying || Vector2.SqrMagnitude(hostPosition - coopPreviewRelayCorePosition) <=
                             Vector2.SqrMagnitude(guestPosition - coopPreviewRelayCorePosition)
                    ? hostPosition : guestPosition;
                var direction = (target - coopPreviewRelayCorePosition).normalized;
                if (direction.sqrMagnitude < .001f) direction = Vector2.down;
                coopPreviewRelayCoreVelocity = direction * CoopRelayCoreRules.BossReturnSpeed;
                coopPreviewRelayCoreDangerous = true;
                coopPreviewRelayCoreEventKind = 3;
            }
            else
            {
                var direction = (coopPreviewRelayCorePosition - enemyPosition).normalized;
                if (direction.sqrMagnitude < .001f) direction = Vector2.up;
                coopPreviewRelayCoreVelocity = direction * Mathf.Max(3.8f, speed * .82f);
                coopPreviewRelayCoreDangerous = false;
                coopPreviewRelayCoreEventKind = 2;
            }
            coopPreviewRelayCoreCharge = 0;
            coopPreviewRelayCoreEventSequence++;
            coopPreviewRelayCoreContactCooldown = .42f;
        }

        private bool TryHandlePreviewRelayCoreShipContact(Vector2 shipPosition)
        {
            var offset = coopPreviewRelayCorePosition - shipPosition;
            if (offset.sqrMagnitude > CoopRelayCoreRules.ShipContactRadius * CoopRelayCoreRules.ShipContactRadius)
                return false;
            var direction = offset.sqrMagnitude > .001f ? offset.normalized : Vector2.up;
            if (coopPreviewRelayCoreDangerous)
            {
                coopPreviewTeamHealth = Mathf.Max(0, coopPreviewTeamHealth - 1);
                coopPreviewRelayCoreDangerous = false;
                coopPreviewRelayCoreCharge = 0;
                coopPreviewRelayCoreEventKind = 4;
                coopPreviewRelayCoreEventPosition = shipPosition;
                coopPreviewRelayCoreEventSequence++;
                if (coopPreviewTeamHealth == 0)
                {
                    coopPreviewFailed = true;
                    coopPreviewFailureSequence++;
                }
            }
            coopPreviewRelayCoreVelocity = Vector2.ClampMagnitude(
                coopPreviewRelayCoreVelocity * .35f + direction * CoopRelayCoreRules.ShipImpulse,
                CoopRelayCoreRules.MaxSpeed);
            return true;
        }

        private void PushCoopPreviewRelayCoreByShot(ShipArchetype ship, float shipAngle)
        {
            if (!coopPreviewRelayCoreActive || coopPreviewEnemyHealth <= 0) return;
            var origin = CoopTrajectorySettings.Position(shipAngle, coopPreviewTrajectoryTime);
            var enemyRadians = coopPreviewEnemyAngle * Mathf.Deg2Rad;
            var target = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) * coopPreviewEnemyRadius;
            if (!CoopRelayCoreRules.TryGetShotImpulse(origin, target, coopPreviewRelayCorePosition, out var impulse)) return;
            coopPreviewRelayCoreVelocity = Vector2.ClampMagnitude(
                coopPreviewRelayCoreVelocity + impulse, CoopRelayCoreRules.MaxSpeed);
            coopPreviewRelayCoreCharge = (byte)Mathf.Min(CoopRelayCoreRules.MaxCharge,
                coopPreviewRelayCoreCharge + 1);
            coopPreviewRelayCoreElement = ShipLoadoutSettings.Get(ship).Element;
            coopPreviewRelayCoreDangerous = false;
        }

        private void UpdateCoopPreviewThreatPulse(float dt)
        {
            coopPreviewTeamDamageCooldown = Mathf.Max(0f, coopPreviewTeamDamageCooldown - Mathf.Max(0f, dt));
            if (coopPreviewEnemyHealth <= 0)
            {
                coopPreviewThreatWindupTimer = 0f;
                coopPreviewThreatPulseTimer = 0f;
                return;
            }
            if (coopPreviewRoomEntryGraceTimer > 0f) return;
            if (coopPreviewThreatWindupTimer > 0f)
            {
                coopPreviewThreatWindupTimer -= Mathf.Max(0f, dt);
                coopPreviewThreatPulseTimer = Mathf.Max(0f, coopPreviewThreatWindupTimer);
                if (coopPreviewThreatWindupTimer > 0f) return;

                var pendingRoomType = (SectorRoomType)Mathf.Clamp(coopPreviewEnemyKind, 0, (int)SectorRoomType.Boss);
                var lockedTargetAngle = coopPreviewThreatTargetsHost
                    ? coopPreviewThreatTargetHostAngle : coopPreviewThreatTargetGuestAngle;
                var currentTargetAngle = coopPreviewThreatTargetsHost
                    ? coopPreviewHostAngle : coopPreviewGuestAngle;
                var hitTarget = CoopThreatAttackRules.Hits(coopPreviewThreatPattern, lockedTargetAngle,
                    coopPreviewThreatPatternAngle, currentTargetAngle);
                var pendingDamage = CoopRoomRules.ThreatDamage(pendingRoomType);
                if (coopLocalPreview && !soloExpeditionPlaying) pendingDamage = 0;
                if (pendingDamage > 0 && coopPreviewTeamDamageCooldown <= 0f && hitTarget)
                {
                    coopPreviewTeamHealth = Mathf.Max(0, coopPreviewTeamHealth - pendingDamage);
                    coopPreviewTeamDamageCooldown = CoopRoomRules.TeamDamageCooldown(pendingRoomType);
                    if (coopPreviewTeamHealth == 0)
                    {
                        coopPreviewFailed = true;
                        coopPreviewFailureSequence++;
                    }
                }
                return;
            }
            coopPreviewThreatAttackTimer -= Mathf.Max(0f, dt);
            if (coopPreviewThreatAttackTimer > 0f) return;
            var roomType = (SectorRoomType)Mathf.Clamp(coopPreviewEnemyKind, 0, (int)SectorRoomType.Boss);
            coopPreviewThreatAttackTimer = CoopRoomRules.ThreatPulseInterval(roomType);
            var nextSequence = coopPreviewThreatPulseSequence + 1u;
            coopPreviewThreatPattern = CoopThreatAttackRules.PatternFor(roomType, coopPreviewRoomIndex, nextSequence);
            // In solo expedition every telegraph and hit check must target the
            // local ship; the guest angle is not simulated in this mode.
            coopPreviewThreatTargetsHost = soloExpeditionPlaying ||
                (coopPreviewRoomIndex + (int)nextSequence) % 2 == 0;
            coopPreviewThreatTargetHostAngle = coopPreviewHostAngle;
            coopPreviewThreatTargetGuestAngle = coopPreviewGuestAngle;
            var selectedAngle = coopPreviewThreatTargetsHost
                ? coopPreviewThreatTargetHostAngle : coopPreviewThreatTargetGuestAngle;
            coopPreviewThreatPatternAngle = CoopThreatAttackRules.PatternAngle(selectedAngle,
                coopPreviewThreatPattern, nextSequence);
            coopPreviewThreatWindupTimer = CoopThreatAttackRules.Windup(coopPreviewThreatPattern);
            coopPreviewThreatPulseTimer = coopPreviewThreatWindupTimer;
            coopPreviewThreatPulseSequence = nextSequence;
            coopPreviewThreatPulseElement = roomType == SectorRoomType.Boss
                ? (DamageElement)(coopPreviewRoomIndex % 3 + 1)
                : (DamageElement)(coopPreviewRoomIndex % 4);
        }

        private void ApplyCoopPreviewDamage(uint hostShots, uint guestShots)
        {
            ApplyCoopPreviewDamageForShip(CoopHostShip(), coopPreviewHostAngle,
                hostShots > 3u ? 3u : hostShots, true);
            if (!soloExpeditionPlaying)
                ApplyCoopPreviewDamageForShip(CoopGuestShip(), coopPreviewGuestAngle,
                    guestShots > 3u ? 3u : guestShots, false);
        }

        private void ApplyCoopPreviewDamageForShip(ShipArchetype ship, float shipAngle, uint shotCount,
            bool fromHost)
        {
            if (coopPreviewEnemyHealth <= 0 || shotCount == 0 || coopPreviewRoomEntryGraceTimer > 0f) return;
            var loadout = ShipLoadoutSettings.Get(ship);
            for (var i = 0u; i < shotCount && coopPreviewEnemyHealth > 0; i++)
            {
                var origin = CoopTrajectorySettings.Position(shipAngle, coopPreviewTrajectoryTime);
                var allyAngle = fromHost ? coopPreviewGuestAngle : coopPreviewHostAngle;
                var allyPosition = CoopTrajectorySettings.Position(allyAngle, coopPreviewTrajectoryTime);
                var enemyRadians = coopPreviewEnemyAngle * Mathf.Deg2Rad;
                var enemyPosition = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) * coopPreviewEnemyRadius;
                if (!soloExpeditionPlaying && coopPreviewRedirectCooldown <= 0f &&
                    CoopFriendlyRedirectRules.TryIntercept(origin, enemyPosition, allyPosition, out _,
                        coopPreviewTetherActive ? CoopFriendlyRedirectRules.EnergizedCaptureRadius :
                        CoopFriendlyRedirectRules.CaptureRadius))
                {
                    coopPreviewRedirectCooldown = CoopFriendlyRedirectRules.RedirectCooldown;
                    coopPreviewRedirectFromHost = fromHost;
                    coopPreviewRedirectPosition = allyPosition;
                    coopPreviewRedirectSequence++;
                    if (!coopPreviewTetherActive)
                    {
                        coopPreviewRedirectKind = 2;
                        coopPreviewRedirectElement = loadout.Element;
                        if (fromHost)
                            coopPreviewGuestAngle = CoopFriendlyRedirectRules.ApplyComicSpin(coopPreviewGuestAngle, true);
                        else
                            coopPreviewHostAngle = CoopFriendlyRedirectRules.ApplyComicSpin(coopPreviewHostAngle, false);
                        continue;
                    }

                    var allyShip = fromHost ? CoopGuestShip() : CoopHostShip();
                    var allyLoadout = ShipLoadoutSettings.Get(allyShip);
                    coopPreviewRedirectKind = 1;
                    coopPreviewRedirectElement = allyLoadout.Element;
                    var redirectResistance = coopPreviewEnemyKind == (byte)SectorRoomType.Boss
                        ? BossSettings.Resistance(allyLoadout.Element) : 1f;
                    ApplyCoopPreviewShotDamage(allyLoadout.Element,
                        CoopFriendlyRedirectRules.RedirectDamage(loadout.DamageMultiplier, redirectResistance));
                    continue;
                }

                PushCoopPreviewRelayCoreByShot(ship, shipAngle);
                var resistance = coopPreviewEnemyKind == (byte)SectorRoomType.Boss ? BossSettings.Resistance(loadout.Element) : 1f;
                var damage = Mathf.Max(1, Mathf.RoundToInt(ElementalCombat.ApplyResistance(loadout.DamageMultiplier, resistance)));
                var speed = BalanceSettings.PlayerProjectileSpeed(1) * loadout.ProjectileSpeedMultiplier;
                coopPreviewPlayerShots.Add(CoopPlayerShotRules.Create(origin, enemyPosition, speed,
                    loadout.Element, damage));
            }
        }

        private void UpdateCoopPreviewPlayerShots(float deltaTime)
        {
            if (coopPreviewEnemyHealth <= 0)
            {
                coopPreviewPlayerShots.Clear();
                return;
            }
            var enemyRadians = coopPreviewEnemyAngle * Mathf.Deg2Rad;
            var enemyPosition = new Vector2(Mathf.Cos(enemyRadians), Mathf.Sin(enemyRadians)) * coopPreviewEnemyRadius;
            var roomType = (SectorRoomType)Mathf.Clamp(coopPreviewEnemyKind, 0, (int)SectorRoomType.Boss);
            for (var i = coopPreviewPlayerShots.Count - 1; i >= 0; i--)
            {
                var shot = coopPreviewPlayerShots[i];
                var hit = CoopPlayerShotRules.Step(ref shot, deltaTime, enemyPosition, roomType);
                if (hit)
                {
                    coopPreviewPlayerShots.RemoveAt(i);
                    if (coopPreviewEnemyHealth > 0) ApplyCoopPreviewShotDamage(shot.Element, shot.Damage);
                }
                else if (shot.Life <= 0f)
                    coopPreviewPlayerShots.RemoveAt(i);
                else
                    coopPreviewPlayerShots[i] = shot;
            }
        }

        private void ApplyCoopPreviewShotDamage(DamageElement element, int damage)
        {
                if (!soloExpeditionPlaying && coopPreviewHasLastElement && coopPreviewLastElementAge <= 1.2f)
                {
                    var reaction = ElementalCombat.ResolveReaction(coopPreviewLastElement, element);
                    var bonus = ElementalCombat.ReactionBonus(reaction);
                    if (bonus > 0)
                    {
                        coopPreviewResonance = reaction;
                        coopPreviewResonanceTimer = 1.35f;
                        coopPreviewResonanceSequence++;
                        damage += Mathf.RoundToInt(bonus * CoopRoomRules.ReactionBonusMultiplier(
                            (SectorRoomType)Mathf.Clamp(coopPreviewEnemyKind, 0, (int)SectorRoomType.Boss)));
                    }
                }
                coopPreviewEnemyHealth = Mathf.Max(0, coopPreviewEnemyHealth - damage);
                coopPreviewLastElement = element;
                coopPreviewLastElementAge = 0f;
                coopPreviewHasLastElement = true;
        }

        private void ConfigureCoopEnemyVisual(byte kind)
        {
            if (coopEnemy == null) return;
            var renderer = coopEnemy.GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            var roomType = (SectorRoomType)Mathf.Clamp(kind, 0, (int)SectorRoomType.Boss);
            renderer.sprite = roomType == SectorRoomType.Boss && bossSprite != null ? bossSprite :
                roomType == SectorRoomType.Elite && orangeEnemySprite != null ? orangeEnemySprite :
                pinkCanEnemySprite != null ? pinkCanEnemySprite : whiteSprite;
            var color = SectorRoomColor(roomType);
            color.a = .95f;
            renderer.color = color;
            SetSpriteWorldSize(renderer, roomType == SectorRoomType.Boss ? .92f :
                roomType == SectorRoomType.Elite ? .72f : .62f);
        }

        private void PositionCoopEnemy(float angleDegrees, float radius, bool active)
        {
            if (coopEnemy == null) return;
            if (coopEnemy.gameObject.activeSelf != active) coopEnemy.gameObject.SetActive(active);
            if (!active) return;
            var radians = angleDegrees * Mathf.Deg2Rad;
            coopEnemy.position = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * Mathf.Max(.3f, radius);
            coopEnemy.rotation = Quaternion.identity;
            var renderer = coopEnemy.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b,
                Mathf.Lerp(.82f, 1f, Mathf.InverseLerp(CoopTrajectorySettings.ThreatSpawnRadius,
                    CoopTrajectorySettings.ThreatOrbitRadius, radius)));
        }

        private void PositionCoopRelayCore(bool active, Vector2 position, byte charge,
            DamageElement element, bool dangerous)
        {
            if (coopRelayCore == null) return;
            if (coopRelayCore.gameObject.activeSelf != active)
            {
                coopRelayCore.gameObject.SetActive(active);
                if (coopRelayCoreTrail != null) coopRelayCoreTrail.Clear();
            }
            if (!active) return;
            coopRelayCore.position = position;
            var color = dangerous ? new Color(1f, .20f, .62f) :
                charge > 0 ? CoopElementColor(element) : new Color(.38f, .90f, 1f);
            var renderer = coopRelayCore.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = color;
                SetSpriteWorldSize(renderer, .48f + charge * .055f);
            }
            if (coopRelayCoreGlow != null)
            {
                var glow = coopRelayCoreGlow.GetComponent<SpriteRenderer>();
                var pulse = .76f + Mathf.Sin(Time.unscaledTime * (dangerous ? 14f : 7f)) * .08f + charge * .08f;
                if (glow != null)
                {
                    var glowColor = color;
                    glowColor.a = dangerous ? .42f : .20f + charge * .07f;
                    glow.color = glowColor;
                    SetSpriteWorldSize(glow, pulse);
                }
            }
            if (coopRelayCoreTrail != null)
            {
                var start = color;
                start.a = dangerous ? .92f : .72f;
                var end = color;
                end.a = 0f;
                coopRelayCoreTrail.startColor = start;
                coopRelayCoreTrail.endColor = end;
                coopRelayCoreTrail.time = dangerous ? .72f : .48f + charge * .08f;
            }
        }

        private void UpdateCoopTetherVisual(bool active, float heat, float overloadTimer,
            float hostAngle, float guestAngle, float trajectoryTime)
        {
            if (coopTetherRenderer == null) return;
            var visible = active || overloadTimer > 0f;
            if (coopTetherRenderer.gameObject.activeSelf != visible)
                coopTetherRenderer.gameObject.SetActive(visible);
            if (!visible) return;

            var hostPosition = CoopTrajectorySettings.Position(hostAngle, trajectoryTime);
            var guestPosition = CoopTrajectorySettings.Position(guestAngle, trajectoryTime);
            coopTetherRenderer.SetPosition(0, hostPosition);
            coopTetherRenderer.SetPosition(1, guestPosition);
            heat = Mathf.Clamp01(heat);
            var overload = overloadTimer > 0f;
            var pulse = overload ? .65f + Mathf.Sin(Time.unscaledTime * 28f) * .35f :
                .72f + Mathf.Sin(Time.unscaledTime * (8f + heat * 10f)) * (.08f + heat * .12f);
            var start = Color.Lerp(new Color(.18f, .92f, 1f), new Color(1f, .24f, .72f), heat);
            var end = Color.Lerp(new Color(.76f, .38f, 1f), new Color(1f, .78f, .22f), heat);
            if (overload)
            {
                start = Color.Lerp(Color.white, new Color(1f, .18f, .58f), pulse);
                end = Color.Lerp(new Color(1f, .92f, .32f), Color.white, pulse);
            }
            start.a = end.a = Mathf.Clamp01(pulse);
            coopTetherRenderer.startColor = start;
            coopTetherRenderer.endColor = end;
            var width = .04f + heat * .035f + (overload ? .025f : 0f);
            coopTetherRenderer.startWidth = coopTetherRenderer.endWidth = width;
        }

        private void UpdateCoopPreviewFire(float dt)
        {
            coopPreviewRedirectCooldown = Mathf.Max(0f, coopPreviewRedirectCooldown - Mathf.Max(0f, dt));
            coopPreviewHostFireTimer -= dt;
            coopPreviewGuestFireTimer -= dt;
            if (coopPreviewHostFireTimer <= 0f)
            {
                coopPreviewHostShots++;
                coopPreviewHostFireTimer += BalanceSettings.PlayerFireInterval(1, false) * ShipLoadoutSettings.Get(CoopHostShip()).FireIntervalMultiplier;
            }
            if (!soloExpeditionPlaying && coopPreviewGuestFireTimer <= 0f)
            {
                coopPreviewGuestShots++;
                coopPreviewGuestFireTimer += BalanceSettings.PlayerFireInterval(1, false) * ShipLoadoutSettings.Get(CoopGuestShip()).FireIntervalMultiplier;
            }
        }

        private void EmitCoopShots(Transform ship, ShipArchetype archetype, ref uint previous, uint current)
        {
            if (ship == null || current <= previous) return;
            var count = current - previous;
            if (count > 3) count = 3;
            previous = current;
            var loadout = ShipLoadoutSettings.Get(archetype);
            var speed = BalanceSettings.PlayerProjectileSpeed(1) * loadout.ProjectileSpeedMultiplier;
            for (var i = 0u; i < count; i++)
            {
                var aim = coopEnemy == null ? -(Vector2)ship.position : (Vector2)(coopEnemy.position - ship.position);
                if (aim.sqrMagnitude < .001f) aim = Vector2.up;
                Shoot(ship.position, aim.normalized * speed, true,
                    loadout.ProjectileColor, loadout.Element, loadout.DamageMultiplier);
            }
        }

        private void EmitCoopThreatBolts(Vector2 origin, Vector2 target, SectorRoomType roomType,
            Color color, DamageElement element)
        {
            var direction = target - origin;
            if (direction.sqrMagnitude < .001f) direction = Vector2.down;
            direction.Normalize();
            var count = roomType == SectorRoomType.Boss ? 3 : roomType == SectorRoomType.Elite ? 2 : 1;
            var center = (count - 1) * .5f;
            for (var i = 0; i < count; i++)
            {
                var p = projectilePool.Get();
                p.SetVisual(whiteSprite, false, true);
                var boltDirection = Rotate(direction, (i - center) * 8f);
                var speed = Mathf.Max(8f, Vector2.Distance(origin, target) /
                    CoopThreatAttackRules.Windup(CoopThreatPattern.Bolt));
                p.ResetProjectile(origin, boltDirection * speed, false, color, element, 0f);
                p.VisualOnly = true;
                p.Life = CoopThreatAttackRules.Windup(CoopThreatPattern.Bolt) + .12f;
                projectiles.Add(p);
            }
        }

        private void UpdateCoopThreatPatternVisual(float deltaTime, float trajectoryTime)
        {
            if (coopThreatPatternVisualTimer <= 0f)
            {
                if (coopThreatCleaveRenderer != null) coopThreatCleaveRenderer.gameObject.SetActive(false);
                if (coopThreatRingLeftRenderer != null) coopThreatRingLeftRenderer.gameObject.SetActive(false);
                if (coopThreatRingRightRenderer != null) coopThreatRingRightRenderer.gameObject.SetActive(false);
                for (var i = 0; i < coopThreatMineMarkers.Count; i++)
                    if (coopThreatMineMarkers[i] != null) coopThreatMineMarkers[i].gameObject.SetActive(false);
                return;
            }
            coopThreatPatternVisualTimer = Mathf.Max(0f, coopThreatPatternVisualTimer - Mathf.Max(0f, deltaTime));
            var progress = 1f - coopThreatPatternVisualTimer / Mathf.Max(.01f, coopThreatPatternVisualDuration);
            var alpha = Mathf.Clamp01(Mathf.Min(1f, progress * 5f) * Mathf.Min(1f, (1f - progress) * 5f));
            var color = new Color(coopThreatVisualColor.r, coopThreatVisualColor.g, coopThreatVisualColor.b, alpha);
            var direction = coopThreatVisualTarget - coopThreatVisualOrigin;
            if (direction.sqrMagnitude < .001f) direction = Vector2.down;
            direction.Normalize();
            var perpendicular = new Vector2(-direction.y, direction.x);

            if (coopThreatVisualPattern == CoopThreatPattern.Cleave)
            {
                coopThreatCleaveRenderer.gameObject.SetActive(true);
                coopThreatCleaveRenderer.startColor = coopThreatCleaveRenderer.endColor = color;
                var center = Vector2.Lerp(coopThreatVisualOrigin, coopThreatVisualTarget, progress);
                for (var i = 0; i < coopThreatCleaveRenderer.positionCount; i++)
                {
                    var side = i / (float)(coopThreatCleaveRenderer.positionCount - 1) * 2f - 1f;
                    var curve = (1f - side * side) * .26f;
                    coopThreatCleaveRenderer.SetPosition(i, center + perpendicular * (side * .82f) + direction * curve);
                }
            }
            else if (coopThreatVisualPattern == CoopThreatPattern.RingGate)
            {
                coopThreatRingLeftRenderer.gameObject.SetActive(true);
                coopThreatRingRightRenderer.gameObject.SetActive(true);
                coopThreatRingLeftRenderer.startColor = coopThreatRingLeftRenderer.endColor = color;
                coopThreatRingRightRenderer.startColor = coopThreatRingRightRenderer.endColor = color;
                var radius = Mathf.Lerp(.2f, 5.9f, progress);
                DrawThreatArc(coopThreatRingLeftRenderer, coopThreatVisualOrigin, radius,
                    coopThreatVisualPatternAngle + 24f, coopThreatVisualPatternAngle + 180f);
                DrawThreatArc(coopThreatRingRightRenderer, coopThreatVisualOrigin, radius,
                    coopThreatVisualPatternAngle + 180f, coopThreatVisualPatternAngle + 336f);
            }
            else if (coopThreatVisualPattern == CoopThreatPattern.Mines)
            {
                var pulse = .82f + Mathf.Sin(Time.unscaledTime * 18f) * .18f;
                for (var i = 0; i < coopThreatMineMarkers.Count; i++)
                {
                    var marker = coopThreatMineMarkers[i];
                    marker.gameObject.SetActive(true);
                    var offset = (i - 1) * 8f;
                    marker.transform.position = CoopTrajectorySettings.Position(
                        coopThreatVisualTargetAngle + offset, trajectoryTime);
                    marker.color = new Color(color.r, color.g, color.b, color.a * pulse);
                    marker.transform.localScale = Vector3.one * (.82f + pulse * .20f);
                }
            }
        }

        private static void DrawThreatArc(LineRenderer line, Vector2 center, float radius,
            float startDegrees, float endDegrees)
        {
            for (var i = 0; i < line.positionCount; i++)
            {
                var t = i / (float)(line.positionCount - 1);
                var angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private void UpdateCoopTrajectory(float trajectoryTime)
        {
            if (coopTrajectoryRenderer == null) return;
            for (var i = 0; i < CoopTrajectorySettings.LineSegments; i++)
            {
                var angle = i * 360f / CoopTrajectorySettings.LineSegments;
                coopTrajectoryRenderer.SetPosition(i, CoopTrajectorySettings.Position(angle, trajectoryTime));
            }
            var state = CoopTrajectorySettings.Evaluate(trajectoryTime);
            var pulse = state.IsTransitioning ? 1f + Mathf.Sin(Time.unscaledTime * 8f) * .18f : 1f;
            coopTrajectoryRenderer.startWidth = coopTrajectoryRenderer.endWidth = CoopTrajectorySettings.LineWidth * pulse;
        }

        private void UpdateCoopRoomEnvironment(int runSeed, int roomIndex, SectorRoomType roomType, float deltaTime)
        {
            if (coopRoomEnvironment == null || coopRoomWash == null || coopRoomMotifs.Count == 0) return;
            var signature = unchecked(runSeed * 486187739 + roomIndex * 16777619 + (int)roomType * 7919);
            if (signature != coopRoomEnvironmentSignature)
            {
                coopRoomEnvironmentSignature = signature;
                ConfigureCoopRoomEnvironment(signature, roomType);
            }
            coopRoomEnvironment.Rotate(0f, 0f, coopRoomEnvironmentRotationSpeed * Mathf.Max(0f, deltaTime));
        }

        private void ConfigureCoopRoomEnvironment(int signature, SectorRoomType roomType)
        {
            var random = new System.Random(signature);
            var primary = SectorRoomColor(roomType);
            var secondary = Color.Lerp(primary,
                roomType == SectorRoomType.Boss ? new Color(.55f, .04f, .12f) : new Color(.12f, .72f, 1f),
                .42f + (float)random.NextDouble() * .22f);
            var washAlpha = roomType == SectorRoomType.Boss ? .105f : roomType == SectorRoomType.Elite ? .082f : .052f;
            coopRoomWash.color = new Color(primary.r, primary.g, primary.b, washAlpha);
            coopRoomEnvironment.localRotation = Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 360f);
            coopRoomEnvironmentRotationSpeed = roomType == SectorRoomType.Boss ? 7.5f : roomType == SectorRoomType.Event ? -2.6f : 1.1f;

            for (var i = 0; i < coopRoomMotifs.Count; i++)
            {
                var motif = coopRoomMotifs[i];
                var normalized = i / (float)Mathf.Max(1, coopRoomMotifs.Count - 1);
                var jitter = (float)random.NextDouble();
                var color = i % 2 == 0 ? primary : secondary;
                color.a = .09f + (float)random.NextDouble() * .18f;
                motif.color = color;
                motif.transform.localRotation = Quaternion.identity;

                switch (roomType)
                {
                    case SectorRoomType.Start:
                    {
                        var angle = normalized * Mathf.PI * 2f;
                        var radius = 4.25f + (jitter - .5f) * .5f;
                        motif.sprite = circleSprite;
                        motif.transform.localPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        motif.transform.localScale = Vector3.one * (.045f + (float)random.NextDouble() * .07f);
                        break;
                    }
                    case SectorRoomType.Event:
                    {
                        var x = Mathf.Lerp(-5.2f, 5.2f, normalized);
                        var y = Mathf.Sin(normalized * Mathf.PI * 4f + jitter) * 3.15f;
                        motif.sprite = circleSprite;
                        motif.transform.localPosition = new Vector2(x, y);
                        motif.transform.localScale = Vector3.one * (.07f + (float)random.NextDouble() * .16f);
                        break;
                    }
                    case SectorRoomType.Shop:
                    {
                        var column = i % 6;
                        var row = i / 6;
                        motif.sprite = whiteSprite;
                        motif.transform.localPosition = new Vector2(-4.5f + column * 1.8f, -3.3f + row * 3.3f);
                        motif.transform.localScale = new Vector3(.12f + jitter * .16f, .12f + jitter * .16f, 1f);
                        motif.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                        break;
                    }
                    case SectorRoomType.Elite:
                    {
                        var angle = normalized * Mathf.PI * 2f + jitter * .18f;
                        var radius = 2.2f + (i % 3) * 1.15f;
                        motif.sprite = whiteSprite;
                        motif.transform.localPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        motif.transform.localScale = new Vector3(.12f, .36f + jitter * .44f, 1f);
                        motif.transform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
                        break;
                    }
                    case SectorRoomType.Boss:
                    {
                        var angle = normalized * Mathf.PI * 2f + jitter * .12f;
                        var radius = 1.55f + normalized * 4.1f;
                        motif.sprite = whiteSprite;
                        motif.transform.localPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        motif.transform.localScale = new Vector3(.055f, .62f + jitter * .85f, 1f);
                        motif.transform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
                        break;
                    }
                    default:
                    {
                        var x = -5.2f + normalized * 10.4f;
                        var y = -4.2f + (float)random.NextDouble() * 8.4f;
                        motif.sprite = i % 4 == 0 ? circleSprite : whiteSprite;
                        motif.transform.localPosition = new Vector2(x, y);
                        motif.transform.localScale = new Vector3(.04f + jitter * .07f, .35f + jitter * .75f, 1f);
                        motif.transform.localRotation = Quaternion.Euler(0f, 0f, 24f + jitter * 28f);
                        break;
                    }
                }
            }
        }

        private static void PositionCoopShip(Transform ship, Transform marker, float angleDegrees, float trajectoryTime)
        {
            if (ship == null) return;
            var position = CoopTrajectorySettings.Position(angleDegrees, trajectoryTime);
            ship.position = position;
            if (position.sqrMagnitude > .025f)
            {
                ship.up = -position.normalized;
            }
            else
            {
                var tangent = CoopTrajectorySettings.Position(angleDegrees + .8f, trajectoryTime) - position;
                if (tangent.sqrMagnitude > .001f) ship.up = new Vector2(-tangent.y, tangent.x).normalized;
            }
            if (marker != null)
            {
                marker.position = position;
                marker.rotation = Quaternion.identity;
            }
        }

        private ShipArchetype CoopHostShip()
        {
            return coopLocalPreview || multiplayerSessions == null ? selectedShip : multiplayerSessions.HostShip;
        }

        private ShipArchetype CoopGuestShip()
        {
            return coopLocalPreview || multiplayerSessions == null ? ShipArchetype.Pyre : multiplayerSessions.GuestShip;
        }

        private void BeginLocalCoopPreview()
        {
            BeginCoopRun(true);
        }

        private void BeginSoloExpedition()
        {
            playerNickname = SanitizeNickname(playerNickname);
            if (string.IsNullOrEmpty(playerNickname))
            {
                nicknameError = "ВВЕДИ ПОЗЫВНОЙ ДЛЯ ЭКСПЕДИЦИИ";
                showSettings = true;
                BeginUiFade();
                return;
            }

            nicknameError = string.Empty;
            PlayerPrefs.SetString("orbital_rift_nickname", playerNickname);
            PlayerPrefs.Save();
            BeginCoopRun(true, true);
        }

        private async void ExitCoopRun()
        {
            var wasPreview = coopLocalPreview;
            FinishCoopRunToMenu();
            if (!wasPreview && multiplayerSessions != null && multiplayerSessions.CurrentSession != null)
                await multiplayerSessions.LeavePartyAsync();
        }

        private void FinishCoopRunToMenu()
        {
            coopPlaying = false;
            coopLocalPreview = false;
            soloExpeditionPlaying = false;
            UpdateCameraFraming(true);
            coopSimulation?.ResetLocalRunState();
            Cleanup();
            SetCoopVisualsActive(false);
            if (player != null) player.gameObject.SetActive(true);
            showMenu = true;
            showCoop = false;
            showSettings = false;
            showResults = false;
            activeControlDirection = 0;
            StopGameplayMusic();
            BeginUiFade();
        }

        private static void SetSpriteWorldSize(SpriteRenderer renderer, float targetSize)
        {
            if (renderer == null || renderer.sprite == null) return;
            var bounds = renderer.sprite.bounds.size;
            var maxSize = Mathf.Max(bounds.x, bounds.y);
            renderer.transform.localScale = maxSize > .0001f ? Vector3.one * (targetSize / maxSize) : Vector3.one;
        }

        private void StartGame()
        {
            playerNickname = SanitizeNickname(playerNickname);
            if (string.IsNullOrEmpty(playerNickname))
            {
                nicknameError = "ВВЕДИ ПОЗЫВНОЙ ДЛЯ ОБЩЕГО РЕЙТИНГА";
                showSettings = true;
                BeginUiFade();
                return;
            }

            nicknameError = string.Empty;
            showSettings = false;
            BeginUiFade();
            PlayerPrefs.SetString("orbital_rift_nickname", playerNickname);
            PlayerPrefs.Save();
            currentRunId = Guid.NewGuid().ToString("N");
            Cleanup(); score = 0; shields = 3; starShields = 0; tripleShotTimer = 0f; phase = 1; cores = 0; playing = true; showMenu = false; showResults = false; paused = false; coreActive = false; bossSpawnPending = false;
            playerAngle = -Mathf.PI * .5f;
            targetAngle = playerAngle;
            playerCommandSource?.Reset();
            touchHintTimer = 5f;
            phaseUpgradeBannerTimer = 2.5f;
            phaseUpgradeLabel = "СИСТЕМА В СЕТИ\nПЕРВАЯ ФАЗА";
            screenShakeTimer = 0f;
            mmrResultTimer = 0f;
            if (GameAudioSettings.MusicEnabled && musicSource != null && musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
            StartWave(); SpawnWarpBurst(36, 1.2f);
        }

        private void UpdatePresentation()
        {
            if (menuEmblem != null)
            {
                // Логотип меню теперь рисуется pixel-интерфейсом: не перекрываем поле позывного.
                menuEmblem.gameObject.SetActive(false);
            }
            if (warpBadge != null)
            {
                warpBadge.gameObject.SetActive(warpTimer > 0f);
                if (warpTimer > 0f) warpBadge.Rotate(0f, 0f, 140f * Time.deltaTime);
            }
        }

        private void Cleanup()
        {
            for (var i=enemies.Count-1;i>=0;i--) enemyPool.Release(enemies[i]); enemies.Clear();
            for (var i=projectiles.Count-1;i>=0;i--) projectilePool.Release(projectiles[i]); projectiles.Clear();
            for (var i=stars.Count-1;i>=0;i--) starPool.Release(stars[i]); stars.Clear();
            starShields = 0;
            for (var i=damageShards.Count-1;i>=0;i--) damageShardPool.Release(damageShards[i]); damageShards.Clear();
            splitPickup.gameObject.SetActive(false);
        }

        private void UpdateInput(float dt, int direction)
        {
            if (direction != 0)
            {
                activeControlDirection = direction;
                targetAngle += direction * dt * TouchOrbitSpeed;
                touchHintTimer = Mathf.Max(0f, touchHintTimer - dt);
            }
            else
            {
                activeControlDirection = 0;
                // После отпускания палец больше не оставляет кораблю «запас» угла.
                targetAngle = playerAngle;
                touchHintTimer = Mathf.Max(0f, touchHintTimer - dt);
            }
        }

        private void UpdatePlayer(float dt)
        {
            playerAngle = Mathf.LerpAngle(playerAngle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg, dt * 8f) * Mathf.Deg2Rad;
            PositionOnOrbit();
            if (invincible > 0) { invincible -= dt; player.gameObject.SetActive(Mathf.Sin(Time.time*28) > -.2f); } else player.gameObject.SetActive(true);
            fireTimer -= dt;
            if (autoFire && fireTimer <= 0)
            {
                var loadout = ShipLoadoutSettings.Get(selectedShip);
                fireTimer = BalanceSettings.PlayerFireInterval(phase, splitShot) * loadout.FireIntervalMultiplier;
                FirePlayer(loadout);
            }
            if (splitShot) { splitShotTimer -= dt; if (splitShotTimer <= 0) splitShot=false; }
            if (tripleShotTimer > 0f) tripleShotTimer = Mathf.Max(0f, tripleShotTimer - dt);
            UpdateSplitPickup(dt);
        }

        private void PositionOnOrbit()
        {
            var pos = new Vector2(Mathf.Cos(playerAngle), Mathf.Sin(playerAngle)) * OrbitSettings.Radius;
            player.position = pos;
            player.up = -pos.normalized;
        }

        private void FirePlayer(ShipLoadout loadout)
        {
            var projectileSpeed = BalanceSettings.PlayerProjectileSpeed(phase) * loadout.ProjectileSpeedMultiplier;
            Shoot((Vector2)player.position, -((Vector2)player.position).normalized * projectileSpeed, true, loadout.ProjectileColor, loadout.Element, loadout.DamageMultiplier);
            if (!splitShot && tripleShotTimer <= 0f) return;
            var inward = -((Vector2)player.position).normalized;
            Shoot(player.position, Rotate(inward, 12f)*projectileSpeed, true, loadout.ProjectileColor, loadout.Element, loadout.DamageMultiplier);
            Shoot(player.position, Rotate(inward,-12f)*projectileSpeed, true, loadout.ProjectileColor, loadout.Element, loadout.DamageMultiplier);
        }

        private void Shoot(Vector2 position, Vector2 velocity, bool friendly, Color color, DamageElement element = DamageElement.Kinetic, float damage = 1f)
        {
            if (!friendly && projectiles.Count >= 80) return;
            var p = projectilePool.Get();
            // Вражеские выстрелы — простые тёмно-зелёные квадраты; PNG игрока сохраняется без тонировки.
            p.SetVisual(friendly && projectileSprite != null ? projectileSprite : whiteSprite, friendly && projectileSprite != null, !friendly);
            p.ResetProjectile(position, velocity, friendly, friendly ? color : new Color(.05f, .30f, .13f), element, damage);
            projectiles.Add(p);
        }

        private void UpdateSpawning(float dt)
        {
            if (coreActive || warpTimer > 0) return;
            if (bossSpawnPending)
            {
                bossSpawnTimer -= dt;
                if (bossSpawnTimer <= 0f)
                {
                    bossSpawnPending = false;
                    SpawnBoss();
                }
                return;
            }
            if (spawnsLeft <= 0) return;
            spawnTimer -= dt;
            if (spawnTimer > 0) return;
            var cap = Mathf.Min(4 + phase * 2, 18);
            if (enemies.Count >= cap) { spawnTimer = .35f; return; }
            var kind = ChooseEnemy(); var e = enemyPool.Get(); e.ResetEnemy(kind, Random.Range(0f, Mathf.PI*2), phase, EnemySpriteFor(kind)); enemies.Add(e);
            spawnsLeft--;
            spawnTimer = BalanceSettings.SpawnInterval(phase);
        }

        private EnemyKind ChooseEnemy()
        {
            var roll = Random.value;
            if (phase >= 5 && roll > .86f) return EnemyKind.Turret;
            if (phase >= 3 && roll > .63f) return EnemyKind.Diver;
            return roll > .55f ? EnemyKind.Spiral : EnemyKind.Scout;
        }

        private Sprite EnemySpriteFor(EnemyKind kind)
        {
            if (kind == EnemyKind.Scout) return orangeEnemySprite;
            if (kind == EnemyKind.Spiral) return pinkCanEnemySprite;
            if (kind == EnemyKind.Boss) return bossSprite;
            return null;
        }

        private void SpawnBoss()
        {
            var boss = enemyPool.Get();
            boss.ResetEnemy(EnemyKind.Boss, Random.Range(0f, Mathf.PI * 2f), phase, EnemySpriteFor(EnemyKind.Boss));
            boss.Radius = BossSettings.OrbitRadius;
            enemies.Add(boss);
            phaseUpgradeBannerTimer = 1.65f;
            phaseUpgradeLabel = "СИГНАЛ БОССА\nСТРАЖ УРАНА";
            SpawnWarpBurst(46, 1.65f);
            AddScreenShake(.16f, .09f);
        }

        private void UpdateEnemies(float dt)
        {
            for (var i=enemies.Count-1;i>=0;i--)
            {
                var e = enemies[i]; e.Life -= dt; e.FireTimer -= dt;
                if (e.Kind == EnemyKind.Boss)
                {
                    UpdateBoss(e, dt);
                    if (e.Life <= 0) RemoveEnemy(i);
                    continue;
                }
                var speed = BalanceSettings.EnemyMovementMultiplier(phase);
                if (e.Kind == EnemyKind.Scout) { e.Radius = Mathf.Min(3.1f, e.Radius + dt*speed); e.Angle += dt*.7f; }
                else if (e.Kind == EnemyKind.Spiral) { e.Radius = 2.35f + Mathf.Sin(Time.time*2.2f+i)*.75f; e.Angle += dt*1.4f; }
                else if (e.Kind == EnemyKind.Diver) { e.Radius = 2.4f + Mathf.PingPong(Time.time*2.6f+i, 1.5f); e.Angle = Mathf.LerpAngle(e.Angle*Mathf.Rad2Deg, playerAngle*Mathf.Rad2Deg, dt*.8f)*Mathf.Deg2Rad; }
                else { e.Radius = 2.7f; e.Angle += dt*.55f; }
                e.transform.position = new Vector2(Mathf.Cos(e.Angle), Mathf.Sin(e.Angle))*e.Radius;
                if (e.FireTimer <= 0) { FireEnemy(e); e.FireTimer = BalanceSettings.EnemyFireInterval(phase); }
                if (e.Life <= 0) RemoveEnemy(i);
            }
            if (!coreActive && !bossSpawnPending && spawnsLeft == 0 && enemies.Count == 0) ActivateCore();
        }

        private void FireEnemy(Enemy e)
        {
            var direction = ((Vector2)player.position-(Vector2)e.transform.position).normalized;
            var projectileSpeed = BalanceSettings.EnemyProjectileSpeed(phase);
            Shoot(e.transform.position, direction * projectileSpeed, false, new Color(1f,.18f,.42f), DamageElement.Poison);
            if (e.Kind == EnemyKind.Turret) { Shoot(e.transform.position, Rotate(direction,18) * projectileSpeed * 1.04f,false,new Color(1f,.18f,.42f), DamageElement.Poison); Shoot(e.transform.position,Rotate(direction,-18) * projectileSpeed * 1.04f,false,new Color(1f,.18f,.42f), DamageElement.Poison); }
        }

        private void UpdateBoss(Enemy boss, float dt)
        {
            boss.BossStateTimer -= dt;
            var playerDirection = ((Vector2)player.position - (Vector2)boss.transform.position).normalized;
            var playerAngleTarget = Mathf.Atan2(player.position.y, player.position.x);
            if (boss.BossStateTimer <= 0f) ChooseBossState(boss);

            switch (boss.BossState)
            {
                case BossAiState.Orbit:
                    boss.Radius = Mathf.Lerp(boss.Radius, BossSettings.OrbitRadius + Mathf.Sin(Time.time * 1.6f) * .2f, dt * 1.5f);
                    boss.Angle = MoveTowardsAngleRadians(boss.Angle, playerAngleTarget + .82f, dt * .85f);
                    if (boss.FireTimer <= 0f)
                    {
                        FireBossFan(boss, playerDirection, 2, 17f);
                        boss.FireTimer = BossSettings.AimBurstInterval;
                    }
                    break;

                case BossAiState.Barrage:
                    boss.Radius = Mathf.Lerp(boss.Radius, 2.75f, dt * 1.8f);
                    boss.Angle += dt * 1.55f;
                    if (boss.FireTimer <= 0f)
                    {
                        FireBossFan(boss, playerDirection, 3, 15f);
                        boss.FireTimer = BossSettings.BarrageInterval;
                    }
                    break;

                default: // Charge: tries to line up with the ship, then floods the lane.
                    boss.Radius = Mathf.MoveTowards(boss.Radius, 1.12f, dt * 1.45f);
                    boss.Angle = MoveTowardsAngleRadians(boss.Angle, playerAngleTarget, dt * 1.8f);
                    if (boss.FireTimer <= 0f)
                    {
                        FireBossRadial(boss, 6);
                        boss.FireTimer = BossSettings.ChargeInterval;
                    }
                    break;
            }

            boss.transform.position = new Vector2(Mathf.Cos(boss.Angle), Mathf.Sin(boss.Angle)) * boss.Radius;
        }

        private void ChooseBossState(Enemy boss)
        {
            var healthRatio = boss.MaxHealth <= 0f ? 1f : boss.Health / boss.MaxHealth;
            if (healthRatio < .48f && Random.value < .52f)
            {
                boss.BossState = BossAiState.Charge;
                boss.BossStateTimer = 2.15f;
                boss.FireTimer = .15f;
                return;
            }

            if (Random.value < .55f)
            {
                boss.BossState = BossAiState.Barrage;
                boss.BossStateTimer = 2.8f;
                boss.FireTimer = .12f;
            }
            else
            {
                boss.BossState = BossAiState.Orbit;
                boss.BossStateTimer = 3.1f;
                boss.FireTimer = .28f;
            }
        }

        private void FireBossFan(Enemy boss, Vector2 direction, int count, float spread)
        {
            var projectileSpeed = BalanceSettings.EnemyProjectileSpeed(phase) * BossSettings.ProjectileSpeedMultiplier;
            var center = (count - 1) * .5f;
            var element = BossSettings.AttackElement(boss.BossState);
            for (var i = 0; i < count; i++) Shoot(boss.transform.position, Rotate(direction, (i - center) * spread) * projectileSpeed, false, Color.white, element);
        }

        private void FireBossRadial(Enemy boss, int count)
        {
            var projectileSpeed = BalanceSettings.EnemyProjectileSpeed(phase) * BossSettings.ProjectileSpeedMultiplier * .86f;
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count + boss.Angle;
                Shoot(boss.transform.position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * projectileSpeed, false, Color.white, BossSettings.AttackElement(boss.BossState));
            }
        }

        private void UpdateProjectiles(float dt)
        {
            for (var i=projectiles.Count-1;i>=0;i--)
            {
                var p=projectiles[i]; p.Life-=dt; p.transform.position += (Vector3)(p.Velocity*dt);
                var released = !p.VisualOnly && (p.FromPlayer ? HitEnemies(i,p) : HitPlayer(i,p));
                if (!released && p.Life <= 0) RemoveProjectile(i);
            }
        }

        private bool HitEnemies(int projectileIndex, Projectile p)
        {
            for (var j = enemies.Count - 1; j >= 0; j--)
            {
                if (Vector2.Distance(p.transform.position, enemies[j].transform.position) >= .28f) continue;
                var enemy = enemies[j];
                var resistance = enemy.Kind == EnemyKind.Boss ? BossSettings.Resistance(p.Element) : 1f;
                enemy.Health -= ElementalCombat.ApplyResistance(p.Damage, resistance);
                RemoveProjectile(projectileIndex);
                var impactColor = EnemyEffectColor(enemy.Kind);
                if (enemy.Health <= 0)
                {
                    score += enemy.Points;
                    PlayEnemyDeathEffect(.82f);
                    var isBoss = enemy.Kind == EnemyKind.Boss;
                    SpawnImpactBurst(enemy.transform.position, impactColor, isBoss ? 42 : 13, isBoss ? 4.6f : 2.9f, isBoss ? .9f : .48f);
                    AddScreenShake(isBoss ? .36f : .09f, isBoss ? .18f : .065f);
                    if (isBoss)
                    {
                        HapticFeedback.Pulse(65);
                        phaseUpgradeBannerTimer = 1.55f;
                        phaseUpgradeLabel = "БОСС УНИЧТОЖЕН\nЯДРО ДОСТУПНО";
                        SpawnWarpBurst(70, 2.15f);
                    }
                    if (!splitPickup.gameObject.activeSelf && Random.value < .10f) ActivateSplitPickup(enemy.transform.position);
                    RemoveEnemy(j);
                }
                else
                {
                    // Короткий процедурный Enemy hit воспринимался на мобильных
                    // как раздражающий тик. Оставляем визуальный импакт и звук
                    // уничтожения, который проигрывается отдельным событием выше.
                    SpawnImpactBurst(enemy.transform.position, impactColor, 4, 1.35f, .18f);
                }
                return true;
            }
            return false;
        }

        private bool HitPlayer(int projectileIndex, Projectile p)
        {
            if (invincible > 0f) return false;

            // Звёзды на орбите корабля перехватывают снаряды раньше, чем те
            // достигают корпуса. Каждое попадание снимает ровно один щит.
            // Мягкий радиус столкновения сохраняет управление отзывчивым.
            for (var i = 0; i < stars.Count; i++)
            {
                var star = stars[i];
                if (!star.IsShield || Vector2.Distance(p.transform.position, star.transform.position) >= .22f) continue;
                RemoveProjectile(projectileIndex);
                ConsumeStarShield(star.transform.position, star);
                return true;
            }

            if (Vector2.Distance(p.transform.position, player.position) >= .3f) return false;
            RemoveProjectile(projectileIndex);
            if (starShields > 0)
            {
                ConsumeStarShield(player.position, null);
                return true;
            }
            DamagePlayer();
            return true;
        }

        private void ConsumeStarShield(Vector2 impactPosition, StarParticle preferredStar)
        {
            var star = preferredStar != null && preferredStar.IsShield ? preferredStar : null;
            if (star == null)
            {
                for (var i = stars.Count - 1; i >= 0; i--)
                {
                    if (stars[i].IsShield) { star = stars[i]; break; }
                }
            }
            if (star == null) { starShields = 0; return; }
            star.ShieldHits--;
            SpawnImpactBurst(impactPosition, star.IsPurple ? new Color(.86f, .5f, 1f, 1f) : new Color(.62f, .9f, 1f, 1f), 10, 1.9f, .3f);
            AddScreenShake(.055f, .025f);
            HapticFeedback.Pulse(20);
            if (star.ShieldHits > 0)
            {
                // Фиолетовый solid-щит переживает первый удар и остаётся
                // вращаться до следующего попадания.
                star.Renderer.color = new Color(.95f, .68f, 1f, 1f);
                return;
            }
            stars.Remove(star);
            starPool.Release(star);
            starShields = Mathf.Max(0, starShields - 1);
        }

        private void DamagePlayer()
        {
            shields--; invincible=1f; hpFlashTimer = .34f; SpawnPlayerDamageBurst(); PlayEffect(playerDamageSound, .8f); AddScreenShake(.22f, .14f); HapticFeedback.Pulse(shields <= 0 ? 110 : 48); if (shields <= 0) EndGame();
        }

        private void ActivateCore() { coreActive=true; core.gameObject.SetActive(true); coreAngle=Random.Range(-2.6f,-.5f); SpawnWarpBurst(14,.7f); }
        private void UpdateCore(float dt)
        {
            if (!coreActive) { core.gameObject.SetActive(false); return; }
            coreAngle += dt*.5f;
            // Ядро идёт по той же орбите, что и корабль, поэтому его можно подобрать.
            var corePosition = new Vector2(Mathf.Cos(coreAngle), Mathf.Sin(coreAngle)) * OrbitSettings.Radius;
            core.position = corePosition;
            core.Rotate(0,0,dt*160f);
            if (Vector2.Distance(corePosition, player.position)<.42f) { coreActive=false; cores++; HapticFeedback.Pulse(24); SpawnWarpBurst(24,1f); if(cores>=3) BeginWarp(); else StartWave(); }
        }

        private void BeginWarp()
        {
            cores = 0;
            phase++;
            warpTimer = 1.9f;
            phaseUpgradeBannerTimer = 1.5f;
            if (phase == 3)
            {
                tripleShotTimer = 7f;
                phaseUpgradeLabel = "БОНУС:\n+3 СНАРЯДА НА 7 СЕК";
            }
            else
            {
                phaseUpgradeLabel = phase % 2 == 0
                    ? "БОНУС:\n+18% К СКОРОСТРЕЛЬНОСТИ"
                    : "БОНУС:\n+16% К СКОРОСТИ ЗАРЯДА";
            }
            StartWave();
            SpawnWarpBurst(80, 2.4f);
            AddScreenShake(.16f, .11f);
        }

        private void StartWave()
        {
            if (phase == BossSettings.Phase)
            {
                spawnsLeft = 0;
                bossSpawnPending = true;
                bossSpawnTimer = BossSettings.IntroDelay;
                return;
            }
            spawnsLeft = 5 + phase * 2 + cores * 2;
            spawnTimer = .72f;
        }

        private void ActivateSplitPickup(Vector2 position)
        {
            var direction = ((Vector2)player.position).normalized;
            splitVelocity = direction * BonusSettings.Speed;
            splitLifetime = BonusSettings.Lifetime;
            splitPickup.position = Vector2.zero;
            splitPickup.gameObject.SetActive(true);
        }

        private void UpdateSplitPickup(float dt)
        {
            if (!splitPickup.gameObject.activeSelf) return;
            var toPlayer = ((Vector2)player.position - (Vector2)splitPickup.position).normalized;
            splitVelocity = Vector2.Lerp(splitVelocity, toPlayer * BonusSettings.Speed, Mathf.Clamp01(dt * BonusSettings.Homing));
            splitPickup.position = splitPickup.position + new Vector3(splitVelocity.x * dt, splitVelocity.y * dt, 0f);
            splitLifetime -= dt;
            splitPickup.Rotate(0, 0, BonusSettings.RotationSpeed * dt);
            if (splitLifetime <= 0f) { splitPickup.gameObject.SetActive(false); return; }
            if (Vector2.Distance(splitPickup.position, player.position) >= .45f) return;
            splitPickup.gameObject.SetActive(false);
            splitShot = true;
            splitShotTimer = 6f;
            SpawnWarpBurst(16, .85f);
        }

        private void UpdateStars(float dt)
        {
            starTimer -= dt; if(starTimer<=0) { starTimer=1f / StarStreamSettings.StarsPerSecond; SpawnWarpBurst(1,.55f); }
            for(var i=stars.Count-1;i>=0;i--)
            {
                var s=stars[i];
                if (s.IsShield)
                {
                    s.ShieldAngle += dt * 3.4f;
                    s.transform.position = player.position + (Vector3)(new Vector2(Mathf.Cos(s.ShieldAngle), Mathf.Sin(s.ShieldAngle)) * s.ShieldRadius);
                    var shieldTint = s.IsPurple ? new Color(.78f, .42f, 1f, 1f) : new Color(.68f, .92f, 1f, 1f);
                    s.Renderer.color = shieldTint;
                    s.Trail.startColor = new Color(shieldTint.r, shieldTint.g, shieldTint.b, .78f);
                    continue;
                }
                s.Life-=dt;
                var viewport = gameCamera.WorldToViewportPoint(s.transform.position);
                var edgeDistance = Mathf.Max(Mathf.Abs(viewport.x - .5f) * 2f, Mathf.Abs(viewport.y - .5f) * 2f);
                var slowdown = Mathf.Lerp(1f, StarStreamSettings.ScreenEdgeSpeedMultiplier, Mathf.InverseLerp(StarStreamSettings.ScreenEdgeSlowStart, 1.15f, edgeDistance));
                s.transform.position+=(Vector3)(s.Velocity * (dt * slowdown));
                // Белые звёзды остаются декоративным потоком. Только редкая
                // фиолетовая звезда может стать solid-щитом корабля.
                if (player != null && s.IsPurple && starShields < 3 && Vector2.Distance(s.transform.position, player.position) < .34f)
                {
                    s.IsShield = true;
                    s.Velocity = Vector2.zero;
                    s.Life = 999f;
                    s.ShieldAngle = Random.Range(0f, Mathf.PI * 2f);
                    s.ShieldRadius = .48f + starShields * .09f;
                    s.ShieldHits = s.IsPurple ? 2 : 1;
                    starShields++;
                    s.Trail.time = StarStreamSettings.ShieldTrailLength;
                    s.Trail.startWidth = StarStreamSettings.ShieldTrailWidth;
                    var shieldTint = s.IsPurple ? new Color(.78f, .42f, 1f, 1f) : new Color(.68f, .92f, 1f, 1f);
                    s.Renderer.color = shieldTint;
                    s.Trail.startColor = new Color(shieldTint.r, shieldTint.g, shieldTint.b, .78f);
                    s.Trail.Clear();
                    continue;
                }
                var alpha = Mathf.Clamp01(s.Life) * s.Brightness;
                var streamTint = s.IsPurple ? new Color(.76f, .38f, 1f, 1f) : Color.white;
                s.Renderer.color = new Color(streamTint.r, streamTint.g, streamTint.b, alpha);
                s.Trail.startColor = new Color(streamTint.r, streamTint.g, streamTint.b, alpha * StarStreamSettings.TrailFade);
                if(s.Life<=0)RemoveStar(i);
            }
            if(warpTimer>0) warpTimer-=dt;
            if (phaseUpgradeBannerTimer > 0f) phaseUpgradeBannerTimer -= dt;
        }

        private void SpawnWarpBurst(int amount,float speed)
        { if (starPool == null) return; for(var i=0;i<amount;i++){var angle=Random.Range(0,Mathf.PI*2);var s=starPool.Get();if (s == null) continue;s.ResetStar(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),speed*Random.Range(.7f,1.3f),Random.Range(1.6f,3f));s.SetPurple(Random.value < StarStreamSettings.PurpleChance);stars.Add(s);} }

        private void SpawnPlayerDamageBurst()
        {
            for (var i = 0; i < Random.Range(5, 7); i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var shard = damageShardPool.Get();
                shard.ResetShard(player.position, direction * Random.Range(1.3f, 2.5f), Random.Range(.035f, .075f), new Color(.2f, 1f, .4f, 1f));
                damageShards.Add(shard);
            }
        }

        private void UpdateDamageShards(float dt)
        {
            for (var i = damageShards.Count - 1; i >= 0; i--)
            {
                var shard = damageShards[i];
                shard.Life -= dt;
                shard.transform.position += (Vector3)(shard.Velocity * dt);
                var color = shard.Renderer.color;
                color.a = Mathf.Clamp01(shard.Life / Mathf.Max(.001f, shard.MaxLife));
                shard.Renderer.color = color;
                if (shard.Life <= 0f) { damageShards.RemoveAt(i); damageShardPool.Release(shard); }
            }
        }

        private void SpawnImpactBurst(Vector2 position, Color color, int amount, float force, float lifetime)
        {
            if (damageShardPool == null) return;
            for (var i = 0; i < amount; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var shard = damageShardPool.Get();
                shard.ResetShard(position, direction * Random.Range(force * .45f, force), Random.Range(.025f, .07f), color, lifetime * Random.Range(.7f, 1.15f));
                damageShards.Add(shard);
            }
        }

        private static Color EnemyEffectColor(EnemyKind kind)
        {
            if (kind == EnemyKind.Scout) return new Color(1f, .42f, .06f, 1f);
            if (kind == EnemyKind.Spiral) return new Color(1f, .15f, .55f, 1f);
            if (kind == EnemyKind.Diver) return new Color(.9f, .25f, 1f, 1f);
            if (kind == EnemyKind.Boss) return new Color(.25f, .9f, 1f, 1f);
            return new Color(1f, .82f, .15f, 1f);
        }

        private void PlayEffect(AudioClip clip, float volume)
        {
            // PlayOneShot смешивает SFX поверх отдельного AudioSource музыки и не прерывает трек.
            if (GameAudioSettings.EffectsEnabled && effectsSource != null && clip != null) effectsSource.PlayOneShot(clip, volume);
        }

        private void PlayEnemyDeathEffect(float volume)
        {
            // Several enemies can die in the same render frame. Stacking the
            // synthesized transients produced the audible mobile "tick".
            if (enemyDeathSfxCooldown > 0f) return;
            enemyDeathSfxCooldown = .075f;
            PlayEffect(enemyDeathSound, volume);
        }

        private void StopGameplayMusic()
        {
            if (musicSource == null) return;
            musicSource.Stop();
            musicSource.time = 0f;
        }

        private void ToggleMusic()
        {
            var enabled = GameAudioSettings.ToggleMusic();
            if (musicSource == null) return;
            if (!enabled) musicSource.Pause();
            else if ((playing || coopPlaying) && musicSource.clip != null)
            {
                if (musicSource.timeSamples > 0) musicSource.UnPause();
                else musicSource.Play();
            }
        }

        private static void ToggleEffects()
        {
            GameAudioSettings.ToggleEffects();
        }

        private void ResumeMusicAfterBackground()
        {
            if ((!playing && !coopPlaying) || !GameAudioSettings.MusicEnabled || musicSource == null || musicSource.clip == null) return;
            if (musicSource.timeSamples > 0) musicSource.UnPause();
            else musicSource.Play();
        }

        private void AddScreenShake(float duration, float strength)
        {
            if (!GameVisualSettings.ScreenShakeEnabled)
            {
                screenShakeTimer = 0f;
                screenShakeStrength = 0f;
                if (gameCamera != null) gameCamera.transform.position = new Vector3(0f, 0f, -10f);
                return;
            }
            screenShakeTimer = Mathf.Max(screenShakeTimer, duration);
            screenShakeStrength = Mathf.Max(screenShakeStrength, strength);
        }

        private void UpdateScreenShake(float dt)
        {
            if (gameCamera == null) return;
            if (screenShakeTimer <= 0f)
            {
                gameCamera.transform.position = new Vector3(0f, 0f, -10f);
                return;
            }
            screenShakeTimer -= dt;
            var amount = screenShakeStrength * Mathf.Clamp01(screenShakeTimer / .25f);
            gameCamera.transform.position = new Vector3(Random.Range(-amount, amount), Random.Range(-amount, amount), -10f);
            if (screenShakeTimer <= 0f) screenShakeStrength = 0f;
        }
        private void RemoveEnemy(int index){var e=enemies[index];enemies.RemoveAt(index);enemyPool.Release(e);}
        private void RemoveProjectile(int index){if(index<0||index>=projectiles.Count)return;var p=projectiles[index];projectiles.RemoveAt(index);projectilePool.Release(p);}
        private void RemoveStar(int index){var s=stars[index];stars.RemoveAt(index);starPool.Release(s);}
        private static Vector2 Rotate(Vector2 value,float degrees){var r=degrees*Mathf.Deg2Rad;return new Vector2(value.x*Mathf.Cos(r)-value.y*Mathf.Sin(r),value.x*Mathf.Sin(r)+value.y*Mathf.Cos(r));}
        private static float MoveTowardsAngleRadians(float current, float target, float maxDelta)
        {
            return Mathf.MoveTowardsAngle(current * Mathf.Rad2Deg, target * Mathf.Rad2Deg, maxDelta * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        }

        private void EndGame()
        {
            playing = false;
            showResults = true;
            BeginUiFade();
            StopGameplayMusic();
            bestScore = Mathf.Max(bestScore, score);
            lastMmrDelta = MmrSettings.CalculateChange(score, mmr);
            mmr = Mathf.Max(MmrSettings.MinimumMmr, mmr + lastMmrDelta);
            mmrResultTimer = 2.25f;
            PlayerPrefs.SetInt("orbital_rift_best", bestScore);
            PlayerPrefs.SetInt("orbital_rift_mmr", mmr);
            PlayerPrefs.Save();
            if (firebaseScores != null) firebaseScores.SubmitProgress(bestScore, mmr, playerNickname, string.IsNullOrWhiteSpace(currentRunId) ? Guid.NewGuid().ToString("N") : currentRunId);
        }

        private void ApplyCloudBestScore(int cloudScore)
        {
            if (cloudScore <= bestScore) return;
            bestScore = cloudScore;
            PlayerPrefs.SetInt("orbital_rift_best", bestScore);
            PlayerPrefs.Save();
        }

        private void ApplyCloudMmr(int cloudMmr)
        {
            // Не меняем базу рейтинга посреди уже начатого забега или на экране
            // его результата. Облачное значение применяется в безопасном меню.
            if (!showMenu) return;
            mmr = Mathf.Max(MmrSettings.MinimumMmr, cloudMmr);
            PlayerPrefs.SetInt("orbital_rift_mmr", mmr);
            PlayerPrefs.SetInt("orbital_rift_mmr_revision", MmrSettings.RatingRevision);
            PlayerPrefs.Save();
        }

        private void ApplyScoreLeaderboard(IReadOnlyList<LeaderboardEntry> entries)
        {
            scoreLeaderboardEntries = entries;
        }

        private void ApplyMmrLeaderboard(IReadOnlyList<LeaderboardEntry> entries)
        {
            mmrLeaderboardEntries = entries;
        }

        private void ApplyFirebaseConnectionState(FirebaseConnectionState state)
        {
            firebaseConnectionState = state;
        }

        private static string SanitizeNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname)) return string.Empty;
            var trimmed = nickname.Trim();
            return trimmed.Length <= 16 ? trimmed : trimmed.Substring(0, 16);
        }

        private static GUIStyle MakeCallsignInputStyle()
        {
            return new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 1,
                normal = { textColor = new Color(1f, 1f, 1f, 0f), background = null },
                focused = { textColor = new Color(1f, 1f, 1f, 0f), background = null },
                hover = { textColor = new Color(1f, 1f, 1f, 0f), background = null },
                active = { textColor = new Color(1f, 1f, 1f, 0f), background = null },
                padding = new RectOffset(8, 8, 4, 4)
            };
        }

        private static bool DrawPixelButton(Rect rect, string label, int pixelSize, Color fill, Color border, Color text)
        {
            var hovered = rect.Contains(Event.current.mousePosition);
            PixelUi.DrawPanel(rect, hovered ? Color.Lerp(fill, Color.white, .13f) : fill, hovered ? Color.white : border, 3f);
            var clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            PixelUi.DrawText(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), label, pixelSize, text);
            return clicked;
        }

        private Enemy ActiveBoss()
        {
            for (var i = 0; i < enemies.Count; i++)
                if (enemies[i] != null && enemies[i].Kind == EnemyKind.Boss) return enemies[i];
            return null;
        }

        private static string RankTitle(int rating)
        {
            if (rating < MmrSettings.GuardianThreshold) return "НАВИГАТОР";
            if (rating < MmrSettings.LegendThreshold) return "СТРАЖ";
            if (rating < MmrSettings.OverlordThreshold) return "ЛЕГЕНДА";
            if (rating < MmrSettings.DivinityThreshold) return "ВЛАСТЕЛИН";
            return "БОЖЕСТВО";
        }

        private static Color RankColor(int rating)
        {
            if (rating < MmrSettings.GuardianThreshold) return new Color(.48f, .64f, .94f);
            if (rating < MmrSettings.LegendThreshold) return new Color(.3f, .98f, .64f);
            if (rating < MmrSettings.OverlordThreshold) return new Color(.86f, .38f, 1f);
            if (rating < MmrSettings.DivinityThreshold) return new Color(1f, .34f, .14f);
            return new Color(1f, .86f, .22f);
        }

        private Sprite RankSprite(int rating)
        {
            if (rating < MmrSettings.GuardianThreshold) return navigatorRankSprite;
            if (rating < MmrSettings.LegendThreshold) return guardianRankSprite;
            if (rating < MmrSettings.OverlordThreshold) return legendRankSprite;
            if (rating < MmrSettings.DivinityThreshold) return overlordRankSprite;
            return divinityRankSprite;
        }

        private void DrawRankIcon(Rect rect, int rating)
        {
            var sprite = RankSprite(rating);
            if (sprite != null && sprite.texture != null)
            {
                GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
                return;
            }
            PixelUi.DrawPanel(rect, new Color(.02f, .035f, .10f, .98f), RankColor(rating), 2f);
        }

        private static string RankRange(int index)
        {
            if (index == 0) return "0–999 MMR";
            if (index == 1) return "1000–1999";
            if (index == 2) return "2000–2999";
            if (index == 3) return "3000–3999";
            return "4000+ MMR";
        }

        private static int RankThreshold(int index)
        {
            if (index == 0) return MmrSettings.NavigatorThreshold;
            if (index == 1) return MmrSettings.GuardianThreshold;
            if (index == 2) return MmrSettings.LegendThreshold;
            if (index == 3) return MmrSettings.OverlordThreshold;
            return MmrSettings.DivinityThreshold;
        }

        private static string CompactLeaderboardNickname(string nickname)
        {
            var value = string.IsNullOrWhiteSpace(nickname) ? "ПИЛОТ" : nickname.Trim();
            const int maxVisibleCharacters = 9;
            return value.Length <= maxVisibleCharacters
                ? value
                : value.Substring(0, maxVisibleCharacters - 1) + ".";
        }

        private void DrawLeaderboardColumn(Rect rect, string title, IReadOnlyList<LeaderboardEntry> entries, bool showRanks, int textSize, Color textColor)
        {
            PixelUi.DrawText(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height * .16f), title, textSize, textColor);
            if (entries == null)
            {
                PixelUi.DrawText(new Rect(rect.x + 4f, rect.y + rect.height * .34f, rect.width - 8f, rect.height * .30f), "SYNC", textSize, new Color(.55f, .7f, .9f));
                return;
            }
            if (entries.Count == 0)
            {
                PixelUi.DrawText(new Rect(rect.x + 4f, rect.y + rect.height * .34f, rect.width - 8f, rect.height * .30f), "ПУСТО", textSize, new Color(.55f, .7f, .9f));
                return;
            }

            var rowHeight = rect.height * .15f;
            for (var i = 0; i < entries.Count; i++)
            {
                var row = new Rect(rect.x + 4f, rect.y + rect.height * .20f + i * rowHeight, rect.width - 8f, rowHeight);
                var valueColor = showRanks ? RankColor(entries[i].Value) : textColor;
                var valueOffset = 0f;
                if (showRanks)
                {
                    var badge = new Rect(row.x, row.y + row.height * .12f, row.height * .72f, row.height * .72f);
                    DrawRankIcon(badge, entries[i].Value);
                    if (GUI.Button(badge, GUIContent.none, GUIStyle.none)) showRankGuide = true;
                    valueOffset = badge.width + 3f;
                }
                var compactNickname = CompactLeaderboardNickname(entries[i].Nickname);
                PixelUi.DrawText(new Rect(row.x + valueOffset, row.y, row.width * .59f - valueOffset, row.height), (i + 1) + ". " + compactNickname, textSize, textColor, TextAnchor.MiddleLeft);
                PixelUi.DrawText(new Rect(row.x + row.width * .61f, row.y, row.width * .39f, row.height), entries[i].Value.ToString(), textSize, valueColor, TextAnchor.MiddleRight);
            }
        }

        private void DrawRankGuide(float left, float top, float width, float height, int pixel, int smallPixel, Color pale, Color panel, Color cyan)
        {
            var shade = new Rect(left, top, width, height);
            PixelUi.DrawPanel(shade, new Color(.005f, .01f, .05f, .94f), new Color(.16f, .3f, .58f, .9f), 2f);
            var guide = new Rect(left + width * .09f, top + height * .055f, width * .82f, height * .84f);
            PixelUi.DrawPanel(guide, new Color(.02f, .035f, .12f, .99f), cyan, 4f);
            PixelUi.DrawText(new Rect(guide.x, guide.y + guide.height * .035f, guide.width, guide.height * .10f), "КЛАССЫ MMR", pixel, Color.white);
            PixelUi.DrawText(new Rect(guide.x, guide.y + guide.height * .13f, guide.width, guide.height * .05f), "ШАГ РАНГА 1000 // ЗА ИГРУ ОТ -150 ДО +150", smallPixel, pale);

            for (var i = 0; i < 5; i++)
            {
                var row = new Rect(guide.x + guide.width * .07f, guide.y + guide.height * (.20f + i * .125f), guide.width * .86f, guide.height * .11f);
                var threshold = RankThreshold(i);
                var rankColor = RankColor(threshold);
                PixelUi.DrawPanel(row, new Color(.015f, .025f, .09f, .98f), rankColor, 2f);
                var icon = new Rect(row.x + row.width * .02f, row.y + row.height * .06f, row.height * .88f, row.height * .88f);
                DrawRankIcon(icon, threshold);
                PixelUi.DrawText(new Rect(icon.xMax + row.width * .04f, row.y, row.width * .47f, row.height), RankTitle(threshold), smallPixel, rankColor, TextAnchor.MiddleLeft);
                PixelUi.DrawText(new Rect(row.x + row.width * .61f, row.y, row.width * .35f, row.height), RankRange(i), smallPixel, pale, TextAnchor.MiddleRight);
            }

            if (DrawPixelButton(new Rect(guide.x + guide.width * .24f, guide.y + guide.height * .86f, guide.width * .52f, guide.height * .09f), "ЗАКРЫТЬ", smallPixel, new Color(.08f, .12f, .30f, .98f), cyan, Color.white)) showRankGuide = false;
        }

        private void DrawRoomGuide(float left, float top, float width, float height, int pixel, int smallPixel,
            Color pale, Color panel, Color cyan)
        {
            PixelUi.DrawPanel(new Rect(left, top, width, height), new Color(.004f, .008f, .03f, .98f),
                new Color(.12f, .30f, .50f, .75f), 2f);
            var guide = new Rect(left + width * .07f, top + height * .035f, width * .86f, height * .91f);
            PixelUi.DrawPanel(guide, new Color(.012f, .026f, .075f, .99f), cyan, 4f);
            PixelUi.DrawText(new Rect(guide.x + 10f, guide.y + guide.height * .025f, guide.width - 20f, guide.height * .065f),
                "ЭКСПЕДИЦИЯ // СПРАВОЧНИК", pixel, Color.white);
            PixelUi.DrawText(new Rect(guide.x + 12f, guide.y + guide.height * .09f, guide.width - 24f, guide.height * .075f),
                "14 КОМНАТ · 5 КОРПУСОВ · АВТООГОНЬ\n0 КОРПУСОВ = КОНЕЦ ЗАБЕГА", smallPixel, pale);

            DrawRoomGuideRow(new Rect(guide.x + guide.width * .05f, guide.y + guide.height * .18f,
                    guide.width * .90f, guide.height * .085f), SectorRoomType.Start,
                "БЕЗОПАСНО. 4 СЕКУНДЫ НА ВХОД.\nНАСТРОЙ ОРБИТУ.", smallPixel, panel);
            DrawRoomGuideRow(new Rect(guide.x + guide.width * .05f, guide.y + guide.height * .275f,
                    guide.width * .90f, guide.height * .085f), SectorRoomType.Combat,
                "ЗАЛП ИЛИ МИНЫ ПО КОРАБЛЮ.\nПОПАДАНИЕ: -1 КОРПУС.", smallPixel, panel);
            DrawRoomGuideRow(new Rect(guide.x + guide.width * .05f, guide.y + guide.height * .37f,
                    guide.width * .90f, guide.height * .085f), SectorRoomType.Elite,
                "ВОЛНА, КОЛЬЦО И ЧАСТЫЕ АТАКИ.\nПОПАДАНИЕ: -1 КОРПУС.", smallPixel, panel);
            DrawRoomGuideRow(new Rect(guide.x + guide.width * .05f, guide.y + guide.height * .465f,
                    guide.width * .90f, guide.height * .085f), SectorRoomType.Event,
                "БЕЗОПАСНО. СОБИРАЙ РЕЗОНАНС.\nТОЛКАЙ ЯДРО В УГРОЗУ.", smallPixel, panel);
            DrawRoomGuideRow(new Rect(guide.x + guide.width * .05f, guide.y + guide.height * .56f,
                    guide.width * .90f, guide.height * .085f), SectorRoomType.Shop,
                "БЕЗОПАСНО. ПЕРЕДЫШКА.\nБОНУС К РЕЗОНАНСУ.", smallPixel, panel);
            DrawRoomGuideRow(new Rect(guide.x + guide.width * .05f, guide.y + guide.height * .655f,
                    guide.width * .90f, guide.height * .085f), SectorRoomType.Boss,
                "ЗАЛП, ВОЛНА, КОЛЬЦО И МИНЫ.\nПОПАДАНИЕ: -2 КОРПУСА.", smallPixel, panel);

            PixelUi.DrawText(new Rect(guide.x + 14f, guide.y + guide.height * .755f, guide.width - 28f, guide.height * .09f),
                "ВАШИ СНАРЯДЫ ЛЕТЯТ ПО ТРАЕКТОРИИ И МОГУТ ПРОМАХНУТЬСЯ.\n" +
                "АТАКИ ЧИТАЮТСЯ ПО ТЕЛЕГРАФУ: ДВИГАЙСЯ, ИЩИ РАЗРЫВ, УХОДИ С МЕТКИ.",
                Mathf.Max(3, smallPixel - 1), new Color(.64f, .90f, 1f));
            if (DrawPixelButton(new Rect(guide.x + guide.width * .27f, guide.y + guide.height * .865f,
                    guide.width * .46f, guide.height * .075f), "ЗАКРЫТЬ", smallPixel,
                    new Color(.08f, .12f, .30f, .98f), cyan, Color.white))
            {
                showRoomGuide = false;
                BeginUiFade();
            }
        }

        private static void DrawRoomGuideRow(Rect rect, SectorRoomType type, string description,
            int smallPixel, Color panel)
        {
            var color = SectorRoomColor(type);
            PixelUi.DrawPanel(rect, panel, color, 2f);
            PixelUi.DrawText(new Rect(rect.x + 8f, rect.y, rect.width * .25f, rect.height),
                SectorRoomLabel(type), smallPixel, color, TextAnchor.MiddleLeft);
            PixelUi.DrawText(new Rect(rect.x + rect.width * .27f, rect.y + 2f, rect.width * .70f, rect.height - 4f),
                description, Mathf.Max(3, smallPixel - 1), Color.white, TextAnchor.MiddleLeft);
        }

        private void CloseSettings()
        {
            playerNickname = SanitizeNickname(playerNickname);
            if (!string.IsNullOrEmpty(playerNickname))
            {
                nicknameError = string.Empty;
                PlayerPrefs.SetString("orbital_rift_nickname", playerNickname);
                PlayerPrefs.Save();
            }
            showSettings = false;
            BeginUiFade();
        }

        private void BeginUiFade()
        {
            uiFadeTimer = Application.isPlaying ? UiFadeDuration : 0f;
        }

        private void DrawUiFade(float left, float top, float width, float height)
        {
            if (uiFadeTimer <= 0f || Event.current.type != EventType.Repaint) return;
            var alpha = Mathf.Clamp01(uiFadeTimer / UiFadeDuration);
            PixelUi.DrawPanel(new Rect(left, top, width, height), new Color(.002f, .004f, .02f, alpha), Color.clear, 0f);
        }

        private async void CreateCoopParty()
        {
            if (multiplayerSessions == null || multiplayerSessions.IsBusy) return;
            if (string.IsNullOrWhiteSpace(playerNickname))
            {
                nicknameError = "ВВЕДИ ПОЗЫВНОЙ ДЛЯ КООПЕРАТИВА";
                showCoop = false;
                showSettings = true;
                BeginUiFade();
                return;
            }
            await multiplayerSessions.CreatePartyAsync(playerNickname, selectedShip);
        }

        private async void JoinCoopParty()
        {
            if (multiplayerSessions == null || multiplayerSessions.IsBusy) return;
            if (string.IsNullOrWhiteSpace(playerNickname))
            {
                nicknameError = "ВВЕДИ ПОЗЫВНОЙ ДЛЯ КООПЕРАТИВА";
                showCoop = false;
                showSettings = true;
                BeginUiFade();
                return;
            }
            await multiplayerSessions.JoinPartyAsync(partyJoinCode, playerNickname, selectedShip);
        }

        private async void LeaveCoopParty()
        {
            if (multiplayerSessions == null) return;
            await multiplayerSessions.LeavePartyAsync();
        }

        private void SelectShip(ShipArchetype archetype)
        {
            selectedShip = archetype;
            PlayerPrefs.SetInt(ShipLoadoutSettings.PlayerPrefsKey, (int)selectedShip);
            PlayerPrefs.Save();
        }

        private void DrawShipSelector(Rect window, int smallPixel, Color pale, Color panel, bool locked)
        {
            var loadout = ShipLoadoutSettings.Get(selectedShip);
            PixelUi.DrawText(new Rect(window.x + 12f, window.y + window.height * .155f, window.width - 24f, window.height * .045f),
                "КОРАБЛЬ // " + ShipLoadoutSettings.Title(selectedShip) + " // " + loadout.Element.ToString().ToUpperInvariant(), smallPixel, pale);

            var labels = new[] { "ШТУРМ", "ХОЛОД", "ОГОНЬ", "ЯД" };
            for (var i = 0; i < ShipLoadoutSettings.Count; i++)
            {
                var archetype = (ShipArchetype)i;
                var selected = archetype == selectedShip;
                var rect = new Rect(window.x + window.width * (.045f + i * .235f), window.y + window.height * .205f,
                    window.width * .205f, window.height * .072f);
                var border = selected ? ShipLoadoutSettings.Get(archetype).ProjectileColor : new Color(.20f, .38f, .56f);
                var fill = selected ? new Color(.12f, .16f, .34f, .98f) : panel;
                if (locked)
                {
                    PixelUi.DrawPanel(rect, fill, border, selected ? 3f : 1f);
                    PixelUi.DrawText(rect, labels[i], smallPixel, selected ? Color.white : new Color(.42f, .52f, .64f));
                }
                else if (DrawPixelButton(rect, labels[i], smallPixel, fill, border, selected ? Color.white : pale))
                    SelectShip(archetype);
            }
        }

        private void DrawCoopScreen(float left, float top, float width, float height, int pixel, int smallPixel, Color pale, Color panel, Color cyan, Color violet)
        {
            if (multiplayerSessions == null) multiplayerSessions = GetComponent<MultiplayerSessionController>();
            if (coopSimulation == null) coopSimulation = GetComponent<CoopSimulationBridge>();
            PixelUi.DrawPanel(new Rect(left, top, width, height), new Color(.004f, .008f, .035f, .98f), new Color(.12f, .25f, .48f, .65f), 2f);
            var window = new Rect(left + width * .08f, top + height * .055f, width * .84f, height * .87f);
            PixelUi.DrawPanel(window, new Color(.018f, .035f, .105f, .98f), cyan, 4f);
            PixelUi.DrawText(new Rect(window.x + 16f, window.y + window.height * .025f, window.width - 32f, window.height * .10f), "КООПЕРАТИВ", Mathf.RoundToInt(pixel * 1.35f), Color.white);
            PixelUi.DrawText(new Rect(window.x + 16f, window.y + window.height * .105f, window.width - 32f, window.height * .045f), "ДВА ПИЛОТА // ОДИН СЕКТОР", smallPixel, pale);
            DrawShipSelector(window, smallPixel, pale, panel,
                multiplayerSessions != null && multiplayerSessions.CurrentSession != null);

            if (multiplayerSessions == null)
            {
                PixelUi.DrawText(new Rect(window.x + window.width * .08f, window.y + window.height * .36f, window.width * .84f, window.height * .18f), "СЕТЕВОЙ МОДУЛЬ НЕ НАЙДЕН", pixel, new Color(1f, .38f, .48f));
            }
            else if (!multiplayerSessions.HasUnityCloudProject)
            {
                PixelUi.DrawPanel(new Rect(window.x + window.width * .09f, window.y + window.height * .34f, window.width * .82f, window.height * .25f), panel, new Color(1f, .55f, .25f), 3f);
                PixelUi.DrawText(new Rect(window.x + window.width * .13f, window.y + window.height * .37f, window.width * .74f, window.height * .075f), "UNITY CLOUD НЕ ПРИВЯЗАН", pixel, new Color(1f, .72f, .35f));
                PixelUi.DrawText(new Rect(window.x + window.width * .13f, window.y + window.height * .47f, window.width * .74f, window.height * .08f), "PROJECT SETTINGS // SERVICES\nLINK PROJECT", smallPixel, pale);
#if UNITY_EDITOR
                if (DrawPixelButton(new Rect(window.x + window.width * .20f, window.y + window.height * .66f, window.width * .60f, window.height * .09f),
                        "ПРЕВЬЮ 2 ПИЛОТА", smallPixel, new Color(.06f, .11f, .27f, .98f), cyan, Color.white))
                    BeginLocalCoopPreview();
#endif
            }
            else if (multiplayerSessions.CurrentSession == null)
            {
                var busy = multiplayerSessions.IsBusy;
                var createLabel = multiplayerSessions.State == PartyConnectionState.Hosting ? "СОЗДАЕМ ПАТИ..." : "СОЗДАТЬ ПАТИ";
                if (DrawPixelButton(new Rect(window.x + window.width * .17f, window.y + window.height * .31f, window.width * .66f, window.height * .11f), createLabel, pixel,
                        busy ? new Color(.08f, .10f, .18f, .96f) : new Color(.14f, .08f, .34f, .98f), violet, busy ? new Color(.48f, .56f, .68f) : Color.white) && !busy)
                    CreateCoopParty();

                PixelUi.DrawText(new Rect(window.x + window.width * .12f, window.y + window.height * .46f, window.width * .76f, window.height * .045f), "ИЛИ ВВЕДИ КОД ПАТИ", smallPixel, pale);
                var codeRect = new Rect(window.x + window.width * .20f, window.y + window.height * .52f, window.width * .60f, window.height * .10f);
                PixelUi.DrawPanel(codeRect, panel, cyan, 3f);
                partyJoinCode = GUI.TextField(codeRect, partyJoinCode ?? string.Empty, 8, MakeCallsignInputStyle()).ToUpperInvariant();
                PixelUi.DrawText(new Rect(codeRect.x + 8f, codeRect.y + 4f, codeRect.width - 16f, codeRect.height - 8f),
                    string.IsNullOrEmpty(partyJoinCode) ? "КОД" : partyJoinCode, pixel,
                    string.IsNullOrEmpty(partyJoinCode) ? new Color(.42f, .62f, .76f, .86f) : Color.white);

                var joinLabel = multiplayerSessions.State == PartyConnectionState.Joining ? "ПОДКЛЮЧАЕМСЯ..." : "ВОЙТИ В ПАТИ";
                if (DrawPixelButton(new Rect(window.x + window.width * .24f, window.y + window.height * .66f, window.width * .52f, window.height * .105f), joinLabel, smallPixel,
                        busy ? new Color(.08f, .10f, .18f, .96f) : new Color(.04f, .13f, .24f, .98f), cyan, busy ? new Color(.48f, .56f, .68f) : Color.white) && !busy)
                    JoinCoopParty();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!busy && DrawPixelButton(new Rect(window.x + window.width * .25f, window.y + window.height * .79f, window.width * .50f, window.height * .06f),
                        "ЛОКАЛЬНЫЙ ТЕСТ // 2 ПИЛОТА", Mathf.Max(3, smallPixel - 1), new Color(.035f, .08f, .16f, .98f),
                        new Color(.30f, .64f, .88f), new Color(.72f, .90f, 1f)))
                    BeginLocalCoopPreview();
#endif
            }
            else
            {
                var session = multiplayerSessions.CurrentSession;
                var reconnecting = multiplayerSessions.State == PartyConnectionState.Reconnecting;
                var statusColor = reconnecting ? new Color(1f, .58f, .28f) :
                    session.PlayerCount >= 2 ? new Color(.35f, 1f, .68f) : new Color(1f, .82f, .32f);
                PixelUi.DrawPanel(new Rect(window.x + window.width * .10f, window.y + window.height * .32f, window.width * .80f, window.height * .29f), panel, statusColor, 3f);
                PixelUi.DrawText(new Rect(window.x + window.width * .13f, window.y + window.height * .345f, window.width * .74f, window.height * .06f), "КОД ПАТИ", smallPixel, pale);
                PixelUi.DrawText(new Rect(window.x + window.width * .13f, window.y + window.height * .405f, window.width * .74f, window.height * .11f), multiplayerSessions.PartyCode, Mathf.RoundToInt(pixel * 1.45f), Color.white);
                PixelUi.DrawText(new Rect(window.x + window.width * .13f, window.y + window.height * .53f, window.width * .74f, window.height * .05f),
                    "ПИЛОТЫ " + session.PlayerCount + "/2 // " + (multiplayerSessions.IsHost ? "ХОСТ" : "КЛИЕНТ"), smallPixel, statusColor);

                PixelUi.DrawText(new Rect(window.x + window.width * .13f, window.y + window.height * .585f, window.width * .74f, window.height * .04f),
                    "SEED " + multiplayerSessions.RunSeed + " // " +
                    (multiplayerSessions.CurrentSector == null ? "КАРТА..." : multiplayerSessions.CurrentSector.Rooms.Count + " КОМНАТ"), smallPixel, pale);

                if (reconnecting)
                    PixelUi.DrawText(new Rect(window.x + window.width * .13f, window.y + window.height * .615f, window.width * .74f, window.height * .035f),
                        "СВЯЗЬ ПОТЕРЯНА // ПОВТОР " + multiplayerSessions.ReconnectAttempts, smallPixel, statusColor);

                var networkReady = coopSimulation != null && coopSimulation.IsNetworkReady;
                var canStart = !reconnecting && multiplayerSessions.IsHost && session.PlayerCount >= 2 && networkReady;
                var startLabel = reconnecting ? "ПЕРЕПОДКЛЮЧАЕМСЯ..." : !multiplayerSessions.IsHost ? "ЖДЕМ ЗАПУСК ХОСТА" :
                    session.PlayerCount < 2 ? "ЖДЕМ ВТОРОГО ПИЛОТА" : networkReady ? "НАЧАТЬ СЕКТОР" : "СЕТЬ ЗАПУСКАЕТСЯ...";
                if (multiplayerSessions.IsHost)
                {
                    if (DrawPixelButton(new Rect(window.x + window.width * .17f, window.y + window.height * .64f, window.width * .66f, window.height * .085f),
                            startLabel, smallPixel, canStart ? new Color(.10f, .24f, .22f, .98f) : new Color(.07f, .09f, .16f, .98f),
                            canStart ? new Color(.35f, 1f, .68f) : new Color(.35f, .46f, .58f), canStart ? Color.white : new Color(.52f, .62f, .72f)) && canStart)
                        coopSimulation.HostStartRun();
                }
                else
                    PixelUi.DrawText(new Rect(window.x + window.width * .12f, window.y + window.height * .65f, window.width * .76f, window.height * .065f), startLabel, smallPixel, statusColor);

                if (DrawPixelButton(new Rect(window.x + window.width * .23f, window.y + window.height * .75f, window.width * .54f, window.height * .07f), "КОПИРОВАТЬ КОД", smallPixel, panel, cyan, pale))
                    GUIUtility.systemCopyBuffer = multiplayerSessions.PartyCode;
                if (DrawPixelButton(new Rect(window.x + window.width * .27f, window.y + window.height * .84f, window.width * .46f, window.height * .07f), "ВЫЙТИ ИЗ ПАТИ", smallPixel, new Color(.22f, .045f, .10f, .98f), new Color(1f, .32f, .45f), Color.white))
                    LeaveCoopParty();
            }

            if (multiplayerSessions != null && !string.IsNullOrEmpty(multiplayerSessions.LastError))
                PixelUi.DrawText(new Rect(window.x + window.width * .08f, window.y + window.height * .79f, window.width * .84f, window.height * .07f), multiplayerSessions.LastError, smallPixel, new Color(1f, .38f, .48f));

            if (multiplayerSessions == null || multiplayerSessions.CurrentSession == null)
                if (DrawPixelButton(new Rect(window.x + window.width * .34f, window.y + window.height * .86f, window.width * .32f, window.height * .075f), "НАЗАД", smallPixel, panel, cyan, pale))
                {
                    showCoop = false;
                    BeginUiFade();
                }
        }

        private void DrawCoopHud(float left, float top, float width, float height, int pixel, int smallPixel, Color pale, Color panel, Color cyan, Color violet)
        {
            var hostName = coopLocalPreview ? (string.IsNullOrWhiteSpace(playerNickname) ? "HOST" : playerNickname) : multiplayerSessions.HostCallsign;
            var guestName = coopLocalPreview ? "BOT-PYRE" : multiplayerSessions.GuestCallsign;
            var seed = coopLocalPreview ? coopPreviewRunSeed : coopSimulation.ActiveRunSeed;
            var layout = coopLocalPreview ? coopPreviewSector : multiplayerSessions.CurrentSector;
            var rooms = layout == null ? 0 : layout.Rooms.Count;
            var roomIndex = coopLocalPreview ? coopPreviewRoomIndex : (coopSimulation == null ? 0 : coopSimulation.ActiveRoomIndex);
            roomIndex = Mathf.Clamp(roomIndex, 0, Mathf.Max(0, rooms - 1));

            var threatHealth = coopLocalPreview ? coopPreviewEnemyHealth : (coopSimulation == null ? 0 : coopSimulation.CoopEnemyHealth);
            var threatMaxHealth = coopLocalPreview ? coopPreviewEnemyMaxHealth : (coopSimulation == null ? 0 : coopSimulation.CoopEnemyMaxHealth);
            var threatKind = coopLocalPreview ? coopPreviewEnemyKind : (coopSimulation == null ? (byte)0 : coopSimulation.CoopEnemyKind);
            var threatRoomType = (SectorRoomType)Mathf.Clamp(threatKind, 0, (int)SectorRoomType.Boss);
            var runCompleted = coopLocalPreview ? coopPreviewCompleted : (coopSimulation != null && coopSimulation.RunCompleted);
            var runFailed = coopLocalPreview ? coopPreviewFailed : (coopSimulation != null && coopSimulation.RunFailed);
            var teamHealth = coopLocalPreview ? coopPreviewTeamHealth : (coopSimulation == null ? 0 : coopSimulation.CoopTeamHealth);
            var teamMaxHealth = coopLocalPreview ? coopPreviewTeamMaxHealth : (coopSimulation == null ? CoopRoomRules.TeamMaxHealth : coopSimulation.CoopTeamMaxHealth);
            var threatColor = SectorRoomColor(threatRoomType);
            var teamColor = runFailed ? new Color(1f, .25f, .30f) : new Color(.34f, 1f, .68f);

            // Compact top-only HUD: the central arena remains visible during combat.
            var header = new Rect(left + width * .035f, top + height * .018f, width * .93f, height * .092f);
            PixelUi.DrawPanel(header, new Color(.008f, .020f, .065f, .88f), cyan, 2f);
            PixelUi.DrawText(new Rect(header.x + 10f, header.y + header.height * .04f, header.width * .54f, header.height * .42f),
                "УЗЕЛ " + (roomIndex + 1).ToString("00") + "/" + rooms.ToString("00") + " // " + SectorRoomLabel(threatRoomType),
                smallPixel, threatColor, TextAnchor.MiddleLeft);
            var rtt = coopSimulation == null ? 0ul : coopSimulation.RoundTripTimeMilliseconds;
            var networkLabel = soloExpeditionPlaying ? "SOLO // #" + seed.ToString("X") :
                coopLocalPreview ? "LOCAL QA" :
                (coopSimulation != null && coopSimulation.IsNetworkReady
                    ? (multiplayerSessions != null && multiplayerSessions.IsHost ? "HOST" : "GUEST PREDICT") +
                      " // " + rtt + " MS"
                    : "RECONNECT");
            var networkColor = coopLocalPreview || rtt <= 120 ? new Color(.35f, 1f, .68f) :
                rtt <= 220 ? new Color(1f, .82f, .28f) : new Color(1f, .36f, .42f);
            PixelUi.DrawText(new Rect(header.x + header.width * .55f, header.y + header.height * .04f, header.width * .42f, header.height * .42f),
                networkLabel, Mathf.Max(3, smallPixel - 1), networkColor, TextAnchor.MiddleRight);
            PixelUi.DrawText(new Rect(header.x + 10f, header.y + header.height * .50f,
                    header.width * (soloExpeditionPlaying ? .94f : .44f), header.height * .38f),
                hostName + " // " + ShipLoadoutSettings.Title(CoopHostShip()), Mathf.Max(3, smallPixel - 1),
                ShipLoadoutSettings.Get(CoopHostShip()).ProjectileColor, TextAnchor.MiddleLeft);
            if (!soloExpeditionPlaying)
                PixelUi.DrawText(new Rect(header.x + header.width * .50f, header.y + header.height * .50f, header.width * .47f, header.height * .38f),
                    guestName + " // " + ShipLoadoutSettings.Title(CoopGuestShip()), Mathf.Max(3, smallPixel - 1),
                    ShipLoadoutSettings.Get(CoopGuestShip()).ProjectileColor, TextAnchor.MiddleRight);

            DrawSectorMap(new Rect(left + width * .045f, top + height * .122f, width * .91f, height * .052f),
                layout, roomIndex);

            var roomReward = CoopRoomRules.RewardAmount(threatRoomType);
            PixelUi.DrawText(new Rect(left + width * .07f, top + height * .18f, width * .86f, height * .027f),
                CoopRoomRules.ObjectiveLabel(threatRoomType) + (roomReward > 0 ? " // +" + roomReward : string.Empty) +
                "  ·  " + CoopRoomRules.ModifierLabel(threatRoomType), Mathf.Max(3, smallPixel - 2), pale, TextAnchor.MiddleCenter);

            var roomDamage = CoopRoomRules.ThreatDamage(threatRoomType);
            PixelUi.DrawText(new Rect(left + width * .08f, top + height * .307f, width * .84f, height * .028f),
                CoopRoomRules.DangerDescription(threatRoomType), Mathf.Max(3, smallPixel - 2),
                roomDamage > 0 ? new Color(1f, .52f, .58f) : new Color(.48f, 1f, .76f), TextAnchor.MiddleCenter);

            PixelUi.DrawText(new Rect(left + width * .045f, top + height * .213f, width * .43f, height * .025f),
                "УГРОЗА " + threatHealth + "/" + Mathf.Max(1, threatMaxHealth), Mathf.Max(3, smallPixel - 1), threatColor, TextAnchor.MiddleLeft);
            PixelUi.DrawText(new Rect(left + width * .525f, top + height * .213f, width * .43f, height * .025f),
                (soloExpeditionPlaying ? "КОРПУС " : "КОМАНДА ") + teamHealth + "/" + Mathf.Max(1, teamMaxHealth),
                Mathf.Max(3, smallPixel - 1), teamColor, TextAnchor.MiddleRight);
            PixelUi.DrawSegmentBar(new Rect(left + width * .045f, top + height * .241f, width * .43f, height * .021f),
                threatHealth, Mathf.Max(1, threatMaxHealth), threatColor, new Color(.08f, .12f, .20f, .8f), threatColor);
            PixelUi.DrawSegmentBar(new Rect(left + width * .525f, top + height * .241f, width * .43f, height * .021f),
                teamHealth, Mathf.Max(1, teamMaxHealth), teamColor, new Color(.08f, .12f, .20f, .8f), teamColor);

            var resonance = coopLocalPreview ? coopPreviewResonance : (coopSimulation == null ? ElementalReaction.None : coopSimulation.CoopResonance);
            var resonanceTimer = coopLocalPreview ? coopPreviewResonanceTimer : (coopSimulation == null ? 0f : coopSimulation.CoopResonanceTimer);
            var resonanceColor = resonanceTimer > 0f ? new Color(1f, .82f, .32f) : new Color(.48f, .58f, .72f);
            var relayActive = coopLocalPreview ? coopPreviewRelayCoreActive : (coopSimulation != null && coopSimulation.RelayCoreActive);
            var relayCharge = coopLocalPreview ? coopPreviewRelayCoreCharge : (coopSimulation == null ? (byte)0 : coopSimulation.RelayCoreCharge);
            var relayElement = coopLocalPreview ? coopPreviewRelayCoreElement : (coopSimulation == null ? DamageElement.Kinetic : coopSimulation.RelayCoreElement);
            var relayDangerous = coopLocalPreview ? coopPreviewRelayCoreDangerous : (coopSimulation != null && coopSimulation.RelayCoreDangerous);
            var tetherActive = !soloExpeditionPlaying && (coopLocalPreview ? coopPreviewTetherActive : coopSimulation != null && coopSimulation.TetherActive);
            var tetherHeat = coopLocalPreview ? coopPreviewTetherHeat : (coopSimulation == null ? 0f : coopSimulation.TetherHeat);
            var tetherOverload = !soloExpeditionPlaying && (coopLocalPreview
                ? coopPreviewTetherOverloadTimer > 0f
                : coopSimulation != null && coopSimulation.TetherOverloadTimer > 0f);
            var redirectCount = coopLocalPreview ? coopPreviewRedirectSequence :
                (coopSimulation == null ? 0u : coopSimulation.FriendlyRedirectSequence);

            var trajectoryTime = coopLocalPreview ? coopPreviewTrajectoryTime : (coopSimulation == null ? 0f : coopSimulation.TrajectoryTimeSeconds);
            var trajectoryState = CoopTrajectorySettings.Evaluate(trajectoryTime);
            if (!runCompleted && !runFailed)
            {
                var trajectoryColor = trajectoryState.IsTransitioning
                    ? Color.Lerp(new Color(.20f, .90f, 1f), new Color(.92f, .36f, 1f), trajectoryState.Blend)
                    : trajectoryState.SecondsUntilTransition <= 3f
                        ? new Color(1f, .82f, .28f)
                        : new Color(.46f, .82f, 1f);
                var trajectoryLabel = trajectoryState.IsTransitioning
                    ? "МОРФ // " + CoopTrajectorySettings.Label(trajectoryState.From) + " > " + CoopTrajectorySettings.Label(trajectoryState.To) +
                      " // " + Mathf.RoundToInt(trajectoryState.Blend * 100f) + "%"
                    : "ТРАЕКТОРИЯ // " + CoopTrajectorySettings.Label(trajectoryState.From) + " // СМЕНА " +
                      Mathf.CeilToInt(trajectoryState.SecondsUntilTransition) + " СЕК";
                PixelUi.DrawText(new Rect(left + width * .14f, top + height * .275f, width * .72f, height * .029f),
                    trajectoryLabel, Mathf.Max(3, smallPixel - 1), trajectoryColor, TextAnchor.MiddleCenter);
            }

            if (relayActive && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .16f, top + height * .342f, width * .68f, height * .030f),
                    relayDangerous ? "ЯДРО // ОПАСНАЯ ОБРАТКА" :
                    "ЯДРО // " + relayCharge + "/" + CoopRelayCoreRules.MaxCharge +
                    (relayCharge > 0 ? " // " + ElementalCombat.ShortName(relayElement) : " // ТОЛКНИ ЕГО В УГРОЗУ"),
                    Mathf.Max(3, smallPixel - 1), relayDangerous ? new Color(1f, .28f, .66f) :
                    (relayCharge > 0 ? CoopElementColor(relayElement) : new Color(.48f, .90f, 1f)), TextAnchor.MiddleCenter);

            if ((tetherActive || tetherOverload) && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .18f, top + height * .377f, width * .64f, height * .030f),
                    tetherOverload ? "СЦЕПКА // ПЕРЕГРУЗКА" :
                    "СЦЕПКА // НАГРЕВ " + Mathf.RoundToInt(Mathf.Clamp01(tetherHeat) * 100f) + "%" +
                    (redirectCount > 0 ? " // РИКОШЕТЫ " + redirectCount : string.Empty),
                    Mathf.Max(3, smallPixel - 1), tetherOverload ? new Color(1f, .34f, .62f) :
                    Color.Lerp(new Color(.30f, .92f, 1f), new Color(1f, .64f, .24f), tetherHeat), TextAnchor.MiddleCenter);

            if (resonanceTimer > 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .18f, top + height * .412f, width * .64f, height * .032f),
                    "РЕЗОНАНС // " + ElementalCombat.ReactionLabel(resonance), smallPixel, resonanceColor, TextAnchor.MiddleCenter);

            var pulseTimer = coopLocalPreview ? coopPreviewThreatPulseTimer : (coopSimulation == null ? 0f : coopSimulation.CoopThreatPulseTimer);
            if (pulseTimer > 0f && !runCompleted && !runFailed)
            {
                var pulseElement = coopLocalPreview ? coopPreviewThreatPulseElement : coopSimulation.CoopThreatPulseElement;
                var pulsePattern = coopLocalPreview ? coopPreviewThreatPattern : coopSimulation.CoopThreatPattern;
                var pulseTarget = coopLocalPreview ? coopPreviewThreatTargetsHost : coopSimulation.CoopThreatTargetsHost;
                PixelUi.DrawText(new Rect(left + width * .18f, top + height * .447f, width * .64f, height * .032f),
                    CoopThreatAttackRules.Label(pulsePattern) + " // " + (pulseTarget ? "P1" : "P2") +
                    " // " + ElementalCombat.ShortName(pulseElement),
                    smallPixel, CoopElementColor(pulseElement), TextAnchor.MiddleCenter);
            }

            if (coopHullHitBannerTimer > 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .18f, top + height * .482f, width * .64f, height * .034f),
                    "ПОПАДАНИЕ // -" + coopLastHullDamage + " КОРПУС",
                    smallPixel, new Color(1f, .34f, .42f), TextAnchor.MiddleCenter);

            if (coopCollisionBannerTimer > 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .17f, top + height * .517f, width * .66f, height * .034f),
                    "БАМ! // КОРАБЛИ ОТСКОЧИЛИ", smallPixel, new Color(.72f, .94f, 1f), TextAnchor.MiddleCenter);

            if (coopRelayCoreBannerTimer > 0f && coopTetherBannerTimer <= 0f && coopRedirectBannerTimer <= 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .14f, top + height * .522f, width * .72f, height * .038f),
                    coopRelayCoreBanner, smallPixel,
                    coopRelayCoreBanner.Contains("-1") ? new Color(1f, .32f, .40f) : new Color(.56f, .95f, 1f),
                    TextAnchor.MiddleCenter);

            if (coopTetherBannerTimer > 0f && coopRedirectBannerTimer <= 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .12f, top + height * .522f, width * .76f, height * .038f),
                    coopTetherBanner, Mathf.Max(3, smallPixel - 1),
                    coopTetherBanner.Contains("ОБРАТКА") ? new Color(1f, .30f, .44f) : new Color(.48f, .94f, 1f),
                    TextAnchor.MiddleCenter);

            if (coopRedirectBannerTimer > 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .10f, top + height * .522f, width * .80f, height * .038f),
                    coopRedirectBanner, Mathf.Max(3, smallPixel - 1),
                    coopRedirectBanner.Contains("ПИНБОЛ") ? new Color(1f, .64f, .30f) : new Color(.52f, 1f, .82f),
                    TextAnchor.MiddleCenter);

            if (coopThreatDefeatedBannerTimer > 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .12f, top + height * .445f, width * .76f, height * .045f),
                    "УГРОЗА УНИЧТОЖЕНА // ПЕРЕХОД", pixel, new Color(.52f, 1f, .74f), TextAnchor.MiddleCenter);

            if (coopRoomIntroTimer > 0f && !runCompleted && !runFailed)
            {
                var intro = new Rect(left + width * .16f, top + height * .48f, width * .68f, height * .145f);
                var introAlpha = Mathf.Clamp01(coopRoomIntroTimer / .35f);
                var introAccent = SectorRoomColor(threatRoomType);
                introAccent.a = introAlpha;
                PixelUi.DrawPanel(intro, new Color(.012f, .026f, .075f, .92f * introAlpha), introAccent, 3f);
                PixelUi.DrawText(new Rect(intro.x + 10f, intro.y + intro.height * .08f, intro.width - 20f, intro.height * .28f),
                    "КОМНАТА " + (roomIndex + 1).ToString("00") + " // " + SectorRoomLabel(threatRoomType),
                    pixel, introAccent, TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(intro.x + 10f, intro.y + intro.height * .40f, intro.width - 20f, intro.height * .22f),
                    CoopRoomRules.DangerDescription(threatRoomType), Mathf.Max(3, smallPixel - 1), Color.white, TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(intro.x + 10f, intro.y + intro.height * .66f, intro.width - 20f, intro.height * .20f),
                    roomDamage > 0 ? "ЦЕЛЬ В ЦЕНТРЕ // 4 СЕК ЗАЩИТЫ" : CoopRoomRules.ObjectiveLabel(threatRoomType),
                    Mathf.Max(3, smallPixel - 2), pale, TextAnchor.MiddleCenter);
            }

            if (runCompleted || runFailed)
            {
                var completionPanel = new Rect(left + width * .09f, top + height * .55f, width * .82f, height * .22f);
                var glow = .72f + Mathf.Sin(Time.unscaledTime * 5f) * .12f;
                var resultPanelColor = runFailed ? new Color(.24f, .035f, .08f, .97f) : new Color(.05f, .20f, .16f, .96f);
                var resultAccent = runFailed ? new Color(1f, .25f, .32f, glow) : new Color(.35f, 1f, .68f, glow);
                PixelUi.DrawPanel(completionPanel, resultPanelColor, resultAccent, 4f);
                PixelUi.DrawText(new Rect(completionPanel.x + 8f, completionPanel.y + completionPanel.height * .07f, completionPanel.width - 16f, completionPanel.height * .25f),
                    soloExpeditionPlaying
                        ? (runFailed ? "ЭКСПЕДИЦИЯ ПОТЕРЯНА" : "ЭКСПЕДИЦИЯ ПРОЙДЕНА")
                        : (runFailed ? "СЕКТОР ПОТЕРЯН" : "СЕКТОР ОЧИЩЕН"),
                    Mathf.RoundToInt(pixel * 1.25f), Color.white, TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(completionPanel.x + 8f, completionPanel.y + completionPanel.height * .32f, completionPanel.width - 16f, completionPanel.height * .19f),
                    runFailed ? "КОРПУС РАЗРУШЕН // ЗАБЕГ ОКОНЧЕН" : "БОСС ПОБЕЖДЕН // ЗАБЕГ ЗАВЕРШЕН", smallPixel,
                    runFailed ? new Color(1f, .52f, .58f) : new Color(.55f, 1f, .76f), TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(completionPanel.x + 8f, completionPanel.y + completionPanel.height * .52f, completionPanel.width - 16f, completionPanel.height * .18f),
                    "ОБЩИЙ РЕЗУЛЬТАТ // " + coopResultScore, smallPixel, Color.white, TextAnchor.MiddleCenter);
                var resultMmrColor = coopResultMmrDelta >= 0 ? new Color(.35f, 1f, .58f) : new Color(1f, .38f, .46f);
                PixelUi.DrawText(new Rect(completionPanel.x + 8f, completionPanel.y + completionPanel.height * .72f, completionPanel.width - 16f, completionPanel.height * .18f),
                    "MMR " + (coopResultMmrDelta >= 0 ? "+" : string.Empty) + coopResultMmrDelta + "  //  " + mmr,
                    smallPixel, resultMmrColor, TextAnchor.MiddleCenter);
            }

            if (soloExpeditionPlaying && (runCompleted || runFailed))
            {
                if (DrawPixelButton(new Rect(left + width * .25f, top + height * .775f, width * .23f, height * .055f), "ЕЩЕ РАЗ", smallPixel,
                        new Color(.05f, .18f, .20f, .94f), cyan, Color.white))
                    BeginSoloExpedition();
                if (DrawPixelButton(new Rect(left + width * .52f, top + height * .775f, width * .23f, height * .055f), "МЕНЮ", smallPixel,
                        new Color(.13f, .035f, .09f, .90f), new Color(1f, .32f, .45f), Color.white))
                    ExitCoopRun();
            }
            else if (DrawPixelButton(new Rect(left + width * .39f, top + height * ((runCompleted || runFailed) ? .775f : .735f), width * .22f, height * .055f),
                         (runCompleted || runFailed) ? "МЕНЮ" : "ВЫХОД", smallPixel,
                         new Color(.13f, .035f, .09f, .90f), new Color(1f, .32f, .45f), Color.white))
                ExitCoopRun();
            DrawUiFade(left, top, width, height);
        }

        private static void DrawSectorMap(Rect rect, SectorLayout layout, int activeRoom)
        {
            PixelUi.DrawPanel(rect, new Color(.018f, .055f, .13f, .92f), new Color(.28f, .75f, 1f, .9f), 3f);
            if (layout == null || layout.Rooms == null || layout.Rooms.Count == 0) return;
            var count = layout.Rooms.Count;
            var step = rect.width / count;
            var nodeSize = Mathf.Clamp(Mathf.Min(step * .58f, rect.height * .58f), 4f, 12f);
            var y = rect.y + (rect.height - nodeSize) * .5f;
            for (var i = 0; i < count - 1; i++)
                PixelUi.DrawPanel(new Rect(rect.x + step * (i + .72f), y + nodeSize * .36f, step * .56f, Mathf.Max(2f, nodeSize * .16f)),
                    i < activeRoom ? new Color(.25f, .85f, .7f, .72f) : new Color(.20f, .35f, .55f, .55f), Color.clear, 0f);
            for (var i = 0; i < count; i++)
            {
                var room = layout.Rooms[i];
                var color = SectorRoomColor(room.Type);
                if (i == activeRoom) color = Color.white;
                var node = new Rect(rect.x + step * i + (step - nodeSize) * .5f, y, nodeSize, nodeSize);
                PixelUi.DrawPanel(node, i == activeRoom ? new Color(.12f, .60f, .78f, .95f) : new Color(color.r, color.g, color.b, .68f), color, i == activeRoom ? 2f : 1f);
            }
        }

        private static Color SectorRoomColor(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Start: return new Color(.35f, 1f, .68f);
                case SectorRoomType.Combat: return new Color(.30f, .70f, 1f);
                case SectorRoomType.Elite: return new Color(1f, .45f, .76f);
                case SectorRoomType.Event: return new Color(1f, .78f, .30f);
                case SectorRoomType.Shop: return new Color(.65f, .48f, 1f);
                case SectorRoomType.Boss: return new Color(1f, .30f, .35f);
                default: return Color.white;
            }
        }

        private static Color CoopElementColor(DamageElement element)
        {
            switch (element)
            {
                case DamageElement.Fire: return new Color(1f, .40f, .16f);
                case DamageElement.Cold: return new Color(.40f, .86f, 1f);
                case DamageElement.Poison: return new Color(.42f, 1f, .48f);
                default: return new Color(.78f, .86f, 1f);
            }
        }

        private static string SectorRoomLabel(SectorRoomType type)
        {
            switch (type)
            {
                case SectorRoomType.Start: return "СТАРТ";
                case SectorRoomType.Combat: return "БОЙ";
                case SectorRoomType.Elite: return "ЭЛИТА";
                case SectorRoomType.Event: return "СОБЫТИЕ";
                case SectorRoomType.Shop: return "МАГАЗИН";
                case SectorRoomType.Boss: return "БОСС";
                default: return "СЕКТОР";
            }
        }

        private void DrawSettingsScreen(float left, float top, float width, float height, int pixel, int smallPixel, Color pale, Color panel, Color cyan, Color violet)
        {
            PixelUi.DrawPanel(new Rect(left, top, width, height), new Color(.004f, .008f, .035f, .97f), new Color(.12f, .25f, .48f, .65f), 2f);
            var settings = new Rect(left + width * .10f, top + height * .09f, width * .80f, height * .80f);
            PixelUi.DrawPanel(settings, new Color(.018f, .035f, .105f, .98f), violet, 4f);
            PixelUi.DrawText(new Rect(settings.x + 12f, settings.y + settings.height * .035f, settings.width - 24f, settings.height * .10f), "НАСТРОЙКИ", Mathf.RoundToInt(pixel * 1.35f), Color.white);
            PixelUi.DrawText(new Rect(settings.x + 12f, settings.y + settings.height * .15f, settings.width - 24f, settings.height * .06f), "ПОЗЫВНОЙ ПИЛОТА", smallPixel, pale);

            var nicknameRect = new Rect(settings.x + settings.width * .14f, settings.y + settings.height * .23f, settings.width * .72f, settings.height * .13f);
            PixelUi.DrawPanel(nicknameRect, panel, cyan, 3f);
            playerNickname = GUI.TextField(nicknameRect, playerNickname ?? string.Empty, 16, MakeCallsignInputStyle());
            if (!string.IsNullOrWhiteSpace(playerNickname)) nicknameError = string.Empty;
            PixelUi.DrawText(new Rect(nicknameRect.x + 10f, nicknameRect.y + 6f, nicknameRect.width - 20f, nicknameRect.height - 12f),
                string.IsNullOrEmpty(playerNickname) ? "ВВЕДИ ПОЗЫВНОЙ" : playerNickname, pixel,
                string.IsNullOrEmpty(playerNickname) ? new Color(.42f, .62f, .76f, .86f) : Color.white);
            if (!string.IsNullOrEmpty(nicknameError))
                PixelUi.DrawText(new Rect(settings.x + 12f, settings.y + settings.height * .37f, settings.width - 24f, settings.height * .07f), nicknameError, smallPixel, new Color(1f, .38f, .48f));

            var toggleY = settings.y + settings.height * .48f;
            var toggleWidth = settings.width * .32f;
            var toggleHeight = settings.height * .11f;
            if (DrawPixelButton(new Rect(settings.x + settings.width * .16f, toggleY, toggleWidth, toggleHeight), GameAudioSettings.MusicEnabled ? "МУЗЫКА: ВКЛ" : "МУЗЫКА: ВЫКЛ", smallPixel, panel, cyan, pale)) ToggleMusic();
            if (DrawPixelButton(new Rect(settings.x + settings.width * .52f, toggleY, toggleWidth, toggleHeight), GameAudioSettings.EffectsEnabled ? "SFX: ВКЛ" : "SFX: ВЫКЛ", smallPixel, panel, violet, pale)) ToggleEffects();
            if (DrawPixelButton(new Rect(settings.x + settings.width * .16f, settings.y + settings.height * .63f, toggleWidth, toggleHeight), HapticFeedback.Enabled ? "ВИБРО: ВКЛ" : "ВИБРО: ВЫКЛ", smallPixel, panel, cyan, pale)) HapticFeedback.Toggle();
            if (DrawPixelButton(new Rect(settings.x + settings.width * .52f, settings.y + settings.height * .63f, toggleWidth, toggleHeight), GameVisualSettings.ScreenShakeEnabled ? "ТРЯСКА: ВКЛ" : "ТРЯСКА: ВЫКЛ", smallPixel, panel, violet, pale)) GameVisualSettings.ToggleScreenShake();

            PixelUi.DrawText(new Rect(settings.x + 20f, settings.y + settings.height * .76f, settings.width - 40f, settings.height * .05f), "НАСТРОЙКИ СОХРАНЯЮТСЯ НА УСТРОЙСТВЕ", smallPixel, new Color(.55f, .72f, .9f));
            if (DrawPixelButton(new Rect(settings.x + settings.width * .28f, settings.y + settings.height * .84f, settings.width * .44f, settings.height * .10f), "ГОТОВО", smallPixel, new Color(.07f, .13f, .30f, .98f), cyan, Color.white)) CloseSettings();
        }

        private void OnGUI()
        {
            var safe = Screen.safeArea;
            var top = Screen.height - safe.yMax;
            var left = safe.x;
            var width = safe.width;
            var height = safe.height;
            // Масштаб учитывает и портретный телефон, и широкое окно Game View на ПК.
            // На горизонтальном экране шрифт не раздувается до размеров панели.
            var scale = Mathf.Clamp(Mathf.Min(width / 940f, height / 1420f), .58f, 1.15f);
            var cyan = new Color(.42f, .96f, 1f, 1f);
            var violet = new Color(.92f, .48f, 1f, 1f);
            var pale = new Color(.82f, .93f, 1f, 1f);
            var panel = new Color(.015f, .04f, .12f, .9f);
            var pixel = Mathf.RoundToInt(8f * scale);
            var smallPixel = Mathf.RoundToInt(5f * scale);

            if (showCoop)
            {
                DrawCoopScreen(left, top, width, height, pixel, smallPixel, pale, panel, cyan, violet);
                DrawUiFade(left, top, width, height);
                return;
            }

            if (showSettings)
            {
                DrawSettingsScreen(left, top, width, height, pixel, smallPixel, pale, panel, cyan, violet);
                DrawUiFade(left, top, width, height);
                return;
            }

            if (coopPlaying)
            {
                DrawCoopHud(left, top, width, height, pixel, smallPixel, pale, panel, cyan, violet);
                return;
            }

            if (showRoomGuide)
            {
                DrawRoomGuide(left, top, width, height, pixel, smallPixel, pale, panel, cyan);
                DrawUiFade(left, top, width, height);
                return;
            }

            if (showMenu)
            {
                var header = new Rect(left + width * .05f, top + height * .045f, width * .90f, height * .18f);
                PixelUi.DrawPanel(header, new Color(.025f, .075f, .17f, .94f), cyan, 4f);
                PixelUi.DrawText(new Rect(header.x + 16f, header.y + header.height * .10f, header.width - 32f, header.height * .52f), "ORBITAL RIFT", Mathf.RoundToInt(12f * scale), cyan);
                PixelUi.DrawText(new Rect(header.x + 16f, header.y + header.height * .69f, header.width - 32f, header.height * .18f), "SECTOR 07 // ORBITAL DEFENSE", smallPixel, pale);

                var contentTop = top + height * .265f;
                var contentHeight = height * .60f;
                var gap = width * .035f;
                var controlsRect = new Rect(left + width * .05f, contentTop, width * .47f, contentHeight);
                var statsRect = new Rect(left + width * .05f + width * .47f + gap, contentTop, width * .43f, contentHeight);
                PixelUi.DrawPanel(controlsRect, new Color(.018f, .05f, .13f, .94f), new Color(.17f, .68f, 1f, .78f), 4f);
                PixelUi.DrawPanel(statsRect, new Color(.03f, .025f, .12f, .94f), new Color(.67f, .36f, 1f, .78f), 4f);

                var greeting = string.IsNullOrEmpty(playerNickname) ? "ДОБРО ПОЖАЛОВАТЬ,\nПИЛОТ" : "С ВОЗВРАЩЕНИЕМ,\n" + playerNickname;
                PixelUi.DrawText(new Rect(controlsRect.x + 16f, controlsRect.y + controlsRect.height * .10f, controlsRect.width - 32f, controlsRect.height * .25f), greeting, pixel, Color.white);
                PixelUi.DrawText(new Rect(controlsRect.x + 16f, controlsRect.y + controlsRect.height * .32f, controlsRect.width - 32f, controlsRect.height * .07f), "КЛАСС // " + RankTitle(mmr), smallPixel, RankColor(mmr));

                if (DrawPixelButton(new Rect(controlsRect.x + controlsRect.width * .10f, controlsRect.y + controlsRect.height * .42f,
                        controlsRect.width * .80f, controlsRect.height * .105f), "СОЛО // КЛАССИКА\nКРУГ · ВОЛНЫ · БОСС", smallPixel,
                        new Color(.20f, .045f, .36f, .98f), violet, Color.white))
                    StartGame();
                if (DrawPixelButton(new Rect(controlsRect.x + controlsRect.width * .10f, controlsRect.y + controlsRect.height * .545f,
                        controlsRect.width * .80f, controlsRect.height * .105f), "СОЛО // ЭКСПЕДИЦИЯ\n14 КОМНАТ · ОБЩИЙ КОРПУС", smallPixel,
                        new Color(.035f, .16f, .20f, .98f), new Color(.28f, 1f, .72f), Color.white))
                    BeginSoloExpedition();
                if (DrawPixelButton(new Rect(controlsRect.x + controlsRect.width * .16f, controlsRect.y + controlsRect.height * .67f,
                        controlsRect.width * .68f, controlsRect.height * .10f), "КООП // 2 ИГРОКА\nОБЩИЙ КОРПУС · СЦЕПКА", smallPixel,
                        new Color(.06f, .11f, .27f, .98f), cyan, Color.white))
                {
                    showCoop = true;
                    BeginUiFade();
                }
                if (DrawPixelButton(new Rect(controlsRect.x + controlsRect.width * .22f, controlsRect.y + controlsRect.height * .795f,
                        controlsRect.width * .56f, controlsRect.height * .085f), "НАСТРОЙКИ", smallPixel, panel, violet, pale))
                {
                    nicknameError = string.Empty;
                    showSettings = true;
                    BeginUiFade();
                }
                PixelUi.DrawText(new Rect(controlsRect.x + 14f, controlsRect.y + controlsRect.height * .89f, controlsRect.width - 28f, controlsRect.height * .055f), "ПОЗЫВНОЙ И ЗВУК — В НАСТРОЙКАХ", smallPixel, new Color(.55f, .72f, .9f));

                var bestRect = new Rect(statsRect.x + statsRect.width * .10f, statsRect.y + statsRect.height * .08f, statsRect.width * .80f, statsRect.height * .14f);
                PixelUi.DrawPanel(bestRect, new Color(.02f, .11f, .16f, .96f), new Color(.2f, .8f, 1f, .72f), 3f);
                PixelUi.DrawText(bestRect, "ЛУЧШИЙ СИГНАЛ\n" + bestScore, pixel, cyan);

                var mmrRect = new Rect(statsRect.x + statsRect.width * .10f, statsRect.y + statsRect.height * .27f, statsRect.width * .80f, statsRect.height * .14f);
                var currentRankColor = RankColor(mmr);
                PixelUi.DrawPanel(mmrRect, new Color(.025f, .13f, .08f, .96f), currentRankColor, 3f);
                var currentBadge = new Rect(mmrRect.x + mmrRect.width * .06f, mmrRect.y + mmrRect.height * .19f, mmrRect.height * .62f, mmrRect.height * .62f);
                DrawRankIcon(currentBadge, mmr);
                if (GUI.Button(currentBadge, GUIContent.none, GUIStyle.none)) showRankGuide = true;
                var rankTextX = currentBadge.xMax + mmrRect.width * .035f;
                PixelUi.DrawText(new Rect(rankTextX, mmrRect.y, mmrRect.xMax - rankTextX - mmrRect.width * .04f, mmrRect.height), "MMR " + mmr + "\n" + RankTitle(mmr), smallPixel, currentRankColor, TextAnchor.MiddleLeft);

                var leaderboardRect = new Rect(statsRect.x + statsRect.width * .10f, statsRect.y + statsRect.height * .47f, statsRect.width * .80f, statsRect.height * .38f);
                var leaderboardGap = leaderboardRect.width * .04f;
                var scoreTopRect = new Rect(leaderboardRect.x, leaderboardRect.y, (leaderboardRect.width - leaderboardGap) * .5f, leaderboardRect.height);
                var mmrTopRect = new Rect(scoreTopRect.xMax + leaderboardGap, leaderboardRect.y, scoreTopRect.width, leaderboardRect.height);
                PixelUi.DrawPanel(scoreTopRect, panel, new Color(.2f, .8f, 1f, .72f), 3f);
                PixelUi.DrawPanel(mmrTopRect, panel, new Color(.6f, .38f, 1f, .72f), 3f);
                DrawLeaderboardColumn(scoreTopRect, "TOP RECORD", scoreLeaderboardEntries, false, smallPixel, cyan);
                DrawLeaderboardColumn(mmrTopRect, "TOP MMR", mmrLeaderboardEntries, true, smallPixel, pale);
                var firebaseLabel = firebaseConnectionState == FirebaseConnectionState.Online
                    ? "FIREBASE // ONLINE"
                    : firebaseConnectionState == FirebaseConnectionState.Connecting
                        ? "FIREBASE // SYNC"
                        : "FIREBASE // OFFLINE";
                var firebaseColor = firebaseConnectionState == FirebaseConnectionState.Online
                    ? new Color(.35f, 1f, .68f, .9f)
                    : firebaseConnectionState == FirebaseConnectionState.Connecting
                        ? new Color(1f, .82f, .32f, .9f)
                        : new Color(1f, .36f, .42f, .9f);
                PixelUi.DrawText(new Rect(statsRect.x + 14f, statsRect.y + statsRect.height * .90f, statsRect.width - 28f, statsRect.height * .07f), firebaseLabel, smallPixel, firebaseColor);
                if (DrawPixelButton(new Rect(left + width * .28f, top + height * .875f, width * .44f, height * .052f),
                        "ОПИСАНИЕ РЕЖИМОВ И КОМНАТ", smallPixel, new Color(.025f, .08f, .16f, .96f), cyan, pale))
                {
                    showRoomGuide = true;
                    BeginUiFade();
                }
                if (showRankGuide) DrawRankGuide(left, top, width, height, pixel, smallPixel, pale, panel, cyan);
                DrawUiFade(left, top, width, height);
                return;
            }

            if (playing)
            {
                var scoreRect = new Rect(left + width * .04f, top + height * .025f, width * .44f, height * .102f);
                var stateRect = new Rect(left + width * .52f, top + height * .025f, width * .44f, height * .14f);
                PixelUi.DrawPanel(scoreRect, panel, new Color(.15f, .72f, 1f, .75f), 3f);
                PixelUi.DrawPanel(stateRect, panel, new Color(.65f, .35f, 1f, .75f), 3f);
                PixelUi.DrawText(scoreRect, "СЧЕТ " + score + "\nФАЗА " + phase, pixel, cyan);
                var hpColor = hpFlashTimer > 0f ? Color.Lerp(new Color(.2f, 1f, .4f), Color.white, hpFlashTimer / .34f) : new Color(.2f, 1f, .4f);
                PixelUi.DrawText(new Rect(stateRect.x + 12f, stateRect.y + stateRect.height * .08f, stateRect.width * .18f, stateRect.height * .22f), "HP", smallPixel, hpColor, TextAnchor.MiddleLeft);
                PixelUi.DrawSegmentBar(new Rect(stateRect.x + stateRect.width * .21f, stateRect.y + stateRect.height * .08f, stateRect.width * .70f, stateRect.height * .22f), shields, 3, hpColor, new Color(.06f, .16f, .12f, .95f), hpColor);
                PixelUi.DrawText(new Rect(stateRect.x + 12f, stateRect.y + stateRect.height * .48f, stateRect.width * .22f, stateRect.height * .25f), "ЯДРА", smallPixel, pale, TextAnchor.MiddleLeft);
                var coreWidth = stateRect.width * .18f;
                for (var coreIndex = 0; coreIndex < 3; coreIndex++)
                    PixelUi.DrawCoreIcon(new Rect(stateRect.x + stateRect.width * (.48f + coreIndex * .17f), stateRect.y + stateRect.height * .43f, coreWidth, stateRect.height * .45f), coreIndex < cores, new Color(1f, .86f, .3f));
                PixelUi.DrawText(new Rect(stateRect.x + 12f, stateRect.y + stateRect.height * .78f, stateRect.width * .82f, stateRect.height * .18f), "ЩИТ  " + starShields + "/3", smallPixel, new Color(.68f, .92f, 1f), TextAnchor.MiddleLeft);

                var activeBoss = ActiveBoss();
                if (activeBoss != null)
                {
                    // Ниже баннера перехода, чтобы имя босса и его HP не накладывались.
                    var bossRect = new Rect(left + width * .18f, top + height * .29f, width * .64f, height * .055f);
                    var healthSegments = Mathf.CeilToInt(Mathf.Clamp01(activeBoss.Health / activeBoss.MaxHealth) * 16f);
                    PixelUi.DrawText(new Rect(bossRect.x, bossRect.y - bossRect.height * .42f, bossRect.width, bossRect.height * .42f), "СТРАЖ УРАНА", smallPixel, new Color(.92f, .54f, 1f), TextAnchor.MiddleCenter);
                    PixelUi.DrawSegmentBar(bossRect, healthSegments, 16, new Color(.82f, .2f, 1f), new Color(.12f, .035f, .18f, .95f), new Color(.92f, .54f, 1f));
                }

                if (splitShot || tripleShotTimer > 0f)
                {
                    var shotTimer = Mathf.Max(splitShot ? splitShotTimer : 0f, tripleShotTimer);
                    PixelUi.DrawText(new Rect(left, top + height * .14f, width, height * .04f), "TRIPLE SHOT  " + shotTimer.ToString("0.0"), smallPixel, new Color(1f, .86f, .3f));
                }
                if (coreActive) PixelUi.DrawText(new Rect(left, top + height * .185f, width, height * .04f), "ЭНЕРГО ЯДРО НА ОРБИТЕ", smallPixel, new Color(1f, .86f, .3f));
                // Compact pause control lives in the gap between the two HUD panels.
                // The old wide button overlapped the shield counter on tall phones.
                if (!paused && DrawPixelButton(new Rect(left + width * .466f, top + height * .028f, width * .068f, height * .040f), "II", smallPixel, new Color(.025f, .06f, .15f, .92f), cyan, pale))
                {
                    paused = true;
                    activeControlDirection = 0;
                }
                if (phaseUpgradeBannerTimer > 0f)
                {
                    var alpha = Mathf.Clamp01(phaseUpgradeBannerTimer / .45f);
                    var banner = new Rect(
                        left + width * .08f,
                        top + height * .17f,
                        width * .84f,
                        height * .10f
                    );

                    PixelUi.DrawText(
                        banner,
                        phaseUpgradeLabel,
                        pixel,
                        new Color(1f, 1f, 1f, alpha)
                    );
                }
                if (paused)
                {
                    var pauseRect = new Rect(left + width * .16f, top + height * .38f, width * .68f, height * .22f);
                    PixelUi.DrawPanel(pauseRect, new Color(.015f, .025f, .10f, .96f), violet, 4f);
                    PixelUi.DrawText(new Rect(pauseRect.x, pauseRect.y + pauseRect.height * .10f, pauseRect.width, pauseRect.height * .30f), "ПАУЗА", Mathf.RoundToInt(12f * scale), pale);
                    if (DrawPixelButton(new Rect(pauseRect.x + pauseRect.width * .12f, pauseRect.y + pauseRect.height * .57f, pauseRect.width * .76f, pauseRect.height * .25f), "ПРОДОЛЖИТЬ", smallPixel, new Color(.11f, .16f, .38f, .96f), cyan, Color.white)) paused = false;
                }
                DrawUiFade(left, top, width, height);
                return;
            }

            if (showResults)
            {
                var resultRect = new Rect(left + width * .12f, top + height * .22f, width * .76f, height * .52f);
                PixelUi.DrawPanel(resultRect, new Color(.08f, .015f, .16f, .94f), violet, 4f);
                PixelUi.DrawText(new Rect(resultRect.x, resultRect.y + resultRect.height * .09f, resultRect.width, resultRect.height * .16f), "СИГНАЛ ПОТЕРЯН", pixel, new Color(1f, .55f, .75f));
                PixelUi.DrawText(new Rect(resultRect.x, resultRect.y + resultRect.height * .29f, resultRect.width, resultRect.height * .29f), "СЧЕТ " + score + "\nРЕКОРД " + bestScore + "\nФАЗА " + phase + "\nMMR " + mmr, pixel, pale);

                var mmrColor = lastMmrDelta >= 0 ? new Color(.3f, 1f, .52f) : new Color(1f, .28f, .38f);
                var mmrDeltaText = (lastMmrDelta >= 0 ? "+" : string.Empty) + lastMmrDelta + " MMR";
                if (mmrResultTimer > 0f)
                {
                    var progress = 1f - mmrResultTimer / 2.25f;
                    // Быстрый "удар" о панель: надпись прилетает сверху, затем немного подпрыгивает.
                    var fall = Mathf.Clamp01(progress * 1.3f);
                    var bounce = Mathf.Sin(Mathf.Clamp01((progress - .58f) / .42f) * Mathf.PI) * height * .014f;
                    var animationY = Mathf.Lerp(top - height * .16f, resultRect.y + resultRect.height * .58f, fall) - bounce;
                    var alpha = Mathf.Clamp01(mmrResultTimer / .28f);
                    PixelUi.DrawText(new Rect(resultRect.x, animationY, resultRect.width, resultRect.height * .12f), mmrDeltaText, Mathf.RoundToInt(13f * scale), new Color(mmrColor.r, mmrColor.g, mmrColor.b, alpha));
                }
                else
                {
                    PixelUi.DrawText(new Rect(resultRect.x, resultRect.y + resultRect.height * .60f, resultRect.width, resultRect.height * .08f), mmrDeltaText, pixel, mmrColor);
                }

                if (DrawPixelButton(new Rect(resultRect.x + resultRect.width * .14f, resultRect.y + resultRect.height * .72f, resultRect.width * .72f, resultRect.height * .11f), "ЕЩЕ РАЗ", smallPixel, new Color(.15f, .06f, .34f, .96f), violet, Color.white)) StartGame();
                if (DrawPixelButton(new Rect(resultRect.x + resultRect.width * .14f, resultRect.y + resultRect.height * .86f, resultRect.width * .72f, resultRect.height * .11f), "МЕНЮ", smallPixel, new Color(.04f, .12f, .22f, .96f), cyan, Color.white)) { showResults = false; showMenu = true; showSettings = false; BeginUiFade(); }
            }
            DrawUiFade(left, top, width, height);
        }
    }
}
