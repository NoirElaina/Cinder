using Cinder.Simulation;

namespace Cinder.Game.Effects
{
    /// <summary>挖掘处理器：球形清空地形，密度 255 的基岩级物质免疫。</summary>
    public sealed class DigHandler : IEffectHandler
    {
        public EffectKind Kind => EffectKind.Dig;

        public void Handle(in EffectRequest request, IEffectWorld world)
        {
            EffectUtil.ForEachInSphere(request.CellX, request.CellY, request.Radius, world,
                (x, y, _) =>
                {
                    ushort m = world.GetMaterial(x, y);
                    if (m == BuiltinMaterials.Empty || world.IsIndestructible(m)) return;
                    world.SetMaterial(x, y, BuiltinMaterials.Empty);
                });
        }
    }

    /// <summary>
    /// 爆炸处理器：外环挖除、心区（半径一半内）点燃可燃物、
    /// 整圆按衰减加热——热量交给温度通道继续引发相变
    /// （水被爆风煮成蒸汽、外围木头随后自燃）。
    /// </summary>
    public sealed class ExplosionHandler : IEffectHandler
    {
        /// <summary>爆炸中心施加的温升（K），按距离线性衰减。</summary>
        public int HeatKelvin = 800;

        public EffectKind Kind => EffectKind.Explosion;

        public void Handle(in EffectRequest request, IEffectWorld world)
        {
            int radius = request.Radius; // in 参数不能进闭包，先拷局部
            int heat = HeatKelvin;
            float inner = radius * 0.5f;
            EffectUtil.ForEachInSphere(request.CellX, request.CellY, radius, world,
                (x, y, falloff) =>
                {
                    if (heat > 0)
                        world.AddHeat(x, y, (int)(heat * falloff));

                    ushort m = world.GetMaterial(x, y);
                    if (m == BuiltinMaterials.Empty || world.IsIndestructible(m)) return;

                    float dist = (1f - falloff) * radius;
                    MaterialProps props = world.PropsOf(m);
                    if (dist <= inner && props.Flammability > 0 && props.BurnsInto != 0)
                        world.SetMaterial(x, y, props.BurnsInto); // 心区点燃
                    else
                        world.SetMaterial(x, y, BuiltinMaterials.Empty); // 爆风挖除
                });
        }
    }

    /// <summary>加热处理器：球形升温（中心 Amount K，线性衰减）。</summary>
    public sealed class HeatHandler : IEffectHandler
    {
        public EffectKind Kind => EffectKind.Heat;

        public void Handle(in EffectRequest request, IEffectWorld world)
        {
            int amount = request.Amount;
            EffectUtil.ForEachInSphere(request.CellX, request.CellY, request.Radius, world,
                (x, y, falloff) => world.AddHeat(x, y, (int)(amount * falloff)));
        }
    }

    /// <summary>冰冻处理器：球形降温（中心 Amount K，线性衰减）。</summary>
    public sealed class FreezeHandler : IEffectHandler
    {
        public EffectKind Kind => EffectKind.Freeze;

        public void Handle(in EffectRequest request, IEffectWorld world)
        {
            int amount = request.Amount;
            EffectUtil.ForEachInSphere(request.CellX, request.CellY, request.Radius, world,
                (x, y, falloff) => world.AddHeat(x, y, -(int)(amount * falloff)));
        }
    }

    /// <summary>点燃处理器：球形内可燃物直接变为燃烧产物。</summary>
    public sealed class IgniteHandler : IEffectHandler
    {
        public EffectKind Kind => EffectKind.Ignite;

        public void Handle(in EffectRequest request, IEffectWorld world)
        {
            EffectUtil.ForEachInSphere(request.CellX, request.CellY, request.Radius, world,
                (x, y, _) =>
                {
                    ushort m = world.GetMaterial(x, y);
                    if (m == BuiltinMaterials.Empty) return;
                    MaterialProps props = world.PropsOf(m);
                    if (props.Flammability > 0 && props.BurnsInto != 0)
                        world.SetMaterial(x, y, props.BurnsInto);
                });
        }
    }

    static class EffectUtil
    {
        /// <summary>遍历圆内格子，falloff = 1（圆心）到 0（边缘）。</summary>
        public delegate void CellAction(int x, int y, float falloff);

        public static void ForEachInSphere(int centerX, int centerY, int radius,
            IEffectWorld world, CellAction action)
        {
            if (radius <= 0)
            {
                if (world.ContainsCell(centerX, centerY)) action(centerX, centerY, 1f);
                return;
            }
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int d2 = dx * dx + dy * dy;
                    if (d2 > r2) continue;
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (!world.ContainsCell(x, y)) continue;
                    float falloff = 1f - (float)System.Math.Sqrt(d2) / radius;
                    action(x, y, falloff);
                }
            }
        }
    }
}
