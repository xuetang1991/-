// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-06

using Terraria;
using Terraria.DataStructures;

namespace sea.Common.Utils;

/// <summary>
/// 把「经典难度基础伤害」换算成当前难度的实际伤害。
/// </summary>
/// <remarks>
/// 原版敌人伤害在生成时按 <see cref="GameModeData.EnemyDamageMultiplier"/> 放大
/// （经典 x1、专家 x2、大师 x3），之后由 <see cref="Player.Hurt"/> 按玩家防御扣减，
/// 扣减比例还取决于难度（经典 0.5、专家 0.75、大师 1.0）。
/// 本 mod 的伤害也只填「经典基础值」，其余全部交回原版结算。
/// </remarks>
public static class GameModeDamage
{
    /// <summary>
    /// 把经典难度基础伤害换算成当前难度下真正丢给 <see cref="Player.Hurt"/> 的数值。
    /// </summary>
    /// <param name="classicDamage">经典难度下的基础伤害。</param>
    /// <returns>当前难度下的伤害。</returns>
    public static int Apply(int classicDamage)
    {
        return (int)(classicDamage * Main.GameModeInfo.EnemyDamageMultiplier);
    }
}
