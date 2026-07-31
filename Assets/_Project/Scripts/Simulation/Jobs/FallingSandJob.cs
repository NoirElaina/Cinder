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

        /// <summary>火焰熄灭/被扑灭时留下 BurnsInto 产物（烟）的概率权重（/255）。</summary>
        const byte SmokeOnDeathChance = 64;

        public void Execute()
        {
            // 整块 memcpy 同步双缓冲，再只在醒着的区块里清 FlagMoved：
            // 上个 tick 移动过的格必在醒块内（EndTick 的唤醒规则保证），
            // 休眠块的残留标志不会被读到（下方扫描也跳过它们）。
            NativeArray<Cell>.Copy(Read, Write);
            ClearAwakeFlags();
            for (int i = 0; i < Moved.Length; i++) Moved[i] = 0;

            bool leftToRight = (Tick & 1u) == 0u;
            for (int y = 0; y < Height; y++)
            {
                int chunkRow = (y >> SimCoords.ChunkShift) * ChunksX;
                if (leftToRight)
                {
                    for (int cx = 0; cx < ChunksX; cx++)
                    {
                        if (Awake[chunkRow + cx] == 0) continue;
                        int x0 = cx << SimCoords.ChunkShift;
                        int x1 = x0 + SimCoords.ChunkSize;
                        for (int x = x0; x < x1; x++) Step(x, y);
                    }
                }
                else
                {
                    for (int cx = ChunksX - 1; cx >= 0; cx--)
                    {
                        if (Awake[chunkRow + cx] == 0) continue;
                        int x0 = cx << SimCoords.ChunkShift;
                        for (int x = x0 + SimCoords.ChunkSize - 1; x >= x0; x--) Step(x, y);
                    }
                }
            }
        }

        /// <summary>只在醒着的区块里清上个 tick 的 FlagMoved（Burst 内直接循环，不用委托）。</summary>
        void ClearAwakeFlags()
        {
            int chunksY = Height >> SimCoords.ChunkShift;
            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cx = 0; cx < ChunksX; cx++)
                {
                    if (Awake[cy * ChunksX + cx] == 0) continue;
                    int baseIndex = (cy << SimCoords.ChunkShift) * Width
                        + (cx << SimCoords.ChunkShift);
                    for (int ly = 0; ly < SimCoords.ChunkSize; ly++)
                    {
                        int start = baseIndex + ly * Width;
                        for (int i = start; i < start + SimCoords.ChunkSize; i++)
                        {
                            Cell c = Write[i];
                            if (c.Flags == 0) continue;
                            c.Flags = 0;
                            Write[i] = c;
                        }
                    }
                }
            }
        }

        void Step(int x, int y)
        {
            int index = y * Width + x;
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

        void StepFluid(int x, int y, Cell c, in MaterialProps p, int dirY)
        {
            uint h = SimHash.Hash(x, y, Tick, Seed);

            // 有寿命的气体（烟等）先衰减，耗尽即消散。
            // 衰减也要计入 Moved：被困住/停顿的烟所在区块不能休眠，否则寿命冻结永不消散
            if (dirY == 1 && p.BaseLife > 0)
            {
                int index = y * Width + x;
                if (c.State <= 1)
                {
                    Write[index] = default;
                    Moved[ChunkIndexOf(x, y)]++;
                    return;
                }
                c.State -= 1;
                Write[index] = c;
                Moved[ChunkIndexOf(x, y)]++;
            }

            int first = (h & 1u) == 0u ? -1 : 1;
            if (dirY == 1)
            {
                // 气体：随机停顿造出翻卷感，同时打散整行同步上移的横向条带
                if (((h >> 20) & 0xFFu) < 48) return;
                // 斜向优先（~43%）：烟羽锥形散开，而不是笔直一根柱
                if (((h >> 2) & 0xFFu) < 110)
                {
                    if (TryMove(x, y, x + first, y + 1, c, p)) return;
                    if (TryMove(x, y, x, y + 1, c, p)) return;
                }
                else
                {
                    if (TryMove(x, y, x, y + 1, c, p)) return;
                    if (TryMove(x, y, x + first, y + 1, c, p)) return;
                }
                if (TryMove(x, y, x - first, y + 1, c, p)) return;
            }
            else
            {
                if (TryMove(x, y, x, y - 1, c, p)) return;
                if (TryMove(x, y, x + first, y - 1, c, p)) return;
                if (TryMove(x, y, x - first, y - 1, c, p)) return;
            }

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

            // 邻不可燃液体（水等）概率熄灭；油等易燃液体不灭火
            if (TouchingExtinguisher(x, y) && ((h >> 3) & 1u) == 0u)
            {
                Extinguish(x, y, index, in p, h);
                return;
            }

            int life = c.State - 1;
            if (life <= 0)
            {
                Extinguish(x, y, index, in p, h);
                return;
            }

            // 点燃邻居：向上蔓延最快、水平次之、向下最慢（Noita 式火舌上舔）
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
                    uint threshold = dy > 0
                        ? tp.Flammability
                        : dy == 0 ? (uint)(tp.Flammability / 2) : (uint)(tp.Flammability / 4);
                    if (((SimHash.Hash(nx, ny, Tick, Seed) >> 4) & 0xFFu) >= threshold) continue;

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

        /// <summary>火焰消失：有概率留下 BurnsInto 产物（数据驱动，火焰配为烟）。</summary>
        void Extinguish(int x, int y, int index, in MaterialProps p, uint h)
        {
            if (p.BurnsInto != 0 && ((h >> 12) & 0xFFu) < SmokeOnDeathChance)
            {
                MaterialProps np = Mats[p.BurnsInto];
                Write[index] = new Cell
                {
                    MaterialId = p.BurnsInto,
                    State = np.BaseLife,
                    Variant = (byte)(h & 3u),
                    Flags = Cell.FlagMoved,
                };
            }
            else
            {
                Write[index] = default;
            }
            Moved[ChunkIndexOf(x, y)]++;
        }

        bool TouchingExtinguisher(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
                    ushort id = Write[ny * Width + nx].MaterialId;
                    if (id == BuiltinMaterials.Empty) continue;
                    MaterialProps np = Mats[id];
                    if (np.Type == MatterType.Liquid && np.Flammability == 0) return true;
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
                // 密度置换：重的可以沉入轻的流体。
                // 火焰不可被置换——否则油会流进火里把火挤走，链式燃烧会中断。
                canOccupy = (tp.Type == MatterType.Liquid
                    || tp.Type == MatterType.Gas)
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
