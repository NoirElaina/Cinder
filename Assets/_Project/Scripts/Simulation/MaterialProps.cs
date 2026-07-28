using System;
using Unity.Collections;

namespace Cinder.Simulation
{
    /// <summary>物质的物理行为类别。</summary>
    public enum MatterType : byte
    {
        Empty = 0,
        StaticSolid = 1,
        Powder = 2,
        Liquid = 3,
        Gas = 4,
        Fire = 5,
    }

    /// <summary>Burst 直读的物质属性（blittable）。颜色等渲染数据不在此处。</summary>
    public struct MaterialProps
    {
        public MatterType Type;

        /// <summary>0..255，密度大的在流体中下沉。</summary>
        public byte Density;

        /// <summary>0..255，液体/气体的水平流动倾向（作为概率权重）。</summary>
        public byte Fluidity;

        /// <summary>0..255，被火焰点燃的概率权重，0 = 不可燃。</summary>
        public byte Flammability;

        /// <summary>放置/点燃时的初始 State（如火焰寿命），0 = 不用。</summary>
        public byte BaseLife;
    }

    /// <summary>内置物质 Id。自定义热插拔物质请使用 >= CustomBase 的 Id。</summary>
    public static class BuiltinMaterials
    {
        public const ushort Empty = 0;
        public const ushort Bedrock = 1;
        public const ushort Rock = 2;
        public const ushort Dirt = 3;
        public const ushort Sand = 4;
        public const ushort Water = 5;
        public const ushort Wood = 6;
        public const ushort Fire = 7;

        public const ushort CustomBase = 16;
    }

    /// <summary>
    /// 确定性哈希，模拟的全部"随机"来源。同参数必得同结果，保证可复现、可单测。
    /// </summary>
    public static class SimHash
    {
        public static uint Hash(int x, int y, uint tick, uint seed)
        {
            uint h = (uint)x * 374761393u
                   + (uint)y * 668265263u
                   + tick * 2246822519u
                   + seed * 1013904223u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        public static byte Variant(int x, int y, uint seed) =>
            (byte)(Hash(x, y, 0u, seed) & 0xFF);
    }

    /// <summary>
    /// 物质属性查找表，按 MaterialId 索引。由 ScriptableObject 层烘焙，
    /// 运行时增删物质 = 重建本表，引擎代码零改动。
    /// </summary>
    public sealed class MaterialTable : IDisposable
    {
        public const int Capacity = 256;

        NativeArray<MaterialProps> props;

        public MaterialTable()
        {
            props = new NativeArray<MaterialProps>(Capacity, Allocator.Persistent);
        }

        public NativeArray<MaterialProps> Native => props;

        public MaterialProps this[ushort id] => props[id];

        public void Set(ushort id, in MaterialProps value) => props[id] = value;

        public void Clear(ushort id) => props[id] = default;

        public void Dispose()
        {
            if (props.IsCreated) props.Dispose();
        }

        /// <summary>内置物质的默认属性，测试与默认数据库共用同一份定义。</summary>
        public static MaterialTable CreateBuiltin()
        {
            var table = new MaterialTable();
            table.Set(BuiltinMaterials.Bedrock, new MaterialProps
                { Type = MatterType.StaticSolid, Density = 255 });
            table.Set(BuiltinMaterials.Rock, new MaterialProps
                { Type = MatterType.StaticSolid, Density = 200 });
            table.Set(BuiltinMaterials.Dirt, new MaterialProps
                { Type = MatterType.StaticSolid, Density = 180 });
            table.Set(BuiltinMaterials.Sand, new MaterialProps
                { Type = MatterType.Powder, Density = 160 });
            table.Set(BuiltinMaterials.Water, new MaterialProps
                { Type = MatterType.Liquid, Density = 100, Fluidity = 220 });
            table.Set(BuiltinMaterials.Wood, new MaterialProps
                { Type = MatterType.StaticSolid, Density = 150, Flammability = 180 });
            table.Set(BuiltinMaterials.Fire, new MaterialProps
                { Type = MatterType.Fire, Density = 5, Fluidity = 160, BaseLife = 40 });
            return table;
        }
    }
}
