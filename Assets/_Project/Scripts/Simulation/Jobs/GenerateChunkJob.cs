using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cinder.Simulation.Jobs
{
    /// <summary>
    /// Burst 版区块生成：比托管 WorldGenerator.Generate 快约一个数量级，
    /// 供主线程同步生成（移位时）使用，避免区块切换卡顿。
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct GenerateChunkJob : IJob
    {
        public int ChunkX;
        public int ChunkY;
        public int Seed;
        public int MinChunkY;
        [WriteOnly] public NativeArray<Cell> Dst;

        public void Execute() =>
            WorldGenerator.Generate(ChunkX, ChunkY, Seed, MinChunkY, Dst);
    }
}
