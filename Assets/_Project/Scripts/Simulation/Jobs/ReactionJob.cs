using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cinder.Simulation.Jobs
{
    /// <summary>
    /// 化学反应 Job：每格只查 右/下 两个邻居（同一对每 tick 只判定一次），
    /// 命中反应表且概率通过后，按 MatA/MatB 与格子实际物质匹配——产物落到正确
    /// 的格子上，与酸在固体的左/右/上/下无关（修复方位错配把石头变成酸的 bug）。
    /// 每个参与者单独结算：Cost&gt;0 的按 State 预算渐进消耗（耗尽才变成产物），
    /// 否则立即按产物转变。处理全部格子——休眠区块内的静态岩浆池也必须持续反应。
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
            ushort idI = Cells[i].MaterialId;
            ushort idJ = Cells[j].MaterialId;
            if (idJ == BuiltinMaterials.Empty) return;

            ReactionRule rule = Reactions[idI * TableCapacity + idJ];
            if (rule.Exists == 0) return;
            if (((SimHash.Hash(x, y, Tick, Seed) >> 2) & 0xFFu) >= rule.Chance) return;

            // 与实际物质匹配谁是 MatA、谁是 MatB（方位无关）
            bool changed;
            if (idI == rule.MatA && idJ == rule.MatB)
            {
                changed = ApplyReactant(i, x, y, rule.MatA, rule.OutA, rule.CostA);
                changed |= ApplyReactant(j, nx, ny, rule.MatB, rule.OutB, rule.CostB);
            }
            else if (idI == rule.MatB && idJ == rule.MatA)
            {
                changed = ApplyReactant(i, x, y, rule.MatB, rule.OutB, rule.CostB);
                changed |= ApplyReactant(j, nx, ny, rule.MatA, rule.OutA, rule.CostA);
            }
            else
            {
                return;
            }

            if (!changed) return;
            int ci = (y >> SimCoords.ChunkShift) * ChunksX + (x >> SimCoords.ChunkShift);
            Moved[ci]++;
            int cj = (ny >> SimCoords.ChunkShift) * ChunksX + (nx >> SimCoords.ChunkShift);
            if (cj != ci) Moved[cj]++;
        }

        /// <summary>
        /// 结算单个参与者。cost&gt;0 = 渐进消耗：State 视为剩余预算，每次反应扣 cost，
        /// 预算耗尽（&lt;=0）才变成 outId；否则保持物质并记下剩余预算（酸因此有了
        /// 腐蚀上限，腐蚀几格后耗尽消失，而不是无限往下钻）。cost==0 = 立即按 outId
        /// 转变（outId==自身 = 不变）。返回是否改写了格子。
        /// </summary>
        bool ApplyReactant(int index, int x, int y, ushort matId, ushort outId, byte cost)
        {
            Cell c = Cells[index];
            if (cost > 0)
            {
                // State 为 0 时按 BaseLife 视作满预算（笔刷与生成都以 BaseLife 初始化）
                int remaining = c.State > 0 ? c.State : Mats[matId].BaseLife;
                remaining -= cost;
                if (remaining > 0)
                {
                    c.State = (byte)remaining;
                    Cells[index] = c;
                    return true;
                }
                Cells[index] = Product(outId, x, y);
                return true;
            }

            if (outId == matId) return false;
            Cells[index] = Product(outId, x, y);
            return true;
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
