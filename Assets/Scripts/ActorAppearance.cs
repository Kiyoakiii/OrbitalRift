using System;
using System.Collections.Generic;
using UnityEngine;
namespace OrbitalRift
{
    [Serializable]
    public sealed class ActorAppearance
    {
        [Tooltip("Включает замену процедурного тела своим изображением.")] public bool Enabled;
        public Sprite Sprite;
        public Texture2D Texture;
        public Material Material;
        [Tooltip("Кадры из Sprite Editor или отдельные PNG; пустой массив = статичное изображение.")] public Sprite[] Frames = Array.Empty<Sprite>();
        [Min(0)] public float FramesPerSecond = 12;
        public bool Loop = true;
        public Color Tint = Color.white;
        [Min(.01f)] public float Size = 1;
        public float RotationSpeed, BobAmplitude, BobFrequency = 2, PulseAmplitude, PulseFrequency = 2;
        public Sprite AuraSprite;
        public Material AuraMaterial;
        public Color AuraColor = new Color(.3f,.7f,1,.3f);
        [Min(0)] public float AuraSize = 1.6f;
        static readonly Dictionary<Texture2D, Sprite> textures = new Dictionary<Texture2D, Sprite>();
        public Sprite Frame(float age)
        {
            if (Frames != null && Frames.Length > 0) { var i = Mathf.FloorToInt(age * FramesPerSecond); return Frames[Loop ? i % Frames.Length : Mathf.Min(i, Frames.Length-1)]; }
            if (Sprite != null) return Sprite;
            if (Texture == null) return null;
            if (!textures.TryGetValue(Texture, out var result)) { result = UnityEngine.Sprite.Create(Texture,new Rect(0,0,Texture.width,Texture.height),new Vector2(.5f,.5f),100); textures[Texture]=result; }
            return result;
        }
    }
    // Explicit ticks share gameplay pause and pooled lifetime; no Animator/timeScale mismatch.
    public sealed class ActorAppearanceView : MonoBehaviour
    {
        SpriteRenderer body, aura;
        ActorAppearance settings;
        public void Configure(ActorAppearance value, SpriteRenderer source)
        {
            settings = value;
            if (body == null) { var child = new GameObject("Configured image"); child.transform.SetParent(transform,false); body=child.AddComponent<SpriteRenderer>(); var glow=new GameObject("Configured aura"); glow.transform.SetParent(transform,false); aura=glow.AddComponent<SpriteRenderer>(); }
            body.enabled = aura.enabled = value != null && value.Enabled;
            if (!body.enabled) return;
            body.sharedMaterial = value.Material != null ? value.Material : source.sharedMaterial;
            body.sortingLayerID=source.sortingLayerID; body.sortingOrder=source.sortingOrder+1;
            aura.sharedMaterial=value.AuraMaterial != null ? value.AuraMaterial : source.sharedMaterial;
            aura.sortingLayerID=source.sortingLayerID; aura.sortingOrder=source.sortingOrder;
            Tick(0);
        }
        public void Tick(float age)
        {
            if (settings == null || !settings.Enabled || body == null) return;
            body.sprite=settings.Frame(age); body.color=settings.Tint;
            var parentScale=Mathf.Max(.0001f,Mathf.Abs(transform.lossyScale.x));
            var size=body.sprite != null ? Mathf.Max(body.sprite.bounds.size.x,body.sprite.bounds.size.y) : 1;
            body.transform.localScale=Vector3.one*(settings.Size*(1+Mathf.Sin(age*settings.PulseFrequency*Mathf.PI*2)*settings.PulseAmplitude)/size/parentScale);
            body.transform.localPosition=Vector3.up*(Mathf.Sin(age*settings.BobFrequency*Mathf.PI*2)*settings.BobAmplitude/parentScale);
            body.transform.localRotation=Quaternion.Euler(0,0,age*settings.RotationSpeed);
            aura.sprite=settings.AuraSprite; aura.enabled=aura.sprite != null; aura.color=settings.AuraColor;
            if(aura.enabled) aura.transform.localScale=Vector3.one*(settings.AuraSize/Mathf.Max(aura.sprite.bounds.size.x,aura.sprite.bounds.size.y)/parentScale);
        }
    }
}
