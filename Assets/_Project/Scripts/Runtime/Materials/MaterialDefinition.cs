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

        public ushort MaterialId => (ushort)Mathf.Clamp(Id, 0, MaterialTable.Capacity - 1);

        public MaterialProps ToProps() => new MaterialProps
        {
            Type = Type,
            Density = (byte)Mathf.Clamp(Density, 0, 255),
            Fluidity = (byte)Mathf.Clamp(Fluidity, 0, 255),
            Flammability = (byte)Mathf.Clamp(Flammability, 0, 255),
            BaseLife = (byte)Mathf.Clamp(BaseLife, 0, 255),
        };
    }
}
