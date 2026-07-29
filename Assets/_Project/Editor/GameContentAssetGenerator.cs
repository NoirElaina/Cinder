using System.Collections.Generic;
using Cinder.Game.Characters;
using Cinder.Game.Effects;
using Cinder.Game.Items;
using Cinder.Game.Spells;
using Cinder.Runtime.Materials;
using Cinder.Simulation;
using UnityEditor;
using UnityEngine;

namespace Cinder.EditorTools
{
    /// <summary>
    /// 一键生成全部游戏内容资产（物质/效果/法术/法杖/物品/角色）。
    /// 由编辑器 API 直接序列化，脚本绑定与引用关系绝对可靠；
    /// 重复运行会覆盖同名资产，即以本脚本为数据真源。
    /// </summary>
    public static class GameContentAssetGenerator
    {
        const string Root = "Assets/_Project/Resources/Cinder";

        // CreateAsset 落盘的是字段默认值，之后赋的字段必须显式标脏才会被保存
        static readonly List<Object> dirtyAssets = new List<Object>();

        [MenuItem("Cinder/Generate Game Content Assets")]
        public static void Generate()
        {
            dirtyAssets.Clear();
            EnsureFolder("Assets/_Project", "Resources");
            EnsureFolder("Assets/_Project/Resources", "Cinder");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Effects");
            EnsureFolder(Root, "Spells");
            EnsureFolder(Root, "Wands");
            EnsureFolder(Root, "Items");
            EnsureFolder(Root, "Characters");

            // ---- 物质 ----
            var bedrock = Mat("Mat_Bedrock", BuiltinMaterials.Bedrock, "基岩", MatterType.StaticSolid, 255,
                colors: C(40, 40, 48, 34, 34, 42));
            var rock = Mat("Mat_Rock", BuiltinMaterials.Rock, "岩石", MatterType.StaticSolid, 200,
                colors: C(110, 105, 100, 95, 90, 88, 122, 116, 106));
            var dirt = Mat("Mat_Dirt", BuiltinMaterials.Dirt, "泥土", MatterType.StaticSolid, 180,
                colors: C(120, 85, 55, 105, 72, 45, 132, 96, 62));
            var sand = Mat("Mat_Sand", BuiltinMaterials.Sand, "沙", MatterType.Powder, 160,
                colors: C(210, 185, 130, 200, 175, 120, 222, 197, 142));
            var water = Mat("Mat_Water", BuiltinMaterials.Water, "水", MatterType.Liquid, 100, fluidity: 220,
                colors: C(45, 110, 220, 40, 100, 210, 60, 125, 235));
            var wood = Mat("Mat_Wood", BuiltinMaterials.Wood, "木头", MatterType.StaticSolid, 150, flammability: 180,
                colors: C(110, 70, 40, 95, 60, 32, 124, 80, 46));
            var fire = Mat("Mat_Fire", BuiltinMaterials.Fire, "火焰", MatterType.Fire, 5, fluidity: 160, baseLife: 40,
                colors: C(250, 180, 40, 245, 120, 20, 255, 220, 90, 235, 80, 10));
            var oil = Mat("Mat_Oil", BuiltinMaterials.Oil, "油", MatterType.Liquid, 90, fluidity: 200, flammability: 210,
                colors: C(45, 35, 30, 55, 42, 32, 38, 30, 26));
            var acid = Mat("Mat_Acid", BuiltinMaterials.Acid, "酸液", MatterType.Liquid, 110, fluidity: 210,
                colors: C(80, 220, 80, 60, 200, 70, 100, 235, 95));
            var steam = Mat("Mat_Steam", BuiltinMaterials.Steam, "蒸汽", MatterType.Gas, 10, fluidity: 180,
                colors: C(200, 200, 205, 215, 215, 220));
            var smoke = Mat("Mat_Smoke", BuiltinMaterials.Smoke, "烟", MatterType.Gas, 15, fluidity: 120, baseLife: 150,
                colors: C(90, 90, 95, 105, 105, 110, 75, 75, 80));
            var lava = Mat("Mat_Lava", BuiltinMaterials.Lava, "岩浆", MatterType.Liquid, 200, fluidity: 60,
                colors: C(255, 90, 20, 230, 60, 10, 255, 140, 40));
            var ice = Mat("Mat_Ice", BuiltinMaterials.Ice, "冰", MatterType.StaticSolid, 92,
                colors: C(170, 220, 245, 190, 230, 250, 150, 205, 240));

            // ---- 热学接线（温度通道数据）----
            bedrock.Conductivity = 90;
            rock.Conductivity = 90;
            dirt.Conductivity = 60;
            sand.Conductivity = 70;
            acid.Conductivity = 100;
            steam.Conductivity = 30;
            smoke.Conductivity = 20;
            fire.Conductivity = 60;
            fire.SelfTempK = 1073;
            lava.Conductivity = 100;
            lava.SelfTempK = 1400;
            wood.Conductivity = 40;
            wood.IgnitePointK = 573;
            wood.BurnsInto = fire;
            oil.Conductivity = 50;
            oil.IgnitePointK = 520;
            oil.BurnsInto = fire;
            water.Conductivity = 120;
            water.BoilPointK = 373;
            water.BoilsInto = steam;
            water.FreezePointK = 273;
            water.FreezesInto = ice;
            ice.Conductivity = 110;
            ice.MeltPointK = 300;
            ice.MeltsInto = water;

            var materials = new List<MaterialDefinition>
            {
                bedrock, rock, dirt, sand, water, wood, fire, oil, acid, steam, smoke, lava, ice,
            };

            // 物质落盘后再建数据库引用，否则跨资源引用可能落空
            FlushAssets();

            // ---- 物质数据库（私有列表，走 SerializedObject）----
            var db = Create<MaterialDatabase>($"{Root}/MaterialDatabase.asset");
            var so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("materials");
            list.arraySize = materials.Count;
            for (int i = 0; i < materials.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];

            // 反应表：岩浆淬水/淬冰成岩，酸腐蚀常规固体
            SerializedProperty reactionList = so.FindProperty("reactions");
            reactionList.arraySize = 6;
            SetReaction(reactionList.GetArrayElementAtIndex(0), lava, water, 0.9f, outA: rock, outB: steam);
            SetReaction(reactionList.GetArrayElementAtIndex(1), lava, ice, 0.9f, outA: rock, outB: water);
            SetReaction(reactionList.GetArrayElementAtIndex(2), acid, rock, 0.25f, consumeB: true);
            SetReaction(reactionList.GetArrayElementAtIndex(3), acid, dirt, 0.3f, consumeB: true);
            SetReaction(reactionList.GetArrayElementAtIndex(4), acid, sand, 0.3f, consumeB: true);
            SetReaction(reactionList.GetArrayElementAtIndex(5), acid, wood, 0.3f, consumeB: true);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- 效果 ----
            var fireTrail = Create<TrailEffect>($"{Root}/Effects/Effect_FireTrail.asset");
            fireTrail.ModuleId = "effect.fire_trail";
            fireTrail.DisplayName = "火焰拖尾";
            fireTrail.TrailMaterial = BuiltinMaterials.Fire;

            var damageUp = Create<StatModifierEffect>($"{Root}/Effects/Effect_DamageUp.asset");
            damageUp.ModuleId = "effect.damage_up";
            damageUp.DisplayName = "伤害强化";
            damageUp.DamageAdd = 6f;
            damageUp.DamageMultiply = 1.5f;

            var explosive = Create<ExplosiveEffect>($"{Root}/Effects/Effect_Explosive.asset");
            explosive.ModuleId = "effect.explosive";
            explosive.DisplayName = "爆炸";
            explosive.RadiusAdd = 4;

            // 效果落盘后再建法术引用
            FlushAssets();

            // ---- 法术 ----
            var spark = Create<ProjectileSpellDefinition>($"{Root}/Spells/Spell_SparkBolt.asset");
            spark.ModuleId = "spell.spark_bolt";
            spark.DisplayName = "火花弹";
            spark.ManaCost = 5f;
            spark.BaseSpec = new ProjectileSpec
            {
                Damage = 12f, Speed = 90f, Gravity = 25f, Lifetime = 2.5f,
                DigPower = 1, Pierce = 0, TrailMaterial = 0,
                Tint = new Color32(255, 230, 120, 255),
            };

            var heavy = Create<ProjectileSpellDefinition>($"{Root}/Spells/Spell_HeavyBolt.asset");
            heavy.ModuleId = "spell.heavy_bolt";
            heavy.DisplayName = "沉重弹丸";
            heavy.ManaCost = 12f;
            heavy.BaseSpec = new ProjectileSpec
            {
                Damage = 28f, Speed = 55f, Gravity = 45f, Lifetime = 3f,
                DigPower = 2, Pierce = 0, TrailMaterial = 0,
                Tint = new Color32(120, 200, 255, 255),
            };

            var multicast = Create<MulticastSpellDefinition>($"{Root}/Spells/Spell_Multicast2.asset");
            multicast.ModuleId = "spell.multicast2";
            multicast.DisplayName = "双重施法";
            multicast.ManaCost = 4f;
            multicast.Count = 2;
            multicast.SpreadStep = 8f;

            var modFireTrail = Create<ModifierSpellDefinition>($"{Root}/Spells/Spell_ModFireTrail.asset");
            modFireTrail.ModuleId = "spell.mod_fire_trail";
            modFireTrail.DisplayName = "火焰轨迹";
            modFireTrail.ManaCost = 2f;
            modFireTrail.Effect = fireTrail;

            var modExplosive = Create<ModifierSpellDefinition>($"{Root}/Spells/Spell_ModExplosive.asset");
            modExplosive.ModuleId = "spell.mod_explosive";
            modExplosive.DisplayName = "爆裂修饰";
            modExplosive.ManaCost = 8f;
            modExplosive.Effect = explosive;

            var trigger = Create<TriggerSpellDefinition>($"{Root}/Spells/Spell_TriggerSpark.asset");
            trigger.ModuleId = "spell.trigger_spark";
            trigger.DisplayName = "触发火花";
            trigger.ManaCost = 6f;
            trigger.Payload = heavy;
            trigger.CarrierSpec = new ProjectileSpec
            {
                Damage = 0f, Speed = 60f, Gravity = 20f, Lifetime = 3f,
                DigPower = 0, Pierce = 0, TrailMaterial = 0,
                Tint = new Color32(180, 220, 255, 255),
            };

            // 法术落盘后再建法杖引用
            FlushAssets();

            // ---- 法杖 ----
            var wand = Create<WandDefinition>($"{Root}/Wands/Wand_Starter.asset");
            wand.ModuleId = "wand.starter";
            wand.DisplayName = "学徒法杖";
            wand.CastDelay = 0.18f;
            wand.RechargeTime = 0.35f;
            wand.ManaMax = 120f;
            wand.ManaRegen = 25f;
            wand.Capacity = 6;
            wand.DefaultSpells.Clear();
            wand.DefaultSpells.Add(multicast);
            wand.DefaultSpells.Add(modFireTrail);
            wand.DefaultSpells.Add(spark);

            // ---- 物品 ----
            var ring = Create<ItemDefinition>($"{Root}/Items/Item_SwiftRing.asset");
            ring.ModuleId = "item.swift_ring";
            ring.DisplayName = "迅捷戒指";
            ring.EquipSlot = "charm1";
            ring.Modifiers = new[]
            {
                new AttributeModifierEntry
                    { Attribute = CharacterAttributes.MoveSpeed, Op = Core.Attributes.ModifierOp.Multiply, Value = 1.5f },
                new AttributeModifierEntry
                    { Attribute = CharacterAttributes.JumpStrength, Op = Core.Attributes.ModifierOp.Add, Value = 6f },
            };

            var core = Create<ItemDefinition>($"{Root}/Items/Item_ManaCore.asset");
            core.ModuleId = "item.mana_core";
            core.DisplayName = "聚能核心";
            core.EquipSlot = "charm2";
            core.Modifiers = new[]
            {
                new AttributeModifierEntry
                    { Attribute = WandAttributes.ManaMax, Op = Core.Attributes.ModifierOp.Multiply, Value = 1.5f },
                new AttributeModifierEntry
                    { Attribute = WandAttributes.ManaRegen, Op = Core.Attributes.ModifierOp.Add, Value = 30f },
            };

            // ---- 角色 ----
            var player = Create<CharacterDefinition>($"{Root}/Characters/Character_Player.asset");
            player.ModuleId = "character.player";
            player.DisplayName = "玩家";
            player.MaxHealth = 100f;
            player.MoveSpeed = 22f;
            player.JumpStrength = 38f;

            foreach (Object asset in dirtyAssets)
                EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Cinder] 内容资产生成完毕：{materials.Count} 物质 + 数据库、3 效果、6 法术、1 法杖、2 物品、1 角色 → {Root}");
        }

