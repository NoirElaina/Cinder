using Cinder.Simulation;
using UnityEngine;

namespace Cinder.Runtime.Materials
{
    /// <summary>
    /// 物质定义资产：热插拔的最小单元。新增物质 = 新建本资产并注册进
    /// MaterialDatabase，无需改动任何引擎代码。
    /// </summary>
    [CreateAssetMenu(menuName = "Cinder/Material Definition", fileName = "MaterialDefinition")]
    public sealed class MaterialDefinition : ScriptableObject
    {
        [Tooltip("自定义物质请用 >= 16 的 Id，0..15 保留给内置物质")]
        [Min(0)] public int Id = BuiltinMaterials.CustomBase;

        public string DisplayName = "New Material";
        public MatterType Type = MatterType.Powder;
        [Range(0, 255)] public int Density = 100;
        [Range(0, 255)] public int Fluidity;
        [Range(0, 255)] public int Flammability;
        [Range(0, 255)] public int BaseLife;

        [Tooltip("渲染调色板，按 Cell.Variant 取色")]
        public Color32[] Palette = { new Color32(255, 0, 255, 255) };

        [Header("热学（温度通道）")]
        [Range(0, 255)] public int Conductivity;

        [Tooltip("自发热温度 K，0 = 无热源（火焰/岩浆）")]
        [Range(0, 3000)] public int SelfTempK;

        [Tooltip("点燃温度 K，达到且 BurnsInto 非空时转变")]
        [Range(0, 3000)] public int IgnitePointK;

        [Range(0, 3000)] public int MeltPointK;
        [Range(0, 3000)] public int BoilPointK;
        [Range(0, 3000)] public int FreezePointK;

        [Tooltip("燃烧后变成的物质（空 = 不发生）")]
        public MaterialDefinition BurnsInto;
        public MaterialDefinition MeltsInto;
        public MaterialDefinition BoilsInto;
        public MaterialDefinition FreezesInto;

        public ushort MaterialId => (ushort)Mathf.Clamp(Id, 0, MaterialTable.Capacity - 1);

        public MaterialProps ToProps() => new MaterialProps
        {
            Type = Type,
            Density = (byte)Mathf.Clamp(Density, 0, 255),
            Fluidity = (byte)Mathf.Clamp(Fluidity, 0, 255),
            Flammability = (byte)Mathf.Clamp(Flammability, 0, 255),
            BaseLife = (byte)Mathf.Clamp(BaseLife, 0, 255),
            Conductivity = (byte)Mathf.Clamp(Conductivity, 0, 255),
            SelfTempK = (ushort)Mathf.Clamp(SelfTempK, 0, 3000),
            IgnitePointK = (ushort)Mathf.Clamp(IgnitePointK, 0, 3000),
            MeltPointK = (ushort)Mathf.Clamp(MeltPointK, 0, 3000),
            BoilPointK = (ushort)Mathf.Clamp(BoilPointK, 0, 3000),
            FreezePointK = (ushort)Mathf.Clamp(FreezePointK, 0, 3000),
            BurnsInto = IdOf(BurnsInto),
            MeltsInto = IdOf(MeltsInto),
            BoilsInto = IdOf(BoilsInto),
            FreezesInto = IdOf(FreezesInto),
        };

        static ushort IdOf(MaterialDefinition def) => def != null ? def.MaterialId : (ushort)0;
    }
}
