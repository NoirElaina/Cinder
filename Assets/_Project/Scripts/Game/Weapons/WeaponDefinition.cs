using System.Collections.Generic;
using Cinder.Core.Modules;
using Cinder.Game.Effects;
using UnityEngine;

namespace Cinder.Game.Weapons
{
    /// <summary>
    /// 武器模块：基础投射物参数 + 默认效果列表 + 武器属性（射速/耗魔）。
    /// 运行时可热插拔效果，见 WeaponInstance。
    /// </summary>
    [CreateAssetMenu(menuName = "Cinder/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject, IModule
    {
        [SerializeField] string moduleId = "weapon.unnamed";
        [SerializeField] string displayName = "Unnamed Weapon";

        public string ModuleId { get => moduleId; set => moduleId = value; }
        public string DisplayName { get => displayName; set => displayName = value; }

        [Tooltip("每秒发射次数")]
        [Min(0.1f)] public float FireRate = 4f;

        [Min(0f)] public float ManaCost;

        public ProjectileSpec BaseSpec = new ProjectileSpec
        {
            Damage = 10f,
            Speed = 30f,
            Gravity = 0f,
            Lifetime = 2f,
            DigPower = 0,
            Pierce = 0,
            TrailMaterial = 0,
            Tint = new Color32(255, 255, 255, 255),
        };

        public List<ProjectileEffectDefinition> DefaultEffects =
            new List<ProjectileEffectDefinition>();
    }
}
