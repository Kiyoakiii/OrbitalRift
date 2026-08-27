# Firebase и общий рейтинг Orbital Rift

## Что уже настроено

Проект Unity: **6000.3.22f1 (Unity 6.3 LTS)**. Android package name: `com.orbitalrift.studio`.

Firebase-проект `Orbital Rift` уже содержит Android-приложение с этим package name, а файл конфигурации лежит в `Assets/google-services.json`.

В игре реализовано следующее:

- игрок вводит позывной перед стартом;
- Firebase создаёт анонимную учётную запись устройства — без регистрации и пароля;
- лучший результат сохраняется локально сразу и синхронизируется, когда есть интернет;
- на стартовом экране показываются пять лучших результатов с других устройств;
- игра остаётся полностью играбельной офлайн: сетевые ошибки не блокируют меню и полёт.

Используется Firebase Unity SDK **13.15.0**. Его пакеты уже лежат в `GooglePackages/` и подключены в `Packages/manifest.json`, поэтому не нужно импортировать `.unitypackage` вручную или смешивать способы установки.

## Как открыть проект у коллеги

1. Установить Unity Hub и редактор **Unity 6000.3.22f1** с модулями **Android Build Support**, **Android SDK & NDK Tools** и **OpenJDK**.
2. Клонировать репозиторий целиком, включая папку `GooglePackages/`.
3. Открыть корневую папку проекта через Unity Hub.
4. Подождать, пока Unity один раз скачает зависимости и завершит импорт.
5. Открыть сцену `Assets/Scenes/Boot.unity` и нажать Play.

При первом открытии Unity видит локальные UPM-пакеты Firebase, создаёт `.meta`-файлы и обновляет `packages-lock.json`. Если Unity покажет окно о перезапуске редактора для импорта пакетов, согласиться.

## Как это устроено в коде

`OrbitalRiftBootstrap` создаёт `FirebaseScoreService` раньше игрового менеджера.

`FirebaseScoreService`:

1. Проверяет зависимости Firebase.
2. Выполняет Anonymous Authentication.
3. Загружает личный лучший результат и публичный топ-5.
4. Записывает лучший результат игрока в Firestore после окончания игры.

`GameManager` хранит позывной в `PlayerPrefs`, требует его перед стартом и обновляет экран рейтинга после ответа Firebase.

Главные файлы:

- `Assets/Scripts/FirebaseScoreService.cs` — Firebase/Auth/Firestore;
- `Assets/Scripts/GameManager.cs` — экран позывного, локальный рекорд и UI топа;
- `Assets/Scripts/OrbitalRiftBootstrap.cs` — ранний запуск сервиса;
- `Assets/google-services.json` — конфигурация Android-приложения.

## Данные Firestore

Коллекция: `leaderboard`. Идентификатор документа — анонимный Firebase UID конкретного устройства.

```text
leaderboard/{uid}
  nickname: string (1–16 символов)
  score: number
  updatedAt: server timestamp
```

В публичный рейтинг попадают только позывной и лучший счёт. UID не выводится в игре.

## Правила Cloud Firestore

В Firebase Console откройте **Firestore Database → Rules** и используйте правила из файла `Docs/firestore.rules`. Они дают всем устройствам чтение топа, но позволяют записать только собственный документ после анонимной авторизации и проверяют формат полей.

Важно: это клиентский рейтинг. Пользователь с модифицированной сборкой теоретически может подделать счёт. Для соревновательного рейтинга до релиза подключите Cloud Functions или свой сервер: клиент отправляет результат, а сервер валидирует раунд и только затем публикует рекорд.

## Проверка на Android

1. На устройстве включить интернет, запустить игру, задать позывной и закончить раунд.
2. В Firebase Console открыть **Authentication → Users**: должен появиться анонимный пользователь.
3. В **Firestore Database → Data** появится `leaderboard/{uid}`.
4. Запустить APK на втором устройстве с другим позывным: после возврата в меню оба результата будут видны в топе.
5. Отключить интернет и запустить игру ещё раз: игра не должна зависать, а локальный рекорд остаётся доступен.

## Если Firebase не подключился

- Убедиться, что `Assets/google-services.json` существует и соответствует `com.orbitalrift.studio`.
- В Unity открыть **Assets → External Dependency Manager → Android Resolver → Force Resolve**.
- Если Android-сборка сообщит об ошибке DEX/методов, включить minification в **Project Settings → Player → Android → Publishing Settings** и собрать ещё раз.
- Не добавлять второй Firebase SDK через `.unitypackage`: проект уже использует UPM `.tgz`-пакеты.

Официальные материалы: [настройка Firebase для Unity](https://firebase.google.com/docs/unity/setup), [анонимная авторизация](https://firebase.google.com/docs/auth/unity/anonymous-auth), [Cloud Firestore в Unity](https://firebase.google.com/docs/firestore/quickstart).
