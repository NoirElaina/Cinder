using Cinder.Core.StateMachine;

namespace Cinder.Game.Characters
{
    /// <summary>角色状态名常量。</summary>
    public static class CharacterStates
    {
        public const string Idle = "Idle";
        public const string Moving = "Moving";
        public const string Jumping = "Jumping";
        public const string Falling = "Falling";
        public const string Dead = "Dead";
    }

    /// <summary>
    /// 角色状态基类。本阶段状态体保持轻量（移动/碰撞集成在玩法层），
    /// 子类按需覆盖钩子。
    /// </summary>
    public abstract class CharacterState : IState<Character>
    {
        public abstract string Name { get; }

        public virtual void Enter(Character context) { }

        public virtual void Exit(Character context) { }

        public virtual void Tick(Character context, float deltaTime) { }
    }

    public sealed class IdleState : CharacterState
    {
        public override string Name => CharacterStates.Idle;
    }

    public sealed class MovingState : CharacterState
    {
        public override string Name => CharacterStates.Moving;
    }

    public sealed class JumpingState : CharacterState
    {
        public override string Name => CharacterStates.Jumping;
    }

    public sealed class FallingState : CharacterState
    {
        public override string Name => CharacterStates.Falling;
    }

    public sealed class DeadState : CharacterState
    {
        public override string Name => CharacterStates.Dead;
    }
}
