# Firebase для Orbital Rift (Unity / Android)

Этот проект сейчас не содержит Firebase SDK. Подключение лучше делать отдельной веткой после того, как будет создан Firebase-проект: конфигурационный файл привязан к конкретному Android package name.

## Что подключать в первой версии

Минимальный, безопасный набор:

1. **Anonymous Authentication** — создаёт игроку UID без окна регистрации.
2. **Cloud Firestore** — хранит лучший результат, номер фазы и таблицу лидеров.
3. **Analytics** — события начала раунда, смерти, прохождения фазы.
4. **Crashlytics** — отчёты об ошибках реальных Android-устройств.
5. Позже: **Remote Config** для сложности, частоты спавна и баланса без выпуска нового APK.

Не записывайте очки в Firestore напрямую как «истину»: клиент можно модифицировать. Для глобального лидерборда используйте Cloud Functions / серверную проверку результата. Локальный рекорд остаётся мгновенным в `PlayerPrefs`, а серверный — подтверждённым.

## 1. Создать Firebase-проект

1. В Firebase Console создать проект `Orbital Rift`.
2. Добавить Android-приложение.
3. В Unity открыть **Edit → Project Settings → Player → Android → Other Settings** и скопировать точный `Package Name` (например, `com.kiyoakiii.orbitalrift`). Он чувствителен к регистру и после регистрации приложения в Firebase не меняется.
4. Скачать `google-services.json` и положить его непосредственно в `Assets/` — не в `Resources/` и не переименовывать в `google-services (2).json`.

Официальная инструкция: https://firebase.google.com/docs/unity/setup

## 2. Установить SDK

Скачать актуальный Firebase Unity SDK с официальной страницы и импортировать только нужные пакеты:

- `FirebaseApp.unitypackage` / Core;
- `FirebaseAuth.unitypackage`;
- `FirebaseFirestore.unitypackage`;
- `FirebaseAnalytics.unitypackage`;
- `FirebaseCrashlytics.unitypackage`.

Не смешивать способы установки: либо `.unitypackage`, либо UPM `.tgz`. Если используется UPM, добавить сначала External Dependency Manager, затем Firebase Core, затем продукты Firebase.

После импорта выполнить **Assets → External Dependency Manager → Android Resolver → Force Resolve**. Для Firestore на Android включить minification в Player Settings → Android → Publishing Settings, если сборка упрётся в limit методов / dex merge.

Альтернативная официальная схема установки: https://firebase.google.com/docs/unity/setup-alternative

## 3. Инициализация до игрового меню

Создать `Assets/Scripts/FirebaseBootstrap.cs` и вызывать его на стартовом объекте раньше `GameManager`:

```csharp
using Firebase;
using Firebase.Extensions;
using UnityEngine;

public sealed class FirebaseBootstrap : MonoBehaviour
{
    public static bool Ready { get; private set; }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogError("Firebase unavailable: " + task.Result);
                return;
            }
            Ready = true;
        });
    }
}
```

Не делать запросы к Auth/Firestore до `Ready == true`.

## 4. Анонимный игрок

В Firebase Console открыть **Authentication → Sign-in method** и включить **Anonymous**. Затем после `Ready`:

```csharp
using Firebase.Auth;
using Firebase.Extensions;

var auth = FirebaseAuth.DefaultInstance;
auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
{
    if (task.IsFaulted || task.IsCanceled) return;
    var uid = task.Result.User.UserId;
    UnityEngine.Debug.Log("Firebase UID: " + uid);
});
```

Официальный пример: https://firebase.google.com/docs/auth/unity/anonymous-auth

## 5. Структура Firestore

Коллекция `players`, документ — UID:

```text
players/{uid}
  bestScore: number
  bestPhase: number
  updatedAt: server timestamp
  build: string
```

Коллекция `leaderboard`, документ создаётся только Cloud Function после валидации:

```text
leaderboard/{entryId}
  uid: string
  score: number
  phase: number
  createdAt: server timestamp
```

Для локального рекорда разрешить игроку писать только `players/{request.auth.uid}`. Для `leaderboard` запретить клиентские записи полностью.

```text
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /players/{uid} {
      allow read: if request.auth != null && request.auth.uid == uid;
      allow create, update: if request.auth != null && request.auth.uid == uid;
    }
    match /leaderboard/{entry} {
      allow read: if true;
      allow write: if false;
    }
  }
}
```

## 6. События

Отправлять Analytics-события без персональных данных:

```text
game_start
phase_complete  { phase }
player_hit      { shields_left }
game_over       { score, phase }
```

## 7. Проверка перед релизом

1. Проверить Android APK на настоящем телефоне с интернетом и без него.
2. В Firebase Console убедиться, что появился анонимный UID.
3. Записать тестовый рекорд и проверить его в Firestore.
4. Убедиться, что правила запрещают запись в `leaderboard` из клиента.
5. Сделать тестовый Crashlytics non-fatal report.

Firebase Unity для Windows Editor имеет ограниченный beta-режим. Функциональную проверку перед релизом делайте на Android.
