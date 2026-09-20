using UnityEngine;
namespace OrbitalRift
{
    // Cache references and clone only when the user changes the material. Never edit a project material.
    public sealed class SpaceLayerVisualBinding
    {
        public Material Instance { get; private set; }
        public bool IsImage { get; private set; }
        public bool UsesDetailedArtwork { get; private set; }
        public float Aspect { get; private set; } = 1;
        public Vector2 UvMin { get; private set; }
        public Vector2 UvMax { get; private set; } = Vector2.one;
        private Material source;
        private Texture2D texture;
        private Sprite sprite;
        private SpaceLayerAppearance appearance;
        private SpaceLayerKind kind;
        private readonly Shader procedural,image;
        private bool initialized;
        private static readonly int ArtMotion=Shader.PropertyToID("_ArtMotion"),Softness=Shader.PropertyToID("_Softness");
        private static readonly int NebulaArt=Shader.PropertyToID("_NebulaArt"),Glow=Shader.PropertyToID("_Glow"),Filament=Shader.PropertyToID("_Filament"),DarkVoids=Shader.PropertyToID("_DarkVoids"),Drift=Shader.PropertyToID("_Drift"),Dust=Shader.PropertyToID("_Dust"),Tint=Shader.PropertyToID("_NebulaTint"),Highlight=Shader.PropertyToID("_HighlightTint");
        public void UpdateNebula(SpaceNebulaSettings s)
        {
            if(!UsesDetailedArtwork||s==null)return;
            Instance.SetFloat(NebulaArt,1);Instance.SetFloat(Glow,s.Glow);Instance.SetFloat(Filament,s.Filament);
            Instance.SetFloat(DarkVoids,s.DarkVoids);Instance.SetFloat(Drift,s.Drift);Instance.SetFloat(Dust,s.Dust);
            Instance.SetColor(Tint,s.Tint);Instance.SetColor(Highlight,s.HighlightTint);
        }
        public void UpdateArt(float motion,float softness)
        {
            if(Instance.HasProperty(ArtMotion))Instance.SetFloat(ArtMotion,motion);
            if(Instance.HasProperty(Softness))Instance.SetFloat(Softness,softness);
        }
        public SpaceLayerVisualBinding(){procedural=Resources.Load<Shader>("SpaceDepth/Shaders/SpaceDepth");image=Resources.Load<Shader>("SpaceDepth/Shaders/SpaceDepthImage");}
        public void Invalidate(){initialized=false;}
        public bool Refresh(SpaceLayerSettings s)
        {
            if(initialized&&source==s.Material&&texture==s.Texture&&sprite==s.Sprite&&appearance==s.Appearance&&kind==s.Kind)return false;
            var rebuild=!initialized||source!=s.Material||appearance!=s.Appearance||kind!=s.Kind;
            source=s.Material;texture=s.Texture;sprite=s.Sprite;appearance=s.Appearance;kind=s.Kind;initialized=true;
            IsImage=appearance==SpaceLayerAppearance.Image;
            if(rebuild)
            {
                if(Instance!=null)Object.Destroy(Instance);
                Instance=source!=null?new Material(source):new Material(IsImage?image:procedural);
                Instance.name="SpaceDepth runtime "+s.DisplayName;
                UsesDetailedArtwork=Instance.HasProperty(NebulaArt);
                if(!IsImage&&Instance.HasProperty("_Kind"))Instance.SetFloat("_Kind",ShaderKind(kind));
            }
            Texture resolved=sprite!=null?sprite.texture:texture!=null?texture:source!=null&&source.HasProperty("_MainTex")?source.GetTexture("_MainTex"):source!=null&&source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):null;
            if(IsImage){if(Instance.HasProperty("_MainTex"))Instance.SetTexture("_MainTex",resolved);if(Instance.HasProperty("_BaseMap"))Instance.SetTexture("_BaseMap",resolved);}
            if(IsImage&&source==null)Instance.SetColor("_Color",resolved!=null?Color.white:Color.clear);
            UvMin=Vector2.zero;UvMax=Vector2.one;Aspect=resolved!=null?resolved.width/(float)resolved.height:1;
            if(IsImage&&sprite!=null)
            {
                var coords=sprite.uv;UvMin=Vector2.one;UvMax=Vector2.zero;
                foreach(var p in coords){UvMin=Vector2.Min(UvMin,p);UvMax=Vector2.Max(UvMax,p);}
                Aspect=sprite.rect.width/sprite.rect.height;
            }
            return true;
        }
        private static int ShaderKind(SpaceLayerKind k){switch(k){case SpaceLayerKind.Streaks:return 1;case SpaceLayerKind.Nebula:return 2;case SpaceLayerKind.Planet:return 3;case SpaceLayerKind.Asteroids:return 4;case SpaceLayerKind.Galaxy:return 5;case SpaceLayerKind.DeepSpace:return 6;case SpaceLayerKind.Dust:return 7;default:return 0;}}
        public void Dispose(){if(Instance!=null)Object.Destroy(Instance);}
    }
}
