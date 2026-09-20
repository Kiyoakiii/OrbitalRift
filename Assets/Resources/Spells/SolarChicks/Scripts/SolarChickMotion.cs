using UnityEngine;

namespace OrbitalRift
{
    // Only the bird-specific deformation belongs here. No projectile or impact simulation.
    public sealed class SolarChickMotion : SpellVfxMotion
    {
        [Header("Силуэт птицы · изображение смотрит вправо")]
        public MeshRenderer Bird;
        public MeshRenderer FlameLeft, FlameRight;
        [Tooltip("Размер PNG-птенца в мировых единицах. Радиус попадания не меняется.")]
        [Range(.1f, 2)] public float BirdSize = .82f;
        [Tooltip("Взмахов в секунду. Крыло деформируется отдельно от головы.")]
        [Range(0, 12)] public float WingBeatSpeed = 3.6f;
        [Tooltip("Насколько крыло складывается. 0 — PNG неподвижен, 1 — сильный взмах.")]
        [Range(0, 1)] public float WingFold = .65f;
        [Tooltip("Маленькое покачивание тела; не изменяет траекторию попадания.")]
        [Range(0, .15f)] public float BodyBob = .018f;
        [Range(0, 20)] public float BankAngle = 4.5f;
        [Header("Огненные PNG-ленты · независимая вторичная анимация")]
        [Range(.1f, 2)] public float FlameSize = .74f;
        [Range(0, 1)] public float FlameSway = .22f;
        [Range(0, 2)] public float FlameBrightness = .7f;
        private MaterialPropertyBlock block;
        private SpellVfxProfile profile;
        private float phase, currentAge;
        private float flightLifetime = 4.25f;
        private bool visible;
        private int layers = 255;
        public float WingPhase => currentAge * WingBeatSpeed * Mathf.PI * 2 + phase;

        public override void Begin(SpellVfxProfile settings, float seed)
        {
            profile = settings;
            var owner = GetComponent<SpellProjectileVfx>().Owner;
            flightLifetime = owner != null && owner.Shot != null ? owner.Shot.Lifetime : 4.25f;
            if (profile.UseBirdMotionSettings)
            {
                BirdSize = profile.BirdSize; WingBeatSpeed = profile.WingBeatSpeed; WingFold = profile.WingFold;
                BodyBob = profile.BodyBob; BankAngle = profile.BankAngle; FlameSize = profile.FlameSize;
                FlameSway = profile.FlameSway; FlameBrightness = profile.FlameBrightness;
            }
            phase = seed; visible = true; layers = 255; Tick(0,0);
        }

        public override void Tick(float age, float dt)
        {
            currentAge = age;
            var spawn = Mathf.SmoothStep(.35f,1,Mathf.Clamp01(age/.12f));
            var bob = (layers & 16) != 0 ? Mathf.Sin(age*7.3f+phase)*BodyBob : 0;
            Bird.transform.localPosition = new Vector3(bob,.025f,0);
            Bird.transform.localRotation = Quaternion.Euler(0,0,90+Mathf.Sin(age*5.1f+phase)*BankAngle);
            Bird.transform.localScale = Vector3.one * (BirdSize*spawn);
            Paint(Bird, profile.BirdTint, Mathf.Min(1.3f,profile.CoreBrightness), WingPhase, WingFold);
            FlameLeft.transform.localPosition = new Vector3(-.07f,-.24f,0);
            FlameRight.transform.localPosition = new Vector3(.07f,-.28f,0);
            FlameLeft.transform.localRotation = Quaternion.Euler(0,0,160+Mathf.Sin(age*6.5f+phase)*6);
            FlameRight.transform.localRotation = Quaternion.Euler(0,0,195+Mathf.Sin(age*5.2f+phase+2)*5);
            FlameLeft.transform.localScale = new Vector3(FlameSize*.85f,FlameSize,1)*spawn;
            FlameRight.transform.localScale = new Vector3(-FlameSize*.7f,FlameSize*.9f,1)*spawn;
            Paint(FlameLeft, profile.FlameLeftTint, FlameBrightness, age*7+phase, FlameSway);
            Paint(FlameRight, profile.FlameRightTint, FlameBrightness*.8f, age*5.8f+phase+2, FlameSway);
            ApplyVisibility();
        }

        private void Paint(Renderer renderer, Color color, float brightness, float beat, float fold)
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (profile.UseLifetimeGradients) color.a *= profile.AlphaOverLife.Evaluate(Mathf.Clamp01(currentAge / Mathf.Max(.01f, flightLifetime)));
            block.SetColor("_Tint",new Color(color.r*brightness,color.g*brightness,color.b*brightness,color.a));
            block.SetFloat("_Beat",beat); block.SetFloat("_Fold",fold);
            renderer.SetPropertyBlock(block);
        }
        public override void End() { visible = false; ApplyVisibility(); }
        public override void SetLayerMask(int mask) { layers=mask; ApplyVisibility(); }
        private void ApplyVisibility()
        {
            if (Bird != null) Bird.enabled=visible && (layers&1)!=0;
            if (FlameLeft != null) FlameLeft.enabled=visible && (layers&2)!=0;
            if (FlameRight != null) FlameRight.enabled=visible && (layers&2)!=0;
        }
    }
}
