using Unity.Collections;
using Unity.Mathematics;

namespace Cinder.Simulation
{
    /// <summary>
    /// 确定性程序化世界生成（细物理格版）：同 seed + 同区块坐标必得同字节结果。
    /// 地形轮廓（地表、洞穴、矿脉、结构）全部先定义在宏观世界单位，
    /// 再经 WorldScale 采样到细格，并叠加细格级边缘噪声，
    /// 使地形边缘呈 1–2 细格的锯齿而不是平滑大弧。
    /// 纯函数、无状态，可在任意线程调用（GenerateChunkJob 走 Burst）。
    /// </summary>
    public static class WorldGenerator
    {
        // ---- 宏观地形参数（世界单位）----
        public const float SurfaceBaseUnits = 8f;
        public const float SurfaceAmplitudeUnits = 42f;
        public const float SurfaceFrequency = 0.012f;

        /// <summary>洞穴开始出现的深度（世界单位）。</summary>
        const float CaveMinDepthUnits = 2.5f;

        /// <summary>洞穴积水的最小深度（世界单位）。</summary>
        const float PoolMinDepthUnits = 6.5f;

        /// <summary>洞穴积岩浆的最小深度（世界单位）。</summary>
        const float LavaMinDepthUnits = 46f;

        /// <summary>油囊出现的最小深度（世界单位）。</summary>
        const float OilMinDepthUnits = 12f;

        /// <summary>表层（沙/土交错带）厚度（世界单位）。</summary>
        const float TopsoilDepthUnits = 3.5f;

        // ---- 木结构层（细格）----
        const int StructGridCellsX = 96;  // 24 世界单位
        const int StructGridCellsY = 64;  // 16 世界单位
        const int PlatformThicknessCells = 3;

        /// <summary>某世界 X 列的地表高度（细格坐标，含细格边缘噪声）。</summary>
        public static int SurfaceHeight(int cellX, int seed)
        {
            float ux = cellX * WorldScale.UnitsPerCell;
            float macro = SurfaceBaseUnits + SurfaceAmplitudeUnits
                * noise.cnoise(new float2(ux * SurfaceFrequency, seed * 0.173f));
            // 细格级起伏：让地表以 1-2 细格的台阶变化，而不是 1 世界单位一跳
            float detail = noise.cnoise(new float2(cellX * 0.11f, seed * 0.377f)) * 2.5f;
            return (int)math.floor(macro * WorldScale.CellsPerUnitF + detail);
        }

        /// <summary>生成一个区块的全部格子写入 dst（长度必须为 ChunkData.CellCount）。</summary>
        public static void Generate(int chunkX, int chunkY, int seed, int minChunkY,
            NativeArray<Cell> dst)
        {
            int originX = SimCoords.ChunkToCellOrigin(chunkX);
            int originY = SimCoords.ChunkToCellOrigin(chunkY);

            for (int ly = 0; ly < SimCoords.ChunkSize; ly++)
            {
                int wy = originY + ly;
                for (int lx = 0; lx < SimCoords.ChunkSize; lx++)
                {
                    int wx = originX + lx;
                    Cell cell = default;
                    cell.Variant = SimHash.Variant(wx, wy, (uint)seed);

                    if (chunkY == minChunkY)
                    {
                        cell.MaterialId = BuiltinMaterials.Bedrock;
                    }
                    else
                    {
                        int surface = SurfaceHeight(wx, seed);
                        if (wy <= surface)
                        {
                            int depthCells = surface - wy;
                            cell.MaterialId = SubsurfaceMaterial(wx, wy, depthCells, seed);
                        }
                    }

                    dst[ly * SimCoords.ChunkSize + lx] = cell;
                }
            }
        }

        static ushort SubsurfaceMaterial(int wx, int wy, int depthCells, int seed)
        {
            float ux = wx * WorldScale.UnitsPerCell;
            float uy = wy * WorldScale.UnitsPerCell;
            float depthUnits = depthCells * WorldScale.UnitsPerCell;

            // 洞穴：宏观单位噪声雕刻，阈值叠加细格噪声让洞壁呈细碎锯齿
            if (depthUnits > CaveMinDepthUnits)
            {
                float cave = noise.cnoise(new float2(
                    ux * 0.045f + seed * 0.31f,
                    uy * 0.045f - seed * 0.17f));
                float edge = noise.cnoise(new float2(wx * 0.16f, wy * 0.16f)) * 0.05f;
                if (cave > 0.42f + edge)
                {
                    // 深层洞穴积液：浅层是水潭，极深层是岩浆湖（光源）
                    float pool = noise.cnoise(new float2(
                        ux * 0.03f - seed * 0.11f,
                        uy * 0.03f + seed * 0.07f));
                    if (depthUnits > LavaMinDepthUnits && pool > 0.42f)
                        return BuiltinMaterials.Lava;
                    if (depthUnits > PoolMinDepthUnits && pool > 0.25f)
                        return BuiltinMaterials.Water;

                    // 洞穴空腔内的木结构（平台/支撑腿），可燃烧塌落
                    if (IsStructureWood(wx, wy, seed))
                        return BuiltinMaterials.Wood;
                    return BuiltinMaterials.Empty;
                }
            }

            // 表层：沙地与泥土交错
            if (depthUnits < TopsoilDepthUnits)
            {
                float patch = noise.cnoise(new float2(ux * 0.08f + 7.3f, seed * 0.5f));
                return patch > 0.25f ? BuiltinMaterials.Sand : BuiltinMaterials.Dirt;
            }

            // 深层油囊：封在岩石里的可燃液体，被挖开/点燃前保持稳定
            if (depthUnits > OilMinDepthUnits)
            {
                float oil = noise.cnoise(new float2(
                    ux * 0.06f - seed * 0.23f,
                    uy * 0.06f + seed * 0.41f));
                if (oil > 0.55f) return BuiltinMaterials.Oil;
            }

            return BuiltinMaterials.Rock;
        }

        /// <summary>
        /// 结构层：以 24x16 世界单位为宏观格，按确定性哈希决定其中是否有
        /// 一条木平台与支撑腿。只在洞穴空腔（本应为 Empty 的格）里生效。
        /// </summary>
        static bool IsStructureWood(int wx, int wy, int seed)
        {
            int gx = wx >= 0 ? wx / StructGridCellsX : (wx - StructGridCellsX + 1) / StructGridCellsX;
            int gy = wy >= 0 ? wy / StructGridCellsY : (wy - StructGridCellsY + 1) / StructGridCellsY;
            uint h = SimHash.Hash(gx, gy, 911u, (uint)seed);
            if ((h & 7u) >= 3u) return false; // 3/8 的宏观格里有平台

            int originX = gx * StructGridCellsX;
            int originY = gy * StructGridCellsY;
            int platformY = originY + 10 + (int)((h >> 8) % 36u);
            int left = originX + 10;
            int right = originX + StructGridCellsX - 10;
            if (wx < left || wx >= right) return false;

            // 平台板
            if (wy >= platformY && wy < platformY + PlatformThicknessCells) return true;

            // 支撑腿：每 24 格一条、2 格宽、向下 14 格
            if (wy < platformY && wy >= platformY - 14)
            {
                int rel = wx - left;
                if (rel % 24 < 2) return true;
            }
            return false;
        }
    }
}
