using Cinder.Core.Modules;

namespace Cinder.Game.Spells
{
    /// <summary>法杖工厂：从定义资产或注册表按 Id 创建法杖实例。</summary>
    public static class WandFactory
    {
        public static WandInstance Create(WandDefinition definition) =>
            new WandInstance(definition);

        public static WandInstance Create(ModuleRegistry<WandDefinition> registry, string moduleId)
        {
            WandDefinition definition = registry.Get(moduleId);
            return definition != null ? new WandInstance(definition) : null;
        }
    }
}
