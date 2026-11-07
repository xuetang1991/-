// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-03

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace sea.Content.DustSea.Buffs;

/// <summary>
/// 「痕迹」：喝下灰色轨迹后的效果，攻击命中敌人时附加一段随机减益。
/// </summary>
/// <remarks>Granted by GrayTrailItem for five minutes. The actual debuff is applied by GrayTrailGlobalNPC on hit.</remarks>
public class GrayTrailBuff : ModBuff
{
    /// <summary>
    /// 饮酒后效果的持续时长（5 分钟，按 tick 计）。
    /// </summary>
    /// <remarks>Kept here so the item and the global NPC share one source of truth.</remarks>
    public const int EffectDurationTicks = 5 * 60 * 60;

    /// <summary>
    /// 命中时附加的减益持续时长（3 秒）。
    /// </summary>
    /// <remarks>Three seconds is long enough to read on the enemy health bar but short enough to stay a flavour effect.</remarks>
    public const int DebuffDurationTicks = 3 * 60;

    /// <summary>
    /// 某个玩家是否正处在「痕迹」状态。
    /// </summary>
    /// <param name="player">要检查的玩家</param>
    /// <returns>拥有该增益返回 true</returns>
    public static bool IsActive(Player player) => player is { active: true } && player.HasBuff(ModContent.BuffType<GrayTrailBuff>());

    /// <summary>
    /// 注册增益的基础显示设定。
    /// </summary>
    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = false;
        Main.buffNoTimeDisplay[Type] = false;
        BuffID.Sets.NurseCannotRemoveDebuff[Type] = false;
    }
}
