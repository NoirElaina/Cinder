using Cinder.Core.Modules;
using Cinder.Game.Effects;
using Cinder.Simulation;
using UnityEngine;

namespace Cinder.Game.Spells
{
    /// <summary>
    /// 法杖工厂：从定义或注册表按 Id 创建；CreateDefault 纯代码构建
    /// 演示法杖（火花弹 + 火焰拖尾修饰符），零资产可跑。
    /// </summary>
    public static class WandFactory
    {
        public static WandInstance Create(WandDefinition definition) =>
            new WandInstance(definition);

        public static WandInstance Create(ModuleRegistry<WandDefinition> registry, string moduleId)
        {
            WandDefinition definition = registry.Get(moduleId);
            return definition != null ? new WandInstance(definition) : null;
        }

        public static WandInstance CreateDefault()
        {
            var multicast = ScriptableObject.CreateInstance<MulticastSpellDefinition>();
            multicast.ModuleId = "spell.multicast2";
            multicast.DisplayName = "双重施法";
            multicast.ManaCost = 4f;
            multicast.Count = 2;
            multicast.SpreadStep = 8f;

            var trail = ScriptableObject.CreateInstance<TrailEffect>();
            trail.ModuleId = "effect.fire_trail";
            trail.DisplayName = "火焰拖尾";
            trail.TrailMaterial = BuiltinMaterials.Fire;

            var fireTrailSpell = ScriptableObject.CreateInstance<ModifierSpellDefinition>();
            fireTrailSpell.ModuleId = "spell.mod_fire_trail";
            fireTrailSpell.DisplayName = "火焰轨迹";
            fireTrailSpell.ManaCost = 2f;
            fireTrailSpell.Effect = trail;

            var spark = ScriptableObject.CreateInstance<ProjectileSpellDefinition>();
            spark.ModuleId = "spell.spark_bolt";
            spark.DisplayName = "火花弹";
            spark.ManaCost = 5f;
            spark.BaseSpec = new ProjectileSpec
            {
                Damage = 12f,
                Speed = 90f,
                Gravity = 25f,
                Lifetime = 2.5f,
                DigPower = 1,
                Pierce = 0,
                TrailMaterial = 0,
                Tint = new Color32(255, 230, 120, 255),
            };

            var wand = ScriptableObject.CreateInstance<WandDefinition>();
            wand.ModuleId = "wand.starter";
            wand.DisplayName = "学徒法杖";
            wand.CastDelay = 0.18f;
            wand.RechargeTime = 0.35f;
            wand.ManaMax = 120f;
            wand.ManaRegen = 25f;
            wand.Capacity = 6;
            wand.DefaultSpells.Add(multicast);
            wand.DefaultSpells.Add(fireTrailSpell);
            wand.DefaultSpells.Add(spark);

            return new WandInstance(wand);
        }
    }
}
