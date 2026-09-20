using System.Collections.Generic;
using Guid = System.Guid;
using UnityEngine;
using OrbitalRift.UI;

namespace OrbitalRift
{
    internal enum ExpeditionUpgrade
    {
        RapidFire,
        PlasmaDrive,
        ReactorAmplifier,
        PrismSplit,
        FieldRepair,
        AegisPlating
    }

    [ExecuteAlways]
    public sealed partial class GameManager : MonoBehaviour
    {
        private readonly List<Enemy> enemies = new List<Enemy>(32);
        private readonly List<Projectile> projectiles = new List<Projectile>(128);
        private readonly List<StarParticle> stars = new List<StarParticle>(128);
        private readonly List<DamageShard> damageShards = new List<DamageShard>(16);
        private SpriteRenderer[] backgroundStars;
        private Vector2[] backgroundStarDirections;
        private float[] backgroundStarPhases, backgroundStarSpeeds, backgroundStarBrightnesses, backgroundStarSizes;
        private Color[] backgroundStarTints;
        private readonly List<LineRenderer> orbitRenderers = new List<LineRenderer>();
        private readonly List<Renderer> workshopHiddenRenderers = new List<Renderer>(256);
        private readonly List<MusicSpaceDistortion> workshopDisabledDistortions = new List<MusicSpaceDistortion>(4);
        private readonly Dictionary<Camera, int> workshopPreviousCameraMasks = new Dictionary<Camera, int>(4);
        private bool workshopCameraMaskApplied;
        private readonly List<CoopPlayerShotState> coopPreviewPlayerShots = new List<CoopPlayerShotState>(48);
        private readonly List<SpriteRenderer> coopThreatMineMarkers = new List<SpriteRenderer>(3);
        private readonly List<SpriteRenderer> coopThreatCleaveNodes = new List<SpriteRenderer>(7);
        private readonly List<SpriteRenderer> defenseFlagshipRunningLights = new List<SpriteRenderer>(5);
        private readonly List<SpriteRenderer> defenseFlagshipDamageMarkers = new List<SpriteRenderer>(3);
        private readonly int[] defenseFlagshipSectionDamage = new int[3];
        private readonly float[] defenseFlagshipSectionFlash = new float[3];
        private ObjectPool<Enemy> enemyPool;
        private ObjectPool<Projectile> projectilePool;
        [SerializeField] private SpellProjectileVfx playerSpellPrefab;
        private SpellVfxPool spellVfxPool;
        [SerializeField] private SpellProjectileVfx solarChickPrefab;
        [SerializeField] private ShieldVfxProfile starShieldVfxProfile;
        private ObjectPool<StarParticle> starPool;
        private ObjectPool<DamageShard> damageShardPool;
        private Transform poolRoot;
        private Camera gameCamera;
        private Transform arena, player, core, splitPickup, menuEmblem, warpBadge, riftEcho, riftEchoGlow;
        private Transform spaceBackdrop;
        private RiftEchoPresentation riftEchoPresentation;
        private Transform coopGuest, coopHostMarker, coopGuestMarker, coopRelayCore, coopRelayCoreGlow;
        private Transform coopLensFirst, coopLensSecond, coopLensFirstGlow, coopLensSecondGlow,
            coopLensFirstPointer, coopLensSecondPointer;
        private LineRenderer coopTrajectoryRenderer, coopTetherRenderer, coopLensLink, coopLensTunnelOuter,
            coopLensTunnelInner, coopThreatCleaveRenderer,
            coopThreatCleaveCoreRenderer, coopThreatCleaveEchoRenderer, coopThreatRingLeftRenderer,
            coopThreatRingCenterRenderer, coopThreatRingRightRenderer;
        private TrailRenderer coopRelayCoreTrail, coopThreatCleaveTrail;
        private Transform coopRoomEnvironment;
        private SpriteRenderer coopRoomWash;
        private readonly List<SpriteRenderer> coopRoomMotifs = new List<SpriteRenderer>(18);
        private Sprite whiteSprite, circleSprite, shipSprite, flagshipSprite, projectileSprite, bonusSprite, orangeEnemySprite, pinkCanEnemySprite, bossSprite, voidMawBossSprite, firebirdBossSprite, harrierBossSprite, menuEmblemSprite, warpBadgeSprite;
        private Sprite firebirdChicksAbilitySprite, firebirdChickProjectileSprite, firebirdEggAbilitySprite, firebirdDiveAbilitySprite, harrierCopiesAbilitySprite, harrierDashAbilitySprite, harrierFanAbilitySprite, voidBeamAbilitySprite, voidRootsAbilitySprite, voidBarrageAbilitySprite;
        private Sprite solarLanceProjectileSprite, harrierShardProjectileSprite, voidPulseProjectileSprite;
        // Visual-only wormhole skins. Each source image is split into two mouths:
        // lens A is one mouth, lens B is the other, and the animated LineRenderers
        // between them show the shared tunnel.
        private Sprite[] pairedWormholeSprites, pairedWormholeFirstMouthSprites, pairedWormholeSecondMouthSprites;
        private int pairedWormholeVariant;
        private int appliedPairedWormholeVariant = -1;
        private static readonly Color[] PairedWormholeAccents =
        {
            new Color(.18f, .78f, 1f), new Color(.56f, .30f, 1f), new Color(.18f, 1f, .64f),
            new Color(1f, .48f, .10f), new Color(1f, .12f, .42f), new Color(.38f, .76f, 1f),
            new Color(.10f, .66f, 1f), new Color(1f, .20f, .06f), new Color(1f, .32f, .78f),
            new Color(.72f, .86f, 1f)
        };
        private Sprite navigatorRankSprite, guardianRankSprite, legendRankSprite, overlordRankSprite, divinityRankSprite;
        private AudioSource musicSource, effectsSource;
        private MusicReactiveVisualDirector musicReactiveVisuals;
        private AudioClip enemyDeathSound, playerDamageSound, coopBumpSound, coopTetherOverloadSound, coopRicochetSound;
        private float playerAngle = -Mathf.PI * .5f, targetAngle, fireTimer, spawnTimer, starTimer, invincible, coreAngle;
        private float riftEchoAngle, riftEchoTimer, riftEchoFireTimer, riftEchoCooldown, vectorSnapCooldown, playerRootTimer;
        private int score, bestScore, mmr, lastMmrDelta, shields = 3, phase = 1, cores, spawnsLeft;
        private ShipArchetype selectedShip;
        private bool playing, showMenu = true, showSettings, showCoop, showResults, autoFire = true, coreActive, splitShot, paused, bossSpawnPending, showRankGuide, showRoomGuide, firstBossMirrorBreakShown, bossMirrorActive;
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
        private OrbitalRiftCanvasRoot canvasUi;
        private readonly ExpeditionHudModel expeditionHudModel = new ExpeditionHudModel();
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
        private bool queuedRiftEcho, queuedVectorSnap;
        private bool workshopIsolationApplied;
        private readonly CombatMomentDirector combatMoments = new CombatMomentDirector();
        private readonly AbilitySandboxSession abilitySandbox = new AbilitySandboxSession();
        private Enemy sandboxBoss;
        private bool sandboxAutoFireBefore;
        private float sandboxPreviousTimeScale = 1f;
        private float pausePreviousTimeScale = 1f;
        private bool pauseTimeScaleApplied;
        private float sandboxVoidBeamTimer, sandboxVoidRootTimer, sandboxVoidBeamAngle, sandboxVoidRootAngle;
        private float sandboxBlackHoleTimer;
        private Vector2 sandboxBlackHoleCenter;
        private SandboxBlackHolePresentation sandboxBlackHolePresentation;
        private SandboxLayeredVfx sandboxLayeredVfx;
        private float shieldOrbitDirection = 1f;
        private float spaceTravelSpeed = 1f, backgroundTravelTime, jumpTimer;
        private bool wasSpaceCombat, wasSpaceRun;
        private float bossMirrorStartedAt;
        private int framedScreenWidth = -1, framedScreenHeight = -1;
        private bool coopPlaying, coopLocalPreview, soloExpeditionPlaying;
        private LivingCosmosRunState livingCosmos;
        private TempoRewardState livingTempo;
        private LivingCosmosCheckpointStore livingCheckpointStore;
        private float livingTempoMillisecondRemainder;
        private ExpeditionModeChoiceView expeditionModeChoice;
        private bool LivingCosmosActive => coopPlaying && soloExpeditionPlaying && livingCosmos != null;
        private bool LivingRouteChoice => LivingCosmosActive && livingCosmos.Phase == LivingEncounterPhase.RouteChoice;
        private bool LivingRewardChoice => LivingCosmosActive && livingCosmos.Phase == LivingEncounterPhase.Reward &&
                                            livingTempo != null && livingTempo.Pending != null;
        private bool LivingModalChoice => LivingRouteChoice || LivingRewardChoice;
        private bool LivingMapVisible => LivingCosmosActive && (paused || LivingRouteChoice || livingCosmos.Phase == LivingEncounterPhase.Departing);
        private bool defensePlaying, defenseRunOver;
        private Transform defenseFlagship, defenseFlagshipGlow;
        private int defenseHull, defenseMaxHull = 9, defenseWave, defenseSpawnsLeft, defenseKills;
        private float defenseSpawnTimer, defenseIntermissionTimer, defenseFlagshipPulse;
        private string defenseStatus = string.Empty;
        // The defense target occupies its own lower-screen bay: a broad
        // concave-up hull, like a protective smile below the main orbit.
        private static readonly Vector2 DefenseFlagshipPosition = new Vector2(0f, -4.55f);
        private static readonly Vector2 ShopFlagshipPosition = new Vector2(0f, 2.55f);
        private static float DefenseFlagshipHitRadius => GameRules.Current.DefenseFlagshipHitRadius;
        private static float DefenseFlagshipHalfWidth => GameRules.Current.DefenseFlagshipHalfWidth;
        private static float DefenseFlagshipWorldSize => GameRules.Current.DefenseFlagshipWorldSize;
        private const float ShopFlagshipWorldSize = 3.18f;
        private const float DefenseFlagshipGlowWorldSize = 1.42f;
        private const float ShopFlagshipGlowWorldSize = 1.38f;
        private const float ExpeditionCameraCenterY = 1.12f;
        private const float ExpeditionBottomWorldMargin = .48f;
        private static readonly Vector2[] DefenseFlagshipLightOffsets =
        {
            new Vector2(-2.58f, .80f), new Vector2(-1.32f, .10f), new Vector2(0f, -.66f),
            new Vector2(1.32f, .10f), new Vector2(2.58f, .80f)
        };
        private static readonly Vector2[] DefenseFlagshipSectionOffsets =
        {
            new Vector2(-1.90f, .40f), new Vector2(0f, -.23f), new Vector2(1.90f, .40f)
        };
        private const float ShopApproachDuration = 2.55f;
        private const float ShopClampDuration = 1.35f;
        private const float ShopDockSequenceDuration = ShopApproachDuration + ShopClampDuration;
        private bool expeditionShopDocking, expeditionShopOpen;
        private int expeditionShopRoomIndex = -1;
        private float expeditionShopDockTimer, expeditionShopFlightSparkTimer;
        private Vector2 expeditionShopDockOrigin;
        private float expeditionFireIntervalMultiplier = 1f, expeditionProjectileSpeedMultiplier = 1f;
        private int expeditionDamageBonus, expeditionPrismLevel, expeditionAegisCharges;
        private int expeditionFieldRepairLevel, expeditionAegisLevel;
        private string expeditionUpgradeNotice = string.Empty;
        private float expeditionUpgradeNoticeTimer;
        private float coopPreviewHostAngle = 210f, coopPreviewGuestAngle = 330f;
        private float coopPreviewTrajectoryTime;
        private float coopPreviewHostFireTimer, coopPreviewGuestFireTimer;
        private uint coopPreviewHostShots, coopPreviewGuestShots, lastCoopHostShots, lastCoopGuestShots;
        private SectorLayout coopPreviewSector;
        private int coopPreviewRunSeed = 27082026;
        private int coopPreviewRoomIndex;
        private float coopPreviewRoomTimer;
        private Transform coopEnemy;
        private float coopPreviewEnemyAngle = 90f, coopPreviewEnemyRadius;
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
        private bool coopPreviewLensesActive;
        private PairedLensPair coopPreviewLensPair;
        private PairedLensTransitState coopPreviewRelayLensState;
        private float coopLensSoundCooldown;
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
        private float coopThreatCleaveSparkTimer;
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
        private static float TouchOrbitSpeed => GameRules.Current.TouchOrbitSpeed;
        private const float UiFadeDuration = .28f;

        private void OnEnable()
        {
            if (!Application.isPlaying) CreateEditorPreview();
        }

        private void Start()
        {
            coopPreviewEnemyRadius = CoopTrajectorySettings.ThreatSpawnRadius;
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
            SetPaused(false);
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
            MusicReactiveSettings.Load();
            GameplayCameraZoomSettings.Load();
            HapticFeedback.Load();
            GameVisualSettings.Load();
            playerCommandSource = new LocalPlayerCommandSource();
            livingCheckpointStore = new LivingCosmosCheckpointStore();
            uiFadeTimer = .45f;
            firebaseScores = GetComponent<FirebaseScoreService>();
            multiplayerSessions = GetComponent<MultiplayerSessionController>();
            coopSimulation = GetComponent<CoopSimulationBridge>();
            canvasUi = FindFirstObjectByType<OrbitalRiftCanvasRoot>();
            BindCanvasUi();
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
            CreateSandboxSpellSprites();
            LoadPairedWormholeSprites();
            shipSprite = LoadResourceSprite("ship", 1024f);
            flagshipSprite = LoadResourceSprite("flagship_guardian_arc", 1024f) ??
                LoadResourceSprite("flagship_guardian_round", 1024f) ??
                LoadResourceSprite("flagship_guardian", 1024f);
            projectileSprite = LoadResourceSprite("projectile", 1024f);
            bonusSprite = LoadResourceSprite("bonus_pickup", 1024f);
            orangeEnemySprite = LoadResourceSprite("enemy_orange", 1024f);
            pinkCanEnemySprite = LoadResourceSprite("enemy_pink_can", 1024f);
            // The sentinel is deliberately a simple, large final silhouette:
            // it remains readable during an expedition and its visible edge
            // matches the forgiving boss hit radius.
            bossSprite = LoadResourceSprite("boss_final_sentinel", 1024f) ??
                LoadResourceSprite("boss_dreadnought", 1024f);
            // The classic phase-3 encounter has its own creature.  Keep the
            // sentinel sprite above for expedition/co-op room markers.
            voidMawBossSprite = LoadResourceSprite("boss_void_maw", 1024f);
            firebirdBossSprite = LoadResourceSprite("boss_astral_firebird", 1024f);
            harrierBossSprite = LoadResourceSprite("boss_umbral_harrier", 1024f);
            firebirdChicksAbilitySprite = LoadResourceSprite("BossAbilities/firebird_solar_chicks", 1024f);
            firebirdChickProjectileSprite = LoadResourceSprite("BossAbilities/firebird_solar_chick_projectile", 1024f);
            firebirdEggAbilitySprite = LoadResourceSprite("BossAbilities/firebird_ashen_egg", 1024f);
            firebirdDiveAbilitySprite = LoadResourceSprite("BossAbilities/firebird_phoenix_dive", 1024f);
            harrierCopiesAbilitySprite = LoadResourceSprite("BossAbilities/harrier_rift_copies", 1024f);
            harrierDashAbilitySprite = LoadResourceSprite("BossAbilities/harrier_phase_dash", 1024f);
            harrierFanAbilitySprite = LoadResourceSprite("BossAbilities/harrier_cold_fan", 1024f);
            voidBeamAbilitySprite = LoadResourceSprite("BossAbilities/void_rift_beam", 1024f);
            voidRootsAbilitySprite = LoadResourceSprite("BossAbilities/void_gravity_roots", 1024f);
            voidBarrageAbilitySprite = LoadResourceSprite("BossAbilities/void_barrage", 1024f);
            menuEmblemSprite = LoadResourceSprite("menu_emblem", 1024f);
            warpBadgeSprite = LoadResourceSprite("warp_badge", 1024f);
            navigatorRankSprite = LoadResourceSprite("Ranks/rank_navigator", 1024f);
            guardianRankSprite = LoadResourceSprite("Ranks/rank_guardian", 1024f);
            legendRankSprite = LoadResourceSprite("Ranks/rank_legend", 1024f);
            overlordRankSprite = LoadResourceSprite("Ranks/rank_overlord", 1024f);
            divinityRankSprite = LoadResourceSprite("Ranks/rank_divinity", 1024f);
            CreateAudio();
            CreateSpaceBackdrop();
            CreateMusicReactiveVisuals();
            // Restore a previously selected external-music session on entering Play mode. On
            // Android this also starts the API-9 Visualizer permission flow; the bridge itself
            // keeps capture opt-in and never opens the screen-share dialog on Android 9.
            if ((ExternalMusicAudioBridge.IsWindowsCaptureSupported || ExternalMusicAudioBridge.IsAndroidCaptureSupported) &&
                MusicReactiveSettings.Enabled && !GameAudioSettings.MusicEnabled)
                ExternalMusicAudioBridge.RequestCapture();
            arena = new GameObject("Arena").transform;
            CreateArena();
            CreatePools();
            CreatePlayer();
        }

        private void Update()
        {
            UpdateCameraFraming();
            if (!Application.isPlaying) return;
            var sandboxOpen = abilitySandbox != null && abilitySandbox.IsOpen;
            if (sandboxOpen)
                Time.timeScale = abilitySandbox.PreviewTimeScale;
            var dt = sandboxOpen
                ? Time.unscaledDeltaTime * abilitySandbox.PreviewTimeScale
                : Time.deltaTime;
            var visualDeltaTime = sandboxOpen ? dt : paused ? 0f : Time.unscaledDeltaTime;
            BindCourseInput();
            var command = playerCommandSource != null ? playerCommandSource.ReadFrame() : PlayerCommandFrame.None;
            if(spaceDepthPanel!=null&&spaceDepthPanel.BlocksInput)command=new PlayerCommandFrame(0,false,command.BackPressed);
            combatMoments.Tick(dt);
            hpFlashTimer = Mathf.Max(0f, hpFlashTimer - dt);
            enemyDeathSfxCooldown = Mathf.Max(0f, enemyDeathSfxCooldown - visualDeltaTime);
            mmrResultTimer = Mathf.Max(0f, mmrResultTimer - dt);
            uiFadeTimer = Mathf.Max(0f, uiFadeTimer - visualDeltaTime);
            UpdateSpaceTravel(paused || LivingModalChoice ? 0f : dt);
            TickDepthSpace(paused || LivingModalChoice ? 0f : dt,command);
            if (!paused && !LivingModalChoice) { UpdateStars(dt); UpdateDamageShards(dt); UpdateScreenShake(dt); }
            UpdatePresentation();
            if (!coopPlaying && coopSimulation != null && coopSimulation.RunStarted)
                BeginCoopRun(false);
            if (expeditionModeChoice != null && expeditionModeChoice.IsOpen)
            {
                if (command.BackPressed) expeditionModeChoice.Hide();
                return;
            }
            if (abilitySandbox.IsOpen)
            {
                UpdateAbilitySandbox(dt, command);
                return;
            }
            if (coopPlaying)
            {
                if (!coopLocalPreview && (multiplayerSessions == null || multiplayerSessions.CurrentSession == null || multiplayerSessions.PlayerCount < 2))
                {
                    FinishCoopRunToMenu();
                    return;
                }
                if (command.BackPressed && CanPauseCurrentRun())
                {
                    SetPaused(!paused);
                    activeControlDirection = 0;
                    return;
                }
                if (!paused) UpdateCoopRun(LivingModalChoice ? 0f : dt, LivingModalChoice ? 0 : command.OrbitDirection);
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
            if (playing && command.BackPressed && CanPauseCurrentRun())
            {
                SetPaused(!paused);
                activeControlDirection = 0;
                return;
            }
            if (defensePlaying)
            {
                if (!paused && !defenseRunOver)
                {
                    UpdateInput(dt, command.OrbitDirection);
                    UpdatePlayer(dt);
                    UpdateDefenseMode(dt);
                    UpdateProjectiles(dt);
                    if (command.ToggleAutoFire) autoFire = !autoFire;
                }
                return;
            }
            if (!playing || paused) return;
            UpdateOrbitalAbilityTimers(dt);
            UpdateInput(dt, command.OrbitDirection);
            if (command.UseRiftEcho || queuedRiftEcho)
            {
                queuedRiftEcho = false;
                TryUseRiftEcho();
            }
            if (command.UseVectorSnap || queuedVectorSnap)
            {
                queuedVectorSnap = false;
                TryUseVectorSnap();
            }
            UpdatePlayer(dt);
            UpdateRiftEcho(dt);
            UpdateSpawning(dt);
            UpdateEnemies(dt);
            UpdateProjectiles(dt);
            UpdateCore(dt);
            if (command.ToggleAutoFire) autoFire = !autoFire;
        }

        private void LateUpdate()
        {
            if (Application.isPlaying)
                spellVfxPool?.Tick(paused || LivingModalChoice ? 0f : Time.deltaTime);
            // A few pooled visual objects update after GameManager.Update and can re-enable
            // their renderer in the same frame. Re-apply the workshop mask after every other
            // component has ticked so the animation plate stays genuinely clean.
            if (Application.isPlaying && abilitySandbox != null && abilitySandbox.VfxEditorOpen)
            {
                SetAbilityVfxWorkshopIsolation(true);
                if (player != null) player.gameObject.SetActive(false);
            }
            if (gameCamera != null)
            {
                gameCamera.clearFlags = CameraClearFlags.Color;
                gameCamera.backgroundColor = backgroundColor;
            }
            if (Application.isPlaying) UpdateCanvasUi();
        }

        private void OnDestroy()
        {
            RestorePauseTimeScale();
            ResetSandboxMechanics();
            UnbindCanvasUi();
            ExternalMusicAudioBridge.StopCapture();
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
                if ((playing || coopPlaying || defensePlaying) && !abilitySandbox.IsOpen) SetPaused(true);
                activeControlDirection = 0;
                if (musicSource != null) musicSource.Pause();
                return;
            }
            ResumeMusicAfterBackground();
        }

        private void OnApplicationPause(bool backgrounded)
        {
            if (backgrounded)
            {
                if ((playing || coopPlaying || defensePlaying) && !abilitySandbox.IsOpen) SetPaused(true);
                activeControlDirection = 0;
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
            gameCamera.orthographic = true;
            gameCamera.clearFlags = CameraClearFlags.Color;
            gameCamera.backgroundColor = backgroundColor;
            gameCamera.transform.position = new Vector3(0,0,-10);
            gameCamera.tag = "MainCamera";
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
            if (defensePlaying)
            {
                halfWidthWithMargin = Mathf.Max(halfWidthWithMargin, DefenseFlagshipWorldSize * .5f + .45f);
                halfHeightWithMargin = Mathf.Max(halfHeightWithMargin, 6.1f);
            }
            if (coopPlaying)
            {
                var trajectoryTime = coopLocalPreview
                    ? coopPreviewTrajectoryTime
                    : coopSimulation == null ? CoopTrajectorySettings.InitialElapsedSeconds : coopSimulation.TrajectoryTimeSeconds;
                var trajectoryExtents = CoopTrajectorySettings.FramingExtents(trajectoryTime);
                halfWidthWithMargin = trajectoryExtents.x + CoopTrajectorySettings.CameraMargin;
                halfHeightWithMargin = trajectoryExtents.y + CoopTrajectorySettings.CameraMargin;
                if (soloExpeditionPlaying)
                    halfHeightWithMargin += ExpeditionCameraCenterY + ExpeditionBottomWorldMargin;
            }
            // На узком портретном экране размер берётся по ширине; на ПК сохраняется обычный масштаб.
            var targetSize = Mathf.Max(5.1f, halfHeightWithMargin, halfWidthWithMargin / aspect);
            // Scale only the world camera. The HUD remains screen-space, so buttons and labels
            // keep the same size while the complete arena moves farther away together.
            targetSize *= GameplayCameraZoomSettings.Value;
            gameCamera.orthographicSize = force
                ? targetSize
                : Mathf.Lerp(gameCamera.orthographicSize, targetSize,
                    1f - Mathf.Exp(-4f * Mathf.Max(0f,
                        abilitySandbox != null && abilitySandbox.IsOpen
                            ? Time.unscaledDeltaTime * abilitySandbox.PreviewTimeScale
                            : Time.unscaledDeltaTime)));
            gameCamera.transform.position = CameraBasePosition();
        }

        private Vector3 CameraBasePosition()
        {
            return new Vector3(0f, soloExpeditionPlaying ? ExpeditionCameraCenterY : 0f, -10f);
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
            CreateSandboxSpellSprites();
            LoadPairedWormholeSprites();
            shipSprite = LoadResourceSprite("ship", 1024f);
            flagshipSprite = LoadResourceSprite("flagship_guardian_arc", 1024f) ??
                LoadResourceSprite("flagship_guardian_round", 1024f) ??
                LoadResourceSprite("flagship_guardian", 1024f);
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

        private void CreateSandboxSpellSprites()
        {
            if (solarLanceProjectileSprite != null) return;
            solarLanceProjectileSprite = CreateSolarLanceProjectileSprite();
            harrierShardProjectileSprite = CreateHarrierShardProjectileSprite();
            voidPulseProjectileSprite = CreateVoidPulseProjectileSprite();
        }

        private static Sprite CreateSolarLanceProjectileSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Solar lance projectile" };
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var u = (x + .5f) / size * 2f - 1f;
                var v = (y + .5f) / size * 2f - 1f;
                var halfWidth = Mathf.Lerp(.05f, .38f, Mathf.Clamp01((u + 1f) * .5f));
                var body = Mathf.Clamp01(1f - Mathf.Abs(v) / halfWidth);
                var head = Mathf.Clamp01((1f - u) * 9f);
                var alpha = body * head;
                var heat = Mathf.Clamp01((u + .22f) * 1.25f);
                texture.SetPixel(x, y, new Color(1f, Mathf.Lerp(.16f, .93f, heat), Mathf.Lerp(.01f, .52f, heat), alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.32f, .5f), size);
        }

        private static Sprite CreateHarrierShardProjectileSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Harrier frost shard" };
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var u = (x + .5f) / size * 2f - 1f;
                var v = (y + .5f) / size * 2f - 1f;
                var taper = .09f + (1f - Mathf.Abs(v)) * .42f;
                var edge = Mathf.Clamp01(1f - Mathf.Abs(u) / taper);
                var pointed = Mathf.Clamp01(1f - Mathf.Abs(v) * 1.04f);
                var alpha = edge * pointed;
                var glint = Mathf.Clamp01(1f - Mathf.Abs(u + v * .38f) * 3.2f);
                texture.SetPixel(x, y, Color.Lerp(new Color(.20f, .40f, 1f, alpha), new Color(.88f, 1f, 1f, alpha), glint));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
        }

        private static Sprite CreateVoidPulseProjectileSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Void pulse projectile" };
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var u = (x + .5f) / size * 2f - 1f;
                var v = (y + .5f) / size * 2f - 1f;
                var radius = Mathf.Sqrt(u * u + v * v);
                var angle = Mathf.Atan2(v, u);
                var spiral = Mathf.Clamp01(Mathf.Sin(angle * 3f + radius * 15f) * .7f + .35f);
                var rim = Mathf.Clamp01(1f - Mathf.Abs(radius - .56f) * 5.4f);
                var core = Mathf.Clamp01(1f - radius * 3.5f);
                var alpha = Mathf.Clamp01(rim * .84f + core * .95f + spiral * Mathf.Clamp01(1f - radius) * .45f);
                var color = Color.Lerp(new Color(.10f, .015f, .30f, alpha), new Color(.95f, .28f, 1f, alpha), spiral * .72f + core * .28f);
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
        }

