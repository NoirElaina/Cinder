using Cinder.Game.Effects;
using Cinder.Runtime.World;
using UnityEngine;

namespace Cinder.Runtime.Combat
{
    /// <summary>
    /// 投射物：按 ProjectileSpec 飞行（速度/重力/寿命），沿路径采样世界格，
    /// 命中固体时走行为装饰链的 OnHitWorld，效果（挖掘/爆炸/点燃…）
    /// 统一入队效果总线，由处理器在 tick 间隙执行。支持拖尾物质。
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        static Sprite sharedSprite;

        WorldStreamer streamer;
        EffectBus effectBus;
        IProjectileBehavior behavior;
        ProjectileSpec spec;
        Vector2 velocity;
        float lifeLeft;

        public static Projectile Spawn(WorldStreamer streamer, EffectBus effectBus,
            IProjectileBehavior behavior, in ProjectileSpec spec, Vector2 origin, Vector2 direction)
        {
            EnsureSprite();
            var go = new GameObject("Projectile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sharedSprite;
            sr.color = spec.Tint;
            sr.sortingOrder = 5;
            go.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            go.transform.position = origin;

            var p = go.AddComponent<Projectile>();
            p.streamer = streamer;
            p.effectBus = effectBus;
            p.behavior = behavior ?? BaseProjectileBehavior.Instance;
            p.spec = spec;
            p.velocity = direction.normalized * spec.Speed;
            p.lifeLeft = spec.Lifetime;
            return p;
        }

        static void EnsureSprite()
        {
            if (sharedSprite != null) return;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sharedSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            velocity.y -= spec.Gravity * dt;

            Vector2 from = transform.position;
            Vector2 step = velocity * dt;
            float distance = step.magnitude;
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance / 0.5f));
            for (int i = 1; i <= samples; i++)
            {
                Vector2 p = from + step * (i / (float)samples);
                int cx = Mathf.FloorToInt(p.x);
                int cy = Mathf.FloorToInt(p.y);
                if (streamer.IsSolidCell(cx, cy))
                {
                    OnHit(cx, cy);
                    return;
                }
            }
            transform.position = from + step;

            if (spec.TrailMaterial != 0)
            {
                streamer.EditSphere(
                    Mathf.FloorToInt(transform.position.x),
                    Mathf.FloorToInt(transform.position.y),
                    0, spec.TrailMaterial);
            }

            lifeLeft -= dt;
            if (lifeLeft <= 0f) Destroy(gameObject);
        }

        void OnHit(int cellX, int cellY)
        {
            var hit = new ProjectileHit(cellX, cellY,
                streamer.GetMaterialAt(cellX, cellY), effectBus);
            behavior.OnHitWorld(ref spec, hit);
            if (spec.DigPower > 0)
                hit.Emit(EffectRequest.Dig(cellX, cellY, spec.DigPower));

            // 触发弹：在命中点向下释放载荷法术（清空 TriggerPayload，不递归）
            if (spec.TriggerPayload != null)
            {
                ProjectileSpec payload = spec.TriggerPayload.BaseSpec;
                payload.TriggerPayload = null;
                Spawn(streamer, effectBus, BaseProjectileBehavior.Instance, payload,
                    new Vector2(cellX + 0.5f, cellY + 0.5f), Vector2.down);
            }
            Destroy(gameObject);
        }
    }
}
