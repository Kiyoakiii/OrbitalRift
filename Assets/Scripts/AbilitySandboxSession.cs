using System;
using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    public enum AbilitySandboxCategory : byte
    {
        Active,
        Passive
    }

    public enum AbilitySandboxAbilityId : byte
    {
        SolarChicks,
        AshenEgg,
        PhoenixDive,
        HarrierRiftCopies,
        HarrierPhaseDash,
        HarrierColdFan,
        VoidRiftBeam,
        VoidGravityRoots,
        VoidBarrage,
        BlackHole,
        RiftEcho,
        VectorSnap,
        SolarPlume,
        AegisOrbit,
        EchoResonator,
        EmberCore,
        KineticOverdrive,
        OrbitAnchor,
        TrajectoryReplay,
        DelayedShot,
        CourseRupture,
        GravityWave,
        MineRing,
        Polarity,
        GhostTrail,
        Gigantism
    }

    /// <summary>
    /// A local, no-score test bench for abilities. It owns only selection and presentation;
    /// GameManager remains the owner of actual world effects and combat systems.
    /// </summary>
    public sealed class AbilitySandboxSession
    {
        public const int MaxLoadoutSize = 10;
        private const float SandboxLift = .12f;
        private const float BossControlLeft = .635f;
        private const float BossControlWidth = .105f;
        private const float TimeControlLeft = .748f;
        private const float TimeControlWidth = .232f;
        private const float PreviewTimeScaleMin = .05f;
        private const float PreviewTimeScaleMax = 2f;

        public sealed class Definition
        {
            public readonly AbilitySandboxAbilityId Id;
            public BossAbilityDefinition Asset => (int)Id<=8 ? BossAssetRegistry.Ability((BossAbilityId)(int)Id) : Id==AbilitySandboxAbilityId.BlackHole?BossAssetRegistry.Ability(BossAbilityId.VoidBlackHole):Id==AbilitySandboxAbilityId.SolarPlume?BossAssetRegistry.Ability(BossAbilityId.FirebirdSolarPlume):Id==AbilitySandboxAbilityId.EmberCore?BossAssetRegistry.Ability(BossAbilityId.FirebirdEmberCore):null;
            public readonly AbilitySandboxCategory Category;
            public readonly string Source;
            readonly string originalName;
            public string Name => Asset != null ? Asset.DisplayName : originalName;
            public readonly string ShortName;
            readonly string originalDescription;
            public string Description => Asset != null ? Asset.Description : originalDescription;
            readonly string originalTiming;
            public string Timing => Asset != null ? "подготовка " + Asset.CastDelay.ToString("0.##") + " с / действие " + Asset.Duration.ToString("0.##") + " с" : originalTiming;
            public readonly string IconResource;
            readonly Color originalAccent;
            public Color Accent => Asset != null ? Asset.Style.Primary : originalAccent;
            readonly float originalCooldown;
            public float Cooldown => Asset != null ? Asset.Cooldown : originalCooldown;

            public Definition(AbilitySandboxAbilityId id, AbilitySandboxCategory category, string source,
                string name, string shortName, string description, string timing, string iconResource, Color accent, float cooldown = 0f)
            {
                Id = id;
                Category = category;
                Source = source;
                originalName = name;
                ShortName = shortName;
                originalDescription = description;
                originalTiming = timing;
                IconResource = iconResource;
                originalAccent = accent;
                originalCooldown = cooldown;
            }
        }

        private static readonly KeyCode[] SlotKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
        };

        private readonly List<Definition> catalog = new List<Definition>(MaxLoadoutSize);
        private readonly List<Definition> loadout = new List<Definition>(MaxLoadoutSize);
        private readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();
        private readonly Dictionary<AbilitySandboxAbilityId, float> readyAt = new Dictionary<AbilitySandboxAbilityId, float>();
        private readonly SandboxVfxLayerEditor vfxEditor = new SandboxVfxLayerEditor();
        private float sessionTime;
        private float previewTimeScale = 1f;
        private bool bossVisible = true;
        private AbilitySandboxCategory visibleCategory = AbilitySandboxCategory.Active;
        private int catalogPage;
        private AbilitySandboxAbilityId? queuedActivation;
        private float activationTimer;
        private string activationLabel = string.Empty;
        private Color activationColor = Color.white;
        private bool loadoutOpen;
        private bool closeRequested;

        public bool IsOpen { get; private set; }
        public IReadOnlyList<Definition> Loadout => loadout;
        public IReadOnlyList<Definition> Catalog => catalog;
        public bool LoadoutOpen => loadoutOpen;
        public bool VfxEditorOpen => vfxEditor.IsOpen;
        public SandboxVfxLayerEditor VfxEditor => vfxEditor;
        public float PreviewTimeScale => previewTimeScale;
        public bool BossVisible => bossVisible;

        public bool PointerOverControls()
        {
            if (vfxEditor.IsOpen) return true;
            if (!Input.GetMouseButton(0) && Input.touchCount == 0) return false;
            var position = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            var safe = Screen.safeArea;
            var x = (position.x - safe.x) / Mathf.Max(1f, safe.width);
            var y = (safe.yMax - position.y) / Mathf.Max(1f, safe.height);
            return new Rect(.018f, .145f - SandboxLift, .51f, .062f).Contains(new Vector2(x, y)) ||
                new Rect(.018f, .225f - SandboxLift, .285f, .485f).Contains(new Vector2(x, y)) ||
                new Rect(BossControlLeft, .145f - SandboxLift, BossControlWidth, .062f).Contains(new Vector2(x, y)) ||
                new Rect(TimeControlLeft, .145f - SandboxLift, TimeControlWidth, .062f).Contains(new Vector2(x, y));
        }

        public AbilitySandboxSession()
        {
            catalog.Add(new Definition(AbilitySandboxAbilityId.SolarChicks, AbilitySandboxCategory.Active, "БОСС",
                "СОЛНЕЧНЫЕ ПТЕНЦЫ", "ПТЕНЦЫ", "Три огненных птенца веером летят от босса к пилоту.",
                "Т 0.20 С  •  ПОЛЕТ 4.25 С", "BossAbilities/firebird_solar_chicks", new Color(1f, .50f, .08f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.AshenEgg, AbilitySandboxCategory.Active, "БОСС",
                "ПЕПЕЛЬНОЕ ЯЙЦО", "ЯЙЦО", "Манекен сворачивается в яйцо и возвращается через пять секунд.",
                "10 HP  •  ОКНО 5.0 С", "BossAbilities/firebird_ashen_egg", new Color(1f, .84f, .28f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.PhoenixDive, AbilitySandboxCategory.Active, "БОСС",
                "КОМЕТНЫЙ НЫРОК", "НЫРОК", "Феникс выпускает вокруг себя горячее кольцо и вспышку крыльев.",
                "Т 0.35 С  •  КД 2.45 С", "BossAbilities/firebird_phoenix_dive", new Color(1f, .25f, .04f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.HarrierRiftCopies, AbilitySandboxCategory.Active, "ТЕНЕВОЙ ОХОТНИК",
                "КОПИИ РАЗЛОМА", "КОПИИ", "Три двойника расходятся по орбите и выпускают холодные импульсы без урона.",
                "3 КОПИИ  •  ЖИВУТ 5.6 С", "BossAbilities/harrier_rift_copies", new Color(.70f, .32f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.HarrierPhaseDash, AbilitySandboxCategory.Active, "ТЕНЕВОЙ ОХОТНИК",
                "ФАЗОВЫЙ РЫВОК", "РЫВОК", "Фиолетовая траектория разрезает арену и раскрывается холодным импульсом.",
                "Т 0.25 С  •  КД 3.0 С", "BossAbilities/harrier_phase_dash", new Color(.48f, .78f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.HarrierColdFan, AbilitySandboxCategory.Active, "ТЕНЕВОЙ ОХОТНИК",
                "ХОЛОДНЫЙ ВЕЕР", "ВЕЕР", "Два холодных осколка летят веером от центра к текущей позиции пилота.",
                "2 СНАРЯДА  •  ТАКТ 0.95 С", "BossAbilities/harrier_cold_fan", new Color(.36f, .82f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.VoidRiftBeam, AbilitySandboxCategory.Active, "ПАСТЬ БЕЗДНЫ",
                "ЛУЧ РАЗЛОМА", "ЛУЧ", "Голубой телеграф собирается в центре, затем безопасно проходит по арене.",
                "Т 1.05 С  •  SWEEP 4.35 С", "BossAbilities/void_rift_beam", new Color(.34f, .90f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.VoidGravityRoots, AbilitySandboxCategory.Active, "ПАСТЬ БЕЗДНЫ",
                "ГРАВИКОРНИ", "КОРНИ", "Метка на орбите вырастает в три живых корня, не блокируя управление в песочнице.",
                "Т 1.05 С  •  УДЕРЖ. 1.0 С", "BossAbilities/void_gravity_roots", new Color(1f, .32f, .76f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.VoidBarrage, AbilitySandboxCategory.Active, "ПАСТЬ БЕЗДНЫ",
                "ЗАЛП БЕЗДНЫ", "ЗАЛП", "Три тёмных импульса уходят по направлению к пилоту и исчезают без столкновений.",
                "3 ИМПУЛЬСА  •  ТАКТ 0.90 С", "BossAbilities/void_barrage", new Color(.68f, .34f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.BlackHole, AbilitySandboxCategory.Active, "ПАСТЬ БЕЗДНЫ",
                "ЧЁРНАЯ ДЫРА", "ДЫРА", "Аккреционный вихрь притягивает пилота, копии, снаряды и осколки к своей воронке.",
                "ПРИТЯЖЕНИЕ 5.5 С  •  БЕЗ УРОНА", "BossAbilities/void_rift_beam", new Color(.18f, .74f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.RiftEcho, AbilitySandboxCategory.Active, "ПИЛОТ",
                "ЭХО РАЗЛОМА", "ЭХО", "Создаёт рядом с пилотом электрическое эхо, которое ведёт огонь вместе с ним.",
                "3.25 С  •  КД 7.5 С", "BossAbilities/void_rift_beam", new Color(.34f, .90f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.VectorSnap, AbilitySandboxCategory.Active, "ПИЛОТ",
                "ВЕКТОРНЫЙ СНАП", "СНАП", "Мгновенно пересаживает корабль по орбите и кратко защищает его.",
                "86°  •  КД 14 С", "BossAbilities/harrier_phase_dash", new Color(.52f, .82f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.SolarPlume, AbilitySandboxCategory.Passive, "БОСС",
                "СОЛНЕЧНОЕ ОПЕРЕНИЕ", "ОПЕРЕНИЕ", "Постоянный жар вокруг крыльев манекена подчёркивает его силуэт.",
                "ПАССИВНО", "BossAbilities/firebird_solar_chicks", new Color(1f, .52f, .12f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.AegisOrbit, AbilitySandboxCategory.Passive, "ПИЛОТ",
                "ЭГИДА ОРБИТЫ", "ЭГИДА", "Подбирает фиолетовые звёзды в щит. Без пассивки звезда остаётся только визуальным объектом.",
                "ПАССИВНО  •  ЩИТ 2 УДАРА", "BossAbilities/firebird_ashen_egg", new Color(.76f, .38f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.EchoResonator, AbilitySandboxCategory.Passive, "ПИЛОТ",
                "РЕЗОНАТОР ЭХА", "РЕЗОНАТОР", "Холодное поле вокруг ядра делает работу Эха читаемой до его запуска.",
                "ПАССИВНО", "BossAbilities/void_rift_beam", new Color(.42f, .54f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.EmberCore, AbilitySandboxCategory.Passive, "БОСС",
                "УГОЛЬНОЕ ЯДРО", "УГОЛЬ", "Тлеющее ядро пульсирует в центре манекена, пока пассивка выбрана.",
                "ПАССИВНО", "BossAbilities/firebird_phoenix_dive", new Color(1f, .28f, .08f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.KineticOverdrive, AbilitySandboxCategory.Passive, "ПИЛОТ",
                "КИНЕТИЧЕСКИЙ ФОРСАЖ", "ФОРСАЖ", "След корабля становится ярче и подсказывает направление для Снапа.",
                "ПАССИВНО", "BossAbilities/harrier_phase_dash", new Color(.72f, .90f, 1f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.OrbitAnchor, AbilitySandboxCategory.Active, "ПИЛОТ",
                "ЯКОРЬ ОРБИТЫ", "ЯКОРЬ", "Тянет корабль к метке. Удерживай полёт от неё 0.75 с, чтобы разорвать связь.",
                "5 С  /  КД 7 С", "SandboxAbilities/orbit_anchor", new Color(.24f, 1f, .78f), 7f));
            catalog.Add(new Definition(AbilitySandboxAbilityId.TrajectoryReplay, AbilitySandboxCategory.Active, "ПИЛОТ",
                "ЗАПИСЬ ТРАЕКТОРИИ", "ЗАПИСЬ", "Двойник повторяет твой полёт с задержкой 2 с и стреляет в манекен.",
                "6 С  /  КД 8 С", "SandboxAbilities/trajectory_replay", new Color(.38f, .78f, 1f), 8f));
            catalog.Add(new Definition(AbilitySandboxAbilityId.DelayedShot, AbilitySandboxCategory.Active, "МАНЕКЕН",
                "ОТЛОЖЕННЫЙ ВЫСТРЕЛ", "ОТСРОЧКА", "Три заряда зависают. Через 1.2 с наводятся на твою новую позицию и летят.",
                "ЗАРЯД 1.2 С  /  КД 3 С", "SandboxAbilities/delayed_shot", new Color(1f, .73f, .24f), 3f));
            catalog.Add(new Definition(AbilitySandboxAbilityId.CourseRupture, AbilitySandboxCategory.Active, "ПИЛОТ",
                "РАЗРЫВ КУРСА", "РЕВЕРС", "На 3 с обращает управление и вращение копий, эха и летящих снарядов.",
                "3 С  /  КД 5 С", "SandboxAbilities/course_rupture", new Color(1f, .38f, .61f), 5f));
            catalog.Add(new Definition(AbilitySandboxAbilityId.GravityWave, AbilitySandboxCategory.Active, "МАНЕКЕН",
                "ГРАВИТАЦИОННАЯ ВОЛНА", "ВОЛНА", "Расширяющееся кольцо сдвигает корабль и снаряды по орбите при касании.",
                "Т 0.4 С  /  КД 3 С", "SandboxAbilities/gravity_wave", new Color(.58f, .52f, 1f), 3f));
            catalog.Add(new Definition(AbilitySandboxAbilityId.MineRing, AbilitySandboxCategory.Active, "МАНЕКЕН",
                "МИННОЕ КОЛЬЦО", "МИНЫ", "Восемь мин ждут пересечения своих секторов. Взрыв отталкивает снаряды.",
                "ВЗВОД 0.8 С  /  8 С  /  КД 9 С", "SandboxAbilities/mine_ring", new Color(1f, .36f, .16f), 9f));
            catalog.Add(new Definition(AbilitySandboxAbilityId.Polarity, AbilitySandboxCategory.Active, "ПИЛОТ",
                "ПОЛЯРНОСТЬ", "ПОЛЮСЫ", "Голубые заряды + притягиваются к кораблю; розовые − отталкиваются.",
                "6 С  /  КД 8 С", "SandboxAbilities/polarity", new Color(.36f, .90f, 1f), 8f));
            catalog.Add(new Definition(AbilitySandboxAbilityId.GhostTrail, AbilitySandboxCategory.Passive, "ПИЛОТ",
                "ПРИЗРАЧНЫЙ СЛЕД", "СЛЕД", "Оставляет след на 3 с. Вернись через него: вспыхнет импульс, отталкивающий снаряды.",
                "ПАССИВНО  /  СЛЕД 3 С", "SandboxAbilities/ghost_trail", new Color(.48f, 1f, .68f)));
            catalog.Add(new Definition(AbilitySandboxAbilityId.Gigantism, AbilitySandboxCategory.Active, "ПИЛОТ",
                "УВЕЛИЧЕНИЕ РАЗМЕРА", "ГИГАНТ", "Корабль вырастает в 2.3 раза. Широкий корпус расталкивает снаряды, затем сжимается.",
                "5 С  /  КД 7 С", "SandboxAbilities/gigantism", new Color(1f, .82f, .30f), 7f));
        }

        public void Open()
        {
            IsOpen = true;
            closeRequested = false;
            loadoutOpen = false;
            queuedActivation = null;
            activationTimer = 0f;
            sessionTime = 0f;
            bossVisible = true;
            readyAt.Clear();
            if (loadout.Count == 0) RestoreStarterLoadout();
            vfxEditor.Close();
        }

        public void Close()
        {
            IsOpen = false;
            closeRequested = false;
            loadoutOpen = false;
            queuedActivation = null;
            vfxEditor.Close();
        }

        public void Tick(float dt)
        {
            if (!IsOpen) return;
            sessionTime += dt;
            activationTimer = Mathf.Max(0f, activationTimer - dt);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (vfxEditor.IsOpen) vfxEditor.Close();
                else if (loadoutOpen) loadoutOpen = false;
                else closeRequested = true;
                return;
            }
            if (loadoutOpen || vfxEditor.IsOpen) return;
            // Number keys belong to active abilities, rather than their raw positions in
            // the loadout.  Adding a passive must never silently shift an active spell.
            var activeIndex = 0;
            for (var i = 0; i < loadout.Count; i++)
            {
                if (loadout[i].Category != AbilitySandboxCategory.Active) continue;
                if (activeIndex < SlotKeys.Length && Input.GetKeyDown(SlotKeys[activeIndex]))
                {
                    QueueActiveSlot(activeIndex);
                    return;
                }
                activeIndex++;
            }
        }

        public bool TryConsumeActivation(out AbilitySandboxAbilityId ability)
        {
            if (!queuedActivation.HasValue)
            {
                ability = default;
                return false;
            }
            ability = queuedActivation.Value;
            queuedActivation = null;
            return true;
        }

        // Keyboard and cards share the same active-only slot mapping, including slot 0 (index 9).
        public void QueueActiveSlot(int index)
        {
            if (!IsOpen || loadoutOpen || index < 0 || index >= SlotKeys.Length) return;
            var activeIndex = 0;
            for (var i = 0; i < loadout.Count; i++)
            {
                if (loadout[i].Category != AbilitySandboxCategory.Active) continue;
                if (activeIndex++ == index) { queuedActivation = loadout[i].Id; return; }
            }
        }

        public bool ConsumeCloseRequest()
        {
            if (!closeRequested) return false;
            closeRequested = false;
            return true;
        }

        public bool HasPassive(AbilitySandboxAbilityId ability)
        {
            for (var i = 0; i < loadout.Count; i++)
                if (loadout[i].Id == ability && loadout[i].Category == AbilitySandboxCategory.Passive) return true;
            return false;
        }

        public void NotifyActivated(AbilitySandboxAbilityId ability)
        {
            var definition = Find(ability);
            if (definition == null) return;
            activationLabel = definition.Name;
            activationColor = definition.Accent;
            activationTimer = 1.25f;
        }

        public void NotifyCooldown(AbilitySandboxAbilityId ability, float seconds)
        {
            var definition = Find(ability);
            if (definition == null) return;
            activationLabel = definition.ShortName + " // КД " + Mathf.CeilToInt(seconds) + " С";
            activationColor = new Color(.70f, .76f, .88f);
            activationTimer = .85f;
        }

        public float CooldownRemaining(AbilitySandboxAbilityId id)
        {
            return readyAt.TryGetValue(id, out var time) ? Mathf.Max(0f, time - sessionTime) : 0f;
        }

        public void StartCooldown(AbilitySandboxAbilityId id)
        {
            var definition = Find(id);
            if (definition != null) readyAt[id] = sessionTime + definition.Cooldown;
        }

        public void NotifyStatus(string label, Color color)
        {
            activationLabel = label;
            activationColor = color;
            activationTimer = 1.4f;
        }

        public void ToggleAbility(AbilitySandboxAbilityId id)
        {
            var definition = Find(id);
            if (definition != null) Toggle(definition);
        }

        public void Draw(float left, float top, float width, float height, int pixel, int smallPixel,
            Color pale, Color cyan)
        {
            if (!IsOpen) return;
            if (vfxEditor.IsOpen)
            {
                vfxEditor.Draw(left, top, width, height, pixel, smallPixel, pale, cyan, sessionTime, catalog);
                return;
            }
            DrawFlightHud(left, top, width, height, pixel, smallPixel, pale, cyan);
            if (loadoutOpen) DrawLoadoutEditor(left, top, width, height, pixel, smallPixel, pale, cyan);
        }

        private void DrawFlightHud(float left, float top, float width, float height, int pixel, int smallPixel,
            Color pale, Color cyan)
        {
            // The flight field stays completely live behind this compact control rail.  The
            // player can keep orbiting exactly as in a regular run and see real projectiles.
            var header = new Rect(left + width * .022f, top + height * (.145f - SandboxLift), width * .305f, height * .062f);
            var headerAccent = activationTimer > 0f ? activationColor : cyan;
            PixelUi.DrawPanel(header, new Color(.008f, .026f, .075f, .90f), headerAccent, 2f);
            PixelUi.DrawText(new Rect(header.x + 8f, header.y, header.width - 16f, header.height * .58f),
                activationTimer > 0f ? "ЗАПУСК // " + activationLabel : "ПЕСОЧНИЦА // ФЕНИКС",
                Mathf.Max(3, pixel - 1), activationTimer > 0f ? activationColor : Color.white, TextAnchor.MiddleLeft);
            PixelUi.DrawText(new Rect(header.x + 8f, header.y + header.height * .55f, header.width - 16f, header.height * .35f),
                "SPACE — ПЛАЗМА · A/D — ПОЛЁТ", Mathf.Max(3, smallPixel - 1), new Color(.58f, .80f, 1f), TextAnchor.MiddleLeft);

            var loadoutButton = new Rect(header.xMax + width * .010f, header.y, width * .105f, header.height);
            var vfxButton = new Rect(loadoutButton.xMax + width * .008f, header.y, width * .085f, header.height);
            var exitButton = new Rect(vfxButton.xMax + width * .008f, header.y, width * .070f, header.height);
            var bossButton = new Rect(left + width * BossControlLeft, header.y, width * BossControlWidth, header.height);
            DrawBossToggleControl(bossButton, smallPixel);
            DrawTimeScaleControl(new Rect(left + width * TimeControlLeft, header.y, width * TimeControlWidth, header.height), smallPixel, pale, cyan);
            DrawButton(loadoutButton, "НАБОР\n" + loadout.Count + "/" + MaxLoadoutSize, smallPixel, new Color(.03f, .14f, .22f, .98f), cyan, Color.white);
            DrawButton(vfxButton, "СЛОИ\nVFX", smallPixel, new Color(.13f, .06f, .22f, .98f), new Color(.76f, .48f, 1f), Color.white);
            DrawButton(exitButton, "ВЫХОД\nESC", smallPixel, new Color(.19f, .025f, .07f, .98f), new Color(1f, .34f, .46f), Color.white);
            if (!loadoutOpen)
            {
                if (GUI.Button(bossButton, GUIContent.none, GUIStyle.none)) bossVisible = !bossVisible;
                if (GUI.Button(loadoutButton, GUIContent.none, GUIStyle.none)) loadoutOpen = true;
                if (GUI.Button(vfxButton, GUIContent.none, GUIStyle.none))
                    vfxEditor.Open(catalog, vfxEditor.SelectedAbility);
                if (GUI.Button(exitButton, GUIContent.none, GUIStyle.none)) closeRequested = true;
            }

            DrawLoadoutSlots(left, top, width, height, smallPixel, pale);
        }

        private void DrawBossToggleControl(Rect control, int smallPixel)
        {
            var accent = bossVisible ? new Color(.36f, 1f, .70f) : new Color(1f, .34f, .46f);
            var fill = bossVisible ? new Color(.025f, .16f, .14f, .98f) : new Color(.19f, .025f, .07f, .98f);
            DrawButton(control, bossVisible ? "БОСС\nВКЛ" : "БОСС\nВЫКЛ", smallPixel, fill, accent, Color.white);
        }

        private void DrawLoadoutSlots(float left, float top, float width, float height, int smallPixel, Color pale)
        {
            var rail = new Rect(left + width * .018f, top + height * (.225f - SandboxLift), width * .285f, height * .485f);
            PixelUi.DrawPanel(rail, new Color(.006f, .015f, .045f, .92f), new Color(.25f, .42f, .68f, .82f), 3f);
            PixelUi.DrawText(new Rect(rail.x + 8f, rail.y + rail.height * .025f, rail.width - 16f, rail.height * .105f),
                "СПЕЛЛЫ // КЛИК ИЛИ 1-0", smallPixel, new Color(.60f, .80f, 1f), TextAnchor.MiddleLeft);
            PixelUi.DrawText(new Rect(rail.x + 8f, rail.y + rail.height * .105f, rail.width - 16f, rail.height * .070f),
                "ПАССИВКИ АКТИВНЫ СРАЗУ", Mathf.Max(3, smallPixel - 1), new Color(.64f, .72f, .90f), TextAnchor.MiddleLeft);
            var slotGapX = rail.width * .030f;
            var slotGapY = rail.height * .020f;
            var slotWidth = (rail.width - rail.width * .10f - slotGapX) * .5f;
            var slotHeight = (rail.height * .765f - slotGapY * 4f) / 5f;
            var activeIndex = 0;
            for (var i = 0; i < MaxLoadoutSize; i++)
            {
                var column = i / 5;
                var row = i % 5;
                var card = new Rect(rail.x + rail.width * .035f + column * (slotWidth + slotGapX),
                    rail.y + rail.height * .205f + row * (slotHeight + slotGapY), slotWidth, slotHeight);
                if (i >= loadout.Count)
                {
                    PixelUi.DrawPanel(card, new Color(.012f, .022f, .06f, .72f), new Color(.20f, .30f, .44f, .48f), 1f);
                    PixelUi.DrawText(card, "+", smallPixel, new Color(.32f, .46f, .64f));
                    continue;
                }
                var definition = loadout[i];
                var passive = definition.Category == AbilitySandboxCategory.Passive;
                var fill = new Color(definition.Accent.r * (passive ? .10f : .16f), definition.Accent.g * (passive ? .10f : .16f),
                    definition.Accent.b * (passive ? .10f : .16f), .98f);
                PixelUi.DrawPanel(card, fill, definition.Accent, 2f);
                var iconSize = Mathf.Min(card.height * .74f, card.width * .44f);
                var iconRect = new Rect(card.x + card.width * .07f, card.y + (card.height - iconSize) * .5f, iconSize, iconSize);
                DrawTexture(iconRect, IconTexture(definition), Color.white);
                var key = passive ? "ПАСС" : SlotLabel(activeIndex++);
                var cooldown = CooldownRemaining(definition.Id);
                if (!passive && cooldown > 0f) key += " / " + cooldown.ToString("0.0") + " С";
                var textX = iconRect.xMax + card.width * .055f;
                PixelUi.DrawText(new Rect(textX, card.y + card.height * .11f, card.xMax - textX - card.width * .05f, card.height * .32f),
                    key, smallPixel, definition.Accent, TextAnchor.MiddleLeft);
                PixelUi.DrawText(new Rect(textX, card.y + card.height * .47f, card.xMax - textX - card.width * .05f, card.height * .32f),
                    definition.ShortName, Mathf.Max(3, smallPixel - 1), pale, TextAnchor.MiddleLeft);
                if (!loadoutOpen && !passive && GUI.Button(card, GUIContent.none, GUIStyle.none)) QueueActiveSlot(activeIndex - 1);
            }
        }

        private void DrawTimeScaleControl(Rect control, int smallPixel, Color pale, Color cyan)
        {
            var accent = previewTimeScale < .35f ? new Color(.34f, .82f, 1f) :
                previewTimeScale > 1.35f ? new Color(1f, .72f, .30f) : cyan;
            PixelUi.DrawPanel(control, new Color(.008f, .026f, .075f, .92f), accent, 2f);
            PixelUi.DrawText(new Rect(control.x + 8f, control.y + control.height * .05f,
                    control.width * .50f, control.height * .42f), "ВРЕМЯ", Mathf.Max(3, smallPixel - 1), pale, TextAnchor.MiddleLeft);
            PixelUi.DrawText(new Rect(control.x + control.width * .47f, control.y + control.height * .05f,
                    control.width * .45f, control.height * .42f), previewTimeScale.ToString("0.00") + "x", smallPixel, accent, TextAnchor.MiddleRight);

            var track = new Rect(control.x + control.width * .07f, control.y + control.height * .67f,
                control.width * .86f, Mathf.Max(3f, control.height * .11f));
            PixelUi.DrawPanel(track, new Color(.02f, .05f, .10f, .96f), new Color(.20f, .36f, .58f), 1f);
            var slider = new Rect(control.x + control.width * .05f, control.y + control.height * .52f,
                control.width * .90f, control.height * .34f);
            previewTimeScale = Mathf.Clamp(GUI.HorizontalSlider(slider, previewTimeScale,
                PreviewTimeScaleMin, PreviewTimeScaleMax), PreviewTimeScaleMin, PreviewTimeScaleMax);
            var normalized = Mathf.InverseLerp(PreviewTimeScaleMin, PreviewTimeScaleMax, previewTimeScale);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(track.x + 2f, track.y + 2f,
                Mathf.Max(2f, (track.width - 4f) * normalized), Mathf.Max(1f, track.height - 4f)), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var knobX = track.x + track.width * normalized;
            PixelUi.DrawPanel(new Rect(knobX - 3f, track.y - 5f, 6f, track.height + 10f), accent, Color.white, 1f);
        }

        private void DrawLoadoutEditor(float left, float top, float width, float height, int pixel, int smallPixel, Color pale, Color cyan)
        {
            var shade = new Rect(left, top, width, height);
            PixelUi.DrawPanel(shade, new Color(0f, .005f, .022f, .68f), Color.clear, 0f);
            var modal = new Rect(left + width * .12f, top + height * .105f, width * .76f, height * .79f);
            PixelUi.DrawPanel(modal, new Color(.012f, .028f, .082f, .99f), new Color(.54f, .84f, 1f, .96f), 4f);
            PixelUi.DrawText(new Rect(modal.x + 16f, modal.y + modal.height * .035f, modal.width - 32f, modal.height * .09f),
                "НАБОР СПЕЛЛОВ  //  " + loadout.Count + "/" + MaxLoadoutSize, Mathf.RoundToInt(pixel * 1.08f), Color.white);
            PixelUi.DrawText(new Rect(modal.x + 16f, modal.y + modal.height * .125f, modal.width - 32f, modal.height * .045f),
                "КЛИК ПО КАРТОЧКЕ: ДОБАВИТЬ ИЛИ УБРАТЬ. ПАССИВКИ ЗАПУСКАЮТСЯ СРАЗУ.", smallPixel, new Color(.58f, .78f, .98f));

            var activeTab = new Rect(modal.x + modal.width * .08f, modal.y + modal.height * .205f, modal.width * .39f, modal.height * .075f);
            var passiveTab = new Rect(modal.x + modal.width * .53f, modal.y + modal.height * .205f, modal.width * .39f, modal.height * .075f);
            DrawButton(activeTab, "АКТИВНЫЕ", smallPixel,
                visibleCategory == AbilitySandboxCategory.Active ? new Color(.04f, .18f, .30f, 1f) : new Color(.02f, .05f, .12f, 1f),
                visibleCategory == AbilitySandboxCategory.Active ? cyan : new Color(.26f, .42f, .62f), Color.white);
            DrawButton(passiveTab, "ПАССИВНЫЕ", smallPixel,
                visibleCategory == AbilitySandboxCategory.Passive ? new Color(.16f, .08f, .20f, 1f) : new Color(.02f, .05f, .12f, 1f),
                visibleCategory == AbilitySandboxCategory.Passive ? new Color(.94f, .52f, 1f) : new Color(.26f, .42f, .62f), Color.white);
            if (GUI.Button(activeTab, GUIContent.none, GUIStyle.none)) { visibleCategory = AbilitySandboxCategory.Active; catalogPage = 0; }
            if (GUI.Button(passiveTab, GUIContent.none, GUIStyle.none)) { visibleCategory = AbilitySandboxCategory.Passive; catalogPage = 0; }

            var filtered = new List<Definition>(catalog.Count);
            for (var i = 0; i < catalog.Count; i++) if (catalog[i].Category == visibleCategory) filtered.Add(catalog[i]);
            const int pageSize = 5;
            var pageCount = Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)pageSize));
            catalogPage = Mathf.Clamp(catalogPage, 0, pageCount - 1);
            var firstItem = catalogPage * pageSize;
            var visibleCount = Mathf.Min(pageSize, filtered.Count - firstItem);
            var previousPage = new Rect(modal.x + modal.width * .08f, modal.y + modal.height * .285f, modal.width * .095f, modal.height * .052f);
            var nextPage = new Rect(modal.x + modal.width * .825f, modal.y + modal.height * .285f, modal.width * .095f, modal.height * .052f);
            PixelUi.DrawText(new Rect(modal.x + modal.width * .26f, previousPage.y, modal.width * .48f, previousPage.height),
                "СТРАНИЦА " + (catalogPage + 1) + "/" + pageCount, smallPixel, new Color(.60f, .80f, 1f));
            if (pageCount > 1)
            {
                DrawButton(previousPage, "‹", smallPixel, new Color(.02f, .07f, .16f, .98f), cyan, Color.white);
                DrawButton(nextPage, "›", smallPixel, new Color(.02f, .07f, .16f, .98f), cyan, Color.white);
                if (GUI.Button(previousPage, GUIContent.none, GUIStyle.none)) catalogPage = Mathf.Max(0, catalogPage - 1);
                if (GUI.Button(nextPage, GUIContent.none, GUIStyle.none)) catalogPage = Mathf.Min(pageCount - 1, catalogPage + 1);
            }
            var listTop = modal.y + modal.height * .35f;
            var cardGap = modal.height * .018f;
            var cardHeight = (modal.height * .47f - cardGap * (visibleCount - 1)) / Mathf.Max(1, visibleCount);
            for (var i = 0; i < visibleCount; i++)
            {
                var definition = filtered[firstItem + i];
                var installed = Contains(definition.Id);
                var card = new Rect(modal.x + modal.width * .08f, listTop + i * (cardHeight + cardGap), modal.width * .84f, cardHeight);
                var fill = installed ? new Color(definition.Accent.r * .14f, definition.Accent.g * .14f, definition.Accent.b * .14f, .98f)
                    : new Color(.018f, .032f, .078f, .98f);
                PixelUi.DrawPanel(card, fill, installed ? definition.Accent : new Color(.24f, .38f, .58f), 2f);
                var iconRect = new Rect(card.x + card.height * .12f, card.y + card.height * .12f, card.height * .76f, card.height * .76f);
                DrawTexture(iconRect, IconTexture(definition), Color.white);
                var textX = iconRect.xMax + card.width * .035f;
                PixelUi.DrawText(new Rect(textX, card.y + card.height * .10f, card.width * .48f, card.height * .28f),
                    definition.Source + " // " + definition.Name, smallPixel, definition.Accent, TextAnchor.MiddleLeft);
                PixelUi.DrawText(new Rect(textX, card.y + card.height * .37f, card.width * .54f, card.height * .37f),
                    definition.Description, smallPixel, pale, TextAnchor.MiddleLeft);
                PixelUi.DrawText(new Rect(textX, card.y + card.height * .77f, card.width * .54f, card.height * .17f),
                    definition.Timing, Mathf.Max(2, smallPixel - 1), definition.Accent, TextAnchor.MiddleLeft);
                var state = installed ? "В НАБОРЕ" : loadout.Count >= MaxLoadoutSize ? "ЛИМИТ" : "ДОБАВИТЬ";
                PixelUi.DrawText(new Rect(card.xMax - card.width * .19f, card.y + card.height * .14f, card.width * .15f, card.height * .70f),
                    state, smallPixel, installed ? new Color(.42f, 1f, .70f) : definition.Accent);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) Toggle(definition);
            }

            var done = new Rect(modal.x + modal.width * .30f, modal.y + modal.height * .86f, modal.width * .40f, modal.height * .08f);
            DrawButton(done, "ГОТОВО", smallPixel, new Color(.04f, .17f, .22f, .98f), new Color(.38f, 1f, .70f), Color.white);
            if (GUI.Button(done, GUIContent.none, GUIStyle.none)) loadoutOpen = false;
        }

        private void Toggle(Definition definition)
        {
            for (var i = loadout.Count - 1; i >= 0; i--)
                if (loadout[i].Id == definition.Id)
                {
                    loadout.RemoveAt(i);
                    return;
                }
            if (loadout.Count < MaxLoadoutSize) loadout.Add(definition);
        }

        private void RestoreStarterLoadout()
        {
            AddDefault(AbilitySandboxAbilityId.SolarChicks);
            AddDefault(AbilitySandboxAbilityId.AshenEgg);
            AddDefault(AbilitySandboxAbilityId.PhoenixDive);
            AddDefault(AbilitySandboxAbilityId.RiftEcho);
            AddDefault(AbilitySandboxAbilityId.VectorSnap);
            AddDefault(AbilitySandboxAbilityId.SolarPlume);
            AddDefault(AbilitySandboxAbilityId.AegisOrbit);
        }

        private void AddDefault(AbilitySandboxAbilityId id)
        {
            var definition = Find(id);
            if (definition != null && loadout.Count < MaxLoadoutSize) loadout.Add(definition);
        }

        private bool Contains(AbilitySandboxAbilityId id)
        {
            for (var i = 0; i < loadout.Count; i++) if (loadout[i].Id == id) return true;
            return false;
        }

        public Definition Find(AbilitySandboxAbilityId id)
        {
            for (var i = 0; i < catalog.Count; i++) if (catalog[i].Id == id) return catalog[i];
            return null;
        }

        private Texture2D IconTexture(Definition definition)
        {
            if(definition?.Asset?.Icon!=null)return definition.Asset.Icon.texture;
            if (definition == null || string.IsNullOrEmpty(definition.IconResource)) return null;
            if (definition.Id == AbilitySandboxAbilityId.BlackHole)
            {
                const string blackHoleIconKey = "__sandbox_black_hole_icon";
                if (iconCache.TryGetValue(blackHoleIconKey, out var blackHoleIcon)) return blackHoleIcon;
                blackHoleIcon = CreateBlackHoleIcon();
                iconCache[blackHoleIconKey] = blackHoleIcon;
                return blackHoleIcon;
            }
            if (definition.Id == AbilitySandboxAbilityId.AegisOrbit)
            {
                const string shieldIconKey = "__sandbox_star_shield_icon";
                if (iconCache.TryGetValue(shieldIconKey, out var shieldIcon)) return shieldIcon;
                shieldIcon = CreateStarShieldIcon();
                iconCache[shieldIconKey] = shieldIcon;
                return shieldIcon;
            }
            if (iconCache.TryGetValue(definition.IconResource, out var icon)) return icon;
            icon = Resources.Load<Texture2D>(definition.IconResource);
            if (icon == null && definition.IconResource.StartsWith("SandboxAbilities/", StringComparison.Ordinal))
                icon = SandboxMechanicIcons.Create(definition.Id, definition.Accent);
            iconCache[definition.IconResource] = icon;
            return icon;
        }

        private static Texture2D CreateBlackHoleIcon()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Black hole ability icon" };
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var u = (x + .5f) / size * 2f - 1f;
                var v = (y + .5f) / size * 2f - 1f;
                var radius = Mathf.Sqrt(u * u + v * v);
                var angle = Mathf.Atan2(v, u);
                var ring = Mathf.Clamp01(1f - Mathf.Abs(radius - .58f) * 6.2f);
                var spiral = Mathf.Clamp01(Mathf.Sin(angle * 3f + radius * 14f) * .8f + .26f) * Mathf.Clamp01(1f - radius);
                var core = Mathf.Clamp01(1f - radius * 4.4f);
                var alpha = Mathf.Clamp01(ring * .92f + spiral * .66f + core);
                texture.SetPixel(x, y, Color.Lerp(new Color(.04f, .01f, .12f, alpha), new Color(.38f, .92f, 1f, alpha), ring * .62f + spiral * .38f));
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateStarShieldIcon()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Star shield ability icon" };
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var u = (x + .5f) / size * 2f - 1f;
                var v = (y + .5f) / size * 2f - 1f;
                var radius = Mathf.Sqrt(u * u + v * v);
                var angle = Mathf.Atan2(v, u);
                var rays = Mathf.Pow(Mathf.Clamp01(Mathf.Abs(Mathf.Cos(angle * 4f))), 8f);
                var core = Mathf.Clamp01(1f - radius * 3.4f);
                var halo = Mathf.Clamp01(1f - Mathf.Abs(radius - .58f) * 6.5f);
                var alpha = Mathf.Clamp01(core + halo * .72f + rays * Mathf.Clamp01(1f - radius) * .34f);
                var color = Color.Lerp(new Color(.28f, .06f, .52f, alpha), new Color(.92f, .66f, 1f, alpha), core * .85f + halo * .15f);
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            return texture;
        }

        private static void DrawButton(Rect rect, string label, int smallPixel, Color fill, Color border, Color text)
        {
            var hovered = rect.Contains(Event.current.mousePosition);
            PixelUi.DrawPanel(rect, hovered ? Color.Lerp(fill, Color.white, .10f) : fill, hovered ? Color.white : border, 2f);
            PixelUi.DrawText(rect, label, smallPixel, text);
        }

        private static string SlotLabel(int index)
        {
            return index == 9 ? "0" : (index + 1).ToString();
        }

        private static void DrawSprite(Rect rect, Sprite sprite, Color color)
        {
            DrawTexture(rect, sprite != null ? sprite.texture : null, color);
        }

        private static void DrawTexture(Rect rect, Texture2D texture, Color color)
        {
            if (texture == null || Event.current.type != EventType.Repaint) return;
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }
    }
}
