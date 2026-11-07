// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-01

using Terraria;
using Terraria.ModLoader;

namespace sea.Content.DustSea.Buffs;

/// <summary>
/// 「并肩作战」：堇涟跟随时与玩家共用防御力与回血，并替玩家承受伤害。
/// </summary>
/// <remarks>Granted while Jinlian follows the player. She shares the player's defense and healing, and takes the damage aimed at the player.</remarks>
public class CompanionBuff : ModBuff
{
    /// <summary>
    /// 注册增益的基础显示设定。
    /// </summary>
    public override void SetStaticDefaults()
    {
        // 跟随期间每帧刷新，不显示倒计时
        Main.buffNoTimeDisplay[Type] = true;
        Main.debuff[Type] = false;
    }
}
