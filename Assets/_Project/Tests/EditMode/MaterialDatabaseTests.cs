using Cinder.Runtime.Materials;
using Cinder.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Cinder.Tests
{
    public class MaterialDatabaseTests
    {
        MaterialDatabase db;

        [SetUp]
        public void SetUp() => db = MaterialDatabase.CreateDefault();

        [TearDown]
        public void TearDown()
        {
            if (db != null) Object.DestroyImmediate(db);
        }

        [Test]
        public void CreateDefault_BakesBuiltinProps()
        {
            Assert.AreEqual(MatterType.Liquid, db.Table[BuiltinMaterials.Water].Type);
            Assert.AreEqual(MatterType.Powder, db.Table[BuiltinMaterials.Sand].Type);
            Assert.Greater(db.Table[BuiltinMaterials.Wood].Flammability, 0);
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
            int rebuilds = 0;
            db.Rebuilt += () => rebuilds++;

            var acid = ScriptableObject.CreateInstance<MaterialDefinition>();
            acid.Id = BuiltinMaterials.CustomBase + 1;
            acid.DisplayName = "酸液";
            acid.Type = MatterType.Liquid;
            acid.Density = 120;
            try
            {
                Assert.IsTrue(db.Register(acid));
                Assert.AreEqual(1, rebuilds);
                Assert.AreEqual(MatterType.Liquid, db.Table[acid.MaterialId].Type);
                Assert.AreEqual(120, db.Table[acid.MaterialId].Density);
                Assert.AreEqual("酸液", db.GetName(acid.MaterialId));

                Assert.IsFalse(db.Register(acid), "重复注册应被拒绝");

                Assert.IsTrue(db.Unregister(acid));
                Assert.AreEqual(MatterType.Empty, db.Table[acid.MaterialId].Type,
                    "注销后该 Id 应回落为 Empty");
            }
            finally
            {
                Object.DestroyImmediate(acid);
            }
        }
    }
}
