// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-01

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace sea.Content.DustSea.Items;

/// <summary>
/// 堇涟赠予的小水母，用来当召唤她的信物
/// </summary>
/// <remarks>A single story keepsake; it never stacks and carries no gameplay stats beyond being a token.</remarks>
public class JinlianJellyfishItem : ModItem
{
    /// <summary>
    /// 物品的稀有度，剧情道具用粉色
    /// </summary>
    /// <remarks>Pink keeps the token visually distinct from ordinary materials.</remarks>
    public const int TokenRarity = ItemRarityID.Pink;

    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 24;
        Item.maxStack = 1;
        Item.value = 0;
        Item.rare = TokenRarity;
        Item.useStyle = ItemUseStyleID.None;
        Item.UseSound = null;
    }
}
