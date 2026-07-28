# Cinder - 像素世界模拟引擎设计（子项目 1）

日期：2026-07-29
状态：已获用户批准

## 目标

对标 Noita 的真像素级物质模拟：无限宽度、有限深度的世界，区块动态加载，
可破坏地形。本设计只覆盖世界模拟引擎与查看器；热插拔玩法架构（武器/效果/
角色/物品）与法术组合系统为后续子项目，但本期通过"物质定义即
ScriptableObject 热插拔"验证注册表机制。

已确认的边界：
- 热插拔 = ScriptableObject 数据驱动注册表（运行时增删，无需改代码）。
- 法杖系统档位 = 精简版（投射物 + 修饰符流水线，接口兼容未来触发类）。
- 角色碰撞 = 自定义像素碰撞（后续子项目）。
- 美术 = 程序化像素/调色板，不依赖外部资源。

## 总体架构

```
Assets/_Project/Scripts/
  Simulation/   纯数据模拟引擎（无 MonoBehaviour，可纯单测）  Cinder.Simulation.asmdef
  Runtime/      MonoBehaviour 胶水：渲染、流式加载、输入、引导  Cinder.Runtime.asmdef
  Game/         玩法层（本期占位）
  Core/         共享基础设施（本期占位，子项目 2 使用）
Assets/_Project/Tests/EditMode/                              Cinder.Tests.EditMode.asmdef
```

依赖方向：Runtime -> Simulation。Simulation 只依赖 Unity.Collections /
Burst / Mathematics。

## 数据模型

- `Cell`（4 字节 blittable）：`MaterialId(ushort)`、`Variant(byte)`、
  `State(byte)`、`Flags(byte，bit0=本帧已移动)`。
- 区块 128x128 格（`SimCoords.ChunkShift=7`）。X 无限，Y 有限：
  `MinChunkY=-31, MaxChunkY=4`（约 4096 格深 + 天空）。
- 坐标换算：位运算（负数坐标 `>>` 即 floor 语义）。
- 世界存储 `WorldGrid`：`Dictionary<long, ChunkData>`，按需生成/读盘。

## 模拟管线

- **SimulationWindow**：覆盖以焦点为中心的 W x H 区块（默认 4x3），
  平坦 `NativeArray<Cell>` 双缓冲。窗口"检出"存储区块，移位时换出/换入，
  每帧零拷贝。
- **FallingSandJob**（Burst，单 Job）：先 Read->Write 拷贝并清移动标记，
  再自下而上扫描，X 方向按 tick 奇偶交替。移动规则：
  - Powder：直下 -> 斜下（随机左右序）；目标为更低密度流体则置换下沉。
  - Liquid：直下 -> 斜下 -> 水平（Fluidity 作为概率权重）。
  - Gas：直上 -> 斜上 -> 水平。
  - Fire：State 寿命递减归零转 Empty；向上飘；按 Flammability 点燃邻居；
    邻水概率熄灭。
  - StaticSolid / Empty：不动。
- 随机性全部来自 `Hash(x, y, tick, seed)`，完全确定，可单测。
- **休眠/唤醒**：Job 输出每区块 moved 计数；0 移动且无邻居移动 -> 休眠；
  写入落入休眠区块即置 moved -> 次帧唤醒；外部编辑（挖掘）直接唤醒。
- 固定 30Hz 模拟，与渲染解耦。

## 物质系统（热插拔验证）

- `MaterialDefinition`（ScriptableObject）：Id、类型、密度、流动性、
  可燃性、寿命、调色板（Variant 取色）。
- `MaterialDatabase`（SO）：Register/Unregister 运行时增删 -> 重建
  `NativeArray<MaterialProps>`（Burst 直读）+ 调色板表。注销后该 Id
  按 Empty 处理。`CreateDefault()` 纯代码生成内置物质（岩石/泥土/沙/
  水/木头/火/基岩），查看器零资产可跑。

## 区块生命周期

- 两级半径：模拟窗口（4x3）< 驻留环（约 7x5）。驻留环内区块保留内存供
  渲染；环外卸载：改过 -> 序列化到 `persistentDataPath/world/{seed}/`，
  未改 -> 丢弃（生成确定性可重建）。
- 生成：seed + Perlin 高度图（表层沙/泥土，深层岩石）+ 洞穴噪声 +
  水囊；底部区块全基岩。Burst 可编译，纯函数。
- IO：加载同步（64KB 亚毫秒），保存后台 Task。格式 `CNK1` 头 + 原始
  Cell 字节。

## 渲染

- 每可见区块一张 128x128 `Texture2D`（Point 过滤），SpriteRenderer，
  1 格 = 1 世界单位。仅重绘 moved/新出现的区块。窗口内区块从窗口数组
  取样，驻留环区块从存储取样。ChunkView 池化。
- 正交相机像素对齐；程序化调色板 + Variant 抖动。

## 查看器（本期交付物）

- `WorldBootstrap`：`RuntimeInitializeOnLoadMethod` 自动创建世界与相机，
  任意场景按 Play 即可运行。
- WASD/方向键飞行相机 + 滚轮缩放；左键挖掘、右键放置、数字键选物质；
  OnGUI 开发 HUD（FPS、笔刷、区块统计）。

## 错误处理

- 读盘失败 -> 重新生成并警告；保存失败重试一次后放弃（不阻塞卸载）。
- Job 开发期 `CompileSynchronously`（沿用 BurstGuardVerifier 规约）。

## 测试

EditMode：坐标换算（负坐标）、沙下落/堆积、水扩散、密度置换、火蔓延/
熄灭、世界生成确定性/基岩层、序列化往返、窗口移位保数据、物质注册/
注销。验证方式：Unity batch mode `-runTests -testPlatform EditMode`。

## 后续子项目挂钩

- 子项目 2（热插拔架构）复用 MaterialDatabase 的注册表模式到武器/效果。
- 子项目 3（玩法）通过 `IWorldEditor` 风格接口挖掘/查询世界，
  不直接碰 Simulation 内部。
