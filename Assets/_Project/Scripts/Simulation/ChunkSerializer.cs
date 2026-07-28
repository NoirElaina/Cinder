using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Cinder.Simulation
{
    /// <summary>
    /// 区块二进制序列化：16 字节头（magic/cx/cy/保留）+ 原始 Cell 字节。
    /// 纯函数，不涉及 IO，便于单测。
    /// </summary>
    public static class ChunkSerializer
    {
        public const uint Magic = 0x314B4E43; // 'CNK1'
        public const int HeaderSize = 16;
        public const int PayloadSize = ChunkData.CellCount * 4;
        public const int TotalSize = HeaderSize + PayloadSize;

        public static unsafe byte[] Serialize(ChunkData chunk)
        {
            var bytes = new byte[TotalSize];
            fixed (byte* p = bytes)
            {
                *(uint*)p = Magic;
                *(int*)(p + 4) = chunk.ChunkX;
                *(int*)(p + 8) = chunk.ChunkY;
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(chunk.Cells);
                Buffer.MemoryCopy(src, p + HeaderSize, PayloadSize, PayloadSize);
            }
            return bytes;
        }

        public static unsafe bool TryDeserialize(byte[] bytes, out ChunkData chunk)
        {
            chunk = null;
            if (bytes == null || bytes.Length != TotalSize) return false;

            fixed (byte* p = bytes)
            {
                if (*(uint*)p != Magic) return false;
                int cx = *(int*)(p + 4);
                int cy = *(int*)(p + 8);
                chunk = new ChunkData(cx, cy) { Modified = true };
                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(chunk.Cells);
                Buffer.MemoryCopy(p + HeaderSize, dst, PayloadSize, PayloadSize);
            }
            return true;
        }
    }
}
