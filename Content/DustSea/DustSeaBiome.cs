// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-31

using sea.Content.FarSea.Systems;

using Terraria;
using Terraria.ModLoader;

namespace sea.Content.DustSea;

/// <summary>
/// 浊海生物群系 - 玩家周围浊沙块数量达到阈值时判定为身处浊海
/// </summary>
/// <remarks>Detection follows the vanilla scene metric scan, so mining or placing Dust Sand can turn any area into the Dust Sea.</remarks>
public class DustSeaBiome : ModBiome
{
    /// <summary>
    /// 判定为浊海所需的浊沙块数量
    /// </summary>
    public const int RequiredDustSandCount = 40;

    /// <summary>
    /// 浊海背景音乐：Assets/Music/DustSeaAmbient.ogg（Music 目录自动注册）
    /// </summary>
    public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Music/DustSeaAmbient");

    /// <summary>
    /// 判断玩家是否身处浊海
    /// </summary>
    /// <param name="player">参与判定的玩家</param>
    /// <returns>周围浊沙块数量达到阈值时返回 true</returns>
    public override bool IsBiomeActive(Player player) => TileCountSystem.DustSandCount >= RequiredDustSandCount;
}
