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
    /// <summary>Opt-in Play Mode regression of real sandbox mechanics, with rendered Game View evidence.</summary>
    [InitializeOnLoad]
    public static class AbilitySandboxValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Pending = "OrbitalRift.SandboxQA.Pending";
        private static GameManager manager;
        private static AbilitySandboxSession session;
        private static string evidence;
        private static readonly List<string> report = new List<string>();
        private static readonly List<string> errors = new List<string>();
        private static IPlayerCommandSource previousInput;
        private static double nextPoll;
        private static readonly AbilitySandboxAbilityId[] NewAbilities =
        {
            AbilitySandboxAbilityId.OrbitAnchor, AbilitySandboxAbilityId.TrajectoryReplay,
            AbilitySandboxAbilityId.DelayedShot, AbilitySandboxAbilityId.CourseRupture,
            AbilitySandboxAbilityId.GravityWave, AbilitySandboxAbilityId.MineRing,
            AbilitySandboxAbilityId.Polarity, AbilitySandboxAbilityId.GhostTrail, AbilitySandboxAbilityId.Gigantism
        };

        static AbilitySandboxValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            // File polling is enabled only for an explicitly launched QA editor.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-orbitalSandboxQA") >= 0)
                EditorApplication.update += PollRequest;
        }

        private static void PollRequest()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1f;
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/SandboxMechanicsQA.request"));
            if (!File.Exists(path)) return;
            var token = File.ReadAllText(path).Trim();
            var previousToken = SessionState.GetString("OrbitalRift.SandboxQA.Token", "");
            if (token == previousToken) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
            SessionState.SetString("OrbitalRift.SandboxQA.Token", token);
            SessionState.SetBool(Pending, true);
            AssetDatabase.Refresh();
            EditorApplication.delayCall += StartPending;
        }

        [MenuItem("Orbital Rift/Validate Ability Sandbox (Play Mode)")]
        public static void RunFromMenu()
        {
            SessionState.SetBool(Pending, true);
            StartPending();
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void AfterReload() { if (SessionState.GetBool(Pending, false)) EditorApplication.delayCall += StartPending; }

        private static void StartPending()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += StartPending; return; }
            var gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.maximized = true;
            gameView.Focus();
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            else Begin();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        { if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false)) EditorApplication.delayCall += Begin; }

        private static void Begin()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            manager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                // Boot creates the runtime GameManager during its first frame; defer the
                // harness once instead of reporting a false failure during scene load.
                SessionState.SetBool(Pending, true);
                EditorApplication.delayCall += Begin;
                return;
            }
            evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/SandboxMechanicsEvidence"));
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++) if (args[i] == "-sandboxEvidence") evidence = args[i + 1];
            Directory.CreateDirectory(evidence);
            report.Clear(); errors.Clear();
            Application.logMessageReceived += CaptureError;
            manager.StartCoroutine(Guarded());
        }

        private static IEnumerator Guarded()
        {
            yield return null; yield return null;
            var test = Run();
            while (true)
            {
                object current;
                try { if (!test.MoveNext()) break; current = test.Current; }
                catch (Exception ex) { errors.Add(ex.ToString()); break; }
                yield return current;
            }
            if (manager != null)
            {
                if (session != null && session.IsOpen) Call("ExitAbilitySandbox");
                if (previousInput != null) Set("playerCommandSource", previousInput);
                manager.enabled = true;
            }
            Application.logMessageReceived -= CaptureError;
            var success = errors.Count == 0;
            report.Add(success ? "PASS: all sandbox checks completed." : "FAIL: " + string.Join("\n", errors));
            File.WriteAllText(Path.Combine(evidence, "validation.txt"), string.Join("\n", report));
            Debug.Log("SANDBOX QA " + (success ? "PASS" : "FAIL") + ": " + evidence);
        }

        private static IEnumerator Run()
        {
            previousInput = Get<IPlayerCommandSource>("playerCommandSource");
            Set("playerCommandSource", new NoInput());
            session = Get<AbilitySandboxSession>("abilitySandbox");
            yield return Capture("00-menu");
            Fresh();
            Require(session.Catalog.Count == 26, "Catalog has previous 17 plus nine new abilities");
            Require(session.Loadout.Count == 10, "Nine new abilities plus black hole fill exactly ten slots");
            session.ToggleAbility(AbilitySandboxAbilityId.SolarChicks);
            Require(session.Loadout.Count == 10, "Eleventh ability rejected");
            session.ToggleAbility(AbilitySandboxAbilityId.GhostTrail);
            Require(!session.HasPassive(AbilitySandboxAbilityId.GhostTrail), "Passive removes from loadout");
            session.ToggleAbility(AbilitySandboxAbilityId.GhostTrail);
            Require(session.HasPassive(AbilitySandboxAbilityId.GhostTrail), "Passive re-adds without duplicates");
            for (var i = 0; i < NewAbilities.Length; i++)
            {
                var def = session.Find(NewAbilities[i]);
                var texture = (Texture2D)typeof(AbilitySandboxSession).GetMethod("IconTexture", Private).Invoke(session, new object[] { def });
                Require(texture != null && texture.width >= 128, "Dedicated icon: " + def.Id);
            }
            SetSession("loadoutOpen", true); SetSession("catalogPage", 2);
            yield return Capture("01-active-catalog");
            SetSession("catalogPage", 3);
            yield return Capture("02-active-catalog");
            SetSession("visibleCategory", AbilitySandboxCategory.Passive); SetSession("catalogPage", 1);
            yield return Capture("03-passive-catalog");
            SetSession("loadoutOpen", false);

            // 1. A real force and a deliberate escape, rather than only an anchor marker.
            Fresh(); Cast(AbilitySandboxAbilityId.OrbitAnchor);
            var angle = Get<float>("playerAngle"); Step(.45f);
            Require(AngleDifference(angle, Get<float>("playerAngle")) > .20f, "Anchor pulls idle pilot");
            yield return Capture("04-orbit-anchor");
            Step(.9f, -1);
            Require(Get<float>("sandboxAnchorTimer") == 0f, "Holding away breaks anchor");
            Cast(AbilitySandboxAbilityId.OrbitAnchor);
            Require(Get<float>("sandboxAnchorTimer") == 0f, "Cooldown rejects premature reactivation");

            // 2. Position comes from actual recorded flight, and the replay emits shots.
            Fresh(); Step(2.4f, 1); Cast(AbilitySandboxAbilityId.TrajectoryReplay); Step(.3f);
            var delayedAngle = (float)Call("SandboxRecordedAngle", Get<float>("sandboxClock") - 2f);
            Require(Mathf.Abs(AngleDifference(Get<float>("playerAngle"), delayedAngle)) > .5f, "Replay reads movement from two seconds ago");
            Require(Projectiles.Exists(p => p.FromRiftEcho), "Replay fires real sandbox projectiles");
            yield return Capture("05-trajectory-replay");

            // 3. Aim is resolved after the hold, using the pilot's changed position.
            Fresh(); Cast(AbilitySandboxAbilityId.DelayedShot); Step(.65f, 1);
            Require(Projectiles.Count == 0, "Delayed shot remains suspended during charge");
            yield return Capture("06-delayed-charge");
            Step(.6f);
            Require(Projectiles.Count == 3, "Delayed shot releases three projectiles");
            foreach (var p in Projectiles)
                Require(Vector2.Dot(p.Velocity.normalized, ((Vector2)Player.position - (Vector2)p.transform.position).normalized) > .97f, "Released shot targets new position");
            yield return Capture("07-delayed-release");

            // 4. Input and tangential courses reverse, then return to normal.
            Fresh(); Cast(AbilitySandboxAbilityId.HarrierRiftCopies); Cast(AbilitySandboxAbilityId.CourseRupture);
            angle = Get<float>("playerAngle"); Step(.4f, 1);
            Require(AngleDifference(angle, Get<float>("playerAngle")) < -.3f, "Course rupture reverses held input");
            yield return Capture("08-course-rupture");
            Step(3f); angle = Get<float>("playerAngle"); Step(.4f, 1);
            Require(AngleDifference(angle, Get<float>("playerAngle")) > .3f, "Course restores when duration ends");

            // 7. The wave intersects each object once and shifts the pilot without damage.
            Fresh(); Call("SandboxSeedProjectiles", 8, Color.cyan, true);
            angle = Get<float>("playerAngle"); Cast(AbilitySandboxAbilityId.GravityWave); Step(1.25f);
            yield return Capture("09-gravity-wave");
            Step(.3f);
            Require(Mathf.Abs(AngleDifference(angle, Get<float>("playerAngle")) - .38f) < .015f, "Wave moves pilot by 22 degrees exactly once");
            Require(Get<HashSet<Projectile>>("sandboxWaveHits").Count > 0, "Wave displaces real projectiles");

            // 9. Arming, sector crossings and one-shot detonations.
            Fresh(); Cast(AbilitySandboxAbilityId.MineRing); Step(1f);
            yield return Capture("10-mine-ring");
            var mineBase = Get<float>("sandboxMineAngle");
            for (var i = 0; i < 8; i++) { Set("playerAngle", mineBase + i * Mathf.PI * .25f); Set("targetAngle", mineBase + i * Mathf.PI * .25f); Step(.04f); }
            Require(Array.TrueForAll(Get<bool[]>("sandboxMines"), active => !active), "All eight sectors detonate on crossing");
            yield return Capture("11-mine-detonations");

            // 10. Both polarities exist and their forces have opposite signs.
            Fresh(); Cast(AbilitySandboxAbilityId.Polarity); Step(.1f);
            var velocities = new Dictionary<Projectile, Vector2>();
            foreach (var p in Projectiles) velocities[p] = p.Velocity;
            Step(.1f);
            var charges = (IDictionary)Get<object>("sandboxPolarities"); var positive = 0; var negative = 0;
            foreach (DictionaryEntry entry in charges)
            {
                var p = (Projectile)entry.Key;
                var plus = (bool)entry.Value.GetType().GetField("Positive").GetValue(entry.Value);
                var dot = Vector2.Dot(p.Velocity - velocities[p], ((Vector2)Player.position - (Vector2)p.transform.position).normalized);
                Require(plus ? dot > 0f : dot < 0f, "Polarity accelerates with correct sign");
                if (plus) positive++; else negative++;
            }
            Require(positive == 6 && negative == 6, "Twelve sample charges split into six plus and six minus");
            yield return Capture("12-polarity");

            // 18. Passive emission, re-entry impulse and immediate removal.
            Fresh(); Step(1f, 1);
            Require(((IList)Get<object>("sandboxGhost")).Count > 0, "Ghost passive records live trail segments");
            yield return Capture("13-ghost-trail");
            var triggered = false;
            for (var i = 0; i < 70; i++) { Step(1f / 60f, -1); triggered |= Get<float>("sandboxGhostCooldown") > 0f; }
            Require(triggered, "Re-crossing mature trail triggers impulse");
            session.ToggleAbility(AbilitySandboxAbilityId.GhostTrail); Step(.1f);
            Require(((IList)Get<object>("sandboxGhost")).Count == 0, "Removing passive clears its trail immediately");

            // Size is functional: a larger collision envelope pushes incoming shots away.
            Fresh(); var normalScale = Player.localScale; Cast(AbilitySandboxAbilityId.Gigantism); Step(.6f);
            Require(Mathf.Abs(Player.localScale.x / normalScale.x - 2.3f) < .01f, "Gigantism grows ship to 2.3x");
            Call("SandboxSeedProjectiles", 1, Color.yellow, false);
            var incoming = Projectiles[0]; incoming.transform.position = Player.position + Vector3.up * .8f; incoming.Velocity = Vector2.down;
            Step(.02f);
            Require(incoming.Velocity.y > 0f, "Giant hull deflects projectile outside normal hull radius");
            yield return Capture("14-gigantism");
            Step(5f);
            Require(Vector3.Distance(Player.localScale, normalScale) < .001f, "Natural expiry restores exact ship scale");

            // Shared slot routing ignores passive positions and supports the tenth key (0).
            Fresh(); session.QueueActiveSlot(0); Step(.03f);
            Require(Get<float>("sandboxAnchorTimer") > 0f, "First active slot activates anchor");
            session.ToggleAbility(AbilitySandboxAbilityId.GhostTrail); session.ToggleAbility(AbilitySandboxAbilityId.SolarChicks);
            session.QueueActiveSlot(9); Step(.03f);
            Require(Projectiles.Count == 3, "Tenth active slot (0) activates after replacing passive");
            yield return Capture("15-ten-active-slots");

            // Combinations and bounded pools, still without AI, damage or scoring.
            Fresh();
            foreach (var id in NewAbilities) if (id != AbilitySandboxAbilityId.GhostTrail) Cast(id);
            Cast(AbilitySandboxAbilityId.BlackHole); Step(1.3f, 1);
            yield return Capture("16-combined-effects");
            Require(Projectiles.Count <= 80, "Combined casts respect projectile cap");
            Require(Get<Enemy>("sandboxBoss").transform.position.sqrMagnitude < .00001f, "Dummy remains centered");
            // Cloud profile loads may complete between screenshots; measure the synchronous sandbox simulation itself.
            var score = Get<int>("score"); var best = Get<int>("bestScore"); var mmr = Get<int>("mmr");
            Step(12f, -1);
            Require(Get<int>("shields") == 3 && Get<int>("score") == score && Get<int>("bestScore") == best && Get<int>("mmr") == mmr,
                "Sandbox preserves state: HP " + Get<int>("shields") + "/3, score " + Get<int>("score") + "/" + score +
                ", best " + Get<int>("bestScore") + "/" + best + ", MMR " + Get<int>("mmr") + "/" + mmr);
            var root = GameObject.Find("Sandbox mechanic FX");
            Require(root != null && root.transform.childCount <= 304, "Presentation uses bounded reusable renderer pools");
            var layered = Get<SandboxLayeredVfx>("sandboxLayeredVfx");
            Require(layered != null && GameObject.Find("Sandbox layered VFX") != null,
                "All abilities share the nine-layer VFX director");
            Cast(AbilitySandboxAbilityId.SolarChicks); Step(1.05f);
            var requiredShotPhases = QualitySettings.GetQualityLevel() <= 1 ? (1 << 0) | (1 << 1) | (1 << 2) | (1 << 4) | (1 << 6) : 0x7f;
            Require(layered != null && (layered.LifetimeShotPhaseMask & requiredShotPhases) == requiredShotPhases,
                "Sandbox shots render anticipation, core, trail, impact and aftermath phases");
            Step(20f);
            Require(Projectiles.Count == 0, "All demonstration projectiles expire");
            Cast(AbilitySandboxAbilityId.Gigantism); Step(.5f); Call("ExitAbilitySandbox");
            Require(Vector3.Distance(Player.localScale, normalScale) < .001f, "Exit during growth restores scale");
            Require(!session.IsOpen && Get<bool>("showMenu") && !Get<bool>("playing"), "Exit returns to main menu");
            Require(root != null && !root.activeSelf && Projectiles.Count == 0, "Exit hides effects and returns projectiles to pool");
            yield return Capture("17-exit-menu");
            Fresh();
            session.Close();
            Call("PauseFromCanvas"); Call("UpdateCanvasUi");
            var pauseView = Object.FindFirstObjectByType<UI.PauseOverlayView>();
            Require(pauseView != null, "Classic pause overlay remains available");
            var modal = (RectTransform)typeof(UI.PauseOverlayView).GetField("modal", Private).GetValue(pauseView);
            Require(modal.gameObject.activeSelf, "Pause opens its visible modal");
            yield return Capture("18-pause-regression", true);
            var icons = (UnityEngine.UI.Image[])typeof(UI.PauseOverlayView).GetField("abilityIcons", Private).GetValue(pauseView);
            Require(icons.Length == 3 && Array.TrueForAll(icons, icon => icon != null && icon.sprite != null), "Pause keeps all three Phoenix spell icons");
            Call("ExitAbilitySandbox");
            Require(errors.Count == 0, "No runtime errors or exceptions during checks");
        }

        private static void Fresh()
        {
            manager.enabled = false;
            if (session.IsOpen) Call("ExitAbilitySandbox");
            Call("OpenAbilitySandbox");
            var old = new List<AbilitySandboxAbilityId>();
            foreach (var def in session.Loadout) old.Add(def.Id);
            foreach (var id in old) session.ToggleAbility(id);
            foreach (var id in NewAbilities) session.ToggleAbility(id);
            session.ToggleAbility(AbilitySandboxAbilityId.BlackHole);
            Set("paused", false);
            Step(.03f);
        }

        private static void Step(float seconds, int direction = 0)
        {
            for (var t = 0f; t < seconds - .00001f; t += 1f / 60f)
            {
                var dt = Mathf.Min(1f / 60f, seconds - t);
                Call("UpdateAbilitySandbox", dt, new PlayerCommandFrame(direction, false, false));
                Call("UpdateDamageShards", dt);
            }
        }

        private static IEnumerator Capture(string name, bool keepPaused = false)
        {
            manager.enabled = true;
            Set("uiFadeTimer", 0f);
            if (!keepPaused) Set("paused", false);
            yield return null; yield return null;
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(evidence, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
            manager.enabled = false;
        }

        private static void Cast(AbilitySandboxAbilityId id) { Call("TriggerAbilitySandboxSpell", id); }
        private static Transform Player => Get<Transform>("player");
        private static List<Projectile> Projectiles => Get<List<Projectile>>("projectiles");
        private static T Get<T>(string name) { return (T)typeof(GameManager).GetField(name, Private).GetValue(manager); }
        private static void Set(string name, object value) { typeof(GameManager).GetField(name, Private).SetValue(manager, value); }
        private static void SetSession(string name, object value) { typeof(AbilitySandboxSession).GetField(name, Private).SetValue(session, value); }
        private static object Call(string name, params object[] args) { return typeof(GameManager).GetMethod(name, Private).Invoke(manager, args); }
        private static float AngleDifference(float from, float to) { return Mathf.DeltaAngle(from * Mathf.Rad2Deg, to * Mathf.Rad2Deg) * Mathf.Deg2Rad; }
        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception(message); report.Add("PASS: " + message); }
        private static void CaptureError(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(condition + "\n" + trace); }
        private sealed class NoInput : IPlayerCommandSource
        { public PlayerCommandFrame ReadFrame() => PlayerCommandFrame.None; public void Reset() { } }
    }
}
#endif
