// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-24

using Microsoft.Xna.Framework;

using sea.Content.FarSea.NPCs;

using SubworldLibrary;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace sea.Content.FarSea.Items;

public class FluorescentBaitItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;

        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = 45;
        Item.useAnimation = 45;
        Item.UseSound = SoundID.Roar;

        Item.maxStack = 9999;
        Item.rare = ItemRarityID.LightRed;
        Item.consumable = true;
    }

    public override bool CanUseItem(Player player)
    {
        // 只能在远海子世界使用
        if (!SubworldSystem.IsActive<Subworlds.FarSea>())
        {
            return false;
        }

        // 检查Boss是否已经存在
        if (NPC.AnyNPCs(ModContent.NPCType<FarSeaEarlNPC>()))
        {
            return false;
        }

        return true;
    }

    public override bool? UseItem(Player player)
    {
        // 只在服务端或单人游戏执行
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            // 计算生成位置（玩家上方300像素）
            Vector2 spawnPos = player.Center - new Vector2(0, 300);

            // 使用NPC.NewNPC生成Boss
            int bossIndex = NPC.NewNPC(
                player.GetSource_ItemUse(Item),
                (int)spawnPos.X,
                (int)spawnPos.Y,
                ModContent.NPCType<FarSeaEarlNPC>()
            );

            // 如果生成成功
            if (bossIndex >= 0 && bossIndex < Main.maxNPCs)
            {
                NPC boss = Main.npc[bossIndex];

                // 设置目标为使用物品的玩家
                boss.target = player.whoAmI;
                boss.netUpdate = true;

                // 同步到其他玩家
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, bossIndex);
                }

                // 显示消息（使用简单方法）
                Main.NewText("远海伯爵已被唤醒！", 175, 75, 255);
            }
        }

        return true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.SharkFin, 5)
            .AddIngredient(ItemID.Seashell, 5)
            .AddIngredient(ItemID.SoulofMight, 5)
            .AddTile(TileID.Anvils)
            .Register();
    }
}
