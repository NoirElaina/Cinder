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

        /// <summary>重力：属性层 90 世界单位/秒² = 360 细格/秒²。</summary>
        const float Gravity = 360f;

        static void Simulate(PixelBody body, int frames)
        {
            for (int i = 0; i < frames; i++) body.Move(Dt, Gravity);
        }

        /// <summary>每帧重新给水平速度（模拟玩家持续按键）。</summary>
        static void Walk(PixelBody body, float speedX, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                body.Velocity.x = speedX;
                body.Move(Dt, Gravity);
            }
        }

        [Test]
        public void Falls_UntilGround()
        {
            var sampler = new FakeSampler { Rule = (x, y) => y < 0 };
            var body = new PixelBody(sampler, new Vector2(0f, 40f));
            Simulate(body, 180);
            Assert.IsTrue(body.Grounded);
            Assert.GreaterOrEqual(body.Position.y, 0f);
            Assert.Less(body.Position.y, 1f, "应停在地面接触处");
            Assert.AreEqual(0f, body.Velocity.y);
        }

        [Test]
        public void Wall_BlocksHorizontalMovement()
        {
            // 墙从 x>=20 起、无限高，无法踏上
            var sampler = new FakeSampler { Rule = (x, y) => y < 0 || x >= 20 };
            var body = new PixelBody(sampler, new Vector2(0f, 0.1f));
            Walk(body, 30f, 120);
            Assert.Less(body.Position.x + body.HalfWidth, 20.01f, "不应穿墙");
        }

        [Test]
        public void StepUp_ClimbsLedgeWithinStepHeight()
        {
            // 2 细格高台阶（= StepHeightCells），持续行走应自动踏上并越过
            var sampler = new FakeSampler { Rule = (x, y) => y < 0 || (x >= 20 && y < 2) };
            var body = new PixelBody(sampler, new Vector2(0f, 0.1f));
            Walk(body, 30f, 180);
            Assert.Greater(body.Position.x, 30f, "应越过台阶继续前进");
            Assert.GreaterOrEqual(body.Position.y, 1.9f, "应站在台阶顶面");
            Assert.IsTrue(body.Grounded);
        }

        [Test]
        public void StepUp_TooTallLedgeBlocks()
        {
            // 4 细格高台阶（> StepHeightCells=2）应像墙一样挡住
            var sampler = new FakeSampler { Rule = (x, y) => y < 0 || (x >= 20 && y < 4) };
            var body = new PixelBody(sampler, new Vector2(0f, 0.1f));
            Walk(body, 30f, 120);
            Assert.Less(body.Position.x + body.HalfWidth, 20.01f, "不应踏上超高台阶");
            Assert.Less(body.Position.y, 1f, "不应被抬升");
        }

        [Test]
        public void WalkingOffLedge_LosesGround()
        {
            // 只有 x<5 有地面，碰撞盒（半宽 6）整体悬空于 x=12 处
            var sampler = new FakeSampler { Rule = (x, y) => y < 0 && x < 5 };
            var body = new PixelBody(sampler, new Vector2(12f, 0.5f));
            Simulate(body, 30);
            Assert.IsFalse(body.Grounded);
            Assert.Less(body.Position.y, 0.5f, "悬空应下落");
        }

        [Test]
        public void Ceiling_BlocksJump()
        {
            // 天花板 y>=30，起跳 90 细格/秒（最高上升 11.25 格 + 身高 20 > 30）
            var sampler = new FakeSampler { Rule = (x, y) => y < 0 || y >= 30 };
            var body = new PixelBody(sampler, new Vector2(0f, 0.1f));
            body.Velocity.y = 90f;
            Simulate(body, 60);
            Assert.Less(body.Position.y + body.Height, 30.01f, "不应穿过天花板");
        }
    }
}
