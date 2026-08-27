using System.Collections.Generic;
using UnityEngine;

namespace OrbitalRift
{
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform parent;
        private readonly Stack<T> inactive = new Stack<T>();

        public ObjectPool(T prefab, Transform parent, int initialCount)
        {
            this.prefab = prefab;
            this.parent = parent;
            for (var i = 0; i < initialCount; i++) Release(Create());
        }

        public T Get()
        {
            var item = inactive.Count > 0 ? inactive.Pop() : Create();
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            item.gameObject.SetActive(false);
            item.transform.SetParent(parent, false);
            inactive.Push(item);
        }

        private T Create()
        {
            var item = Object.Instantiate(prefab, parent);
            item.gameObject.SetActive(false);
            return item;
        }
    }
}
