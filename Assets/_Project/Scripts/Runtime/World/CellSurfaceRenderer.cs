using Cinder.Runtime.Materials;
using Cinder.Simulation;
using Cinder.Simulation.Channels;
using Cinder.Simulation.Jobs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Cinder.Runtime.World
{
    /// <summary>
    /// 细格世界的唯一渲染器：一个覆盖整个模拟窗口的 Quad + CellSurface.shader，
    /// 每帧 PackCellsJob（Burst）直接写入 LockBufferForWrite 锁定的 GraphicsBuffer
    /// -> 一次 DrawCall。无中间数组无 SetData 拷贝；三缓冲轮转避免改写 GPU 正在读的帧。
    /// 主循环零托管分配；缓冲区只在窗口尺寸变化时重建。
    /// </summary>
    public sealed class CellSurfaceRenderer : MonoBehaviour
    {
        const int RingSize = 3;

        Material material;
        Mesh mesh;
        readonly GraphicsBuffer[] cellsRing = new GraphicsBuffer[RingSize];
        int ringIndex;
        GraphicsBuffer paletteBuffer;
        GraphicsBuffer paramsBuffer;
        int capacity;
        int lastPackedMode = -1;

        SimulationWindow window;
        LightChannel lightChannel;
        ThermalChannel thermalChannel;
        MaterialDatabase db;

        static readonly int CellsProp = Shader.PropertyToID("_Cells");
        static readonly int PalettesProp = Shader.PropertyToID("_Palettes");
        static readonly int MatParamsProp = Shader.PropertyToID("_MatParams");
        static readonly int WinWProp = Shader.PropertyToID("_WinW");
        static readonly int WinHProp = Shader.PropertyToID("_WinH");
        static readonly int OriginXProp = Shader.PropertyToID("_OriginX");
        static readonly int OriginYProp = Shader.PropertyToID("_OriginY");
        static readonly int CellsPerUnitProp = Shader.PropertyToID("_CellsPerUnit");
        static readonly int SurfaceYProp = Shader.PropertyToID("_SurfaceY");
        static readonly int DebugModeProp = Shader.PropertyToID("_DebugMode");

        /// <summary>0 = 正常渲染，1 = 温度热力图（F2）。</summary>
        public int DebugMode { get; set; }

        public static CellSurfaceRenderer Create(Transform parent)
        {
            var go = new GameObject("CellSurface");
            go.transform.SetParent(parent, false);
            return go.AddComponent<CellSurfaceRenderer>();
        }

        void Awake()
        {
            Shader shader = Shader.Find("Cinder/CellSurface");
            material = new Material(shader);
            mesh = BuildUnitQuad();

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        /// <summary>绑定模拟数据源。窗口/通道被重建（如 R 重置）后需重新调用。</summary>
        public void Bind(SimulationWindow window, LightChannel lightChannel,
            ThermalChannel thermalChannel, MaterialDatabase db)
        {
            if (this.db != null) this.db.Rebuilt -= UploadStaticTables;
            this.window = window;
            this.lightChannel = lightChannel;
            this.thermalChannel = thermalChannel;
            this.db = db;
            db.Rebuilt += UploadStaticTables;

            EnsureCellBuffer();
            UploadStaticTables();
            material.SetFloat(CellsPerUnitProp, WorldScale.CellsPerUnitF);
            material.SetFloat(SurfaceYProp, WorldGenerator.SurfaceBaseUnits);
        }

        /// <summary>每帧（LateUpdate 末尾、效果总线 Flush 之后）调用。
        /// worldChanged = false 且视图模式未变时跳过打包与上传（内容只在
        /// tick/编辑/平移时变，30tps 下约省一半帧的 CPU 打包成本）。</summary>
        public void Render(bool worldChanged)
        {
            if (window == null) return;
            EnsureCellBuffer();

            if (worldChanged || DebugMode != lastPackedMode)
            {
                lastPackedMode = DebugMode;
                ringIndex = (ringIndex + 1) % RingSize;
                GraphicsBuffer buf = cellsRing[ringIndex];
                NativeArray<uint> dst = buf.LockBufferForWrite<uint>(0, capacity);
                new PackCellsJob
                {
                    Cells = window.ReadArray,
                    Light = lightChannel.Light,
                    Temps = thermalChannel.CurrentTemps,
                    Mats = db.Table.Native,
                    Packed = dst,
                    Mode = DebugMode,
                }.Run();
                buf.UnlockBufferAfterWrite<uint>(capacity);
                material.SetBuffer(CellsProp, buf);

                material.SetInt(OriginXProp, SimCoords.ChunkToCellOrigin(window.OriginChunkX));
                material.SetInt(OriginYProp, SimCoords.ChunkToCellOrigin(window.OriginChunkY));
                material.SetInt(DebugModeProp, DebugMode);
            }

            int originX = SimCoords.ChunkToCellOrigin(window.OriginChunkX);
            int originY = SimCoords.ChunkToCellOrigin(window.OriginChunkY);
            // Quad 覆盖窗口的世界矩形，z=1 让精灵（z=0）画在前面
            transform.position = new Vector3(
                originX * WorldScale.UnitsPerCell,
                originY * WorldScale.UnitsPerCell, 1f);
            transform.localScale = new Vector3(
                window.Width * WorldScale.UnitsPerCell,
                window.Height * WorldScale.UnitsPerCell, 1f);
        }

        void EnsureCellBuffer()
        {
            int count = window.Width * window.Height;
            if (count == capacity) return;
            capacity = count;

            for (int i = 0; i < RingSize; i++)
            {
                cellsRing[i]?.Dispose();
                cellsRing[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite, count, sizeof(uint));
            }
            // 首帧 pack 前 shader 也要有合法绑定
            material.SetBuffer(CellsProp, cellsRing[ringIndex]);
            material.SetInt(WinWProp, window.Width);
            material.SetInt(WinHProp, window.Height);
        }

        void UploadStaticTables()
        {
            paletteBuffer?.Dispose();
            paramsBuffer?.Dispose();
            paletteBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                db.GpuPalettes.Length, sizeof(uint));
            paramsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                db.GpuParams.Length, sizeof(uint));
            paletteBuffer.SetData(db.GpuPalettes);
            paramsBuffer.SetData(db.GpuParams);
            material.SetBuffer(PalettesProp, paletteBuffer);
            material.SetBuffer(MatParamsProp, paramsBuffer);
        }

        static Mesh BuildUnitQuad()
        {
            var mesh = new Mesh { name = "CellSurfaceQuad" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f), new Vector3(1f, 0f),
                new Vector3(0f, 1f), new Vector3(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            if (db != null) db.Rebuilt -= UploadStaticTables;
            for (int i = 0; i < RingSize; i++) cellsRing[i]?.Dispose();
            paletteBuffer?.Dispose();
            paramsBuffer?.Dispose();
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }
    }
}
