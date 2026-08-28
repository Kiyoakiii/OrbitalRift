using System.Collections.Generic;
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
        private ObjectPool<Enemy> enemyPool;
        private ObjectPool<Projectile> projectilePool;
        private ObjectPool<StarParticle> starPool;
        private ObjectPool<DamageShard> damageShardPool;
        private Camera gameCamera;
        private Transform arena, player, core, splitPickup, menuEmblem, warpBadge;
        private Sprite whiteSprite, circleSprite, shipSprite, projectileSprite, bonusSprite, orangeEnemySprite, pinkCanEnemySprite, menuEmblemSprite, warpBadgeSprite;
        private AudioSource musicSource, effectsSource;
        private AudioClip enemyHitSound, enemyDeathSound, playerDamageSound;
        private float playerAngle = -Mathf.PI * .5f, targetAngle, fireTimer, spawnTimer, starTimer, invincible, coreAngle;
        private int score, bestScore, shields = 3, phase = 1, cores, spawnsLeft;
        private bool playing, showMenu = true, showResults, autoFire = true, coreActive, splitShot, paused;
        private string playerNickname;
        private string nicknameError;
        private IReadOnlyList<LeaderboardEntry> leaderboardEntries;
        private FirebaseScoreService firebaseScores;
        private float splitShotTimer, warpTimer, splitLifetime;
        private Vector2 splitVelocity;
        private float touchHintTimer;
        private float phaseUpgradeBannerTimer;
        private string phaseUpgradeLabel;
        private float screenShakeTimer, screenShakeStrength;
        private int controlFingerId = -1;
        private int framedScreenWidth = -1, framedScreenHeight = -1;

        [Header("Editor preview / visual tuning")]
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Color distantStarColor = new Color(.55f, .66f, 1f, .5f);
        [SerializeField] private Color shipTint = Color.white;

        // Скорость движения по единственной орбите при удержании сенсорной зоны.
        private const float TouchOrbitSpeed = 3.4f;

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
            showResults = false;
            paused = false;
            coreActive = false;
            splitShot = false;
            phaseUpgradeBannerTimer = 0f;
            screenShakeTimer = 0f;
            bestScore = PlayerPrefs.GetInt("orbital_rift_best", 0);
            playerNickname = PlayerPrefs.GetString("orbital_rift_nickname", string.Empty);
            firebaseScores = GetComponent<FirebaseScoreService>();
            if (firebaseScores != null)
            {
                firebaseScores.PersonalBestLoaded += ApplyCloudBestScore;
                firebaseScores.LeaderboardLoaded += ApplyLeaderboard;
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
            menuEmblemSprite = LoadResourceSprite("menu_emblem", 1024f);
            warpBadgeSprite = LoadResourceSprite("warp_badge", 1024f);
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
            if (!paused) { UpdateStars(dt); UpdateDamageShards(dt); UpdateScreenShake(dt); }
            UpdatePresentation();
            if (!playing || paused) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { paused = true; return; }
            UpdateInput(dt);
            UpdatePlayer(dt);
            UpdateSpawning(dt);
            UpdateEnemies(dt);
            UpdateProjectiles(dt);
            UpdateCore(dt);
            if (Input.GetKeyDown(KeyCode.Space)) autoFire = !autoFire;
        }

        private void LateUpdate()
        {
            if (gameCamera == null) return;
            gameCamera.clearFlags = CameraClearFlags.Color;
            gameCamera.backgroundColor = backgroundColor;
        }

        private void OnApplicationFocus(bool focus) { if (!focus && playing) paused = true; }

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
            if (!force && framedScreenWidth == Screen.width && framedScreenHeight == Screen.height) return;
            framedScreenWidth = Screen.width;
            framedScreenHeight = Screen.height;
            var aspect = Mathf.Max(.01f, Screen.width / (float)Mathf.Max(1, Screen.height));
            var halfOrbitWithMargin = OrbitSettings.Radius + .55f;
            // На узком портретном экране размер берётся по ширине; на ПК сохраняется обычный масштаб.
            gameCamera.orthographicSize = Mathf.Max(5.1f, halfOrbitWithMargin / aspect);
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
            gameCamera = FindObjectOfType<Camera>();
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
            musicSource.volume = .42f;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.name = "Effects source";
            effectsSource.volume = .7f;
            effectsSource.playOnAwake = false;
            effectsSource.spatialBlend = 0f;
            enemyHitSound = SoundEffects.CreateEnemyHit();
            enemyDeathSound = SoundEffects.CreateEnemyDeath();
            playerDamageSound = SoundEffects.CreatePlayerDamage();
        }

        private SpriteRenderer MakeSprite(string name, Transform parent, Color color, Vector3 scale, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent); go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = whiteSprite; sr.color = color; sr.sortingOrder = order; return sr;
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
                return;
            }

            nicknameError = string.Empty;
            PlayerPrefs.SetString("orbital_rift_nickname", playerNickname);
            PlayerPrefs.Save();
            Cleanup(); score = 0; shields = 3; phase = 1; cores = 0; playing = true; showMenu = false; showResults = false; paused = false; coreActive = false;
            playerAngle = -Mathf.PI * .5f;
            targetAngle = playerAngle;
            controlFingerId = -1;
            touchHintTimer = 5f;
            phaseUpgradeBannerTimer = 2.5f;
            phaseUpgradeLabel = "СИСТЕМА В СЕТИ\nПЕРВАЯ ФАЗА";
            screenShakeTimer = 0f;
            if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
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
            for (var i=damageShards.Count-1;i>=0;i--) damageShardPool.Release(damageShards[i]); damageShards.Clear();
            splitPickup.gameObject.SetActive(false);
        }

        private void UpdateInput(float dt)
        {
            var direction = 0f;

            // Левая половина: по часовой стрелке. Правая половина: против часовой.
            // Направление действует, пока палец удерживается на экране.
            if (Input.touchCount > 0)
            {
                Touch touch = default(Touch);
                var foundTouch = false;
                if (controlFingerId >= 0)
                {
                    for (var i = 0; i < Input.touchCount; i++)
                    {
                        if (Input.GetTouch(i).fingerId != controlFingerId) continue;
                        touch = Input.GetTouch(i);
                        foundTouch = true;
                        break;
                    }
                }
                if (!foundTouch)
                {
                    for (var i = 0; i < Input.touchCount; i++)
                    {
                        var candidate = Input.GetTouch(i);
                        if (candidate.phase == TouchPhase.Ended || candidate.phase == TouchPhase.Canceled) continue;
                        touch = candidate;
                        controlFingerId = candidate.fingerId;
                        foundTouch = true;
                        break;
                    }
                }
                if (foundTouch)
                {
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        controlFingerId = -1;
                    }
                    else
                    {
                        direction = touch.position.x < Screen.width * .5f ? -1f : 1f;
                    }
                }
            }
            else
            {
                controlFingerId = -1;
            }

            // Мышь повторяет сенсорные зоны, чтобы механику было удобно проверять на ПК.
            if (Mathf.Abs(direction) < .01f && Input.GetMouseButton(0))
                direction = Input.mousePosition.x < Screen.width * .5f ? -1f : 1f;

            var keys = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(keys) > .01f) direction = Mathf.Sign(keys);

            if (Mathf.Abs(direction) > .01f)
            {
                targetAngle += direction * dt * TouchOrbitSpeed;
                touchHintTimer = Mathf.Max(0f, touchHintTimer - dt);
            }
            else
            {
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
            if (autoFire && fireTimer <= 0) { fireTimer = BalanceSettings.PlayerFireInterval(phase, splitShot); FirePlayer(); }
            if (splitShot) { splitShotTimer -= dt; if (splitShotTimer <= 0) splitShot=false; }
            UpdateSplitPickup(dt);
        }

        private void PositionOnOrbit()
        {
            var pos = new Vector2(Mathf.Cos(playerAngle), Mathf.Sin(playerAngle)) * OrbitSettings.Radius;
            player.position = pos;
            player.up = -pos.normalized;
        }

        private void FirePlayer()
        {
            var projectileSpeed = BalanceSettings.PlayerProjectileSpeed(phase);
            Shoot((Vector2)player.position, -((Vector2)player.position).normalized * projectileSpeed, true, new Color(.65f,1f,1f));
            if (!splitShot) return;
            var inward = -((Vector2)player.position).normalized;
            Shoot(player.position, Rotate(inward, 12f)*projectileSpeed, true, new Color(.65f,1f,1f)); Shoot(player.position, Rotate(inward,-12f)*projectileSpeed, true, new Color(.65f,1f,1f));
        }

        private void Shoot(Vector2 position, Vector2 velocity, bool friendly, Color color)
        {
            if (!friendly && projectiles.Count >= 80) return;
            var p = projectilePool.Get();
            // Вражеские выстрелы — простые тёмно-зелёные квадраты; PNG игрока сохраняется без тонировки.
            p.SetVisual(friendly && projectileSprite != null ? projectileSprite : whiteSprite, friendly && projectileSprite != null, !friendly);
            p.ResetProjectile(position, velocity, friendly, friendly ? color : new Color(.05f, .30f, .13f));
            projectiles.Add(p);
        }

        private void UpdateSpawning(float dt)
        {
            if (coreActive || warpTimer > 0) return;
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
            return null;
        }

        private void UpdateEnemies(float dt)
        {
            for (var i=enemies.Count-1;i>=0;i--)
            {
                var e = enemies[i]; e.Life -= dt; e.FireTimer -= dt;
                var speed = BalanceSettings.EnemyMovementMultiplier(phase);
                if (e.Kind == EnemyKind.Scout) { e.Radius = Mathf.Min(3.1f, e.Radius + dt*speed); e.Angle += dt*.7f; }
                else if (e.Kind == EnemyKind.Spiral) { e.Radius = 2.35f + Mathf.Sin(Time.time*2.2f+i)*.75f; e.Angle += dt*1.4f; }
                else if (e.Kind == EnemyKind.Diver) { e.Radius = 2.4f + Mathf.PingPong(Time.time*2.6f+i, 1.5f); e.Angle = Mathf.LerpAngle(e.Angle*Mathf.Rad2Deg, playerAngle*Mathf.Rad2Deg, dt*.8f)*Mathf.Deg2Rad; }
                else { e.Radius = 2.7f; e.Angle += dt*.55f; }
                e.transform.position = new Vector2(Mathf.Cos(e.Angle), Mathf.Sin(e.Angle))*e.Radius;
                if (e.FireTimer <= 0) { FireEnemy(e); e.FireTimer = BalanceSettings.EnemyFireInterval(phase); }
                if (e.Life <= 0) RemoveEnemy(i);
            }
            if (!coreActive && spawnsLeft == 0 && enemies.Count == 0) ActivateCore();
        }

        private void FireEnemy(Enemy e)
        {
            var direction = ((Vector2)player.position-(Vector2)e.transform.position).normalized;
            var projectileSpeed = BalanceSettings.EnemyProjectileSpeed(phase);
            Shoot(e.transform.position, direction * projectileSpeed, false, new Color(1f,.18f,.42f));
            if (e.Kind == EnemyKind.Turret) { Shoot(e.transform.position, Rotate(direction,18) * projectileSpeed * 1.04f,false,new Color(1f,.18f,.42f)); Shoot(e.transform.position,Rotate(direction,-18) * projectileSpeed * 1.04f,false,new Color(1f,.18f,.42f)); }
        }

        private void UpdateProjectiles(float dt)
        {
            for (var i=projectiles.Count-1;i>=0;i--)
            {
                var p=projectiles[i]; p.Life-=dt; p.transform.position += (Vector3)(p.Velocity*dt);
                var released = p.FromPlayer ? HitEnemies(i,p) : HitPlayer(i,p);
                if (!released && p.Life <= 0) RemoveProjectile(i);
            }
        }

        private bool HitEnemies(int projectileIndex, Projectile p)
        {
            for (var j = enemies.Count - 1; j >= 0; j--)
            {
                if (Vector2.Distance(p.transform.position, enemies[j].transform.position) >= .28f) continue;
                var enemy = enemies[j];
                enemy.Health--;
                RemoveProjectile(projectileIndex);
                var impactColor = EnemyEffectColor(enemy.Kind);
                if (enemy.Health <= 0)
                {
                    score += enemy.Points;
                    PlayEffect(enemyDeathSound, .82f);
                    SpawnImpactBurst(enemy.transform.position, impactColor, 13, 2.9f, .48f);
                    AddScreenShake(.09f, .065f);
                    if (!splitPickup.gameObject.activeSelf && Random.value < .10f) ActivateSplitPickup(enemy.transform.position);
                    RemoveEnemy(j);
                }
                else
                {
                    PlayEffect(enemyHitSound, .48f);
                    SpawnImpactBurst(enemy.transform.position, impactColor, 4, 1.35f, .18f);
                }
                return true;
            }
            return false;
        }

        private bool HitPlayer(int projectileIndex, Projectile p)
        {
            if (invincible<=0 && Vector2.Distance(p.transform.position,player.position)<.3f) { RemoveProjectile(projectileIndex); DamagePlayer(); return true; }
            return false;
        }

        private void DamagePlayer()
        {
            shields--; invincible=1f; SpawnPlayerDamageBurst(); PlayEffect(playerDamageSound, .8f); AddScreenShake(.22f, .14f); if (shields <= 0) EndGame();
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
            if (Vector2.Distance(corePosition, player.position)<.42f) { coreActive=false; cores++; SpawnWarpBurst(24,1f); if(cores>=3) BeginWarp(); else StartWave(); }
        }

        private void BeginWarp()
        {
            cores = 0;
            phase++;
            warpTimer = 1.5f;
            phaseUpgradeBannerTimer = 2.35f;
            phaseUpgradeLabel = phase % 2 == 0
                ? "УЛУЧШЕНИЕ: ТЕМП ОГНЯ\n+18% К СКОРОСТРЕЛЬНОСТИ"
                : "УЛУЧШЕНИЕ: ПЛАЗМА-ДРАЙВ\n+16% К СКОРОСТИ ЗАРЯДА";
            StartWave();
            SpawnWarpBurst(80, 2.4f);
            AddScreenShake(.16f, .11f);
        }

        private void StartWave() { spawnsLeft = 5 + phase * 2 + cores * 2; spawnTimer = .72f; }

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
                s.Life-=dt;
                var viewport = gameCamera.WorldToViewportPoint(s.transform.position);
                var edgeDistance = Mathf.Max(Mathf.Abs(viewport.x - .5f) * 2f, Mathf.Abs(viewport.y - .5f) * 2f);
                var slowdown = Mathf.Lerp(1f, StarStreamSettings.ScreenEdgeSpeedMultiplier, Mathf.InverseLerp(StarStreamSettings.ScreenEdgeSlowStart, 1.15f, edgeDistance));
                s.transform.position+=(Vector3)(s.Velocity * (dt * slowdown));
                var alpha = Mathf.Clamp01(s.Life) * s.Brightness;
                s.Renderer.color = new Color(1f, 1f, 1f, alpha);
                s.Trail.startColor = new Color(1f, 1f, 1f, alpha * StarStreamSettings.TrailFade);
                if(s.Life<=0)RemoveStar(i);
            }
            if(warpTimer>0) warpTimer-=dt;
            if (phaseUpgradeBannerTimer > 0f) phaseUpgradeBannerTimer -= dt;
        }

        private void SpawnWarpBurst(int amount,float speed)
        { if (starPool == null) return; for(var i=0;i<amount;i++){var angle=Random.Range(0,Mathf.PI*2);var s=starPool.Get();if (s == null) continue;s.ResetStar(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),speed*Random.Range(.7f,1.3f),Random.Range(1.6f,3f));stars.Add(s);} }

        private void SpawnPlayerDamageBurst()
        {
            for (var i = 0; i < Random.Range(5, 7); i++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var shard = damageShardPool.Get();
                shard.ResetShard(player.position, direction * Random.Range(1.3f, 2.5f), Random.Range(.035f, .075f), new Color(1f, .08f, .12f, 1f));
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
            return new Color(1f, .82f, .15f, 1f);
        }

        private void PlayEffect(AudioClip clip, float volume)
        {
            // PlayOneShot смешивает SFX поверх отдельного AudioSource музыки и не прерывает трек.
            if (effectsSource != null && clip != null) effectsSource.PlayOneShot(clip, volume);
        }

        private void AddScreenShake(float duration, float strength)
        {
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

        private void EndGame()
        {
            playing = false;
            showResults = true;
            if (musicSource != null) musicSource.Stop();
            bestScore = Mathf.Max(bestScore, score);
            PlayerPrefs.SetInt("orbital_rift_best", bestScore);
            PlayerPrefs.Save();
            if (firebaseScores != null) firebaseScores.SubmitBestScore(bestScore, playerNickname);
        }

        private void ApplyCloudBestScore(int cloudScore)
        {
            if (cloudScore <= bestScore) return;
            bestScore = cloudScore;
            PlayerPrefs.SetInt("orbital_rift_best", bestScore);
            PlayerPrefs.Save();
        }

        private void ApplyLeaderboard(IReadOnlyList<LeaderboardEntry> entries)
        {
            leaderboardEntries = entries;
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

        private string LeaderboardText()
        {
            if (leaderboardEntries == null) return "ОБЩИЙ РЕЙТИНГ\nСИНХРОНИЗАЦИЯ";
            if (leaderboardEntries.Count == 0) return "ОБЩИЙ РЕЙТИНГ\nПОКА ПУСТО";
            var text = "ОБЩИЙ РЕЙТИНГ\n";
            for (var i = 0; i < leaderboardEntries.Count; i++)
                text += (i + 1) + ". " + leaderboardEntries[i].Nickname + "  " + leaderboardEntries[i].Score + (i + 1 < leaderboardEntries.Count ? "\n" : string.Empty);
            return text;
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

                PixelUi.DrawText(new Rect(controlsRect.x + 16f, controlsRect.y + controlsRect.height * .08f, controlsRect.width - 32f, controlsRect.height * .10f), "ПИЛОТ // ПОЗЫВНОЙ", smallPixel, pale, TextAnchor.UpperCenter);
                var nicknameRect = new Rect(controlsRect.x + controlsRect.width * .10f, controlsRect.y + controlsRect.height * .21f, controlsRect.width * .80f, controlsRect.height * .16f);
                PixelUi.DrawPanel(nicknameRect, panel, cyan, 3f);
                playerNickname = GUI.TextField(nicknameRect, playerNickname ?? string.Empty, 16, MakeCallsignInputStyle());
                PixelUi.DrawText(new Rect(nicknameRect.x + 10f, nicknameRect.y + 6f, nicknameRect.width - 20f, nicknameRect.height - 12f),
                    string.IsNullOrEmpty(playerNickname) ? "ВВЕДИ ПОЗЫВНОЙ" : playerNickname, pixel, string.IsNullOrEmpty(playerNickname) ? new Color(.36f, .58f, .72f, .82f) : Color.white);

                var startRect = new Rect(controlsRect.x + controlsRect.width * .10f, controlsRect.y + controlsRect.height * .43f, controlsRect.width * .80f, controlsRect.height * .20f);
                if (DrawPixelButton(startRect, "НАЧАТЬ ПОЛЕТ", pixel, new Color(.20f, .045f, .36f, .98f), violet, Color.white)) StartGame();
                if (!string.IsNullOrEmpty(nicknameError)) PixelUi.DrawText(new Rect(controlsRect.x + 12f, controlsRect.y + controlsRect.height * .65f, controlsRect.width - 24f, controlsRect.height * .12f), nicknameError, smallPixel, new Color(1f, .38f, .48f));
                PixelUi.DrawText(new Rect(controlsRect.x + 18f, controlsRect.y + controlsRect.height * .72f, controlsRect.width - 36f, controlsRect.height * .18f), "УДЕРЖИВАЙ\nЛЕВУЮ ИЛИ ПРАВУЮ ПОЛОВИНУ", smallPixel, new Color(.58f, .75f, 1f, .9f));

                var bestRect = new Rect(statsRect.x + statsRect.width * .10f, statsRect.y + statsRect.height * .10f, statsRect.width * .80f, statsRect.height * .15f);
                PixelUi.DrawPanel(bestRect, new Color(.02f, .11f, .16f, .96f), new Color(.2f, .8f, 1f, .72f), 3f);
                PixelUi.DrawText(bestRect, "ЛУЧШИЙ СИГНАЛ\n" + bestScore, pixel, cyan);
                var leaderboardRect = new Rect(statsRect.x + statsRect.width * .10f, statsRect.y + statsRect.height * .31f, statsRect.width * .80f, statsRect.height * .56f);
                PixelUi.DrawPanel(leaderboardRect, panel, new Color(.45f, .35f, 1f, .72f), 3f);
                PixelUi.DrawText(new Rect(leaderboardRect.x + 10f, leaderboardRect.y + 10f, leaderboardRect.width - 20f, leaderboardRect.height - 20f), LeaderboardText(), smallPixel, pale);
                PixelUi.DrawText(new Rect(statsRect.x + 14f, statsRect.y + statsRect.height * .90f, statsRect.width - 28f, statsRect.height * .07f), "FIREBASE // ONLINE", smallPixel, new Color(.35f, 1f, .68f, .9f));
                PixelUi.DrawText(new Rect(left + width * .05f, top + height * .90f, width * .90f, height * .035f), "ЛЕВО // ПО ЧАСОВОЙ        ПРАВО // ПРОТИВ", smallPixel, new Color(.58f, .75f, 1f, .9f));
                return;
            }

            if (playing)
            {
                var scoreRect = new Rect(left + width * .04f, top + height * .025f, width * .44f, height * .102f);
                var stateRect = new Rect(left + width * .52f, top + height * .025f, width * .44f, height * .102f);
                PixelUi.DrawPanel(scoreRect, panel, new Color(.15f, .72f, 1f, .75f), 3f);
                PixelUi.DrawPanel(stateRect, panel, new Color(.65f, .35f, 1f, .75f), 3f);
                PixelUi.DrawText(scoreRect, "СЧЕТ " + score + "\nФАЗА " + phase, pixel, cyan);
                PixelUi.DrawText(stateRect, "ЩИТЫ " + shields + " / 3\nЯДРА " + cores + " / 3", pixel, pale);

                if (splitShot) PixelUi.DrawText(new Rect(left, top + height * .14f, width, height * .04f), "SPLIT SHOT  " + splitShotTimer.ToString("0.0"), smallPixel, new Color(1f, .86f, .3f));
                if (coreActive) PixelUi.DrawText(new Rect(left, top + height * .185f, width, height * .04f), "ЭНЕРГО ЯДРО НА ОРБИТЕ", smallPixel, new Color(1f, .86f, .3f));
                if (phaseUpgradeBannerTimer > 0f)
                {
                    var alpha = Mathf.Clamp01(phaseUpgradeBannerTimer / .45f);
                    var banner = new Rect(left + width * .10f, top + height * .38f, width * .80f, height * .11f);
                    PixelUi.DrawPanel(banner, new Color(.12f, .02f, .26f, .88f * alpha), new Color(.82f, .3f, 1f, alpha), 4f);
                    PixelUi.DrawText(banner, phaseUpgradeLabel, pixel, new Color(.96f, .84f, 1f, alpha));
                }
                if (touchHintTimer > 0 && !paused)
                {
                    var leftHint = new Rect(left + width * .04f, top + height * .88f, width * .43f, height * .055f);
                    var rightHint = new Rect(left + width * .53f, top + height * .88f, width * .43f, height * .055f);
                    PixelUi.DrawPanel(leftHint, panel, new Color(.2f, .7f, 1f, .55f), 2f);
                    PixelUi.DrawPanel(rightHint, panel, new Color(.2f, .7f, 1f, .55f), 2f);
                    PixelUi.DrawText(leftHint, "ЛЕВО // ЧАС", smallPixel, pale);
                    PixelUi.DrawText(rightHint, "ПРАВО // ПРОТИВ", smallPixel, pale);
                }
                if (paused)
                {
                    var pauseRect = new Rect(left + width * .16f, top + height * .38f, width * .68f, height * .22f);
                    PixelUi.DrawPanel(pauseRect, new Color(.015f, .025f, .10f, .96f), violet, 4f);
                    PixelUi.DrawText(new Rect(pauseRect.x, pauseRect.y + pauseRect.height * .10f, pauseRect.width, pauseRect.height * .30f), "ПАУЗА", Mathf.RoundToInt(12f * scale), pale);
                    if (DrawPixelButton(new Rect(pauseRect.x + pauseRect.width * .12f, pauseRect.y + pauseRect.height * .57f, pauseRect.width * .76f, pauseRect.height * .25f), "ПРОДОЛЖИТЬ", smallPixel, new Color(.11f, .16f, .38f, .96f), cyan, Color.white)) paused = false;
                }
                return;
            }

            if (showResults)
            {
                var resultRect = new Rect(left + width * .12f, top + height * .22f, width * .76f, height * .48f);
                PixelUi.DrawPanel(resultRect, new Color(.08f, .015f, .16f, .94f), violet, 4f);
                PixelUi.DrawText(new Rect(resultRect.x, resultRect.y + resultRect.height * .09f, resultRect.width, resultRect.height * .16f), "СИГНАЛ ПОТЕРЯН", pixel, new Color(1f, .55f, .75f));
                PixelUi.DrawText(new Rect(resultRect.x, resultRect.y + resultRect.height * .30f, resultRect.width, resultRect.height * .27f), "СЧЕТ " + score + "\nРЕКОРД " + bestScore + "\nФАЗА " + phase, pixel, pale);
                if (DrawPixelButton(new Rect(resultRect.x + resultRect.width * .14f, resultRect.y + resultRect.height * .65f, resultRect.width * .72f, resultRect.height * .12f), "ЕЩЕ РАЗ", smallPixel, new Color(.15f, .06f, .34f, .96f), violet, Color.white)) StartGame();
                if (DrawPixelButton(new Rect(resultRect.x + resultRect.width * .14f, resultRect.y + resultRect.height * .81f, resultRect.width * .72f, resultRect.height * .12f), "МЕНЮ", smallPixel, new Color(.04f, .12f, .22f, .96f), cyan, Color.white)) { showResults = false; showMenu = true; }
            }
        }
    }
}
