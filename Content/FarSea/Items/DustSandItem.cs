// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-24

using sea.Content.FarSea.Tiles; // 核心修改：去掉了.Content，直接引用 sea.Content.FarSea.Tiles

using Terraria;
using Terraria.ModLoader;

namespace sea.Content.FarSea.Items;

public class DustSandItem : ModItem
{
    public override void SetDefaults()
    {
        // 绑定方块，并自动设置为“挖掉掉落自身”
        Item.DefaultToPlaceableTile(ModContent.TileType<DustSandTile>());

        Item.width = 16;
        Item.height = 16;
        Item.maxStack = 9999;
        Item.value = Item.sellPrice(copper: 1);
    }
}
