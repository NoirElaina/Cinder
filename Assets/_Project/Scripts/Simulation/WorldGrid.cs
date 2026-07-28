using System;
using System.Collections.Generic;

namespace Cinder.Simulation
{
    /// <summary>
    /// 世界存储：稀疏保存"驻留但未在模拟窗口中"的区块。
    /// X 无限，Y 限定在 [MinChunkY, MaxChunkY]。
    /// 加载顺序：内存 -> 磁盘（loader 回调）-> 程序化生成。
    /// </summary>
    public sealed class WorldGrid : IDisposable
    {
        public readonly int Seed;
        public readonly int MinChunkY;
        public readonly int MaxChunkY;

        readonly Dictionary<long, ChunkData> chunks = new Dictionary<long, ChunkData>();

        public WorldGrid(int seed, int minChunkY = -31, int maxChunkY = 4)
        {
            Seed = seed;
            MinChunkY = minChunkY;
            MaxChunkY = maxChunkY;
        }

        public int LoadedCount => chunks.Count;

        public IEnumerable<ChunkData> Loaded => chunks.Values;

        public bool ContainsY(int chunkY) => chunkY >= MinChunkY && chunkY <= MaxChunkY;

        public bool TryGet(int chunkX, int chunkY, out ChunkData chunk) =>
            chunks.TryGetValue(SimCoords.PackKey(chunkX, chunkY), out chunk);

        /// <summary>
        /// 获取区块；未加载时依次尝试磁盘与程序化生成。
        /// Y 越界返回 null（天空之上与基岩层以下都不存在）。
        /// </summary>
        public ChunkData GetOrCreate(int chunkX, int chunkY,
            Func<int, int, byte[]> diskLoader = null)
        {
            if (!ContainsY(chunkY)) return null;
            if (chunks.TryGetValue(SimCoords.PackKey(chunkX, chunkY), out ChunkData existing))
                return existing;

            ChunkData chunk = null;
            byte[] bytes = diskLoader?.Invoke(chunkX, chunkY);
            if (bytes != null && ChunkSerializer.TryDeserialize(bytes, out ChunkData loaded)
                && loaded.ChunkX == chunkX && loaded.ChunkY == chunkY)
            {
                chunk = loaded;
            }
            else
            {
                chunk = new ChunkData(chunkX, chunkY);
                WorldGenerator.Generate(chunkX, chunkY, Seed, MinChunkY, chunk.Cells);
            }

            chunks.Add(chunk.Key, chunk);
            return chunk;
        }

        /// <summary>把区块挂回存储（模拟窗口换出时使用）。</summary>
        public void Attach(ChunkData chunk)
        {
            if (chunk == null) return;
            chunks[chunk.Key] = chunk;
        }

        /// <summary>从存储摘除（不释放），调用方取得所有权。</summary>
        public ChunkData Remove(int chunkX, int chunkY)
        {
            long key = SimCoords.PackKey(chunkX, chunkY);
            if (!chunks.TryGetValue(key, out ChunkData chunk)) return null;
            chunks.Remove(key);
            return chunk;
        }

        public void Dispose()
        {
            foreach (ChunkData chunk in chunks.Values) chunk.Dispose();
            chunks.Clear();
        }
    }
}
