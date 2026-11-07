// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-31

using System.Collections.Generic;

using Terraria;
using Terraria.ModLoader;

namespace sea.Content.DustSea.Global;

/// <summary>
/// 浊海刷怪控制 - 把浊海当作独立环境，禁止刷新其他环境的生物
/// </summary>
/// <remarks>The Dust Sea is treated as its own environment, so every vanilla spawn pool entry is cleared there. Dedicated Dust Sea creatures can be added later from their own spawn system.</remarks>
public class DustSeaGlobalNPC : GlobalNPC
{
    /// <summary>
    /// 清空浊海区域的原版刷怪池
    /// </summary>
    /// <param name="pool">刷怪权重表，键为 NPC 类型，值为权重</param>
    /// <param name="spawnInfo">本次刷怪的上下文信息</param>
    /// <remarks>Key 0 in the pool stands for the vanilla spawn branch, so clearing the pool stops every vanilla spawn inside the Dust Sea.</remarks>
    public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
    {
        if (!IsDustSeaActive(spawnInfo.Player))
        {
            return;
        }

        // 0 号权重代表「交给原版刷怪分支处理」，清空后浊海不会再刷新其他环境的生物
        pool.Clear();
    }

    /// <summary>
    /// 浊海内关闭自然刷怪上限
    /// </summary>
    /// <param name="player">参与刷怪判定的玩家</param>
    /// <param name="spawnRate">刷怪间隔权重</param>
    /// <param name="maxSpawns">当前自然刷怪上限</param>
    /// <remarks>Setting the cap to zero keeps vanilla spawn logic from trickling creatures into the Dust Sea even when other biomes are active around the player.</remarks>
    public override void EditSpawnRate(
        Player player,
        ref int spawnRate,
        ref int maxSpawns
    )
    {
        if (!IsDustSeaActive(player))
        {
            return;
        }

        maxSpawns = 0;
    }

    /// <summary>
    /// 判断玩家当前是否处在浊海环境中
    /// </summary>
    /// <param name="player">参与判定的玩家</param>
    /// <returns>在浊海环境中返回 true</returns>
    private static bool IsDustSeaActive(Player player)
    {
        if (player == null || !player.active)
        {
            return false;
        }

        return player.InModBiome<DustSeaBiome>();
    }
}
