using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Single scene entry point. The game is generated from primitives, so it has no borrowed art assets.</summary>
    public sealed class OrbitalRiftBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<GameManager>();
        }
    }
}
