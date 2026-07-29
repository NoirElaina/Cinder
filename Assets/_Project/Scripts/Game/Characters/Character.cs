using System;
using Cinder.Core.Attributes;
using Cinder.Core.StateMachine;

namespace Cinder.Game.Characters
{
    /// <summary>
    /// 角色运行时：属性集（生命/速度/跳跃，可被修饰符热改）+ 有限状态机。
    /// 生命归零自动切换 Dead 并触发 Died；死亡后伤害/治疗均无效。
    /// </summary>
    public sealed class Character
    {
        public Character(CharacterDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Attributes = new AttributeSet();
            Attributes.GetOrAdd(CharacterAttributes.MaxHealth).SetBase(definition.MaxHealth);
            Attributes.GetOrAdd(CharacterAttributes.MoveSpeed).SetBase(definition.MoveSpeed);
            Attributes.GetOrAdd(CharacterAttributes.JumpStrength).SetBase(definition.JumpStrength);
            CurrentHealth = definition.MaxHealth;

            Fsm = new StateMachine<Character>(this);
            Fsm.Register(new IdleState());
            Fsm.Register(new MovingState());
            Fsm.Register(new JumpingState());
            Fsm.Register(new FallingState());
            Fsm.Register(new DeadState());
            Fsm.ChangeTo(CharacterStates.Idle);
        }

        public CharacterDefinition Definition { get; }

        public AttributeSet Attributes { get; }

        public StateMachine<Character> Fsm { get; }

        public float CurrentHealth { get; private set; }

        public bool IsDead => Fsm.IsIn(CharacterStates.Dead);

        public event Action Died;

        public void ApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth = Math.Max(0f, CurrentHealth - amount);
            if (CurrentHealth > 0f) return;
            Fsm.ChangeTo(CharacterStates.Dead);
            Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            float max = Attributes.GetValue(CharacterAttributes.MaxHealth, Definition.MaxHealth);
            CurrentHealth = Math.Min(max, CurrentHealth + amount);
        }
    }

    public static class CharacterAttributes
    {
        public const string MaxHealth = "character.max_health";
        public const string MoveSpeed = "character.move_speed";
        public const string JumpStrength = "character.jump_strength";
    }
}
