using System;
using System.Collections.Generic;
using Cinder.Simulation;
using UnityEngine;

namespace Cinder.Runtime.Materials
{
    /// <summary>
    /// 物质注册表（热插拔验证）：Register/Unregister 随时增删物质并重建
    /// 原生查找表，Rebuilt 事件通知模拟引擎热替换。注销后该 Id 按 Empty 处理。
    /// </summary>
    [CreateAssetMenu(menuName = "Cinder/Material Database", fileName = "MaterialDatabase")]
    public sealed class MaterialDatabase : ScriptableObject
    {
        /// <summary>双格反应条目（可在 Inspector 里编辑）。新增反应 = 加一行，零代码。</summary>
        [Serializable]
        public class ReactionEntry
        {
            public MaterialDefinition A;
            public MaterialDefinition B;
            [Range(0f, 1f)] public float Chance = 1f;

            [Tooltip("反应后 A 变成（空 = 保持不变）")]
            public MaterialDefinition OutA;
            [Tooltip("反应后 B 变成（空 = 保持不变）")]
            public MaterialDefinition OutB;

            [Tooltip("勾选则反应消耗 A")]
            public bool ConsumeA;
            [Tooltip("勾选则反应消耗 B")]
            public bool ConsumeB;
        }

        [SerializeField] List<MaterialDefinition> materials = new List<MaterialDefinition>();
        [SerializeField] List<ReactionEntry> reactions = new List<ReactionEntry>();

        public IReadOnlyList<MaterialDefinition> Materials => materials;
        public IReadOnlyList<ReactionEntry> Reactions => reactions;

        /// <summary>当前生效的原生物质表（最近一次 Rebuild 的结果）。</summary>
        public MaterialTable Table { get; private set; }

        /// <summary>表重建后触发，模拟引擎应替换其 Table 引用。</summary>
        public event Action Rebuilt;

        Color32[][] palettes = new Color32[MaterialTable.Capacity][];
        string[] names = new string[MaterialTable.Capacity];

        public void Rebuild()
        {
            Table?.Dispose();
            Table = new MaterialTable();
            palettes = new Color32[MaterialTable.Capacity][];
            names = new string[MaterialTable.Capacity];

            foreach (MaterialDefinition def in materials)
            {
                if (def == null) continue;
                ushort id = def.MaterialId;
                Table.Set(id, def.ToProps());
                palettes[id] = def.Palette;
                names[id] = def.DisplayName;
            }

            foreach (ReactionEntry e in reactions)
            {
                if (e == null || e.A == null || e.B == null) continue;
                ushort a = e.A.MaterialId;
                ushort b = e.B.MaterialId;
                ushort outA = e.ConsumeA ? BuiltinMaterials.Empty : (e.OutA != null ? e.OutA.MaterialId : a);
                ushort outB = e.ConsumeB ? BuiltinMaterials.Empty : (e.OutB != null ? e.OutB.MaterialId : b);
                Table.SetReaction(a, b, (byte)Mathf.RoundToInt(Mathf.Clamp01(e.Chance) * 255f), outA, outB);
            }
            Rebuilt?.Invoke();
        }

        /// <summary>运行时挂载一个物质模块。</summary>
        public bool Register(MaterialDefinition def)
        {
            if (def == null || materials.Contains(def)) return false;
            materials.Add(def);
            Rebuild();
            return true;
        }

        /// <summary>运行时卸载一个物质模块。</summary>
        public bool Unregister(MaterialDefinition def)
        {
            if (def == null || !materials.Remove(def)) return false;
            Rebuild();
            return true;
        }

        public MaterialDefinition GetById(ushort id)
        {
            foreach (MaterialDefinition def in materials)
                if (def != null && def.MaterialId == id) return def;
            return null;
        }

        public Color32 GetColor(ushort id, byte variant)
        {
            if (id == BuiltinMaterials.Empty) return new Color32(0, 0, 0, 0);
            Color32[] palette = palettes[id];
            if (palette == null || palette.Length == 0) return new Color32(255, 0, 255, 255);
            Color32 c = palette[variant % palette.Length];
            c.a = 255;
            return c;
        }

        public string GetName(ushort id) => names[id] ?? $"#{id}";

        /// <summary>释放原生查找表（可重复调用）。资产常驻内存，
        /// 使用方（如 WorldController）应在退出 Play 时调用以避免 NativeArray 泄漏。</summary>
        public void DisposeTable()
        {
            Table?.Dispose();
            Table = null;
        }

        void OnDestroy()
        {
            DisposeTable();
        }
    }
}
