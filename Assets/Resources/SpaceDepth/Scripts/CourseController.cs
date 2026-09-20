using System;
using UnityEngine;
namespace OrbitalRift
{
    public enum CourseAutoTest { Off, Horizontal, Vertical, Circle, FigureEight }
    [Serializable]
    public sealed class CourseSteeringSettings
    {
        [Tooltip("Включает новый курс. Выключение сбрасывает центр в режим Phase 1.")]
        public bool SteeringEnabled = true;
        [Tooltip("Скорость перемещения цели ввода, в долях экрана за секунду.")]
        [Min(0)] public float CourseMoveSpeed = .22f;
        [Tooltip("Время сглаживания текущего центра. Больше = мягче и медленнее.")]
        [Min(.01f)] public float CourseSmoothTime = .28f;
        [Tooltip("Ограничение скорости текущего центра, доли экрана за секунду.")]
        [Min(.01f)] public float MaxCourseCenterSpeed = .65f;
        [Tooltip("Отклонение от середины экрана. Дополнительно ограничено полным радиусом орбиты и запасом.")]
        [Range(0,.45f)] public float MaxCourseOffsetX = .22f, MaxCourseOffsetY = .20f;
        [Range(0,.9f)] public float InputDeadZone = .16f;
        [Tooltip("После отпускания ввода возвращать цель в середину. Иначе курс сохраняется.")]
        public bool ReturnToCenter;
        [Min(0)] public float ReturnToCenterSpeed = .22f;
        [Tooltip("Общая сила смещения точек схода слоёв. Не меняет скорость полёта или радиус орбиты.")]
        [Range(0,2)] public float GlobalSteeringStrength = 1;
        [Tooltip("Насколько центр орбиты следует за курсом: 0 = прежний центр, 1 = полный ход.")]
        [Range(0,1)] public float OrbitCenterFollowStrength = 1;
        [Min(0)] public float SafeScreenMargin = .25f;
        public bool InstantReset, ShowCourseDebug, ShowDebugLines = true;
        public KeyCode LeftKey=KeyCode.J, RightKey=KeyCode.L, UpKey=KeyCode.I, DownKey=KeyCode.K;
        public CourseSteeringSettings Copy()=>(CourseSteeringSettings)MemberwiseClone();
    }

    // Course coordinates are viewport units; world conversion uses the existing orthographic camera.
    public sealed class CourseController
    {
        public Vector2 NeutralCourseCenter => new Vector2(.5f,.5f);
        public Vector2 TargetCourseCenter { get; private set; } = new Vector2(.5f,.5f);
        public Vector2 CurrentCourseCenter { get; private set; } = new Vector2(.5f,.5f);
        public Vector2 CourseOffset => CurrentCourseCenter-NeutralCourseCenter;
        public Vector2 NeutralWorldCenter { get; private set; }
        public Vector2 WorldCourseCenter => NeutralWorldCenter+Vector2.Scale(CourseOffset,WorldSize);
        public Vector2 SafeOffset { get; private set; }
        public CourseSteeringSettings Settings { get; set; }
        public CourseAutoTest AutoTest;
        private readonly Camera view;
        private readonly float orbitRadius;
        private Vector2 velocity;
        private float testTime;
        private Vector2 WorldSize => new Vector2(view.orthographicSize*2*view.aspect,view.orthographicSize*2);
        public CourseController(Camera camera,Vector2 neutral,float radius,CourseSteeringSettings settings)
        {view=camera;NeutralWorldCenter=neutral;orbitRadius=radius;Settings=settings;UpdateBounds();}
        public void UpdateBounds()
        {
            var world=WorldSize;var follow=Mathf.Clamp01(Settings.OrbitCenterFollowStrength);
            var reserve=orbitRadius+Mathf.Max(0,Settings.SafeScreenMargin);
            SafeOffset=new Vector2(Mathf.Min(Mathf.Max(0,Settings.MaxCourseOffsetX),follow>.001f?Mathf.Max(0,(world.x*.5f-reserve)/(world.x*follow)):.45f),Mathf.Min(Mathf.Max(0,Settings.MaxCourseOffsetY),follow>.001f?Mathf.Max(0,(world.y*.5f-reserve)/(world.y*follow)):.45f));
        }
        private Vector2 Clamp(Vector2 target)
        {
            var d=target-NeutralCourseCenter;
            var q=new Vector2(SafeOffset.x>.00001f?d.x/SafeOffset.x:0,SafeOffset.y>.00001f?d.y/SafeOffset.y:0);
            q=Vector2.ClampMagnitude(q,1);return NeutralCourseCenter+Vector2.Scale(q,SafeOffset);
        }
        public void SetTarget(Vector2 viewport){UpdateBounds();TargetCourseCenter=Clamp(viewport);}
        public void Preset(Vector2 direction){SetTarget(NeutralCourseCenter+Vector2.Scale(direction,SafeOffset));}
        public void Center(){TargetCourseCenter=NeutralCourseCenter;if(Settings.InstantReset)ResetInstant();}
        public void ResetInstant(){TargetCourseCenter=CurrentCourseCenter=NeutralCourseCenter;velocity=Vector2.zero;testTime=0;}
        public void Tick(Vector2 input,float dt,bool inputBlocked=false)
        {
            UpdateBounds();if(!Settings.SteeringEnabled){ResetInstant();return;}if(dt<=0)return;
            TargetCourseCenter=Clamp(TargetCourseCenter);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(AutoTest!=CourseAutoTest.Off)
            {
                testTime+=dt*.65f;
                var direction=AutoTest==CourseAutoTest.Horizontal?new Vector2(Mathf.Sin(testTime),0):AutoTest==CourseAutoTest.Vertical?new Vector2(0,Mathf.Sin(testTime)):new Vector2(Mathf.Cos(testTime),Mathf.Sin(testTime*(AutoTest==CourseAutoTest.FigureEight?2:1)));
                Preset(direction*.8f);input=Vector2.zero;inputBlocked=true;
            }
#endif
            if(!inputBlocked)
            {
                input=Vector2.ClampMagnitude(input,1);
                if(input.magnitude>Settings.InputDeadZone)SetTarget(TargetCourseCenter+input*Settings.CourseMoveSpeed*dt);
                else if(Settings.ReturnToCenter)TargetCourseCenter=Vector2.MoveTowards(TargetCourseCenter,NeutralCourseCenter,Settings.ReturnToCenterSpeed*dt);
            }
            CurrentCourseCenter=Clamp(Vector2.SmoothDamp(CurrentCourseCenter,TargetCourseCenter,ref velocity,Mathf.Max(.01f,Settings.CourseSmoothTime),Mathf.Max(.01f,Settings.MaxCourseCenterSpeed),dt));
        }
    }
}
