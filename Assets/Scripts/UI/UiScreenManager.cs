using System;
using UnityEngine;

namespace OrbitalRift.UI
{
    public enum UiScreenId
    {
        None,
        MainMenu,
        Settings,
        CoopLobby,
        ClassicHud,
        ExpeditionHud,
        DefenseHud,
        Results
    }

    /// <summary>One place owns screen visibility. Gameplay code chooses a screen; views own their layout.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class UiScreenManager : MonoBehaviour
    {
        [Serializable]
        private struct ScreenBinding
        {
            public UiScreenId Id;
            public RectTransform Root;
        }

        [SerializeField] private ScreenBinding[] screens = Array.Empty<ScreenBinding>();
        [SerializeField] private UiScreenId visibleScreen;

        public UiScreenId VisibleScreen => visibleScreen;

        public void Configure(params (UiScreenId id, RectTransform root)[] bindings)
        {
            screens = new ScreenBinding[bindings.Length];
            for (var i = 0; i < bindings.Length; i++)
                screens[i] = new ScreenBinding { Id = bindings[i].id, Root = bindings[i].root };
        }

        public void ShowOnly(UiScreenId screen)
        {
            if (visibleScreen == screen)
            {
                var alreadyCorrect = true;
                for (var i = 0; i < screens.Length; i++)
                    if (screens[i].Root != null && screens[i].Root.gameObject.activeSelf != (screens[i].Id == screen))
                    {
                        alreadyCorrect = false;
                        break;
                    }
                if (alreadyCorrect) return;
            }
            visibleScreen = screen;
            for (var i = 0; i < screens.Length; i++)
                if (screens[i].Root != null)
                    screens[i].Root.gameObject.SetActive(screens[i].Id == screen);
        }
    }
}
