using System;
using UnityEngine;

namespace OrbitalRift
{
    public enum SpaceLayerKind { DeepSpace, Galaxy, Stars, Nebula, Planet, Streaks, Asteroids, ExternalMusicRings, Dust }
    public enum SpaceLayerAppearance { Procedural, Image }

    [Serializable]
    public sealed class SpaceNebulaSettings
    {
        [Range(2, 4)] public int ClusterCount = 3;
        [Min(1)] public float ClusterSize = 7.2f;
        [Range(0, 3)] public float Brightness = 1.15f;
        [Range(0, 1)] public float Opacity = .82f;
        [Range(0, 2)] public float Glow = .78f;
        [Range(0, 1)] public float Filament = .72f;
        [Range(0, 1)] public float DarkVoids = .72f;
        [Range(0, 1)] public float Drift = .22f;
        [Range(0, 2)] public float Dust = .62f;
        public Color Tint = new Color(.86f, .70f, 1f, 1f);
        public Color HighlightTint = new Color(.35f, .78f, 1f, 1f);
        public SpaceNebulaSettings Copy() => (SpaceNebulaSettings)MemberwiseClone();
    }

    [Serializable]
    public sealed class SpaceLayerSettings
    {
        public bool Enabled = true;
        public string DisplayName;
        public SpaceLayerKind Kind;
        public SpaceLayerAppearance Appearance;
        [Tooltip("Необязательно. Исходный материал не изменяется. Shader должен поддерживать UV и vertex COLOR.")]
        public Material Material;
        [Tooltip("Изображение для Image. Sprite имеет приоритет над Texture.")]
        public Texture2D Texture;
        public Sprite Sprite;
        public bool PreserveImageAspect = true;
        [Tooltip("Для галактик и туманностей: композиция по краям кадра, с учётом пропорций экрана.")]
        public bool FrameComposition;
        [Range(0,1)] public float Depth;
        [Min(0)] public float SpeedMultiplier;
        [Tooltip("Амплитуда смещения точки схода при смене курса.")]
        [Range(0,1)] public float SteeringInfluence;
        [Min(0)] public float SteeringResponseSpeed = 2;
        [Min(0)] public float ParallaxMultiplier = 1;
        [Min(0)] public float Density = 1;
        public int Capacity = 128;
        public int BaseCount = 50;
        [Min(.001f)] public float MinScale = .02f, MaxScale = .04f;
        [Range(0,1)] public float MinAlpha = .1f, MaxAlpha = .4f;
        [Range(0,3)] public float Brightness = 1;
        public Color PrimaryColor = Color.white;
        [Tooltip("Дополнительный оттенок: разные элементы получают смесь двух цветов.")]
        public Color SecondaryColor = Color.white;
        [Range(0,1)] public float ColorVariation;
        [Tooltip("Три плана размеров, скорости и освещения внутри слоя обломков / пыли.")]
        public bool DepthVariety;
        [Tooltip("Мягкий край и расфокус только частиц этого слоя, без размытия сцены.")]
        [Range(0,1)] public float Softness = .6f;
        [Tooltip("Крупные ближние обломки и дымка проявляются за этим радиусом от центра. World units.")]
        [Min(0)] public float CenterClearance = 5;
        [Min(0)] public float SpawnRadius = .35f;
        [Min(.1f)] public float DespawnRadius = 12;
        public float RotationSpeed;
        [Range(0,1)] public float NoiseAmount = .1f;
        public bool RadialMotion = true;
        public float TangentialMotion;
        [Tooltip("Длина полосы относительно размера. Только Streaks.")]
        public float Stretch = 18;
        public SpaceLayerSettings Copy() => (SpaceLayerSettings)MemberwiseClone();
    }

