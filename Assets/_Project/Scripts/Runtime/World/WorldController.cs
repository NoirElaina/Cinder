using System.Collections.Generic;
using Cinder.Game.Effects;
using Cinder.Runtime.Materials;
using Cinder.Runtime.Player;
using Cinder.Runtime.UI;
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
        [SerializeField] bool spawnPlayer = true;

        static readonly ushort[] BrushOrder =
        {
            BuiltinMaterials.Sand, BuiltinMaterials.Water, BuiltinMaterials.Dirt,
            BuiltinMaterials.Rock, BuiltinMaterials.Wood, BuiltinMaterials.Fire,
            BuiltinMaterials.Bedrock, BuiltinMaterials.Oil, BuiltinMaterials.Acid,
            BuiltinMaterials.Smoke, BuiltinMaterials.Lava, BuiltinMaterials.Ice,
        };

        MaterialDatabase db;
        WorldStreamer streamer;
        SimulationEngine engine;
        Simulation.Channels.ThermalChannel thermalChannel;
        EffectBus effectBus;
        SimEffectWorld effectWorld;
        ChunkViewPool pool;
        readonly Dictionary<long, ChunkView> views = new Dictionary<long, ChunkView>();
        readonly List<long> staleKeys = new List<long>();

        Camera cam;
        FlyCamera flyCam;
        PlayerController player;
        bool freeFly;
        int debugView; // 0 = 普通, 1 = 温度热力图
        float tickAccumulator;
        ushort brushMaterial = BuiltinMaterials.Sand;

        float fpsTimer;
        int fpsFrames;

        public int Fps { get; private set; }

        /// <summary>当前玩家（效果拾取物/装配画布据此访问）。</summary>
        public PlayerController Player => player;

        void Awake()
        {
            Instance = this;
            db = GameContent.LoadMaterials();
            if (db == null)
            {
                enabled = false;
                return;
            }
            effectBus = CreateEffectBus();
            BuildSimulation();
            db.Rebuilt += OnMaterialsRebuilt;
            pool = new ChunkViewPool(transform);
        }

        /// <summary>效果总线 + 全部内置处理器。热插拔点：运行时增删 Handler 即可。</summary>
        static EffectBus CreateEffectBus()
        {
            var bus = new EffectBus();
            bus.AddHandler(new DigHandler());
            bus.AddHandler(new ExplosionHandler());
            bus.AddHandler(new HeatHandler());
            bus.AddHandler(new FreezeHandler());
            bus.AddHandler(new IgniteHandler());
            return bus;
        }

        /// <summary>（重）建模拟内核：流式器 + 引擎 + 物理场通道 + 效果世界。</summary>
        void BuildSimulation()
        {
            streamer = new WorldStreamer(seed, db);
            engine = new SimulationEngine(streamer.Window, db.Table, seed);
            engine.AddChannel(new Simulation.Channels.ReactionChannel());
            thermalChannel = new Simulation.Channels.ThermalChannel();
            engine.AddChannel(thermalChannel);
            effectWorld = new SimEffectWorld(streamer.Window, thermalChannel, db.Table, (uint)seed);
        }

        void Start()
        {
            cam = Camera.main;
            if (cam != null && cam.GetComponent<FlyCamera>() == null)
                flyCam = cam.gameObject.AddComponent<FlyCamera>();
            int spawnY = WorldGenerator.SurfaceHeight(0, seed) + 30;
            if (cam != null) cam.transform.position = new Vector3(0f, spawnY, -10f);
            streamer.SetFocus(0, spawnY);

            if (spawnPlayer && cam != null)
            {
                int groundY = WorldGenerator.SurfaceHeight(0, seed) + 4;
                player = PlayerController.Spawn(streamer, effectBus, cam, new Vector2(0.5f, groundY));
                if (flyCam != null) flyCam.enabled = false;
                SetupWeaponCanvasAndPickups(groundY);
            }
        }

        /// <summary>挂载武器装配画布，并在玩家附近摆放演示效果拾取物（重置时先清旧拾取物）。</summary>
        void SetupWeaponCanvasAndPickups(int groundY)
        {
            WeaponCanvasController canvas = GetComponent<WeaponCanvasController>();
            if (canvas == null) canvas = gameObject.AddComponent<WeaponCanvasController>();
            canvas.Bind(player);

            foreach (EffectPickup old in FindObjectsByType<EffectPickup>(FindObjectsSortMode.None))
                Destroy(old.gameObject);
            ProjectileEffectDefinition[] effects = GameContent.LoadAllEffects();
            for (int i = 0; i < effects.Length; i++)
            {
                float x = 0.5f + (i - (effects.Length - 1) * 0.5f) * 6f;
                Color color = Color.HSVToRGB((i * 0.17f) % 1f, 0.6f, 0.95f);
                EffectPickup.Spawn(effects[i], new Vector2(x, groundY + 1f), color);
            }
        }

        void OnMaterialsRebuilt()
        {
            engine.Table = db.Table;
            effectWorld.Table = db.Table;
        }

        void Update()
        {
            HandleModeToggle();
            HandleDebugViewInput();
            HandleResetInput();
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
            FollowPlayer();
            Vector3 p = cam.transform.position;
            streamer.SetFocus(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            streamer.ProcessPendingLoads(3);
            // 效果请求在 tick 间隙统一执行：此时没有模拟 Job 在飞，
            // 写世界安全，且本帧 UpdateViews 能直接看到脏区块
            effectBus.Flush(effectWorld);
            UpdateViews();
        }

        void HandleModeToggle()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || player == null) return;
            if (kb.fKey.wasPressedThisFrame)
            {
                freeFly = !freeFly;
                if (flyCam != null) flyCam.enabled = freeFly;
                if (player != null) player.InputEnabled = !freeFly;
            }
        }

        /// <summary>F1 普通视图 / F2 温度热力图（缺氧式调试视图，可继续扩展 F3+）。</summary>
        void HandleDebugViewInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            int next = debugView;
            if (kb.f1Key.wasPressedThisFrame) next = 0;
            else if (kb.f2Key.wasPressedThisFrame) next = 1;
            if (next == debugView) return;
            debugView = next;
            // 切换视图后全量重绘
            foreach (ChunkView view in views.Values)
                view.PendingRedraw = true;
        }

        /// <summary>温度覆盖层取色：250K 深蓝 → 环境青 → 燃点黄 → 1400K+ 近白红。</summary>
        Color32 TempOverlay(int flatIndex, Cell cell)
        {
            short k = thermalChannel != null ? thermalChannel.GetTempK(flatIndex) : Simulation.Channels.ThermalChannel.AmbientK;
            float f = Mathf.InverseLerp(250f, 1400f, k);
            float hue = Mathf.Lerp(0.62f, 0f, f);
            float sat = Mathf.Lerp(0.9f, 0.25f, f * f);
            // 空气也着色但调暗：隔空的热量能直接在热力图里看到
            float val = cell.MaterialId == BuiltinMaterials.Empty ? 0.45f : Mathf.Lerp(0.55f, 1f, f);
            return Color.HSVToRGB(hue, sat, val);
        }

        void HandleResetInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
                ResetWorld();
        }

        /// <summary>
        /// 世界重置：清存档、重建模拟（全新生成的地形），
        /// 玩家销毁重生，相机回出生点。
        /// </summary>
        void ResetWorld()
        {
            foreach (ChunkView view in views.Values)
                pool.Release(view);
            views.Clear();
            staleKeys.Clear();

            engine.Dispose();
            streamer.DeleteSaveData();
            streamer.Dispose();

            BuildSimulation();

            if (player != null)
            {
                // 先停用再销毁：旧流式器已释放，避免旧玩家同帧 Update 访问失效数据
                player.gameObject.SetActive(false);
                Destroy(player.gameObject);
                player = null;
            }
            freeFly = false;
            if (flyCam != null) flyCam.enabled = false;

            int spawnY = WorldGenerator.SurfaceHeight(0, seed) + 30;
            if (cam != null) cam.transform.position = new Vector3(0f, spawnY, -10f);
            streamer.SetFocus(0, spawnY);

            if (spawnPlayer && cam != null)
            {
                int groundY = WorldGenerator.SurfaceHeight(0, seed) + 4;
                player = PlayerController.Spawn(streamer, effectBus, cam, new Vector2(0.5f, groundY));
                SetupWeaponCanvasAndPickups(groundY);
            }
        }

        void FollowPlayer()
        {
            if (player == null || freeFly) return;
            Vector3 target = player.transform.position;
            Vector3 pos = cam.transform.position;
            float t = 1f - Mathf.Exp(-6f * Time.deltaTime);
            cam.transform.position = new Vector3(
                Mathf.Lerp(pos.x, target.x, t),
                Mathf.Lerp(pos.y, target.y, t),
                -10f);
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
            else if (kb.digit8Key.wasPressedThisFrame) brushMaterial = BrushOrder[7];
            else if (kb.digit9Key.wasPressedThisFrame) brushMaterial = BrushOrder[8];
            else if (kb.digit0Key.wasPressedThisFrame) brushMaterial = BrushOrder[9];
            else if (kb.minusKey.wasPressedThisFrame) brushMaterial = BrushOrder[10];
            else if (kb.equalsKey.wasPressedThisFrame) brushMaterial = BrushOrder[11];
        }

        void HandleEditInput()
        {
            // 装配画布开启时，鼠标交给画布（右键删节点/断线），不挖世界
            if (WeaponCanvasController.IsOpen) return;
            Mouse mouse = Mouse.current;
            if (mouse == null || cam == null) return;
            // 有玩家时左键留给法杖；右键挖掘，Shift+右键放置笔刷物质
            bool shift = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            bool dig = player == null
                ? mouse.rightButton.isPressed || mouse.leftButton.isPressed
                : mouse.rightButton.isPressed && !shift;
            bool place = player == null
                ? mouse.rightButton.isPressed
                : mouse.rightButton.isPressed && shift;
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
            // 调试视图下温度每 tick 都在变，放开重绘预算逐帧刷新
            int maxRedrawsPerFrame = debugView == 0 ? 4 : 64;
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
                        ? view.PendingRedraw || streamer.Window.ChunkDirty[windowIndex] == 1 || debugView != 0
                        : view.PendingRedraw;
                    if (!dirty || redraws >= maxRedrawsPerFrame) continue;

                    if (inWindow)
                    {
                        int start = ((cy - streamer.Window.OriginChunkY) * SimCoords.ChunkSize)
                            * streamer.Window.Width
                            + (cx - streamer.Window.OriginChunkX) * SimCoords.ChunkSize;
                        if (debugView == 1)
                        {
                            view.RedrawFromWindowOverlay(streamer.Window.ReadArray,
                                streamer.Window.Width, start, TempOverlay);
                        }
                        else
                        {
                            view.RedrawFromWindow(streamer.Window.ReadArray,
                                streamer.Window.Width, start, db);
                        }
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
            if (player != null)
            {
                GUILayout.Label($"生命 {player.Character.CurrentHealth:F0}   法力 {player.Wand.CurrentMana:F0}   状态 {player.Character.Fsm.Current?.Name}");
                var equipped = new System.Text.StringBuilder("装备:");
                foreach (string slot in player.Equipment.Slots)
                    equipped.Append(' ').Append(player.Equipment.Get(slot)?.DisplayName);
                if (equipped.Length == 3) equipped.Append(" (G 戒指 / H 核心)");
                GUILayout.Label(equipped.ToString());
                GUILayout.Label($"笔刷: {db?.GetName(brushMaterial)} (1-0/-/=)   AD走/空格跳/左键施法/右键挖/Shift+右键放/F自由视角/R重置/F1普通/F2温度");
            }
            else
            {
                GUILayout.Label($"笔刷: {db?.GetName(brushMaterial)}   (数字键 1-0/-/= 选择)");
                GUILayout.Label("左键挖掘 / 右键放置 / WASD 移动 / Shift 加速 / 滚轮缩放 / R 重置世界 / F1 普通 / F2 温度");
            }
            GUILayout.EndArea();
            DrawProbePanel();
        }

        /// <summary>鼠标探针：显示指针所在格的物质与各物理场通道数据（缺氧式）。</summary>
        void DrawProbePanel()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || cam == null || streamer == null || engine == null) return;
            Vector3 screen = mouse.position.ReadValue();
            screen.z = -cam.transform.position.z;
            Vector3 world = cam.ScreenToWorldPoint(screen);
            int cx = Mathf.FloorToInt(world.x);
            int cy = Mathf.FloorToInt(world.y);

            GUILayout.BeginArea(new Rect(10, 165, 360, 105), GUI.skin.box);
            GUILayout.Label($"鼠标格 ({cx}, {cy})   视图: {(debugView == 0 ? "普通" : "温度")}");
            if (!streamer.Window.ContainsCell(cx, cy))
            {
                GUILayout.Label("窗口外（区块未驻留）");
            }
            else
            {
                int flat = streamer.Window.FlatIndexOf(cx, cy);
                Cell cell = streamer.Window.ReadArray[flat];
                GUILayout.Label($"物质 {db.GetName(cell.MaterialId)}   寿命 {cell.State}   变体 {cell.Variant}");
                // 各物理场通道的探针行：实现 ISimProbe 的通道自动出现在这里
                foreach (ISimChannel channel in engine.Channels)
                {
                    if (channel is ISimProbe probe)
                        GUILayout.Label($"{channel.Name}  {probe.ProbeLine(flat)}");
                }
                // ChunkAwake 按窗口局部区块索引，须先换算（负坐标直接移位会越界）
                int ci = streamer.Window.WindowChunkIndex(
                    cx >> SimCoords.ChunkShift, cy >> SimCoords.ChunkShift);
                GUILayout.Label($"区块 {(streamer.Window.ChunkAwake[ci] == 1 ? "唤醒" : "休眠")}");
            }
            GUILayout.EndArea();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            engine?.Dispose();
            streamer?.SaveAll();
            streamer?.Dispose();
            if (db != null)
            {
                db.Rebuilt -= OnMaterialsRebuilt;
                // 数据库是常驻资产，仅释放原生查找表
                db.DisposeTable();
            }
        }
    }
}
