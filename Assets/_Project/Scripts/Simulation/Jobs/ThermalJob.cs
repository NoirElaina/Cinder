using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cinder.Simulation.Jobs
{
    /// <summary>
    /// 温度 Job：自热物质恒温在 SelfTempK；其余格与四邻均值按导热系数交换，
    /// 并缓慢回落到环境温度。随后执行单格相变（数据驱动）：
    /// 达到燃点/熔点/沸点/凝固点即转变为对应物质。
    /// 处理全部格子，让热量可以传入休眠区块并在相变时唤醒它。
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct ThermalJob : IJob
    {
        public NativeArray<Cell> Cells;
        [ReadOnly] public NativeArray<MaterialProps> Mats;
        public NativeArray<int> Moved;
        [ReadOnly] public NativeArray<short> TempRead;
        public NativeArray<short> TempWrite;
        public int Width;
        public int Height;
        public int ChunksX;
        public uint Tick;
        public uint Seed;
        public short AmbientK;

        public void Execute()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    Cell c = Cells[i];
                    MaterialProps p = Mats[c.MaterialId];

                    short t;
                    if (p.SelfTempK > 0)
                    {
                        t = (short)p.SelfTempK;
                    }
                    else
                    {
                        int sum = 0, n = 0;
                        if (x > 0) { sum += TempRead[i - 1]; n++; }
                        if (x < Width - 1) { sum += TempRead[i + 1]; n++; }
                        if (y > 0) { sum += TempRead[i - Width]; n++; }
                        if (y < Height - 1) { sum += TempRead[i + Width]; n++; }

                        // 空气也给一个基础导热，让热量能隔空缓慢传播
                        int k = c.MaterialId == BuiltinMaterials.Empty ? 40 : p.Conductivity;
                        int cur = TempRead[i];
                        int avg = n > 0 ? sum / n : cur;
                        int next = cur + (avg - cur) * k / 255;
                        next += (AmbientK - next) / 512;
                        t = (short)next;
                    }
                    TempWrite[i] = t;

                    // 相变（单格决策，全部查表）
                    ushort into = 0;
                    if (p.BurnsInto != 0 && p.IgnitePointK > 0 && t >= (short)p.IgnitePointK) into = p.BurnsInto;
                    else if (p.BoilsInto != 0 && p.BoilPointK > 0 && t >= (short)p.BoilPointK) into = p.BoilsInto;
                    else if (p.MeltsInto != 0 && p.MeltPointK > 0 && t >= (short)p.MeltPointK) into = p.MeltsInto;
                    else if (p.FreezesInto != 0 && p.FreezePointK > 0 && t <= (short)p.FreezePointK) into = p.FreezesInto;
                    if (into == 0) continue;

                    MaterialProps np = Mats[into];
                    Cells[i] = new Cell
                    {
                        MaterialId = into,
                        State = np.BaseLife,
                        Variant = (byte)(SimHash.Hash(x, y, Tick, Seed) & 3u),
                        Flags = Cell.FlagMoved,
                    };
                    Moved[(y >> SimCoords.ChunkShift) * ChunksX + (x >> SimCoords.ChunkShift)]++;
                    if (np.SelfTempK > 0) TempWrite[i] = (short)np.SelfTempK;
                }
            }
        }
    }
}
