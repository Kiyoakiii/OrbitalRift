#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrbitalRift
{
    // One reference asset authoring recipe. Existing assets are never overwritten.
    public static class PlasmaBoltAssets
    {
        public const string Root = "Assets/Resources/Spells/";
        public const string Spell = Root + "PlasmaBolt/";
        [MenuItem("Orbital Rift/Spells/Create Missing Plasma Bolt Assets")]
        public static void CreateMissing()
        {
            foreach (var path in new[] { "_Shared/Materials", "_Shared/Textures", "_Shared/Shaders", "_Shared/Scripts", "_Shared/Prefabs",
                "_Documentation", "PlasmaBolt/Prefabs", "PlasmaBolt/Materials", "PlasmaBolt/Textures", "PlasmaBolt/Profiles" })
                Directory.CreateDirectory(Root + path);
            AssetDatabase.Refresh();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "_Shared/Shaders/SpellUnlit.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new System.Exception("Spell shader missing or invalid");
            var core = Material("PlasmaCore", Mask("CoreMask", 0), shader);
            var glow = Material("PlasmaGlow", Mask("HaloMask", 1), shader);
            var trail = Material("PlasmaTrail", Mask("TrailMask", 2), shader, false, .35f);
            var ring = Material("PlasmaRing", Mask("RingMask", 3), shader);
            var mist = Material("PlasmaMist", Mask("MistMask", 4), shader, true);
            var profile = AssetDatabase.LoadAssetAtPath<SpellVfxProfile>(Spell + "Profiles/PlasmaBolt.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<SpellVfxProfile>();
                profile.TrailColor.SetKeys(new[] { new GradientColorKey(new Color(.3f,.86f,1),0), new GradientColorKey(new Color(.13f,.38f,1),.4f), new GradientColorKey(new Color(.35f,.08f,.7f),1) },
                    new[] {new GradientAlphaKey(.85f,0),new GradientAlphaKey(.45f,.4f),new GradientAlphaKey(0,1)});
                AssetDatabase.CreateAsset(profile, Spell + "Profiles/PlasmaBolt.asset");
            }
            var impact = AssetDatabase.LoadAssetAtPath<SpellImpactVfx>(Spell + "Prefabs/PlasmaBoltImpact.prefab");
            if (impact == null)
            {
                var go = new GameObject("PlasmaBoltImpact");
                var fx = go.AddComponent<SpellImpactVfx>();
                fx.Flash = Quad("Flash", go.transform, core, 29);
                fx.Ring = Quad("Ring", go.transform, ring, 26);
                fx.Mist = Quad("Mist", go.transform, mist, 23);
                fx.Aftermath = Quad("Aftermath", go.transform, glow, 24);
                fx.Sparks = Particles("Sparks", go.transform, core, false);
                impact = PrefabUtility.SaveAsPrefabAsset(go, Spell + "Prefabs/PlasmaBoltImpact.prefab").GetComponent<SpellImpactVfx>();
                Object.DestroyImmediate(go);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Spell + "Prefabs/PlasmaBolt.prefab") == null)
            {
                var go = new GameObject("PlasmaBolt");
                var fx = go.AddComponent<SpellProjectileVfx>();
                fx.Profile = profile; fx.ImpactPrefab = impact;
                fx.Core = Quad("Core", go.transform, core, 28);
                fx.Glow = Quad("Glow", go.transform, glow, 25);
                fx.Core.transform.localScale = new Vector3(profile.CoreSize.x,profile.CoreSize.y,1);
                fx.Glow.transform.localScale = new Vector3(profile.GlowSize.x,profile.GlowSize.y,1);
                fx.MainTrail = Trail("MainTrail", Group("Trails", go.transform), trail, .16f, 26);
                var secondary = Group("SecondaryMotion", go.transform);
                fx.RibbonA = Trail("Ribbon_A", secondary, trail, .024f, 27);
                fx.RibbonB = Trail("Ribbon_B", secondary, trail, .018f, 27);
                var particles = Group("Particles", go.transform);
                fx.MainParticles = Particles("MainParticles", particles, core, true);
                fx.MicroParticles = Particles("MicroParticles", particles, glow, true);
                PrefabUtility.SaveAsPrefabAsset(go, Spell + "Prefabs/PlasmaBolt.prefab");
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Plasma Bolt assets ready: " + Spell);
        }

        private static Transform Group(string name, Transform parent)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }

        private static MeshRenderer Quad(string name, Transform parent, Material material, int order)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name; go.transform.SetParent(parent, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }

        private static TrailRenderer Trail(string name, Transform parent, Material material, float width, int order)
        {
            var trail = Group(name, parent).gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material; trail.time = .16f; trail.minVertexDistance = .025f;
            trail.widthMultiplier = width; trail.widthCurve = AnimationCurve.EaseInOut(0,1,1,0);
            trail.numCapVertices = 3; trail.numCornerVertices = 2;
            trail.textureMode = LineTextureMode.Stretch; trail.alignment = LineAlignment.View;
            trail.sortingOrder = order; trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
            return trail;
        }

        private static ParticleSystem Particles(string name, Transform parent, Material material, bool loop)
        {
            var ps = Group(name, parent).gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.playOnAwake = false; m.loop = loop; m.duration = 1;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = loop ? 32 : 48;
            m.startLifetime = new ParticleSystem.MinMaxCurve(.12f,.3f);
            m.startSize = new ParticleSystem.MinMaxCurve(.025f,.055f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(.1f,.5f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            var e = ps.emission; e.enabled = loop; e.rateOverTime = loop ? 24 : 0;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = loop ? .065f : .035f;
            var noise = ps.noise; noise.enabled = loop; noise.strength = .12f; noise.frequency = 1.7f; noise.scrollSpeed = .3f; noise.quality = ParticleSystemNoiseQuality.Low;
            var color = ps.colorOverLifetime; color.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(new Color(.4f,.5f,1),1) },
                new[] {new GradientAlphaKey(.85f,0),new GradientAlphaKey(.55f,.25f),new GradientAlphaKey(0,1)}); color.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,1,1,.1f));
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = material; r.sortingOrder = 27;
            r.renderMode = ParticleSystemRenderMode.Billboard; r.maxParticleSize = .08f;
            return ps;
        }

        private static Material Material(string name, Texture2D texture, Shader shader, bool alpha = false, float flow = 0)
        {
            var path = Spell + "Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(shader) { name = name }; m.mainTexture = texture;
            m.SetFloat("_DstBlend", alpha ? 10 : 1); m.SetFloat("_Flow", flow);
            AssetDatabase.CreateAsset(m, path); return m;
        }

        private static Texture2D Mask(string name, int mode)
        {
            var path = Spell + "Textures/" + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
            const int n = 128; var tex = new Texture2D(n,n,TextureFormat.RGBA32,false);
            for (var y=0;y<n;y++) for (var x=0;x<n;x++)
            {
                float u=(x+.5f)/n*2-1, v=(y+.5f)/n*2-1, r=Mathf.Sqrt(u*u+v*v), a;
                switch(mode)
                {
                    case 0: a=Mathf.Pow(Mathf.Clamp01(1-r*r),2)*Mathf.Clamp01((1-r)*12); break;
                    case 1: a=Mathf.Pow(Mathf.Clamp01(1-r),3); break;
                    case 2: a=Mathf.Pow(Mathf.Clamp01(1-Mathf.Abs(v)),2); break;
                    case 3: a=Mathf.Exp(-Mathf.Pow((r-.78f)/.045f,2)); break;
                    default: a=Mathf.Pow(Mathf.Clamp01(1-r),1.8f)*Mathf.Lerp(.25f,1,Mathf.PerlinNoise(u*3+5,v*3+7)); break;
                }
                tex.SetPixel(x,y,new Color(1,1,1,a));
            }
            tex.Apply(); File.WriteAllBytes(path,tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode=TextureWrapMode.Clamp; importer.mipmapEnabled=false; importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
#endif
