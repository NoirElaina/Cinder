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

        /// <summary>0..255，导热系数：每 tick 与邻居均温的交换比例。</summary>
        public byte Conductivity;

        /// <summary>自发热温度（K），0 = 无热源。火焰/岩浆恒为该温度。</summary>
        public ushort SelfTempK;

        /// <summary>点燃温度（K），达到且 BurnsInto 非 0 时转变。</summary>
        public ushort IgnitePointK;

        /// <summary>熔点（K）。</summary>
        public ushort MeltPointK;

        /// <summary>沸点（K）。</summary>
        public ushort BoilPointK;

        /// <summary>凝固点（K），温度低于等于它时转变。</summary>
        public ushort FreezePointK;

        /// <summary>燃烧后变成的物质 Id，0 = 不发生。</summary>
        public ushort BurnsInto;

        /// <summary>熔化后变成的物质 Id，0 = 不发生。</summary>
        public ushort MeltsInto;

        /// <summary>沸腾后变成的物质 Id，0 = 不发生。</summary>
        public ushort BoilsInto;

        /// <summary>凝固后变成的物质 Id，0 = 不发生。</summary>
        public ushort FreezesInto;
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
        public const ushort Oil = 8;
        public const ushort Acid = 9;
        public const ushort Steam = 10;
        public const ushort Smoke = 11;
        public const ushort Lava = 12;
        public const ushort Ice = 13;

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
        NativeArray<ReactionRule> reactions;

        public MaterialTable()
        {
            props = new NativeArray<MaterialProps>(Capacity, Allocator.Persistent);
            reactions = new NativeArray<ReactionRule>(Capacity * Capacity, Allocator.Persistent);
        }

        public NativeArray<MaterialProps> Native => props;

        /// <summary>对称反应表，[a * Capacity + b] 与 [b * Capacity + a] 同时写入。</summary>
        public NativeArray<ReactionRule> Reactions => reactions;

        public MaterialProps this[ushort id] => props[id];

        public void Set(ushort id, in MaterialProps value) => props[id] = value;

        public void Clear(ushort id) => props[id] = default;

        /// <summary>
        /// 注册一条双向反应：A 遇 B 以 chance/255 概率反应。outA/outB 为产物
        /// （与自身同 Id = 不变）。costA/costB &gt; 0 时该反应物按 State 预算渐进消耗，
        /// 耗尽才变成对应 out（用于"酸腐蚀有上限"）。MatA/MatB 记录参与者，
        /// 让产物落格与方位无关。对称写入，双向查询等价。
        /// </summary>
        public void SetReaction(ushort a, ushort b, byte chance, ushort outA, ushort outB,
            byte costA = 0, byte costB = 0)
        {
            var rule = new ReactionRule
            {
                Exists = 1, Chance = chance,
                MatA = a, MatB = b, OutA = outA, OutB = outB,
                CostA = costA, CostB = costB,
            };
            reactions[a * Capacity + b] = rule;
            reactions[b * Capacity + a] = rule;
        }

        public void Dispose()
        {
            if (props.IsCreated) props.Dispose();
            if (reactions.IsCreated) reactions.Dispose();
        }

        /// <summary>内置物质的默认属性，测试与默认数据库共用同一份定义。</summary>
        public static MaterialTable CreateBuiltin()
        {
            var table = new MaterialTable();
            table.Set(BuiltinMaterials.Bedrock, new MaterialProps
                { Type = MatterType.StaticSolid, Density = 255, Conductivity = 90 });
            table.Set(BuiltinMaterials.Rock, new MaterialProps
                { Type = MatterType.StaticSolid, Density = 200, Conductivity = 90 });
            table.Set(BuiltinMaterials.Dirt, new MaterialProps
                { Type = MatterType.StaticSolid, Density = 180, Conductivity = 60 });
            table.Set(BuiltinMaterials.Sand, new MaterialProps
                { Type = MatterType.Powder, Density = 160, Conductivity = 70 });
            table.Set(BuiltinMaterials.Water, new MaterialProps
            {
                Type = MatterType.Liquid, Density = 100, Fluidity = 220, Conductivity = 120,
                BoilPointK = 373, BoilsInto = BuiltinMaterials.Steam,
                FreezePointK = 273, FreezesInto = BuiltinMaterials.Ice,
            });
            table.Set(BuiltinMaterials.Wood, new MaterialProps
            {
                Type = MatterType.StaticSolid, Density = 150, Flammability = 18, Conductivity = 40,
                IgnitePointK = 573, BurnsInto = BuiltinMaterials.Fire,
            });
            table.Set(BuiltinMaterials.Fire, new MaterialProps
            {
                Type = MatterType.Fire, Density = 5, Fluidity = 160, BaseLife = 40, Conductivity = 60,
                SelfTempK = 1073, BurnsInto = BuiltinMaterials.Smoke,
            });
            table.Set(BuiltinMaterials.Oil, new MaterialProps
            {
                Type = MatterType.Liquid, Density = 90, Fluidity = 200, Flammability = 60, Conductivity = 50,
                IgnitePointK = 520, BurnsInto = BuiltinMaterials.Fire,
            });
            table.Set(BuiltinMaterials.Acid, new MaterialProps
            {
                Type = MatterType.Liquid, Density = 110, Fluidity = 210, Conductivity = 100,
                // 腐蚀预算：每格酸可腐蚀的固体数，耗尽即消失（BaseLife 经笔刷初始化 State）
                BaseLife = 8,
            });
            table.Set(BuiltinMaterials.Steam, new MaterialProps
                { Type = MatterType.Gas, Density = 10, Fluidity = 180, Conductivity = 30 });
            table.Set(BuiltinMaterials.Smoke, new MaterialProps
                { Type = MatterType.Gas, Density = 15, Fluidity = 120, BaseLife = 150, Conductivity = 20 });
            table.Set(BuiltinMaterials.Lava, new MaterialProps
                { Type = MatterType.Liquid, Density = 200, Fluidity = 60, Conductivity = 100, SelfTempK = 1400 });
            table.Set(BuiltinMaterials.Ice, new MaterialProps
            {
                Type = MatterType.StaticSolid, Density = 92, Conductivity = 110,
                // 熔点 274K（≈1℃）：与水凝固点 273K 留 1K 死区避免抖动；
                // 环境温度 290K 高于它，冰在室温会自然融化回水（修复冰永不融化）。
                MeltPointK = 274, MeltsInto = BuiltinMaterials.Water,
            });

            // 内置反应（对称写入）：岩浆淬水成岩 + 蒸汽；酸腐蚀常规固体
            table.SetReaction(BuiltinMaterials.Lava, BuiltinMaterials.Water, 230,
                BuiltinMaterials.Rock, BuiltinMaterials.Steam);
            table.SetReaction(BuiltinMaterials.Lava, BuiltinMaterials.Ice, 230,
                BuiltinMaterials.Rock, BuiltinMaterials.Water);
            // 酸腐蚀固体：被腐蚀方立即消失（outB=Empty），酸自身按预算渐进消耗
            // （costA=1，耗尽成 Empty），因此酸有腐蚀上限，不会一路钻到基岩。
            table.SetReaction(BuiltinMaterials.Acid, BuiltinMaterials.Rock, 60,
                BuiltinMaterials.Empty, BuiltinMaterials.Empty, costA: 1);
            table.SetReaction(BuiltinMaterials.Acid, BuiltinMaterials.Dirt, 76,
                BuiltinMaterials.Empty, BuiltinMaterials.Empty, costA: 1);
            table.SetReaction(BuiltinMaterials.Acid, BuiltinMaterials.Sand, 76,
                BuiltinMaterials.Empty, BuiltinMaterials.Empty, costA: 1);
            table.SetReaction(BuiltinMaterials.Acid, BuiltinMaterials.Wood, 76,
                BuiltinMaterials.Empty, BuiltinMaterials.Empty, costA: 1);
            return table;
        }
    }
}
