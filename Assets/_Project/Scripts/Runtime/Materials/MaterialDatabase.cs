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
        [SerializeField] List<MaterialDefinition> materials = new List<MaterialDefinition>();

        public IReadOnlyList<MaterialDefinition> Materials => materials;

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

        /// <summary>纯代码构建内置物质库，查看器零资产可跑。</summary>
        public static MaterialDatabase CreateDefault()
        {
            var db = CreateInstance<MaterialDatabase>();
            db.materials = new List<MaterialDefinition>
            {
                Make(BuiltinMaterials.Bedrock, "基岩", MatterType.StaticSolid, 255,
                    colors: C(40, 40, 48, 34, 34, 42)),
                Make(BuiltinMaterials.Rock, "岩石", MatterType.StaticSolid, 200,
                    colors: C(110, 105, 100, 95, 90, 88, 122, 116, 106)),
                Make(BuiltinMaterials.Dirt, "泥土", MatterType.StaticSolid, 180,
                    colors: C(120, 85, 55, 105, 72, 45, 132, 96, 62)),
                Make(BuiltinMaterials.Sand, "沙", MatterType.Powder, 160,
                    colors: C(210, 185, 130, 200, 175, 120, 222, 197, 142)),
                Make(BuiltinMaterials.Water, "水", MatterType.Liquid, 100, fluidity: 220,
                    colors: C(45, 110, 220, 40, 100, 210, 60, 125, 235)),
                Make(BuiltinMaterials.Wood, "木头", MatterType.StaticSolid, 150, flammability: 180,
                    colors: C(110, 70, 40, 95, 60, 32, 124, 80, 46)),
                Make(BuiltinMaterials.Fire, "火焰", MatterType.Fire, 5, fluidity: 160, baseLife: 40,
                    colors: C(250, 180, 40, 245, 120, 20, 255, 220, 90, 235, 80, 10)),
            };
            db.Rebuild();
            return db;
        }

        static Color32[] C(params int[] rgb)
        {
            var colors = new Color32[rgb.Length / 3];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color32((byte)rgb[i * 3], (byte)rgb[i * 3 + 1], (byte)rgb[i * 3 + 2], 255);
            return colors;
        }

        static MaterialDefinition Make(ushort id, string displayName, MatterType type,
            int density, int fluidity = 0, int flammability = 0, int baseLife = 0,
            Color32[] colors = null)
        {
            var def = CreateInstance<MaterialDefinition>();
            def.Id = id;
            def.DisplayName = displayName;
            def.Type = type;
            def.Density = density;
            def.Fluidity = fluidity;
            def.Flammability = flammability;
            def.BaseLife = baseLife;
            def.Palette = colors ?? new Color32[] { new Color32(255, 0, 255, 255) };
            return def;
        }

        void OnDestroy()
        {
            Table?.Dispose();
        }
    }
}
