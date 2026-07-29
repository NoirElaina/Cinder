using Cinder.Core.Modules;

namespace Cinder.Game.Weapons
{
    /// <summary>
    /// 武器工厂：从定义或注册表按 Id 创建运行时实例。
    /// 新增武器 = 新建 WeaponDefinition 资产并注册，工厂无需改动。
    /// </summary>
    public static class WeaponFactory
    {
        public static WeaponInstance Create(WeaponDefinition definition) =>
            new WeaponInstance(definition);

        /// <summary>按模块 Id 从注册表创建；未注册返回 null。</summary>
        public static WeaponInstance Create(
            ModuleRegistry<WeaponDefinition> registry, string moduleId)
        {
            WeaponDefinition definition = registry.Get(moduleId);
            return definition != null ? new WeaponInstance(definition) : null;
        }
    }
}
