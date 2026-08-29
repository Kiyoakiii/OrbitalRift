# Orbital Rift

Оригинальная мобильная 2D-аркада для Android, созданная по ТЗ. Проект не использует материалы или название GTA.

## Требования для коллеги

- Windows 10/11.
- Unity **6000.3.22f1** (Unity 6; revision `1c726e1fb402`). Используйте именно эту версию, указанную в `ProjectSettings/ProjectVersion.txt`.
- В Unity Hub при установке этой версии включите модули **Android Build Support**, **Android SDK & NDK Tools** и **OpenJDK**.
- Проект рассчитан на Android с минимальным API 25 и архитектурой ARM64 (`arm64-v8a`).
- Firebase Unity SDK **13.15.0** и External Dependency Manager уже подключены локальными UPM-пакетами из `GooglePackages`.

## Открытие

1. Склонируйте репозиторий и добавьте папку `OrbitalRift` в Unity Hub.
2. Выберите Unity **6000.3.22f1**.
3. Откройте `Assets/Scenes/Boot.unity` и нажмите Play.

Стартовая сцена уже добавлена. При запуске игра сама создаёт меню, UI, арену, эффекты и игровую логику.

## Управление

- Android/сенсорный экран: удерживайте левую половину экрана — корабль движется по часовой стрелке; удерживайте правую половину — против часовой. Отпустите палец, чтобы остановиться. В первые секунды раунда подсказки зон показываются прямо внизу экрана.
- ПК: зажмите левую/правую половину окна мышью или используйте A/D и стрелки. Space переключает автоогонь.

## Android build

В Build Settings выберите Android, затем включите IL2CPP и ARM64 в Player Settings. Для локальной проверки соберите APK; для Play Console — AAB. Идентификатор приложения уже задан: `com.orbitalrift.studio`.

Перед сборкой можно запустить `Orbital Rift -> Validate Product Readiness`. Проверка сразу сообщает о неверной сцене, package id, IL2CPP, ARM64, минимальном API, Firebase-конфигурации и отсутствующем Android launcher Activity.

В Unity можно собрать debug APK через меню `Orbital Rift -> Build Android Debug APK`. Файл появится в `Builds/OrbitalRift-debug.apk`.

Для Google Play настройте собственный keystore в Player Settings и выберите `Orbital Rift -> Build Android Release AAB`. Валидатор не позволит случайно собрать неподписанный production-файл. Результат появится в `Builds/OrbitalRift-release.aab`. Пароли keystore нельзя добавлять в Git.

Полный порядок проверок перед публикацией находится в `RELEASE_CHECKLIST.md`.

Командная строка для автоматической сборки:

```powershell
& "<путь-к-Unity>\Editor\Unity.exe" -batchmode -nographics -quit `
  -projectPath "<путь-к-проекту>" `
  -executeMethod OrbitalRift.BuildAndroid.BuildDebugApk
```

Для release AAB замените последний метод на:

```powershell
-executeMethod OrbitalRift.BuildAndroid.BuildReleaseAab
```

Пример для стандартного расположения на диске A:

```powershell
& "A:\GameDev\Unity\Editors\6000.3.22f1\Editor\Unity.exe" -batchmode -nographics -quit `
  -projectPath "A:\GameDev\Projects\OrbitalRift" `
  -executeMethod OrbitalRift.BuildAndroid.BuildDebugApk
```

Папки `Library`, `Temp`, `Logs`, `UserSettings` и `Builds` не хранятся в Git: Unity создаёт их заново, а APK собирается локально.

После сборки можно проверить, что Android видит точку входа приложения:

```powershell
& "<путь-к-Android-SDK>\build-tools\<версия>\aapt.exe" dump badging `
  "Builds\OrbitalRift-debug.apk" | Select-String "launchable-activity"
```

В выводе должен быть `com.unity3d.player.UnityPlayerActivity`. Скрипт сборки и валидатор проекта настраивают и проверяют это автоматически.

## Содержимое

- Обычные противники и полноценный босс третьей фазы с несколькими состояниями ИИ.
- Система фаз, ядер перехода, временный Triple Shot и постоянные улучшения корабля.
- Редкие фиолетовые звёзды превращаются в вращающийся solid-щит; белые звёзды остаются декоративным потоком.
- Пули, враги, взрывы, pickup-объекты и звёздный поток используют пул объектов.
- Рекорд, MMR, позывной и настройки звука сохраняются через PlayerPrefs.
- Ранги идут ступенями по 1000 MMR; изменение за забег ограничено диапазоном от `-150` до `+150` и не может увести рейтинг ниже нуля.
- Firebase Anonymous Auth и Firestore синхронизируют общий TOP RECORD и TOP MMR; меню честно показывает `SYNC`, `ONLINE` или `OFFLINE`.
- Личный рекорд и MMR загружаются после авторизации, а незагруженный результат хранится локально до подтверждённой записи Firestore.
- Правила и production-чеклист Firebase находятся в `firebase/firestore.rules` и `FIREBASE_PRODUCTION.md`.

## Мобильное управление

- Удержание левой половины экрана: движение по часовой стрелке.
- Удержание правой половины: движение против часовой стрелки.
- Активная сенсорная половина подсвечивается.
- Кнопка `II` ставит забег на паузу; Android Back/Escape переключает паузу.
- Виброотдачу можно выключить в стартовом меню.
- Тряску камеры можно отдельно выключить в настройках для комфортной игры.

Мобильная аркадная игра с одной орбитой вокруг ядра.

## Где менять параметры

- `Assets/Scripts/OrbitSettings.cs` — радиус и диаметр орбиты, цвет, толщину и тип линии (`Solid`, `Dashed`, `Dotted`).
- `Assets/Scripts/BonusSettings.cs` — скорость бонуса, мягкое наведение к кораблю, время жизни и вращение.
- `Assets/Scripts/StarStreamSettings.cs` — количество, размер, яркость, скорость и длина затухания следа звёзд.
- `Assets/Scripts/BossSettings.cs` — здоровье, размер, интервалы атак и скорость снарядов босса.
- `Assets/Scripts/MmrSettings.cs` — начальный рейтинг, границы рангов и формула изменения MMR.
- `Assets/Scripts/GameAudioSettings.cs` — сохраняемые настройки музыки и эффектов.

Корабль загружается из `Assets/Resources/ship.png`, снаряд — из `Assets/Resources/projectile.png`, бонус — из `Assets/Resources/bonus_pickup.png`. Если `projectile.png` ещё не создан, временно используется простой прямоугольник.
