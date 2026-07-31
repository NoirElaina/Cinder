using System.Collections.Generic;
using Cinder.Game.Characters;
using Cinder.Game.Effects;
using Cinder.Game.Items;
using Cinder.Game.Physics;
using Cinder.Game.Spells;
using Cinder.Game.Weapons;
using Cinder.Runtime.Combat;
using Cinder.Runtime.World;
using Cinder.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinder.Runtime.Player
{
    /// <summary>
    /// 玩家角色：像素碰撞移动（A/D 走、空格跳）+ 法杖施法（左键朝鼠标方向）。
    /// 碰撞体工作在细格坐标（12x20 格），Transform/属性值用世界单位，
    /// 换算只走 WorldScale。移动驱动 Character 的 FSM（Idle/Moving/Jumping/Falling/Dead）。
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        /// <summary>重力（细格/秒²）：属性层的 90 世界单位/秒²。</summary>
        const float Gravity = 90f * WorldScale.CellsPerUnit;

        WorldStreamer streamer;
        EffectBus effectBus;
        Camera cam;
        PixelBody body;
        SpriteRenderer spriteRenderer;
        readonly List<CastResult> castResults = new List<CastResult>();

        public Character Character { get; private set; }
        public WandInstance Wand { get; private set; }
        public Inventory Inventory { get; private set; }
        public Equipment Equipment { get; private set; }

        /// <summary>效果背包：拾取到的投射物效果，画布节点图的拖拽源。</summary>
        public EffectStash EffectStash { get; private set; }

        ItemDefinition demoRing;
        ItemDefinition demoCore;

        /// <summary>自由视角时置 false：人物不再响应移动/开火，但仍受物理模拟。</summary>
        public bool InputEnabled { get; set; } = true;

        public static PlayerController Spawn(WorldStreamer streamer, EffectBus effectBus,
            Camera cam, Vector2 feetPosition)
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
            player.effectBus = effectBus;
            player.cam = cam;
            player.spriteRenderer = sr;

            player.Character = new Character(definition);
            player.body = new PixelBody(new WindowCellSampler(streamer),
                feetPosition * WorldScale.CellsPerUnitF);
            player.Wand = wand;

            // 演示物品：入包 + 装备系统（属性修饰按 wand. 前缀路由）
            player.Inventory = new Inventory(12);
            player.EffectStash = new EffectStash();
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

        /// <summary>程序化 12x20 细格巫师（长袍 + 尖帽），PPU = 每单位细格数。</summary>
        static Sprite CreatePlayerSprite()
        {
            const int w = 12, h = 20;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };
            var clear = new Color32(0, 0, 0, 0);
            var robe = new Color32(88, 70, 150, 255);
            var robeDark = new Color32(60, 46, 108, 255);
            var belt = new Color32(168, 124, 56, 255);
            var skin = new Color32(232, 190, 152, 255);
            var hat = new Color32(70, 54, 128, 255);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, clear);

            // 长袍：下宽上窄，两侧描暗色边
            for (int y = 0; y < 12; y++)
            {
                int half = y < 6 ? 4 : 3;
                for (int x = 6 - half; x <= 5 + half; x++)
                    tex.SetPixel(x, y, x == 6 - half || x == 5 + half ? robeDark : robe);
            }
            for (int x = 3; x <= 8; x++) tex.SetPixel(x, 6, belt);

            // 头部
            for (int y = 12; y < 16; y++)
                for (int x = 4; x <= 7; x++)
                    tex.SetPixel(x, y, skin);

            // 尖帽：帽檐 + 锥体
            for (int x = 2; x <= 9; x++) tex.SetPixel(x, 16, hat);
            for (int x = 4; x <= 7; x++) tex.SetPixel(x, 17, hat);
            for (int x = 5; x <= 6; x++) tex.SetPixel(x, 18, hat);
            tex.SetPixel(5, 19, hat);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h),
                new Vector2(0.5f, 0f), WorldScale.CellsPerUnitF);
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
                SyncTransform();
            }
            UpdateFsm();
        }

        /// <summary>碰撞体（细格）-> Transform（世界单位）。</summary>
        void SyncTransform()
        {
            transform.position = (Vector2)(body.Position * WorldScale.UnitsPerCell);
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

            // 属性值是世界单位/秒，碰撞体速度是细格/秒
            body.Velocity.x = moveX * WorldScale.CellsPerUnitF
                * Character.Attributes.GetValue(CharacterAttributes.MoveSpeed, 22f);
            if (jump && body.Grounded)
                body.Velocity.y = WorldScale.CellsPerUnitF
                    * Character.Attributes.GetValue(CharacterAttributes.JumpStrength, 38f);

            body.Move(dt, Gravity);
            SyncTransform();

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
            Vector2 muzzle = body.Center * WorldScale.UnitsPerCell;
            Vector2 direction = aim - muzzle;
            if (direction.sqrMagnitude < 0.01f) return;

            if (Wand.TryCast(castResults))
            {
                Vector2 baseDirection = direction.normalized;
                foreach (CastResult result in castResults)
                {
                    Vector2 dir = result.AngleOffset != 0f
                        ? Quaternion.Euler(0f, 0f, result.AngleOffset) * baseDirection
                        : baseDirection;
                    Projectile.Spawn(streamer, effectBus, result.Behavior, result.Spec, muzzle, dir);
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
            // 速度阈值是细格/秒
            string next = body.Grounded
                ? (Mathf.Abs(body.Velocity.x) > 4f ? CharacterStates.Moving : CharacterStates.Idle)
                : (body.Velocity.y > 0f ? CharacterStates.Jumping : CharacterStates.Falling);
            Character.Fsm.ChangeTo(next);
        }
    }
}
