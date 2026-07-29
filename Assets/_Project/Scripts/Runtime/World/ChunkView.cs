using Cinder.Runtime.Materials;
using Cinder.Simulation;
using Unity.Collections;
using UnityEngine;

namespace Cinder.Runtime.World
{
    /// <summary>
    /// 区块视图：一张 128x128 点采样纹理，1 格 = 1 世界单位。
    /// 仅在被标记脏时重绘上传。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ChunkView : MonoBehaviour
    {
        const int Size = SimCoords.ChunkSize;

        Texture2D texture;
        Sprite sprite;
        Color32[] pixels;

        /// <summary>
        /// 首次绑定到某区块后置真，重绘后清除。
        /// 配合每帧重绘预算，超预算时留待下一帧。
        /// </summary>
        public bool PendingRedraw { get; set; }

        public void Initialize()
        {
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[Size * Size];
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), Vector2.zero, 1f);
            GetComponent<SpriteRenderer>().sprite = sprite;
        }

        /// <summary>从模拟窗口的平坦数组重绘（跨步取样）。</summary>
        public void RedrawFromWindow(NativeArray<Cell> windowCells, int windowWidth,
            int startIndex, MaterialDatabase db)
        {
            for (int ly = 0; ly < Size; ly++)
            {
                int srcRow = startIndex + ly * windowWidth;
                int dstRow = ly * Size;
                for (int lx = 0; lx < Size; lx++)
                {
                    Cell c = windowCells[srcRow + lx];
                    pixels[dstRow + lx] = db.GetColor(c.MaterialId, c.Variant);
                }
            }
            Upload();
        }

        /// <summary>从驻留存储区块重绘（连续取样）。</summary>
        public void RedrawFromChunk(ChunkData chunk, MaterialDatabase db)
        {
            for (int i = 0; i < chunk.Cells.Length; i++)
            {
                Cell c = chunk.Cells[i];
                pixels[i] = db.GetColor(c.MaterialId, c.Variant);
            }
            Upload();
        }

        void Upload()
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            PendingRedraw = false;
        }

        void OnDestroy()
        {
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }
}
