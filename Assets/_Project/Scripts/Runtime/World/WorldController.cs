using System.Collections.Generic;
using Cinder.Runtime.Materials;
using Cinder.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinder.Runtime.World
{
    /// <summary>
    /// 世界查看器总控：固定步长驱动模拟、按相机位置流式加载、
    /// 维护区块视图、处理笔刷输入。任意场景按 Play 即可运行（见 WorldBootstrap）。
    /// </summary>
    public sealed class WorldController : MonoBehaviour
    {
        public static WorldController Instance { get; private set; }

        [SerializeField] int seed = 1337;
        [SerializeField] float ticksPerSecond = 30f;
        [SerializeField] int brushRadius = 5;

        static readonly ushort[] BrushOrder =
        {
            BuiltinMaterials.Sand, BuiltinMaterials.Water, BuiltinMaterials.Dirt,
            BuiltinMaterials.Rock, BuiltinMaterials.Wood, BuiltinMaterials.Fire,
            BuiltinMaterials.Bedrock,
        };

        MaterialDatabase db;
        WorldStreamer streamer;
        SimulationEngine engine;
        ChunkViewPool pool;
        readonly Dictionary<long, ChunkView> views = new Dictionary<long, ChunkView>();
        readonly List<long> staleKeys = new List<long>();

        Camera cam;
        float tickAccumulator;
        ushort brushMaterial = BuiltinMaterials.Sand;

        float fpsTimer;
        int fpsFrames;

        public int Fps { get; private set; }

        void Awake()
        {
            Instance = this;
            db = MaterialDatabase.CreateDefault();
            streamer = new WorldStreamer(seed, db);
            engine = new SimulationEngine(streamer.Window, db.Table, seed);
            db.Rebuilt += OnMaterialsRebuilt;
            pool = new ChunkViewPool(transform);
        }

        void Start()
        {
            cam = Camera.main;
            if (cam != null && cam.GetComponent<FlyCamera>() == null)
                cam.gameObject.AddComponent<FlyCamera>();
            int spawnY = WorldGenerator.SurfaceHeight(0, seed) + 30;
            if (cam != null) cam.transform.position = new Vector3(0f, spawnY, -10f);
            streamer.SetFocus(0, spawnY);
        }

        void OnMaterialsRebuilt() => engine.Table = db.Table;

        void Update()
        {
            HandleBrushInput();
            HandleEditInput();

            tickAccumulator += Time.deltaTime;
            float interval = 1f / Mathf.Max(1f, ticksPerSecond);
            int steps = 0;
            while (tickAccumulator >= interval && steps < 4)
            {
                engine.Step();
                tickAccumulator -= interval;
                steps++;
            }

            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                Fps = Mathf.RoundToInt(fpsFrames / fpsTimer);
                fpsFrames = 0;
                fpsTimer = 0f;
            }
        }

        void LateUpdate()
        {
            if (cam == null) return;
            Vector3 p = cam.transform.position;
            streamer.SetFocus(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            streamer.ProcessPendingLoads(3);
            UpdateViews();
        }

        void HandleBrushInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) brushMaterial = BrushOrder[0];
            else if (kb.digit2Key.wasPressedThisFrame) brushMaterial = BrushOrder[1];
            else if (kb.digit3Key.wasPressedThisFrame) brushMaterial = BrushOrder[2];
            else if (kb.digit4Key.wasPressedThisFrame) brushMaterial = BrushOrder[3];
            else if (kb.digit5Key.wasPressedThisFrame) brushMaterial = BrushOrder[4];
            else if (kb.digit6Key.wasPressedThisFrame) brushMaterial = BrushOrder[5];
            else if (kb.digit7Key.wasPressedThisFrame) brushMaterial = BrushOrder[6];
        }

        void HandleEditInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || cam == null) return;
            bool dig = mouse.leftButton.isPressed;
            bool place = mouse.rightButton.isPressed;
            if (!dig && !place) return;

            Vector3 screen = mouse.position.ReadValue();
            screen.z = -cam.transform.position.z;
            Vector3 world = cam.ScreenToWorldPoint(screen);
            streamer.EditSphere(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y),
                dig ? brushRadius + 2 : brushRadius,
                dig ? BuiltinMaterials.Empty : brushMaterial);
        }

        void UpdateViews()
        {
            const int MaxRedrawsPerFrame = 4;
            int redraws = 0;

            Vector3 center = cam.transform.position;
            float halfH = cam.orthographicSize + SimCoords.ChunkSize;
            float halfW = halfH * cam.aspect + SimCoords.ChunkSize;
            int minCx = SimCoords.CellToChunk(Mathf.FloorToInt(center.x - halfW));
            int maxCx = SimCoords.CellToChunk(Mathf.FloorToInt(center.x + halfW));
            int minCy = SimCoords.CellToChunk(Mathf.FloorToInt(center.y - halfH));
            int maxCy = SimCoords.CellToChunk(Mathf.FloorToInt(center.y + halfH));

            staleKeys.AddRange(views.Keys);

            for (int cy = minCy; cy <= maxCy; cy++)
            {
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    long key = SimCoords.PackKey(cx, cy);
                    staleKeys.Remove(key);

                    bool inWindow = streamer.Window.ContainsChunk(cx, cy);
                    ChunkData stored = null;
                    if (!inWindow && !streamer.Grid.TryGet(cx, cy, out stored)) continue;

                    if (!views.TryGetValue(key, out ChunkView view))
                    {
                        view = pool.Get();
                        view.transform.position = new Vector3(
                            SimCoords.ChunkToCellOrigin(cx), SimCoords.ChunkToCellOrigin(cy), 0f);
                        view.PendingRedraw = true;
                        views.Add(key, view);
                    }

                    int windowIndex = streamer.Window.WindowChunkIndex(cx, cy);
                    bool dirty = inWindow
                        ? view.PendingRedraw || streamer.Window.ChunkDirty[windowIndex] == 1
                        : view.PendingRedraw;
                    if (!dirty || redraws >= MaxRedrawsPerFrame) continue;

                    if (inWindow)
                    {
                        int start = ((cy - streamer.Window.OriginChunkY) * SimCoords.ChunkSize)
                            * streamer.Window.Width
                            + (cx - streamer.Window.OriginChunkX) * SimCoords.ChunkSize;
                        view.RedrawFromWindow(streamer.Window.ReadArray,
                            streamer.Window.Width, start, db);
                        streamer.Window.ChunkDirty[windowIndex] = 0;
                    }
                    else
                    {
                        view.RedrawFromChunk(stored, db);
                    }
                    redraws++;
                }
            }

            foreach (long key in staleKeys)
            {
                pool.Release(views[key]);
                views.Remove(key);
            }
            staleKeys.Clear();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 360, 150), GUI.skin.box);
            GUILayout.Label($"FPS {Fps}   Tick {engine?.Tick ?? 0}");
            Vector3 p = cam != null ? cam.transform.position : Vector3.zero;
            GUILayout.Label($"相机 ({p.x:F0}, {p.y:F0})   驻留区块 {streamer?.Grid.LoadedCount ?? 0}   视图 {views.Count}");
            GUILayout.Label($"笔刷: {db?.GetName(brushMaterial)}   (数字键 1-7 选择)");
            GUILayout.Label("左键挖掘 / 右键放置 / WASD 移动 / Shift 加速 / 滚轮缩放");
            GUILayout.EndArea();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (db != null) db.Rebuilt -= OnMaterialsRebuilt;
            streamer?.SaveAll();
            streamer?.Dispose();
            if (db != null) Destroy(db);
        }
    }
}
