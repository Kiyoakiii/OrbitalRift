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
        private Transform arena, player, core, splitPickup;
        private Sprite whiteSprite, circleSprite, shipSprite, projectileSprite, bonusSprite, orangeEnemySprite, pinkCanEnemySprite;
        private AudioSource musicSource, effectsSource;
        private AudioClip enemyHitSound, enemyDeathSound;
        private float playerAngle = -Mathf.PI * .5f, targetAngle, fireTimer, spawnTimer, starTimer, invincible, coreAngle;
        private int score, bestScore, shields = 3, phase = 1, cores, spawnsLeft;
        private bool playing, showMenu = true, showResults, autoFire = true, coreActive, splitShot, paused;
        private float splitShotTimer, warpTimer, splitLifetime;
        private Vector2 splitVelocity;
        private float touchHintTimer;
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
            bestScore = PlayerPrefs.GetInt("orbital_rift_best", 0);
            RemoveEditorPreviewObjects();
            CreateCamera();
            whiteSprite = CreateWhiteSprite();
            circleSprite = CreateCircleSprite();
            shipSprite = LoadResourceSprite("ship", 1024f);
            projectileSprite = LoadResourceSprite("projectile", 1024f);
            bonusSprite = LoadResourceSprite("bonus_pickup", 1024f);
            orangeEnemySprite = LoadResourceSprite("enemy_orange", 1024f);
            pinkCanEnemySprite = LoadResourceSprite("enemy_pink_can", 1024f);
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
            if (!paused) { UpdateStars(dt); UpdateDamageShards(dt); }
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
            musicSource.clip = Resources.Load<AudioClip>("deep_space_drift");
            musicSource.loop = true;
            musicSource.volume = .42f;
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.volume = .7f;
            enemyHitSound = SoundEffects.CreateEnemyHit();
            enemyDeathSound = SoundEffects.CreateEnemyDeath();
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
            Cleanup(); score = 0; shields = 3; phase = 1; cores = 0; playing = true; showMenu = false; showResults = false; paused = false; coreActive = false;
            playerAngle = -Mathf.PI * .5f;
            targetAngle = playerAngle;
            controlFingerId = -1;
            touchHintTimer = 5f;
            if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
            StartWave(); SpawnWarpBurst(36, 1.2f);
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
            if (autoFire && fireTimer <= 0) { fireTimer = splitShot ? .15f : .2f; FirePlayer(); }
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
            Shoot((Vector2)player.position, -((Vector2)player.position).normalized * 9f, true, new Color(.65f,1f,1f));
            if (!splitShot) return;
            var inward = -((Vector2)player.position).normalized;
            Shoot(player.position, Rotate(inward, 12f)*9f, true, new Color(.65f,1f,1f)); Shoot(player.position, Rotate(inward,-12f)*9f, true, new Color(.65f,1f,1f));
        }

        private void Shoot(Vector2 position, Vector2 velocity, bool friendly, Color color)
        {
            if (!friendly && projectiles.Count >= 80) return;
            var p = projectilePool.Get();
            // Вражеские выстрелы — простые фиолетовые квадраты; PNG игрока сохраняется без тонировки.
            p.SetVisual(friendly && projectileSprite != null ? projectileSprite : whiteSprite, friendly && projectileSprite != null, !friendly);
            p.ResetProjectile(position, velocity, friendly, friendly ? color : new Color(.72f, .18f, .95f));
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
            spawnTimer = Mathf.Max(.24f, .95f - phase*.045f);
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
                var speed = 1f + phase*.07f;
                if (e.Kind == EnemyKind.Scout) { e.Radius = Mathf.Min(3.1f, e.Radius + dt*speed); e.Angle += dt*.7f; }
                else if (e.Kind == EnemyKind.Spiral) { e.Radius = 2.35f + Mathf.Sin(Time.time*2.2f+i)*.75f; e.Angle += dt*1.4f; }
                else if (e.Kind == EnemyKind.Diver) { e.Radius = 2.4f + Mathf.PingPong(Time.time*2.6f+i, 1.5f); e.Angle = Mathf.LerpAngle(e.Angle*Mathf.Rad2Deg, playerAngle*Mathf.Rad2Deg, dt*.8f)*Mathf.Deg2Rad; }
                else { e.Radius = 2.7f; e.Angle += dt*.55f; }
                e.transform.position = new Vector2(Mathf.Cos(e.Angle), Mathf.Sin(e.Angle))*e.Radius;
                if (e.FireTimer <= 0) { FireEnemy(e); e.FireTimer = Mathf.Max(.38f, 1.35f-phase*.06f); }
                if (e.Life <= 0) RemoveEnemy(i);
            }
            if (!coreActive && spawnsLeft == 0 && enemies.Count == 0) ActivateCore();
        }

        private void FireEnemy(Enemy e)
        {
            var direction = ((Vector2)player.position-(Vector2)e.transform.position).normalized;
            Shoot(e.transform.position, direction*(3.3f+phase*.12f), false, new Color(1f,.18f,.42f));
            if (e.Kind == EnemyKind.Turret) { Shoot(e.transform.position, Rotate(direction,18)*3.4f,false,new Color(1f,.18f,.42f)); Shoot(e.transform.position,Rotate(direction,-18)*3.4f,false,new Color(1f,.18f,.42f)); }
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
            for (var j=enemies.Count-1;j>=0;j--) if (Vector2.Distance(p.transform.position,enemies[j].transform.position)<.28f)
            { var enemy=enemies[j]; enemy.Health--; RemoveProjectile(projectileIndex); if(enemy.Health<=0){ if (effectsSource != null) effectsSource.PlayOneShot(enemyDeathSound); score+=enemy.Points; if (!splitPickup.gameObject.activeSelf && Random.value < .10f) ActivateSplitPickup(enemy.transform.position); RemoveEnemy(j);} else if (effectsSource != null) effectsSource.PlayOneShot(enemyHitSound); return true; }
            return false;
        }

        private bool HitPlayer(int projectileIndex, Projectile p)
        {
            if (invincible<=0 && Vector2.Distance(p.transform.position,player.position)<.3f) { RemoveProjectile(projectileIndex); DamagePlayer(); return true; }
            return false;
        }

        private void DamagePlayer()
        {
            shields--; invincible=1f; SpawnPlayerDamageBurst(); if (shields <= 0) EndGame();
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

        private void BeginWarp() { cores=0; phase++; warpTimer=1.5f; StartWave(); SpawnWarpBurst(80,2.4f); }
        private void StartWave() { spawnsLeft = 7 + phase * 3 + cores * 2; spawnTimer = .5f; }

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
                s.Renderer.color=new Color(1,1,1,Mathf.Clamp01(s.Life));
                if(s.Life<=0)RemoveStar(i);
            }
            if(warpTimer>0) warpTimer-=dt;
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
                shard.ResetShard(player.position, direction * Random.Range(1.3f, 2.5f), Random.Range(.035f, .075f));
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
                color.a = Mathf.Clamp01(shard.Life / .28f);
                shard.Renderer.color = color;
                if (shard.Life <= 0f) { damageShards.RemoveAt(i); damageShardPool.Release(shard); }
            }
        }
        private void RemoveEnemy(int index){var e=enemies[index];enemies.RemoveAt(index);enemyPool.Release(e);}
        private void RemoveProjectile(int index){if(index<0||index>=projectiles.Count)return;var p=projectiles[index];projectiles.RemoveAt(index);projectilePool.Release(p);}
        private void RemoveStar(int index){var s=stars[index];stars.RemoveAt(index);starPool.Release(s);}
        private static Vector2 Rotate(Vector2 value,float degrees){var r=degrees*Mathf.Deg2Rad;return new Vector2(value.x*Mathf.Cos(r)-value.y*Mathf.Sin(r),value.x*Mathf.Sin(r)+value.y*Mathf.Cos(r));}

        private void EndGame(){playing=false;showResults=true;if(musicSource!=null)musicSource.Stop();bestScore=Mathf.Max(bestScore,score);PlayerPrefs.SetInt("orbital_rift_best",bestScore);PlayerPrefs.Save();}

        private static GUIStyle MakeLabelStyle(int fontSize, Color color)
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                wordWrap = true,
                normal = { textColor = color }
            };
        }

        private static GUIStyle MakeButtonStyle(int fontSize)
        {
            return new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                wordWrap = true
            };
        }

        private void OnGUI()
        {
            var safe = Screen.safeArea;
            var top = Screen.height - safe.yMax;
            var scale = Mathf.Clamp(Mathf.Min(safe.width / 1080f, safe.height / 1920f), .72f, 1.35f);
            var font = Mathf.RoundToInt(42f * scale);
            var line = Mathf.RoundToInt(font * 1.45f);
            var style = MakeLabelStyle(font, Color.white);
            var button = MakeButtonStyle(Mathf.RoundToInt(38f * scale));
            var left = safe.x;
            var width = safe.width;
            var height = safe.height;

            if (showMenu)
            {
                var title = MakeLabelStyle(Mathf.RoundToInt(62f * scale), Color.white);
                GUI.Label(new Rect(left, top + height * .16f, width, line * 1.8f), "ORBITAL RIFT", title);
                if (GUI.Button(new Rect(left + width * .2f, top + height * .50f, width * .6f, 82f * scale), "ИГРАТЬ", button)) StartGame();
                GUI.Label(new Rect(left, top + height * .63f, width, line * 1.3f), "РЕКОРД: " + bestScore, style);
                return;
            }

            if (playing)
            {
                GUI.Label(new Rect(left + 12f * scale, top + 10f * scale, width - 24f * scale, line * 2.2f), "СЧЁТ " + score + "    ФАЗА " + phase + "\nЩИТЫ " + shields + "    ЯДРА " + cores + "/3", style);
                if (splitShot) GUI.Label(new Rect(left, top + line * 2.2f, width, line), "SPLIT SHOT " + splitShotTimer.ToString("0.0"), style);
                if (coreActive) GUI.Label(new Rect(left, top + line * 3.1f, width, line), "ЭНЕРГО-ЯДРО НА ОРБИТЕ", style);
                if (warpTimer > 0) GUI.Label(new Rect(left, top + height * .30f, width, line * 2.2f), "РАУНД ПРОЙДЕН\nФАЗА " + phase, style);
                if (touchHintTimer > 0 && !paused)
                {
                    var hint = MakeLabelStyle(Mathf.RoundToInt(29f * scale), new Color(1f, 1f, 1f, .82f));
                    GUI.Label(new Rect(left, top + height * .84f, width * .5f, line * 1.2f), "ЛЕВО — ПО ЧАСОВОЙ", hint);
                    GUI.Label(new Rect(left + width * .5f, top + height * .84f, width * .5f, line * 1.2f), "ПРАВО — ПРОТИВ ЧАСОВОЙ", hint);
                }
                if (paused)
                {
                    GUI.Label(new Rect(left, top + height * .40f, width, line * 1.3f), "ПАУЗА", style);
                    if (GUI.Button(new Rect(left + width * .3f, top + height * .50f, width * .4f, 76f * scale), "ПРОДОЛЖИТЬ", button)) paused = false;
                }
                return;
            }

            if (showResults)
            {
                GUI.Label(new Rect(left, top + height * .25f, width, line * 1.3f), "СИГНАЛ ПОТЕРЯН", style);
                GUI.Label(new Rect(left, top + height * .34f, width, line * 3.5f), "СЧЁТ " + score + "\nРЕКОРД " + bestScore + "\nФАЗА " + phase, style);
                if (GUI.Button(new Rect(left + width * .25f, top + height * .58f, width * .5f, 76f * scale), "ЕЩЁ РАЗ", button)) StartGame();
                if (GUI.Button(new Rect(left + width * .25f, top + height * .69f, width * .5f, 76f * scale), "МЕНЮ", button)) { showResults = false; showMenu = true; }
            }
        }
    }
}
