using UnityEngine;

namespace Cinder.Game.Spells
{
    /// <summary>
    /// 多重施法：下一个投射物法术额外复制 N-1 份（总 N 份），
    /// 按 SpreadStep（度）扇形展开。
    /// </summary>
    [CreateAssetMenu(menuName = "Cinder/Spells/Multicast Spell")]
    public sealed class MulticastSpellDefinition : SpellDefinition
    {
        public override SpellKind Kind => SpellKind.Modifier;

        [Min(1)] public int Count = 2;

        [Tooltip("相邻投射物的角度间隔（度）")]
        public float SpreadStep = 8f;
    }
}
