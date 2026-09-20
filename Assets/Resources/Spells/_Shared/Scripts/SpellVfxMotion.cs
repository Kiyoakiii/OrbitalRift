using UnityEngine;

namespace OrbitalRift
{
    // Optional presentation extension; travel, impact and pooling stay shared.
    public abstract class SpellVfxMotion : MonoBehaviour
    {
        public abstract void Begin(SpellVfxProfile profile, float phase);
        public abstract void Tick(float age, float dt);
        public abstract void End();
        public virtual void SetLayerMask(int mask) { }
    }
}
