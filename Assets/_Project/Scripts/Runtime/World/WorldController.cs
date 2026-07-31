using Cinder.Game.Effects;
using Cinder.Runtime.Materials;
using Cinder.Runtime.Player;
using Cinder.Runtime.UI;
using Cinder.Simulation;
using Cinder.Simulation.Channels;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinder.Runtime.World
{
    /// <summary>
    /// 世界总控：固定步长驱动模拟、按相机位置流式加载、
    /// 驱动 Cell Surface 渲染、处理笔刷输入。任意场景按 Play 即可运行（见 WorldBootstrap）。
    /// 相机/角色用世界单位，模拟/挖掘用细格，换算只走 WorldScale。
    /// </summary>
    public sealed class WorldController : MonoBehaviour
    {
        public static WorldController Instance { get; private set; }

        [SerializeField] int seed = 1337;
        [SerializeField] float ticksPerSecond = 30f;

        [Tooltip("笔刷半径（细格）")]
        [SerializeField] int brushRadius = 12;
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
        ThermalChannel thermalChannel;
        LightChannel lightChannel;
        EffectBus effectBus;
        SimEffectWorld effectWorld;
        CellSurfaceRenderer surface;

        Camera cam;
        FlyCamera flyCam;
        PlayerController player;
        bool freeFly;
        int debugView; // 0 = 普通, 1 = 温度热力图
        float tickAccumulator;
        ushort brushMaterial = BuiltinMaterials.Sand;

        /// <summary>本帧世界内容是否变过（tick/笔刷/效果/窗口平移），
        /// 未变则渲染器跳过重新打包上传（30tps 下约省一半帧的打包成本）。</summary>
        bool worldDirty = true;

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
            surface = CellSurfaceRenderer.Create(transform);
            surface.Bind(streamer.Window, lightChannel, thermalChannel, db);
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
            engine.AddChannel(new ReactionChannel());
            thermalChannel = new ThermalChannel();
            engine.AddChannel(thermalChannel);
            lightChannel = new LightChannel();
            engine.AddChannel(lightChannel);
            effectWorld = new SimEffectWorld(streamer.Window, thermalChannel, db.Table, (uint)seed);
        }

        void Start()
        {
            cam = Camera.main;
            if (cam != null && cam.GetComponent<FlyCamera>() == null)
                flyCam = cam.gameObject.AddComponent<FlyCamera>();

            int surfaceCell = WorldGenerator.SurfaceHeight(0, seed);
            float surfaceY = WorldScale.CellToWorld(surfaceCell);
            if (cam != null) cam.transform.position = new Vector3(0f, surfaceY + 8f, -10f);
            streamer.SetFocus(0, surfaceCell + 32);
            RebuildLight(); // 首帧就有光，不等第一个 tick

            if (spawnPlayer && cam != null)
            {
                player = PlayerController.Spawn(streamer, effectBus, cam,
                    new Vector2(0.125f, surfaceY + 2f));
                if (flyCam != null) flyCam.enabled = false;
                SetupWeaponCanvasAndPickups(surfaceY);
            }
        }

        /// <summary>挂载武器装配画布，并在玩家附近摆放演示效果拾取物（重置时先清旧拾取物）。</summary>
        void SetupWeaponCanvasAndPickups(float groundY)
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
                EffectPickup.Spawn(effects[i], new Vector2(x, groundY + 2f), color);
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
            while (tickAccumulator >= interval && steps < 2)
            {
                engine.Step();
                tickAccumulator -= interval;
                steps++;
            }
            if (steps > 0) worldDirty = true;
            // 死亡螺旋防护：单 tick 超预算时丢弃积欠时间（模拟慢放），
            // 绝不允许每帧追更多 tick 把帧率锁死在谷底
            if (tickAccumulator > interval) tickAccumulator = interval;

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
            int prevOriginX = streamer.Window.OriginChunkX;
            int prevOriginY = streamer.Window.OriginChunkY;
            streamer.SetFocus(WorldScale.WorldToCell(p.x), WorldScale.WorldToCell(p.y));
            streamer.ProcessPendingLoads(3);
            // 窗口平移后光照场还是旧布局，不等下个 tick，立即重建防闪烁
            if (streamer.Window.OriginChunkX != prevOriginX
                || streamer.Window.OriginChunkY != prevOriginY)
            {
                RebuildLight();
                worldDirty = true;
            }
            // 效果请求在 tick 间隙统一执行：此时没有模拟 Job 在飞，
            // 写世界安全，且本帧 Render 能直接看到最新格子
            if (effectBus.PendingCount > 0) worldDirty = true;
            effectBus.Flush(effectWorld);
            surface.Render(worldDirty);
            worldDirty = false;
        }

        void RebuildLight() => lightChannel.Rebuild(
            streamer.Window.ReadArray, db.Table.Native,
            streamer.Window.Width, streamer.Window.Height);

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

        /// <summary>F1 普通视图 / F2 温度热力图（同一渲染器的调试分支，可继续扩展 F3+）。</summary>
        void HandleDebugViewInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) debugView = 0;
            else if (kb.f2Key.wasPressedThisFrame) debugView = 1;
            surface.DebugMode = debugView;
        }

        void HandleResetInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
                ResetWorld();
        }

        /// <summary>
        /// 世界重置：清存档、重建模拟（全新生成的地形），
        /// 渲染器重绑数据源，玩家销毁重生，相机回出生点。
        /// </summary>
        void ResetWorld()
        {
            engine.Dispose();
            streamer.DeleteSaveData();
            streamer.Dispose();

            BuildSimulation();
            surface.Bind(streamer.Window, lightChannel, thermalChannel, db);

            if (player != null)
            {
                // 先停用再销毁：旧流式器已释放，避免旧玩家同帧 Update 访问失效数据
                player.gameObject.SetActive(false);
                Destroy(player.gameObject);
                player = null;
            }
            freeFly = false;
            if (flyCam != null) flyCam.enabled = false;

            int surfaceCell = WorldGenerator.SurfaceHeight(0, seed);
            float surfaceY = WorldScale.CellToWorld(surfaceCell);
            if (cam != null) cam.transform.position = new Vector3(0f, surfaceY + 8f, -10f);
            streamer.SetFocus(0, surfaceCell + 32);
            RebuildLight();
            worldDirty = true;

            if (spawnPlayer && cam != null)
            {
                player = PlayerController.Spawn(streamer, effectBus, cam,
                    new Vector2(0.125f, surfaceY + 2f));
                SetupWeaponCanvasAndPickups(surfaceY);
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
            streamer.EditSphere(
                WorldScale.WorldToCell(world.x), WorldScale.WorldToCell(world.y),
                dig ? brushRadius + 6 : brushRadius,
                dig ? BuiltinMaterials.Empty : brushMaterial);
            worldDirty = true;
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 360, 150), GUI.skin.box);
            GUILayout.Label($"FPS {Fps}   Tick {engine?.Tick ?? 0}");
            Vector3 p = cam != null ? cam.transform.position : Vector3.zero;
            GUILayout.Label($"相机 ({p.x:F0}, {p.y:F0})   驻留区块 {streamer?.Grid.LoadedCount ?? 0}");
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

        /// <summary>鼠标探针：显示指针所在细格的物质与各物理场通道数据（缺氧式）。</summary>
        void DrawProbePanel()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || cam == null || streamer == null || engine == null) return;
            Vector3 screen = mouse.position.ReadValue();
            screen.z = -cam.transform.position.z;
            Vector3 world = cam.ScreenToWorldPoint(screen);
            int cx = WorldScale.WorldToCell(world.x);
            int cy = WorldScale.WorldToCell(world.y);

            GUILayout.BeginArea(new Rect(10, 165, 360, 120), GUI.skin.box);
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
