using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cinder.Simulation.Jobs
{
    /// <summary>
    /// 落沙模拟：对 SimulationWindow 的平坦数组执行一个 tick。
    /// 单 Job 顺序扫描（Burst 向量化后 20 万格开销可忽略），
    /// 跨区块移动因共享同一数组而无竞态。
    /// 规约见 BurstGuardVerifier：struct、无托管字段、CompileSynchronously。
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct FallingSandJob : IJob
    {
        [ReadOnly] public NativeArray<Cell> Read;
        public NativeArray<Cell> Write;
        [ReadOnly] public NativeArray<MaterialProps> Mats;
        [ReadOnly] public NativeArray<byte> Awake;
        public NativeArray<int> Moved;

        public int Width;
        public int Height;
        public int ChunksX;
        public uint Tick;
        public uint Seed;
        public ushort FireId;

        public void Execute()
        {
            for (int i = 0; i < Read.Length; i++)
            {
                Cell c = Read[i];
                c.Flags = 0;
                Write[i] = c;
            }
            for (int i = 0; i < Moved.Length; i++) Moved[i] = 0;

            bool leftToRight = (Tick & 1u) == 0u;
            for (int y = 0; y < Height; y++)
            {
                if (leftToRight)
                {
                    for (int x = 0; x < Width; x++) Step(x, y);
                }
                else
                {
                    for (int x = Width - 1; x >= 0; x--) Step(x, y);
                }
            }
        }

        void Step(int x, int y)
        {
            int index = y * Width + x;
            int chunkIndex = ChunkIndexOf(x, y);
            if (Awake[chunkIndex] == 0) return;

            Cell c = Write[index];
            if (c.MaterialId == BuiltinMaterials.Empty) return;
            if ((c.Flags & Cell.FlagMoved) != 0) return;

            MaterialProps p = Mats[c.MaterialId];
            switch (p.Type)
            {
                case MatterType.Powder:
                    StepPowder(x, y, c, p);
                    break;
                case MatterType.Liquid:
                    StepFluid(x, y, c, p, -1);
                    break;
                case MatterType.Gas:
                    StepFluid(x, y, c, p, 1);
                    break;
                case MatterType.Fire:
                    StepFire(x, y, c, p);
                    break;
            }
        }

        void StepPowder(int x, int y, in Cell c, in MaterialProps p)
        {
            if (TryMove(x, y, x, y - 1, c, p)) return;
            uint h = SimHash.Hash(x, y, Tick, Seed);
            int first = (h & 1u) == 0u ? -1 : 1;
            if (TryMove(x, y, x + first, y - 1, c, p)) return;
            TryMove(x, y, x - first, y - 1, c, p);
        }

        void StepFluid(int x, int y, in Cell c, in MaterialProps p, int dirY)
        {
            if (TryMove(x, y, x, y + dirY, c, p)) return;
            uint h = SimHash.Hash(x, y, Tick, Seed);
            int first = (h & 1u) == 0u ? -1 : 1;
            if (TryMove(x, y, x + first, y + dirY, c, p)) return;
            if (TryMove(x, y, x - first, y + dirY, c, p)) return;

            // 水平扩散，Fluidity 作为概率权重
            if (((h >> 8) & 0xFFu) < p.Fluidity)
            {
                int dir = ((h >> 16) & 1u) == 0u ? -1 : 1;
                if (TryMove(x, y, x + dir, y, c, p)) return;
                TryMove(x, y, x - dir, y, c, p);
            }
        }

        void StepFire(int x, int y, in Cell c, in MaterialProps p)
        {
            int index = y * Width + x;
            uint h = SimHash.Hash(x, y, Tick, Seed);

            // 邻水概率熄灭
            if (TouchingType(x, y, MatterType.Liquid) && ((h >> 3) & 1u) == 0u)
            {
                Write[index] = default;
                Moved[ChunkIndexOf(x, y)]++;
                return;
            }

            int life = c.State - 1;
            if (life <= 0)
            {
                Write[index] = default;
                Moved[ChunkIndexOf(x, y)]++;
                return;
            }

            // 点燃邻居
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
                    int nIndex = ny * Width + nx;
                    Cell t = Write[nIndex];
                    if (t.MaterialId == BuiltinMaterials.Empty) continue;
                    MaterialProps tp = Mats[t.MaterialId];
                    if (tp.Flammability == 0) continue;
                    if (((SimHash.Hash(nx, ny, Tick, Seed) >> 4) & 0xFFu) >= tp.Flammability) continue;

                    Write[nIndex] = new Cell
                    {
                        MaterialId = FireId,
                        State = Mats[FireId].BaseLife,
                        Variant = (byte)(h & 3u),
                        Flags = Cell.FlagMoved,
                    };
                    Moved[ChunkIndexOf(nx, ny)]++;
                }
            }

            // 向上飘（概率），否则原地减寿并闪烁
            Cell updated = c;
            updated.State = (byte)life;
            updated.Variant = (byte)(h & 3u);
            if (((h >> 8) & 0xFFu) < p.Fluidity)
            {
                int first = (h & 1u) == 0u ? -1 : 1;
                if (TryMove(x, y, x, y + 1, updated, p)) return;
                if (TryMove(x, y, x + first, y + 1, updated, p)) return;
                if (TryMove(x, y, x - first, y + 1, updated, p)) return;
            }
            Write[index] = updated;
            Moved[ChunkIndexOf(x, y)]++;
        }

        bool TouchingType(int x, int y, MatterType type)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
                    ushort id = Write[ny * Width + nx].MaterialId;
                    if (id != BuiltinMaterials.Empty && Mats[id].Type == type) return true;
                }
            }
            return false;
        }

        bool TryMove(int x, int y, int nx, int ny, in Cell c, in MaterialProps p)
        {
            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) return false;
            int from = y * Width + x;
            int to = ny * Width + nx;
            Cell target = Write[to];

            bool canOccupy;
            if (target.MaterialId == BuiltinMaterials.Empty)
            {
                canOccupy = true;
            }
            else
            {
                MaterialProps tp = Mats[target.MaterialId];
                // 密度置换：重的可以沉入轻的流体/火焰
                canOccupy = (tp.Type == MatterType.Liquid
                    || tp.Type == MatterType.Gas
                    || tp.Type == MatterType.Fire)
                    && tp.Density < p.Density;
            }
            if (!canOccupy) return false;

            Cell moved = c;
            moved.Flags |= Cell.FlagMoved;
            Cell displaced = target;
            displaced.Flags |= Cell.FlagMoved;
            Write[to] = moved;
            Write[from] = displaced;
            Moved[ChunkIndexOf(x, y)]++;
            Moved[ChunkIndexOf(nx, ny)]++;
            return true;
        }

        int ChunkIndexOf(int x, int y) =>
            (y >> SimCoords.ChunkShift) * ChunksX + (x >> SimCoords.ChunkShift);
    }
}
