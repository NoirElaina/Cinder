using System;
using System.Collections.Generic;

namespace Cinder.Core.Modules
{
    /// <summary>所有热插拔模块（武器/效果/角色/物品…）的统一契约。</summary>
    public interface IModule
    {
        /// <summary>全局唯一模块 Id，建议 "类别.名称" 命名（如 "effect.fire_trail"）。</summary>
        string ModuleId { get; }
    }

    /// <summary>
    /// 通用模块注册表：注册表模式。任何模块类型（武器/效果/角色…）各自持有一个
    /// 实例，运行时 Register/Unregister 即热插拔，事件通知所有监听方。
    /// 线程模型：仅主线程。
    /// </summary>
    public sealed class ModuleRegistry<T> where T : class, IModule
    {
        readonly Dictionary<string, T> byId = new Dictionary<string, T>();
        readonly List<T> ordered = new List<T>();

        public event Action<T> Registered;
        public event Action<T> Unregistered;

        public int Count => ordered.Count;

        /// <summary>按注册顺序排列的全部模块。</summary>
        public IReadOnlyList<T> All => ordered;

        public bool Register(T module)
        {
            if (module == null || string.IsNullOrEmpty(module.ModuleId)) return false;
            if (byId.ContainsKey(module.ModuleId)) return false;
            byId.Add(module.ModuleId, module);
            ordered.Add(module);
            Registered?.Invoke(module);
            return true;
        }

        public bool Unregister(string moduleId)
        {
            if (moduleId == null || !byId.TryGetValue(moduleId, out T module)) return false;
            byId.Remove(moduleId);
            ordered.Remove(module);
            Unregistered?.Invoke(module);
            return true;
        }

        public T Get(string moduleId) =>
            moduleId != null && byId.TryGetValue(moduleId, out T module) ? module : null;

        public bool Contains(string moduleId) =>
            moduleId != null && byId.ContainsKey(moduleId);

        public void Clear()
        {
            var snapshot = ordered.ToArray();
            byId.Clear();
            ordered.Clear();
            foreach (T module in snapshot) Unregistered?.Invoke(module);
        }
    }
}