        static MaterialDefinition Mat(string fileName, ushort id, string displayName, MatterType type,
            int density, int fluidity = 0, int flammability = 0, int baseLife = 0, Color32[] colors = null)
        {
            var def = Create<MaterialDefinition>($"{Root}/Materials/{fileName}.asset");
            def.Id = id;
            def.DisplayName = displayName;
            def.Type = type;
            def.Density = density;
            def.Fluidity = fluidity;
            def.Flammability = flammability;
            def.BaseLife = baseLife;
            def.Palette = colors ?? C(255, 0, 255);

            // 复用旧资产时把热学字段全部归零，保证脚本是唯一数据真源，
            // 后面的热学接线段再按需赋值
            def.Conductivity = 0;
            def.SelfTempK = 0;
            def.IgnitePointK = 0;
            def.MeltPointK = 0;
            def.BoilPointK = 0;
            def.FreezePointK = 0;
            def.BurnsInto = null;
            def.MeltsInto = null;
            def.BoilsInto = null;
            def.FreezesInto = null;
            return def;
        }

        static Color32[] C(params int[] rgb)
        {
            var colors = new Color32[rgb.Length / 3];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color32((byte)rgb[i * 3], (byte)rgb[i * 3 + 1], (byte)rgb[i * 3 + 2], 255);
            return colors;
        }

        static void FlushAssets()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static T Create<T>(string path) where T : ScriptableObject
        {
            // 存在则原地复用：保住 GUID，所有指向它的引用永不落空；
            // 只有类型对不上（绑定损坏）时才删了重建
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                dirtyAssets.Add(asset);
                return asset;
            }

            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            dirtyAssets.Add(asset);
            return asset;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
                AssetDatabase.CreateFolder(parent, name);
        }

        static void SetReaction(SerializedProperty entry, MaterialDefinition a, MaterialDefinition b,
            float chance, MaterialDefinition outA = null, MaterialDefinition outB = null,
            bool consumeA = false, bool consumeB = false)
        {
            entry.FindPropertyRelative("A").objectReferenceValue = a;
            entry.FindPropertyRelative("B").objectReferenceValue = b;
            entry.FindPropertyRelative("Chance").floatValue = chance;
            entry.FindPropertyRelative("OutA").objectReferenceValue = outA;
            entry.FindPropertyRelative("OutB").objectReferenceValue = outB;
            entry.FindPropertyRelative("ConsumeA").boolValue = consumeA;
            entry.FindPropertyRelative("ConsumeB").boolValue = consumeB;
        }
    }
}
