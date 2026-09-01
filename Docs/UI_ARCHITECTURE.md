# UI Orbital Rift: Canvas и редактирование как LEGO

## Что уже перенесено

Solo Expedition HUD больше не рисуется внутри большого `GameManager.OnGUI()`. Он собран обычными объектами Unity uGUI
под адаптивным Canvas. `GameManager` передаёт только модель состояния, а `ExpeditionHudView` отвечает только за внешний
вид. Магазин, результаты и остальные режимы пока продолжают работать через старый renderer и переносятся отдельными
этапами, чтобы не сломать готовую игру одним большим изменением.

## Где искать объекты

Откройте `Assets/Scenes/Boot.unity` и раскройте:

```text
Orbital Rift Bootstrap
└── UI Root [Canvas]
    ├── Safe Area
    │   ├── 01 Main Menu [migration pending]
    │   ├── 02 Settings [migration pending]
    │   ├── 03 Coop Lobby [migration pending]
    │   ├── 04 Classic HUD [migration pending]
    │   ├── 05 Expedition HUD
    │   │   ├── 01 Header
    │   │   ├── 02 Sector Map
    │   │   ├── 03 Objective
    │   │   ├── 04 Event Ticker
    │   │   ├── 05 Trajectory
    │   │   ├── Pause Button
    │   │   ├── 06 Threat Bar
    │   │   ├── 07 Hull Bar
    │   │   └── 08 Room Intro
    │   └── 90 Modal Layer [migration pending]
    └── Event System
```

Вне Play Mode отображается тестовый Expedition HUD. Поэтому расположение элементов видно сразу, без запуска игры.

## Как самостоятельно двигать элементы

1. Остановите Play Mode.
2. Выберите нужный объект внутри `05 Expedition HUD`.
3. Включите инструмент Rect Tool клавишей `T`.
4. Меняйте якоря и границы `RectTransform` в Scene View или Inspector.
5. Проверяйте одновременно портретный Game View и широкий PC Game View.

Положение хранится в сцене. Скрипт создаёт только отсутствующие объекты и не сбрасывает уже существующие
`RectTransform`, поэтому ручная раскладка сохраняется.

Цвета темы меняются на компоненте `Expedition Hud View` объекта `05 Expedition HUD`: `Panel Color`, `Cyan`, `Pale` и
`Empty Segment`. Тексты в дочерних объектах служат только preview: во время игры их содержимое обновляет модель.

## Почему интерфейс не вылезает за экран

- `Canvas Scaler`: `Scale With Screen Size`.
- Базовое разрешение: `1080 × 1920`.
- `Match Width Or Height`: `0.5`.
- `SafeAreaFitter` переводит `Screen.safeArea` в anchors и защищает интерфейс от вырезов, скруглений и Android navigation bar.
- Все элементы Expedition HUD используют относительные anchors, а не пиксельные координаты конкретного телефона.

## Разделение кода

- `OrbitalRiftCanvasRoot` — создаёт Canvas, Safe Area и контейнеры экранов.
- `UiScreenManager` — включает только активный экран.
- `ExpeditionHudModel` — простые данные: подписи, здоровье, цвета, состояние карты и интро.
- `ExpeditionHudView` — отображает модель и обрабатывает только кнопку паузы.
- `GameManager.BuildExpeditionHudModel()` — переводит игровое состояние в UI-модель.

View не выдаёт урон, не создаёт врагов и не меняет комнату. Gameplay-код не знает координаты текста и полосок. Это
главное правило, благодаря которому дизайн можно менять независимо от механик.

## План полного переноса

1. **Готово:** общий Canvas, Safe Area, Screen Manager, редакторский preview и Expedition combat HUD.
2. Перенести магазин экспедиции и экран стыковки в `ExpeditionShopView`.
3. Перенести общее pause modal в `PauseView` и использовать его во всех режимах.
4. Перенести стартовое меню и таблицы лидеров в `MainMenuView`.
5. Перенести настройки в `SettingsView`.
6. Перенести Classic HUD и Defense HUD.
7. Перенести кооперативное лобби и сетевой HUD.
8. Перенести результаты/MMR и справочники комнат/рангов.
9. После визуальной сверки удалить оставшийся IMGUI-код из `GameManager.OnGUI()`.

## Восстановление структуры

Если случайно удалён обязательный объект, используйте меню:

`Tools → Orbital Rift → UI → Install or Repair Editable Canvas`

Инструмент добавляет только недостающие элементы и сохраняет `Boot.unity`.
