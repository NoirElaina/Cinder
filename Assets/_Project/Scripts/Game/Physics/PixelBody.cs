using Cinder.Simulation;
using UnityEngine;

namespace Cinder.Game.Physics
{
    /// <summary>
    /// 像素碰撞体：AABB 对细物理格逐轴解算，贴合可破坏地形。
    /// Position 为脚底中心（细格坐标，浮点）；速度单位是细格/秒。
    /// 水平移动被 1..StepHeight 格的台阶挡住时自动踏上去（Noita 式爬坡）。
    /// 纯逻辑，可单测。
    /// </summary>
    public sealed class PixelBody
    {
        /// <summary>碰撞盒半宽（细格）。</summary>
        public float HalfWidth = WorldScale.PlayerWidthCells * 0.5f;

        /// <summary>碰撞盒高度（细格）。</summary>
        public float Height = WorldScale.PlayerHeightCells;

        /// <summary>可自动踏上的台阶高度（细格）。</summary>
        public int StepHeight = WorldScale.StepHeightCells;

        /// <summary>单步最大位移（细格），防止高速穿透。</summary>
        public float MaxStep = 0.9f;

        /// <summary>终端下落速度（细格/秒）。</summary>
        public float TerminalFallSpeed = 320f;

        public Vector2 Position;
        public Vector2 Velocity;

        public bool Grounded { get; private set; }

        readonly ICellSampler sampler;

        public PixelBody(ICellSampler sampler, Vector2 start)
        {
            this.sampler = sampler;
            Position = start;
        }

        public Vector2 Center => Position + new Vector2(0f, Height * 0.5f);

        public void Move(float deltaTime, float gravity)
        {
            Velocity.y = Mathf.Max(Velocity.y - gravity * deltaTime, -TerminalFallSpeed);
            bool wasGrounded = Grounded;
            Grounded = false;
            MoveAxis(Velocity.x * deltaTime, xAxis: true, allowStepUp: wasGrounded);
            MoveAxis(Velocity.y * deltaTime, xAxis: false, allowStepUp: false);
        }

        void MoveAxis(float delta, bool xAxis, bool allowStepUp)
        {
            float remaining = delta;
            while (Mathf.Abs(remaining) > 1e-6f)
            {
                float step = Mathf.Clamp(remaining, -MaxStep, MaxStep);
                if (xAxis) Position.x += step;
                else Position.y += step;

                if (OverlapsSolid())
                {
                    if (xAxis)
                    {
                        if (allowStepUp && TryStepUp())
                        {
                            remaining -= step;
                            continue;
                        }
                        Position.x -= step;
                        Velocity.x = 0f;
                    }
                    else
                    {
                        Position.y -= step;
                        if (step < 0f) Grounded = true;
                        Velocity.y = 0f;
                    }
                    return;
                }
                remaining -= step;
            }
        }

        /// <summary>水平被挡时尝试抬升 1..StepHeight 格：能站上去就算跨过台阶。</summary>
        bool TryStepUp()
        {
            float savedY = Position.y;
            for (int lift = 1; lift <= StepHeight; lift++)
            {
                Position.y = savedY + lift;
                if (!OverlapsSolid()) return true;
            }
            Position.y = savedY;
            return false;
        }

        bool OverlapsSolid()
        {
            float x0 = Position.x - HalfWidth;
            float x1 = Position.x + HalfWidth;
            float y0 = Position.y;
            float y1 = Position.y + Height;
            for (int x = Mathf.FloorToInt(x0); x < x1; x++)
                for (int y = Mathf.FloorToInt(y0); y < y1; y++)
                    if (sampler.IsSolid(x, y)) return true;
            return false;
        }
    }
}
