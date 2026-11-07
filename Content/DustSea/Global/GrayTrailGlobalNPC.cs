// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-03

using sea.Content.DustSea.Buffs;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace sea.Content.DustSea.Global;

/// <summary>
/// 「痕迹」效果：带着该增益时，每次命中敌人都会附加一段随机减益。
/// </summary>
/// <remarks>Only the authoritative side rolls the debuff, so multiplayer clients never desync on the random pick.</remarks>
public class GrayTrailGlobalNPC : GlobalNPC
{
    /// <summary>
    /// 命中时可附加的减益池，偏向「看得出变化但不致命」的类型。
    /// </summary>
    /// <remarks>Read only so it stays outside the mutable static state rules.</remarks>
    private static readonly int[] DebuffPool =
    [
        BuffID.OnFire,
        BuffID.Poisoned,
        BuffID.Venom,
        BuffID.Frostburn,
        BuffID.CursedInferno,
        BuffID.Ichor,
        BuffID.ShadowFlame,
        BuffID.BrokenArmor,
        BuffID.Confused,
        BuffID.Slow,
    ];

    public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) => TryApplyTrace(npc, player);

    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        Player owner = Main.player[projectile.owner];

        if (owner is not { active: true })
        {
            return;
        }

        TryApplyTrace(npc, owner);
    }

    /// <summary>
    /// 若攻击者带着「痕迹」，给目标附加一段随机减益。
    /// </summary>
    /// <param name="npc">被命中的敌人</param>
    /// <param name="player">攻击者</param>
    private static void TryApplyTrace(NPC npc, Player player)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !npc.active || npc.friendly || npc.immortal)
        {
            return;
        }

        if (!GrayTrailBuff.IsActive(player))
        {
            return;
        }

        int choice = DebuffPool[Main.rand.Next(DebuffPool.Length)];

        npc.AddBuff(choice, GrayTrailBuff.DebuffDurationTicks);
    }
}
