#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OrbitalRift
{
    public static class SolarChicksAssets
    {
        public const string Root = "Assets/Resources/Spells/SolarChicks/";
        [MenuItem("Orbital Rift/Spells/Create Missing Solar Chicks Assets")]
        public static void CreateMissing()
        {
            PlasmaBoltAssets.CreateMissing();
            foreach (var folder in new[] {"Prefabs","Profiles","Materials","Textures","Meshes","Scripts","Shaders"})
                Directory.CreateDirectory(Root+folder);
            AssetDatabase.Refresh();
            var bird=CopyTexture("Assets/Resources/BossAbilities/firebird_solar_chick_projectile.png","Chick.png",512);
            var ribbons=CopyTexture("Assets/Resources/SandboxVfxLayers/SolarChicks/Runtime/layer1-ribbons.png","FlameRibbons.png",256);
            var embers=CopyTexture("Assets/Resources/SandboxVfxLayers/SolarChicks/Runtime/layer3-particles.png","EmberAtlas.png",256);
            var flash=CopyTexture("Assets/Resources/SandboxVfxLayers/SolarChicks/Runtime/layer6-impact.png","ImpactFlare.png",256);
            var aftermath=CopyTexture("Assets/Resources/SandboxVfxLayers/SolarChicks/Runtime/layer7-decal.png","Afterglow.png",256);
            var glow=CopyTexture("Assets/Resources/SandboxVfxLayers/SolarChicks/Runtime/layer5-glow.png","Glow.png",256);
            var core=CopyTexture(PlasmaBoltAssets.Spell+"Textures/CoreMask.png","CoreMask.png",128);
            var trail=CopyTexture(PlasmaBoltAssets.Spell+"Textures/TrailMask.png","TrailMask.png",128);
            var ring=CopyTexture(PlasmaBoltAssets.Spell+"Textures/RingMask.png","RingMask.png",128);
            var mist=CopyTexture(PlasmaBoltAssets.Spell+"Textures/MistMask.png","MistMask.png",128);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(PlasmaBoltAssets.Root+"_Shared/Shaders/SpellUnlit.shader");
            var wingShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"Shaders/SolarChickWing.shader");
            if(shader==null||wingShader==null||ShaderUtil.ShaderHasError(shader)||ShaderUtil.ShaderHasError(wingShader))
                throw new System.Exception("Solar Chicks shader import failed");
            var birdMat=Mat("Chick",bird,wingShader,true,true,false);
            var ribbonMat=Mat("FlameRibbons",ribbons,wingShader,false,true,true);
            ribbonMat.SetFloat("_Rig",1);
            var emberMat=Mat("Embers",embers,shader,false,true,true);
            var coreMat=Mat("Heart",core,shader);
            var glowMat=Mat("Glow",glow,shader,false,true,true);
            var trailMat=Mat("Trail",trail,shader);
            var flashMat=Mat("ImpactFlare",flash,shader,false,true,true);
            var ringMat=Mat("Ring",ring,shader);
            var mistMat=Mat("Mist",mist,shader,true);
            var afterMat=Mat("Afterglow",aftermath,shader,false,true,true);
            var profile=AssetDatabase.LoadAssetAtPath<SpellVfxProfile>(Root+"Profiles/SolarChicks.asset");
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<SpellVfxProfile>();
                profile.CoreSize=new Vector2(.15f,.21f); profile.CoreColor=new Color(1,.82f,.38f);
                profile.CoreBrightness=1.12f; profile.PulseAmount=.05f; profile.PulseSpeed=4.3f;
                profile.GlowSize=new Vector2(.9f,.9f); profile.GlowColor=new Color(1,.64f,.3f,.18f); profile.GlowBrightness=.65f;
                profile.TrailWidth=.09f; profile.TrailLifetime=.26f; profile.TrailBrightness=.75f;
                profile.TrailColor.SetKeys(new[]{new GradientColorKey(new Color(1,.75f,.2f),0),new GradientColorKey(new Color(1,.22f,.025f),.4f),new GradientColorKey(new Color(.45f,.035f,.01f),1)},
                    new[]{new GradientAlphaKey(.7f,0),new GradientAlphaKey(.4f,.4f),new GradientAlphaKey(0,1)});
                profile.ParticleCount=18; profile.MicroCount=22; profile.ParticleSize=.12f; profile.ParticleSpeed=.6f; profile.ParticleLifetime=.42f;
                profile.ParticleColor=new Color(1,.8f,.45f,.85f); profile.NoiseStrength=.10f;
                profile.SecondaryMotionSpeed=2.1f; profile.SecondaryMotionRadius=.12f; profile.RibbonColor=new Color(1,.32f,.035f,.6f);
                profile.ImpactSize=1.15f; profile.ImpactBrightness=1.05f; profile.ImpactParticleCount=16; profile.ImpactDuration=.9f;
                profile.ImpactColor=new Color(1,.48f,.07f);
                AssetDatabase.CreateAsset(profile,Root+"Profiles/SolarChicks.asset");
            }
            var impact=AssetDatabase.LoadAssetAtPath<SpellImpactVfx>(Root+"Prefabs/SolarChickImpact.prefab");
            if(impact==null)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlasmaBoltAssets.Spell+"Prefabs/PlasmaBoltImpact.prefab"));
                PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                go.name="SolarChickImpact"; var fx=go.GetComponent<SpellImpactVfx>();
                fx.Flash.sharedMaterial=flashMat; fx.Ring.sharedMaterial=ringMat; fx.Mist.sharedMaterial=mistMat; fx.Aftermath.sharedMaterial=afterMat;
                fx.Sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial=coreMat;
                WarmParticles(fx.Sparks);
                impact=PrefabUtility.SaveAsPrefabAsset(go,Root+"Prefabs/SolarChickImpact.prefab").GetComponent<SpellImpactVfx>(); Object.DestroyImmediate(go);
            }
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/SolarChick.prefab")==null)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlasmaBoltAssets.Spell+"Prefabs/PlasmaBolt.prefab"));
                PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                go.name="SolarChick"; var fx=go.GetComponent<SpellProjectileVfx>(); fx.Profile=profile; fx.ImpactPrefab=impact;
                fx.Core.name="Heart"; fx.Core.sharedMaterial=coreMat; fx.Core.sortingOrder=27;
                fx.Core.transform.localScale=new Vector3(profile.CoreSize.x,profile.CoreSize.y,1);
                fx.Glow.sharedMaterial=glowMat; fx.Glow.transform.localScale=new Vector3(.9f,.9f,1);
                fx.MainTrail.sharedMaterial=fx.RibbonA.sharedMaterial=fx.RibbonB.sharedMaterial=trailMat;
                fx.MainParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial=emberMat;
                fx.MicroParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial=coreMat;
                WarmParticles(fx.MainParticles); WarmParticles(fx.MicroParticles);
                // A static PNG sheet becomes a set of independently emitted cells.
                var sheet=fx.MainParticles.textureSheetAnimation; sheet.enabled=true; sheet.numTilesX=4; sheet.numTilesY=4;
                sheet.animation=ParticleSystemAnimationType.WholeSheet; sheet.frameOverTime=new ParticleSystem.MinMaxCurve(0);
                sheet.startFrame=new ParticleSystem.MinMaxCurve(0,1);
                var motion=go.AddComponent<SolarChickMotion>(); var grid=Grid();
                motion.Bird=GridRenderer("Bird",go.transform,grid,birdMat,30);
                motion.FlameLeft=GridRenderer("FlameLeft",go.transform,grid,ribbonMat,28);
                motion.FlameRight=GridRenderer("FlameRight",go.transform,grid,ribbonMat,28);
                motion.Begin(profile,0); motion.Tick(.2f,0);
                PrefabUtility.SaveAsPrefabAsset(go,Root+"Prefabs/SolarChick.prefab"); Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets(); Debug.Log("Solar Chicks assets ready: "+Root);
        }

        private static Texture2D CopyTexture(string source,string name,int maxSize)
        {
            var path=Root+"Textures/"+name;
            if(!File.Exists(path)) File.Copy(source,path);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default; importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
            importer.wrapMode=TextureWrapMode.Clamp; importer.maxTextureSize=maxSize; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static Material Mat(string name,Texture2D texture,Shader shader,bool alpha=false,bool rgb=false,bool key=false)
        {
            var path=Root+"Materials/"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path); if(mat!=null)return mat;
            mat=new Material(shader){name=name,mainTexture=texture}; mat.SetFloat("_DstBlend",alpha?10:1);
            if(mat.HasProperty("_TextureColor"))mat.SetFloat("_TextureColor",rgb?1:0);
            mat.SetFloat("_KeyDarkPlate",key?1:0); AssetDatabase.CreateAsset(mat,path); return mat;
        }
        private static void WarmParticles(ParticleSystem ps)
        {
            var colors=ps.colorOverLifetime; var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.25f,.025f),1)},
                new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.6f,.3f),new GradientAlphaKey(0,1)});
            colors.color=gradient;
        }
        private static Mesh Grid()
        {
            var path=Root+"Meshes/AnimatedCard.asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(existing!=null)return existing;
            const int nx=24,ny=16; var vertices=new Vector3[(nx+1)*(ny+1)]; var uv=new Vector2[vertices.Length]; var indices=new int[nx*ny*6];
            for(var y=0;y<=ny;y++)for(var x=0;x<=nx;x++){var i=y*(nx+1)+x; uv[i]=new Vector2(x/(float)nx,y/(float)ny);vertices[i]=new Vector3(uv[i].x-.5f,uv[i].y-.5f,0);}
            var t=0; for(var y=0;y<ny;y++)for(var x=0;x<nx;x++){var i=y*(nx+1)+x;indices[t++]=i;indices[t++]=i+nx+1;indices[t++]=i+1;indices[t++]=i+1;indices[t++]=i+nx+1;indices[t++]=i+nx+2;}
            var mesh=new Mesh{name="Animated PNG card 24x16",vertices=vertices,uv=uv,triangles=indices};
            mesh.RecalculateNormals(); mesh.bounds=new Bounds(Vector3.zero,Vector3.one*2); AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
        private static MeshRenderer GridRenderer(string name,Transform parent,Mesh mesh,Material material,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=order;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
    }
}
#endif
