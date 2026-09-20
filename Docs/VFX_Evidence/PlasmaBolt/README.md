# Plasma Bolt — проверка 11 сентября 2026

Проверено в открытом проекте OrbitalRift, Unity 6000.3.22f1,
Built-in Render Pipeline, Windows Editor, DX12, активная платформа проекта Android.
Сборка APK и профилирование на телефоне не выполнялись.

## Результат

Unity скомпилировал runtime и Editor scripts; ошибок C# и ошибок шейдера Plasma Bolt нет.
Проверка `PlasmaBoltValidation.ValidateAssets` проверила обе prefab-иерархии,
missing scripts, ссылки слоёв, профиль, impact, материалы, shader и маски.

[validation.txt](validation.txt): 18 успешных проверок в Play Mode.

- Обе системы частиц действительно испускают частицы.
- Выстрел игрока вызывает отдельный impact в Sandbox, здоровье/счёт не меняются.
- Семь одновременных выстрелов дают семь impacts.
- Следы и aftermath завершаются и возвращаются в существующие ObjectPool.
- Проверена работа production `UpdateProjectiles` → `HitEnemies`: урон сохраняется,
  impact создаётся один раз. В этой части теста менеджер временно отключён, методы
  настоящего GameManager вызываются с контролируемым dt; это не полный игровой забег.
- Быстрый отрезок попадания даёт точку на границе цели, а не за ней.
- Истечение Life не создаёт impact.
- Cleanup очищает оба пула визуалов.
- Три повторных использования не сохраняют старые частицы/следы и возвращают
  исходный SpriteRenderer игровому projectile.

## Визуальные кадры

Кадры получены `Camera.Render` из настоящей Sandbox на игровом фоне, 1280×720.
Это рендер мира без экранного IMGUI-интерфейса; увеличений яркости и ретуши нет.

- [01-stationary.png](01-stationary.png): неподвижный корабль и автоогонь.
- [02-moving-ship.png](02-moving-ship.png): стрельба при движении корабля.
- [03-camera-offset.png](03-camera-offset.png): кадр после движения камеры за 12 кадров.
- [04-seven-projectiles.png](04-seven-projectiles.png): семь выстрелов с разных сторон.
- [05-overlapping-impacts.png](05-overlapping-impacts.png): одновременные попадания.
- [06-gameplay-impact.png](06-gameplay-impact.png): production-попадание с уроном.

Визуально проверены форма ядра, направление следов и читаемость манекена при
перекрывающихся попаданиях. Маленькие детали намеренно слабее ядра.

## Существующие общие тесты

Отдельно запущен `Orbital Rift/Run Gameplay Rules Tests`. Он сообщил два нарушения:

1. `Boss archetypes must rotate Void Maw, Astral Firebird, Umbral Harrier.`
2. `The normal boss phase must select the first configured boss.`

В существующем `BossArchetypeSettings` порядок сейчас AstralFirebird → UmbralHarrier →
VoidMaw и `DebugBossTestMode = true`. Валидатор ожидает порядок с VoidMaw первым
и обычный режим фаз. Эти файлы/правила не изменялись в рамках Plasma Bolt.
Это ошибки assertions общего набора, а не ошибки компиляции VFX.

## Границы проверки

Не проверены APK, мобильный GPU, сетевой матч двух устройств и подключение Plasma
impacts к отдельным сетевым событиям. Обычные friendly-выстрелы через `Shoot`,
классические `HitEnemies` и Sandbox интегрированы. Новый универсальный VFX-редактор
не добавлялся. Существующий редактор слоёв сохранён.

Повторить: выйти из Play Mode → Orbital Rift → Spells →
Validate Plasma Bolt in Sandbox. Проверка заканчивается открытой Sandbox с автоогнём.
