// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-24

using SubworldLibrary;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace sea.Content.FarSea.Items;

public class EnterFarSeaItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;

        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.useTurn = true;
        Item.UseSound = SoundID.Item6;

        Item.maxStack = 1;
        Item.rare = ItemRarityID.Blue;
        Item.consumable = false;
    }

    public override bool CanUseItem(Player player) =>
        // 已经在远海时不能重复使用
        !SubworldSystem.IsActive<Subworlds.FarSea>();

    public override bool? UseItem(Player player)
    {
        // 只让本地玩家执行进入子世界
        if (player.whoAmI == Main.myPlayer)
        {
            SubworldSystem.Enter<Subworlds.FarSea>();
        }

        return true;
    }

    public override void AddRecipes()
    {
        // 机械三王的魂 + 金锭（金锭或铂金锭都可以）
        CreateRecipe()
            .AddIngredient(ItemID.SoulofSight, 3)
            .AddIngredient(ItemID.SoulofMight, 3)
            .AddIngredient(ItemID.SoulofFright, 3)
            .AddIngredient(ItemID.GoldBar, 5)
            .AddTile(TileID.Anvils)
            .Register();

        CreateRecipe()
            .AddIngredient(ItemID.SoulofSight, 3)
            .AddIngredient(ItemID.SoulofMight, 3)
            .AddIngredient(ItemID.SoulofFright, 3)
            .AddIngredient(ItemID.PlatinumBar, 5)
            .AddTile(TileID.Anvils)
            .Register();
    }
}
