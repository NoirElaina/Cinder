using Cinder.Game.Characters;
using Cinder.Game.Items;
using Cinder.Game.Spells;
using Cinder.Runtime.Materials;
using UnityEngine;

namespace Cinder.Runtime
{
    /// <summary>
    /// 游戏内容目录：优先从 Resources/Cinder 加载数据资产（热插拔数据源，
    /// 在 Project 窗口直接编辑/增删即可），资产缺失时回退到纯代码默认构建，
    /// 保证零资产也能跑。
    /// </summary>
    public static class GameContent
    {
        /// <summary>ownsInstance = true 表示返回的是代码创建的临时实例，使用方负责 Destroy。</summary>
        public static MaterialDatabase LoadMaterials(out bool ownsInstance)
        {
            var db = Resources.Load<MaterialDatabase>("Cinder/MaterialDatabase");
            if (db != null)
            {
                db.Rebuild();
                ownsInstance = false;
                return db;
            }
            ownsInstance = true;
            return MaterialDatabase.CreateDefault();
        }

        public static CharacterDefinition LoadPlayerCharacter()
        {
            var def = Resources.Load<CharacterDefinition>("Cinder/Characters/Character_Player");
            if (def != null) return def;

            var fallback = ScriptableObject.CreateInstance<CharacterDefinition>();
            fallback.ModuleId = "character.player";
            fallback.DisplayName = "玩家";
            fallback.MaxHealth = 100f;
            fallback.MoveSpeed = 22f;
            fallback.JumpStrength = 38f;
            return fallback;
        }

        public static WandInstance LoadStarterWand()
        {
            var def = Resources.Load<WandDefinition>("Cinder/Wands/Wand_Starter");
            return def != null ? WandFactory.Create(def) : WandFactory.CreateDefault();
        }

        public static ItemDefinition[] LoadStarterItems()
        {
            var ring = Resources.Load<ItemDefinition>("Cinder/Items/Item_SwiftRing");
            var core = Resources.Load<ItemDefinition>("Cinder/Items/Item_ManaCore");
            if (ring == null) ring = ItemFactory.CreateDemoRing();
            if (core == null) core = ItemFactory.CreateDemoCore();
            return new[] { ring, core };
        }
    }
}
