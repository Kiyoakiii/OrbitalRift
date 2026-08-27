# Orbital Rift

Оригинальная мобильная 2D-аркада для Android, созданная по ТЗ. Проект не использует материалы или название GTA.

## Требования для коллеги

- Windows 10/11.
- Unity **6000.3.22f1** (Unity 6; revision `1c726e1fb402`). Используйте именно эту версию, указанную в `ProjectSettings/ProjectVersion.txt`.
- В Unity Hub при установке этой версии включите модули **Android Build Support**, **Android SDK & NDK Tools** и **OpenJDK**.
- Проект рассчитан на Android с минимальным API 25 и архитектурой ARM64 (`arm64-v8a`).

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

В Unity можно собрать текущий debug APK через меню `Orbital Rift -> Build Android Debug APK`. Файл появится в `Builds/OrbitalRift-debug.apk`. Это тот же способ, которым собран последний APK. Для публикации в магазине нужно отдельно настроить подпись (keystore) и собрать AAB.

Командная строка для автоматической сборки:

```powershell
& "<путь-к-Unity>\Editor\Unity.exe" -batchmode -nographics -quit `
  -projectPath "<путь-к-проекту>" `
  -executeMethod OrbitalRift.BuildAndroid.BuildDebugApk
```

Пример для стандартного расположения на диске A:

```powershell
& "A:\GameDev\Unity\Editors\6000.3.22f1\Editor\Unity.exe" -batchmode -nographics -quit `
  -projectPath "A:\GameDev\Projects\OrbitalRift" `
  -executeMethod OrbitalRift.BuildAndroid.BuildDebugApk
```

Папки `Library`, `Temp`, `Logs`, `UserSettings` и `Builds` не хранятся в Git: Unity создаёт их заново, а APK собирается локально.

## Содержимое

- Четыре типа врагов и бесконечная кривая сложности.
- Система фаз, ядер перехода и Split Shot.
- Пули, враги, взрывы, pickup-объекты и звёздный поток используют пул объектов.
- Рекорд и звук сохраняются через PlayerPrefs.

Мобильная аркадная игра с одной орбитой вокруг ядра.

## Где менять параметры

- `Assets/Scripts/OrbitSettings.cs` — радиус и диаметр орбиты, цвет, толщину и тип линии (`Solid`, `Dashed`, `Dotted`).
- `Assets/Scripts/BonusSettings.cs` — скорость бонуса, мягкое наведение к кораблю, время жизни и вращение.
- `Assets/Scripts/StarStreamSettings.cs` — количество, размер, яркость, скорость и длина затухания следа звёзд.

Корабль загружается из `Assets/Resources/ship.png`, бонус — из `Assets/Resources/bonus_pickup.png`.
