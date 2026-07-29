using Cinder.Simulation;
using Cinder.Simulation.Channels;

namespace Cinder.Game.Effects
{
    /// <summary>
    /// IEffectWorld 的模拟实现：架在 SimulationWindow（格子读写）与
    /// ThermalChannel（温度变化，可空）之上。物质表热替换后由宿主
    /// 重新赋值 Table。
    /// </summary>
    public sealed class SimEffectWorld : IEffectWorld
    {
        /// <summary>不可破坏约定的密度阈值（基岩 = 255）。</summary>
        public const byte IndestructibleDensity = 255;

        readonly SimulationWindow window;
        readonly ThermalChannel thermal;
        readonly uint seed;

        public SimEffectWorld(SimulationWindow window, ThermalChannel thermal,
            MaterialTable table, uint seed)
        {
            this.window = window;
            this.thermal = thermal;
            Table = table;
            this.seed = seed;
        }

        /// <summary>物质表（热插拔物质重建后由宿主换上新表）。</summary>
        public MaterialTable Table { get; set; }

        public bool ContainsCell(int worldX, int worldY) => window.ContainsCell(worldX, worldY);

        public ushort GetMaterial(int worldX, int worldY) =>
            window.ContainsCell(worldX, worldY)
                ? window.GetCell(worldX, worldY).MaterialId
                : BuiltinMaterials.Empty;

        public void SetMaterial(int worldX, int worldY, ushort materialId)
        {
            if (!window.ContainsCell(worldX, worldY)) return;
            window.SetCell(worldX, worldY, Cell.Of(
                materialId,
                SimHash.Variant(worldX, worldY, seed),
                Table[materialId].BaseLife));
        }

        public MaterialProps PropsOf(ushort materialId) => Table[materialId];

        public void AddHeat(int worldX, int worldY, int deltaK)
        {
            if (thermal == null || !window.ContainsCell(worldX, worldY)) return;
            thermal.AddHeat(window.FlatIndexOf(worldX, worldY), deltaK);
        }

        public bool IsIndestructible(ushort materialId) =>
            Table[materialId].Density >= IndestructibleDensity;
    }
}
