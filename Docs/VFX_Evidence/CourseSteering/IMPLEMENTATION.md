# Phase 2 — что изменилось

Изменение продолжает существующую Space Depth. Музыкальные кольца остаются
внешними. Гравитационная следующая фаза не начата.

## Ответы на пункты ТЗ

| № | Вопрос | Реализация |
|---|---|---|
| 1 | Изменённые scripts Phase 1 | SpaceDepthProfile, SpaceFlightVisualController, SpaceDepthLayer, SpaceLayerDebugPanel; адаптер GameManager.SpaceDepth |
| 2 | Новые scripts | CourseController, SpaceLayerVisualBinding, Editor/CourseSteeringValidation; новый SpaceDepthImage.shader |
| 3 | Где CourseController | Assets/Resources/SpaceDepth/Scripts/CourseController.cs |
| 4 | Где настройки | SpaceDepthProfile.asset → Steering; отдельные слои → Layers |
| 5 | CourseCenter | Ввод интегрирует Target; Current плавно следует за ним; Neutral = центр экрана; ограничение эллипсом |
| 6 | OrbitCenter | Мировое смещение курса × OrbitCenterFollowStrength; прибавляется к прежней окружности корабля |
| 7 | MaxCourseOffset | Steering → Max Course Offset X/Y; фактические границы учитывают радиус и Safe Screen Margin |
| 8 | Скорость steering | Course Move Speed; Max Course Center Speed ограничивает скорость Current |
| 9 | Smoothing | Course Smooth Time; для слоёв отдельно Steering Response Speed |
| 10 | Общая сила | Global Steering Strength |
| 11 | Сила слоя | Layers → нужный слой → Steering Influence |
| 12 | Sandbox controls | SPACE DEPTH внизу справа → КУРС; 3 × 3 пресета, Center и автотесты |
| 13 | Debug centers | Маркеры центра и слоёв, Editor / Development Build |
| 14 | EffectiveVP | Цветные номера слоёв и линии; CurrentEffectiveVanishingPoint доступен также в API слоя |
| 15 | Phase 1 mode | Выключить Steering Enabled; нейтральный центр и прежние радиальные траектории |
| 16 | Оптимизации | Общий mesh на слой, повторное использование элементов, фиксированные буферы и история хвоста, кэш материалов |
| 17 | Ограничения | Классический solo и Sandbox; Android GPU и физический gamepad не проверены; Full Rect sprites без вращения упаковки |
| 18 | Инструкция | Assets/Resources/SpaceDepth/Documentation/COURSE_STEERING_GUIDE.md |

## Дополнительные интеграции

- PlayerCommandSource / PlayerCommandFrame: отдельный вектор курса, настраиваемые
  клавиши I/J/K/L; старые конструкторы command frame сохранены.
- ProjectSettings/InputManager.asset: CourseHorizontal, CourseVertical,
  OrbitKeyboard, OrbitHorizontal. При выключенном steering и в остальных режимах
  сохраняется прежняя ось орбиты.
- GameManager.cs: порядок чтения ввода, смещённая орбита, нос и стрельба; тот же центр
  используется Rift Echo, собираемым ядром и меткой существующих корней.
- VoidMawBossPresentation.cs: необязательный orbitCenter только для позиции метки
  корней, совпадающей с её gameplay-проверкой. Это не добавление гравитации.
- SpaceDepthValidation.cs: прежняя регрессия запускается с Steering Enabled=false.

## Ручные изображения

У каждого управляемого слоя появились Appearance (Procedural / Image), Material,
Texture, Sprite, Preserve Image Aspect. Sprite имеет приоритет, материал необязателен.
Источник материала клонируется; изменения исходного .mat можно перечитать кнопкой
**Обновить материалы**. Всё сохраняется прежней кнопкой **Сохранить**.

Поля видны в Inspector профиля и в панели Sandbox внутри Unity Editor. В сборке
используются сохранённые ссылки на assets. Выбор материалов проекта недоступен
в standalone, где нет Unity Asset Database.

Подробности: Assets/Resources/SpaceDepth/Documentation/ARTWORK_GUIDE.md.

## Проверяемые свидетельства

- flow.txt и flow-*.png — промежуточная проверка без корабля, крупных слоёв и музыки.
- full.txt — курс, границы, движение, игровые попадания, щит, ядро, пауза и CPU/GC.
- ui.txt / sandbox-artwork-panel.png — реальные поля назначения assets без
  предупреждений о стилях Editor.
- production-hud.png — текущая игровая камера с интерфейсом.
- custom-image-layer.png — настоящий PNG корабля вместо процедурной планеты,
  назначенный только временной runtime-копии профиля во время QA.
- motion/frame-*.png / course-turn.mp4 — последовательность поворотов потока
  вправо и влево. MP4 собран из кадров Unity без дополнительного bloom/коррекции.
- ../SpaceDepth/stage-3.txt — регрессия Phase 1: слои, Solo, скорости, плотность,
  параллакс, игровой запуск и отсутствие ошибок.
- Baseline/music-hashes.json — контрольные SHA-256 двух файлов музыкальной системы
  до работы. Сравнение после работы подтвердило отсутствие изменений.

Это проверка Play Mode текущего Unity Editor, а не тест Android-сборки. CPU измерен
отдельно для контроллера; он не включает GPU-прозрачность, OnGUI и музыкальные эффекты.
Кадры с изолированной камерой не содержат музыку; кадры Game View сохраняют её.

## Результат проверки 12 сентября 2026

Полная проверка Phase 2: PASS. Девять направлений, переходы и случайные резкие
развороты, 0 / 0.08 / 3.2 / 9 / 30 travel speed, четыре соотношения сторон
(9:16, 1:1, 16:9, 32:9), согласованность отклика на 30 и 120 FPS. В игровом режиме
проверены орбита, ориентация корабля, выстрелы, урон врагу и кораблю, перехват щитом,
позиция ядра и пауза. Материалы и Sprite/Texture проверены на реальном рендерере.

| Нагрузка | Элементов | CPU курса + слоёв, среднее | Managed GC за 240 обновлений |
|---|---:|---:|---:|
| Обычный профиль | 470 | 0.475 мс | 0 байт |
| Плотность ×3 | 1410 | 1.407 мс | 0 байт |

Панель материалов: PASS после устранения несовместимости стилей Editor с GameSkin.
Поля Material / Texture / Sprite теперь имеют обычную высоту строки и видимую кнопку
выбора; кадр sandbox-artwork-panel.png проверен визуально.

Регрессия Phase 1 с явным SteeringEnabled=false: PASS, отчёт stage-3.txt от 18:47.
CPU слоёв в этом режиме: 0.315 мс при 470 элементах, 0.908 мс при 1410; GC = 0.
Whole-frame значения Editor зависят от планировщика и не используются для оценки
стоимости GPU. В production-impact белый квадрат — временный контрольный враг QA.
Исходные музыкальные файлы подтверждены также в music-verified.json.
