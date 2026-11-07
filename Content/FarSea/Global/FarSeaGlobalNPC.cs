// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-31

using System.Collections.Generic;

using SubworldLibrary;

using Terraria.ModLoader;

namespace sea.Content.FarSea.Global;

/// <summary>
/// 远海子世界刷怪控制 - 禁止子世界刷新任何怪物
/// </summary>
public class FarSeaGlobalNPC : GlobalNPC
{
    /// <summary>
    /// 清空远海子世界的原版刷怪池
    /// </summary>
    /// <param name="pool">刷怪权重表，键为 NPC 类型，值为权重</param>
    /// <param name="spawnInfo">本次刷怪的上下文信息</param>
    /// <remarks>Key 0 in the pool stands for the vanilla spawn branch, so clearing the pool stops every vanilla spawn in the subworld.</remarks>
    public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
    {
        if (!IsFarSeaActive())
        {
            return;
        }

        // 0 号权重代表「交给原版刷怪分支处理」，清空后子世界不会再刷新任何怪物
        pool.Clear();
    }

    /// <summary>
    /// 判断玩家当前是否处在远海子世界中
    /// </summary>
    /// <returns>在远海子世界中返回 true</returns>
    private static bool IsFarSeaActive() => SubworldSystem.IsActive<Subworlds.FarSea>();
}
