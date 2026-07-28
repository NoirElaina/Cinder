using System.Collections.Generic;
using UnityEngine;

namespace Cinder.Runtime.World
{
    /// <summary>区块视图对象池，避免频繁创建/销毁纹理与 GameObject。</summary>
    public sealed class ChunkViewPool
    {
        readonly Transform parent;
        readonly Stack<ChunkView> pool = new Stack<ChunkView>();

        public ChunkViewPool(Transform parent)
        {
            this.parent = parent;
        }

        public ChunkView Get()
        {
            if (pool.Count > 0)
            {
                ChunkView reused = pool.Pop();
                reused.gameObject.SetActive(true);
                return reused;
            }
            var go = new GameObject("ChunkView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ChunkView>();
            view.Initialize();
            return view;
        }

        public void Release(ChunkView view)
        {
            if (view == null) return;
            view.gameObject.SetActive(false);
            pool.Push(view);
        }
    }
}
