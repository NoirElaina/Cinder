using Unity.Collections;
using Unity.Mathematics;

namespace Cinder.Simulation
{
    /// <summary>
    /// 确定性程序化世界生成：同 seed + 同区块坐标必得同字节结果。
    /// 未修改的区块卸载后不落盘，重新生成即可。
    /// 纯函数、无状态，可在任意线程调用。
    /// </summary>
    public static class WorldGenerator
    {
        public const float SurfaceBase = 8f;
        public const float SurfaceAmplitude = 42f;
        public const float SurfaceFrequency = 0.012f;

        /// <summary>某世界 X 列的地表高度（格坐标）。</summary>
        public static int SurfaceHeight(int worldX, int seed)
        {
            float n = noise.cnoise(new float2(worldX * SurfaceFrequency, seed * 0.173f));
            return (int)(SurfaceBase + SurfaceAmplitude * n);
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
                            int depth = surface - wy;
                            cell.MaterialId = SubsurfaceMaterial(wx, wy, depth, seed);
                        }
                    }

                    dst[ly * SimCoords.ChunkSize + lx] = cell;
                }
            }
        }

        static ushort SubsurfaceMaterial(int wx, int wy, int depth, int seed)
        {
            // 洞穴：地表一定深度以下按 2D 噪声雕刻
            if (depth > 10)
            {
                float cave = noise.cnoise(new float2(
                    wx * 0.045f + seed * 0.31f,
                    wy * 0.045f - seed * 0.17f));
                if (cave > 0.42f)
                {
                    // 深层洞穴里积水成潭
                    float pool = noise.cnoise(new float2(
                        wx * 0.03f - seed * 0.11f,
                        wy * 0.03f + seed * 0.07f));
                    return depth > 26 && pool > 0.25f
                        ? BuiltinMaterials.Water
                        : BuiltinMaterials.Empty;
                }
            }

            if (depth < 14)
            {
                // 表层：沙地与泥土交错
                float patch = noise.cnoise(new float2(wx * 0.02f + 7.3f, seed * 0.5f));
                return patch > 0.25f ? BuiltinMaterials.Sand : BuiltinMaterials.Dirt;
            }

            return BuiltinMaterials.Rock;
        }
    }
}
