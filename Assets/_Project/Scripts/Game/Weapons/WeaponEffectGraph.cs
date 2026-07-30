using System;
using System.Collections.Generic;
using Cinder.Game.Effects;
using UnityEngine;

namespace Cinder.Game.Weapons
{
    /// <summary>
    /// 武器效果装配图（蓝图）：核心节点(=当前武器) + 效果节点 + 连线。
    /// 把效果节点连到核心即让武器拥有该效果。连线 topology 是未来
    /// "连线情况不同效果不同"的扩展点，全部集中在 <see cref="Compile"/> 一处实现。
    /// </summary>
    public sealed class WeaponEffectGraph
    {
        public enum NodeType { Core, Effect }

        public sealed class Node
        {
            public int Id;
            public NodeType Type;
            public ProjectileEffectDefinition Effect; // 仅 Effect 节点有
            public Vector2 CanvasPos;                 // 画布坐标（像素，由 UI 维护）
        }

        public readonly struct Edge
        {
            public readonly int From;
            public readonly int To;
            public Edge(int from, int to) { From = from; To = to; }
        }

        readonly List<Node> nodes = new List<Node>();
        readonly List<Edge> edges = new List<Edge>();
        int nextId = 1;

        /// <summary>图（节点/边）发生变化时触发，供 UI 重绘并同步到武器。</summary>
        public event Action Changed;

        public IReadOnlyList<Node> Nodes => nodes;
        public IReadOnlyList<Edge> Edges => edges;
        public Node Core { get; private set; }

        /// <summary>初始化核心节点（唯一）。center 为画布坐标。</summary>
        public Node EnsureCore(Vector2 center)
        {
            if (Core != null) return Core;
            Core = new Node { Id = 0, Type = NodeType.Core, CanvasPos = center };
            nodes.Add(Core);
            Changed?.Invoke();
            return Core;
        }

        /// <summary>在画布位置放一个效果节点，返回节点。</summary>
        public Node AddEffectNode(ProjectileEffectDefinition effect, Vector2 pos)
        {
            if (effect == null) return null;
            var node = new Node { Id = nextId++, Type = NodeType.Effect, Effect = effect, CanvasPos = pos };
            nodes.Add(node);
            Changed?.Invoke();
            return node;
        }

        /// <summary>删除一个效果节点（连带其全部边）。核心不可删。</summary>
        public bool RemoveNode(int id)
        {
            Node node = Get(id);
            if (node == null || node.Type == NodeType.Core) return false;
            nodes.Remove(node);
            edges.RemoveAll(e => e.From == id || e.To == id);
            Changed?.Invoke();
            return true;
        }

        /// <summary>连线 from→to（防重复、防自环、节点须存在）。</summary>
        public bool Connect(int fromId, int toId)
        {
            if (fromId == toId) return false;
            Node from = Get(fromId), to = Get(toId);
            if (from == null || to == null) return false;
            if (edges.Exists(e => e.From == fromId && e.To == toId)) return false;
            edges.Add(new Edge(fromId, toId));
            Changed?.Invoke();
            return true;
        }

        /// <summary>断开 from→to 这条线。</summary>
        public bool Disconnect(int fromId, int toId)
        {
            int removed = edges.RemoveAll(e => e.From == fromId && e.To == toId);
            if (removed == 0) return false;
            Changed?.Invoke();
            return true;
        }

        public Node Get(int id) => nodes.Find(n => n.Id == id);

        /// <summary>
        /// 把图拓扑编译成"武器应拥有的效果列表"。
        /// 当前规则：直接连到核心的效果节点 → 拥有该效果（按资产去重，避免叠加）。
        /// 未来"连线情况不同效果不同"（串联组合 / 顺序 / 条件）只改本函数即可。
        /// </summary>
        public List<ProjectileEffectDefinition> Compile()
        {
            var result = new List<ProjectileEffectDefinition>();
            if (Core == null) return result;
            foreach (Edge e in edges)
            {
                // 无向：只要一条边把核心和效果节点连起来，无论从哪头拉线都生效
                Node other = null;
                if (e.To == Core.Id) other = Get(e.From);
                else if (e.From == Core.Id) other = Get(e.To);
                if (other == null || other.Type != NodeType.Effect || other.Effect == null) continue;
                if (!result.Contains(other.Effect)) result.Add(other.Effect);
            }
            return result;
        }
    }
}
