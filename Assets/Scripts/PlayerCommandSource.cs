using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// Compact input snapshot consumed by the gameplay simulation. Network clients will
    /// send this value instead of manipulating ships and combat objects directly.
    /// </summary>
    public readonly struct PlayerCommandFrame
    {
        public static readonly PlayerCommandFrame None = new PlayerCommandFrame(0, false, false, false, false);

        public readonly int OrbitDirection;
        public readonly bool ToggleAutoFire;
        public readonly bool BackPressed;
        public readonly bool UseRiftEcho;
        public readonly bool UseVectorSnap;
        public readonly Vector2 CourseDirection;

        // Editor validators and offline simulations still use the original
        // three-value snapshot.  Keep that public contract intact while the
        // local source supplies the two new ability edges.
        public PlayerCommandFrame(int orbitDirection, bool toggleAutoFire, bool backPressed)
            : this(orbitDirection, toggleAutoFire, backPressed, false, false)
        {
        }

        public PlayerCommandFrame(int orbitDirection, bool toggleAutoFire, bool backPressed, bool useRiftEcho, bool useVectorSnap)
            : this(orbitDirection,toggleAutoFire,backPressed,useRiftEcho,useVectorSnap,Vector2.zero) { }

        public PlayerCommandFrame(int orbitDirection, bool toggleAutoFire, bool backPressed, bool useRiftEcho, bool useVectorSnap,Vector2 courseDirection)
        {
            CourseDirection=Vector2.ClampMagnitude(courseDirection,1);
            OrbitDirection = orbitDirection < 0 ? -1 : orbitDirection > 0 ? 1 : 0;
            ToggleAutoFire = toggleAutoFire;
            BackPressed = backPressed;
            UseRiftEcho = useRiftEcho;
            UseVectorSnap = useVectorSnap;
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
        public CourseSteeringSettings CourseBindings;
        public bool CourseControlsActive;

        public PlayerCommandFrame ReadFrame()
        {
            var direction = ReadOrbitDirection();
            return new PlayerCommandFrame(
                direction,
                Input.GetKeyDown(KeyCode.Space),
                Input.GetKeyDown(KeyCode.Escape),
                Input.GetKeyDown(KeyCode.Q),
                Input.GetKeyDown(KeyCode.E), ReadCourseDirection());
        }
        private Vector2 ReadCourseDirection()
        {
            if(CourseBindings==null||!CourseControlsActive||!CourseBindings.SteeringEnabled)return Vector2.zero;
            var s=CourseBindings;
            var keyboard=new Vector2((Input.GetKey(s.RightKey)?1:0)-(Input.GetKey(s.LeftKey)?1:0),(Input.GetKey(s.UpKey)?1:0)-(Input.GetKey(s.DownKey)?1:0));
            var stick=new Vector2(Input.GetAxisRaw("CourseHorizontal"),Input.GetAxisRaw("CourseVertical"));
            return Vector2.ClampMagnitude(keyboard+stick,1);
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

            var keys = CourseControlsActive&&CourseBindings!=null&&CourseBindings.SteeringEnabled
                ? Input.GetAxisRaw("OrbitKeyboard")+Input.GetAxisRaw("OrbitHorizontal")
                : Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(keys) > .01f) direction = keys < 0f ? -1 : 1;
            return direction;
        }
    }
}
