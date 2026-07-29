using Cinder.Runtime.Materials;
using Cinder.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Cinder.Tests
{
    /// <summary>
    /// 物质数据库测试：直接加载 Resources 里的真实资产验证烘焙结果，
    /// 资产即数据真源，测试不再维护代码版默认内容。
    /// </summary>
    public class MaterialDatabaseTests
    {
        const string DbPath = "Assets/_Project/Resources/Cinder/MaterialDatabase.asset";

        MaterialDatabase db;

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<MaterialDatabase>(DbPath);
            Assert.NotNull(db, "内容资产缺失，请先运行菜单 Cinder → Generate Game Content Assets");
            db.Rebuild();
        }

        [TearDown]
        public void TearDown() => db.DisposeTable();

        [Test]
        public void Asset_BakesBuiltinProps()
        {
            Assert.AreEqual(MatterType.Liquid, db.Table[BuiltinMaterials.Water].Type);
            Assert.AreEqual(MatterType.Powder, db.Table[BuiltinMaterials.Sand].Type);
            Assert.Greater(db.Table[BuiltinMaterials.Wood].Flammability, 0);
        }

        [Test]
        public void Asset_BakesThermalProps()
        {
            Assert.AreEqual((ushort)1400, db.Table[BuiltinMaterials.Lava].SelfTempK);
            Assert.AreEqual(BuiltinMaterials.Steam, db.Table[BuiltinMaterials.Water].BoilsInto);
            Assert.AreEqual(BuiltinMaterials.Fire, db.Table[BuiltinMaterials.Wood].BurnsInto);
            Assert.AreEqual(BuiltinMaterials.Water, db.Table[BuiltinMaterials.Ice].MeltsInto);
        }

        [Test]
        public void Asset_BakesReactions_Symmetric()
        {
            ReactionRule r = db.Table.Reactions[
                BuiltinMaterials.Lava * MaterialTable.Capacity + BuiltinMaterials.Water];
            Assert.AreEqual(1, r.Exists);
            Assert.AreEqual(BuiltinMaterials.Rock, r.OutA);
            Assert.AreEqual(BuiltinMaterials.Steam, r.OutB);

            ReactionRule reverse = db.Table.Reactions[
                BuiltinMaterials.Water * MaterialTable.Capacity + BuiltinMaterials.Lava];
            Assert.AreEqual(1, reverse.Exists, "反应表应对称写入");
        }

        [Test]
        public void GetColor_Water_IsBlueish()
        {
            Color32 c = db.GetColor(BuiltinMaterials.Water, 0);
            Assert.Greater(c.b, c.r);
            Assert.AreEqual(255, c.a);
        }

        [Test]
        public void GetColor_Empty_IsTransparent()
        {
            Assert.AreEqual(0, db.GetColor(BuiltinMaterials.Empty, 0).a);
        }

        [Test]
        public void Register_HotPlugsNewMaterial()
        {
            // 用独立实例验证热插拔，不污染真实资产
            var fresh = ScriptableObject.CreateInstance<MaterialDatabase>();
            fresh.Rebuild();
            int rebuilds = 0;
            fresh.Rebuilt += () => rebuilds++;

            var custom = ScriptableObject.CreateInstance<MaterialDefinition>();
            custom.Id = BuiltinMaterials.CustomBase + 1;
            custom.DisplayName = "测试物质";
            custom.Type = MatterType.Liquid;
            custom.Density = 120;
            try
            {
                Assert.IsTrue(fresh.Register(custom));
                Assert.AreEqual(1, rebuilds);
                Assert.AreEqual(MatterType.Liquid, fresh.Table[custom.MaterialId].Type);
                Assert.AreEqual(120, fresh.Table[custom.MaterialId].Density);
                Assert.AreEqual("测试物质", fresh.GetName(custom.MaterialId));

                Assert.IsFalse(fresh.Register(custom), "重复注册应被拒绝");

                Assert.IsTrue(fresh.Unregister(custom));
                Assert.AreEqual(MatterType.Empty, fresh.Table[custom.MaterialId].Type,
                    "注销后该 Id 应回落为 Empty");
            }
            finally
            {
                Object.DestroyImmediate(custom);
                Object.DestroyImmediate(fresh);
            }
        }
    }
}
