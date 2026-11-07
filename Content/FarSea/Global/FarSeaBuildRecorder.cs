// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-05

using sea.Content.FarSea.Systems;

using SubworldLibrary;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace sea.Content.FarSea.Global;

/// <summary>记录玩家在远海子世界里挖掉的方块（挖掉的记录会被删掉，重新进入就复原）。</summary>
public class FarSeaBuildRecorder : GlobalTile
{
    public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (FarSeaBuildStore.Suppress || !SubworldSystem.IsActive<Subworlds.FarSea>())
        {
            return;
        }

        if (fail || Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        FarSeaBuildStore.Forget(i, j);
    }
}
