using System;
using System.IO;
using System.Threading.Tasks;
using Cinder.Simulation;
using UnityEngine;

namespace Cinder.Runtime.World
{
    /// <summary>
    /// 区块磁盘存取：persistentDataPath/world/{seed}/{cx}_{cy}.cnk。
    /// 加载同步（64KB 亚毫秒），保存后台写盘。
    /// </summary>
    public sealed class ChunkStore
    {
        readonly string directory;

        public ChunkStore(int seed)
        {
            directory = Path.Combine(Application.persistentDataPath, "world", seed.ToString());
            Directory.CreateDirectory(directory);
        }

        string PathFor(int chunkX, int chunkY) =>
            Path.Combine(directory, $"{chunkX}_{chunkY}.cnk");

        public byte[] TryLoad(int chunkX, int chunkY)
        {
            try
            {
                string path = PathFor(chunkX, chunkY);
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Cinder] 区块读取失败 ({chunkX},{chunkY})，将重新生成: {e.Message}");
                return null;
            }
        }

        public void SaveAsync(ChunkData chunk)
        {
            byte[] bytes = ChunkSerializer.Serialize(chunk);
            string path = PathFor(chunk.ChunkX, chunk.ChunkY);
            Task.Run(() =>
            {
                try { File.WriteAllBytes(path, bytes); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Cinder] 区块保存失败 ({chunk.ChunkX},{chunk.ChunkY}): {e.Message}");
                }
            });
        }

        public void SaveSync(ChunkData chunk)
        {
            try { File.WriteAllBytes(PathFor(chunk.ChunkX, chunk.ChunkY), ChunkSerializer.Serialize(chunk)); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Cinder] 区块保存失败 ({chunk.ChunkX},{chunk.ChunkY}): {e.Message}");
            }
        }
    }
}
