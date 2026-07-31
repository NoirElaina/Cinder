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

            [Tooltip("A 每次反应消耗的 State 预算；0 = 立即转变，>0 = 渐进消耗（配合物质 BaseLife 作预算）")]
            [Range(0, 255)] public int CostA;
            [Tooltip("B 每次反应消耗的 State 预算；0 = 立即转变，>0 = 渐进消耗")]
            [Range(0, 255)] public int CostB;
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

        /// <summary>每种物质烘培的 GPU 色带数（暗 -> 亮）。</summary>
        public const int PaletteBands = 8;

        uint[] gpuPalettes = new uint[MaterialTable.Capacity * PaletteBands];
        uint[] gpuParams = new uint[MaterialTable.Capacity];

        /// <summary>GPU 调色板：[id * PaletteBands + band] = RGBA8（低位 R）。</summary>
        public uint[] GpuPalettes => gpuPalettes;

        /// <summary>GPU 渲染参数：[0..3] 类型 | [4..11] 自发光 | [12..19] 颗粒 | [20..27] 边光。</summary>
        public uint[] GpuParams => gpuParams;

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
                byte costA = (byte)Mathf.Clamp(e.CostA, 0, 255);
                byte costB = (byte)Mathf.Clamp(e.CostB, 0, 255);
                Table.SetReaction(a, b, (byte)Mathf.RoundToInt(Mathf.Clamp01(e.Chance) * 255f), outA, outB, costA, costB);
            }

            BakeGpuTables();
            Rebuilt?.Invoke();
        }

        /// <summary>
        /// 烘培 GPU 视觉表：把资产里的少量调色板色按亮度排序后扩成 8 个色带
        /// （暗 -> 亮，着色器用团块噪声选带 + 颗粒抖动），并从物理属性派生
        /// 渲染参数（自发光与 SelfTempK 联动，颗粒/边光按物质类型）。
        /// </summary>
        void BakeGpuTables()
        {
            gpuPalettes = new uint[MaterialTable.Capacity * PaletteBands];
            gpuParams = new uint[MaterialTable.Capacity];

            for (int id = 0; id < MaterialTable.Capacity; id++)
            {
                Color32[] src = palettes[id];
                if (src == null || src.Length == 0) continue;

                // 按亮度升序，色带索引即明度索引
                var sorted = (Color32[])src.Clone();
                Array.Sort(sorted, (a, b) =>
                    (a.r * 3 + a.g * 6 + a.b).CompareTo(b.r * 3 + b.g * 6 + b.b));

                for (int band = 0; band < PaletteBands; band++)
                {
                    Color32 c = sorted[band * sorted.Length / PaletteBands];
                    float shade = 0.80f + 0.055f * band; // 0.80 .. 1.19
                    uint r = (uint)Mathf.Min(255, Mathf.RoundToInt(c.r * shade));
                    uint g = (uint)Mathf.Min(255, Mathf.RoundToInt(c.g * shade));
                    uint bb = (uint)Mathf.Min(255, Mathf.RoundToInt(c.b * shade));
                    gpuPalettes[id * PaletteBands + band] = r | (g << 8) | (bb << 16) | 0xFF000000u;
                }

                MaterialProps p = Table[(ushort)id];
                uint kind = (uint)p.Type;
                uint emission = p.Type == MatterType.Fire ? 255u
                    : p.SelfTempK > 600 ? (uint)Mathf.Min(255, (p.SelfTempK - 600) * 3 / 10) : 0u;
                uint grain = p.Type switch
                {
                    MatterType.Powder => 90u,
                    MatterType.StaticSolid => 60u,
                    MatterType.Liquid => 20u,
                    _ => 0u,
                };
                uint edge = p.Type switch
                {
                    MatterType.StaticSolid => 85u,
                    MatterType.Powder => 60u,
                    MatterType.Liquid => 120u,
                    _ => 0u,
                };
                gpuParams[id] = kind | (emission << 4) | (grain << 12) | (edge << 20);
            }
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
