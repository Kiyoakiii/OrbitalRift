#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OrbitalRift
{
    // Repeatable checks in the open Editor; no scene or profile is saved by QA.
    [InitializeOnLoad]
    public static class GameplayRestorationValidation
    {
        const string Evidence = "Docs/VFX_Evidence/GameplayRestoration/";
        const string Request = "Temp/GameplayRestoration.command";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly List<string> Report = new List<string>();
        static GameManager manager;

        static GameplayRestorationValidation()
        {
            EditorApplication.update += ReadCommand;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("GameplayRestorationQA", false))
                    EditorApplication.delayCall += Begin;
            };
        }

        static void ReadCommand()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool("GameplayRestorationBuild", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool("GameplayRestorationBuild", false);
                // Execute from the idle editor update: a queued delayCall can be lost during Play exit/domain reload.
                BuildApk();
                return;
            }
            if (!File.Exists(Request)) return;
            var command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            Directory.CreateDirectory(Evidence);
            if (command == "status")
                File.WriteAllText(Evidence + "status.txt", "Playing=" + EditorApplication.isPlaying + "; target=" + EditorUserBuildSettings.activeBuildTarget);
            else if (command == "verify") Run();
            else if (command == "build-apk")
            {
                SessionState.SetBool("GameplayRestorationBuild", true);
                EditorApplication.isPlaying = false;
            }
        }

        [MenuItem("Orbital Rift/Validate Restored Waves and Asteroids")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before QA.");
            Directory.CreateDirectory(Evidence);
            SessionState.SetBool("GameplayRestorationQA", true);
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }

        static void Begin()
        {
            manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null) { EditorApplication.delayCall += Begin; return; }
            SessionState.SetBool("GameplayRestorationQA", false);
            Application.runInBackground = true;
            Time.timeScale = 1;
            manager.StartCoroutine(Guard());
        }

        static IEnumerator Guard()
        {
            Report.Clear();
            Application.logMessageReceived += Log;
            var checks = Checks();
            while (true)
            {
                object current;
                try { if (!checks.MoveNext()) break; current = checks.Current; }
                catch (Exception ex) { Report.Add("FAIL " + ex); break; }
                yield return current;
            }
            (checks as IDisposable)?.Dispose();
            Application.logMessageReceived -= Log;
            Report.Add(Report.Exists(s => s.StartsWith("FAIL")) ? "FAIL" : "PASS");
            File.WriteAllText(Evidence + "runtime.txt", DateTime.Now.ToString("O") + "\n" + string.Join("\n", Report));
            Debug.Log("GAMEPLAY RESTORATION QA " + Report[Report.Count - 1]);
        }

        static IEnumerator Checks()
        {
            GameplayRulesValidator.RunFromMenu();
            Require(!string.IsNullOrWhiteSpace(Get<string>("playerNickname")), "Existing player profile available");
            Call("StartGame");
            Set("autoFire", false);
            Set("invincible", 1000f);
            yield return new WaitForSeconds(.9f);
            var c = manager.SpaceDepth;
            Require(c != null, "Classic run uses SpaceDepth");
            var rocks = c.Layers[7];
            Require(rocks.Settings.Enabled && rocks.ActiveCount == 24, "24 asteroids are active");
            var material = rocks.Renderer.sharedMaterial;
            Require(rocks.Settings.Appearance == SpaceLayerAppearance.Procedural && material.HasProperty("_Kind") && material.GetFloat("_Kind") == 4,
                "Asteroid renderer uses the rock shape, with no missing image dependency");
            Require(!ShaderUtil.ShaderHasError(material.shader), "Asteroid shader compiles");
            Require(c.Layers[4].ActiveCount == 0, "Planets stay disabled");
            Capture("gameplay", c, 1920, 1080, false);
            Capture("space-with-asteroids", c, 1920, 1080, true);
            var originalSolo = c.SoloLayer;
            c.SoloLayer = 7;
            c.Tick(0);
            Capture("asteroids-only", c, 1920, 1080, true);
            c.SoloLayer = originalSolo;
            c.Tick(0);
            var position = rocks.GetItemPosition(0);
            var spin = rocks.GetItemSpin(0);
            yield return new WaitForSeconds(.5f);
            Require(Vector2.Distance(position, rocks.GetItemPosition(0)) > .001f && Mathf.Abs(spin - rocks.GetItemSpin(0)) > .001f,
                "Asteroids travel and rotate during gameplay");

            // Exercise real spawning and collectible-core transitions. QA removes
            // spawned enemies through their pool instead of waiting for combat.
            Call("StartGame");
            Set("autoFire", false);
            var enemies = Get<List<Enemy>>("enemies");
            var expected = new[] { BossArchetype.AstralFirebird, BossArchetype.VoidMaw, BossArchetype.UmbralHarrier, BossArchetype.AstralFirebird };
            for (var phase = 1; phase <= 12; phase++)
            {
                Require(Get<int>("phase") == phase, "Phase advances sequentially to " + phase);
                var bossPhase = phase % 3 == 0;
                var waves = bossPhase ? 1 : 3;
                for (var wave = 0; wave < waves; wave++)
                {
                    Set("warpTimer", 0f);
                    if (bossPhase)
                    {
                        Require(Get<bool>("bossSpawnPending") && Get<int>("spawnsLeft") == 0, "Boss intro is scheduled");
                        Call("UpdateSpawning", BossSettings.IntroDelay + .01f);
                        Require(enemies.Count == 1 && enemies[0].Kind == EnemyKind.Boss && enemies[0].BossType == expected[phase / 3 - 1],
                            "Boss type at phase " + phase + " is " + expected[phase / 3 - 1]);
                        Report.Add("Phase " + phase + ": " + enemies[0].BossType + ", one encounter");
                        Call("RemoveEnemy", 0);
                    }
                    else
                    {
                        Require(!Get<bool>("bossSpawnPending"), "Ordinary waves do not schedule a boss");
                        var count = Get<int>("spawnsLeft");
                        Require(count == 5 + phase * 2 + wave * 2, "Ordinary wave retains its enemy count");
                        for (var spawn = 0; spawn < count; spawn++)
                        {
                            Call("UpdateSpawning", 100f);
                            Require(enemies.Count == 1 && enemies[0].Kind != EnemyKind.Boss, "Ordinary enemy actually spawns");
                            Call("RemoveEnemy", 0);
                        }
                        Report.Add("Phase " + phase + ", wave " + (wave + 1) + ": " + count + " ordinary enemies");
                    }
                    Call("UpdateEnemies", 0f);
                    Require(Get<bool>("coreActive"), "Cleared wave exposes a collectible core");
                    var angle = Get<float>("coreAngle");
                    var center = manager.ShipOrbitCenter;
                    Get<Transform>("player").position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * OrbitSettings.Radius;
                    Call("UpdateCore", 0f);
                    Require(Get<int>("phase") == (wave == waves - 1 ? phase + 1 : phase), "Core advances only at the end of the phase");
                }
            }
            Call("StartGame");
            Set("autoFire", false);
            Set("invincible", 1000f);
            yield return new WaitForSeconds(1.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Evidence + "gameplay-hud.png");
            yield return null;
            Report.Add("Ordinary phases 1/2, 4/5, 7/8, 10/11 verified; boss phases 3/6/9/12 verified.");
            Report.Add("Asteroid motion and camera rendering verified in Unity Editor; Android device performance is not measured here.");
        }

        static void BuildApk()
        {
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + "build.txt", "RUNNING " + DateTime.Now.ToString("O"));
            try
            {
                Application.logMessageReceived += BuildLog;
                BuildAndroid.BuildDebugApk();
                var apk = new FileInfo("Builds/OrbitalRift-debug.apk");
                File.WriteAllText(Evidence + "build.txt", "PASS " + DateTime.Now.ToString("O") + "\n" + apk.FullName + "\nBytes=" + apk.Length);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Evidence + "build.txt", "FAIL " + ex);
                Debug.LogException(ex);
            }
            finally { Application.logMessageReceived -= BuildLog; }
        }

        static void BuildLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception)File.AppendAllText(Evidence+"build-errors.txt",message+"\n"+trace+"\n");}
        static void Capture(string name, SpaceFlightVisualController c, int width, int height, bool isolated) =>
            typeof(SpaceReferenceArtValidation).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "../GameplayRestoration/" + name, c, width, height, isolated });
        static T Get<T>(string field) => (T)typeof(GameManager).GetField(field, Private).GetValue(manager);
        static void Set(string field, object value) => typeof(GameManager).GetField(field, Private).SetValue(manager, value);
        static void Call(string method, params object[] args) => typeof(GameManager).GetMethod(method, Private).Invoke(manager, args);
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Report.Add("FAIL " + message);
        }
    }
}
#endif
