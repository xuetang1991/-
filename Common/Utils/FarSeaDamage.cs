// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-06

namespace sea.Common.Utils;

/// <summary>
/// 远海伯爵全部攻击的伤害数值（经典难度基础值，唯一来源）。
/// </summary>
/// <remarks>
/// 这些数字是「经典难度」的原始值，实际伤害由
/// <see cref="GameModeDamage.Apply(int)"/> 按难度放大后再交给
/// <see cref="Terraria.Player.Hurt(Terraria.DataStructures.PlayerDeathReason, int, int, bool, bool, int, bool, float, float, float)"/>
/// 按玩家防御结算 —— 与原版敌人在 <c>NPC.SetDefaults</c> 里填 <c>NPC.damage</c> 是同一套口径。
/// 调平衡只改这里。
/// </remarks>
public static class FarSeaDamage
{
    /// <summary>冲刺撞击。专家 + 防御 45 = 150。</summary>
    public const int Dash = 92;

    /// <summary>旋转帽子。专家 + 防御 45 = 80。</summary>
    public const int Hat = 57;

    /// <summary>激光。专家 + 防御 45 = 40。</summary>
    public const int Laser = 37;

    /// <summary>小水母（伯爵吐出的追踪弹幕）。专家 + 防御 45 = 30、防御 52 = 25。</summary>
    public const int Jellyfish = 32;

    /// <summary>网格弹幕。专家 + 防御 52 = 15（防御 45 时 20）。</summary>
    public const int Grid = 27;

    /// <summary>吸取圈（按碰撞伤害结算）。专家 + 防御 52 = 10、防御 45 = 15。</summary>
    /// <remarks>用 24.5 是为了让「×2 难度倍率」后正好是 49，减掉 52×0.75 刚好 10。</remarks>
    public const float SuckAura = 24.5f;
}
