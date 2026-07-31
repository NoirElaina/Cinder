using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Cinder.Simulation
{
    /// <summary>
    /// 区块二进制序列化：16 字节头（magic/cx/cy/保留）+ 原始 Cell 字节。
    /// 纯函数，不涉及 IO，便于单测。
    /// CNK2 = 细物理格世界（1 世界单位 = 4 格）。旧版 CNK1 粗格存档不兼容，
    /// 反序列化直接拒收并重新生成。
    /// </summary>
    public static class ChunkSerializer
    {
        public const uint Magic = 0x324B4E43; // 'CNK2'
        public const int HeaderSize = 16;

        /// <summary>单格字节数取自真实布局，避免对齐填充假设。</summary>
        public static readonly int CellStride = UnsafeUtility.SizeOf<Cell>();
        public static readonly int PayloadSize = ChunkData.CellCount * CellStride;
        public static readonly int TotalSize = HeaderSize + PayloadSize;

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
