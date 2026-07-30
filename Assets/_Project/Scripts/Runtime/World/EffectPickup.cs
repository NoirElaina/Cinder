using Cinder.Game.Effects;
using Cinder.Runtime.Player;
using UnityEngine;

namespace Cinder.Runtime.World
{
    /// <summary>
    /// 世界效果拾取物：持有一个 ProjectileEffectDefinition，玩家靠近自动拾取进效果背包。
    /// 视觉是运行时生成的小圆形图标，无需 prefab。
    /// </summary>
    public sealed class EffectPickup : MonoBehaviour
    {
        /// <summary>拾取半径（格）。</summary>
        public float Radius = 2f;

        ProjectileEffectDefinition effect;

        public static EffectPickup Spawn(ProjectileEffectDefinition effect, Vector2 position, Color color)
        {
            if (effect == null) return null;
            var go = new GameObject($"Pickup_{effect.DisplayName}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSprite(color);
            sr.sortingOrder = 8;
            go.transform.position = new Vector3(position.x, position.y, 0f);
            var pickup = go.AddComponent<EffectPickup>();
            pickup.effect = effect;
            return pickup;
        }

        void Update()
        {
            PlayerController player = WorldController.Instance != null
                ? WorldController.Instance.Player : null;
            if (player == null || player.EffectStash == null || effect == null) return;
            Vector2 dp = (Vector2)player.transform.position - (Vector2)transform.position;
            if (dp.sqrMagnitude > Radius * Radius) return;
            if (player.EffectStash.Add(effect)) Destroy(gameObject);
        }

        static Sprite CreateSprite(Color color)
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            int c = size / 2, r = size / 2 - 1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = x - c, dy = y - c;
                    tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? color : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
