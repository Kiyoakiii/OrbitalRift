using UnityEngine;

namespace OrbitalRift
{
    [CreateAssetMenu(menuName = "Orbital Rift/Space Visual Profile")]
    public sealed class SpaceVisualProfile : ScriptableObject
    {
        public Color primary = new Color(.12f, .38f, .8f);
        public Color secondary = new Color(.08f, .15f, .38f);
        public Color spark = new Color(.48f, .88f, 1f);
        [Range(.5f, 3f)] public float cloudScale = 1f;
        [Range(.5f, 3f)] public float density = 1.5f;
        [Range(-2f, 2f)] public float shear;
    }
}