    [CreateAssetMenu(menuName="Orbital Rift/Space Depth Profile")]
    public sealed class SpaceDepthProfile : ScriptableObject
    {
        [Min(0)] public float GlobalTravelSpeed = 1.8f;
        public CourseSteeringSettings Steering = new CourseSteeringSettings();
        public SpaceNebulaSettings Nebula = new SpaceNebulaSettings();
        public SpaceLayerSettings[] Layers;
        public static SpaceLayerSettings[] Defaults()
        {
            var names=new[]{"Дальний космос","Галактики","Дальние звёзды","Туманности","Планеты","Средние звёзды","Дальние стрики","Астероиды","Средние стрики","Кольца музыки (внешние)","Ближние стрики","Передняя пыль"};
            var kinds=new[]{SpaceLayerKind.DeepSpace,SpaceLayerKind.Galaxy,SpaceLayerKind.Stars,SpaceLayerKind.Nebula,SpaceLayerKind.Planet,SpaceLayerKind.Stars,SpaceLayerKind.Streaks,SpaceLayerKind.Asteroids,SpaceLayerKind.Streaks,SpaceLayerKind.ExternalMusicRings,SpaceLayerKind.Streaks,SpaceLayerKind.Dust};
            float[] depths={0,.05f,.1f,.15f,.2f,.35f,.45f,.55f,.7f,.75f,.9f,1};
            float[] speeds={.008f,.025f,.13f,.04f,.055f,.42f,.65f,.8f,1.2f,0,2.1f,2.6f};
            int[] counts={1,2,240,3,1,100,52,7,30,0,12,22};
            float[] sizes={26,6,.035f,8,3.2f,.045f,.016f,.28f,.023f,1,.032f,.04f};
            float[] alphas={.65f,.30f,.70f,.23f,.65f,.66f,.35f,.62f,.46f,0,.65f,.4f};
            var a=new SpaceLayerSettings[12];
            for(var i=0;i<a.Length;i++)
            {
                a[i]=new SpaceLayerSettings{DisplayName=names[i],Kind=kinds[i],Depth=depths[i],SpeedMultiplier=speeds[i],SteeringInfluence=depths[i],SteeringResponseSpeed=Mathf.Lerp(.6f,8,depths[i]),BaseCount=counts[i],Capacity=Mathf.Max(4,counts[i]*3),MinScale=sizes[i],MaxScale=sizes[i]*1.65f,MinAlpha=alphas[i]*.45f,MaxAlpha=alphas[i],PrimaryColor=new Color(.48f,.67f,1),RadialMotion=i!=0&&i!=1&&i!=3&&i!=4,RotationSpeed=i==7?14:0,NoiseAmount=i==3?.18f:.06f,Enabled=i!=9,Stretch=i==10?35:i==8?26:18};
            }
            a[0].PrimaryColor=new Color(.05f,.10f,.25f);a[0].MinScale=a[0].MaxScale=35;
            a[1].PrimaryColor=new Color(.45f,.38f,.83f);
            a[3].PrimaryColor=new Color(.3f,.21f,.65f);
            a[4].PrimaryColor=new Color(.3f,.53f,.8f);
            a[7].PrimaryColor=new Color(.43f,.36f,.32f);
            a[10].PrimaryColor=new Color(.72f,.77f,1);
            a[11].PrimaryColor=new Color(.65f,.8f,.92f);
            ApplyArtDirection(a);
            return a;
        }
        // Art values only. Does not modify steering, input, layer identities or external music.
        public static void ApplyArtDirection(SpaceLayerSettings[] a)
        {
            a[0].PrimaryColor=new Color(.025f,.045f,.12f);
            a[1].BaseCount=1;a[1].MinAlpha=.04f;a[1].MaxAlpha=.10f;
            a[2].BaseCount=200;a[2].MinAlpha=.24f;a[2].MaxAlpha=.70f;
            var n=a[3];n.BaseCount=3;n.Capacity=4;n.MinScale=9;n.MaxScale=12;
            n.MinAlpha=.4f;n.MaxAlpha=.6f;n.Brightness=1.2f;n.PrimaryColor=new Color(1,.82f,1);n.SecondaryColor=new Color(.52f,.9f,1);n.ColorVariation=.55f;
            n.SpeedMultiplier=.025f;n.RotationSpeed=.35f;n.NoiseAmount=.15f;
            a[4].Enabled=false;a[4].BaseCount=1;a[4].Capacity=1;a[4].MinScale=2.2f;a[4].MaxScale=2.8f;
            a[5].BaseCount=85;a[5].MinAlpha=.2f;a[5].MaxAlpha=.65f;
            var r=a[7];r.BaseCount=24;r.Capacity=72;r.MinScale=.38f;r.MaxScale=1.05f;
            r.MinAlpha=.72f;r.MaxAlpha=1;r.PrimaryColor=new Color(.53f,.47f,.48f);r.SecondaryColor=new Color(.4f,.53f,.68f);r.ColorVariation=.65f;
            r.DepthVariety=true;r.CenterClearance=4.7f;r.SpeedMultiplier=.42f;r.RotationSpeed=22;r.NoiseAmount=.015f;
            int[] streakSlots={6,8,10};int[] counts={42,24,8};float[] alphas={.3f,.42f,.56f};float[] stretches={16,24,32};
            for(var i=0;i<3;i++){var s=a[streakSlots[i]];s.BaseCount=counts[i];s.MinAlpha=alphas[i]*.45f;s.MaxAlpha=alphas[i];s.Stretch=stretches[i];s.SecondaryColor=new Color(.75f,.35f,1);s.ColorVariation=.5f;}
            var d=a[11];d.BaseCount=46;d.Capacity=138;d.MinScale=.045f;d.MaxScale=.13f;d.MinAlpha=.2f;d.MaxAlpha=.5f;
            d.PrimaryColor=new Color(.47f,.7f,1);d.SecondaryColor=new Color(.84f,.44f,.67f);d.ColorVariation=.75f;d.DepthVariety=true;d.Softness=.75f;d.CenterClearance=4.8f;d.SpeedMultiplier=1.6f;d.NoiseAmount=.04f;
        }
    }
}
