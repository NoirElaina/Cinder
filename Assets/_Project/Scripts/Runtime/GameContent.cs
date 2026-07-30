using System.Collections.Generic;
using Cinder.Game.Characters;
using Cinder.Game.Effects;
using Cinder.Game.Items;
using Cinder.Game.Spells;
using Cinder.Runtime.Materials;
using UnityEngine;

namespace Cinder.Runtime
{
    /// <summary>
    /// 游戏内容目录：从 Resources/Cinder 加载数据资产，这是唯一数据源。
    /// 资产缺失即报错（数据驱动，不设代码回退）；
    /// 缺资产时运行一次菜单 Cinder → Generate Game Content Assets 即可生成全套。
    /// </summary>
    public static class GameContent
    {
        public const string MaterialsPath = "Cinder/MaterialDatabase";
        public const string PlayerCharacterPath = "Cinder/Characters/Character_Player";
        public const string StarterWandPath = "Cinder/Wands/Wand_Starter";
        public const string SwiftRingPath = "Cinder/Items/Item_SwiftRing";
        public const string ManaCorePath = "Cinder/Items/Item_ManaCore";

        public static MaterialDatabase LoadMaterials()
        {
            var db = Load<MaterialDatabase>(MaterialsPath, required: true);
            if (db != null) db.Rebuild();
            return db;
        }

        public static CharacterDefinition LoadPlayerCharacter() =>
            Load<CharacterDefinition>(PlayerCharacterPath, required: true);

        public static WandInstance LoadStarterWand()
        {
            var def = Load<WandDefinition>(StarterWandPath, required: true);
            return def != null ? WandFactory.Create(def) : null;
        }

        /// <summary>初始物品（可选内容，缺失仅告警不阻断）。</summary>
        public static ItemDefinition[] LoadStarterItems()
        {
            var items = new List<ItemDefinition>(2);
            ItemDefinition ring = Load<ItemDefinition>(SwiftRingPath, required: false);
            ItemDefinition core = Load<ItemDefinition>(ManaCorePath, required: false);
            if (ring != null) items.Add(ring);
            if (core != null) items.Add(core);
            return items.ToArray();
        }

        /// <summary>全部效果资产（Cinder/Effects 下），供效果背包/拾取物使用。缺失仅告警。</summary>
        public static ProjectileEffectDefinition[] LoadAllEffects()
        {
            ProjectileEffectDefinition[] all = Resources.LoadAll<ProjectileEffectDefinition>("Cinder/Effects");
            if (all == null || all.Length == 0)
                Debug.LogWarning("[Cinder] 未找到效果资产: Assets/_Project/Resources/Cinder/Effects（运行菜单 Cinder → Generate Game Content Assets 生成）");
            return all;
        }

        static T Load<T>(string path, bool required) where T : Object
        {
            var asset = Resources.Load<T>(path);
            if (asset != null) return asset;

            string message = $"[Cinder] 内容资产缺失: Assets/_Project/Resources/{path}.asset" +
                "（运行菜单 Cinder → Generate Game Content Assets 生成）";
            if (required) Debug.LogError(message);
            else Debug.LogWarning(message);
            return null;
        }
    }
}
