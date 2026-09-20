using UnityEngine;

namespace OrbitalRift
{
    // Playback driver for the existing Sandbox panel, not an editor or gameplay system.
    public sealed class SpellVfxPreview : MonoBehaviour
    {
        private SpellVfxPool pool;
        private SpellProjectileVfx template;
        private SpellVfxProfile original, previewProfile;
        private readonly Projectile[] owners = new Projectile[3];
        private float age;
        private bool running, impacted;
        public SpellProjectileVfx SourcePrefab { get; private set; }
        public SpellVfxPool Pool => pool;

        public void Configure(SpellProjectileVfx prefab)
        {
            if (pool != null) return;
            SourcePrefab=prefab; original=prefab.Profile;
            previewProfile=Instantiate(original); previewProfile.name=original.name+" (preview only)";
            template=Instantiate(prefab,transform); template.gameObject.SetActive(false); template.Profile=previewProfile;
            pool=new GameObject("Preview pool").AddComponent<SpellVfxPool>(); pool.transform.SetParent(transform,false);
            pool.ProjectilePrefab=template; pool.Prewarm(template,3,3);
            for(var i=0;i<owners.Length;i++)
            {
                var go=new GameObject("Preview projectile "+i);go.transform.SetParent(transform,false);
                go.AddComponent<SpriteRenderer>();owners[i]=go.AddComponent<Projectile>();go.SetActive(false);
            }
            SetLayer(transform,31);
        }

        public void Show(float dt,float scale,float brightness,float glow,int mask)
        {
            if(pool==null)return;
            gameObject.SetActive(true);
            previewProfile.CoreBrightness=original.CoreBrightness*brightness;
            previewProfile.GlowBrightness=original.GlowBrightness*brightness*glow;
            previewProfile.TrailBrightness=original.TrailBrightness*brightness;
            previewProfile.TrailWidth=original.TrailWidth*scale;
            previewProfile.ImpactSize=original.ImpactSize*scale;
            previewProfile.ImpactBrightness=original.ImpactBrightness*brightness;
            if(!running || age>=3.5f)
            {
                pool.Clear();age=0;impacted=false;running=true;
                for(var i=0;i<owners.Length;i++)
                {
                    var p=owners[i];p.gameObject.SetActive(true);
                    p.ResetProjectile(new Vector2(-2.1f,-.82f+(i-1)*.64f),Vector2.right*4.1f,false,Color.white,DamageElement.Fire,0);
                    pool.Attach(p,template);
                    p.SpellVfx.transform.localScale=Vector3.one*scale;
                }
            }
            age+=dt;
            foreach(var p in owners)
            {
                if(p.SpellVfx==null)continue;
                p.transform.position=new Vector3(-2.1f+Mathf.Min(age,2)*4.1f,p.transform.position.y,0);
                p.SpellVfx.transform.localScale=Vector3.one*scale;
                if(age>=2 && !impacted)pool.Hit(p,p.transform.position);
            }
            if(age>=2)impacted=true;
            pool.Tick(dt);pool.SetLayerMask(mask);
        }

        public void Hide()
        {
            if(!running)return;
            pool.Clear();foreach(var p in owners)p.gameObject.SetActive(false);
            running=false;gameObject.SetActive(false);
        }
        private void OnDestroy(){if(previewProfile!=null)Destroy(previewProfile);}
        private static void SetLayer(Transform root,int layer)
        { root.gameObject.layer=layer;foreach(Transform child in root)SetLayer(child,layer); }
    }
}
