using UnityEngine;

namespace Cinder.Game.Physics
{
    /// <summary>
    /// 像素碰撞体：AABB 对世界格逐轴解算，贴合可破坏地形。
    /// Position 为脚底中心（格坐标，浮点）。纯逻辑，可单测。
    /// </summary>
    public sealed class PixelBody
    {
        /// <summary>碰撞盒半宽（格）。</summary>
        public float HalfWidth = 0.8f;

        /// <summary>碰撞盒高度（格）。</summary>
        public float Height = 2.8f;

        /// <summary>单步最大位移，防止高速穿透。</summary>
        public float MaxStep = 0.4f;

        public float TerminalFallSpeed = 80f;

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
            Grounded = false;
            MoveAxis(Velocity.x * deltaTime, xAxis: true);
            MoveAxis(Velocity.y * deltaTime, xAxis: false);
        }

        void MoveAxis(float delta, bool xAxis)
        {
            float remaining = delta;
            while (Mathf.Abs(remaining) > 1e-6f)
            {
                float step = Mathf.Clamp(remaining, -MaxStep, MaxStep);
                if (xAxis) Position.x += step;
                else Position.y += step;

                if (OverlapsSolid())
                {
                    if (xAxis) { Position.x -= step; Velocity.x = 0f; }
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
