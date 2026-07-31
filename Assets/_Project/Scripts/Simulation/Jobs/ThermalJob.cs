using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cinder.Simulation.Jobs
{
    /// <summary>
    /// 温度 Job：自热物质恒温在 SelfTempK；其余格与四邻均值按导热系数交换，
    /// 并缓慢回落到环境温度。随后执行单格相变（数据驱动）：
    /// 达到燃点/熔点/沸点/凝固点即转变为对应物质。
    /// 调度：先整块拷贝温度场（双缓冲每格必须有值），再只在醒块内求解；
    /// 休眠块温度冻结在当前值，靠 FullPass（每 8 tick）全量补扫传入热量并触发相变。
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct ThermalJob : IJob
    {
        public NativeArray<Cell> Cells;
        [ReadOnly] public NativeArray<MaterialProps> Mats;
        public NativeArray<int> Moved;
        [ReadOnly] public NativeArray<short> TempRead;
        public NativeArray<short> TempWrite;
        [ReadOnly] public NativeArray<byte> Awake;
        public int Width;
        public int Height;
        public int ChunksX;
        public uint Tick;
        public uint Seed;
        public short AmbientK;
        /// <summary>非 0 = 本 tick 全量求解（含休眠块）。</summary>
        public byte FullPass;

        public void Execute()
        {
            NativeArray<short>.Copy(TempRead, TempWrite);
            for (int y = 0; y < Height; y++)
            {
                int chunkRow = (y >> SimCoords.ChunkShift) * ChunksX;
                for (int cx = 0; cx < ChunksX; cx++)
                {
                    if (FullPass == 0 && Awake[chunkRow + cx] == 0) continue;
                    int x0 = cx << SimCoords.ChunkShift;
                    int x1 = x0 + SimCoords.ChunkSize;
                    for (int x = x0; x < x1; x++)
                    {
                        Solve(x, y);
                    }
                }
            }
        }

        void Solve(int x, int y)
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
                // 四邻热量交换：带符号向上取整，避免弱梯度被整数除法吞掉而卡死
                int diff = avg - cur;
                int exch = diff >= 0 ? (diff * k + 254) / 255 : (diff * k - 254) / 255;
                int next = cur + exch;
                // 环境回落：同样的带符号取整，保证温度最终收敛到环境温度，
                // 而不是在 |ΔT|<512K 时被截断为 0 而永久停在环境温度上方。
                int pull = AmbientK - next;
                int regress = pull >= 0 ? (pull + 511) / 512 : (pull - 511) / 512;
                next += regress;
                t = (short)next;
            }
            TempWrite[i] = t;

            // 相变（单格决策，全部查表）。熔/沸/凝固是确定性的；
            // 唯独点燃按易燃度概率发生——整片可燃物到温后陆续起火，
            // 而不是同一帧全部闪燃。
            ushort into = 0;
            if (p.BurnsInto != 0 && p.IgnitePointK > 0 && t >= (short)p.IgnitePointK
                && ((SimHash.Hash(x, y, Tick, Seed) >> 6) & 0xFFu) < p.Flammability) into = p.BurnsInto;
            else if (p.BoilsInto != 0 && p.BoilPointK > 0 && t >= (short)p.BoilPointK) into = p.BoilsInto;
            else if (p.MeltsInto != 0 && p.MeltPointK > 0 && t >= (short)p.MeltPointK) into = p.MeltsInto;
            else if (p.FreezesInto != 0 && p.FreezePointK > 0 && t <= (short)p.FreezePointK) into = p.FreezesInto;
            if (into == 0) return;

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
