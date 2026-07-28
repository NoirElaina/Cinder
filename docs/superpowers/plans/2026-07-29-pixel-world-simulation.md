# 像素世界模拟引擎实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: 本会话内联执行（用户指示
> "直接开始写"）。步骤用 checkbox 跟踪。

**Goal:** 实现 Noita 式像素物质模拟引擎 + 无限宽/有限深区块流式世界 +
可运行查看器。

**Architecture:** 分块 NativeArray 双缓冲 + 单个 Burst Job 模拟 4x3 区块
窗口；窗口从 WorldGrid 检出/换入区块；物质定义 SO 烘焙为 NativeArray；
每区块 Texture2D 渲染。

**Tech Stack:** Unity 6000.3.10f1、URP 2D、Burst、Collections、
Mathematics、Input System（activeInputHandler=1，仅用新 API）。

**验证:** `& "D:\Software\Unity\Hub\Editor\Unity 6000.3.10f1\Editor\Unity.exe" -batchmode -nographics -projectPath "D:\Issue\Unity\Cinder" -runTests -testPlatform EditMode -testResults Temp\EditModeResults.xml -quit -logFile Temp\unity-editmode.log`

---

### Task 1: 模拟核心数据类型

**Files:**
- Create: `Assets/_Project/Scripts/Simulation/Cinder.Simulation.asmdef`（refs: Burst/Collections/Mathematics，allowUnsafe）
- Create: `Assets/_Project/Scripts/Simulation/SimCoords.cs`
- Create: `Assets/_Project/Scripts/Simulation/Cell.cs`
- Create: `Assets/_Project/Scripts/Simulation/MaterialProps.cs`（MatterType/MaterialProps/BuiltinMaterials/MaterialTable/CreateBuiltin/SimHash）
- Create: `Assets/_Project/Tests/EditMode/Cinder.Tests.EditMode.asmdef`
- Create: `Assets/_Project/Tests/EditMode/SimCoordsTests.cs`

- [ ] 实现 + 单测：负坐标 floor 换算、PackKey 往返、MaterialTable 烘焙索引

### Task 2: 世界生成器

**Files:**
- Create: `Assets/_Project/Scripts/Simulation/ChunkData.cs`
- Create: `Assets/_Project/Scripts/Simulation/WorldGenerator.cs`
- Create: `Assets/_Project/Tests/EditMode/WorldGeneratorTests.cs`

- [ ] 同 seed 同区块字节一致；不同 chunkX 不同；底部区块全基岩；
  表面层含固体、天空含 Empty

### Task 3: 存储与序列化

**Files:**
- Create: `Assets/_Project/Scripts/Simulation/WorldGrid.cs`
- Create: `Assets/_Project/Scripts/Simulation/ChunkSerializer.cs`
- Create: `Assets/_Project/Tests/EditMode/ChunkSerializerTests.cs`

- [ ] 序列化往返字节一致（含坐标头）；WorldGrid 加载/卸载/生成回退

### Task 4: 模拟窗口 + Burst Job + 引擎

**Files:**
- Create: `Assets/_Project/Scripts/Simulation/SimulationWindow.cs`
- Create: `Assets/_Project/Scripts/Simulation/Jobs/FallingSandJob.cs`
- Create: `Assets/_Project/Scripts/Simulation/SimulationEngine.cs`
- Create: `Assets/_Project/Tests/EditMode/FallingSandTests.cs`（沙下落/堆积、水扩散、密度置换）
- Create: `Assets/_Project/Tests/EditMode/FireTests.cs`（蔓延、熄灭）
- Create: `Assets/_Project/Tests/EditMode/SimulationWindowTests.cs`（移位保数据）

- [ ] 全部单测通过

### Task 5: 物质 ScriptableObject 层（热插拔验证）

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/Cinder.Runtime.asmdef`（refs: Simulation/InputSystem）
- Create: `Assets/_Project/Scripts/Runtime/Materials/MaterialDefinition.cs`
- Create: `Assets/_Project/Scripts/Runtime/Materials/MaterialDatabase.cs`
- Create: `Assets/_Project/Tests/EditMode/MaterialDatabaseTests.cs`

- [ ] CreateDefault 调色板正确；Register/Unregister 重建表

### Task 6: 渲染 + 流式加载 + IO

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/World/ChunkView.cs`
- Create: `Assets/_Project/Scripts/Runtime/World/ChunkViewPool.cs`
- Create: `Assets/_Project/Scripts/Runtime/World/ChunkStore.cs`
- Create: `Assets/_Project/Scripts/Runtime/World/WorldStreamer.cs`

### Task 7: 查看器

**Files:**
- Create: `Assets/_Project/Scripts/Runtime/World/WorldController.cs`
- Create: `Assets/_Project/Scripts/Runtime/World/FlyCamera.cs`
- Create: `Assets/_Project/Scripts/Runtime/Dev/DevHud.cs`
- Create: `Assets/_Project/Scripts/Runtime/World/WorldBootstrap.cs`

### Task 8: 验证与收尾

- [ ] Unity batch EditMode 测试全绿
- [ ] BurstGuardVerifier 规约自查（Job 为 struct、无托管字段）
- [ ] 提交
