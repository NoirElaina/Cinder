using System;
using Cinder.Game.Physics;
using NUnit.Framework;
using UnityEngine;

namespace Cinder.Tests
{
    public class PixelBodyTests
    {
        sealed class FakeSampler : ICellSampler
        {
            public Func<int, int, bool> Rule;
            public bool IsSolid(int cellX, int cellY) => Rule(cellX, cellY);
        }

        const float Dt = 1f / 60f;
        const float Gravity = 90f;

        static void Simulate(PixelBody body, int frames)
        {
            for (int i = 0; i < frames; i++) body.Move(Dt, Gravity);
        }

        [Test]
        public void Falls_UntilGround()
        {
            var sampler = new FakeSampler { Rule = (x, y) => y < 0 };
            var body = new PixelBody(sampler, new Vector2(0f, 10f));
            Simulate(body, 180);
            Assert.IsTrue(body.Grounded);
            Assert.Less(body.Position.y, 0.5f, "应停在地面接触处");
            Assert.AreEqual(0f, body.Velocity.y);
        }

        [Test]
        public void Wall_BlocksHorizontalMovement()
        {
            var sampler = new FakeSampler { Rule = (x, y) => y < 0 || x >= 5 };
            var body = new PixelBody(sampler, new Vector2(0f, 0.1f));
            body.Velocity.x = 10f;
            Simulate(body, 120);
            Assert.Less(body.Position.x, 5f, "不应穿墙");
            Assert.AreEqual(0f, body.Velocity.x);
        }

        [Test]
        public void WalkingOffLedge_LosesGround()
        {
            // 只有 x<5 的悬崖下方有地面
            var sampler = new FakeSampler { Rule = (x, y) => y < 0 && x < 5 };
            var body = new PixelBody(sampler, new Vector2(7f, 0.5f));
            Simulate(body, 30);
            Assert.IsFalse(body.Grounded);
            Assert.Less(body.Position.y, 0.5f, "走出悬崖应下落");
        }

        [Test]
        public void Ceiling_BlocksJump()
        {
            var sampler = new FakeSampler { Rule = (x, y) => y < 0 || y >= 4 };
            var body = new PixelBody(sampler, new Vector2(0f, 0.1f));
            body.Velocity.y = 40f;
            Simulate(body, 60);
            Assert.Less(body.Position.y + body.Height, 4.01f, "不应穿过天花板");
        }
    }
}
