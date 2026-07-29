using System.Collections.Generic;
using Cinder.Game.Characters;
using Cinder.Game.Items;
using Cinder.Game.Physics;
using Cinder.Game.Spells;
using Cinder.Runtime.Combat;
using Cinder.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinder.Runtime.Player
{
    /// <summary>
    /// 玩家角色：像素碰撞移动（A/D 走、空格跳）+ 法杖施法（左键朝鼠标方向）。
    /// 移动驱动 Character 的 FSM（Idle/Moving/Jumping/Falling/Dead）。
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        const float Gravity = 90f;

        WorldStreamer streamer;
        Camera cam;
        PixelBody body;
        SpriteRenderer spriteRenderer;
        readonly List<CastResult> castResults = new List<CastResult>();

        public Character Character { get; private set; }
        public WandInstance Wand { get; private set; }
        public Inventory Inventory { get; private set; }
        public Equipment Equipment { get; private set; }

        ItemDefinition demoRing;
        ItemDefinition demoCore;

        /// <summary>自由视角时置 false：人物不再响应移动/开火，但仍受物理模拟。</summary>
        public bool InputEnabled { get; set; } = true;

        public static PlayerController Spawn(WorldStreamer streamer, Camera cam, Vector2 feetPosition)
        {
            var definition = GameContent.LoadPlayerCharacter();
            WandInstance wand = GameContent.LoadStarterWand();
            if (definition == null || wand == null)
            {
                Debug.LogError("[Cinder] 玩家内容资产缺失，跳过玩家生成（详见上方错误）。");
                return null;
            }

            var go = new GameObject("Player");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreatePlayerSprite();
            sr.sortingOrder = 10;
            go.transform.position = feetPosition;

            var player = go.AddComponent<PlayerController>();
            player.streamer = streamer;
            player.cam = cam;
            player.spriteRenderer = sr;

            player.Character = new Character(definition);
            player.body = new PixelBody(new WindowCellSampler(streamer), feetPosition);
            player.Wand = wand;

            // 演示物品：入包 + 装备系统（属性修饰按 wand. 前缀路由）
            player.Inventory = new Inventory(12);
            ItemDefinition[] starterItems = GameContent.LoadStarterItems();
            player.demoRing = starterItems.Length > 0 ? starterItems[0] : null;
            player.demoCore = starterItems.Length > 1 ? starterItems[1] : null;
            foreach (ItemDefinition item in starterItems) player.Inventory.Add(item);
            player.Equipment = new Equipment(attribute =>
                attribute != null && attribute.StartsWith("wand.")
                    ? player.Wand.Attributes
                    : player.Character.Attributes);
            return player;
        }

        static Sprite CreatePlayerSprite()
        {
            var tex = new Texture2D(2, 3, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };
            var robe = new Color32(190, 80, 40, 255);
            var robeDark = new Color32(150, 60, 30, 255);
            var skin = new Color32(235, 195, 155, 255);
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 2; x++)
                    tex.SetPixel(x, y, y == 2 ? skin : (x == 0 ? robe : robeDark));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 3), new Vector2(0.5f, 0f), 1f);
        }

        void Update()
        {
            if (Character.IsDead) return;
            float dt = Time.deltaTime;

            Wand.Tick(dt);
            if (InputEnabled)
            {
                HandleMovement(dt);
                HandleFire();
                HandleItems();
            }
            else
            {
                body.Velocity.x = 0f;
                body.Move(dt, Gravity);
                transform.position = body.Position;
            }
            UpdateFsm();
        }

        void HandleMovement(float dt)
        {
            Keyboard kb = Keyboard.current;
            float moveX = 0f;
            bool jump = false;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveX -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveX += 1f;
                jump = kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            }

            body.Velocity.x = moveX * Character.Attributes.GetValue(CharacterAttributes.MoveSpeed, 22f);
            if (jump && body.Grounded)
                body.Velocity.y = Character.Attributes.GetValue(CharacterAttributes.JumpStrength, 38f);

            body.Move(dt, Gravity);
            transform.position = body.Position;

            if (moveX != 0f)
                spriteRenderer.flipX = moveX < 0f;
        }

        void HandleFire()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || cam == null || !mouse.leftButton.isPressed) return;

            Vector3 screen = mouse.position.ReadValue();
            screen.z = -cam.transform.position.z;
            Vector2 aim = cam.ScreenToWorldPoint(screen);
            Vector2 direction = aim - body.Center;
            if (direction.sqrMagnitude < 0.01f) return;

            if (Wand.TryCast(castResults))
            {
                Vector2 baseDirection = direction.normalized;
                foreach (CastResult result in castResults)
                {
                    Vector2 dir = result.AngleOffset != 0f
                        ? Quaternion.Euler(0f, 0f, result.AngleOffset) * baseDirection
                        : baseDirection;
                    Projectile.Spawn(streamer, result.Behavior, result.Spec, body.Center, dir);
                }
            }
        }

        void HandleItems()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.gKey.wasPressedThisFrame) ToggleEquip(demoRing);
            if (kb.hKey.wasPressedThisFrame) ToggleEquip(demoCore);
        }

        void ToggleEquip(ItemDefinition item)
        {
            if (item == null) return;
            if (Equipment.Get(item.EquipSlot) != null)
                Equipment.Unequip(item.EquipSlot);
            else
                Equipment.Equip(item);
        }

        void UpdateFsm()
        {
            string next = body.Grounded
                ? (Mathf.Abs(body.Velocity.x) > 1f ? CharacterStates.Moving : CharacterStates.Idle)
                : (body.Velocity.y > 0f ? CharacterStates.Jumping : CharacterStates.Falling);
            Character.Fsm.ChangeTo(next);
        }
    }
}