        private Sprite LoadResourceSprite(string resourceName, float fallbackPixelsPerUnit)
        {
            // Image models occasionally flatten a transparent prompt to an
            // off-white backdrop. This flagship asset is keyed from its outer
            // edge at runtime, preserving metallic detail without a rectangle.
            if (resourceName == "flagship_guardian_arc")
            {
                var arcTexture = Resources.Load<Texture2D>(resourceName);
                var keyedArc = CreateEdgeKeyedSprite(arcTexture, fallbackPixelsPerUnit);
                if (keyedArc != null) return keyedArc;
            }
            var sprite = Resources.Load<Sprite>(resourceName);
            if (sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>(resourceName);
            if (texture == null) return null;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), fallbackPixelsPerUnit);
        }

        private void LoadPairedWormholeSprites()
        {
            if (pairedWormholeSprites != null && pairedWormholeSprites.Length > 0 &&
                pairedWormholeFirstMouthSprites != null && pairedWormholeSecondMouthSprites != null) return;
            var textures = Resources.LoadAll<Texture2D>("PairedWormholes");
            if (textures == null || textures.Length == 0)
            {
                // Unity may expose a PNG imported as Sprite instead of Texture2D.
                // Keep the loader tolerant of either importer setting.
                var importedSprites = Resources.LoadAll<Sprite>("PairedWormholes");
                if (importedSprites == null || importedSprites.Length == 0) return;
                var orderedSprites = new List<Sprite>(importedSprites);
                orderedSprites.Sort((a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty,
                    b != null ? b.name : string.Empty));
                pairedWormholeSprites = orderedSprites.ToArray();
                pairedWormholeFirstMouthSprites = CreateWormholeMouthSprites(orderedSprites, true);
                pairedWormholeSecondMouthSprites = CreateWormholeMouthSprites(orderedSprites, false);
                appliedPairedWormholeVariant = -1;
                return;
            }
            var ordered = new List<Texture2D>(textures);
            ordered.Sort((a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty,
                b != null ? b.name : string.Empty));
            var loaded = new List<Sprite>(ordered.Count);
            for (var i = 0; i < ordered.Count; i++)
            {
                var texture = ordered[i];
                if (texture == null || texture.width < 2 || texture.height < 2) continue;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                var sprite = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(.5f, .5f), 1024f);
                sprite.name = texture.name + "_sprite";
                loaded.Add(sprite);
            }
            pairedWormholeSprites = loaded.ToArray();
            pairedWormholeFirstMouthSprites = CreateWormholeMouthSprites(loaded, true);
            pairedWormholeSecondMouthSprites = CreateWormholeMouthSprites(loaded, false);
            appliedPairedWormholeVariant = -1;
        }

        private static Sprite[] CreateWormholeMouthSprites(List<Sprite> sources, bool upperMouth)
        {
            var mouths = new Sprite[sources != null ? sources.Count : 0];
            for (var i = 0; i < mouths.Length; i++) mouths[i] = CreateWormholeMouthSprite(sources[i], upperMouth);
            return mouths;
        }

        private static Sprite CreateWormholeMouthSprite(Sprite source, bool upperMouth)
        {
            if (source == null || source.texture == null) return source;
            var sourceRect = source.rect;
            // Remove the original throat from the middle. The remaining 44% is a
            // single mouth, so the two lenses can no longer read as two complete
            // wormholes pasted side-by-side.
            const float mouthFraction = .44f;
            var height = Mathf.Max(1f, Mathf.Floor(sourceRect.height * mouthFraction));
            var y = upperMouth ? sourceRect.y + sourceRect.height - height : sourceRect.y;
            var mouth = Sprite.Create(source.texture, new Rect(sourceRect.x, y, sourceRect.width, height),
                new Vector2(.5f, .5f), 1024f);
            mouth.name = source.name + (upperMouth ? "_mouth_A" : "_mouth_B");
            return mouth;
        }

        private static Sprite CreateEdgeKeyedSprite(Texture2D source, float pixelsPerUnit)
        {
            if (source == null || !source.isReadable) return null;
            var width = source.width;
            var height = source.height;
            var pixels = source.GetPixels();
            var backdrop = new bool[pixels.Length];
            var queue = new Queue<int>();

            void AddIfBackdrop(int index)
            {
                if (backdrop[index] || !LooksLikeOffWhiteBackdrop(pixels[index])) return;
                backdrop[index] = true;
                queue.Enqueue(index);
            }

            for (var x = 0; x < width; x++)
            {
                AddIfBackdrop(x);
                AddIfBackdrop((height - 1) * width + x);
            }
            for (var y = 1; y < height - 1; y++)
            {
                AddIfBackdrop(y * width);
                AddIfBackdrop(y * width + width - 1);
            }

            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                var x = index % width;
                var y = index / width;
                if (x > 0) AddIfBackdrop(index - 1);
                if (x < width - 1) AddIfBackdrop(index + 1);
                if (y > 0) AddIfBackdrop(index - width);
                if (y < height - 1) AddIfBackdrop(index + width);
            }

            for (var i = 0; i < pixels.Length; i++)
                if (backdrop[i]) pixels[i].a = 0f;
            var keyedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = source.name + "_keyed",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            keyedTexture.SetPixels(pixels);
            keyedTexture.Apply(false, true);
            return Sprite.Create(keyedTexture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), pixelsPerUnit);
        }

        private static bool LooksLikeOffWhiteBackdrop(Color color)
        {
            var brightest = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            var darkest = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            return color.a > .01f && darkest > .82f && brightest - darkest < .10f;
        }

        private void CreateSpaceBackdrop(string rootName = "Deep space background")
        {
            if (CreateDepthSpaceBackdrop()) return;
            spaceBackdrop = new GameObject(rootName).transform;
            var backdrop = spaceBackdrop;
            MakeSprite("Black space", backdrop, Color.black, new Vector3(20f, 20f, 1f), -100);
            backgroundStars = new SpriteRenderer[StarStreamSettings.BackgroundStarCount];
            backgroundStarDirections = new Vector2[StarStreamSettings.BackgroundStarCount];
            backgroundStarPhases = new float[StarStreamSettings.BackgroundStarCount];
            backgroundStarSpeeds = new float[StarStreamSettings.BackgroundStarCount];
            backgroundStarBrightnesses = new float[StarStreamSettings.BackgroundStarCount];
            backgroundStarSizes = new float[StarStreamSettings.BackgroundStarCount];
            backgroundStarTints = new Color[StarStreamSettings.BackgroundStarCount];
            for (var i = 0; i < StarStreamSettings.BackgroundStarCount; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var star = MakeSprite("Distant star", backdrop, Color.clear, Vector3.one, -5);
                star.sprite = circleSprite;
                backgroundStars[i] = star;
                backgroundStarDirections[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                backgroundStarPhases[i] = Random.value;
                backgroundStarSpeeds[i] = Random.Range(.62f, 1.42f);
                backgroundStarBrightnesses[i] = Random.Range(.10f, distantStarColor.a) * StarStreamSettings.BackgroundStarBrightness;
                backgroundStarSizes[i] = Random.Range(.008f, .028f);
                var tintRoll = Random.value;
                // The far parallax sky is mostly deep blue. Sparse pale points stop it from
                // becoming a flat monochrome layer, while the bright white stream stays distinct.
                backgroundStarTints[i] = tintRoll < .43f ? new Color(.20f, .38f, .92f, 1f) :
                    tintRoll < .76f ? new Color(.34f, .62f, 1f, 1f) :
                    tintRoll < .86f ? new Color(.64f, .84f, 1f, 1f) : Color.white;
            }
        }

        private void UpdateBackgroundStars()
        {
            if (spaceDepth != null) return;
            if (backgroundStars == null || backgroundStarDirections == null) return;
            var time = backgroundTravelTime * StarStreamSettings.BackgroundTravelSpeed;
            for (var i = 0; i < backgroundStars.Length; i++)
            {
                var star = backgroundStars[i];
                if (star == null) continue;
                var progress = Mathf.Repeat(backgroundStarPhases[i] + time * backgroundStarSpeeds[i], 1f);
                // A quiet outward curve reads as distant parallax, not another projectile layer.
                // These are plain sprites, so background stars never receive a visible trail.
                var travel = 1f - Mathf.Pow(1f - progress, 1.58f);
                var radius = Mathf.Lerp(StarStreamSettings.BackgroundMinRadius, StarStreamSettings.BackgroundMaxRadius, travel);
                star.transform.localPosition = backgroundStarDirections[i] * radius;
                var lifeFade = Mathf.Sin(progress * Mathf.PI);
                var alpha = backgroundStarBrightnesses[i] * lifeFade * Mathf.Lerp(.56f, 1f, travel);
                var tint = backgroundStarTints != null && i < backgroundStarTints.Length
                    ? backgroundStarTints[i]
                    : distantStarColor;
                star.color = new Color(tint.r, tint.g, tint.b, alpha);
                star.transform.localScale = Vector3.one * backgroundStarSizes[i] * Mathf.Lerp(.72f, 1.48f, travel);
            }
        }

        private void CreateMusicReactiveVisuals()
        {
            musicReactiveVisuals = GetComponent<MusicReactiveVisualDirector>();
            if (musicReactiveVisuals == null) musicReactiveVisuals = gameObject.AddComponent<MusicReactiveVisualDirector>();
            musicReactiveVisuals.Initialize(gameCamera);
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
            line.numCornerVertices = 3;
            line.gameObject.SetActive(false);
            return line;
        }

        private TrailRenderer CreateThreatTrail(string name, int sortingOrder)
        {
            var trail = new GameObject(name).AddComponent<TrailRenderer>();
            trail.transform.SetParent(arena);
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.time = .24f;
            trail.minVertexDistance = .025f;
            trail.startWidth = .22f;
            trail.endWidth = .015f;
            trail.numCapVertices = 4;
            trail.numCornerVertices = 3;
            trail.sortingOrder = sortingOrder;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(.18f, .95f, 1f), 0f),
                    new GradientColorKey(new Color(.78f, .24f, 1f), .58f),
                    new GradientColorKey(new Color(.06f, .35f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(.88f, 0f),
                    new GradientAlphaKey(.32f, .46f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = gradient;
            trail.gameObject.SetActive(false);
            return trail;
        }

        private void CreateArena()
        {
            CreateRing(OrbitSettings.Radius, OrbitSettings.LineColor, OrbitSettings.LineWidth);
            core = MakeSprite("Warp core", arena, new Color(.75f,1f,1f,.95f), new Vector3(.26f,.26f,1), 2).transform;
            core.gameObject.SetActive(false);
            var flagshipGlow = MakeSprite("Defense flagship glow", arena, new Color(.16f, .78f, 1f, .18f), Vector3.one, 0);
            flagshipGlow.sprite = circleSprite;
            SetSpriteWorldSize(flagshipGlow, DefenseFlagshipGlowWorldSize);
            flagshipGlow.gameObject.SetActive(false);
            defenseFlagshipGlow = flagshipGlow.transform;
            var flagship = MakeSprite("Guardian flagship", arena, Color.white, Vector3.one, 2);
            flagship.sprite = flagshipSprite != null ? flagshipSprite : (shipSprite != null ? shipSprite : whiteSprite);
            flagship.color = flagshipSprite != null || shipSprite != null ? Color.white : new Color(.28f, .9f, 1f);
            // A large, unmistakable collision target: it is a ship to defend,
            // not the tiny player sprite used on the orbit.
            SetSpriteWorldSize(flagship, DefenseFlagshipWorldSize);
            flagship.gameObject.SetActive(false);
            defenseFlagship = flagship.transform;
            CreateDefenseFlagshipEffects();
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
            if (!OrbitSettings.ShowTrajectory) return;
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
            poolRoot = new GameObject("Pools").transform;
            if (Application.isPlaying)
            {
                spellVfxPool = poolRoot.gameObject.AddComponent<SpellVfxPool>();
                spellVfxPool.ProjectilePrefab = playerSpellPrefab;
                spellVfxPool.Initialize();
                if (solarChickPrefab == null) solarChickPrefab = Resources.Load<SpellProjectileVfx>("Spells/SolarChicks/Prefabs/SolarChick");
                if (solarChickPrefab != null) spellVfxPool.Prewarm(solarChickPrefab);
            }
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
            starPool = new ObjectPool<StarParticle>(starParticle, poolRoot, 180);
            damageShardPool = new ObjectPool<DamageShard>(damagePrefab, poolRoot, 12);
            enemyPrefab.gameObject.SetActive(false);
            projectilePrefab.gameObject.SetActive(false);
            starParticle.gameObject.SetActive(false);
            damagePrefab.gameObject.SetActive(false);
        }

        private void CreateDefenseFlagshipEffects()
        {
            for (var i = 0; i < DefenseFlagshipLightOffsets.Length; i++)
            {
                var light = MakeSprite("Flagship running light " + i.ToString("00"), arena, Color.clear, Vector3.one, 3);
                light.sprite = circleSprite;
                SetSpriteWorldSize(light, i == 2 ? .20f : .125f);
                light.gameObject.SetActive(false);
                defenseFlagshipRunningLights.Add(light);
            }
            for (var i = 0; i < DefenseFlagshipSectionOffsets.Length; i++)
            {
                var breach = MakeSprite("Flagship armor breach " + i.ToString("00"), arena, Color.clear, Vector3.one, 4);
                breach.sprite = circleSprite;
                SetSpriteWorldSize(breach, .10f);
                breach.gameObject.SetActive(false);
                defenseFlagshipDamageMarkers.Add(breach);
            }
        }

        private void SetDefenseFlagshipEffectsActive(bool active)
        {
            for (var i = 0; i < defenseFlagshipRunningLights.Count; i++)
                if (defenseFlagshipRunningLights[i] != null) defenseFlagshipRunningLights[i].gameObject.SetActive(active);
            for (var i = 0; i < defenseFlagshipDamageMarkers.Count; i++)
                if (defenseFlagshipDamageMarkers[i] != null)
                    defenseFlagshipDamageMarkers[i].gameObject.SetActive(active && defenseFlagshipSectionDamage[i] > 0);
        }

        private void ResetDefenseFlagshipDamage()
        {
            for (var i = 0; i < defenseFlagshipSectionDamage.Length; i++)
            {
                defenseFlagshipSectionDamage[i] = 0;
                defenseFlagshipSectionFlash[i] = 0f;
            }
        }

        private void UpdateDefenseFlagshipEffects(float deltaTime)
        {
            for (var i = 0; i < defenseFlagshipRunningLights.Count; i++)
            {
                var light = defenseFlagshipRunningLights[i];
                if (light == null) continue;
                var pulse = .52f + Mathf.Sin(Time.unscaledTime * (4.2f + i * .34f) + i * 1.7f) * .32f;
                var isReactor = i == 2;
                light.transform.position = DefenseFlagshipPosition + DefenseFlagshipLightOffsets[i];
                light.color = isReactor
                    ? new Color(.24f, .95f, 1f, .35f + pulse * .55f)
                    : new Color(1f, .42f, .13f, .20f + pulse * .55f);
                SetSpriteWorldSize(light, (isReactor ? .17f : .10f) + pulse * (isReactor ? .09f : .04f));
            }
            for (var i = 0; i < defenseFlagshipDamageMarkers.Count; i++)
            {
                defenseFlagshipSectionFlash[i] = Mathf.Max(0f, defenseFlagshipSectionFlash[i] - deltaTime);
                var marker = defenseFlagshipDamageMarkers[i];
                if (marker == null) continue;
                var damage = defenseFlagshipSectionDamage[i];
                marker.transform.position = DefenseFlagshipPosition + DefenseFlagshipSectionOffsets[i];
                marker.gameObject.SetActive(damage > 0);
                if (damage <= 0) continue;
                var flash = defenseFlagshipSectionFlash[i] / .36f;
                marker.color = Color.Lerp(new Color(1f, .10f, .08f, .30f + damage * .10f),
                    new Color(1f, .84f, .42f, .92f), flash);
                SetSpriteWorldSize(marker, .08f + damage * .055f + flash * .16f);
            }
        }

        private static int DefenseFlagshipSectionFor(Vector2 impactPosition)
        {
            var localX = impactPosition.x - DefenseFlagshipPosition.x;
            return localX < -.76f ? 0 : localX > .76f ? 2 : 1;
        }

        private void RepairMostDamagedFlagshipSection()
        {
            var section = 0;
            for (var i = 1; i < defenseFlagshipSectionDamage.Length; i++)
                if (defenseFlagshipSectionDamage[i] > defenseFlagshipSectionDamage[section]) section = i;
            if (defenseFlagshipSectionDamage[section] <= 0) return;
            defenseFlagshipSectionDamage[section]--;
            defenseFlagshipSectionFlash[section] = .18f;
        }

        private void CreatePlayer()
        {
            var sr = MakeSprite("Player", arena, shipTint, Vector3.one, 5);
            if (shipSprite != null) sr.sprite = shipSprite;
            SetSpriteWorldSize(sr, .95f);
            player = sr.transform;
            PositionOnOrbit();
            EnsureRiftEchoVisual();
        }

        private void EnsureRiftEchoVisual()
        {
            if (riftEcho != null) return;
            var echo = MakeSprite("Rift Echo", arena, new Color(.38f, .90f, 1f, .68f), Vector3.one, 4);
            echo.sprite = shipSprite != null ? shipSprite : whiteSprite;
            SetSpriteWorldSize(echo, .82f);
            riftEcho = echo.transform;

            var bloom = MakeSprite("Rift Echo bloom", riftEcho, new Color(.28f, .86f, 1f, .16f), Vector3.one, 2);
            bloom.sprite = circleSprite != null ? circleSprite : whiteSprite;
            SetSpriteWorldSize(bloom, 1.35f);
            riftEchoGlow = bloom.transform;
            riftEchoPresentation = riftEcho.gameObject.AddComponent<RiftEchoPresentation>();
            riftEchoPresentation.Configure(arena, circleSprite != null ? circleSprite : whiteSprite);
            riftEcho.gameObject.SetActive(false);
        }

        private void ResetOrbitalAbilityState()
        {
            riftEchoTimer = 0f;
            riftEchoFireTimer = 0f;
            riftEchoCooldown = 0f;
            vectorSnapCooldown = 0f;
            playerRootTimer = 0f;
            queuedRiftEcho = false;
            queuedVectorSnap = false;
            combatMoments.Clear();
            if (riftEcho != null) riftEcho.gameObject.SetActive(false);
            riftEchoPresentation?.SetVisible(false);
        }

        private void UpdateOrbitalAbilityTimers(float dt)
        {
            riftEchoCooldown = Mathf.Max(0f, riftEchoCooldown - dt);
            vectorSnapCooldown = Mathf.Max(0f, vectorSnapCooldown - dt);
            playerRootTimer = Mathf.Max(0f, playerRootTimer - dt);
        }

        private void TryUseRiftEcho()
        {
            if (riftEchoCooldown > 0f || player == null) return;
            EnsureRiftEchoVisual();
            riftEchoCooldown = OrbitalAbilitySettings.EchoCooldown;
            riftEchoTimer = OrbitalAbilitySettings.EchoDuration;
            riftEchoFireTimer = .06f;
            var lead = activeControlDirection == 0 ? 1f : activeControlDirection;
            riftEchoAngle = playerAngle + lead * OrbitalAbilitySettings.EchoOrbitLeadDegrees * Mathf.Deg2Rad;
            riftEcho.gameObject.SetActive(true);
            riftEchoPresentation?.SetVisible(true);
            SpawnImpactBurst(player.position, new Color(.30f, .88f, 1f), 16, 2.5f, .30f);
            combatMoments.EchoCast();
            HapticFeedback.Pulse(24);
        }

        private void TryUseVectorSnap()
        {
            if (vectorSnapCooldown > 0f || player == null) return;
            var boss = ActiveBoss();
            var escapedBeam = boss != null && boss.BossState == BossAiState.BeamSweep &&
                              BossAttackRules.IsInsideBeam(boss.transform.position, player.position, boss.BossBeamAngle,
                                  BossSettings.BeamCount(boss.MaxHealth <= 0f ? 1f : boss.Health / boss.MaxHealth));
            var direction = activeControlDirection == 0 ? 1f : activeControlDirection;
            var before = (Vector2)player.position;
            playerAngle = Mathf.Repeat(playerAngle + direction * OrbitalAbilitySettings.VectorSnapDegrees * Mathf.Deg2Rad + Mathf.PI * 2f, Mathf.PI * 2f);
            targetAngle = playerAngle;
            PositionOnOrbit();
            player.gameObject.SetActive(true);
            invincible = Mathf.Max(invincible, OrbitalAbilitySettings.VectorSnapInvulnerability);
            vectorSnapCooldown = OrbitalAbilitySettings.VectorSnapCooldown;
            SpawnImpactBurst(before, new Color(.74f, .44f, 1f), 18, 2.4f, .28f);
            SpawnImpactBurst(player.position, new Color(.82f, .62f, 1f), 24, 3.0f, .34f);
            AddScreenShake(.10f, .055f);
            combatMoments.VectorSnap(escapedBeam);
            HapticFeedback.Pulse(35);
        }

        private void UpdateRiftEcho(float dt)
        {
            if (riftEcho == null) return;
            if (abilitySandbox != null && abilitySandbox.VfxEditorOpen)
            {
                riftEchoTimer = 0f;
                riftEcho.gameObject.SetActive(false);
                riftEchoPresentation?.SetVisible(false);
                return;
            }
            if (riftEchoTimer <= 0f)
            {
                riftEcho.gameObject.SetActive(false);
                riftEchoPresentation?.SetVisible(false);
                return;
            }

            riftEchoTimer = Mathf.Max(0f, riftEchoTimer - dt);
            var flow = activeControlDirection == 0 ? .52f : activeControlDirection * .88f;
            if (abilitySandbox.IsOpen && sandboxReverseTimer > 0f && activeControlDirection == 0) flow = -flow;
            riftEchoAngle += flow * dt;
            var position = new Vector2(Mathf.Cos(riftEchoAngle), Mathf.Sin(riftEchoAngle)) * OrbitSettings.Radius;
            position += ShipOrbitCenter;
            riftEcho.position = position;
            riftEcho.up = (ShipOrbitCenter-position).normalized;
            var fade = Mathf.Clamp01(riftEchoTimer / .42f);
            var renderer = riftEcho.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.color = new Color(.38f, .92f, 1f, .28f + fade * .48f);
            if (riftEchoGlow != null)
            {
                riftEchoGlow.position = position;
                riftEchoGlow.localScale = Vector3.one * (1.0f + Mathf.Sin(Time.time * 8f) * .12f);
            }
            riftEchoPresentation?.Render(position, player.position, fade);

            riftEchoFireTimer -= dt;
            if (riftEchoFireTimer > 0f) return;
            var target = FindClosestEnemy(position);
            if (target == null) return;
            riftEchoFireTimer = OrbitalAbilitySettings.EchoFireInterval;
            var direction = ((Vector2)target.transform.position - position).normalized;
            Shoot(position, direction * OrbitalAbilitySettings.EchoProjectileSpeed, true,
                new Color(.34f, .92f, 1f), DamageElement.Cold, OrbitalAbilitySettings.EchoDamage, true);
            SpawnImpactBurst(position, new Color(.42f, .94f, 1f, .72f), 3, .92f, .16f);
        }

        private Enemy FindClosestEnemy(Vector2 position)
        {
            Enemy closest = null;
            var closestDistance = float.MaxValue;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null) continue;
                var distance = ((Vector2)enemy.transform.position - position).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closest = enemy;
                closestDistance = distance;
            }
            return closest;
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
            EnsurePairedLensVisuals();
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
                coopThreatCleaveRenderer = CreateThreatLine("Threat cleave glow", 15, .34f);
                coopThreatCleaveRenderer.positionCount = 19;
            }
            if (coopThreatCleaveCoreRenderer == null)
            {
                coopThreatCleaveCoreRenderer = CreateThreatLine("Threat cleave core", 17, .075f);
                coopThreatCleaveCoreRenderer.positionCount = 19;
            }
            if (coopThreatCleaveEchoRenderer == null)
            {
                coopThreatCleaveEchoRenderer = CreateThreatLine("Threat cleave echo", 14, .15f);
                coopThreatCleaveEchoRenderer.positionCount = 19;
            }
            if (coopThreatCleaveTrail == null)
            {
                coopThreatCleaveTrail = CreateThreatTrail("Threat cleave plasma trail", 13);
            }
            if (coopThreatCleaveNodes.Count == 0)
            {
                for (var i = 0; i < 7; i++)
                {
                    var node = MakeSprite("Threat cleave energy node " + i.ToString("00"), arena,
                        Color.white, Vector3.one, 18);
                    node.sprite = circleSprite != null ? circleSprite : whiteSprite;
                    SetSpriteWorldSize(node, .09f);
                    node.gameObject.SetActive(false);
                    coopThreatCleaveNodes.Add(node);
                }
            }
            if (coopThreatRingLeftRenderer == null)
            {
                coopThreatRingLeftRenderer = CreateThreatLine("Threat ring left", 14, .075f);
                coopThreatRingLeftRenderer.positionCount = 17;
            }
            if (coopThreatRingCenterRenderer == null)
            {
                coopThreatRingCenterRenderer = CreateThreatLine("Threat ring center", 14, .075f);
                coopThreatRingCenterRenderer.positionCount = 17;
            }
            if (coopThreatRingRightRenderer == null)
            {
                coopThreatRingRightRenderer = CreateThreatLine("Threat ring right", 14, .075f);
                coopThreatRingRightRenderer.positionCount = 17;
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
                coopRoomWash.enabled = false;
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
            SetPairedLensVisualsActive(active && coopPreviewLensesActive);
            // The co-op/expedition path is the coloured cyan-violet guide for a
            // morphing route. It is independent from the old dotted solo orbit,
            // which is still controlled by OrbitSettings.ShowTrajectory.
            if (coopTrajectoryRenderer != null)
            {
                coopTrajectoryRenderer.gameObject.SetActive(active);
                coopTrajectoryRenderer.enabled = active;
            }
            if (coopTetherRenderer != null) coopTetherRenderer.gameObject.SetActive(false);
            if (coopThreatCleaveRenderer != null) coopThreatCleaveRenderer.gameObject.SetActive(false);
            if (coopThreatCleaveCoreRenderer != null) coopThreatCleaveCoreRenderer.gameObject.SetActive(false);
            if (coopThreatCleaveEchoRenderer != null) coopThreatCleaveEchoRenderer.gameObject.SetActive(false);
            if (coopThreatCleaveTrail != null)
            {
                coopThreatCleaveTrail.gameObject.SetActive(false);
                coopThreatCleaveTrail.Clear();
            }
            if (coopThreatRingLeftRenderer != null) coopThreatRingLeftRenderer.gameObject.SetActive(false);
            if (coopThreatRingCenterRenderer != null) coopThreatRingCenterRenderer.gameObject.SetActive(false);
            if (coopThreatRingRightRenderer != null) coopThreatRingRightRenderer.gameObject.SetActive(false);
            for (var i = 0; i < coopThreatMineMarkers.Count; i++)
                if (coopThreatMineMarkers[i] != null) coopThreatMineMarkers[i].gameObject.SetActive(false);
            for (var i = 0; i < coopThreatCleaveNodes.Count; i++)
                if (coopThreatCleaveNodes[i] != null) coopThreatCleaveNodes[i].gameObject.SetActive(false);
            if (coopRoomEnvironment != null) coopRoomEnvironment.gameObject.SetActive(active);
            for (var i = 0; i < orbitRenderers.Count; i++)
                if (orbitRenderers[i] != null) orbitRenderers[i].enabled = !active && OrbitSettings.ShowTrajectory;
        }

        private void EnsurePairedLensVisuals()
        {
            if (coopLensFirst == null)
            {
                coopLensFirst = CreatePairedLensVisual("Paired lens A", new Color(.22f, .92f, 1f, .82f),
                    out coopLensFirstGlow, out coopLensFirstPointer);
                coopLensSecond = CreatePairedLensVisual("Paired lens B", new Color(.94f, .34f, 1f, .82f),
                    out coopLensSecondGlow, out coopLensSecondPointer);
            }
            if (coopLensLink == null)
                coopLensLink = CreatePairedLensTunnelLine("Paired wormhole tunnel core", .042f, 1);
            if (coopLensTunnelOuter == null)
                coopLensTunnelOuter = CreatePairedLensTunnelLine("Paired wormhole tunnel filament A", .026f, 0);
            if (coopLensTunnelInner == null)
                coopLensTunnelInner = CreatePairedLensTunnelLine("Paired wormhole tunnel filament B", .026f, 0);
            ApplyPairedWormholeVariant();
        }

        private LineRenderer CreatePairedLensTunnelLine(string name, float width, int sortingOrder)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(arena);
            line.useWorldSpace = true;
            line.positionCount = 17;
            line.startWidth = line.endWidth = width;
            line.numCornerVertices = 3;
            line.numCapVertices = 4;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = sortingOrder;
            line.gameObject.SetActive(false);
            return line;
        }

        private Transform CreatePairedLensVisual(string name, Color color, out Transform glow, out Transform pointer)
        {
            var body = MakeSprite(name, arena, color, Vector3.one, 6);
            body.sprite = circleSprite != null ? circleSprite : whiteSprite;
            SetSpriteWorldSize(body, PairedLensRules.LensRadius * 2f);
            var glowRenderer = MakeSprite(name + " glow", body.transform, new Color(color.r, color.g, color.b, .18f),
                Vector3.one, 5);
            glowRenderer.sprite = circleSprite != null ? circleSprite : whiteSprite;
            SetSpriteWorldSize(glowRenderer, PairedLensRules.LensRadius * 2.55f);
            var pointerRenderer = MakeSprite(name + " direction", arena, Color.white, Vector3.one, 8);
            pointerRenderer.sprite = whiteSprite;
            pointerRenderer.transform.localScale = new Vector3(.075f, .30f, 1f);
            glow = glowRenderer.transform;
            pointer = pointerRenderer.transform;
            return body.transform;
        }

        private void ApplyPairedWormholeVariant()
        {
            if (pairedWormholeSprites == null || pairedWormholeSprites.Length == 0 ||
                pairedWormholeFirstMouthSprites == null || pairedWormholeSecondMouthSprites == null) return;
            var mouthCount = Mathf.Min(pairedWormholeFirstMouthSprites.Length, pairedWormholeSecondMouthSprites.Length);
            if (mouthCount == 0) return;
            var variant = Mathf.Abs(pairedWormholeVariant) % mouthCount;
            if (variant == appliedPairedWormholeVariant && coopLensFirst != null && coopLensSecond != null)
                return;
            var firstRenderer = coopLensFirst != null ? coopLensFirst.GetComponent<SpriteRenderer>() : null;
            var secondRenderer = coopLensSecond != null ? coopLensSecond.GetComponent<SpriteRenderer>() : null;
            if (firstRenderer != null)
            {
                firstRenderer.sprite = pairedWormholeFirstMouthSprites[variant] ?? pairedWormholeSprites[variant];
                firstRenderer.color = Color.white;
                SetSpriteWorldSize(firstRenderer, PairedLensRules.LensRadius * 2.20f);
            }
            if (secondRenderer != null)
            {
                secondRenderer.sprite = pairedWormholeSecondMouthSprites[variant] ?? pairedWormholeSprites[variant];
                secondRenderer.color = Color.white;
                SetSpriteWorldSize(secondRenderer, PairedLensRules.LensRadius * 2.20f);
            }
            var accent = PairedWormholeAccents[variant % PairedWormholeAccents.Length];
            var firstGlow = coopLensFirstGlow != null ? coopLensFirstGlow.GetComponent<SpriteRenderer>() : null;
            var secondGlow = coopLensSecondGlow != null ? coopLensSecondGlow.GetComponent<SpriteRenderer>() : null;
            if (firstGlow != null) firstGlow.color = new Color(accent.r, accent.g, accent.b, .13f);
            if (secondGlow != null) secondGlow.color = new Color(accent.r, accent.g, accent.b, .13f);
            appliedPairedWormholeVariant = variant;
        }

        private void SetPairedLensVisualsActive(bool active)
        {
            if (coopLensFirst != null) coopLensFirst.gameObject.SetActive(active);
            if (coopLensSecond != null) coopLensSecond.gameObject.SetActive(active);
            // The mouth orientation and the outward exit path now communicate the
            // direction. Extra white arrows made the shared tunnel read as a stick.
            if (coopLensFirstPointer != null) coopLensFirstPointer.gameObject.SetActive(false);
            if (coopLensSecondPointer != null) coopLensSecondPointer.gameObject.SetActive(false);
            if (coopLensLink != null) coopLensLink.gameObject.SetActive(active);
            if (coopLensTunnelOuter != null) coopLensTunnelOuter.gameObject.SetActive(active);
            if (coopLensTunnelInner != null) coopLensTunnelInner.gameObject.SetActive(active);
        }

        private void UpdatePairedLensVisuals()
        {
            var active = coopPlaying && coopLocalPreview && coopPreviewLensesActive && coopPreviewLensPair.IsValid &&
                !coopPreviewCompleted && !coopPreviewFailed;
            SetPairedLensVisualsActive(active);
            if (!active) return;
            ApplyPairedWormholeVariant();
            PositionPairedLensVisual(coopLensFirst, coopLensFirstGlow, coopLensFirstPointer,
                coopPreviewLensPair.FirstCenter, coopPreviewLensPair.FirstNormal, 1f);
            PositionPairedLensVisual(coopLensSecond, coopLensSecondGlow, coopLensSecondPointer,
                coopPreviewLensPair.SecondCenter, coopPreviewLensPair.SecondNormal, -1f);
            var accent = PairedWormholeAccents[Mathf.Abs(pairedWormholeVariant) % PairedWormholeAccents.Length];
            UpdatePairedWormholeTunnel(coopPreviewLensPair.FirstCenter, coopPreviewLensPair.SecondCenter, accent);
        }

        private void UpdatePairedWormholeTunnel(Vector2 first, Vector2 second, Color accent)
        {
            if (coopLensLink == null || coopLensTunnelOuter == null || coopLensTunnelInner == null) return;
            var axis = second - first;
            if (axis.sqrMagnitude < .0001f) return;
            var axisNormal = axis.normalized;
            var perpendicular = new Vector2(-axisNormal.y, axisNormal.x);
            var time = Time.unscaledTime;
            const int points = 17;
            for (var i = 0; i < points; i++)
            {
                var progress = i / (float)(points - 1);
                var envelope = Mathf.Sin(progress * Mathf.PI);
                var phase = progress * Mathf.PI * 2.25f + time * 2.15f;
                var core = Vector2.Lerp(first, second, progress) + perpendicular * Mathf.Sin(phase) * (.045f * envelope);
                var outerOffset = Mathf.Sin(phase + 1.85f) * (.155f * envelope);
                var innerOffset = Mathf.Sin(phase - 1.30f) * (.105f * envelope);
                coopLensLink.SetPosition(i, core);
                coopLensTunnelOuter.SetPosition(i, core + perpendicular * outerOffset);
                coopLensTunnelInner.SetPosition(i, core + perpendicular * innerOffset);
            }
            var pulse = .72f + Mathf.Sin(time * 3.2f) * .16f;
            var coreColor = Color.Lerp(accent, Color.white, .38f);
            coopLensLink.startColor = new Color(coreColor.r, coreColor.g, coreColor.b, .46f * pulse);
            coopLensLink.endColor = new Color(coreColor.r, coreColor.g, coreColor.b, .46f * pulse);
            coopLensTunnelOuter.startColor = new Color(accent.r, accent.g, accent.b, .20f * pulse);
            coopLensTunnelOuter.endColor = new Color(accent.r, accent.g, accent.b, .20f * pulse);
            var innerColor = Color.Lerp(accent, Color.white, .58f);
            coopLensTunnelInner.startColor = new Color(innerColor.r, innerColor.g, innerColor.b, .26f * pulse);
            coopLensTunnelInner.endColor = new Color(innerColor.r, innerColor.g, innerColor.b, .26f * pulse);
        }

        private static void PositionPairedLensVisual(Transform body, Transform glow, Transform pointer,
            Vector2 center, Vector2 normal, float rotationDirection)
        {
            if (body != null)
            {
                body.position = center;
                // A mouth faces away from the tunnel. A restrained wobble sells
                // pressure in the throat without turning it into a whole second
                // hourglass at this end.
                var baseAngle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
                var wobble = Mathf.Sin(Time.unscaledTime * 2.4f + center.x) * 2.2f * rotationDirection;
                body.rotation = Quaternion.Euler(0f, 0f, baseAngle + wobble);
            }
            if (glow != null)
            {
                glow.localPosition = Vector3.zero;
                var pulse = 1f + Mathf.Sin(Time.unscaledTime * 5.4f + center.x) * .10f;
                glow.localScale = Vector3.one * pulse;
            }
            if (pointer == null) return;
            pointer.position = center + normal * (PairedLensRules.LensRadius * .68f);
            pointer.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f);
        }

        private void BeginCoopRun(bool localPreview, bool soloExpedition = false, bool experimental = false,
            LivingCosmosCheckpoint restore = null)
        {
            defensePlaying = false;
            defenseRunOver = false;
            if (defenseFlagship != null) defenseFlagship.gameObject.SetActive(false);
            if (defenseFlagshipGlow != null) defenseFlagshipGlow.gameObject.SetActive(false);
            SetDefenseFlagshipEffectsActive(false);
            coopLocalPreview = localPreview;
            soloExpeditionPlaying = soloExpedition;
            coopPlaying = true;
            UpdateCameraFraming(true);
            playing = false;
            SetPaused(false);
            showMenu = false;
            showSettings = false;
            showCoop = false;
            showResults = false;
            Cleanup();
            livingCosmos = experimental && soloExpedition ? new LivingCosmosRunState() : null;
            livingTempo = null;
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
            coopPreviewHostShots = coopPreviewGuestShots = 0;
            coopPreviewHostFireTimer = .25f;
            coopPreviewGuestFireTimer = .48f;
            // Preview and Expedition both use a new deterministic seed per
            // run. The seed still makes a session reproducible for its host.
            coopPreviewRunSeed = restore == null ? Mathf.Max(1, Guid.NewGuid().GetHashCode() & int.MaxValue) : restore.seed;
            var trajectoryEntryAngle = CoopTrajectorySettings.InitialAngleOffsetForRun(coopPreviewRunSeed);
            coopPreviewHostAngle = Mathf.Repeat(210f + trajectoryEntryAngle, 360f);
            coopPreviewGuestAngle = Mathf.Repeat(330f + trajectoryEntryAngle, 360f);
            // A run can enter on a different part of the morph cycle, so an
            // eight does not always begin from the same left loop.
            coopPreviewTrajectoryTime = CoopTrajectorySettings.InitialElapsedForRun(coopPreviewRunSeed);
            coopPreviewSector = LivingCosmosActive ? LivingCosmosRunState.CreatePreviewRoute(coopPreviewRunSeed) : SectorGenerator.Generate(coopPreviewRunSeed);
            if (LivingCosmosActive)
            {
                if (restore == null) livingCosmos.Initialize(coopPreviewRunSeed);
                else livingCosmos.Restore(restore.route);
            }
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
            coopPreviewLensesActive = false;
            coopPreviewLensPair = default;
            coopPreviewRelayLensState = default;
            coopLensSoundCooldown = 0f;
            coopThreatPatternVisualTimer = 0f;
            coopThreatPatternVisualDuration = 0f;
            coopThreatCleaveSparkTimer = 0f;
            expeditionShopDocking = false;
            expeditionShopOpen = false;
            expeditionShopRoomIndex = -1;
            expeditionShopDockTimer = 0f;
            expeditionShopFlightSparkTimer = 0f;
            expeditionFireIntervalMultiplier = 1f;
            expeditionProjectileSpeedMultiplier = 1f;
            expeditionDamageBonus = 0;
            expeditionPrismLevel = 0;
            expeditionAegisCharges = 0;
            expeditionFieldRepairLevel = 0;
            expeditionAegisLevel = 0;
            expeditionUpgradeNotice = string.Empty;
            expeditionUpgradeNoticeTimer = 0f;
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
            coopResultRunId = restore != null ? restore.runId : soloExpeditionPlaying
                ? "solo-expedition-" + Guid.NewGuid().ToString("N")
                : localPreview
                ? "preview-" + coopPreviewRunSeed
                : (multiplayerSessions == null ? string.Empty : multiplayerSessions.RunId);
            if (string.IsNullOrWhiteSpace(coopResultRunId))
                coopResultRunId = "coop-" + (multiplayerSessions == null ? coopPreviewRunSeed : multiplayerSessions.RunSeed);
            coopResultSubmitted = false;
            if (LivingCosmosActive)
                livingTempo = restore == null ? new TempoRewardState(coopResultRunId, coopPreviewRunSeed) : TempoRewardState.Restore(restore.tempo);
            if (restore != null) coopPreviewRoomIndex = restore.route.roomIndex;
            ResetCoopPreviewEnemy(restore == null);
            if (restore != null) RestoreLivingCheckpointPresentation(restore);
            if (LivingCosmosActive && restore == null) livingCosmos.OpenInitialNavigation();
            if (LivingCosmosActive) BindCanvasUi();
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
            if (LivingCosmosActive && coopPreviewFailed)
                livingCosmos.Tick(dt, coopPreviewEnemyHealth, 0, 0f, false, false, false);
            activeControlDirection = localDirection;
            coopCollisionBannerTimer = Mathf.Max(0f, coopCollisionBannerTimer - Mathf.Max(0f, dt));
            coopRelayCoreBannerTimer = Mathf.Max(0f, coopRelayCoreBannerTimer - Mathf.Max(0f, dt));
            coopTetherBannerTimer = Mathf.Max(0f, coopTetherBannerTimer - Mathf.Max(0f, dt));
            coopRedirectBannerTimer = Mathf.Max(0f, coopRedirectBannerTimer - Mathf.Max(0f, dt));
            coopLensSoundCooldown = Mathf.Max(0f, coopLensSoundCooldown - Mathf.Max(0f, dt));
            coopThreatDefeatedBannerTimer = Mathf.Max(0f, coopThreatDefeatedBannerTimer - Mathf.Max(0f, dt));
            coopHullHitBannerTimer = Mathf.Max(0f, coopHullHitBannerTimer - Mathf.Max(0f, dt));
            expeditionUpgradeNoticeTimer = Mathf.Max(0f, expeditionUpgradeNoticeTimer - Mathf.Max(0f, dt));
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
                    coopPreviewHostAngle = CoopSimulationRules.StepAngle(coopPreviewHostAngle, localDirection, dt,
                        coopPreviewTrajectoryTime);
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
                        coopPreviewGuestAngle = CoopSimulationRules.StepAngle(coopPreviewGuestAngle, guestDirection, dt,
                            coopPreviewTrajectoryTime);
                        UpdateLocalCoopShipCollision(dt);
                    }
                    var previewRoomType = (SectorRoomType)Mathf.Clamp(coopPreviewEnemyKind, 0, (int)SectorRoomType.Boss);
                    var expeditionShopActive = IsExpeditionShopActive(previewRoomType);
                    if (!expeditionShopActive && (!LivingCosmosActive || livingCosmos.CanDealDamage)) UpdateCoopPreviewFire(dt);
                    if (expeditionShopActive)
                    {
                        UpdateExpeditionShopDock(dt);
                        coopPreviewRoomTimer = 0f;
                    }
                    else if (coopPreviewEnemyHealth > 0)
                        coopPreviewRoomTimer = 0f;
                    else
                        coopPreviewRoomTimer += Mathf.Max(0f, dt);
                    if (LivingCosmosActive)
                    {
                        UpdateLivingTempo(dt);
                        if (livingCosmos.Tick(dt, coopPreviewEnemyHealth, coopPreviewTeamHealth,
                            coopPreviewRoomEntryGraceTimer, expeditionShopDocking, expeditionShopOpen,
                            coopPreviewRoomIndex == coopPreviewSector.Rooms.Count - 1))
                        {
                            coopPreviewRoomIndex = livingCosmos.PendingNodeId;
                            ResetCoopPreviewEnemy();
                        }
                        if (livingCosmos.Phase == LivingEncounterPhase.Completed && !coopPreviewCompleted)
                        { coopPreviewCompleted = true; coopPreviewCompletionSequence++; }
                        if (!livingCosmos.CanDealDamage)
                        {
                            coopPreviewPlayerShots.Clear();
                            coopPreviewRelayCoreDangerous = false;
                            coopThreatPatternVisualTimer = coopPreviewThreatPulseTimer = coopPreviewThreatWindupTimer = 0f;
                        }
                    }
                    else if (!expeditionShopActive && coopPreviewRoomTimer >= CoopRoomRules.RoomClearDelay && coopPreviewSector != null && coopPreviewEnemyHealth <= 0)
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
                    coopPreviewEnemyAngle = Mathf.Repeat(coopPreviewEnemyAngle + Mathf.Max(0f, dt) * CoopRoomRules.EnemyOrbitSpeed(previewRoomType), 360f);
                    coopPreviewEnemyRadius = Mathf.MoveTowards(coopPreviewEnemyRadius,
                        CoopTrajectorySettings.ThreatOrbitRadius, Mathf.Max(0f, dt) * .44f);
                    if (!expeditionShopActive) UpdateCoopPreviewTether(dt);
                }
                hostAngle = coopPreviewHostAngle;
                guestAngle = coopPreviewGuestAngle;
                hostShots = coopPreviewHostShots;
                guestShots = soloExpeditionPlaying ? 0u : coopPreviewGuestShots;
                var hostDelta = hostShots > lastCoopHostShots ? hostShots - lastCoopHostShots : 0u;
                var guestDelta = guestShots > lastCoopGuestShots ? guestShots - lastCoopGuestShots : 0u;
                var allowLivingDamage = !LivingCosmosActive || (!coopPreviewFailed && livingCosmos.CanDealDamage && coopPreviewEnemyHealth > 0);
                if (allowLivingDamage)
                {
                    ApplyCoopPreviewDamage(hostDelta, guestDelta);
                    UpdateCoopPreviewPlayerShots(dt);
                    if (!LivingCosmosActive || coopPreviewEnemyHealth > 0) UpdateCoopPreviewRelayCore(dt);
                }
                if (!coopPreviewCompleted && !coopPreviewFailed &&
                    (!LivingCosmosActive || (livingCosmos.CanDealDamage && coopPreviewEnemyHealth > 0)) &&
                    !IsExpeditionShopActive((SectorRoomType)Mathf.Clamp(coopPreviewEnemyKind, 0, (int)SectorRoomType.Boss)))
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
            UpdatePairedLensVisuals();
            UpdateCoopRoomEnvironment(runSeed, roomIndex,
                (SectorRoomType)Mathf.Clamp(enemyKind, 0, (int)SectorRoomType.Boss), dt);
            PositionCoopShip(player, coopHostMarker, hostAngle, trajectoryTime);
            if (IsExpeditionShopActive((SectorRoomType)Mathf.Clamp(enemyKind, 0, (int)SectorRoomType.Boss)))
                PositionExpeditionShopShip();
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
                HideCoopThreatPatternVisuals();
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
                    if (threatPattern == CoopThreatPattern.Cleave)
                    {
                        coopThreatCleaveSparkTimer = 0f;
                        if (coopThreatCleaveTrail != null) coopThreatCleaveTrail.Clear();
                        SpawnCleaveWaveLaunch(coopEnemy.position, coopThreatVisualTarget, pulseColor);
                    }
                    else if (threatPattern == CoopThreatPattern.Bolt)
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
            if (LivingCosmosActive && success) coopResultScore = ComputeLivingScore();
            coopResultFingerprint = ComputeCoopResultFingerprint(layout, coopResultRunId, coopResultScore);
            if (LivingCosmosActive)
            {
                // Experimental scores must not touch local best/MMR or the Firebase queue.
                if (livingCheckpointStore == null) livingCheckpointStore = new LivingCosmosCheckpointStore();
                livingCheckpointStore.Clear();
                coopResultMmrDelta = lastMmrDelta = 0;
                mmrResultTimer = 0f;
                coopResultSubmitted = true;
                return;
            }
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

        private int ComputeLivingScore()
        {
            var total = 0;
            foreach (var node in livingCosmos.Cleared)
            {
                var room = livingCosmos.Layout.Rooms[node];
                total += 180 + Mathf.Clamp(room.Threat, 1, 20) * 35 + CoopRoomRules.RewardAmount(room.Type);
            }
            return total;
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

        private void ResetCoopPreviewEnemy(bool enterLivingRoom = true)
        {
            if (coopPreviewSector == null || coopPreviewSector.Rooms.Count == 0) return;
            var room = coopPreviewSector.Rooms[Mathf.Clamp(coopPreviewRoomIndex, 0, coopPreviewSector.Rooms.Count - 1)];
            var isExpeditionShop = soloExpeditionPlaying && room.Type == SectorRoomType.Shop;
            coopPreviewEnemyKind = (byte)room.Type;
            coopPreviewEnemyMaxHealth = isExpeditionShop || (LivingCosmosActive && room.Type == SectorRoomType.Start) ? 0 : CoopRoomRules.EnemyHealth(room);
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
            // M6 is intentionally an offline experimental encounter. The first ordinary
            // combat room teaches an object that redirects physical player shots and the
            // relay core; ships themselves never collide with a lens.
            coopPreviewLensPair = default;
            coopPreviewLensesActive = LivingCosmosActive && coopPreviewRoomIndex == 1 &&
                room.Type == SectorRoomType.Combat &&
                PairedLensRules.TryCreatePair(coopPreviewRunSeed, coopPreviewRoomIndex, out coopPreviewLensPair);
            if (coopPreviewLensesActive && pairedWormholeSprites != null && pairedWormholeSprites.Length > 0)
            {
                // One stable skin per room keeps both mouths visually paired while
                // allowing all ten generated variants to appear across runs.
                var variantSeed = coopPreviewRunSeed ^ (coopPreviewRoomIndex * 7919);
                pairedWormholeVariant = (variantSeed & int.MaxValue) % pairedWormholeSprites.Length;
                appliedPairedWormholeVariant = -1;
            }
            coopPreviewRelayLensState = default;
            if (isExpeditionShop)
                BeginExpeditionShopDock();
            else
                HideExpeditionShopDock();
            if (LivingCosmosActive && enterLivingRoom) livingCosmos.EnterRoom(coopPreviewRoomIndex);
        }

        private void RestoreLivingCheckpointPresentation(LivingCosmosCheckpoint checkpoint)
        {
            // M4 checkpoints exist only while the defeated encounter's reward is pending.
            // Rebuild the deterministic room shell, then keep it non-combat until the choice commits.
            coopPreviewTeamHealth=Mathf.Clamp(checkpoint.teamHealth,1,coopPreviewTeamMaxHealth);
            expeditionFireIntervalMultiplier=checkpoint.fireIntervalMilli/1000f;
            expeditionProjectileSpeedMultiplier=checkpoint.projectileSpeedMilli/1000f;
            expeditionDamageBonus=checkpoint.damageBonus;
            expeditionPrismLevel=checkpoint.prismLevel;
            expeditionAegisCharges=checkpoint.aegisCharges;
            expeditionFieldRepairLevel=checkpoint.fieldRepairLevel;
            expeditionAegisLevel=checkpoint.aegisLevel;
            coopPreviewEnemyHealth=0;
            coopPreviewRoomEntryGraceTimer=0f;
            coopPreviewPlayerShots.Clear();
            coopPreviewRelayCoreDangerous=false;
            expeditionUpgradeNotice="ИМПУЛЬ ВОССТАНОВЛЕН · ВЫБЕРИ МОДУЛЬ";
            expeditionUpgradeNoticeTimer=99f;
        }

        private void BeginExpeditionShopDock()
        {
            expeditionShopDocking = true;
            expeditionShopOpen = false;
            expeditionShopRoomIndex = coopPreviewRoomIndex;
            expeditionShopDockTimer = 0f;
            expeditionShopFlightSparkTimer = 0f;
            expeditionShopDockOrigin = player == null ? Vector2.zero : player.position;
            expeditionUpgradeNotice = "ПЕРЕХОД К ФЛАГМАНУ";
            expeditionUpgradeNoticeTimer = ShopDockSequenceDuration;
            if (defenseFlagship != null)
            {
                defenseFlagship.position = ShopFlagshipPosition;
                defenseFlagship.rotation = Quaternion.identity;
                SetSpriteWorldSize(defenseFlagship.GetComponent<SpriteRenderer>(), ShopFlagshipWorldSize);
                defenseFlagship.gameObject.SetActive(true);
            }
            if (defenseFlagshipGlow != null)
            {
                defenseFlagshipGlow.position = ShopFlagshipPosition;
                defenseFlagshipGlow.localScale = Vector3.one * ShopFlagshipGlowWorldSize;
                defenseFlagshipGlow.gameObject.SetActive(true);
            }
            SpawnWarpBurst(18, .95f);
        }

        private void HideExpeditionShopDock()
        {
            expeditionShopDocking = false;
            expeditionShopOpen = false;
            expeditionShopRoomIndex = -1;
            expeditionShopDockTimer = 0f;
            expeditionShopFlightSparkTimer = 0f;
            if (!defensePlaying && defenseFlagship != null) defenseFlagship.gameObject.SetActive(false);
            if (!defensePlaying && defenseFlagshipGlow != null) defenseFlagshipGlow.gameObject.SetActive(false);
        }

        private void UpdateExpeditionShopDock(float deltaTime)
        {
            if (!expeditionShopDocking || expeditionShopOpen) return;
            expeditionShopDockTimer = Mathf.Min(ShopDockSequenceDuration,
                expeditionShopDockTimer + Mathf.Max(0f, deltaTime));
            expeditionShopFlightSparkTimer -= Mathf.Max(0f, deltaTime);
            if (expeditionShopFlightSparkTimer <= 0f && player != null)
            {
                expeditionShopFlightSparkTimer = .13f;
                SpawnImpactBurst(player.position, new Color(.28f, .88f, 1f, .72f), 2, .56f, .17f);
            }
            if (expeditionShopDockTimer < ShopDockSequenceDuration) return;
            expeditionShopOpen = true;
            expeditionUpgradeNotice = "СТЫКОВКА ЗАВЕРШЕНА // ВЫБЕРИ МОДУЛЬ";
            expeditionUpgradeNoticeTimer = 99f;
            if (player != null) SpawnImpactBurst(player.position, new Color(.52f, 1f, .86f), 18, 2.4f, .34f);
            HapticFeedback.Pulse(25);
        }

        private bool IsExpeditionShopActive(SectorRoomType roomType)
        {
            return soloExpeditionPlaying && roomType == SectorRoomType.Shop &&
                   (expeditionShopDocking || expeditionShopOpen);
        }

        private void PositionExpeditionShopShip()
        {
            if (player == null) return;
            var stagingPoint = ShopFlagshipPosition + Vector2.down * 1.72f;
            var dockPoint = ShopFlagshipPosition + Vector2.down * .76f;
            Vector2 nextPosition;
            if (expeditionShopDockTimer <= ShopApproachDuration)
            {
                var travel = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(expeditionShopDockTimer / ShopApproachDuration));
                // Slight arc makes the departure from the orbit and approach
                // to the larger ship visually legible instead of a teleport.
                var straight = Vector2.Lerp(expeditionShopDockOrigin, stagingPoint, travel);
                nextPosition = straight + Vector2.right * Mathf.Sin(travel * Mathf.PI) * .58f;
            }
            else
            {
                var clamp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                    (expeditionShopDockTimer - ShopApproachDuration) / ShopClampDuration));
                nextPosition = Vector2.Lerp(stagingPoint, dockPoint, clamp);
            }
            var travelDirection = nextPosition - (Vector2)player.position;
            player.position = nextPosition;
            player.up = travelDirection.sqrMagnitude > .0025f ? travelDirection.normalized : Vector2.up;
            if (coopHostMarker != null) coopHostMarker.position = player.position;
        }

        private void ApplyExpeditionUpgrade(ExpeditionUpgrade upgrade)
        {
            if (!expeditionShopOpen || !soloExpeditionPlaying ||
                ExpeditionUpgradeRank(upgrade) >= ExpeditionUpgradeMaxRanks(upgrade)) return;
            if (LivingCosmosActive && !livingCosmos.ConsumeShopChoice()) return;
            switch (upgrade)
            {
                case ExpeditionUpgrade.RapidFire:
                    expeditionFireIntervalMultiplier = Mathf.Max(.48f, expeditionFireIntervalMultiplier * .82f);
                    break;
                case ExpeditionUpgrade.PlasmaDrive:
                    expeditionProjectileSpeedMultiplier = Mathf.Min(2.15f, expeditionProjectileSpeedMultiplier * 1.18f);
                    break;
                case ExpeditionUpgrade.ReactorAmplifier:
                    expeditionDamageBonus = Mathf.Min(6, expeditionDamageBonus + 1);
                    break;
                case ExpeditionUpgrade.PrismSplit:
                    expeditionPrismLevel = Mathf.Min(2, expeditionPrismLevel + 1);
                    break;
                case ExpeditionUpgrade.FieldRepair:
                    expeditionFieldRepairLevel++;
                    coopPreviewTeamHealth = Mathf.Min(coopPreviewTeamMaxHealth, coopPreviewTeamHealth + 2);
                    break;
                case ExpeditionUpgrade.AegisPlating:
                    expeditionAegisLevel++;
                    expeditionAegisCharges = Mathf.Min(6, expeditionAegisCharges + 2);
                    break;
            }
            expeditionUpgradeNotice = "УСТАНОВЛЕНО // " + ExpeditionUpgradeAppliedLabel(upgrade);
            expeditionUpgradeNoticeTimer = 1.8f;
            SpawnImpactBurst(player.position, ExpeditionUpgradeColor(upgrade), 24, 3.1f, .38f);
            PlayEffect(coopRicochetSound, .72f);
            HapticFeedback.Pulse(35);
            if (LivingCosmosActive && livingCosmos.ShopChoicesRemaining > 0)
            {
                expeditionUpgradeNotice = "НАГРАДА ЗА ЭЛИТУ // ВЫБЕРИ ЕЩЕ МОДУЛЬ";
                return;
            }
            HideExpeditionShopDock();
            coopPreviewRoomTimer = CoopRoomRules.RoomClearDelay;
        }

        private static string ExpeditionUpgradeTitle(ExpeditionUpgrade upgrade)
        {
            switch (upgrade)
            {
                case ExpeditionUpgrade.RapidFire: return "РАПИД-КАТУШКИ";
                case ExpeditionUpgrade.PlasmaDrive: return "ПЛАЗМА-ДРАЙВ";
                case ExpeditionUpgrade.ReactorAmplifier: return "РЕАКТОР x2";
                case ExpeditionUpgrade.PrismSplit: return "ПРИЗМЕННЫЙ ВЕЕР";
                case ExpeditionUpgrade.FieldRepair: return "ПОЛЕВОЙ РЕМОНТ";
                default: return "ЭГИДА-ФОРС";
            }
        }

        private static string ExpeditionUpgradeDescription(ExpeditionUpgrade upgrade)
        {
            switch (upgrade)
            {
                case ExpeditionUpgrade.RapidFire: return "-18% ПЕРЕЗАРЯДКА";
                case ExpeditionUpgrade.PlasmaDrive: return "+18% СКОРОСТЬ ПЛАЗМЫ";
                case ExpeditionUpgrade.ReactorAmplifier: return "+1 УРОН СНАРЯДА";
                case ExpeditionUpgrade.PrismSplit: return "+2 ВИДИМЫХ БОКОВЫХ ЛУЧА";
                case ExpeditionUpgrade.FieldRepair: return "+2 К КОРПУСУ";
                default: return "БЛОК 2 ПОПАДАНИЙ";
            }
        }

        private int ExpeditionUpgradeRank(ExpeditionUpgrade upgrade)
        {
            switch (upgrade)
            {
                case ExpeditionUpgrade.RapidFire:
                    return Mathf.RoundToInt(Mathf.Log(expeditionFireIntervalMultiplier) / Mathf.Log(.82f));
                case ExpeditionUpgrade.PlasmaDrive:
                    return Mathf.RoundToInt(Mathf.Log(expeditionProjectileSpeedMultiplier) / Mathf.Log(1.18f));
                case ExpeditionUpgrade.ReactorAmplifier: return expeditionDamageBonus;
                case ExpeditionUpgrade.PrismSplit: return expeditionPrismLevel;
                case ExpeditionUpgrade.FieldRepair: return expeditionFieldRepairLevel;
                default: return expeditionAegisLevel;
            }
        }

        private static int ExpeditionUpgradeMaxRanks(ExpeditionUpgrade upgrade)
        {
            switch (upgrade)
            {
                case ExpeditionUpgrade.RapidFire: return 4;
                case ExpeditionUpgrade.PlasmaDrive: return 5;
                case ExpeditionUpgrade.ReactorAmplifier: return 6;
                case ExpeditionUpgrade.PrismSplit: return 2;
                case ExpeditionUpgrade.FieldRepair: return 3;
                default: return 3;
            }
        }

        private static string ExpeditionUpgradeAppliedLabel(ExpeditionUpgrade upgrade)
        {
            switch (upgrade)
            {
                case ExpeditionUpgrade.RapidFire: return "РАПИД-КАТУШКИ // -18% ПЕРЕЗАРЯДКА";
                case ExpeditionUpgrade.PlasmaDrive: return "ПЛАЗМА-ДРАЙВ // +18% СКОРОСТЬ";
                case ExpeditionUpgrade.ReactorAmplifier: return "РЕАКТОР x2 // +1 УРОН ВСЕМ ЛУЧАМ";
                case ExpeditionUpgrade.PrismSplit: return "ПРИЗМЕННЫЙ ВЕЕР // +2 БОКОВЫХ ЛУЧА";
                case ExpeditionUpgrade.FieldRepair: return "ПОЛЕВОЙ РЕМОНТ // +2 КОРПУСА";
                default: return "ЭГИДА-ФОРС // +2 БЛОКА УДАРА";
            }
        }

        private string ExpeditionLoadoutSummary()
        {
            if (!soloExpeditionPlaying) return string.Empty;
            var parts = new List<string>(5);
            if (expeditionFireIntervalMultiplier < .999f)
                parts.Add("РЕЙТ x" + (1f / expeditionFireIntervalMultiplier).ToString("0.0"));
            if (expeditionProjectileSpeedMultiplier > 1.001f)
                parts.Add("СКОР x" + expeditionProjectileSpeedMultiplier.ToString("0.0"));
            if (expeditionDamageBonus > 0) parts.Add("УРОН +" + expeditionDamageBonus);
            if (expeditionPrismLevel > 0) parts.Add("ВЕЕР +" + (expeditionPrismLevel * 2));
            if (expeditionAegisCharges > 0) parts.Add("ЭГИДА " + expeditionAegisCharges);
            return parts.Count == 0 ? "МОДУЛИ // НЕТ" : "МОДУЛИ // " + string.Join(" · ", parts);
        }

        private static Color ExpeditionUpgradeColor(ExpeditionUpgrade upgrade)
        {
            switch (upgrade)
            {
                case ExpeditionUpgrade.RapidFire: return new Color(1f, .76f, .24f);
                case ExpeditionUpgrade.PlasmaDrive: return new Color(.32f, .94f, 1f);
                case ExpeditionUpgrade.ReactorAmplifier: return new Color(1f, .40f, .76f);
                case ExpeditionUpgrade.PrismSplit: return new Color(.78f, .42f, 1f);
                case ExpeditionUpgrade.FieldRepair: return new Color(.34f, 1f, .68f);
                default: return new Color(.54f, .90f, 1f);
            }
        }

        private void UpdateCoopPreviewRelayCore(float dt)
        {
            if (!coopPreviewRelayCoreActive || coopPreviewCompleted || coopPreviewFailed) return;
            coopPreviewRelayCoreContactCooldown = Mathf.Max(0f,
                coopPreviewRelayCoreContactCooldown - Mathf.Max(0f, dt));
            PairedLensRules.Tick(ref coopPreviewRelayLensState, dt);
            var previousPosition = coopPreviewRelayCorePosition;
            CoopRelayCoreRules.Step(ref coopPreviewRelayCorePosition, ref coopPreviewRelayCoreVelocity, dt);
            if (coopPreviewLensesActive && PairedLensRules.TryTransit(ref coopPreviewRelayCorePosition,
                ref coopPreviewRelayCoreVelocity, previousPosition, PairedLensRules.RelayCoreRadius,
                ref coopPreviewRelayLensState, coopPreviewLensPair, PairedLensRules.RelayCoreCooldown,
                out var lensTransit))
                PlayPairedLensTransit(lensTransit, new Color(.48f, .92f, 1f), coopPreviewRelayCoreVelocity);

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
                var blocked=TryBlockLivingTempoDamage();
                if (!blocked) coopPreviewTeamHealth = Mathf.Max(0, coopPreviewTeamHealth - 1);
                coopPreviewRelayCoreDangerous = false;
                coopPreviewRelayCoreCharge = 0;
                coopPreviewRelayCoreEventKind = 4;
                coopPreviewRelayCoreEventPosition = shipPosition;
                coopPreviewRelayCoreEventSequence++;
                if (!blocked && coopPreviewTeamHealth == 0)
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
                if (pendingDamage > 0 && hitTarget && soloExpeditionPlaying && expeditionAegisCharges > 0)
                {
                    expeditionAegisCharges--;
                    pendingDamage = 0;
                    expeditionUpgradeNotice = "ЭГИДА ПОГЛОТИЛА УДАР // " + expeditionAegisCharges;
                    expeditionUpgradeNoticeTimer = 1.15f;
                    SpawnImpactBurst(player.position, new Color(.56f, .92f, 1f), 16, 2.1f, .28f);
                }
                if (pendingDamage > 0 && hitTarget && TryBlockLivingTempoDamage()) pendingDamage=0;
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

        private bool TryBlockLivingTempoDamage()
        {
            if (!LivingCosmosActive || livingTempo == null || !livingTempo.TryBlockDamage(expeditionAegisCharges)) return false;
            expeditionUpgradeNotice="КОНДЕНСАТОР ПОГЛОТИЛ УДАР";
            expeditionUpgradeNoticeTimer=1.15f;
            if (player != null) SpawnImpactBurst(player.position,new Color(.76f,.55f,1f),18,2.3f,.30f);
            HapticFeedback.Pulse(24);
            return true;
        }

        private void UpdateLivingTempo(float dt)
        {
            if (!LivingCosmosActive || livingTempo == null || livingCosmos.Phase != LivingEncounterPhase.Combat) return;
            if (!livingTempo.EncounterActive && coopPreviewEnemyHealth > 0)
            {
                var room=coopPreviewSector.Rooms[coopPreviewRoomIndex];
                livingTempo.BeginEncounter(coopPreviewRoomIndex,room.Type,TempoRewardRules.ReferenceMilliseconds(room.Type));
                livingTempoMillisecondRemainder=0f;
            }
            if (!livingTempo.EncounterActive) return;
            livingTempoMillisecondRemainder+=Mathf.Max(0f,dt)*1000f;
            var milliseconds=Mathf.FloorToInt(livingTempoMillisecondRemainder);
            if (milliseconds>0)
            {
                livingTempoMillisecondRemainder-=milliseconds;
                livingTempo.AdvanceCombat(milliseconds,false,coopPreviewEnemyHealth>0);
            }
            if (coopPreviewEnemyHealth>0) return;
            var result=livingTempo.FinishEncounter(coopPreviewRoomIndex,true,coopPreviewTeamHealth>0);
            if (result==null) return;
            if (result.Granted && livingCosmos.BeginReward())
            {
                expeditionUpgradeNotice="ИМПУЛЬ НАЙДЕН · ВЫБЕРИ МОДУЛЬ";
                expeditionUpgradeNoticeTimer=99f;
                SaveLivingRewardCheckpoint();
                HapticFeedback.Pulse(35);
            }
            else if (!result.Granted)
            {
                expeditionUpgradeNotice="ИМПУЛЬ НЕ НАЙДЕН · ШАНС НАКОПЛЕН";
                expeditionUpgradeNoticeTimer=2.1f;
            }
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
                var damage = Mathf.Max(1, Mathf.RoundToInt(ElementalCombat.ApplyResistance(loadout.DamageMultiplier, resistance))) +
                    (soloExpeditionPlaying ? expeditionDamageBonus : 0);
                var speed = BalanceSettings.PlayerProjectileSpeed(1) * loadout.ProjectileSpeedMultiplier *
                    (soloExpeditionPlaying ? expeditionProjectileSpeedMultiplier : 1f);
                if (LivingCosmosActive) speed=WeaponStatsResolver.ResolveTempo(.3f,speed,livingTempo).ProjectileSpeed;
                // Expedition fire is physical and radial: shots leave the ship and
                // travel toward the centre. They never steer toward an enemy.
                var primaryShot = CoopPlayerShotRules.Create(origin, Vector2.zero, speed, loadout.Element, damage);
                coopPreviewPlayerShots.Add(primaryShot);
                // Each Prism rank creates a clearly legible, symmetric pair. The
                // simulation and visual emission use this same layout.
                var prismRanks = soloExpeditionPlaying ? expeditionPrismLevel : 0;
                for (var prismRank = 1; prismRank <= prismRanks; prismRank++)
                {
                    var spread = 15f + (prismRank - 1) * 13f;
                    for (var side = -1; side <= 1; side += 2)
                    {
                        var splitShot = primaryShot;
                        splitShot.Velocity = Rotate(primaryShot.Velocity.normalized, side * spread) *
                            primaryShot.Velocity.magnitude;
                        coopPreviewPlayerShots.Add(splitShot);
                    }
                }
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
                var previousPosition = shot.Position;
                CoopPlayerShotRules.StepMotion(ref shot, deltaTime);
                var lensState = new PairedLensTransitState { Passes = shot.LensPasses, Cooldown = shot.LensCooldown };
                var lensTransit = default(PairedLensTransitEvent);
                var passedLens = shot.Life > 0f && coopPreviewLensesActive && PairedLensRules.TryTransit(ref shot.Position,
                    ref shot.Velocity, previousPosition, PairedLensRules.PlayerShotRadius, ref lensState,
                    coopPreviewLensPair, PairedLensRules.PlayerShotCooldown, out lensTransit);
                shot.LensPasses = lensState.Passes;
                shot.LensCooldown = lensState.Cooldown;
                if (passedLens) PlayPairedLensTransit(lensTransit, CoopElementColor(shot.Element), shot.Velocity);
                // A lens transit consumes this frame's collision segment. Otherwise a shot
                // could score against an enemy that was only reached on its pre-teleport path.
                var hit = !passedLens && CoopTetherRules.DistanceToSegment(enemyPosition, previousPosition,
                    shot.Position) <= CoopPlayerShotRules.HitRadius(roomType);
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

        private void PlayPairedLensTransit(PairedLensTransitEvent transit, Color color, Vector2 exitVelocity)
        {
            SpawnImpactBurst(transit.EntryPoint, color, 10, 1.85f, .22f);
            SpawnImpactBurst(transit.ExitPoint, Color.Lerp(color, Color.white, .35f), 14, 2.35f, .28f);
            EmitPairedLensExitShot(transit.ExitPoint, exitVelocity, color);
            if (coopLensSoundCooldown > 0f) return;
            coopLensSoundCooldown = .12f;
            PlayEffect(coopRicochetSound, .34f);
        }

        private void EmitPairedLensExitShot(Vector2 position, Vector2 velocity, Color color)
        {
            if (velocity.sqrMagnitude < .001f) return;
            var shot = projectilePool.Get();
            shot.SetVisual(projectileSprite != null ? projectileSprite : whiteSprite, projectileSprite != null, false);
            shot.ResetProjectile(position + velocity.normalized * .08f, velocity, true, color, DamageElement.Kinetic, 0f);
            shot.VisualOnly = true;
            // Keep the teleported visual long enough to cross the whole arena. It
            // is VisualOnly, so this cannot add damage or collide with anything.
            shot.Life = CoopPlayerShotRules.Lifetime;
            projectiles.Add(shot);
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
            // Preserve the generated final boss artwork instead of tinting its
            // silhouette red. Other room types remain colour-coded.
            if (roomType == SectorRoomType.Boss && bossSprite != null)
                renderer.color = Color.white;
            else
            {
                var color = SectorRoomColor(roomType);
                color.a = .95f;
                renderer.color = color;
            }
            SetSpriteWorldSize(renderer, roomType == SectorRoomType.Boss ? 1.18f :
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
                var interval=BalanceSettings.PlayerFireInterval(1, false) *
                    ShipLoadoutSettings.Get(CoopHostShip()).FireIntervalMultiplier *
                    (soloExpeditionPlaying ? expeditionFireIntervalMultiplier : 1f);
                if (LivingCosmosActive) interval=WeaponStatsResolver.ResolveTempo(interval,1f,livingTempo).FireInterval;
                coopPreviewHostFireTimer += interval;
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
            var speed = BalanceSettings.PlayerProjectileSpeed(1) * loadout.ProjectileSpeedMultiplier *
                (soloExpeditionPlaying ? expeditionProjectileSpeedMultiplier : 1f);
            if (LivingCosmosActive) speed=WeaponStatsResolver.ResolveTempo(.3f,speed,livingTempo).ProjectileSpeed;
            for (var i = 0u; i < count; i++)
            {
                // Match the simulation: expedition shots always fly inward from the
                // ship to the centre. No homing or magnetic correction is applied.
                var aim = -(Vector2)ship.position;
                if (aim.sqrMagnitude < .001f) aim = Vector2.up;
                var direction = aim.normalized;
                Shoot(ship.position, direction * speed, true,
                    loadout.ProjectileColor, loadout.Element, loadout.DamageMultiplier);
                var prismRanks = soloExpeditionPlaying ? expeditionPrismLevel : 0;
                for (var prismRank = 1; prismRank <= prismRanks; prismRank++)
                {
                    var spread = 15f + (prismRank - 1) * 13f;
                    for (var side = -1; side <= 1; side += 2)
                        Shoot(ship.position, Rotate(direction, side * spread) * speed, true,
                            loadout.ProjectileColor, loadout.Element, loadout.DamageMultiplier);
                }
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
                HideCoopThreatPatternVisuals();
                return;
            }
            coopThreatPatternVisualTimer = Mathf.Max(0f, coopThreatPatternVisualTimer - Mathf.Max(0f, deltaTime));
            var progress = 1f - coopThreatPatternVisualTimer / Mathf.Max(.01f, coopThreatPatternVisualDuration);
            var alpha = Mathf.Clamp01(Mathf.Min(1f, progress * 5f) * Mathf.Min(1f, (1f - progress) * 4.25f));
            var color = new Color(coopThreatVisualColor.r, coopThreatVisualColor.g, coopThreatVisualColor.b, alpha);
            var direction = coopThreatVisualTarget - coopThreatVisualOrigin;
            if (direction.sqrMagnitude < .001f) direction = Vector2.down;
            direction.Normalize();
            var perpendicular = new Vector2(-direction.y, direction.x);

            if (coopThreatVisualPattern == CoopThreatPattern.Cleave)
            {
                coopThreatCleaveRenderer.gameObject.SetActive(true);
                coopThreatCleaveCoreRenderer.gameObject.SetActive(true);
                coopThreatCleaveEchoRenderer.gameObject.SetActive(true);
                var travel = Mathf.SmoothStep(0f, 1f, progress);
                var center = Vector2.Lerp(coopThreatVisualOrigin, coopThreatVisualTarget, travel);
                var pulse = .88f + Mathf.Sin(Time.unscaledTime * 24f + progress * 9f) * .12f;
                var glowColor = Color.Lerp(new Color(.06f, .32f, 1f, alpha * .52f), color, .62f);
                var coreColor = Color.Lerp(Color.white, color, .28f);
                coreColor.a = alpha * pulse;
                var echoColor = Color.Lerp(new Color(.88f, .22f, 1f, alpha * .36f), color, .42f);
                coopThreatCleaveRenderer.startColor = coopThreatCleaveRenderer.endColor = glowColor;
                coopThreatCleaveCoreRenderer.startColor = coopThreatCleaveCoreRenderer.endColor = coreColor;
                coopThreatCleaveEchoRenderer.startColor = coopThreatCleaveEchoRenderer.endColor = echoColor;
                coopThreatCleaveRenderer.startWidth = coopThreatCleaveRenderer.endWidth = .34f * pulse;
                coopThreatCleaveCoreRenderer.startWidth = coopThreatCleaveCoreRenderer.endWidth = .072f * pulse;
                coopThreatCleaveEchoRenderer.startWidth = coopThreatCleaveEchoRenderer.endWidth = .16f;
                DrawCleaveWave(coopThreatCleaveRenderer, center, direction, perpendicular, .98f, .34f, progress, 0f);
                DrawCleaveWave(coopThreatCleaveCoreRenderer, center + direction * .018f, direction, perpendicular, .79f, .30f, progress, .21f);
                DrawCleaveWave(coopThreatCleaveEchoRenderer, center - direction * (.18f + progress * .11f), direction, perpendicular, .72f, .25f, progress, .47f);
                if (coopThreatCleaveTrail != null)
                {
                    coopThreatCleaveTrail.gameObject.SetActive(true);
                    coopThreatCleaveTrail.transform.position = center - direction * .06f;
                }
                UpdateCleaveEnergyNodes(center, direction, perpendicular, progress, color, alpha, pulse);
                coopThreatCleaveSparkTimer -= Mathf.Max(0f, deltaTime);
                if (coopThreatCleaveSparkTimer <= 0f)
                {
                    coopThreatCleaveSparkTimer = .055f;
                    SpawnCleaveWaveSparks(center, direction, perpendicular, progress, color);
                }
            }
            else if (coopThreatVisualPattern == CoopThreatPattern.RingGate)
            {
                coopThreatRingLeftRenderer.gameObject.SetActive(true);
                coopThreatRingCenterRenderer.gameObject.SetActive(true);
                coopThreatRingRightRenderer.gameObject.SetActive(true);
                coopThreatRingLeftRenderer.startColor = coopThreatRingLeftRenderer.endColor = color;
                coopThreatRingCenterRenderer.startColor = coopThreatRingCenterRenderer.endColor = color;
                coopThreatRingRightRenderer.startColor = coopThreatRingRightRenderer.endColor = color;
                var radius = Mathf.Lerp(.2f, 5.9f, Mathf.SmoothStep(0f, 1f, progress));
                const float gapHalfAngle = 20f;
                DrawThreatArc(coopThreatRingLeftRenderer, coopThreatVisualOrigin, radius,
                    coopThreatVisualPatternAngle + gapHalfAngle, coopThreatVisualPatternAngle + 120f - gapHalfAngle);
                DrawThreatArc(coopThreatRingCenterRenderer, coopThreatVisualOrigin, radius,
                    coopThreatVisualPatternAngle + 120f + gapHalfAngle, coopThreatVisualPatternAngle + 240f - gapHalfAngle);
                DrawThreatArc(coopThreatRingRightRenderer, coopThreatVisualOrigin, radius,
                    coopThreatVisualPatternAngle + 240f + gapHalfAngle, coopThreatVisualPatternAngle + 360f - gapHalfAngle);
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

        private void HideCoopThreatPatternVisuals()
        {
            if (coopThreatCleaveRenderer != null) coopThreatCleaveRenderer.gameObject.SetActive(false);
            if (coopThreatCleaveCoreRenderer != null) coopThreatCleaveCoreRenderer.gameObject.SetActive(false);
            if (coopThreatCleaveEchoRenderer != null) coopThreatCleaveEchoRenderer.gameObject.SetActive(false);
            if (coopThreatCleaveTrail != null)
            {
                coopThreatCleaveTrail.gameObject.SetActive(false);
                coopThreatCleaveTrail.Clear();
            }
            if (coopThreatRingLeftRenderer != null) coopThreatRingLeftRenderer.gameObject.SetActive(false);
            if (coopThreatRingCenterRenderer != null) coopThreatRingCenterRenderer.gameObject.SetActive(false);
            if (coopThreatRingRightRenderer != null) coopThreatRingRightRenderer.gameObject.SetActive(false);
            for (var i = 0; i < coopThreatMineMarkers.Count; i++)
                if (coopThreatMineMarkers[i] != null) coopThreatMineMarkers[i].gameObject.SetActive(false);
            for (var i = 0; i < coopThreatCleaveNodes.Count; i++)
                if (coopThreatCleaveNodes[i] != null) coopThreatCleaveNodes[i].gameObject.SetActive(false);
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

        private static void DrawCleaveWave(LineRenderer line, Vector2 center, Vector2 direction,
            Vector2 perpendicular, float span, float bend, float progress, float phaseOffset)
        {
            for (var i = 0; i < line.positionCount; i++)
            {
                var side = i / (float)(line.positionCount - 1) * 2f - 1f;
                line.SetPosition(i, CleaveWavePoint(center, direction, perpendicular, side, span, bend,
                    progress, phaseOffset));
            }
        }

        private static Vector2 CleaveWavePoint(Vector2 center, Vector2 direction, Vector2 perpendicular,
            float side, float span, float bend, float progress, float phaseOffset)
        {
            var body = 1f - side * side;
            var ripple = Mathf.Sin((side * 3.5f + progress * 3.8f + phaseOffset) * Mathf.PI) * .075f * body;
            var slashCurve = body * bend + ripple;
            return center + perpendicular * (side * span) + direction * slashCurve;
        }

        private void UpdateCleaveEnergyNodes(Vector2 center, Vector2 direction, Vector2 perpendicular,
            float progress, Color color, float alpha, float pulse)
        {
            for (var i = 0; i < coopThreatCleaveNodes.Count; i++)
            {
                var node = coopThreatCleaveNodes[i];
                if (node == null) continue;
                var side = i / (float)(coopThreatCleaveNodes.Count - 1) * 2f - 1f;
                node.gameObject.SetActive(true);
                node.transform.position = CleaveWavePoint(center, direction, perpendicular, side,
                    .93f, .33f, progress, .12f);
                node.transform.localScale = Vector3.one * (.046f + (1f - Mathf.Abs(side)) * .065f * pulse);
                var nodeColor = Color.Lerp(Color.white, color, .32f + Mathf.Abs(side) * .32f);
                nodeColor.a = alpha * (.58f + (1f - Mathf.Abs(side)) * .38f);
                node.color = nodeColor;
            }
        }

        private void SpawnCleaveWaveLaunch(Vector2 origin, Vector2 target, Color color)
        {
            var direction = target - origin;
            if (direction.sqrMagnitude < .001f) direction = Vector2.down;
            direction.Normalize();
            var perpendicular = new Vector2(-direction.y, direction.x);
            SpawnCleaveWaveSparks(origin + direction * .16f, direction, perpendicular, 0f, color, 9);
        }

        private void SpawnCleaveWaveSparks(Vector2 center, Vector2 direction, Vector2 perpendicular,
            float progress, Color color, int amount = 3)
        {
            if (damageShardPool == null || damageShards.Count >= 96) return;
            for (var i = 0; i < amount && damageShards.Count < 96; i++)
            {
                var side = amount == 1 ? 0f : i / (float)(amount - 1) * 2f - 1f;
                side = Mathf.Clamp(side + Random.Range(-.12f, .12f), -1f, 1f);
                var point = CleaveWavePoint(center, direction, perpendicular, side, .92f, .32f,
                    progress, Random.Range(0f, 1f));
                var outward = (perpendicular * Mathf.Sign(side == 0f ? Random.value - .5f : side) +
                    direction * Random.Range(.22f, .75f)).normalized;
                var shard = damageShardPool.Get();
                var sparkColor = Color.Lerp(Color.white, color, Random.Range(.22f, .72f));
                sparkColor.a = Random.Range(.6f, .95f);
                shard.ResetShard(point, outward * Random.Range(1.6f, 3.6f), Random.Range(.018f, .047f),
                    sparkColor, Random.Range(.13f, .27f));
                shard.Renderer.sortingOrder = 19;
                damageShards.Add(shard);
            }
        }

        private void UpdateCoopTrajectory(float trajectoryTime)
        {
            if (coopTrajectoryRenderer == null) return;
            coopTrajectoryRenderer.enabled = true;
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
            // The flat white-sprite wash had visible rectangular bounds over the arena.
            if (coopRoomWash != null) coopRoomWash.enabled = false;
            if (LivingCosmosActive)
            {
                foreach (var motif in coopRoomMotifs) if (motif != null) motif.enabled = false;
                return; // new region atmosphere is entirely procedural, no coloured quads
            }
            foreach (var motif in coopRoomMotifs) if (motif != null) motif.enabled = true;
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
            if (canvasUi == null) UpdateCanvasUi();
            if (canvasUi == null) return;
            if (expeditionModeChoice == null)
            {
                canvasUi.EnsureStructure();
                expeditionModeChoice = canvasUi.ExpeditionModeChoice;
                expeditionModeChoice.Selected -= BeginSoloExpeditionRun;
                expeditionModeChoice.Selected += BeginSoloExpeditionRun;
                expeditionModeChoice.ResumeLivingRequested -= ResumeLivingCosmosRun;
                expeditionModeChoice.ResumeLivingRequested += ResumeLivingCosmosRun;
            }
            expeditionModeChoice.Show();
            if (livingCheckpointStore == null) livingCheckpointStore = new LivingCosmosCheckpointStore();
            expeditionModeChoice.SetLivingCheckpointAvailable(livingCheckpointStore.HasCheckpoint);
        }

        private void BeginSoloExpeditionRun(bool experimental)
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
            if (experimental)
            {
                if (livingCheckpointStore == null) livingCheckpointStore = new LivingCosmosCheckpointStore();
                livingCheckpointStore.Clear();
            }
            BeginCoopRun(true, true, experimental);
        }

        private void ResumeLivingCosmosRun()
        {
            if (livingCheckpointStore == null) livingCheckpointStore = new LivingCosmosCheckpointStore();
            if (!livingCheckpointStore.TryLoad(out var checkpoint)) return;
            try { BeginCoopRun(true,true,true,checkpoint); }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Living Cosmos checkpoint rejected: " + exception.Message);
                livingCheckpointStore.Clear();
            }
        }

        private void BeginDefenseMode()
        {
            playerNickname = SanitizeNickname(playerNickname);
            if (string.IsNullOrEmpty(playerNickname))
            {
                nicknameError = "ВВЕДИ ПОЗЫВНОЙ ДЛЯ ЗАЩИТЫ ФЛАГМАНА";
                showSettings = true;
                BeginUiFade();
                return;
            }

            nicknameError = string.Empty;
            PlayerPrefs.SetString("orbital_rift_nickname", playerNickname);
            PlayerPrefs.Save();
            // Domain reload during an in-editor test can leave a live manager
            // without its runtime pools. Recreate them instead of throwing on
            // the first defense spawn.
            if (enemyPool == null || projectilePool == null || starPool == null || damageShardPool == null) CreatePools();
            Cleanup();
            coopPlaying = false;
            soloExpeditionPlaying = false;
            defensePlaying = true;
            defenseRunOver = false;
            playing = true;
            SetPaused(false);
            showMenu = false;
            showCoop = false;
            showSettings = false;
            showResults = false;
            coreActive = false;
            bossSpawnPending = false;
            splitShot = false;
            tripleShotTimer = 0f;
            score = 0;
            phase = 1;
            defenseMaxHull = GameRules.Current.DefenseHull;
            defenseHull = defenseMaxHull;
            defenseWave = 0;
            defenseSpawnsLeft = 0;
            defenseKills = 0;
            defenseSpawnTimer = .65f;
            defenseIntermissionTimer = .7f;
            defenseFlagshipPulse = 0f;
            ResetDefenseFlagshipDamage();
            defenseStatus = "ПРИБЛИЖЕНИЕ К ФЛАГМАНУ";
            // Start away from the lower flagship so the pilot silhouette and
            // the objective never overlap on the opening frame.
            playerAngle = Mathf.PI * .83f;
            targetAngle = playerAngle;
            PositionOnOrbit();
            fireTimer = .1f;
            playerCommandSource?.Reset();
            touchHintTimer = 4f;
            if (defenseFlagship != null)
            {
                defenseFlagship.position = DefenseFlagshipPosition;
                defenseFlagship.rotation = Quaternion.identity;
                SetSpriteWorldSize(defenseFlagship.GetComponent<SpriteRenderer>(), DefenseFlagshipWorldSize);
                defenseFlagship.gameObject.SetActive(true);
            }
            if (defenseFlagshipGlow != null)
            {
                defenseFlagshipGlow.position = DefenseFlagshipPosition;
                defenseFlagshipGlow.localScale = Vector3.one * DefenseFlagshipGlowWorldSize;
                defenseFlagshipGlow.gameObject.SetActive(true);
            }
            SetDefenseFlagshipEffectsActive(true);
            if (player != null) player.gameObject.SetActive(true);
            if (GameAudioSettings.MusicEnabled && musicSource != null && musicSource.clip != null) musicSource.Play();
            phaseUpgradeBannerTimer = 1.9f;
            phaseUpgradeLabel = "РЕЖИМ ЗАЩИТЫ\nНЕ ДАЙ ВРАГАМ ДОЙТИ ДО ФЛАГМАНА";
            SpawnWarpBurst(48, 1.8f);
            BeginUiFade();
        }

        private void UpdateDefenseMode(float dt)
        {
            if (defenseFlagship == null) return;
            defenseFlagshipPulse = Mathf.Max(0f, defenseFlagshipPulse - dt);
            UpdateDefenseFlagshipEffects(dt);
            var glowRenderer = defenseFlagshipGlow == null ? null : defenseFlagshipGlow.GetComponent<SpriteRenderer>();
            if (glowRenderer != null)
            {
                var pulse = .92f + Mathf.Sin(Time.unscaledTime * 5.5f) * .08f;
                defenseFlagshipGlow.position = DefenseFlagshipPosition;
                defenseFlagshipGlow.localScale = Vector3.one * (DefenseFlagshipGlowWorldSize * pulse);
                var damageTint = defenseFlagshipPulse > 0f ? new Color(1f, .22f, .34f, .42f) : new Color(.16f, .78f, 1f, .18f);
                glowRenderer.color = damageTint;
            }

            if (defenseIntermissionTimer > 0f)
            {
                defenseIntermissionTimer -= dt;
                if (defenseIntermissionTimer <= 0f) BeginDefenseWave();
                return;
            }

            if (defenseSpawnsLeft > 0)
            {
                defenseSpawnTimer -= dt;
                if (defenseSpawnTimer <= 0f)
                {
                    SpawnDefenseEnemy();
                    defenseSpawnsLeft--;
                    defenseSpawnTimer = Mathf.Max(GameRules.Current.DefenseSpawnMinimum, GameRules.Current.DefenseSpawnInterval - defenseWave * GameRules.Current.DefenseSpawnReduction);
                }
            }

            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if(enemy.Kind==EnemyKind.ShadeClone){enemy.FireTimer-=dt;enemy.Life-=dt;UpdateHarrierClone(enemy,dt);if(enemy.Life<=0)RemoveEnemy(i);continue;}
                var position = (Vector2)enemy.transform.position;
                var impactPoint = DefenseFlagshipImpactPoint(position);
                var toFlagship = impactPoint - position;
                var distance = toFlagship.magnitude;
                if (distance <= DefenseFlagshipHitRadius)
                {
                    DamageDefenseFlagship(position);
                    RemoveEnemy(i);
                    if (defenseRunOver) break;
                    continue;
                }
                var direction = toFlagship / Mathf.Max(.001f, distance);
                var perpendicular = new Vector2(-direction.y, direction.x);
                var m=enemy.Mob;
                var laneWobble=m!=null?Mathf.Sin(Time.time*m.DefenseWobbleFrequency+enemy.Angle)*m.DefenseWobble:0;
                var approachSpeed=m!=null?m.DefenseSpeed+defenseWave*m.DefenseSpeedPerWave:.52f;
                enemy.Angle+=dt*(m!=null?m.DefenseAngularSpeed:.75f);
                enemy.FireTimer-=dt;
                if(enemy.ActiveAbility==null&&m!=null&&m.ShootInDefense&&m.Shot!=null&&enemy.FireTimer<=0){FireConfiguredBossShot(enemy,m.Shot);enemy.FireTimer=m.Shot.FireInterval;}
                TickMobVisualsAndSpells(enemy,dt,true);
                enemy.transform.position = position + (direction * approachSpeed + perpendicular * laneWobble) * dt;
                enemy.Radius = distance;
            }

            if (defenseSpawnsLeft == 0 && enemies.Count == 0 && !defenseRunOver)
            {
                defenseIntermissionTimer = GameRules.Current.DefenseIntermission;
                defenseStatus = "ПЕРИМЕТР ЧИСТ // ГОТОВЬСЯ";
                phaseUpgradeBannerTimer = .9f;
                phaseUpgradeLabel = "ПЕРИМЕТР ЧИСТ";
            }
        }

        private void BeginDefenseWave()
        {
            defenseWave++;
            phase = defenseWave;
            configuredSpawnOrdinal=0;
            var step=GameRules.Current.Defense.Step(defenseWave);
            defenseSpawnsLeft = step.BaseCount + defenseWave * step.CountPerPhase;
            defenseSpawnTimer = .18f;
            defenseStatus = "ВОЛНА " + defenseWave + " // " + defenseSpawnsLeft + " КОНТАКТОВ";
            if (defenseWave > 1 && defenseWave % GameRules.Current.DefenseRepairEvery == 1)
            {
                defenseHull = Mathf.Min(defenseMaxHull, defenseHull + 1);
                RepairMostDamagedFlagshipSection();
                defenseStatus += " // РЕМОНТ +1";
            }
            phaseUpgradeBannerTimer = 1.2f;
            phaseUpgradeLabel = defenseStatus;
            SpawnImpactBurst(Vector2.zero, new Color(.35f, .92f, 1f), 18, 2.1f, .32f);
        }

        private static Vector2 DefenseFlagshipImpactPoint(Vector2 fromPosition)
        {
            // Spread incoming contacts across the raised ends of the hull as
            // well as its central keel, matching the wide U silhouette.
            var localX = Mathf.Clamp((fromPosition.x - DefenseFlagshipPosition.x) * .62f,
                -DefenseFlagshipHalfWidth * .78f, DefenseFlagshipHalfWidth * .78f);
            var normalizedX = localX / DefenseFlagshipHalfWidth;
            var arcHeight = .38f + normalizedX * normalizedX * .62f;
            return DefenseFlagshipPosition + new Vector2(localX, arcHeight);
        }

        private void SpawnDefenseEnemy()
        {
            if (enemyPool == null) return;
            var step = GameRules.Current.Defense.Step(defenseWave);
            var enemy = SpawnConfiguredMob(step.Pick(defenseWave, configuredSpawnOrdinal++), true);
            // Contacts emerge around the central rift and commit to a visible
            // attack line toward the large flagship at the bottom of the field.
            var spawnPosition = new Vector2(Random.Range(-2.55f, 2.55f), Random.Range(-.15f, 3.75f));
            if (Vector2.Distance(spawnPosition, DefenseFlagshipPosition) < 3f)
                spawnPosition.y = Random.Range(1.25f, 3.75f);
            enemy.Radius = Vector2.Distance(spawnPosition, DefenseFlagshipPosition);
            enemy.Life = enemy.Mob.Lifetime;
            enemy.FireTimer = enemy.Mob != null && enemy.Mob.Shot != null ? enemy.Mob.Shot.FireInterval : 999f;
            enemy.transform.position = spawnPosition;
        }

        private void DamageDefenseFlagship(Vector2 impactPosition)
        {
            defenseHull = Mathf.Max(0, defenseHull - 1);
            var section = DefenseFlagshipSectionFor(impactPosition);
            defenseFlagshipSectionDamage[section] = Mathf.Min(3, defenseFlagshipSectionDamage[section] + 1);
            defenseFlagshipSectionFlash[section] = .36f;
            defenseFlagshipPulse = .35f;
            hpFlashTimer = .35f;
            SpawnImpactBurst(impactPosition, new Color(1f, .22f, .34f), 20, 2.8f, .38f);
            SpawnImpactBurst(DefenseFlagshipPosition + DefenseFlagshipSectionOffsets[section],
                new Color(1f, .70f, .24f), 9, 1.35f, .25f);
            PlayEffect(playerDamageSound, .78f);
            AddScreenShake(.24f, .14f);
            HapticFeedback.Pulse(defenseHull == 0 ? 105 : 45);
            defenseStatus = defenseHull > 0 ? "ФЛАГМАН ПОЛУЧИЛ УРОН // -1 КОРПУС" : "ФЛАГМАН УНИЧТОЖЕН";
            if (defenseHull > 0) return;

            defenseRunOver = true;
            playing = false;
            StopGameplayMusic();
            phaseUpgradeBannerTimer = 1.55f;
            phaseUpgradeLabel = "ФЛАГМАН УНИЧТОЖЕН";
        }

        private void ExitDefenseMode()
        {
            defensePlaying = false;
            defenseRunOver = false;
            playing = false;
            SetPaused(false);
            Cleanup();
            if (defenseFlagship != null) defenseFlagship.gameObject.SetActive(false);
            if (defenseFlagshipGlow != null) defenseFlagshipGlow.gameObject.SetActive(false);
            SetDefenseFlagshipEffectsActive(false);
            player.gameObject.SetActive(true);
            showMenu = true;
            showResults = false;
            activeControlDirection = 0;
            StopGameplayMusic();
            BeginUiFade();
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
            var discardedLiving=LivingCosmosActive;
            SetPaused(false);
            defensePlaying = false;
            defenseRunOver = false;
            coopPlaying = false;
            coopLocalPreview = false;
            soloExpeditionPlaying = false;
            UpdateCameraFraming(true);
            coopSimulation?.ResetLocalRunState();
            if (discardedLiving)
            {
                if (livingCheckpointStore == null) livingCheckpointStore = new LivingCosmosCheckpointStore();
                livingCheckpointStore.Clear();
            }
            Cleanup();
            SetCoopVisualsActive(false);
            if (defenseFlagship != null) defenseFlagship.gameObject.SetActive(false);
            if (defenseFlagshipGlow != null) defenseFlagshipGlow.gameObject.SetActive(false);
            SetDefenseFlagshipEffectsActive(false);
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

        private void OpenAbilitySandbox()
        {
            ResetSandboxMechanics();
            // The sandbox starts from the menu and owns an isolated, no-score world.
            // Clean pooled combat objects only; never touch player progression or save data.
            Cleanup();
            ResetOrbitalAbilityState();
            sandboxVoidBeamTimer = 0f;
            sandboxVoidRootTimer = 0f;
            sandboxBlackHoleTimer = 0f;
            sandboxBlackHolePresentation?.SetVisible(false);
            sandboxPreviousTimeScale = Time.timeScale;
            abilitySandbox.Open();
            musicReactiveVisuals?.SetSandboxTimeMode(true);
            Time.timeScale = abilitySandbox.PreviewTimeScale;
            sandboxAutoFireBefore = autoFire;
            autoFire = false;
            // Keep the normal orbit loop, world render and combat HUD alive.  The sandbox
            // intercepts spawning and boss AI below, so this is the familiar classic flight
            // without score progression or incoming damage.
            playing = true;
            defensePlaying = false;
            defenseRunOver = false;
            coopPlaying = false;
            coopLocalPreview = false;
            soloExpeditionPlaying = false;
            showMenu = false;
            showSettings = false;
            showCoop = false;
            showResults = false;
            showRoomGuide = false;
            showRankGuide = false;
            SetPaused(false);
            coreActive = false;
            bossSpawnPending = false;
            phase = 3;
            shields = 3;
            starShields = 0;
            playerAngle = -Mathf.PI * .5f;
            targetAngle = playerAngle;
            activeControlDirection = 0;
            if (player != null)
            {
                player.gameObject.SetActive(true);
                PositionOnOrbit();
            }
            CreateAbilitySandboxBoss();
            InitializeSandboxMechanics();
            BeginUiFade();
        }

        private void CreateAbilitySandboxBoss()
        {
            sandboxBoss = enemyPool.Get();
            var sprite = BossSpriteFor(BossArchetype.AstralFirebird);
            sandboxBoss.ResetEnemy(EnemyKind.Boss, 0f, phase, sprite);
            sandboxBoss.BossType = BossArchetype.AstralFirebird;
            sandboxBoss.Definition = BossAssetRegistry.Get(BossArchetype.AstralFirebird);
            sandboxBoss.BossMainSprite = sprite;
            sandboxBoss.Health = sandboxBoss.MaxHealth = 9999f;
            sandboxBoss.Radius = 0f;
            sandboxBoss.Angle = 0f;
            sandboxBoss.FireTimer = 999f;
            sandboxBoss.BossState = BossAiState.Orbit;
            sandboxBoss.BossStateTimer = 999f;
            sandboxBoss.transform.position = Vector3.zero;
            if (sandboxBoss.BossPresentation != null) sandboxBoss.BossPresentation.SetVisible(false);
            if (sandboxBoss.HarrierPresentation != null) sandboxBoss.HarrierPresentation.SetVisible(false);
            if (sandboxBoss.FirebirdPresentation == null) sandboxBoss.FirebirdPresentation = sandboxBoss.gameObject.AddComponent<FirebirdBossPresentation>();
            if (sandboxBoss.BossPresentation == null) sandboxBoss.BossPresentation = sandboxBoss.gameObject.AddComponent<VoidMawBossPresentation>();
            sandboxBoss.FirebirdPresentation.Configure(arena, circleSprite != null ? circleSprite : whiteSprite, sandboxBoss.Definition.Presentation);
            sandboxBoss.BossPresentation.Configure(arena, circleSprite != null ? circleSprite : whiteSprite);
            sandboxBoss.BossPresentation.SetVisible(false);
            if (sandboxBlackHolePresentation == null)
                sandboxBlackHolePresentation = GetComponent<SandboxBlackHolePresentation>() ?? gameObject.AddComponent<SandboxBlackHolePresentation>();
            sandboxBlackHolePresentation.Configure(arena, circleSprite != null ? circleSprite : whiteSprite);
            sandboxBlackHolePresentation.SetVisible(false);
            sandboxBoss.FirebirdPresentation.SetVisible(true);
            sandboxBoss.FirebirdPresentation.Render(sandboxBoss, false);
            enemies.Add(sandboxBoss);
        }

        private void UpdateAbilitySandbox(float dt, PlayerCommandFrame command)
        {
            ConsumeSandboxVfxEditorCommit();
            if (abilitySandbox.ConsumeCloseRequest())
            {
                ExitAbilitySandbox();
                return;
            }

            abilitySandbox.Tick(dt);
            if (abilitySandbox.ConsumeCloseRequest())
            {
                ExitAbilitySandbox();
                return;
            }
            UpdateOrbitalAbilityTimers(dt);
            var sandboxDirection = abilitySandbox.LoadoutOpen || abilitySandbox.VfxEditorOpen ? 0 : command.OrbitDirection;
            if (abilitySandbox.PointerOverControls() && Mathf.Abs(Input.GetAxisRaw("Horizontal")) < .01f) sandboxDirection = 0;
            if (sandboxReverseTimer > 0f) sandboxDirection = -sandboxDirection;
            UpdateInput(dt, sandboxDirection);
            if (!abilitySandbox.LoadoutOpen && !abilitySandbox.VfxEditorOpen && command.ToggleAutoFire)
                autoFire = !autoFire;
            UpdatePlayer(dt);
            if (!abilitySandbox.LoadoutOpen && !abilitySandbox.VfxEditorOpen && (command.UseRiftEcho || queuedRiftEcho))
            {
                queuedRiftEcho = false;
                TriggerAbilitySandboxSpell(AbilitySandboxAbilityId.RiftEcho);
            }
            if (!abilitySandbox.LoadoutOpen && !abilitySandbox.VfxEditorOpen && (command.UseVectorSnap || queuedVectorSnap))
            {
                queuedVectorSnap = false;
                TriggerAbilitySandboxSpell(AbilitySandboxAbilityId.VectorSnap);
            }
            if (abilitySandbox.TryConsumeActivation(out var ability)) TriggerAbilitySandboxSpell(ability);
            UpdateRiftEcho(dt);
            UpdateAbilitySandboxProjectiles(dt);
            if (!abilitySandbox.BossVisible) ClearSandboxBossShards();
            UpdateAbilitySandboxHarrierClones(dt);

            // Apply workshop isolation before touching the optional mannequin. The editor can
            // remain open while the pooled boss is absent (for example after a reload), and
            // an early return here must never leave the classic arena, stars, or projectiles
            // visible behind the clean animation plate.
            SetAbilityVfxWorkshopIsolation(abilitySandbox.VfxEditorOpen);
            if (player != null)
                player.gameObject.SetActive(!abilitySandbox.VfxEditorOpen);

            if (sandboxBoss != null)
            {
                if (abilitySandbox.VfxEditorOpen || !abilitySandbox.BossVisible)
                {
                    // The workshop preview must show the selected ability itself;
                    // the static Phoenix mannequin would otherwise cover the three chicks.
                    // FirebirdBossPresentation owns only the decorative wings/feathers;
                    // the painted Phoenix body is the pooled enemy SpriteRenderer itself.
                    sandboxBoss.gameObject.SetActive(false);
                    sandboxBoss.FirebirdPresentation?.SetVisible(false);
                    sandboxBoss.BossPresentation?.SetVisible(false);
                    if (sandboxBoss.Renderer != null) sandboxBoss.Renderer.enabled = false;
                }
                else
                {
                    sandboxBoss.gameObject.SetActive(true);
                    if (sandboxBoss.BossState == BossAiState.Egg)
                    {
                        if (sandboxBoss.Renderer != null) sandboxBoss.Renderer.enabled = true;
                        sandboxBoss.BossStateTimer -= dt;
                        sandboxBoss.FirebirdPresentation?.Render(sandboxBoss, true);
                        if (sandboxBoss.BossStateTimer <= 0f)
                        {
                            ReviveFirebird(sandboxBoss);
                            // Preserve the room's promise: after a demonstration ends, the
                            // phoenix returns to the middle and never chooses an attack state.
                            sandboxBoss.Radius = 0f;
                            sandboxBoss.Angle = 0f;
                            sandboxBoss.transform.position = Vector3.zero;
                            sandboxBoss.BossStateTimer = 999f;
                            sandboxBoss.FireTimer = 999f;
                        }
                    }
                    else
                    {
                        if (sandboxBoss.Renderer != null) sandboxBoss.Renderer.enabled = true;
                        sandboxBoss.FirebirdPresentation?.Render(sandboxBoss, false);
                    }
                }
            }
            UpdateSandboxVoidPresentation(dt);
            UpdateSandboxBlackHole(dt);
            UpdateSandboxMechanics(dt);
            UpdateSandboxLayeredVfx(dt);
        }

        private void SetAbilityVfxWorkshopIsolation(bool isolated)
        {
            // The editor is an animation plate, not a playable arena. Keep the
            // world camera and UI, but remove the classic orbit and music rings.
            for (var i = 0; i < orbitRenderers.Count; i++)
                if (orbitRenderers[i] != null) orbitRenderers[i].enabled = !isolated && OrbitSettings.ShowTrajectory;
            if (backgroundStars != null)
                for (var i = 0; i < backgroundStars.Length; i++)
                    if (backgroundStars[i] != null) backgroundStars[i].enabled = !isolated;
            if (spaceBackdrop != null) spaceBackdrop.gameObject.SetActive(!isolated);
            for (var i = 0; i < stars.Count; i++)
                if (stars[i] != null) stars[i].gameObject.SetActive(!isolated);
            if (poolRoot != null) poolRoot.gameObject.SetActive(!isolated);
            musicReactiveVisuals?.SetWorkshopSuppressed(isolated);
            if (isolated && !workshopIsolationApplied)
            {
                // The editor is a presentation plate. Drop any transient combat objects that
                // were already alive when the user opened it, otherwise their old trails can
                // look like stray spell layers in the preview.
                for (var i = projectiles.Count - 1; i >= 0; i--)
                    if (projectiles[i] != null) ReleaseConfiguredShot(projectiles[i]);
                projectiles.Clear();
                for (var i = stars.Count - 1; i >= 0; i--)
                    if (stars[i] != null) starPool?.Release(stars[i]);
                stars.Clear();
                for (var i = damageShards.Count - 1; i >= 0; i--)
                    if (damageShards[i] != null) damageShardPool?.Release(damageShards[i]);
                damageShards.Clear();
            }
            if (isolated)
            {
                // There can be a second preview camera after an editor reload. Suppress every
                // distortion component, not only the current gameplay camera, so its full-screen
                // pass cannot bleed into the clean plate.
                var distortions = FindObjectsByType<MusicSpaceDistortion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (var i = 0; i < distortions.Length; i++)
                {
                    var distortion = distortions[i];
                    if (distortion == null) continue;
                    if (isolated)
                    {
                        if (distortion.enabled && !workshopDisabledDistortions.Contains(distortion))
                            workshopDisabledDistortions.Add(distortion);
                        distortion.SetActive(false);
                        distortion.enabled = false;
                    }
                    else if (workshopDisabledDistortions.Contains(distortion))
                    {
                        distortion.enabled = true;
                    }
                }
            }
            else
            {
                for (var i = 0; i < workshopDisabledDistortions.Count; i++)
                    if (workshopDisabledDistortions[i] != null) workshopDisabledDistortions[i].enabled = true;
                workshopDisabledDistortions.Clear();
            }
            // The workshop is rendered from a private layer so unrelated world sprites cannot
            // survive the clean animation plate even if another system re-enables its renderer.
            const int workshopLayerIndex = 31;
            if (isolated && !workshopCameraMaskApplied)
            {
                var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (var i = 0; i < cameras.Length; i++)
                {
                    var camera = cameras[i];
                    if (camera == null) continue;
                    if (!workshopPreviousCameraMasks.ContainsKey(camera))
                        workshopPreviousCameraMasks.Add(camera, camera.cullingMask);
                    camera.cullingMask = 1 << workshopLayerIndex;
                }
                workshopCameraMaskApplied = true;
            }
            else if (!isolated && workshopCameraMaskApplied)
            {
                foreach (var previous in workshopPreviousCameraMasks)
                {
                    if (previous.Key != null) previous.Key.cullingMask = previous.Value;
                }
                workshopPreviousCameraMasks.Clear();
                workshopCameraMaskApplied = false;
            }
            if (isolated)
            {
                var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (var i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null || !renderer.enabled || IsWorkshopVfxRenderer(renderer)) continue;
                    if (!workshopHiddenRenderers.Contains(renderer)) workshopHiddenRenderers.Add(renderer);
                    renderer.enabled = false;
                }
                workshopIsolationApplied = true;
            }
            else if (!isolated && workshopIsolationApplied)
            {
                for (var i = 0; i < workshopHiddenRenderers.Count; i++)
                    if (workshopHiddenRenderers[i] != null) workshopHiddenRenderers[i].enabled = true;
                workshopHiddenRenderers.Clear();
                workshopIsolationApplied = false;
            }
        }

        private static bool IsWorkshopVfxRenderer(Renderer renderer)
        {
            var current = renderer.transform;
            while (current != null)
            {
                if (current.name == "Sandbox layered VFX") return true;
                current = current.parent;
            }
            return false;
        }

        private void TriggerAbilitySandboxSpell(AbilitySandboxAbilityId ability)
        {
            if (sandboxBoss == null) return;
            if (TryTriggerSandboxMechanic(ability)) return;
            if (ability == AbilitySandboxAbilityId.RiftEcho && (riftEchoCooldown > 0f || player == null))
            {
                abilitySandbox.NotifyCooldown(ability, riftEchoCooldown);
                return;
            }
            if (ability == AbilitySandboxAbilityId.VectorSnap && (vectorSnapCooldown > 0f || player == null))
            {
                abilitySandbox.NotifyCooldown(ability, vectorSnapCooldown);
                return;
            }
            abilitySandbox.NotifyActivated(ability);
            BeginSandboxLayeredAbility(ability);
            switch (ability)
            {
                case AbilitySandboxAbilityId.SolarChicks:
                {
                    var direction = ((Vector2)player.position - (Vector2)sandboxBoss.transform.position).normalized;
                    FireConfiguredBossShot(sandboxBoss, BossAssetRegistry.Ability(BossAbilityId.FirebirdSolarChicks).Shot);
                    break;
                }
                case AbilitySandboxAbilityId.AshenEgg:
                    BeginFirebirdEgg(sandboxBoss);
                    sandboxBoss.Radius = 0f;
                    sandboxBoss.transform.position = Vector3.zero;
                    break;
                case AbilitySandboxAbilityId.PhoenixDive:
                    sandboxBoss.FirebirdPresentation?.TriggerPhoenixDiveBurst();
                    FireConfiguredBossShot(sandboxBoss, BossAssetRegistry.Ability(BossAbilityId.FirebirdPhoenixDive).Shot);
                    break;
                case AbilitySandboxAbilityId.HarrierRiftCopies:
                    RemoveHarrierClones();
                    SpawnHarrierClones(sandboxBoss, BossAssetRegistry.Ability(BossAbilityId.HarrierRiftCopies).Summon.Count);
                    if (sandboxReverseTimer > 0f)
                        for (var i = 0; i < enemies.Count; i++)
                            if (enemies[i].Kind == EnemyKind.ShadeClone) enemies[i].CloneOrbitDirection *= -1f;
                    break;
                case AbilitySandboxAbilityId.HarrierPhaseDash:
                    TriggerSandboxHarrierDash();
                    break;
                case AbilitySandboxAbilityId.HarrierColdFan:
                {
                    var direction = ((Vector2)player.position - (Vector2)sandboxBoss.transform.position).normalized;
                    FireConfiguredBossShot(sandboxBoss, BossAssetRegistry.Ability(BossAbilityId.HarrierColdFan).Shot);
                    break;
                }
                case AbilitySandboxAbilityId.VoidRiftBeam:
                    sandboxVoidBeamTimer = BossSettings.BeamTelegraphDuration + BossSettings.BeamSweepDuration;
                    sandboxVoidBeamAngle = Mathf.Atan2(player.position.y, player.position.x) * Mathf.Rad2Deg;
                    SpawnImpactBurst(sandboxBoss.transform.position, new Color(.36f, .88f, 1f), 16, 1.55f, .36f, true);
                    break;
                case AbilitySandboxAbilityId.VoidGravityRoots:
                    sandboxVoidRootTimer = BossSettings.RootTelegraphDuration + BossSettings.RootLockDuration;
                    sandboxVoidRootAngle = playerAngle * Mathf.Rad2Deg;
                    SpawnImpactBurst(player.position,
                        new Color(1f, .28f, .76f), 12, 1.45f, .32f, true);
                    break;
                case AbilitySandboxAbilityId.VoidBarrage:
                {
                    var direction = ((Vector2)player.position - (Vector2)sandboxBoss.transform.position).normalized;
                    FireConfiguredBossShot(sandboxBoss, BossAssetRegistry.Ability(BossAbilityId.VoidBarrage).Shot);
                    break;
                }
                case AbilitySandboxAbilityId.BlackHole:
                    TriggerSandboxBlackHole();
                    break;
                case AbilitySandboxAbilityId.RiftEcho:
                    TryUseRiftEcho();
                    break;
                case AbilitySandboxAbilityId.VectorSnap:
                    TryUseVectorSnap();
                    break;
            }
        }

        private void TriggerSandboxHarrierDash()
        {
            if (sandboxBoss == null || player == null) return;
            var origin = (Vector2)sandboxBoss.transform.position;
            var direction = ((Vector2)player.position - origin).normalized;
            var arrival = direction * OrbitSettings.Radius;
            for (var i = 0; i < 5; i++)
            {
                var point = Vector2.Lerp(origin, arrival, i / 4f);
                SpawnImpactBurst(point, new Color(.62f, .25f, 1f), 7, 1.52f, .30f, true);
            }
            FireConfiguredBossShot(sandboxBoss, BossAssetRegistry.Ability(BossAbilityId.HarrierPhaseDash).Shot);
            AddScreenShake(.08f, .05f);
        }

        private void UpdateAbilitySandboxHarrierClones(float dt)
        {
            if (abilitySandbox != null && (abilitySandbox.VfxEditorOpen || !abilitySandbox.BossVisible))
            {
                // Clones are gameplay targets, not part of the isolated VFX plate.
                for (var i = enemies.Count - 1; i >= 0; i--)
                {
                    var clone = enemies[i];
                    if (clone == null || clone.Kind != EnemyKind.ShadeClone) continue;
                    RemoveEnemy(i);
                }
                return;
            }
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var clone = enemies[i];
                if (clone == null || clone.Kind != EnemyKind.ShadeClone) continue;
                clone.Life -= dt;
                clone.FireTimer -= dt;
                UpdateHarrierClone(clone, dt);
                if (clone.Life <= 0f) RemoveEnemy(i);
            }
        }

        private void UpdateSandboxVoidPresentation(float dt)
        {
            if (sandboxBoss == null || sandboxBoss.BossPresentation == null) return;
            if (abilitySandbox != null && (abilitySandbox.VfxEditorOpen || !abilitySandbox.BossVisible))
            {
                sandboxVoidBeamTimer = 0f;
                sandboxVoidRootTimer = 0f;
                sandboxBoss.BossPresentation.SetVisible(false);
                return;
            }
            sandboxVoidBeamTimer = Mathf.Max(0f, sandboxVoidBeamTimer - dt);
            sandboxVoidRootTimer = Mathf.Max(0f, sandboxVoidRootTimer - dt);
            if (sandboxVoidBeamTimer <= 0f && sandboxVoidRootTimer <= 0f)
            {
                sandboxBoss.BossPresentation.SetVisible(false);
                return;
            }

            var beamTotal = BossSettings.BeamTelegraphDuration + BossSettings.BeamSweepDuration;
            var beamElapsed = beamTotal - sandboxVoidBeamTimer;
            var beamTelegraph = sandboxVoidBeamTimer > 0f
                ? Mathf.Clamp01(beamElapsed / BossSettings.BeamTelegraphDuration) : 0f;
            var sweeping = sandboxVoidBeamTimer > 0f && beamElapsed >= BossSettings.BeamTelegraphDuration;
            var beamAngle = sandboxVoidBeamAngle;
            if (sweeping)
                beamAngle += (beamElapsed - BossSettings.BeamTelegraphDuration) * BossSettings.BeamAngularSpeed;

            var rootTotal = BossSettings.RootTelegraphDuration + BossSettings.RootLockDuration;
            var rootElapsed = rootTotal - sandboxVoidRootTimer;
            var rootTelegraph = sandboxVoidRootTimer > 0f
                ? Mathf.Clamp01(rootElapsed / BossSettings.RootTelegraphDuration) : 0f;
            var rootLocked = sandboxVoidRootTimer > 0f && rootElapsed >= BossSettings.RootTelegraphDuration;
            sandboxBoss.BossPresentation.SetVisible(true);
            sandboxBoss.BossPresentation.Render(sandboxBoss, beamAngle, beamTelegraph, sweeping,
                sandboxVoidRootAngle, rootTelegraph, rootLocked,ShipOrbitCenter);
        }

        private void TriggerSandboxBlackHole()
        {
            if (player == null || sandboxBlackHolePresentation == null) return;
            var ability = BossAssetRegistry.Ability(BossAbilityId.VoidBlackHole);
            var anchorAngle = playerAngle + Mathf.PI * ability.Gravity.AnchorAngle;
            sandboxBlackHoleCenter = new Vector2(Mathf.Cos(anchorAngle), Mathf.Sin(anchorAngle)) * OrbitSettings.Radius * ability.Gravity.AnchorRadius;
            sandboxBlackHoleTimer = ability.Duration;
            sandboxBlackHolePresentation.SetVisible(true);
            spellVfxPool?.EmitImpact(sandboxBlackHoleCenter, ability.CastPrefab, ability.Vfx);
            AddScreenShake(.12f, .07f);
        }

        private void UpdateSandboxBlackHole(float dt)
        {
            if (sandboxBlackHolePresentation == null) return;
            if (abilitySandbox != null && (abilitySandbox.VfxEditorOpen || !abilitySandbox.BossVisible))
            {
                sandboxBlackHoleTimer = 0f;
                sandboxBlackHolePresentation.SetVisible(false);
                return;
            }
            sandboxBlackHoleTimer = Mathf.Max(0f, sandboxBlackHoleTimer - dt);
            if (sandboxBlackHoleTimer <= 0f)
            {
                sandboxBlackHolePresentation.SetVisible(false);
                return;
            }

            var ability = BossAssetRegistry.Ability(BossAbilityId.VoidBlackHole);
            var gravity = ability.Gravity;
            var remaining01 = sandboxBlackHoleTimer / Mathf.Max(.01f, ability.Duration);
            sandboxBlackHolePresentation.SetVisible(true);
            sandboxBlackHolePresentation.Render(sandboxBlackHoleCenter, remaining01);

            var pullAngle = Mathf.Atan2(sandboxBlackHoleCenter.y, sandboxBlackHoleCenter.x);
            if (player != null)
            {
                var distance = Vector2.Distance(player.position, sandboxBlackHoleCenter);
                var pull = Mathf.Lerp(gravity.PlayerPullMin, gravity.PlayerPullMax, Mathf.Clamp01(1f - distance / (OrbitSettings.Radius * 1.65f)));
                playerAngle = MoveTowardsAngleRadians(playerAngle, pullAngle, dt * pull);
                targetAngle = MoveTowardsAngleRadians(targetAngle, pullAngle, dt * pull * .78f);
                PositionOnOrbit();
            }

            for (var i = 0; i < projectiles.Count; i++)
            {
                var projectile = projectiles[i];
                var delta = sandboxBlackHoleCenter - (Vector2)projectile.transform.position;
                var distanceSq = Mathf.Max(gravity.MinDistanceSquared, delta.sqrMagnitude);
                projectile.Velocity += delta.normalized * (gravity.ProjectilePull / distanceSq) * dt;
            }
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.Kind != EnemyKind.ShadeClone) continue;
                enemy.Radius = Mathf.MoveTowards(enemy.Radius, sandboxBlackHoleCenter.magnitude, dt * .75f);
                enemy.Angle = MoveTowardsAngleRadians(enemy.Angle, pullAngle, dt * .92f);
            }
            for (var i = 0; i < damageShards.Count; i++)
            {
                var shard = damageShards[i];
                if (shard == null) continue;
                var delta = sandboxBlackHoleCenter - (Vector2)shard.transform.position;
                shard.Velocity += delta.normalized * (gravity.ParticlePull / Mathf.Max(.25f, delta.sqrMagnitude)) * dt;
            }
            if (riftEcho != null && riftEcho.gameObject.activeSelf)
                riftEchoAngle = MoveTowardsAngleRadians(riftEchoAngle, pullAngle, dt * .92f);
        }

        private void UpdateAbilitySandboxProjectiles(float dt)
        {
            if (abilitySandbox != null && abilitySandbox.VfxEditorOpen)
            {
                for (var i = projectiles.Count - 1; i >= 0; i--)
                    if (projectiles[i] != null) ReleaseConfiguredShot(projectiles[i]);
                projectiles.Clear();
                return;
            }
            // The test room deliberately renders projectiles but excludes all hit tests:
            // the dummy cannot damage the pilot, be killed, or advance a wave.
            for (var i = projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = projectiles[i];
                if (abilitySandbox != null && !abilitySandbox.BossVisible && projectile.SandboxBossEffect)
                {
                    RemoveProjectile(i);
                    continue;
                }
                TickConfiguredShot(projectile, dt);
                projectile.Life -= dt;
                var previousPosition = projectile.transform.position;
                projectile.transform.position += (Vector3)(projectile.Velocity * dt);
                // Only the new player visual tests the inert dummy; no damage, score or boss AI.
                if (projectile.SpellVfx != null && projectile.FromPlayer && sandboxBoss != null && abilitySandbox.BossVisible &&
                    CoopTetherRules.DistanceToSegment(sandboxBoss.transform.position, previousPosition,
                        projectile.transform.position) <= BossSettings.WorldSize * .57f)
                {
                    spellVfxPool.Hit(projectile, SpellVfxPool.ContactPoint(previousPosition,
                        projectile.transform.position, sandboxBoss.transform.position, BossSettings.WorldSize * .57f));
                    RemoveProjectile(i);
                    continue;
                }
                if (projectile.SpellVfx != null && (projectile.Shot != null || projectile.VisualStyle == ProjectileVisualStyle.FirebirdChick) &&
                    !projectile.FromPlayer && player != null &&
                    CoopTetherRules.DistanceToSegment(player.position, previousPosition, projectile.transform.position) <= .3f)
                {
                    spellVfxPool.Hit(projectile, SpellVfxPool.ContactPoint(previousPosition,
                        projectile.transform.position, player.position, .3f));
                    RemoveProjectile(i);
                    continue;
                }
                projectile.AnimateFirebirdChick(Time.time);
                projectile.UpdateFirebirdChickTrail(Time.time);
                if (projectile.Life <= 0f || ShouldDespawnProjectile(projectile) || projectile.transform.position.sqrMagnitude > 100f)
                    RemoveProjectile(i);
            }
        }

        private void ClearSandboxBossShards()
        {
            for (var i = damageShards.Count - 1; i >= 0; i--)
            {
                var shard = damageShards[i];
                if (shard == null || !shard.SandboxBossEffect) continue;
                damageShards.RemoveAt(i);
                damageShardPool?.Release(shard);
            }
        }

        private void ExitAbilitySandbox()
        {
            ResetSandboxMechanics();
            abilitySandbox.Close();
            musicReactiveVisuals?.SetSandboxTimeMode(false);
            Time.timeScale = sandboxPreviousTimeScale;
            sandboxPreviousTimeScale = 1f;
            if (sandboxBoss != null && sandboxBoss.Renderer != null) sandboxBoss.Renderer.enabled = true;
            if (sandboxBoss != null && sandboxBoss.BossPresentation != null) sandboxBoss.BossPresentation.SetVisible(false);
            sandboxVoidBeamTimer = 0f;
            sandboxVoidRootTimer = 0f;
            sandboxBlackHoleTimer = 0f;
            sandboxBlackHolePresentation?.SetVisible(false);
            Cleanup();
            ResetOrbitalAbilityState();
            sandboxBoss = null;
            autoFire = sandboxAutoFireBefore;
            phase = 1;
            playing = false;
            SetPaused(false);
            showMenu = true;
            activeControlDirection = 0;
            if (player != null) player.gameObject.SetActive(true);
            BeginUiFade();
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
            defensePlaying = false;
            defenseRunOver = false;
            if (defenseFlagship != null) defenseFlagship.gameObject.SetActive(false);
            if (defenseFlagshipGlow != null) defenseFlagshipGlow.gameObject.SetActive(false);
            SetDefenseFlagshipEffectsActive(false);
            showSettings = false;
            BeginUiFade();
            PlayerPrefs.SetString("orbital_rift_nickname", playerNickname);
            PlayerPrefs.Save();
            currentRunId = Guid.NewGuid().ToString("N");
            Cleanup(); ResetOrbitalAbilityState(); score = 0; shields = GameRules.Current.PlayerStartingHull; starShields = 0; tripleShotTimer = 0f; phase = 1; cores = 0; playing = true; showMenu = false; showResults = false; SetPaused(false); coreActive = false; bossSpawnPending = false; firstBossMirrorBreakShown = false; bossMirrorActive = false; shieldOrbitDirection = 1f;
            playerAngle = -Mathf.PI * .5f;
            targetAngle = playerAngle;
            playerCommandSource?.Reset();
            touchHintTimer = 5f;
            phaseUpgradeBannerTimer = 2.5f;
            phaseUpgradeLabel = "СИСТЕМА В СЕТИ\nПЕРВАЯ ФАЗА";
            screenShakeTimer = 0f;
            mmrResultTimer = 0f;
            if (GameAudioSettings.MusicEnabled && musicSource != null && musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
            StartWave(); SeedStreamField(StarStreamSettings.InitialFieldStarCount); SpawnWarpBurst(36, 1.2f);
        }

        private void UpdateSpaceTravel(float dt)
        {
            var runActive = (playing && !showResults) || (defensePlaying && !defenseRunOver) || coopPlaying;
            var combat = false;
            var waterReach = OrbitSettings.Radius;
            if (coopPlaying)
            {
                var finished = coopLocalPreview ? coopPreviewCompleted || coopPreviewFailed :
                    coopSimulation == null || coopSimulation.RunCompleted || coopSimulation.RunFailed;
                runActive = !finished;
                var kind = (SectorRoomType)(coopLocalPreview ? coopPreviewEnemyKind :
                    coopSimulation == null ? (byte)SectorRoomType.Start : coopSimulation.CoopEnemyKind);
                var health = coopLocalPreview ? coopPreviewEnemyHealth : coopSimulation == null ? 0 : coopSimulation.CoopEnemyHealth;
                combat = runActive && health > 0 && (kind == SectorRoomType.Combat || kind == SectorRoomType.Elite || kind == SectorRoomType.Boss);
                var time = coopLocalPreview ? coopPreviewTrajectoryTime : coopSimulation == null ? 0f : coopSimulation.TrajectoryTimeSeconds;
                waterReach = CoopTrajectorySettings.FramingExtents(time).magnitude;
            }
            else
                combat = runActive && !defensePlaying && ActiveBoss() != null;

            if (dt > 0f)
            {
                if (!LivingCosmosActive && runActive && ((!wasSpaceRun) || (wasSpaceCombat && !combat)))
                    jumpTimer = StarStreamSettings.JumpDuration;
                if (!runActive) jumpTimer = 0f;
                jumpTimer = Mathf.Max(0f, jumpTimer - dt);
                wasSpaceRun = runActive;
                wasSpaceCombat = combat;
            }
            var progress = 1f - jumpTimer / StarStreamSettings.JumpDuration;
            var jump = jumpTimer > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .16f, progress)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.32f, 1f, progress))) : 0f;
            if (LivingCosmosActive)
            {
                jump = livingCosmos.Jump;
                combat = livingCosmos.Phase != LivingEncounterPhase.Departing;
            }
            musicReactiveVisuals?.SetLivingRegion(LivingCosmosActive, livingCosmos?.Region ?? 0,
                livingCosmos?.NextRegion ?? 0, livingCosmos?.Transition ?? 0f);
            // Arrive over half a second instead of snapping the particles to rest.
            var targetSpeed = combat ? StarStreamSettings.CombatTravelSpeed : Mathf.Lerp(1f, StarStreamSettings.JumpTravelSpeed, jump);
            spaceTravelSpeed = Mathf.Lerp(spaceTravelSpeed, targetSpeed, 1f - Mathf.Exp(-dt * 4f));
            backgroundTravelTime += dt * spaceTravelSpeed;
            musicReactiveVisuals?.SetSpaceTravel(spaceTravelSpeed, combat ? jump * .22f : jump,
                runActive, paused || LivingModalChoice, waterReach);
        }

        private void UpdatePresentation()
        {
            UpdateBackgroundStars();
            if (menuEmblem != null)
            {
                // Логотип меню теперь рисуется pixel-интерфейсом: не перекрываем поле позывного.
                menuEmblem.gameObject.SetActive(false);
            }
            if (warpBadge != null)
            {
                warpBadge.gameObject.SetActive(warpTimer > 0f && (abilitySandbox == null || !abilitySandbox.VfxEditorOpen));
                if (warpTimer > 0f) warpBadge.Rotate(0f, 0f, 140f * Time.deltaTime);
            }
        }

        private void Cleanup()
        {
            spellVfxPool?.Clear();
            livingCosmos = null;
            livingTempo = null;
            livingTempoMillisecondRemainder = 0f;
            wasSpaceCombat = wasSpaceRun = false;
            jumpTimer = 0f;
            spaceTravelSpeed = 1f;
            bossMirrorActive = false;
            musicReactiveVisuals?.EndMirrorBreak();
            for (var i=enemies.Count-1;i>=0;i--) ReleaseConfiguredEnemy(enemies[i]); enemies.Clear();
            for (var i=projectiles.Count-1;i>=0;i--) ReleaseConfiguredShot(projectiles[i]); projectiles.Clear();
            for (var i=stars.Count-1;i>=0;i--) starPool.Release(stars[i]); stars.Clear();
            starShields = 0;
            for (var i=damageShards.Count-1;i>=0;i--) damageShardPool.Release(damageShards[i]); damageShards.Clear();
            splitPickup.gameObject.SetActive(false);
        }

        private void UpdateInput(float dt, int direction)
        {
            if (playerRootTimer > 0f)
            {
                activeControlDirection = 0;
                targetAngle = playerAngle;
                return;
            }
            if (direction != 0)
            {
                activeControlDirection = direction;
                // Keep the shield direction after release: positive orbit input is
                // counter-clockwise, so the shield flow mirrors it clockwise.
                shieldOrbitDirection = -direction;
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
            player.position = pos+ShipOrbitCenter;
            player.up = -pos.normalized;
        }

        private void FirePlayer(ShipLoadout loadout)
        {
            var projectileSpeed = BalanceSettings.PlayerProjectileSpeed(phase) * loadout.ProjectileSpeedMultiplier;
            Shoot((Vector2)player.position, (ShipOrbitCenter-(Vector2)player.position).normalized * projectileSpeed, true, loadout.ProjectileColor, loadout.Element, loadout.DamageMultiplier * GameRules.Current.PlayerBaseDamage);
            if (!splitShot && tripleShotTimer <= 0f) return;
            var inward = (ShipOrbitCenter-(Vector2)player.position).normalized;
            Shoot(player.position, Rotate(inward, 12f)*projectileSpeed, true, loadout.ProjectileColor, loadout.Element, loadout.DamageMultiplier * GameRules.Current.PlayerBaseDamage);
            Shoot(player.position, Rotate(inward,-12f)*projectileSpeed, true, loadout.ProjectileColor, loadout.Element, loadout.DamageMultiplier * GameRules.Current.PlayerBaseDamage);
        }

        private void Shoot(Vector2 position, Vector2 velocity, bool friendly, Color color, DamageElement element = DamageElement.Kinetic, float damage = -1f, bool fromRiftEcho = false)
        {
            if (!friendly && projectiles.Count >= GameRules.Current.ProjectileCap) return;
            if(damage<0)damage=friendly?GameRules.Current.PlayerBaseDamage:GameRules.Current.HostileBaseDamage;
            var p = projectilePool.Get();
            // Вражеские выстрелы — простые тёмно-зелёные квадраты; PNG игрока сохраняется без тонировки.
            p.SetVisual(friendly && projectileSprite != null ? projectileSprite : whiteSprite, friendly && projectileSprite != null, !friendly);
            p.ResetProjectile(position, velocity, friendly, friendly ? color : new Color(.05f, .30f, .13f), element, damage);
            p.FromRiftEcho = fromRiftEcho;
            if (friendly) spellVfxPool?.Attach(p);
            projectiles.Add(p);
            if (abilitySandbox.IsOpen && p.SpellVfx == null)
                sandboxLayeredVfx?.EmitShot(position, velocity, color, p.Renderer != null ? p.Renderer.sprite : null);
        }

        private void UpdateSpawning(float dt)
        {
            // The ability sandbox is a presentation room: it has its own inert boss
            // and must never advance the combat spawner while the runtime pools are
            // being rebuilt or inspected.
            if (abilitySandbox != null && abilitySandbox.IsOpen) return;
            if (enemyPool == null) return;
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
            var cap = Mathf.Min(GameRules.Current.EnemyCapBase + phase * GameRules.Current.EnemyCapPerPhase, GameRules.Current.EnemyCapMax);
            if (enemies.Count >= cap) { spawnTimer = .35f; return; }
            SpawnConfiguredMob(CurrentEncounter.Pick(phase, configuredSpawnOrdinal++), false);
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
            if (kind == EnemyKind.ShadeClone) return harrierBossSprite != null ? harrierBossSprite : pinkCanEnemySprite;
            if (kind == EnemyKind.Boss) return voidMawBossSprite != null ? voidMawBossSprite : bossSprite;
            return null;
        }

        private Sprite BossSpriteFor(BossArchetype archetype)
        {
            var definition = BossAssetRegistry.Get(archetype);
            if (definition != null && definition.Sprite != null) return definition.Sprite;
            switch (archetype)
            {
                case BossArchetype.AstralFirebird: return firebirdBossSprite != null ? firebirdBossSprite : bossSprite;
                case BossArchetype.UmbralHarrier: return harrierBossSprite != null ? harrierBossSprite : bossSprite;
                default: return voidMawBossSprite != null ? voidMawBossSprite : bossSprite;
            }
        }

        private void SpawnBoss()
        {
            if (abilitySandbox != null && abilitySandbox.IsOpen) return;
            if (enemyPool == null) return;
            var archetype = BossArchetypeSettings.ForPhase(phase);
            var definition = CurrentEncounter?.Boss ?? BossAssetRegistry.Get(archetype);
            archetype = definition.Archetype;
            var boss = GetConfiguredEnemy(definition.Prefab);
            var sprite = definition.Sprite;
            boss.ResetEnemy(EnemyKind.Boss, Random.Range(0f, Mathf.PI * 2f), phase, sprite);
            boss.BossType = archetype;
            boss.BossMainSprite = sprite;
            boss.Health = definition.MaxHp;
            boss.MaxHealth = boss.Health;
            ConfigureBossData(boss, definition);
            boss.BossBeamAngle = boss.Angle * Mathf.Rad2Deg + 90f;
            if (boss.BossPresentation != null) boss.BossPresentation.SetVisible(false);
            if (boss.FirebirdPresentation != null) boss.FirebirdPresentation.SetVisible(false);
            if (boss.HarrierPresentation != null) boss.HarrierPresentation.SetVisible(false);
            if (definition.UseLegacyPresentation && archetype == BossArchetype.VoidMaw)
            {
                if (boss.BossPresentation == null) boss.BossPresentation = boss.gameObject.AddComponent<VoidMawBossPresentation>();
                boss.BossPresentation.Configure(arena, circleSprite != null ? circleSprite : whiteSprite);
                boss.BossPresentation.SetVisible(true);
            }
            else if (definition.UseLegacyPresentation && archetype == BossArchetype.AstralFirebird)
            {
                if (boss.FirebirdPresentation == null) boss.FirebirdPresentation = boss.gameObject.AddComponent<FirebirdBossPresentation>();
                boss.FirebirdPresentation.Configure(arena, circleSprite != null ? circleSprite : whiteSprite, definition.Presentation);
                boss.FirebirdPresentation.SetVisible(true);
            }
            else if (definition.UseLegacyPresentation)
            {
                if (boss.HarrierPresentation == null) boss.HarrierPresentation = boss.gameObject.AddComponent<HarrierBossPresentation>();
                boss.HarrierPresentation.Configure(arena, circleSprite != null ? circleSprite : whiteSprite);
                boss.HarrierPresentation.SetVisible(true);
            }
            ConfigureAppearance(boss, definition.Appearance);
            enemies.Add(boss);
            phaseUpgradeBannerTimer = 1.65f;
            phaseUpgradeLabel = "СИГНАЛ БОССА\n" + definition.DisplayName;
            SpawnWarpBurst(46, 1.65f);
            // Keep the broken-screen implementation available for a later visual pass,
            // but do not force it on during normal boss readability testing.
            if (!firstBossMirrorBreakShown && BossArchetypeSettings.MirrorBreakEnabled)
            {
                firstBossMirrorBreakShown = true;
                bossMirrorActive = true;
                bossMirrorStartedAt = Time.unscaledTime;
                musicReactiveVisuals?.TriggerMirrorBreak();
            }
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
                if (e.Kind == EnemyKind.ShadeClone)
                {
                    UpdateHarrierClone(e, dt);
                    if (e.Life <= 0) RemoveEnemy(i);
                    continue;
                }
                if (e.Mob != null) { MoveConfiguredMob(e, i, dt); if (e.Life <= 0) RemoveEnemy(i); continue; }
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

        private void FireStyledBossFan(Enemy boss, Vector2 direction, int count, float spread,
            Color color, DamageElement element, float size, float speedMultiplier)
        {
            FireStyledBossFan(boss, direction, count, spread, color, element, size, speedMultiplier, null);
        }

        private void FireStyledBossFan(Enemy boss, Vector2 direction, int count, float spread,
            Color color, DamageElement element, float size, float speedMultiplier, Sprite visual)
        {
            var center = (count - 1) * .5f;
            var speed = BalanceSettings.EnemyProjectileSpeed(phase) * speedMultiplier;
            for (var i = 0; i < count; i++)
                ShootStyledHostile(boss.transform.position, Rotate(direction, (i - center) * spread) * speed,
                    color, element, size, visual, abilitySandbox != null && abilitySandbox.IsOpen);
        }

        private void FireStyledBossRadial(Enemy boss, int count, Color color, DamageElement element, float size, float speedMultiplier)
        {
            FireStyledBossRadial(boss, count, color, element, size, speedMultiplier, null);
        }

        private void FireStyledBossRadial(Enemy boss, int count, Color color, DamageElement element, float size, float speedMultiplier, Sprite visual)
        {
            var speed = BalanceSettings.EnemyProjectileSpeed(phase) * speedMultiplier;
            for (var i = 0; i < count; i++)
            {
                var angle = boss.Angle + i * Mathf.PI * 2f / count;
                ShootStyledHostile(boss.transform.position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    color, element, size, visual, abilitySandbox != null && abilitySandbox.IsOpen);
            }
        }

        private void ShootStyledHostile(Vector2 position, Vector2 velocity, Color color, DamageElement element, float size)
        {
            ShootStyledHostile(position, velocity, color, element, size, null);
        }

        private void ShootStyledHostile(Vector2 position, Vector2 velocity, Color color, DamageElement element,
            float size, Sprite visual, bool sandboxBossEffect = false)
        {
            if (projectiles.Count >= 80) return;
            var projectile = projectilePool.Get();
            projectile.SetVisual(visual != null ? visual : (circleSprite != null ? circleSprite : whiteSprite), visual != null, true);
            projectile.ResetProjectile(position, velocity, false, color, element, 1f);
            projectile.transform.localScale = Vector3.one * size;
            if (visual != null && velocity.sqrMagnitude > .001f)
                projectile.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            var style = visual == firebirdChickProjectileSprite ? ProjectileVisualStyle.FirebirdChick
                : visual == harrierShardProjectileSprite ? ProjectileVisualStyle.HarrierShard
                : visual == voidPulseProjectileSprite ? ProjectileVisualStyle.VoidPulse
                : visual == solarLanceProjectileSprite ? ProjectileVisualStyle.SolarLance
                : ProjectileVisualStyle.Default;
            var sharedChick = style == ProjectileVisualStyle.FirebirdChick && solarChickPrefab != null &&
                spellVfxPool != null && spellVfxPool.Attach(projectile, solarChickPrefab);
            projectile.SetSpellVisualStyle(style, !sharedChick);
            projectile.SandboxBossEffect = sandboxBossEffect;
            projectile.Life = 4.25f;
            if (visual == firebirdChickProjectileSprite && !sharedChick)
            {
                SpawnSolarChickLaunchFx(position, velocity.normalized, color, sandboxBossEffect);
                projectile.AnimateFirebirdChick(Time.time);
            }
            projectiles.Add(projectile);
            if (abilitySandbox.IsOpen && !sharedChick)
                sandboxLayeredVfx?.EmitShot(position, velocity, color, visual != null ? visual : projectile.Renderer.sprite);
        }

        private void SpawnSolarChickLaunchFx(Vector2 position, Vector2 direction, Color color, bool sandboxBossEffect = false)
        {
            if (damageShardPool == null) return;
            var side = new Vector2(-direction.y, direction.x);
            for (var i = 0; i < 8; i++)
            {
                var shard = damageShardPool.Get();
                var angle = Random.Range(-35f, 35f) * Mathf.Deg2Rad;
                var spread = Rotate(direction, angle * Mathf.Rad2Deg);
                shard.ResetShard(position + side * Random.Range(-.08f, .08f),
                    spread * Random.Range(.55f, 1.35f), Random.Range(.025f, .06f),
                    Color.Lerp(new Color(1f, .98f, .60f), color, .35f), Random.Range(.18f, .34f));
                shard.SandboxBossEffect = sandboxBossEffect;
                shard.Renderer.sortingOrder = 10;
                damageShards.Add(shard);
            }
        }

        private bool TryDamagePlayerWithBossBeam(Enemy boss)
        {
            if (player == null || invincible > 0f) return false;
            var settings = (boss.ActiveAbility ?? BossAssetRegistry.Ability(BossAbilityId.VoidRiftBeam)).Beam;
            var healthRatio = boss.MaxHealth <= 0f ? 1f : boss.Health / boss.MaxHealth;
            if (!BossAttackRules.IsInsideBeam(boss.transform.position, player.position,
                    boss.BossBeamAngle, settings.Number(healthRatio), settings.InnerSafeRadius, settings.Length, settings.HalfWidthDegrees)) return false;
            if (starShields > 0)
            {
                ConsumeStarShield(player.position, null);
                return true;
            }
            DamagePlayer(settings.Damage);
            return true;
        }

        private void TryApplyGravroot(Enemy boss)
        {
            if (player == null) return;
            var ability = boss.ActiveAbility ?? BossAssetRegistry.Ability(BossAbilityId.VoidGravityRoots);
            var target = new Vector2(Mathf.Cos(boss.BossRootAngle * Mathf.Deg2Rad),
                Mathf.Sin(boss.BossRootAngle * Mathf.Deg2Rad)) * OrbitSettings.Radius+ShipOrbitCenter;
            var delta = Mathf.Abs(Mathf.DeltaAngle(playerAngle * Mathf.Rad2Deg, boss.BossRootAngle));
            spellVfxPool?.EmitImpact(target, ability.CastPrefab, ability.Vfx);
            if (delta > ability.Roots.HalfWidthDegrees)
            {
                combatMoments.GravrootDodged();
                return;
            }
            playerRootTimer = ability.Roots.LockDuration;
            targetAngle = playerAngle;
            activeControlDirection = 0;
            spellVfxPool?.EmitImpact(player.position, ability.AftereffectPrefab, ability.Vfx);
            AddScreenShake(.075f, .045f);
            combatMoments.GravrootCaught();
            HapticFeedback.Pulse(28);
        }

        private void UpdateProjectiles(float dt)
        {
            for (var i=projectiles.Count-1;i>=0;i--)
            {
                var p=projectiles[i];
                TickConfiguredShot(p, dt);
                p.AnimateFirebirdChick(Time.time);
                var previousPosition = (Vector2)p.transform.position;
                var nextPosition = previousPosition + p.Velocity * dt;
                // The combat simulation already emitted the paired exit shot when
                // its state crossed a lens. Consume the matching incoming sprite
                // here; otherwise the old straight visual would continue through
                // the entry lens and create a duplicate stream.
                // Exit shots are VisualOnly and already start outside the paired
                // disc. Do not feed them through the entry-consumption filter a
                // second time: on a reversed heading their first few pixels can
                // geometrically overlap the lens they just left.
                if (p.FromPlayer && !p.VisualOnly && coopPreviewLensesActive)
                {
                    var visualLensState = default(PairedLensTransitState);
                    if (PairedLensRules.TryTransit(ref nextPosition, ref p.Velocity, previousPosition,
                            PairedLensRules.PlayerShotRadius, ref visualLensState, coopPreviewLensPair,
                            PairedLensRules.PlayerShotCooldown, out _))
                    {
                        RemoveProjectile(i);
                        continue;
                    }
                }
                p.Life-=dt;
                p.transform.position = nextPosition;
                p.UpdateFirebirdChickTrail(Time.time);
                var released = !p.VisualOnly && (p.FromPlayer ? HitEnemies(i, p, previousPosition) : HitPlayer(i,p,previousPosition));
                if (!released && (p.Life <= 0f || ShouldDespawnProjectile(p))) RemoveProjectile(i);
            }
        }

        private bool ShouldDespawnProjectile(Projectile projectile)
        {
            if (projectile == null) return false;
            var profile = projectile.SpellVfx != null ? projectile.SpellVfx.Profile : null;
            var padding = profile != null ? Mathf.Max(0f, profile.ScreenPadding) : .12f;
            if (IsOutsideCamera(projectile.transform.position, padding)) return true;
            if (profile == null || profile.DespawnRadius <= 0f) return false;
            var fromCenter = (Vector2)projectile.transform.position - ShipOrbitCenter;
            return fromCenter.sqrMagnitude >= profile.DespawnRadius * profile.DespawnRadius;
        }

        private bool IsOutsideCamera(Vector3 worldPosition, float padding)
        {
            if (gameCamera == null) return false;
            var viewport = gameCamera.WorldToViewportPoint(worldPosition);
            return viewport.z < 0f || viewport.x < -padding || viewport.x > 1f + padding ||
                viewport.y < -padding || viewport.y > 1f + padding;
        }

        private ShieldVfxProfile StarShieldProfile
        {
            get
            {
                if (starShieldVfxProfile == null)
                    starShieldVfxProfile = Resources.Load<ShieldVfxProfile>("Spells/StarShield/Profiles/StarShield");
                return starShieldVfxProfile;
            }
        }

        private bool StarShieldPassiveEnabled => abilitySandbox == null || !abilitySandbox.IsOpen ||
            abilitySandbox.HasPassive(AbilitySandboxAbilityId.AegisOrbit);

        private int MaxStarShields => Mathf.Max(1, StarShieldProfile != null ? StarShieldProfile.MaxShields : 3);

        private void ClearStarShields()
        {
            for (var i = stars.Count - 1; i >= 0; i--)
            {
                if (!stars[i].IsShield) continue;
                var star = stars[i];
                stars.RemoveAt(i);
                starPool?.Release(star);
            }
            starShields = 0;
        }

        private bool HitEnemies(int projectileIndex, Projectile p, Vector2 previousPosition)
        {
            for (var j = enemies.Count - 1; j >= 0; j--)
            {
                var enemy = enemies[j];
                var hitRadius = enemy.Kind == EnemyKind.Boss
                    ? (enemy.BossState == BossAiState.Egg && enemy.ActiveAbility != null ? enemy.ActiveAbility.Egg.HitRadius : enemy.Definition != null ? enemy.Definition.HitRadius : BossSettings.WorldSize * .57f)
                    : enemy.Kind == EnemyKind.ShadeClone ? .34f : enemy.Mob != null ? enemy.Mob.HitRadius : .28f;
                // Test the whole travelled segment. This makes a projectile
                // that clips a large boss edge register even at a low mobile
                // frame rate, instead of tunnelling between two frames.
                if (CoopTetherRules.DistanceToSegment(enemy.transform.position, previousPosition,
                        p.transform.position) > hitRadius) continue;
                var resistance = enemy.Kind == EnemyKind.Boss ? (enemy.Definition != null ? enemy.Definition.Resistance(p.Element) : BossResistance(enemy.BossType, p.Element)) : 1f;
                enemy.Health -= ElementalCombat.ApplyResistance(p.Damage, resistance);
                spellVfxPool?.Hit(p, SpellVfxPool.ContactPoint(previousPosition,
                    p.transform.position, enemy.transform.position, hitRadius));
                RemoveProjectile(projectileIndex);
                var impactColor = EnemyEffectColor(enemy.Kind);
                if (enemy.Health <= 0)
                {
                    // The firebird's first apparent death is a timed execution
                    // window. It is not removed or rewarded until its small egg
                    // is actually broken; surviving five seconds restores it.
                    if (enemy.Kind == EnemyKind.Boss && enemy.Definition != null && enemy.Definition.Ability(BossAbilityBehaviour.RebirthEgg) != null && !enemy.BossSecondLifeSpent)
                    {
                        BeginFirebirdEgg(enemy);
                        return true;
                    }
                    if (p.FromRiftEcho) combatMoments.EchoKill();
                    score += enemy.Points;
                    if (defensePlaying) defenseKills++;
                    PlayEnemyDeathEffect(.82f);
                    var isBoss = enemy.Kind == EnemyKind.Boss;
                    SpawnImpactBurst(enemy.transform.position, impactColor, isBoss ? 42 : 13, isBoss ? 4.6f : 2.9f, isBoss ? .9f : .48f);
                    AddScreenShake(isBoss ? .36f : .09f, isBoss ? .18f : .065f);
                    if (isBoss)
                    {
                        if (enemy.BossType == BossArchetype.AstralFirebird && enemy.BossState == BossAiState.Egg)
                        {
                            // Breaking the egg is a warm white/orange payoff,
                            // distinct from an ordinary boss death burst.
                            SpawnImpactBurst(enemy.transform.position, new Color(1f, .84f, .36f), 76, 5.8f, 1.15f);
                            AddScreenShake(.42f, .24f);
                            phaseUpgradeBannerTimer = 1.25f;
                            phaseUpgradeLabel = "ЯЙЦО РАСКОЛОТО\nЖАР-ПТИЦА ПОВЕРЖЕНА";
                        }
                        if (enemy.BossType == BossArchetype.UmbralHarrier) RemoveHarrierClones();
                        bossMirrorActive = false;
                        musicReactiveVisuals?.EndMirrorBreak();
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

        private static float BossResistance(BossArchetype archetype, DamageElement element)
        {
            switch (archetype)
            {
                case BossArchetype.AstralFirebird:
                    // Fire is still useful, just not the best answer to a
                    // creature made of it; poison represents cooling ash.
                    if (element == DamageElement.Fire) return .64f;
                    if (element == DamageElement.Poison) return 1.28f;
                    return 1f;
                case BossArchetype.UmbralHarrier:
                    if (element == DamageElement.Cold) return .70f;
                    if (element == DamageElement.Fire) return 1.22f;
                    return 1f;
                default:
                    return BossSettings.Resistance(element);
            }
        }

        private void RemoveHarrierClones()
        {
            for (var i = enemies.Count - 1; i >= 0; i--)
                if (enemies[i].Kind == EnemyKind.ShadeClone) RemoveEnemy(i);
        }

        private bool HitPlayer(int projectileIndex, Projectile p, Vector2 previousPosition)
        {
            if (invincible > 0f) return false;
            var shot = p.Shot;
            var shieldBlocks = shot == null || shot.ShieldCanBlock;
            var shieldRadius = shot != null ? shot.ShieldHitRadius : .22f;
            var hitRadius = shot != null ? shot.PlayerHitRadius : .3f;
            if (shieldBlocks)
                for (var i = 0; i < stars.Count; i++)
                {
                    var star = stars[i];
                    if (!star.IsShield || CoopTetherRules.DistanceToSegment(star.transform.position, previousPosition, p.transform.position) >= shieldRadius) continue;
                    spellVfxPool?.Hit(p, p.transform.position);
                    RemoveProjectile(projectileIndex); ConsumeStarShield(star.transform.position, star); return true;
                }
            if (CoopTetherRules.DistanceToSegment(player.position, previousPosition, p.transform.position) >= hitRadius) return false;
            var damage = Mathf.CeilToInt(p.Damage);
            var removed = shot == null || shot.DestroyOnHit || (shieldBlocks && starShields > 0);
            if (removed) { spellVfxPool?.Hit(p, p.transform.position); RemoveProjectile(projectileIndex); }
            else spellVfxPool?.EmitImpact(p.transform.position, shot.ImpactPrefab, shot.Vfx);
            if (shieldBlocks && starShields > 0) { ConsumeStarShield(player.position, null); return removed; }
            DamagePlayer(damage);
            return removed;
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
            var profile = StarShieldProfile;
            var impactColor = profile != null ? profile.ImpactColor : (star.IsPurple ? new Color(.86f, .5f, 1f, 1f) : new Color(.62f, .9f, 1f, 1f));
            var impactBrightness = profile != null ? profile.ImpactBrightness : 1f;
            var impactCount = profile != null ? Mathf.Max(1, profile.ImpactParticleCount) : 10;
            var impactForce = profile != null ? Mathf.Max(.1f, profile.ImpactSize * 2.25f) : 1.9f;
            var impactDuration = profile != null ? profile.ImpactDuration : .3f;
            SpawnImpactBurst(impactPosition,
                new Color(impactColor.r * impactBrightness, impactColor.g * impactBrightness,
                    impactColor.b * impactBrightness, impactColor.a), impactCount, impactForce, impactDuration);
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

        private void DamagePlayer(int damage = 1)
        {
            if (damage <= 0) return;
            shields -= damage; invincible=GameRules.Current.PlayerHitInvulnerability; hpFlashTimer = .34f; SpawnPlayerDamageBurst(); PlayEffect(playerDamageSound, .8f); AddScreenShake(.22f, .14f); HapticFeedback.Pulse(shields <= 0 ? 110 : 48); if (shields <= 0) EndGame();
        }

        private void ActivateCore() { coreActive=true; core.gameObject.SetActive(true); coreAngle=Random.Range(-2.6f,-.5f); SpawnWarpBurst(14,.7f); }
        private void UpdateCore(float dt)
        {
            if (!coreActive) { core.gameObject.SetActive(false); return; }
            coreAngle += dt*.5f;
            // Ядро идёт по той же орбите, что и корабль, поэтому его можно подобрать.
            var corePosition = new Vector2(Mathf.Cos(coreAngle), Mathf.Sin(coreAngle)) * OrbitSettings.Radius+ShipOrbitCenter;
            core.position = corePosition;
            core.Rotate(0,0,dt*160f);
            if (Vector2.Distance(corePosition, player.position)<.42f)
            {
                coreActive=false; cores++; HapticFeedback.Pulse(24); SpawnWarpBurst(24,1f);
                // A boss closes its phase with one encounter. Ordinary phases
                // still contain three waves, each ending with a collectible core.
                if(cores >= (CurrentEncounter?.Waves ?? 3) || BossArchetypeSettings.IsBossWave(phase)) BeginWarp();
                else StartWave();
            }
        }

        private void BeginWarp()
        {
            jumpTimer = StarStreamSettings.JumpDuration;
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
            if (BossArchetypeSettings.IsBossWave(phase))
            {
                spawnsLeft = 0;
                bossSpawnPending = true;
                bossSpawnTimer = (CurrentEncounter?.Boss ?? BossAssetRegistry.Get(BossArchetypeSettings.ForPhase(phase))).IntroDelay;
                return;
            }
            configuredSpawnOrdinal = 0;
            spawnsLeft = CurrentEncounter.BaseCount + phase * CurrentEncounter.CountPerPhase + cores * CurrentEncounter.CountPerWave;
            spawnTimer = CurrentEncounter.InitialDelay;
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
            splitShotTimer = GameRules.Current.SplitShotDuration;
            SpawnWarpBurst(16, .85f);
        }

        private void UpdateStars(float dt)
        {
            if (abilitySandbox != null && abilitySandbox.VfxEditorOpen) return;
            // The menu/pause scene can tick before gameplay pools are created (for
            // example during a script reload).  Keep the decorative stream inert until
            // its dependencies exist instead of spamming a runtime NullReference.
            if (starPool == null || player == null || stars == null) return;
            var shieldProfile = StarShieldProfile;
            var shieldPassiveEnabled = StarShieldPassiveEnabled;
            var maxStarShields = Mathf.Max(1, shieldProfile != null ? shieldProfile.MaxShields : 3);
            if (!shieldPassiveEnabled && starShields > 0) ClearStarShields();
            starTimer -= dt;
            // Keep purple pickup cadence while reducing only the decorative white stream.
            if (starTimer <= 0f)
            {
                starTimer = 1f / Mathf.Max(1f, StarStreamSettings.StarsPerSecond);
                var purple = Random.value < StarStreamSettings.PurpleChance;
                if (purple || (spaceDepth == null && Random.value < Mathf.Clamp01(spaceTravelSpeed)))
                {
                    var angle = Random.Range(0f, Mathf.PI * 2f);
                    var star = starPool.Get();
                    if (star != null)
                    {
                        star.ResetStar(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), .55f, 1f);
                        star.SetPurple(purple, shieldProfile);
                        stars.Add(star);
                    }
                }
            }
            for(var i=stars.Count-1;i>=0;i--)
            {
                var s=stars[i];
                if (s.IsShield)
                {
                    var orbitSpeed = shieldProfile != null ? shieldProfile.OrbitSpeed : 3.4f;
                    s.ShieldAngle += dt * orbitSpeed * shieldOrbitDirection;
                    s.transform.position = player.position + (Vector3)(new Vector2(Mathf.Cos(s.ShieldAngle), Mathf.Sin(s.ShieldAngle)) * s.ShieldRadius);
                    s.TickShieldVisual(shieldProfile, Time.time);
                    continue;
                }
                var travelSpeed = s.IsPurple ? 1f : spaceTravelSpeed;
                s.Life -= dt * travelSpeed;
                var viewport = gameCamera.WorldToViewportPoint(s.transform.position);
                var edgeDistance = Mathf.Max(Mathf.Abs(viewport.x - .5f) * 2f, Mathf.Abs(viewport.y - .5f) * 2f);
                var slowdown = Mathf.Lerp(1f, StarStreamSettings.ScreenEdgeSpeedMultiplier, Mathf.InverseLerp(StarStreamSettings.ScreenEdgeSlowStart, 1.15f, edgeDistance));
                s.transform.position+=(Vector3)(s.Velocity * (dt * slowdown * travelSpeed));
                var outsideScreen = IsOutsideCamera(s.transform.position,
                    shieldProfile != null ? Mathf.Max(0f, shieldProfile.ScreenPadding) : .12f);
                var outsideRadius = shieldProfile != null && shieldProfile.DespawnRadius > 0f &&
                    ((Vector2)s.transform.position - ShipOrbitCenter).sqrMagnitude >=
                    shieldProfile.DespawnRadius * shieldProfile.DespawnRadius;
                if (outsideScreen || outsideRadius)
                {
                    RemoveStar(i);
                    continue;
                }
                // Белые звёзды остаются декоративным потоком. Только редкая
                // фиолетовая звезда может стать solid-щитом корабля.
                var pickupRadius = shieldProfile != null ? shieldProfile.PickupRadius : .34f;
                if (shieldPassiveEnabled && player != null && s.IsPurple && starShields < maxStarShields &&
                    Vector2.Distance(s.transform.position, player.position) < pickupRadius)
                {
                    s.ConfigureShield(shieldProfile, starShields);
                    starShields++;
                    continue;
                }
                var flyby = !s.IsPurple && s.HasEdgeFlyby
                    ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StarStreamSettings.EdgeFlybyStart, 1.06f, edgeDistance))
                    : 0f;
                // Only naturally brighter white stars receive the edge fly-by. Their heads
                // grow and brighten late in the trip; the rest remain small, transparent dust.
                var visualBrightness = Mathf.Lerp(s.BaseBrightness, Mathf.Clamp01(s.BaseBrightness + StarStreamSettings.EdgeFlybyAlphaBoost), flyby);
                s.transform.localScale = Vector3.one * s.BaseSize * Mathf.Lerp(1f, StarStreamSettings.EdgeFlybyMaxScale, flyby);
                var alpha = Mathf.Clamp01(s.Life) * visualBrightness;
                var streamTint = s.IsPurple
                    ? (shieldProfile != null ? shieldProfile.CoreColor : new Color(.76f, .38f, 1f, 1f))
                    : s.StreamTint;
                var coreBrightness = s.IsPurple && shieldProfile != null ? shieldProfile.CoreBrightness : 1f;
                var trailTint = s.IsPurple && shieldProfile != null ? shieldProfile.TrailColor : streamTint;
                var trailBrightness = shieldProfile != null ? shieldProfile.TrailBrightness : 1f;
                s.Renderer.color = new Color(streamTint.r * coreBrightness, streamTint.g * coreBrightness,
                    streamTint.b * coreBrightness, alpha * streamTint.a);
                s.Trail.startColor = new Color(trailTint.r * trailBrightness, trailTint.g * trailBrightness,
                    trailTint.b * trailBrightness, alpha * StarStreamSettings.TrailFade * trailTint.a);
                if (!s.IsPurple)
                {
                    var depth = Mathf.Clamp01(edgeDistance * .28f + flyby * .72f);
                    var desiredLength = Mathf.Lerp(StarStreamSettings.FarTrailSeconds, StarStreamSettings.NearTrailSeconds, depth) * s.TrailVariation;
                    // Compensate the existing edge slowdown: a growing head must not acquire
                    // a shorter tail just because its decorative motion eases near the bezel.
                    s.Trail.time = Mathf.Min(3f, desiredLength / Mathf.Max(.4f, slowdown)) * Mathf.Lerp(.15f, 1f, Mathf.Clamp01(spaceTravelSpeed));
                    s.Trail.startWidth = Mathf.Lerp(StarStreamSettings.FarTrailWidth, StarStreamSettings.NearTrailWidth, depth) * s.TrailVariation;
                }
                if (s.Halo != null)
                {
                    s.Halo.gameObject.SetActive(!s.IsPurple && s.HasEdgeFlyby);
                    s.Halo.transform.localScale = Vector3.one * Mathf.Lerp(StarStreamSettings.FlybyHaloBaseScale, StarStreamSettings.FlybyHaloMaxScale, flyby);
                    s.Halo.color = new Color(streamTint.r, streamTint.g, streamTint.b, alpha * (flyby * .12f));
                }
                if(s.Life<=0)RemoveStar(i);
            }
            if(warpTimer>0) warpTimer-=dt;
            if (phaseUpgradeBannerTimer > 0f) phaseUpgradeBannerTimer -= dt;
        }

        private void SpawnWarpBurst(int amount,float speed)
        { if (starPool == null) return; for(var i=0;i<amount;i++){var purple=Random.value < StarStreamSettings.PurpleChance;if(spaceDepth!=null&&!purple)continue;var angle=Random.Range(0,Mathf.PI*2);var s=starPool.Get();if (s == null) continue;s.ResetStar(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),speed*Random.Range(.7f,1.3f),Random.Range(1.6f,3f));s.SetPurple(purple, StarShieldProfile);stars.Add(s);} }

        private void SeedStreamField(int amount)
        {
            if (spaceDepth != null) return;
            if (starPool == null) return;
            for (var i = 0; i < amount; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var star = starPool.Get();
                if (star == null) continue;
                star.ResetStar(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), Random.Range(.34f, 1.15f), 1f, true);
                star.SetPurple(false);
                stars.Add(star);
            }
        }

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
            if (abilitySandbox != null && abilitySandbox.VfxEditorOpen) return;
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

        private void SpawnImpactBurst(Vector2 position, Color color, int amount, float force, float lifetime,
            bool sandboxBossEffect = false)
        {
            if (damageShardPool == null) return;
            for (var i = 0; i < amount; i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var shard = damageShardPool.Get();
                shard.ResetShard(position, direction * Random.Range(force * .45f, force), Random.Range(.025f, .07f), color, lifetime * Random.Range(.7f, 1.15f));
                shard.SandboxBossEffect = sandboxBossEffect;
                shard.Renderer.sortingOrder = 6;
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
            if (!enabled)
            {
                musicSource.Pause();
                if (MusicReactiveSettings.Enabled) ExternalMusicAudioBridge.RequestCapture();
            }
            else if ((playing || coopPlaying) && musicSource.clip != null)
            {
                ExternalMusicAudioBridge.StopCapture();
                if (musicSource.timeSamples > 0) musicSource.UnPause();
                else musicSource.Play();
            }
        }

        private void ToggleMusicReactiveVisuals()
        {
            var enabled = MusicReactiveSettings.Toggle();
            if (enabled && !GameAudioSettings.MusicEnabled) ExternalMusicAudioBridge.RequestCapture();
            else if (!enabled) ExternalMusicAudioBridge.StopCapture();
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
                if (gameCamera != null) gameCamera.transform.position = CameraBasePosition();
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
                gameCamera.transform.position = CameraBasePosition();
                return;
            }
            screenShakeTimer -= dt;
            var amount = screenShakeStrength * Mathf.Clamp01(screenShakeTimer / .25f);
            gameCamera.transform.position = CameraBasePosition() + new Vector3(Random.Range(-amount, amount), Random.Range(-amount, amount), 0f);
            if (screenShakeTimer <= 0f) screenShakeStrength = 0f;
        }
        private void RemoveEnemy(int index){var e=enemies[index];enemies.RemoveAt(index);ReleaseConfiguredEnemy(e);}
        private void RemoveProjectile(int index)
        {
            if (index < 0 || index >= projectiles.Count) return;
            var p = projectiles[index];
            if (p.VisualStyle == ProjectileVisualStyle.FirebirdChick && p.FirebirdChickVisual && !p.SandboxBossEffect)
            {
                // The flight presentation ends with a visible phoenix impact instead of
                // disappearing on the exact frame the pooled projectile is released.
                SpawnImpactBurst(p.transform.position, new Color(1f, .70f, .16f), 14, 2.0f, .30f);
                SpawnImpactBurst(p.transform.position, new Color(1f, .22f, .035f), 8, 1.25f, .20f);
            }
            ForgetSandboxProjectile(p);
            projectiles.RemoveAt(index);
            ReleaseConfiguredShot(p);
        }
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

            // While paused, the shared panel is the only interactive surface.
            // This prevents a tap on its exit button leaking through to a shop
            // card or to an underlying co-op control.
            if (paused && !runCompleted && !runFailed) return;

            // A dock room is a focused interaction state.  Rendering the full
            // combat telemetry underneath the shop turned the screen into a
            // wall of labels, so keep only one slim contextual header here.
            if (soloExpeditionPlaying && (expeditionShopDocking || expeditionShopOpen))
            {
                var dockHeader = new Rect(left + width * .12f, top + height * .018f, width * .76f, height * .050f);
                var dockAccent = expeditionShopOpen ? new Color(.68f, .48f, 1f) : cyan;
                PixelUi.DrawPanel(dockHeader, new Color(.008f, .020f, .065f, .84f), dockAccent, 2f);
                PixelUi.DrawText(dockHeader,
                    expeditionShopOpen ? "ДОК ФЛАГМАНА // ВЫБЕРИ МОДУЛЬ" : "ЭКСПЕДИЦИЯ // ПОДХОД К ФЛАГМАНУ",
                    Mathf.Max(3, smallPixel - 1), Color.white, TextAnchor.MiddleCenter);
                DrawExpeditionShopOverlay(left, top, width, height, pixel, smallPixel, pale, panel, cyan, violet);
                DrawUiFade(left, top, width, height);
                return;
            }

            // Solo Expedition combat HUD has moved to editable uGUI objects.
            // Results, shop and the shared pause modal remain on their legacy
            // paths until their own screens are migrated.
            if (soloExpeditionPlaying && !runCompleted && !runFailed &&
                canvasUi != null && canvasUi.ExpeditionHudAvailable)
            {
                DrawUiFade(left, top, width, height);
                return;
            }

            // Expedition uses a single slim header. Its combat health lives on
            // the screen edges and its text telemetry lives below the arena,
            // leaving the complete upper arc visible on narrow phones.
            var header = new Rect(left + width * .035f, top + height * .014f, width * .93f,
                height * (soloExpeditionPlaying ? .052f : .092f));
            PixelUi.DrawPanel(header, new Color(.008f, .020f, .065f, .88f), cyan, 2f);
            PixelUi.DrawText(new Rect(header.x + 10f, header.y + header.height * .04f, header.width * .54f,
                    header.height * (soloExpeditionPlaying ? .88f : .42f)),
                "УЗЕЛ " + (roomIndex + 1).ToString("00") + "/" + rooms.ToString("00") + " // " + SectorRoomLabel(threatRoomType),
                smallPixel, threatColor, TextAnchor.MiddleLeft);
            var rtt = coopSimulation == null ? 0ul : coopSimulation.RoundTripTimeMilliseconds;
            // The solo expedition never needs a run hash. This legacy branch is still drawn on
            // the result screen, so keeping "SOLO // #..." here made it reappear after death.
            if (!soloExpeditionPlaying)
            {
                var networkLabel = coopLocalPreview ? "LOCAL QA" :
                    (coopSimulation != null && coopSimulation.IsNetworkReady
                        ? (multiplayerSessions != null && multiplayerSessions.IsHost ? "HOST" : "GUEST PREDICT") +
                          " // " + rtt + " MS"
                        : "RECONNECT");
                var networkColor = coopLocalPreview || rtt <= 120 ? new Color(.35f, 1f, .68f) :
                    rtt <= 220 ? new Color(1f, .82f, .28f) : new Color(1f, .36f, .42f);
                PixelUi.DrawText(new Rect(header.x + header.width * .55f, header.y + header.height * .04f, header.width * .42f,
                        header.height * .42f),
                    networkLabel, Mathf.Max(3, smallPixel - 1), networkColor, TextAnchor.MiddleRight);
            }
            if (!soloExpeditionPlaying)
            {
                PixelUi.DrawText(new Rect(header.x + 10f, header.y + header.height * .50f,
                        header.width * .44f, header.height * .38f),
                    hostName + " // " + ShipLoadoutSettings.Title(CoopHostShip()), Mathf.Max(3, smallPixel - 1),
                    ShipLoadoutSettings.Get(CoopHostShip()).ProjectileColor, TextAnchor.MiddleLeft);
                PixelUi.DrawText(new Rect(header.x + header.width * .50f, header.y + header.height * .50f, header.width * .47f, header.height * .38f),
                    guestName + " // " + ShipLoadoutSettings.Title(CoopGuestShip()), Mathf.Max(3, smallPixel - 1),
                    ShipLoadoutSettings.Get(CoopGuestShip()).ProjectileColor, TextAnchor.MiddleRight);
            }

            DrawSectorMap(new Rect(left + width * (soloExpeditionPlaying ? .16f : .045f),
                    top + height * (soloExpeditionPlaying ? .078f : .122f),
                    width * (soloExpeditionPlaying ? .68f : .91f), height * (soloExpeditionPlaying ? .034f : .052f)),
                layout, roomIndex);

            var roomReward = CoopRoomRules.RewardAmount(threatRoomType);
            var roomDamage = CoopRoomRules.ThreatDamage(threatRoomType);
            if (soloExpeditionPlaying)
            {
                const int healthSegments = 12;
                var threatSegments = Mathf.CeilToInt(Mathf.Clamp01(threatHealth / (float)Mathf.Max(1, threatMaxHealth)) * healthSegments);
                var teamSegments = Mathf.CeilToInt(Mathf.Clamp01(teamHealth / (float)Mathf.Max(1, teamMaxHealth)) * healthSegments);
                var sideBarWidth = Mathf.Max(22f, width * .038f);
                var sideBarTop = top + height * .205f;
                var sideBarHeight = height * .39f;
                var leftBar = new Rect(left + width * .018f, sideBarTop, sideBarWidth, sideBarHeight);
                var rightBar = new Rect(left + width * .982f - sideBarWidth, sideBarTop, sideBarWidth, sideBarHeight);
                PixelUi.DrawVerticalSegmentBar(leftBar, threatSegments, healthSegments, threatColor,
                    new Color(.06f, .09f, .16f, .76f), threatColor);
                PixelUi.DrawVerticalSegmentBar(rightBar, teamSegments, healthSegments, teamColor,
                    new Color(.06f, .09f, .16f, .76f), teamColor);
                PixelUi.DrawText(new Rect(left, top + height * .162f, width * .105f, height * .030f),
                    threatRoomType == SectorRoomType.Boss ? "БОСС" : "ЦЕЛЬ", Mathf.Max(3, smallPixel - 2), threatColor,
                    TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(left + width * .895f, top + height * .162f, width * .105f, height * .030f),
                    "КОРПУС", Mathf.Max(3, smallPixel - 2), teamColor, TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(left, top + height * .603f, width * .105f, height * .030f),
                    threatHealth + "/" + Mathf.Max(1, threatMaxHealth), Mathf.Max(3, smallPixel - 2), threatColor,
                    TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(left + width * .895f, top + height * .603f, width * .105f, height * .030f),
                    teamHealth + "/" + Mathf.Max(1, teamMaxHealth), Mathf.Max(3, smallPixel - 2), teamColor,
                    TextAnchor.MiddleCenter);
                if (coopRoomIntroTimer <= 0f)
                    PixelUi.DrawText(new Rect(left + width * .11f, top + height * .122f, width * .78f, height * .025f),
                        CoopRoomRules.ObjectiveLabel(threatRoomType) + (roomReward > 0 ? " // +" + roomReward : string.Empty) +
                        "  ·  " + CoopRoomRules.ModifierLabel(threatRoomType), Mathf.Max(3, smallPixel - 2), pale,
                        TextAnchor.MiddleCenter);
            }
            else
            {
                PixelUi.DrawText(new Rect(left + width * .07f, top + height * .18f, width * .86f, height * .027f),
                    CoopRoomRules.ObjectiveLabel(threatRoomType) + (roomReward > 0 ? " // +" + roomReward : string.Empty) +
                    "  ·  " + CoopRoomRules.ModifierLabel(threatRoomType), Mathf.Max(3, smallPixel - 2), pale,
                    TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(left + width * .08f, top + height * .307f, width * .84f, height * .028f),
                    CoopRoomRules.DangerDescription(threatRoomType), Mathf.Max(3, smallPixel - 2),
                    roomDamage > 0 ? new Color(1f, .52f, .58f) : new Color(.48f, 1f, .76f), TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(left + width * .045f, top + height * .213f, width * .43f, height * .025f),
                    "УГРОЗА " + threatHealth + "/" + Mathf.Max(1, threatMaxHealth), Mathf.Max(3, smallPixel - 1), threatColor,
                    TextAnchor.MiddleLeft);
                PixelUi.DrawText(new Rect(left + width * .525f, top + height * .213f, width * .43f, height * .025f),
                    "КОМАНДА " + teamHealth + "/" + Mathf.Max(1, teamMaxHealth), Mathf.Max(3, smallPixel - 1), teamColor,
                    TextAnchor.MiddleRight);
                PixelUi.DrawSegmentBar(new Rect(left + width * .045f, top + height * .241f, width * .43f, height * .021f),
                    threatHealth, Mathf.Max(1, threatMaxHealth), threatColor, new Color(.08f, .12f, .20f, .8f), threatColor);
                PixelUi.DrawSegmentBar(new Rect(left + width * .525f, top + height * .241f, width * .43f, height * .021f),
                    teamHealth, Mathf.Max(1, teamMaxHealth), teamColor, new Color(.08f, .12f, .20f, .8f), teamColor);
            }

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
                PixelUi.DrawText(new Rect(left + width * .14f,
                        top + height * (soloExpeditionPlaying ? (coopRoomIntroTimer > 0f ? .198f : .183f) : .275f),
                        width * .72f, height * .029f),
                    trajectoryLabel, Mathf.Max(3, smallPixel - 1), trajectoryColor, TextAnchor.MiddleCenter);
            }

            if (!soloExpeditionPlaying && relayActive && !runCompleted && !runFailed)
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

            if (!soloExpeditionPlaying && resonanceTimer > 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .18f, top + height * .412f, width * .64f, height * .032f),
                    "РЕЗОНАНС // " + ElementalCombat.ReactionLabel(resonance), smallPixel, resonanceColor, TextAnchor.MiddleCenter);

            var pulseTimer = coopLocalPreview ? coopPreviewThreatPulseTimer : (coopSimulation == null ? 0f : coopSimulation.CoopThreatPulseTimer);
            if (!soloExpeditionPlaying && pulseTimer > 0f && !runCompleted && !runFailed)
            {
                var pulseElement = coopLocalPreview ? coopPreviewThreatPulseElement : coopSimulation.CoopThreatPulseElement;
                var pulsePattern = coopLocalPreview ? coopPreviewThreatPattern : coopSimulation.CoopThreatPattern;
                var pulseTarget = coopLocalPreview ? coopPreviewThreatTargetsHost : coopSimulation.CoopThreatTargetsHost;
                PixelUi.DrawText(new Rect(left + width * .18f, top + height * .447f, width * .64f, height * .032f),
                    CoopThreatAttackRules.Label(pulsePattern) + " // " + (pulseTarget ? "P1" : "P2") +
                    " // " + ElementalCombat.ShortName(pulseElement),
                    smallPixel, CoopElementColor(pulseElement), TextAnchor.MiddleCenter);
            }

            if (!soloExpeditionPlaying && coopHullHitBannerTimer > 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .18f, top + height * .482f, width * .64f, height * .034f),
                    "ПОПАДАНИЕ // -" + coopLastHullDamage + " КОРПУС",
                    smallPixel, new Color(1f, .34f, .42f), TextAnchor.MiddleCenter);

            if (!soloExpeditionPlaying && coopCollisionBannerTimer > 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .17f, top + height * .517f, width * .66f, height * .034f),
                    "БАМ! // КОРАБЛИ ОТСКОЧИЛИ", smallPixel, new Color(.72f, .94f, 1f), TextAnchor.MiddleCenter);

            if (!soloExpeditionPlaying && coopRelayCoreBannerTimer > 0f && coopTetherBannerTimer <= 0f &&
                coopRedirectBannerTimer <= 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .14f, top + height * .522f, width * .72f, height * .038f),
                    coopRelayCoreBanner, smallPixel,
                    coopRelayCoreBanner.Contains("-1") ? new Color(1f, .32f, .40f) : new Color(.56f, .95f, 1f),
                    TextAnchor.MiddleCenter);

            if (!soloExpeditionPlaying && coopTetherBannerTimer > 0f && coopRedirectBannerTimer <= 0f &&
                !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .12f, top + height * .522f, width * .76f, height * .038f),
                    coopTetherBanner, Mathf.Max(3, smallPixel - 1),
                    coopTetherBanner.Contains("ОБРАТКА") ? new Color(1f, .30f, .44f) : new Color(.48f, .94f, 1f),
                    TextAnchor.MiddleCenter);

            if (!soloExpeditionPlaying && coopRedirectBannerTimer > 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .10f, top + height * .522f, width * .80f, height * .038f),
                    coopRedirectBanner, Mathf.Max(3, smallPixel - 1),
                    coopRedirectBanner.Contains("ПИНБОЛ") ? new Color(1f, .64f, .30f) : new Color(.52f, 1f, .82f),
                    TextAnchor.MiddleCenter);

            if (!soloExpeditionPlaying && coopThreatDefeatedBannerTimer > 0f && !runCompleted && !runFailed)
                PixelUi.DrawText(new Rect(left + width * .12f, top + height * .445f, width * .76f, height * .045f),
                    "УГРОЗА УНИЧТОЖЕНА // ПЕРЕХОД", pixel, new Color(.52f, 1f, .74f), TextAnchor.MiddleCenter);

            if (soloExpeditionPlaying && !runCompleted && !runFailed)
            {
                string ticker = null;
                var tickerColor = pale;
                if (coopHullHitBannerTimer > 0f)
                {
                    ticker = "ПОПАДАНИЕ // -" + coopLastHullDamage + " КОРПУС";
                    tickerColor = new Color(1f, .34f, .42f);
                }
                else if (pulseTimer > 0f)
                {
                    var pulseElement = coopLocalPreview ? coopPreviewThreatPulseElement : coopSimulation.CoopThreatPulseElement;
                    var pulsePattern = coopLocalPreview ? coopPreviewThreatPattern : coopSimulation.CoopThreatPattern;
                    ticker = CoopThreatAttackRules.Label(pulsePattern) + " // " + ElementalCombat.ShortName(pulseElement);
                    tickerColor = CoopElementColor(pulseElement);
                }
                else if (resonanceTimer > 0f)
                {
                    ticker = "РЕЗОНАНС // " + ElementalCombat.ReactionLabel(resonance);
                    tickerColor = resonanceColor;
                }
                else if (coopRelayCoreBannerTimer > 0f)
                {
                    ticker = coopRelayCoreBanner;
                    tickerColor = coopRelayCoreBanner.Contains("-1")
                        ? new Color(1f, .32f, .40f)
                        : new Color(.56f, .95f, 1f);
                }
                else if (coopThreatDefeatedBannerTimer > 0f)
                {
                    ticker = "УГРОЗА УНИЧТОЖЕНА // ПЕРЕХОД";
                    tickerColor = new Color(.52f, 1f, .74f);
                }

                if (!string.IsNullOrEmpty(ticker) && coopRoomIntroTimer <= 0f)
                    PixelUi.DrawText(new Rect(left + width * .12f, top + height * .150f, width * .76f, height * .031f),
                        ticker, Mathf.Max(3, smallPixel - 1), tickerColor, TextAnchor.MiddleCenter);
            }

            if (coopRoomIntroTimer > 0f && !runCompleted && !runFailed)
            {
                var intro = soloExpeditionPlaying
                    ? new Rect(left + width * .17f, top + height * .120f, width * .66f, height * .070f)
                    : new Rect(left + width * .16f, top + height * .48f, width * .68f, height * .145f);
                var introAlpha = Mathf.Clamp01(coopRoomIntroTimer / .35f);
                var introAccent = SectorRoomColor(threatRoomType);
                introAccent.a = introAlpha;
                PixelUi.DrawPanel(intro, new Color(.012f, .026f, .075f, .92f * introAlpha), introAccent, 3f);
                if (soloExpeditionPlaying)
                {
                    PixelUi.DrawText(new Rect(intro.x + 8f, intro.y + intro.height * .06f, intro.width - 16f, intro.height * .40f),
                        "КОМНАТА " + (roomIndex + 1).ToString("00") + " // " + SectorRoomLabel(threatRoomType),
                        Mathf.Max(3, smallPixel - 1), introAccent, TextAnchor.MiddleCenter);
                    PixelUi.DrawText(new Rect(intro.x + 8f, intro.y + intro.height * .49f, intro.width - 16f, intro.height * .38f),
                        CoopRoomRules.DangerDescription(threatRoomType), Mathf.Max(3, smallPixel - 2), Color.white,
                        TextAnchor.MiddleCenter);
                }
                else
                {
                    PixelUi.DrawText(new Rect(intro.x + 10f, intro.y + intro.height * .08f, intro.width - 20f, intro.height * .28f),
                        "КОМНАТА " + (roomIndex + 1).ToString("00") + " // " + SectorRoomLabel(threatRoomType),
                        pixel, introAccent, TextAnchor.MiddleCenter);
                    PixelUi.DrawText(new Rect(intro.x + 10f, intro.y + intro.height * .40f, intro.width - 20f, intro.height * .22f),
                        CoopRoomRules.DangerDescription(threatRoomType), Mathf.Max(3, smallPixel - 1), Color.white, TextAnchor.MiddleCenter);
                    PixelUi.DrawText(new Rect(intro.x + 10f, intro.y + intro.height * .66f, intro.width - 20f, intro.height * .20f),
                        roomDamage > 0 ? "ЦЕЛЬ В ЦЕНТРЕ // 4 СЕК ЗАЩИТЫ" : CoopRoomRules.ObjectiveLabel(threatRoomType),
                        Mathf.Max(3, smallPixel - 2), pale, TextAnchor.MiddleCenter);
                }
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
                    LivingCosmosActive ? "ПРОТОТИП // БЕЗ РЕЙТИНГА" : "MMR " + (coopResultMmrDelta >= 0 ? "+" : string.Empty) + coopResultMmrDelta + "  //  " + mmr,
                    smallPixel, resultMmrColor, TextAnchor.MiddleCenter);
            }

            if (soloExpeditionPlaying && (runCompleted || runFailed))
            {
                if (DrawPixelButton(new Rect(left + width * .25f, top + height * .775f, width * .23f, height * .055f), "ЕЩЕ РАЗ", smallPixel,
                        new Color(.05f, .18f, .20f, .94f), cyan, Color.white))
                    BeginSoloExpeditionRun(LivingCosmosActive);
                if (DrawPixelButton(new Rect(left + width * .52f, top + height * .775f, width * .23f, height * .055f), "МЕНЮ", smallPixel,
                        new Color(.13f, .035f, .09f, .90f), new Color(1f, .32f, .45f), Color.white))
                    ExitCoopRun();
            }
            else if ((runCompleted || runFailed) &&
                     DrawPixelButton(new Rect(left + width * .39f, top + height * .90f, width * .22f, height * .05f),
                         "МЕНЮ", smallPixel, new Color(.13f, .035f, .09f, .90f), new Color(1f, .32f, .45f), Color.white))
                ExitCoopRun();
            if (soloExpeditionPlaying && (expeditionShopDocking || expeditionShopOpen))
                DrawExpeditionShopOverlay(left, top, width, height, pixel, smallPixel, pale, panel, cyan, violet);
            else if (soloExpeditionPlaying && expeditionUpgradeNoticeTimer > 0f && !string.IsNullOrEmpty(expeditionUpgradeNotice))
                PixelUi.DrawText(new Rect(left + width * .12f, top + height * .59f, width * .76f, height * .04f),
                    expeditionUpgradeNotice, smallPixel, new Color(.62f, 1f, .78f), TextAnchor.MiddleCenter);
            DrawUiFade(left, top, width, height);
        }

        private void DrawExpeditionShopOverlay(float left, float top, float width, float height, int pixel,
            int smallPixel, Color pale, Color panel, Color cyan, Color violet)
        {
            var accent = new Color(.68f, .48f, 1f);
            if (!expeditionShopOpen)
            {
                // Keep the arena visible throughout the flight; this is a
                // cinematic status strip rather than a modal blocking window.
                var progress = Mathf.Clamp01(expeditionShopDockTimer / ShopDockSequenceDuration);
                var strip = new Rect(left + width * .19f, top + height * .805f, width * .62f, height * .070f);
                PixelUi.DrawPanel(strip, new Color(.012f, .026f, .080f, .84f), accent, 2f);
                var phaseLabel = expeditionShopDockTimer < ShopApproachDuration
                    ? "ПЕРЕХОД К ФЛАГМАНУ // СТЫКОВОЧНЫЙ КУРС"
                    : "ШЛЮЗ ЗАХВАЧЕН // ФИНАЛЬНАЯ СТЫКОВКА";
                PixelUi.DrawText(new Rect(strip.x + 8f, strip.y + strip.height * .05f, strip.width - 16f, strip.height * .47f),
                    phaseLabel, Mathf.Max(3, smallPixel - 1), Color.white, TextAnchor.MiddleCenter);
                PixelUi.DrawSegmentBar(new Rect(strip.x + strip.width * .10f, strip.y + strip.height * .61f,
                        strip.width * .80f, strip.height * .18f),
                    Mathf.RoundToInt(progress * 16f), 16, cyan, new Color(.05f, .07f, .18f), violet);
                return;
            }

            var dock = new Rect(left + width * .07f, top + height * .22f, width * .86f, height * .58f);
            PixelUi.DrawPanel(dock, new Color(.015f, .025f, .09f, .965f), accent, 4f);

            PixelUi.DrawText(new Rect(dock.x + 10f, dock.y + dock.height * .045f, dock.width - 20f, dock.height * .07f),
                LivingCosmosActive ? "ДОК // ОСТАЛОСЬ ВЫБОРОВ: " + livingCosmos.ShopChoicesRemaining : "ДОК ФЛАГМАНА // ВЫБЕРИ 1 МОДУЛЬ", pixel, Color.white, TextAnchor.MiddleCenter);
            PixelUi.DrawText(new Rect(dock.x + 12f, dock.y + dock.height * .125f, dock.width - 24f, dock.height * .045f),
                "МОДУЛЬ ОСТАНЕТСЯ ДО КОНЦА ЭКСПЕДИЦИИ", smallPixel, pale, TextAnchor.MiddleCenter);
            const int columns = 2;
            const int rows = 3;
            var gridX = dock.x + dock.width * .065f;
            var gridY = dock.y + dock.height * .205f;
            var gapX = dock.width * .04f;
            var gapY = dock.height * .035f;
            var itemWidth = (dock.width - dock.width * .13f - gapX) / columns;
            var itemHeight = (dock.height * .72f - gapY * (rows - 1)) / rows;
            for (var index = 0; index < 6; index++)
            {
                var upgrade = (ExpeditionUpgrade)index;
                var column = index % columns;
                var row = index / columns;
                var rect = new Rect(gridX + column * (itemWidth + gapX), gridY + row * (itemHeight + gapY), itemWidth, itemHeight);
                var color = ExpeditionUpgradeColor(upgrade);
                var rank = ExpeditionUpgradeRank(upgrade);
                var maxRank = ExpeditionUpgradeMaxRanks(upgrade);
                var isMaxed = rank >= maxRank;
                var label = ExpeditionUpgradeTitle(upgrade) + "\nВЗЯТО " + rank + "/" + maxRank +
                    "\n" + ExpeditionUpgradeDescription(upgrade);
                var background = isMaxed ? new Color(.035f, .045f, .08f, .96f) : new Color(.035f, .055f, .16f, .98f);
                var frame = isMaxed ? new Color(.34f, .40f, .52f) : color;
                var text = isMaxed ? new Color(.56f, .64f, .74f) : Color.white;
                var clicked = DrawPixelButton(rect, label, Mathf.Max(3, smallPixel - 2), background, frame, text);
                if (!isMaxed && clicked)
                    ApplyExpeditionUpgrade(upgrade);
            }
        }

        private void ExitClassicMode()
        {
            playing = false;
            SetPaused(false);
            Cleanup();
            if (player != null) player.gameObject.SetActive(true);
            showMenu = true;
            showResults = false;
            showSettings = false;
            activeControlDirection = 0;
            StopGameplayMusic();
            BeginUiFade();
        }

        private void DrawDefenseHud(float left, float top, float width, float height, int pixel, int smallPixel,
            Color pale, Color panel, Color cyan, Color violet)
        {
            var scoreRect = new Rect(left + width * .04f, top + height * .025f, width * .43f, height * .10f);
            var hullRect = new Rect(left + width * .53f, top + height * .025f, width * .43f, height * .10f);
            PixelUi.DrawPanel(scoreRect, new Color(.04f, .055f, .14f, .95f), cyan, 3f);
            PixelUi.DrawPanel(hullRect, new Color(.15f, .025f, .07f, .95f), new Color(1f, .34f, .46f), 3f);
            PixelUi.DrawText(scoreRect, "ЗАЩИТА // ВОЛНА " + defenseWave + "\nУНИЧТОЖЕНО " + defenseKills, smallPixel, cyan);
            PixelUi.DrawText(new Rect(hullRect.x + 10f, hullRect.y + hullRect.height * .08f, hullRect.width - 20f, hullRect.height * .22f),
                "КОРПУС ФЛАГМАНА", smallPixel, pale, TextAnchor.MiddleCenter);
            var hullColor = defenseFlagshipPulse > 0f ? Color.white : new Color(.38f, 1f, .72f);
            PixelUi.DrawSegmentBar(new Rect(hullRect.x + hullRect.width * .08f, hullRect.y + hullRect.height * .44f,
                hullRect.width * .84f, hullRect.height * .28f), defenseHull, defenseMaxHull, hullColor,
                new Color(.18f, .035f, .06f, .96f), new Color(1f, .34f, .46f));
            PixelUi.DrawText(new Rect(hullRect.x + 8f, hullRect.y + hullRect.height * .75f, hullRect.width - 16f, hullRect.height * .18f),
                "БРОНЯ Л/Ц/П  " + (3 - defenseFlagshipSectionDamage[0]) + "/" +
                (3 - defenseFlagshipSectionDamage[1]) + "/" + (3 - defenseFlagshipSectionDamage[2]),
                Mathf.Max(3, smallPixel - 2), new Color(1f, .68f, .38f), TextAnchor.MiddleCenter);

            if (!defenseRunOver)
            {
                PixelUi.DrawText(new Rect(left + width * .12f, top + height * .17f, width * .76f, height * .04f),
                    defenseStatus, smallPixel, new Color(.72f, .94f, 1f), TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(left + width * .12f, top + height * .82f, width * .76f, height * .04f),
                    "ДЕРЖИ ОРБИТУ // СТРЕЛЯЙ В ПРИБЛИЖАЮЩИЕСЯ ЦЕЛИ", smallPixel, pale, TextAnchor.MiddleCenter);
            }
            else
            {
                var result = new Rect(left + width * .12f, top + height * .36f, width * .76f, height * .30f);
                PixelUi.DrawPanel(result, new Color(.18f, .018f, .06f, .97f), new Color(1f, .28f, .42f), 4f);
                PixelUi.DrawText(new Rect(result.x + 8f, result.y + result.height * .10f, result.width - 16f, result.height * .25f),
                    "ФЛАГМАН УНИЧТОЖЕН", pixel, Color.white, TextAnchor.MiddleCenter);
                PixelUi.DrawText(new Rect(result.x + 8f, result.y + result.height * .38f, result.width - 16f, result.height * .18f),
                    "ВОЛНА " + defenseWave + " // УНИЧТОЖЕНО " + defenseKills + " // СЧЕТ " + score,
                    smallPixel, pale, TextAnchor.MiddleCenter);
                if (DrawPixelButton(new Rect(result.x + result.width * .10f, result.y + result.height * .68f, result.width * .36f, result.height * .18f),
                        "ЕЩЕ РАЗ", smallPixel, new Color(.05f, .18f, .20f, .94f), cyan, Color.white))
                    BeginDefenseMode();
                if (DrawPixelButton(new Rect(result.x + result.width * .54f, result.y + result.height * .68f, result.width * .36f, result.height * .18f),
                        "МЕНЮ", smallPixel, new Color(.13f, .035f, .09f, .90f), new Color(1f, .32f, .45f), Color.white))
                    ExitDefenseMode();
            }
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

            var toggleY = settings.y + settings.height * .43f;
            var toggleWidth = settings.width * .32f;
            var toggleHeight = settings.height * .10f;
            if (DrawPixelButton(new Rect(settings.x + settings.width * .16f, toggleY, toggleWidth, toggleHeight), GameAudioSettings.MusicEnabled ? "МУЗЫКА: ВКЛ" : "МУЗЫКА: ВЫКЛ", smallPixel, panel, cyan, pale)) ToggleMusic();
            if (DrawPixelButton(new Rect(settings.x + settings.width * .52f, toggleY, toggleWidth, toggleHeight), GameAudioSettings.EffectsEnabled ? "SFX: ВКЛ" : "SFX: ВЫКЛ", smallPixel, panel, violet, pale)) ToggleEffects();
            if (DrawPixelButton(new Rect(settings.x + settings.width * .16f, settings.y + settings.height * .57f, toggleWidth, toggleHeight), HapticFeedback.Enabled ? "ВИБРО: ВКЛ" : "ВИБРО: ВЫКЛ", smallPixel, panel, cyan, pale)) HapticFeedback.Toggle();
            if (DrawPixelButton(new Rect(settings.x + settings.width * .52f, settings.y + settings.height * .57f, toggleWidth, toggleHeight), GameVisualSettings.ScreenShakeEnabled ? "ТРЯСКА: ВКЛ" : "ТРЯСКА: ВЫКЛ", smallPixel, panel, violet, pale)) GameVisualSettings.ToggleScreenShake();

            var zoomY = settings.y + settings.height * .70f;
            PixelUi.DrawText(new Rect(settings.x + settings.width * .16f, zoomY, settings.width * .38f, toggleHeight),
                "ПОЛЕ: " + GameplayCameraZoomSettings.PercentLabel, smallPixel, pale);
            if (DrawPixelButton(new Rect(settings.x + settings.width * .59f, zoomY, settings.width * .12f, toggleHeight), "-", smallPixel, panel, cyan, Color.white))
            {
                GameplayCameraZoomSettings.Adjust(-1f);
                UpdateCameraFraming(true);
            }
            if (DrawPixelButton(new Rect(settings.x + settings.width * .75f, zoomY, settings.width * .12f, toggleHeight), "+", smallPixel, panel, cyan, Color.white))
            {
                GameplayCameraZoomSettings.Adjust(1f);
                UpdateCameraFraming(true);
            }

            if (DrawPixelButton(new Rect(settings.x + settings.width * .16f, settings.y + settings.height * .80f, settings.width * .68f, toggleHeight),
                    MusicReactiveSettings.Enabled ? "РЕАКТИВНАЯ ВНЕШНЯЯ МУЗЫКА: ВКЛ" : "РЕАКТИВНАЯ ВНЕШНЯЯ МУЗЫКА: ВЫКЛ",
                    smallPixel, panel, new Color(.34f, 1f, .68f), pale))
                ToggleMusicReactiveVisuals();

            PixelUi.DrawText(new Rect(settings.x + 20f, settings.y + settings.height * .89f, settings.width - 40f, settings.height * .035f),
                ExternalMusicAudioBridge.StatusLabel, Mathf.Max(3, smallPixel - 1), new Color(.55f, .72f, .9f));
            if (DrawPixelButton(new Rect(settings.x + settings.width * .28f, settings.y + settings.height * .94f, settings.width * .44f, settings.height * .07f), "ГОТОВО", smallPixel, new Color(.07f, .13f, .30f, .98f), cyan, Color.white)) CloseSettings();
        }

        private void BindCanvasUi()
        {
            if (canvasUi == null) return;
            if (canvasUi.CosmosMap != null)
            {
                canvasUi.CosmosMap.DestinationRequested -= ChooseLivingDestination;
                canvasUi.CosmosMap.DestinationRequested += ChooseLivingDestination;
                canvasUi.CosmosMap.ResumeRequested -= ResumeFromCanvas;
                canvasUi.CosmosMap.ResumeRequested += ResumeFromCanvas;
                canvasUi.CosmosMap.ExitRequested -= ExitLivingMap;
                canvasUi.CosmosMap.ExitRequested += ExitLivingMap;
            }
            if (canvasUi.TempoReward != null)
            {
                canvasUi.TempoReward.ChoiceRequested -= ChooseLivingTempoModule;
                canvasUi.TempoReward.ChoiceRequested += ChooseLivingTempoModule;
            }
            canvasUi.PauseRequested -= PauseFromCanvas;
            canvasUi.PauseRequested += PauseFromCanvas;
            canvasUi.ResumeRequested -= ResumeFromCanvas;
            canvasUi.ResumeRequested += ResumeFromCanvas;
            canvasUi.ExitRequested -= ExitFromCanvas;
            canvasUi.ExitRequested += ExitFromCanvas;
        }

        private void UnbindCanvasUi()
        {
            if (expeditionModeChoice != null)
            {
                expeditionModeChoice.Selected -= BeginSoloExpeditionRun;
                expeditionModeChoice.ResumeLivingRequested -= ResumeLivingCosmosRun;
            }
            if (canvasUi == null) return;
            if (canvasUi.CosmosMap != null)
            {
                canvasUi.CosmosMap.DestinationRequested -= ChooseLivingDestination;
                canvasUi.CosmosMap.ResumeRequested -= ResumeFromCanvas;
                canvasUi.CosmosMap.ExitRequested -= ExitLivingMap;
            }
            if (canvasUi.TempoReward != null)
                canvasUi.TempoReward.ChoiceRequested -= ChooseLivingTempoModule;
            canvasUi.PauseRequested -= PauseFromCanvas;
            canvasUi.ResumeRequested -= ResumeFromCanvas;
            canvasUi.ExitRequested -= ExitFromCanvas;
        }

        private bool CanPauseCurrentRun()
        {
            if (coopPlaying)
            {
                var completed = coopLocalPreview ? coopPreviewCompleted : coopSimulation != null && coopSimulation.RunCompleted;
                var failed = coopLocalPreview ? coopPreviewFailed : coopSimulation != null && coopSimulation.RunFailed;
                return !completed && !failed && !LivingRewardChoice && (!soloExpeditionPlaying || (!expeditionShopOpen && !expeditionShopDocking));
            }
            if (defensePlaying) return !defenseRunOver;
            return playing && !showResults;
        }

        private void SetPaused(bool value)
        {
            if (paused == value)
            {
                if (!value) RestorePauseTimeScale();
                return;
            }

            if (value)
            {
                pausePreviousTimeScale = Time.timeScale;
                pauseTimeScaleApplied = true;
                paused = true;
                musicReactiveVisuals?.SetGameplayPaused(true);
                Time.timeScale = 0f;
            }
            else
            {
                paused = false;
                RestorePauseTimeScale();
            }
        }

        private void RestorePauseTimeScale()
        {
            if (pauseTimeScaleApplied)
            {
                Time.timeScale = pausePreviousTimeScale;
                pauseTimeScaleApplied = false;
            }
            musicReactiveVisuals?.SetGameplayPaused(false);
        }

        private void ChooseLivingDestination(int node)
        {
            if (!LivingCosmosActive || paused) return;
            livingCosmos.TryChoose(node);
        }

        private void ChooseLivingTempoModule(TempoModule module, TempoModule replace)
        {
            if (!LivingRewardChoice || livingTempo == null) return;
            var pending=livingTempo.Pending;
            if (pending == null || !livingTempo.Choose(pending.RewardId,module,replace)) return;
            if (livingCheckpointStore == null) livingCheckpointStore=new LivingCosmosCheckpointStore();
            livingCheckpointStore.Clear();
            livingCosmos.ContinueAfterReward();
            expeditionUpgradeNotice="ИМПУЛЬ УСТАНОВЛЕН · "+TempoRewardRules.Name(module)+" · 2 БОЯ";
            expeditionUpgradeNoticeTimer=2.4f;
            if (player != null) SpawnImpactBurst(player.position,new Color(.62f,.92f,1f),26,2.8f,.36f);
            HapticFeedback.Pulse(40);
        }

        private void SaveLivingRewardCheckpoint()
        {
            if (!LivingRewardChoice || livingTempo == null) return;
            if (livingCheckpointStore == null) livingCheckpointStore=new LivingCosmosCheckpointStore();
            var checkpoint=new LivingCosmosCheckpoint {
                seed=coopPreviewRunSeed, runId=coopResultRunId, route=livingCosmos.CreateCheckpoint(), tempo=livingTempo.CreateCheckpoint(),
                teamHealth=coopPreviewTeamHealth, fireIntervalMilli=Mathf.RoundToInt(expeditionFireIntervalMultiplier*1000f),
                projectileSpeedMilli=Mathf.RoundToInt(expeditionProjectileSpeedMultiplier*1000f), damageBonus=expeditionDamageBonus,
                prismLevel=expeditionPrismLevel, aegisCharges=expeditionAegisCharges, fieldRepairLevel=expeditionFieldRepairLevel,
                aegisLevel=expeditionAegisLevel
            };
            if (!livingCheckpointStore.Save(checkpoint))
            {
                expeditionUpgradeNotice="ИМПУЛЬ НАЙДЕН · ЛОКАЛЬНОЕ СОХРАНЕНИЕ НЕДОСТУПНО";
                expeditionUpgradeNoticeTimer=3f;
            }
        }

        private void ExitLivingMap()
        {
            if (!LivingCosmosActive) return;
            SetPaused(true);
            ExitFromCanvas(); // same exit/audio/cleanup route as the shared pause component
        }

        private string PauseModeLabel()
        {
            if (LivingCosmosActive) return "ЖИВОЙ КОСМОС // БЕЗ РЕЙТИНГА";
            if (coopPlaying) return soloExpeditionPlaying ? "СОЛО // ЭКСПЕДИЦИЯ" : "КООП // СЕКТОР";
            if (defensePlaying) return "ЗАЩИТА ФЛАГМАНА";
            return "СОЛО // КЛАССИКА";
        }

        private void PauseFromCanvas()
        {
            if (!CanPauseCurrentRun()) return;
            SetPaused(true);
            activeControlDirection = 0;
        }

        private void ResumeFromCanvas()
        {
            if (!paused) return;
            SetPaused(false);
            activeControlDirection = 0;
        }

        private void ExitFromCanvas()
        {
            if (!paused) return;
            if (coopPlaying) ExitCoopRun();
            else if (defensePlaying) ExitDefenseMode();
            else if (playing) ExitClassicMode();
        }

        private void UpdateCanvasUi()
        {
            if (canvasUi == null)
            {
                canvasUi = FindFirstObjectByType<OrbitalRiftCanvasRoot>();
                BindCanvasUi();
            }
            if (canvasUi == null) return;
            canvasUi.SetExpeditionHud(BuildExpeditionHudModel());
            var sandboxActive = abilitySandbox.IsOpen;
            canvasUi.SetPauseOverlay(CanPauseCurrentRun() && !sandboxActive && !LivingMapVisible && !LivingRewardChoice, paused, PauseModeLabel());
            var pausedBoss = ActiveBoss();
            var phaseBoss = pausedBoss != null
                ? (BossArchetype?)pausedBoss.BossType
                : BossArchetypeSettings.IsBossWave(phase) ? BossArchetypeSettings.ForPhase(phase) : (BossArchetype?)null;
            canvasUi.SetBossAbilityGuide(phaseBoss,
                paused && !sandboxActive && phaseBoss.HasValue && CanPauseCurrentRun() && !LivingMapVisible && !LivingRewardChoice);
            canvasUi.CosmosMap?.Apply(LivingCosmosActive ? livingCosmos : null, paused);
            canvasUi.TempoReward?.Apply(LivingRewardChoice ? livingTempo : null);
        }

        private ExpeditionHudModel BuildExpeditionHudModel()
        {
            var runCompleted = coopLocalPreview ? coopPreviewCompleted : coopSimulation != null && coopSimulation.RunCompleted;
            var runFailed = coopLocalPreview ? coopPreviewFailed : coopSimulation != null && coopSimulation.RunFailed;
            var model = expeditionHudModel;
            model.Visible = coopPlaying && soloExpeditionPlaying && !paused && !LivingMapVisible && !LivingRewardChoice && !expeditionShopDocking && !expeditionShopOpen &&
                            !runCompleted && !runFailed;
            if (!model.Visible) return model;

            var layout = coopLocalPreview ? coopPreviewSector : multiplayerSessions == null ? null : multiplayerSessions.CurrentSector;
            var roomCount = layout == null ? 0 : layout.Rooms.Count;
            var roomIndex = coopLocalPreview ? coopPreviewRoomIndex : coopSimulation == null ? 0 : coopSimulation.ActiveRoomIndex;
            roomIndex = Mathf.Clamp(roomIndex, 0, Mathf.Max(0, roomCount - 1));
            var threatKind = coopLocalPreview ? coopPreviewEnemyKind : coopSimulation == null ? (byte)0 : coopSimulation.CoopEnemyKind;
            var roomType = (SectorRoomType)Mathf.Clamp(threatKind, 0, (int)SectorRoomType.Boss);
            if (layout != null && roomIndex < layout.Rooms.Count) roomType = layout.Rooms[roomIndex].Type;

            var threatHealth = coopLocalPreview ? coopPreviewEnemyHealth : coopSimulation == null ? 0 : coopSimulation.CoopEnemyHealth;
            var threatMaxHealth = coopLocalPreview ? coopPreviewEnemyMaxHealth : coopSimulation == null ? 0 : coopSimulation.CoopEnemyMaxHealth;
            var hullHealth = coopLocalPreview ? coopPreviewTeamHealth : coopSimulation == null ? 0 : coopSimulation.CoopTeamHealth;
            var hullMaxHealth = coopLocalPreview ? coopPreviewTeamMaxHealth : coopSimulation == null ? CoopRoomRules.TeamMaxHealth : coopSimulation.CoopTeamMaxHealth;
            var threatRatio = Mathf.Clamp01(threatHealth / (float)Mathf.Max(1, threatMaxHealth));
            var hullRatio = Mathf.Clamp01(hullHealth / (float)Mathf.Max(1, hullMaxHealth));
            var criticalRed = new Color(1f, .12f, .20f);
            var depletedRed = new Color(.19f, .018f, .040f, .94f);
            var threatBaseColor = SectorRoomColor(roomType);
            // Remaining cubes progressively heat from their room color to red. Missing cubes stay
            // dark red, so damage is readable immediately instead of looking like neutral padding.
            var threatColor = Color.Lerp(criticalRed, threatBaseColor,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, 1f, threatRatio)));
            var hullColor = Color.Lerp(new Color(1f, .20f, .25f), new Color(.34f, 1f, .68f),
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, 1f, hullRatio)));
            // The compact HUD lives in the upper-left. Keep it player-facing: current
            // room type first, then how many rooms have already been cleared.
            var roomsCleared = Mathf.Clamp(roomIndex, 0, roomCount);
            model.RoomLabel = SectorRoomLabel(roomType) + "\nПРОЙДЕНО " +
                              roomsCleared.ToString("00") + "/" + roomCount.ToString("00");
            var hasNextRoom = layout != null && roomIndex + 1 < layout.Rooms.Count;
            var nextRoomType = hasNextRoom ? layout.Rooms[roomIndex + 1].Type : SectorRoomType.Boss;
            model.ObjectiveLabel = hasNextRoom
                ? "ДАЛЕЕ\n" + SectorRoomLabel(nextRoomType)
                : "ДАЛЕЕ\nСЕКТОР ЗАЧИЩЕН";
            model.NextRoomColor = hasNextRoom ? SectorRoomColor(nextRoomType) : new Color(.62f, 1f, .78f);
            model.ThreatTitle = roomType == SectorRoomType.Boss ? "БОСС" : "ЦЕЛЬ";
            model.ThreatValue = threatHealth + "/" + Mathf.Max(1, threatMaxHealth);
            model.HullValue = hullHealth + "/" + Mathf.Max(1, hullMaxHealth);
            model.ThreatColor = threatColor;
            model.ThreatEmptyColor = depletedRed;
            model.HullColor = hullColor;
            model.HullEmptyColor = depletedRed;
            model.ThreatSegments = Mathf.CeilToInt(Mathf.Clamp01(threatHealth / (float)Mathf.Max(1, threatMaxHealth)) * 12f);
            model.HullSegments = Mathf.CeilToInt(Mathf.Clamp01(hullHealth / (float)Mathf.Max(1, hullMaxHealth)) * 12f);

            if (layout != null)
            {
                if (model.Rooms == null || model.Rooms.Length != layout.Rooms.Count)
                    model.Rooms = new ExpeditionRoomNodeModel[layout.Rooms.Count];
                for (var i = 0; i < layout.Rooms.Count; i++)
                    model.Rooms[i] = new ExpeditionRoomNodeModel(SectorRoomColor(layout.Rooms[i].Type), LivingCosmosActive ? livingCosmos.Cleared.Contains(i) : i < roomIndex, i == roomIndex);
            }
            else model.Rooms = System.Array.Empty<ExpeditionRoomNodeModel>();

            var trajectoryTime = coopLocalPreview ? coopPreviewTrajectoryTime : coopSimulation == null ? 0f : coopSimulation.TrajectoryTimeSeconds;
            var trajectoryState = CoopTrajectorySettings.Evaluate(trajectoryTime);
            // Match the understated status styling below the active room label. The trajectory
            // remains readable during a morph without competing with room-type colours.
            model.TrajectoryColor = new Color(.70f, .70f, .74f);
            model.TrajectoryLabel = trajectoryState.IsTransitioning
                ? "СМЕНА ТРАЕКТОРИИ\n" + CoopTrajectorySettings.Label(trajectoryState.From) + " > " +
                  CoopTrajectorySettings.Label(trajectoryState.To) + "  " + Mathf.RoundToInt(trajectoryState.Blend * 100f) + "%"
                : "СМЕНА ТРАЕКТОРИИ\n" + Mathf.CeilToInt(trajectoryState.SecondsUntilTransition) + " СЕК";

            model.TickerLabel = string.Empty;
            model.TickerColor = new Color(.82f, .93f, 1f);
            if (coopHullHitBannerTimer > 0f)
            {
                model.TickerLabel = "ПОПАДАНИЕ // -" + coopLastHullDamage + " КОРПУС";
                model.TickerColor = new Color(1f, .34f, .42f);
            }
            else if (expeditionUpgradeNoticeTimer > 0f && !string.IsNullOrEmpty(expeditionUpgradeNotice))
            {
                model.TickerLabel = expeditionUpgradeNotice;
                model.TickerColor = new Color(.62f, 1f, .78f);
            }
            else
            {
                var pulseTimer = coopLocalPreview ? coopPreviewThreatPulseTimer : coopSimulation == null ? 0f : coopSimulation.CoopThreatPulseTimer;
                if (pulseTimer > 0f)
                {
                    var pulseElement = coopLocalPreview ? coopPreviewThreatPulseElement : coopSimulation == null ? DamageElement.Kinetic : coopSimulation.CoopThreatPulseElement;
                    var pulsePattern = coopLocalPreview ? coopPreviewThreatPattern : coopSimulation == null ? CoopThreatPattern.Cleave : coopSimulation.CoopThreatPattern;
                    model.TickerLabel = CoopThreatAttackRules.Label(pulsePattern) + " // " + ElementalCombat.ShortName(pulseElement);
                    model.TickerColor = CoopElementColor(pulseElement);
                }
                else
                {
                    var resonance = coopLocalPreview ? coopPreviewResonance : coopSimulation == null ? ElementalReaction.None : coopSimulation.CoopResonance;
                    var resonanceTimer = coopLocalPreview ? coopPreviewResonanceTimer : coopSimulation == null ? 0f : coopSimulation.CoopResonanceTimer;
                    if (resonanceTimer > 0f)
                    {
                        model.TickerLabel = "РЕЗОНАНС // " + ElementalCombat.ReactionLabel(resonance);
                        model.TickerColor = new Color(1f, .82f, .32f);
                    }
                    else if (coopRelayCoreBannerTimer > 0f)
                    {
                        model.TickerLabel = coopRelayCoreBanner;
                        model.TickerColor = coopRelayCoreBanner.Contains("-1") ? new Color(1f, .32f, .40f) : new Color(.56f, .95f, 1f);
                    }
                    else if (coopThreatDefeatedBannerTimer > 0f)
                    {
                        model.TickerLabel = "УГРОЗА УНИЧТОЖЕНА // ПЕРЕХОД";
                        model.TickerColor = new Color(.52f, 1f, .74f);
                    }
                }
            }

            model.IntroAlpha = coopRoomIntroTimer > 0f ? Mathf.Clamp01(coopRoomIntroTimer / .35f) : 0f;
            model.IntroColor = threatColor;
            model.IntroTitle = "КОМНАТА " + (roomIndex + 1).ToString("00") + " // " + SectorRoomLabel(roomType);
            model.IntroSubtitle = CoopRoomRules.DangerDescription(roomType);
            if (LivingCosmosActive)
            {
                model.IntroTitle = LivingCosmosRunState.RegionName(livingCosmos.Region);
                model.IntroSubtitle = LivingCosmosRunState.NodeName(roomIndex) + " · " + SectorRoomLabel(roomType);
                if (coopPreviewLensesActive)
                {
                    // The marker directions are physical gameplay information, not merely a
                    // colour pairing: ordinary shots and the relay core leave at that arrow.
                    model.IntroTitle = "ПАРНЫЕ ЛИНЗЫ";
                    model.IntroSubtitle = "СНАРЯДЫ И ЯДРО: ПЕРЕХОД ЧЕРЕЗ ЛИНЗЫ";
                }
                model.RoomLabel = SectorRoomLabel(roomType) + "\nПРОЙДЕНО " + livingCosmos.ClearedCount;
                var connections = livingCosmos.Layout.Rooms[roomIndex].Connections;
                model.ObjectiveLabel = "ДАЛЕЕ\n" + (connections.Count > 1 ? "ВЫБОР КУРСА" : connections.Count == 1 ? SectorRoomLabel(layout.Rooms[connections[0]].Type) : "ЦЕЛЬ ПУТИ");
            }
            return model;
        }

        private Matrix4x4 BossMirrorGuiMatrix()
        {
            // IMGUI is drawn after the camera image, so it cannot participate in the camera
            // post-process. This companion transform keeps the HUD inside the same unsettled
            // broken-space moment while the first boss is alive.
            var elapsed = Mathf.Max(0f, Time.unscaledTime - bossMirrorStartedAt);
            var appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .42f));
            var drift = new Vector3(
                Mathf.Sin(elapsed * 7.2f) * 10f * appear,
                Mathf.Cos(elapsed * 5.6f) * 6.5f * appear,
                0f);
            var pivot = new Vector3(Screen.width * .5f, Screen.height * .5f, 0f);
            return Matrix4x4.Translate(pivot + drift) *
                Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 3.2f) * .82f * appear)) *
                Matrix4x4.Scale(Vector3.one * (1f + .013f * appear)) *
                Matrix4x4.Translate(-pivot);
        }

        private void DrawOrbitalAbilityHud(float left, float top, float width, float height, int smallPixel, Color pale)
        {
            var y = top + height * .872f;
            var buttonWidth = width * .255f;
            var buttonHeight = height * .078f;
            var gap = width * .025f;
            var startX = left + width * .5f - (buttonWidth * 2f + gap) * .5f;

            DrawAbilityButton(new Rect(startX, y, buttonWidth, buttonHeight), "ЭХО", "Q", riftEchoCooldown,
                new Color(.04f, .18f, .24f, .94f), new Color(.34f, .94f, 1f), smallPixel, pale, ref queuedRiftEcho);
            DrawAbilityButton(new Rect(startX + buttonWidth + gap, y, buttonWidth, buttonHeight), "СКАЧОК", "E", vectorSnapCooldown,
                new Color(.18f, .07f, .28f, .94f), new Color(.86f, .56f, 1f), smallPixel, pale, ref queuedVectorSnap);

            if (playerRootTimer > 0f)
                PixelUi.DrawText(new Rect(left, y - height * .040f, width, height * .028f),
                    "ГРАВКОРНИ  " + playerRootTimer.ToString("0.0") + "s", smallPixel, new Color(1f, .38f, .78f));
        }

        private void DrawAbilityButton(Rect rect, string label, string key, float cooldown, Color background,
            Color accent, int smallPixel, Color pale, ref bool queued)
        {
            var ready = cooldown <= 0f && playerRootTimer <= 0f;
            var frame = ready ? accent : new Color(.28f, .34f, .44f, .76f);
            var fill = ready ? background : new Color(.025f, .04f, .09f, .82f);
            PixelUi.DrawPanel(rect, fill, frame, 3f);
            var caption = ready ? label + "\n" + key : label + "\n" + cooldown.ToString("0.0");
            PixelUi.DrawText(rect, caption, Mathf.Max(3, smallPixel - 1), ready ? pale : new Color(.55f, .62f, .72f));
            if (ready && GUI.Button(rect, GUIContent.none, GUIStyle.none)) queued = true;
        }

        private void DrawCombatMoment(float left, float top, float width, float height, int smallPixel)
        {
            if (!combatMoments.IsVisible) return;
            var alpha = Mathf.Clamp01(Mathf.Min(combatMoments.TimeLeft / .22f, 1f));
            var rect = new Rect(left + width * .18f, top + height * .665f, width * .64f, height * .064f);
            var color = combatMoments.Color;
            PixelUi.DrawPanel(rect, new Color(.015f, .025f, .075f, .74f * alpha),
                new Color(color.r, color.g, color.b, .78f * alpha), 3f);
            PixelUi.DrawText(rect, combatMoments.Caption, smallPixel, new Color(color.r, color.g, color.b, alpha));
        }

        private Sprite BossAbilitySprite(BossAbilityInfo info)
        {
            if (info != null && info.Asset != null && info.Asset.Icon != null) return info.Asset.Icon;
            if (info == null) return null;
            switch (info.Id)
            {
                case BossAbilityId.FirebirdSolarChicks: return firebirdChicksAbilitySprite;
                case BossAbilityId.FirebirdAshenEgg: return firebirdEggAbilitySprite;
                case BossAbilityId.FirebirdPhoenixDive: return firebirdDiveAbilitySprite;
                case BossAbilityId.HarrierRiftCopies: return harrierCopiesAbilitySprite;
                case BossAbilityId.HarrierPhaseDash: return harrierDashAbilitySprite;
                case BossAbilityId.HarrierColdFan: return harrierFanAbilitySprite;
                case BossAbilityId.VoidRiftBeam: return voidBeamAbilitySprite;
                case BossAbilityId.VoidGravityRoots: return voidRootsAbilitySprite;
                default: return voidBarrageAbilitySprite;
            }
        }

        private BossAbilityInfo BossAbilityForState(Enemy boss)
        {
            if (boss == null || boss.ActiveAbility == null) return null;
            foreach (var info in BossAbilityCatalog.For(boss.Definition ?? BossAssetRegistry.Get(boss.BossType)))
                if (info.Asset == boss.ActiveAbility || info.Asset == boss.ActiveAbility.SecondaryAbility) return info;
            return null;
        }

        private float BossAbilityCooldownRemaining(Enemy boss, BossAbilityInfo info, out float maxCooldown)
        {
            var ability = info != null ? info.Asset : null;
            maxCooldown = ability != null ? Mathf.Max(.1f, ability.Shot != null ? ability.Shot.FireInterval : ability.Cooldown > 0 ? ability.Cooldown : ability.Duration) : 1;
            if (boss == null || ability == null) return 0;
            if (boss.ActiveAbility == ability || boss.ActiveAbility?.SecondaryAbility == ability)
                return Mathf.Clamp(ability.Shot != null ? boss.FireTimer : boss.BossStateTimer, 0, maxCooldown);
            return boss.AbilityReadyAt.TryGetValue(ability, out var ready) ? Mathf.Max(0, ready - boss.BossAge) : 0;
        }

        private void DrawCooldownClockHand(Rect iconRect, float remaining, float maxCooldown, Color accent)
        {
            if (remaining <= 0f || maxCooldown <= .001f) return;
            var normalized = Mathf.Clamp01(remaining / maxCooldown);
            var center = iconRect.center;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(normalized * 360f, center);
            PixelUi.DrawPanel(new Rect(center.x - 1.5f, iconRect.y + iconRect.height * .17f,
                3f, iconRect.height * .34f), new Color(accent.r, accent.g, accent.b, .92f), Color.clear, 0f);
            GUI.matrix = matrix;
        }

        private void DrawBossAbilityCooldownHud(Enemy boss, float left, float top, float width, float height, int smallPixel)
        {
            if (boss == null || boss.Kind != EnemyKind.Boss) return;
            var abilities = BossAbilityCatalog.For(boss.Definition ?? BossAssetRegistry.Get(boss.BossType));
            if (abilities == null || abilities.Length == 0) return;

            // A narrow vertical rail leaves the playfield clear and reads like a
            // familiar MOBA spell strip: icon, dark cooldown mask, clock hand, timer.
            var rail = new Rect(left + width * .858f, top + height * .205f, width * .132f, height * .245f);
            PixelUi.DrawPanel(rail, new Color(.006f, .014f, .04f, .66f), new Color(.34f, .48f, .68f, .45f), 2f);
            var cardGap = rail.height * .045f;
            var cardHeight = (rail.height - cardGap * (abilities.Length + 1)) / abilities.Length;
            for (var i = 0; i < abilities.Length; i++)
            {
                var info = abilities[i];
                var card = new Rect(rail.x + rail.width * .08f, rail.y + cardGap + i * (cardHeight + cardGap),
                    rail.width * .84f, cardHeight);
                var remaining = BossAbilityCooldownRemaining(boss, info, out var maxCooldown);
                var ready = remaining <= .001f;
                var accent = info.Accent;
                PixelUi.DrawPanel(card, ready
                    ? new Color(accent.r * .08f, accent.g * .08f, accent.b * .08f, .86f)
                    : new Color(.025f, .035f, .065f, .94f),
                    new Color(accent.r, accent.g, accent.b, ready ? .72f : .34f), 2f);

                var iconRect = new Rect(card.x + card.width * .06f, card.y + card.height * .12f,
                    card.height * .76f, card.height * .76f);
                var icon = BossAbilitySprite(info);
                if (icon != null && icon.texture != null)
                {
                    GUI.color = ready ? Color.white : new Color(.37f, .42f, .50f, .72f);
                    GUI.DrawTexture(iconRect, icon.texture, ScaleMode.ScaleToFit, true);
                    GUI.color = Color.white;
                }
                if (!ready)
                {
                    PixelUi.DrawPanel(iconRect, new Color(.005f, .01f, .025f, .56f), Color.clear, 0f);
                    DrawCooldownClockHand(iconRect, remaining, maxCooldown, accent);
                }
                var timerRect = new Rect(iconRect.xMax + card.width * .04f, card.y + card.height * .08f,
                    card.xMax - iconRect.xMax - card.width * .06f, card.height * .84f);
                PixelUi.DrawText(timerRect, ready ? "ГОТ" : remaining.ToString("0.0"), Mathf.Max(3, smallPixel - 1),
                    ready ? new Color(.76f, 1f, .86f) : new Color(.70f, .76f, .86f), TextAnchor.MiddleCenter);
            }
        }

        private void DrawBossAbilityGuide(Enemy boss, float left, float top, float width, float height, int smallPixel, Color pale)
        {
            if (boss == null || boss.Kind != EnemyKind.Boss) return;
            var abilities = BossAbilityCatalog.For(boss.Definition ?? BossAssetRegistry.Get(boss.BossType));
            if (abilities == null || abilities.Length == 0) return;
            var panel = new Rect(left + width * .055f, top + height * .705f, width * .89f, height * .115f);
            PixelUi.DrawPanel(panel, new Color(.008f, .018f, .055f, .84f), new Color(.38f, .52f, .75f, .58f), 2f);
            var cardGap = panel.width * .018f;
            var cardWidth = (panel.width - cardGap * (abilities.Length + 1)) / abilities.Length;
            for (var i = 0; i < abilities.Length; i++)
            {
                var info = abilities[i];
                var card = new Rect(panel.x + cardGap + i * (cardWidth + cardGap), panel.y + panel.height * .12f,
                    cardWidth, panel.height * .76f);
                PixelUi.DrawPanel(card, new Color(info.Accent.r * .12f, info.Accent.g * .12f, info.Accent.b * .12f, .78f),
                    new Color(info.Accent.r, info.Accent.g, info.Accent.b, .65f), 2f);
                var iconRect = new Rect(card.x + card.width * .04f, card.y + card.height * .14f, card.height * .68f, card.height * .68f);
                var icon = BossAbilitySprite(info);
                if (icon != null && icon.texture != null) GUI.DrawTexture(iconRect, icon.texture, ScaleMode.ScaleToFit, true);
                var textRect = new Rect(iconRect.xMax + card.width * .035f, card.y + card.height * .08f,
                    card.xMax - iconRect.xMax - card.width * .06f, card.height * .84f);
                // Timing carries both cooldown and duration/telegraph data, so
                // a player can learn the counterplay without opening a wiki.
                PixelUi.DrawText(textRect, info.Name + "\n" + info.Timing, Mathf.Max(3, smallPixel - 1), pale, TextAnchor.MiddleLeft);
            }
            PixelUi.DrawText(new Rect(panel.x + 8f, panel.y - panel.height * .32f, panel.width - 16f, panel.height * .3f),
                "СПЕЛЫ БОССА // СМОТРИ ТЕЛЕГРАФ И УХОДИ С ЛИНИИ", smallPixel, new Color(.62f, .76f, .92f), TextAnchor.MiddleCenter);
            var focus = BossAbilityForState(boss);
            if (focus != null)
                PixelUi.DrawText(new Rect(panel.x + 10f, panel.yMax + height * .004f, panel.width - 20f, height * .027f),
                    focus.ShortDescription, Mathf.Max(3, smallPixel - 1), focus.Accent, TextAnchor.MiddleCenter);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (DrawDepthSpacePanel()) return;
            if (expeditionModeChoice != null && expeditionModeChoice.IsOpen) return;
            if (LivingMapVisible || LivingRewardChoice) return;
            var originalGuiMatrix = GUI.matrix;
            try
            {
            if (bossMirrorActive) GUI.matrix = BossMirrorGuiMatrix();
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

            // The Canvas overlay is the only pause surface. Do not leave legacy IMGUI controls
            // active behind it, otherwise a tap on "ВЫЙТИ" could also hit gameplay UI.
            if (paused && !abilitySandbox.IsOpen && (coopPlaying || defensePlaying || playing)) return;

            if (coopPlaying)
            {
                DrawCoopHud(left, top, width, height, pixel, smallPixel, pale, panel, cyan, violet);
                return;
            }

            if (defensePlaying)
            {
                DrawDefenseHud(left, top, width, height, pixel, smallPixel, pale, panel, cyan, violet);
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

                if (DrawPixelButton(new Rect(controlsRect.x + controlsRect.width * .10f, controlsRect.y + controlsRect.height * .405f,
                        controlsRect.width * .80f, controlsRect.height * .09f), "СОЛО // КЛАССИКА\nКРУГ · ВОЛНЫ · БОСС", smallPixel,
                        new Color(.20f, .045f, .36f, .98f), violet, Color.white))
                    StartGame();
                if (DrawPixelButton(new Rect(controlsRect.x + controlsRect.width * .10f, controlsRect.y + controlsRect.height * .51f,
                        controlsRect.width * .80f, controlsRect.height * .09f), "СОЛО // ЭКСПЕДИЦИЯ\nКОМНАТЫ · МАГАЗИН · БОСС", smallPixel,
                        new Color(.035f, .16f, .20f, .98f), new Color(.28f, 1f, .72f), Color.white))
                    BeginSoloExpedition();
                if (DrawPixelButton(new Rect(controlsRect.x + controlsRect.width * .10f, controlsRect.y + controlsRect.height * .615f,
                        controlsRect.width * .80f, controlsRect.height * .09f), "ЗАЩИТА ФЛАГМАНА\nВРАГИ ЛЕТЯТ К КОРАБЛЮ", smallPixel,
                        new Color(.14f, .06f, .10f, .98f), new Color(1f, .42f, .55f), Color.white))
                    BeginDefenseMode();
                if (DrawPixelButton(new Rect(controlsRect.x + controlsRect.width * .10f, controlsRect.y + controlsRect.height * .72f,
                        controlsRect.width * .80f, controlsRect.height * .07f), "ПЕСОЧНИЦА СПОСОБНОСТЕЙ\nМАНЕКЕН · СПЕЛЛЫ · ВИЗУАЛИЗАЦИЯ", smallPixel,
                        new Color(.08f, .11f, .28f, .98f), new Color(.92f, .58f, 1f), Color.white))
                    OpenAbilitySandbox();
                if (DrawPixelButton(new Rect(controlsRect.x + controlsRect.width * .16f, controlsRect.y + controlsRect.height * .805f,
                        controlsRect.width * .68f, controlsRect.height * .060f), "КООП // 2 ИГРОКА\nОБЩИЙ КОРПУС · СЦЕПКА", smallPixel,
                        new Color(.06f, .11f, .27f, .98f), cyan, Color.white))
                {
                    showCoop = true;
                    BeginUiFade();
                }
                if (DrawPixelButton(new Rect(controlsRect.x + controlsRect.width * .22f, controlsRect.y + controlsRect.height * .88f,
                        controlsRect.width * .56f, controlsRect.height * .052f), "НАСТРОЙКИ", smallPixel, panel, violet, pale))
                {
                    nicknameError = string.Empty;
                    showSettings = true;
                    BeginUiFade();
                }
                PixelUi.DrawText(new Rect(controlsRect.x + 14f, controlsRect.y + controlsRect.height * .945f, controlsRect.width - 28f, controlsRect.height * .035f), "ПОЗЫВНОЙ И ЗВУК — В НАСТРОЙКАХ", smallPixel, new Color(.55f, .72f, .9f));

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
                var activeBoss = ActiveBoss();
                if (!abilitySandbox.VfxEditorOpen)
                {
                    // The ability sandbox is its own work surface. Keep the combat
                    // score/phase and HP/core HUD out of it so the authoring rail
                    // remains readable and does not inherit unrelated run state.
                    if (!abilitySandbox.IsOpen)
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
                        PixelUi.DrawText(new Rect(stateRect.x + 12f, stateRect.y + stateRect.height * .78f, stateRect.width * .82f, stateRect.height * .18f), "ЩИТ  " + starShields + "/" + MaxStarShields, smallPixel, new Color(.68f, .92f, 1f), TextAnchor.MiddleLeft);
                    }

                    if (activeBoss != null && !abilitySandbox.IsOpen)
                    {
                        // Ниже баннера перехода, чтобы имя босса и его HP не накладывались.
                        var bossRect = new Rect(left + width * .18f, top + height * .29f, width * .64f, height * .055f);
                        var healthSegments = Mathf.CeilToInt(Mathf.Clamp01(activeBoss.Health / activeBoss.MaxHealth) * 16f);
                        var bossLabel = abilitySandbox.IsOpen ? "ФЕНИКС-МАНЕКЕН // БЕЗ АТАК" : "СТРАЖ УРАНА";
                        PixelUi.DrawText(new Rect(bossRect.x, bossRect.y - bossRect.height * .42f, bossRect.width, bossRect.height * .42f), bossLabel, smallPixel, new Color(.92f, .54f, 1f), TextAnchor.MiddleCenter);
                        PixelUi.DrawSegmentBar(bossRect, healthSegments, 16, new Color(.82f, .2f, 1f), new Color(.12f, .035f, .18f, .95f), new Color(.92f, .54f, 1f));
                    }

                    if (splitShot || tripleShotTimer > 0f)
                    {
                        var shotTimer = Mathf.Max(splitShot ? splitShotTimer : 0f, tripleShotTimer);
                        PixelUi.DrawText(new Rect(left, top + height * .14f, width, height * .04f), "TRIPLE SHOT  " + shotTimer.ToString("0.0"), smallPixel, new Color(1f, .86f, .3f));
                    }
                    if (coreActive) PixelUi.DrawText(new Rect(left, top + height * .185f, width, height * .04f), "ЭНЕРГО ЯДРО НА ОРБИТЕ", smallPixel, new Color(1f, .86f, .3f));
                    if (phaseUpgradeBannerTimer > 0f)
                    {
                        var alpha = Mathf.Clamp01(phaseUpgradeBannerTimer / .45f);
                        var banner = new Rect(left + width * .08f, top + height * .17f, width * .84f, height * .10f);
                        PixelUi.DrawText(banner, phaseUpgradeLabel, pixel, new Color(1f, 1f, 1f, alpha));
                    }
                }
                if (!abilitySandbox.IsOpen) DrawBossAbilityCooldownHud(activeBoss, left, top, width, height, smallPixel);
                if (!abilitySandbox.VfxEditorOpen) DrawCombatMoment(left, top, width, height, smallPixel);
                if (!abilitySandbox.IsOpen || (!abilitySandbox.LoadoutOpen && !abilitySandbox.VfxEditorOpen))
                {
                    var hudTop = abilitySandbox.IsOpen ? top - height * .12f : top;
                    DrawOrbitalAbilityHud(left, 1, width, height, smallPixel, pale);
                }
                if (abilitySandbox.IsOpen)
                    abilitySandbox.Draw(left, top, width, height, pixel, smallPixel, pale, cyan);
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
            finally
            {
                GUI.matrix = originalGuiMatrix;
            }
        }
    }
}
