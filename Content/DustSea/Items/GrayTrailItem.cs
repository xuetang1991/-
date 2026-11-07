// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-03

using sea.Content.DustSea.Buffs;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace sea.Content.DustSea.Items;

/// <summary>
/// 「灰色轨迹」：堇涟商店出售的酒，致敬 Beyond 的同名歌曲。
/// </summary>
/// <remarks>Drinking grants the 痕迹 buff for five minutes; while it lasts, hits apply a random short debuff.</remarks>
public class GrayTrailItem : ModItem
{
    /// <summary>
    /// 商店售价（铜币计，1 金币）。
    /// </summary>
    /// <remarks>Shared with the shop UI so the listed price and the purchased price never drift apart.</remarks>
    public const int ShopPrice = 10000;

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 24;
        Item.maxStack = 30;
        Item.value = Item.sellPrice(silver: 50);
        Item.rare = ItemRarityID.Blue;
        Item.useStyle = ItemUseStyleID.DrinkLiquid;
        Item.useTime = 17;
        Item.useAnimation = 17;
        Item.useTurn = true;
        Item.autoReuse = false;
        Item.consumable = true;
        Item.UseSound = SoundID.Item3;
        Item.buffType = ModContent.BuffType<GrayTrailBuff>();
        Item.buffTime = GrayTrailBuff.EffectDurationTicks;
    }
}
