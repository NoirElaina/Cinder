using System;
using System.Collections.Generic;

namespace Cinder.Core.StateMachine
{
    /// <summary>有限状态机状态。实现类应无状态或仅持会话内数据。</summary>
    public interface IState<TContext>
    {
        string Name { get; }
        void Enter(TContext context);
        void Exit(TContext context);
        void Tick(TContext context, float deltaTime);
    }

    /// <summary>
    /// 有限状态机：状态显式注册、按名切换，Enter/Exit 严格配对。
    /// 重复切换到当前状态为空操作。切换失败（未注册）返回 false 且不改动现状。
    /// </summary>
    public sealed class StateMachine<TContext>
    {
        readonly TContext context;
        readonly Dictionary<string, IState<TContext>> states =
            new Dictionary<string, IState<TContext>>();

        public StateMachine(TContext context)
        {
            this.context = context;
        }

        public IState<TContext> Current { get; private set; }

        /// <summary>参数为 (旧状态, 新状态)，初始进入时旧状态为 null。</summary>
        public event Action<IState<TContext>, IState<TContext>> StateChanged;

        public bool IsIn(string name) => Current != null && Current.Name == name;

        public void Register(IState<TContext> state)
        {
            if (state == null || string.IsNullOrEmpty(state.Name))
                throw new ArgumentException("状态必须有名字");
            states[state.Name] = state;
        }

        public bool ChangeTo(string name)
        {
            if (!states.TryGetValue(name, out IState<TContext> next)) return false;
            if (ReferenceEquals(Current, next)) return true;

            IState<TContext> previous = Current;
            previous?.Exit(context);
            Current = next;
            next.Enter(context);
            StateChanged?.Invoke(previous, next);
            return true;
        }

        public void Tick(float deltaTime) => Current?.Tick(context, deltaTime);
    }
}
