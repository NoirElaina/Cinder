# Cinder - 热插拔玩法架构设计（子项目 2）

日期：2026-07-29
状态：已获用户批准（方向：SO 数据驱动热插拔 + 设计模式组合）

## 目标

为武器、效果、角色、物品属性提供统一的热插拔框架：各模块可独立
增删替换，运行时生效，新增模块不改框架代码。本层纯数据/逻辑，
不依赖场景，全部可 EditMode 单测。

## 分层

```
Cinder.Core（零依赖）
  Modules/      IModule + ModuleRegistry<T>     注册表模式
  Attributes/   AttributeSet/Attribute/Modifier 装饰器思路的属性修饰栈
  StateMachine/ StateMachine<TContext>/IState   有限状态机
Cinder.Game（依赖 Core）
  Effects/      ProjectileEffectDefinition 等   抽象工厂 + 装饰器
  Weapons/      WeaponDefinition/Instance/Factory 工厂模式 + 组合
  Characters/   CharacterDefinition/Character/States FSM + 属性集
```

## 关键设计

- **模块契约**：所有模块实现 `IModule { string ModuleId; }`，均为
  ScriptableObject 资产。每类模块一个 `ModuleRegistry<T>`，
  Register/Unregister 即热插拔，Registered/Unregistered 事件广播。
- **效果 = 装饰器**：`IProjectileBehavior`（ModifySpec/OnHitWorld），
  `ProjectileEffectDefinition.Decorate(inner)` 工厂方法产出包装器。
  内置：StatModifierEffect（数值修饰）、TrailEffect（拖尾物质）、
  ExplosiveEffect（命中追加挖掘半径）。新增效果类型 = 新 SO 子类。
- **武器 = 组合 + 工厂**：`WeaponDefinition`（基础 ProjectileSpec +
  默认效果 + 射速/耗魔属性）；`WeaponInstance` 持可变效果列表，
  AddEffect/RemoveEffect 热插拔，ComposeSpec 按列表顺序重建装饰链；
  `WeaponFactory` 从定义或注册表按 Id 创建。
- **属性 = 修饰栈**：Attribute = 基础值 + Modifier 列表，
  求值顺序固定 Add -> Multiply -> Override（与注册顺序无关），
  按 Source 整体移除（卸装备）。武器/角色/物品通用。
- **角色 = FSM + 属性集**：Idle/Moving/Jumping/Falling/Dead，
  Enter/Exit 严格配对，生命归零自动 Dead。移动与像素碰撞在
  子项目 3 接入。

## 测试

注册表增删/事件/去重；属性求值顺序/按源移除/变更事件；FSM 转换配对/
非法转换；装饰链顺序/钩子穿透；武器热插拔重组/工厂/属性初始化；
角色伤害死亡/治疗钳制。

## 后续挂钩

子项目 3（法杖法术组合）：法术 = 另一种 IModule；修饰符法术直接复用
ProjectileEffectDefinition 装饰链；施法管线 = 法杖（武器特化）+
法术列表折叠。角色 FSM 接像素碰撞控制器。
