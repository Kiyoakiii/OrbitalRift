# Phase 1 — аудит до интеграции, 12 сентября 2026

- Сцена `Assets/Scenes/Boot.unity`. `OrbitalRiftBootstrap` создаёт/находит GameManager.
- Sandbox — `AbilitySandboxSession`, методы `OpenAbilitySandbox`, `UpdateAbilitySandbox`
  в GameManager; `GameManager.SandboxMechanics` и `SandboxVfxLayerEditor` управляют spell VFX.
- `PositionOnOrbit` использует `playerAngle` и `OrbitSettings.Radius = 3.2`, центр (0,0).
  Команды приходят из `PlayerCommandSource` через старый Unity Input. Управление не меняется.
- `CreateSpaceBackdrop/UpdateBackgroundStars`: 560 SpriteRenderer, массивы скоростей,
  размеров, фаз. `UpdateStars`, `StarStreamSettings`, `StarParticle`: поток с TrailRenderer,
  существующий ObjectPool. Белые звёзды декоративные; фиолетовые — игровые pickups/щиты.
- `UpdateSpaceTravel` уже связывает крейсерскую скорость, бой и переход между фазами.
  Новый контроллер получает этот коэффициент; музыкальный визуализатор получает прежнее значение.
- `SpaceVisualProfile` хранит только цвет/облачность музыкальных регионов, не 12 depth layers.
  Его не заменяем новым профилем: это другая ответственность.
- Built-in pipeline: `GraphicsSettings.m_CustomRenderPipeline = 0`, URP/HDRP отсутствуют
  в manifest. Есть модуль ParticleSystem, нет Addressables и нового Input System.
- Gameplay: SpriteRenderer, TrailRenderer, LineRenderer, MeshRenderer и ParticleSystem.
  Общие spell-пулы и ScriptableObject уже существуют; их не включаем в слой космоса.
- `MusicReactiveVisualDirector` и `MusicSpaceDistortion` — кольца, музыкальный фон и
  существующая обработка изображения. Они сохраняются без изменений.
- Старые дальние звёзды имеют sortingOrder -5, чёрный фон -100. Новые слои получают
  упорядоченные значения -95…-40, оставаясь позади gameplay. HUD не переподчиняется.
- Готовых отдельных planet/galaxy/asteroid assets не найдено. Используем различимые
  процедурные шейдерные placeholders, без внешнего сервиса и генерации изображений.

## Минимальная архитектура

SpaceDepthProfile + общие SpaceLayerSettings → SpaceFlightVisualController →
один SpaceDepthLayer с переиспользуемым буфером четырёхугольников для каждого слоя.
Крупные объекты не имеют физики. Буфер хранит положение и вращение независимо от звёзд.
Вместо сотен объектов — один MeshRenderer на слой. Это пакетная альтернатива ParticleSystem.
Компактная IMGUI-панель расширяет существующую Sandbox.

## Риски и границы

Нельзя отключить все StarParticle: потеряются фиолетовые щиты. Убирается только белая
декоративная генерация при наличии нового профиля. Без профиля остаётся старый путь.
Нельзя менять скорость music rings или выдавать их за новый speed effect: слот 10
внешний и не управляется профилем согласно прямому уточнению пользователя.
Изоляция spell workshop продолжает использовать существующее скрытие spaceBackdrop.
Сцена с большим количеством незакоммиченных правок не перезаписывается.
