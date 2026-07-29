using Cinder.Core.Modules;
using UnityEngine;

namespace Cinder.Game.Effects
{
    /// <summary>
    /// 效果模块基类（抽象工厂 + 装饰器）。新增一种效果 = 新建本类的一个
    /// 子类资产并注册，框架代码零改动；给武器加/卸效果 = 运行时增删本资产，
    /// WeaponInstance 重建装饰链即可。
    /// </summary>
    public abstract class ProjectileEffectDefinition : ScriptableObject, IModule
    {
        [SerializeField] string moduleId = "effect.unnamed";
        [SerializeField] string displayName = "Unnamed Effect";

        public string ModuleId { get => moduleId; set => moduleId = value; }
        public string DisplayName { get => displayName; set => displayName = value; }

        /// <summary>工厂方法：创建包装 inner 的装饰器实例。</summary>
        public abstract IProjectileBehavior Decorate(IProjectileBehavior inner);
    }
}
