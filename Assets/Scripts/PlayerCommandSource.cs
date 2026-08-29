using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Compact input snapshot consumed by the gameplay simulation. Network clients will
    /// send this value instead of manipulating ships and combat objects directly.
    /// </summary>
    public readonly struct PlayerCommandFrame
    {
        public static readonly PlayerCommandFrame None = new PlayerCommandFrame(0, false, false);

        public readonly int OrbitDirection;
        public readonly bool ToggleAutoFire;
        public readonly bool BackPressed;

        public PlayerCommandFrame(int orbitDirection, bool toggleAutoFire, bool backPressed)
        {
            OrbitDirection = orbitDirection < 0 ? -1 : orbitDirection > 0 ? 1 : 0;
            ToggleAutoFire = toggleAutoFire;
            BackPressed = backPressed;
        }
    }

    public interface IPlayerCommandSource
    {
        PlayerCommandFrame ReadFrame();
        void Reset();
    }

    /// <summary>Reads the current phone, mouse and keyboard controls for the local player.</summary>
    public sealed class LocalPlayerCommandSource : IPlayerCommandSource
    {
        private int controlFingerId = -1;

        public PlayerCommandFrame ReadFrame()
        {
            var direction = ReadOrbitDirection();
            return new PlayerCommandFrame(
                direction,
                Input.GetKeyDown(KeyCode.Space),
                Input.GetKeyDown(KeyCode.Escape));
        }

        public void Reset()
        {
            controlFingerId = -1;
        }

        private int ReadOrbitDirection()
        {
            var direction = 0;

            // Левая половина: по часовой стрелке. Правая: против часовой.
            if (Input.touchCount > 0)
            {
                Touch touch = default;
                var foundTouch = false;
                if (controlFingerId >= 0)
                {
                    for (var i = 0; i < Input.touchCount; i++)
                    {
                        if (Input.GetTouch(i).fingerId != controlFingerId) continue;
                        touch = Input.GetTouch(i);
                        foundTouch = true;
                        break;
                    }
                }

                if (!foundTouch)
                {
                    for (var i = 0; i < Input.touchCount; i++)
                    {
                        var candidate = Input.GetTouch(i);
                        if (candidate.phase == TouchPhase.Ended || candidate.phase == TouchPhase.Canceled) continue;
                        touch = candidate;
                        controlFingerId = candidate.fingerId;
                        foundTouch = true;
                        break;
                    }
                }

                if (foundTouch)
                {
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        controlFingerId = -1;
                    else
                        direction = touch.position.x < Screen.width * .5f ? -1 : 1;
                }
            }
            else
            {
                controlFingerId = -1;
            }

            // Мышь повторяет сенсорные зоны для проверки управления на ПК.
            if (direction == 0 && Input.GetMouseButton(0))
                direction = Input.mousePosition.x < Screen.width * .5f ? -1 : 1;

            var keys = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(keys) > .01f) direction = keys < 0f ? -1 : 1;
            return direction;
        }
    }
}
