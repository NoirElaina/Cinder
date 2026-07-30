using System.Collections.Generic;
using Cinder.Game.Effects;
using Cinder.Game.Weapons;
using Cinder.Runtime.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinder.Runtime.UI
{
    /// <summary>
    /// 武器效果装配画布（IMGUI 节点图）：C 键开关。
    /// 中心是正六边形"核心石"（= 当前武器法杖）；从左侧效果背包把效果拖成节点，
    /// 拖线连到核心即让武器拥有该效果（图变化即 Compile 同步到 Wand.AttachedEffects）。
    /// 右键效果节点：删除并还回背包；右键连线：断开。
    /// 连线拓扑如何影响效果是 WeaponEffectGraph.Compile 的扩展点。
    /// </summary>
    public sealed class WeaponCanvasController : MonoBehaviour
    {
        const float NodeSize = 96f;
        const float NodeRadius = NodeSize * 0.5f;
        const float PortRadius = 10f;
        const float StashWidth = 190f;
        const float StashRowH = 30f;
        const float EdgePickDist = 7f;

        /// <summary>画布是否开启（WorldController 据此暂停世界编辑输入）。</summary>
        public static bool IsOpen { get; private set; }

        PlayerController player;
        WeaponEffectGraph graph;
        Texture2D hexTex;

        // 拖拽状态
        ProjectileEffectDefinition draggingEffect;
        int movingNodeId = -1;
        int linkingFromId = -1;

        GUIStyle labelStyle;
        GUIStyle titleStyle;
        Rect canvasRect;
        Rect stashRect;

        /// <summary>绑定玩家（WorldController 生成玩家后调用）。</summary>
        public void Bind(PlayerController target)
        {
            player = target;
            graph = new WeaponEffectGraph();
            graph.Changed += SyncToWeapon;
            SyncToWeapon();
        }

        void SyncToWeapon()
        {
            if (player == null || player.Wand == null || graph == null) return;
            player.Wand.SetAttachedEffects(graph.Compile());
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || player == null) return;
            if (kb.cKey.wasPressedThisFrame) Toggle();
        }

        void Toggle()
        {
            IsOpen = !IsOpen;
            if (IsOpen)
            {
                canvasRect = new Rect(Screen.width * 0.08f, Screen.height * 0.08f,
                    Screen.width * 0.84f, Screen.height * 0.84f);
                graph.EnsureCore(canvasRect.center);
                player.InputEnabled = false;
            }
            else
            {
                player.InputEnabled = true;
                draggingEffect = null;
                movingNodeId = -1;
                linkingFromId = -1;
            }
        }

        void OnGUI()
        {
            if (!IsOpen || player == null || graph == null) return;
            if (hexTex == null) hexTex = CreateHexTexture(128);
            EnsureStyles();

            Vector2 m = Event.current.mousePosition;

            // 背景
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(canvasRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(canvasRect.x + 12, canvasRect.y + 8, 400, 24),
                "武器装配画布（C 关闭）：从背包拖效果到画布 → 连到核心即生效", titleStyle);

            DrawStashPanel();
            DrawEdges(m);
            DrawNodes();
            DrawGhost(m);

            HandleEvents(m);
        }

        // ---------- 绘制 ----------

        void DrawStashPanel()
        {
            stashRect = new Rect(canvasRect.x + 14, canvasRect.y + 40, StashWidth, canvasRect.height - 56);
            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            GUI.DrawTexture(stashRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(stashRect.x + 6, stashRect.y + 4, stashRect.width - 12, 20),
                $"效果背包 ({player.EffectStash?.Count ?? 0})", labelStyle);

            if (player.EffectStash == null) return;
            IReadOnlyList<ProjectileEffectDefinition> all = player.EffectStash.All;
            for (int i = 0; i < all.Count; i++)
            {
                Rect r = StashRowRect(i);
                bool hover = r.Contains(Event.current.mousePosition);
                GUI.color = hover ? new Color(1f, 1f, 1f, 0.25f) : new Color(1f, 1f, 1f, 0.12f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 6, r.y, r.width - 12, r.height), all[i].DisplayName, labelStyle);
            }
        }

        Rect StashRowRect(int i) =>
            new Rect(stashRect.x + 6, stashRect.y + 30 + i * (StashRowH + 4), stashRect.width - 12, StashRowH);

        void DrawEdges(Vector2 m)
        {
            foreach (WeaponEffectGraph.Edge e in graph.Edges)
            {
                WeaponEffectGraph.Node a = graph.Get(e.From);
                WeaponEffectGraph.Node b = graph.Get(e.To);
                if (a == null || b == null) continue;
                DrawLine(a.CanvasPos, b.CanvasPos, new Color(0.55f, 0.9f, 1f, 0.9f), 3f);
            }
            if (linkingFromId >= 0)
            {
                WeaponEffectGraph.Node from = graph.Get(linkingFromId);
                if (from != null) DrawLine(from.CanvasPos, m, new Color(1f, 0.85f, 0.3f, 0.9f), 3f);
            }
        }

        void DrawNodes()
        {
            foreach (WeaponEffectGraph.Node node in graph.Nodes)
            {
                bool isCore = node.Type == WeaponEffectGraph.NodeType.Core;
                Color tint = isCore ? new Color(0.55f, 0.55f, 0.6f) : EffectColor(node.Effect);
                Rect r = new Rect(node.CanvasPos.x - NodeRadius, node.CanvasPos.y - NodeRadius, NodeSize, NodeSize);
                GUI.color = tint;
                GUI.DrawTexture(r, hexTex);
                GUI.color = Color.white;

                string text = isCore
                    ? $"核心\n{player.Wand.Definition.DisplayName}"
                    : node.Effect != null ? node.Effect.DisplayName : "?";
                GUI.Label(r, text, labelStyle);

                // 连接端口（右侧中点小圆点）
                Vector2 port = PortPos(node);
                GUI.color = new Color(1f, 1f, 1f, 0.9f);
                GUI.DrawTexture(new Rect(port.x - 3, port.y - 3, 6, 6), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        void DrawGhost(Vector2 m)
        {
            if (draggingEffect == null) return;
            Rect r = new Rect(m.x - NodeRadius, m.y - NodeRadius, NodeSize, NodeSize);
            GUI.color = new Color(EffectColor(draggingEffect).r, EffectColor(draggingEffect).g,
                EffectColor(draggingEffect).b, 0.55f);
            GUI.DrawTexture(r, hexTex);
            GUI.color = Color.white;
            GUI.Label(r, draggingEffect.DisplayName, labelStyle);
        }

        void DrawLine(Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 d = b - a;
            float dist = d.magnitude;
            if (dist < 1f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 saved = GUI.matrix;
            Color savedColor = GUI.color;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.color = color;
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, dist, width), Texture2D.whiteTexture);
            GUI.matrix = saved;
            GUI.color = savedColor;
        }

        Vector2 PortPos(WeaponEffectGraph.Node node) =>
            node.CanvasPos + new Vector2(NodeRadius * 0.9f, 0f);

        // ---------- 事件 ----------

        void HandleEvents(Vector2 m)
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0) OnLeftDown(m, e);
                    else if (e.button == 1) OnRightDown(m, e);
                    break;
                case EventType.MouseDrag:
                    if (movingNodeId >= 0)
                    {
                        WeaponEffectGraph.Node n = graph.Get(movingNodeId);
                        if (n != null) n.CanvasPos = m;
                        e.Use();
                    }
                    else if (linkingFromId >= 0 || draggingEffect != null) e.Use();
                    break;
                case EventType.MouseUp:
                    if (e.button == 0) OnLeftUp(m, e);
                    break;
            }
        }

        void OnLeftDown(Vector2 m, Event e)
        {
            int stashIdx = StashIndexAt(m);
            if (stashIdx >= 0)
            {
                draggingEffect = player.EffectStash.All[stashIdx];
                e.Use();
                return;
            }
            WeaponEffectGraph.Node port = HitPort(m);
            if (port != null)
            {
                linkingFromId = port.Id;
                e.Use();
                return;
            }
            WeaponEffectGraph.Node node = HitNode(m);
            if (node != null && node.Type == WeaponEffectGraph.NodeType.Effect)
            {
                movingNodeId = node.Id;
                e.Use();
            }
        }

        void OnLeftUp(Vector2 m, Event e)
        {
            if (draggingEffect != null)
            {
                if (canvasRect.Contains(m) && !stashRect.Contains(m))
                {
                    graph.AddEffectNode(draggingEffect, m);
                    player.EffectStash.Remove(draggingEffect);
                }
                draggingEffect = null;
                e.Use();
                return;
            }
            if (linkingFromId >= 0)
            {
                WeaponEffectGraph.Node target = HitNode(m);
                if (target != null && target.Id != linkingFromId)
                    graph.Connect(linkingFromId, target.Id);
                linkingFromId = -1;
                e.Use();
                return;
            }
            if (movingNodeId >= 0)
            {
                movingNodeId = -1;
                e.Use();
            }
        }

        void OnRightDown(Vector2 m, Event e)
        {
            WeaponEffectGraph.Node node = HitNode(m);
            if (node != null && node.Type == WeaponEffectGraph.NodeType.Effect)
            {
                player.EffectStash.Add(node.Effect); // 还回背包
                graph.RemoveNode(node.Id);
                e.Use();
                return;
            }
            if (HitEdge(m, out WeaponEffectGraph.Edge edge))
            {
                graph.Disconnect(edge.From, edge.To);
                e.Use();
            }
        }

        // ---------- 命中 ----------

        int StashIndexAt(Vector2 m)
        {
            if (player.EffectStash == null || !stashRect.Contains(m)) return -1;
            for (int i = 0; i < player.EffectStash.All.Count; i++)
                if (StashRowRect(i).Contains(m)) return i;
            return -1;
        }

        WeaponEffectGraph.Node HitNode(Vector2 m)
        {
            for (int i = graph.Nodes.Count - 1; i >= 0; i--)
            {
                WeaponEffectGraph.Node n = graph.Nodes[i];
                if (((Vector2)(n.CanvasPos) - m).sqrMagnitude <= NodeRadius * NodeRadius) return n;
            }
            return null;
        }

        WeaponEffectGraph.Node HitPort(Vector2 m)
        {
            foreach (WeaponEffectGraph.Node n in graph.Nodes)
            {
                Vector2 port = PortPos(n);
                if ((port - m).sqrMagnitude <= (PortRadius + 4f) * (PortRadius + 4f)) return n;
            }
            return null;
        }

        bool HitEdge(Vector2 m, out WeaponEffectGraph.Edge hit)
        {
            foreach (WeaponEffectGraph.Edge e in graph.Edges)
            {
                WeaponEffectGraph.Node a = graph.Get(e.From);
                WeaponEffectGraph.Node b = graph.Get(e.To);
                if (a == null || b == null) continue;
                if (DistToSegment(m, a.CanvasPos, b.CanvasPos) <= EdgePickDist)
                {
                    hit = e;
                    return true;
                }
            }
            hit = default;
            return false;
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }

        // ---------- 资源 ----------

        void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                };
                labelStyle.normal.textColor = Color.white;
            }
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                };
                titleStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            }
        }

        static Color EffectColor(ProjectileEffectDefinition effect)
        {
            if (effect == null) return new Color(0.5f, 0.8f, 1f);
            // 用 moduleId 哈希出稳定的节点颜色，不同效果一眼区分
            unchecked
            {
                uint h = 2166136261u;
                string s = effect.ModuleId ?? "e";
                foreach (char ch in s) { h ^= ch; h *= 16777619u; }
                float hue = (h % 360u) / 360f;
                return Color.HSVToRGB(hue, 0.55f, 0.95f);
            }
        }

        static Texture2D CreateHexTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            const float sqrt3 = 1.7320508f;
            float cx = size * 0.5f, cy = size * 0.5f, R = size * 0.5f - 2f, h = R * 0.8660254f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float ax = Mathf.Abs(x - cx), ay = Mathf.Abs(y - cy);
                    bool inside = ax <= R && ay <= h && (sqrt3 * ax + ay) <= sqrt3 * R;
                    if (!inside) { tex.SetPixel(x, y, Color.clear); continue; }
                    float d = Mathf.Max(ax / R, ay / h, (sqrt3 * ax + ay) / (sqrt3 * R));
                    tex.SetPixel(x, y, d > 0.8f ? new Color(0.72f, 0.72f, 0.72f, 1f) : Color.white);
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
