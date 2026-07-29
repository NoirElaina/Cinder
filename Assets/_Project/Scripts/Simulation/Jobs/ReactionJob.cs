using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cinder.Simulation.Jobs
{
    /// <summary>
    /// 化学反应 Job：每格只查 右/下 两个邻居（同一对每 tick 只判定一次），
    /// 命中反应表且概率通过则同时替换双方物质。
    /// 处理全部格子——休眠区块内的静态岩浆池也必须持续反应。
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct ReactionJob : IJob
    {
        public NativeArray<Cell> Cells;
        [ReadOnly] public NativeArray<ReactionRule> Reactions;
        [ReadOnly] public NativeArray<MaterialProps> Mats;
        public NativeArray<int> Moved;
        public int Width;
        public int Height;
        public int ChunksX;
        public int TableCapacity;
        public uint Tick;
        public uint Seed;

        public void Execute()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (Cells[i].MaterialId == BuiltinMaterials.Empty) continue;
                    TryReact(x, y, i, x + 1, y);
                    TryReact(x, y, i, x, y + 1);
                }
            }
        }

        void TryReact(int x, int y, int i, int nx, int ny)
        {
            if (nx >= Width || ny >= Height) return;
            int j = ny * Width + nx;
            Cell a = Cells[i];
            Cell b = Cells[j];
            if (b.MaterialId == BuiltinMaterials.Empty) return;

            ReactionRule rule = Reactions[a.MaterialId * TableCapacity + b.MaterialId];
            if (rule.Exists == 0) return;
            if (((SimHash.Hash(x, y, Tick, Seed) >> 2) & 0xFFu) >= rule.Chance) return;

            if (rule.OutA != a.MaterialId) Cells[i] = Product(rule.OutA, x, y);
            if (rule.OutB != b.MaterialId) Cells[j] = Product(rule.OutB, nx, ny);

            int ci = (y >> SimCoords.ChunkShift) * ChunksX + (x >> SimCoords.ChunkShift);
            Moved[ci]++;
            int cj = (ny >> SimCoords.ChunkShift) * ChunksX + (nx >> SimCoords.ChunkShift);
            if (cj != ci) Moved[cj]++;
        }

        Cell Product(ushort id, int x, int y) => new Cell
        {
            MaterialId = id,
            State = Mats[id].BaseLife,
            Variant = (byte)(SimHash.Hash(x, y, Tick, Seed) & 3u),
            Flags = Cell.FlagMoved,
        };
    }
}
