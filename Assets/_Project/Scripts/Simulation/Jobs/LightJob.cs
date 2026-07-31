using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cinder.Simulation.Jobs
{
    /// <summary>
    /// 光照 Job：每 tick 从零重建整窗光照场（byte 0..255）。
    /// 两步：①单次列扫描同时写入发光种子（火焰/岩浆等自热物质）与天光
    /// （自窗口顶部向下，空气不衰减、实体快速衰减）②四方向扫掠传播
    /// （每格取邻居光 x 本格透光率的最大值），横+竖各一轮组合出对角衰减。
    /// 共 5 次全窗遍历，纯 O(N)，禁止逐光源半径扫描。
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct LightJob : IJob
    {
        [ReadOnly] public NativeArray<Cell> Cells;
        [ReadOnly] public NativeArray<MaterialProps> Mats;
        public NativeArray<byte> Light;
        public int Width;
        public int Height;

        public void Execute()
        {
            SeedAndSkylight();
            SweepHorizontal();
            SweepVertical();
        }

        /// <summary>本格的透光率（0..255）：光穿过该格时保留的比例。</summary>
        int Transmittance(in MaterialProps p)
        {
            switch (p.Type)
            {
                case MatterType.Empty: return 247;
                case MatterType.Gas: return 244;
                case MatterType.Fire: return 250;
                case MatterType.Liquid: return 236;
                default: return 198; // Powder / StaticSolid：光只渗入边缘十几格
            }
        }

        /// <summary>单次列扫描：自热物质发光种子 + 自顶向下的天光，取两者最大值。</summary>
        void SeedAndSkylight()
        {
            for (int x = 0; x < Width; x++)
            {
                int sky = 255;
                for (int y = Height - 1; y >= 0; y--)
                {
                    int i = y * Width + x;
                    MaterialProps p = Mats[Cells[i].MaterialId];

                    int e = 0;
                    if (p.Type == MatterType.Fire) e = 255;
                    else if (p.SelfTempK > 600) e = (p.SelfTempK - 600) * 3 / 10;
                    if (e > 255) e = 255;

                    if (p.Type != MatterType.Empty && p.Type != MatterType.Gas)
                        sky = sky * Transmittance(p) >> 8;
                    if (sky <= 2) sky = 0;

                    Light[i] = (byte)(sky > e ? sky : e);
                }
            }
        }

        void SweepHorizontal()
        {
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                // 左 -> 右
                int carry = 0;
                for (int x = 0; x < Width; x++)
                {
                    int i = row + x;
                    carry = carry * Transmittance(Mats[Cells[i].MaterialId]) >> 8;
                    if (Light[i] > carry) carry = Light[i];
                    else Light[i] = (byte)carry;
                }
                // 右 -> 左
                carry = 0;
                for (int x = Width - 1; x >= 0; x--)
                {
                    int i = row + x;
                    carry = carry * Transmittance(Mats[Cells[i].MaterialId]) >> 8;
                    if (Light[i] > carry) carry = Light[i];
                    else Light[i] = (byte)carry;
                }
            }
        }

        void SweepVertical()
        {
            for (int x = 0; x < Width; x++)
            {
                // 下 -> 上
                int carry = 0;
                for (int y = 0; y < Height; y++)
                {
                    int i = y * Width + x;
                    carry = carry * Transmittance(Mats[Cells[i].MaterialId]) >> 8;
                    if (Light[i] > carry) carry = Light[i];
                    else Light[i] = (byte)carry;
                }
                // 上 -> 下
                carry = 0;
                for (int y = Height - 1; y >= 0; y--)
                {
                    int i = y * Width + x;
                    carry = carry * Transmittance(Mats[Cells[i].MaterialId]) >> 8;
                    if (Light[i] > carry) carry = Light[i];
                    else Light[i] = (byte)carry;
                }
            }
        }
    }
}
