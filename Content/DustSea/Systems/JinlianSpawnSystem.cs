// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-31

using sea.Content.DustSea.NPCs;

using SubworldLibrary;

using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace sea.Content.DustSea.Systems;

/// <summary>
/// 堇涟的出现流程 - 主世界里没有堇涟时在左侧浊海的陆地补一个，已有则不动
/// </summary>
/// <remarks>The NPC is treated as a self healing world state: as long as the main world has no Jinlian, one is placed on the left dust sea shore. The Earl defeat flag is still recorded for later content but no longer gates the spawn, so worlds that lost the flag still get their NPC.</remarks>
public class JinlianSpawnSystem : ModSystem
{
    private const string DefeatedKey = "farSeaEarlDefeated";
    private const string SpawnedKey = "jinlianSpawned";
    private const string IntroducedKey = "jinlianIntroduced";
    private const string StoryVersionKey = "jinlianStoryVersion";
    private const string TalkCountKey = "jinlianTalkCount";
    private const string GiftGivenKey = "jinlianGiftGiven";
    private const string DeadKey = "jinlianDead";
    private const string ReviveTalkKey = "jinlianReviveTalk";
    private const string FirstReviveStoryKey = "jinlianFirstReviveStory";
    private const int CurrentStoryVersion = 1;
    private const int SearchStartX = 150;
    private const int SearchMaxX = 380;
    private const int SpawnRetryInterval = 60;
    private const int SpawnLiquidFreeTiles = 2;

    private int _retryTimer;

    /// <summary>
    /// 当前世界是否已击败过远海伯爵
    /// </summary>
    public static bool FarSeaEarlDefeated { get; private set; }

    /// <summary>
    /// 本次游戏内是否刚击败远海伯爵（用于跨子世界保留战果）
    /// </summary>
    public static bool PendingDefeat { get; private set; }

    /// <summary>
    /// 堇涟是否已经生成过
    /// </summary>
    public static bool JinlianSpawned { get; private set; }

    /// <summary>
    /// 堇涟的首次对话剧情是否已经完成
    /// </summary>
    public static bool JinlianIntroduced { get; private set; }

    /// <summary>
    /// 已经看过的首次对话剧情版本号
    /// </summary>
    /// <remarks>Bumping the version replays the story once on older saves, which is how the three option layout reached worlds that finished the two button version.</remarks>
    public static int JinlianStoryVersion { get; private set; }

    /// <summary>
    /// 记录堇涟的首次对话剧情已经完成
    /// </summary>
    public static void MarkJinlianIntroduced()
    {
        JinlianIntroduced = true;
        JinlianStoryVersion = CurrentStoryVersion;
    }

    /// <summary>
    /// 剧情结束后与堇涟搭话的次数
    /// </summary>
    /// <remarks>The count is what decides when the jellyfish keepsake is handed out, so it has to survive reloads.</remarks>
    public static int JinlianTalkCount { get; private set; }

    /// <summary>
    /// 小水母是否已经赠出过
    /// </summary>
    public static bool JinlianGiftGiven { get; private set; }

    /// <summary>
    /// 记一次与堇涟的搭话
    /// </summary>
    public static void MarkJinlianTalked() => JinlianTalkCount++;

    /// <summary>
    /// 记录小水母已经赠出
    /// </summary>
    public static void MarkJinlianGiftGiven() => JinlianGiftGiven = true;

    /// <summary>
    /// 堇涟是否已经死亡并等待玩家复活
    /// </summary>
    /// <remarks>While this is set the auto spawn is suppressed, otherwise the NPC would be restored on the next tick and death would never stick.</remarks>
    public static bool JinlianDead { get; private set; }

    /// <summary>
    /// 堇涟复活后是否还没有说过复活后的那句台词
    /// </summary>
    public static bool JinlianAwaitingReviveTalk { get; private set; }

    /// <summary>
    /// 第一次复活后的那套新剧情是否已经播过
    /// </summary>
    /// <remarks>Only the first revive plays the long conversation; later revives fall back to the short line.</remarks>
    public static bool JinlianFirstReviveStorySeen { get; private set; }

    /// <summary>
    /// 找当前世界里活着的堇涟
    /// </summary>
    /// <returns>堇涟 NPC，没有则返回 null</returns>
    public static NPC? FindLiveJinlian()
    {
        int npcType = ModContent.NPCType<NPCs.JinlianNPC>();

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (npc.active && npc.type == npcType)
            {
                return npc;
            }
        }

        return null;
    }

    /// <summary>
    /// 记录堇涟已经死亡
    /// </summary>
    public static void MarkJinlianDead()
    {
        if (JinlianDead)
        {
            return;
        }

        JinlianDead = true;
        ModLoader.GetMod("sea")?.Logger.Info("堇涟已死亡，等待召回面板复活 / Jinlian died, she now waits to be revived from the recall panel");
    }

    /// <summary>
    /// 记录堇涟已经复活
    /// </summary>
    public static void MarkJinlianRevived()
    {
        JinlianDead = false;
        JinlianSpawned = true;
        JinlianAwaitingReviveTalk = true;
    }

    /// <summary>
    /// 记录复活后的那句台词已经说完
    /// </summary>
    public static void MarkReviveTalked() => JinlianAwaitingReviveTalk = false;

    /// <summary>
    /// 记录第一次复活后的新剧情已经看完
    /// </summary>
    public static void MarkFirstReviveStorySeen() => JinlianFirstReviveStorySeen = true;

    /// <summary>
    /// 记录远海伯爵已被击败
    /// </summary>
    /// <remarks>The flag is written to the world data only when the main world is saved, so it is also cached in memory until the town NPC is spawned.</remarks>
    public static void MarkFarSeaEarlDefeated()
    {
        PendingDefeat = true;
        ModLoader.GetMod("sea")?.Logger.Info("远海伯爵已被击败，回到主世界后会在浊海左岸放置堇涟 / Far Sea Earl defeated, Jinlian will be placed once the main world is back");
    }

    /// <summary>
    /// 在主世界刷新堇涟：击败过远海伯爵且世界上没有堇涟时补一个，已有则不动
    /// </summary>
    /// <remarks>The existence check runs before the defeat check so an already placed NPC is never duplicated, while a missing one is restored after deaths or failed placements once the Earl has been defeated.</remarks>
    public override void PostUpdateWorld()
    {
        if (Main.gameMenu || !HasActivePlayer())
        {
            return;
        }

        // 子世界里不落 NPC，等玩家回到主世界
        if (SubworldSystem.IsActive<sea.Content.FarSea.Subworlds.FarSea>())
        {
            return;
        }

        int npcType = ModContent.NPCType<JinlianNPC>();

        // 世界上已经有堇涟就不重复生成
        if (NPC.AnyNPCs(npcType))
        {
            if (!JinlianSpawned)
            {
                Mod.Logger.Info("检测到世界上已有堇涟，跳过生成 / Jinlian already exists in this world, skipping spawn");
            }

            JinlianSpawned = true;
            return;
        }

        // 她死过一次就不再自动补生成，交给召回面板里的复活流程
        if (JinlianDead)
        {
            return;
        }

        // 没击败过远海伯爵就不出现
        if (!FarSeaEarlDefeated && !PendingDefeat)
        {
            return;
        }

        if (_retryTimer > 0)
        {
            _retryTimer--;
            return;
        }

        if (!TryFindSpawnTile(out int tileX, out int tileY))
        {
            _retryTimer = SpawnRetryInterval;
            return;
        }

        int npcIndex = NPC.NewNPC(
            new EntitySource_SpawnNPC(),
            tileX * 16,
            (tileY - 3) * 16,
            npcType
        );

        if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
        {
            _retryTimer = SpawnRetryInterval;
            return;
        }

        NPC jinlian = Main.npc[npcIndex];

        jinlian.ai[0] = JinlianIntroduced ? 1f : 0f;
        jinlian.ai[1] = tileX;
        jinlian.ai[2] = tileY;
        jinlian.direction = -1;
        jinlian.spriteDirection = -1;
        jinlian.netUpdate = true;

        FarSeaEarlDefeated = true;
        PendingDefeat = false;
        JinlianSpawned = true;
        Mod.Logger.Info($"堇涟已生成于浊海左岸 {tileX}, {tileY} / Jinlian spawned at the dust sea shore {tileX}, {tileY}");
    }

    /// <summary>
    /// 保存战败与生成记录
    /// </summary>
    /// <param name="tag">世界存档标签</param>
    public override void SaveWorldData(TagCompound tag)
    {
        if (FarSeaEarlDefeated || PendingDefeat)
        {
            tag[DefeatedKey] = true;
        }

        if (JinlianSpawned)
        {
            tag[SpawnedKey] = true;
        }

        if (JinlianIntroduced)
        {
            tag[IntroducedKey] = true;
            tag[StoryVersionKey] = JinlianStoryVersion;
        }

        if (JinlianTalkCount > 0)
        {
            tag[TalkCountKey] = JinlianTalkCount;
        }

        if (JinlianGiftGiven)
        {
            tag[GiftGivenKey] = true;
        }

        if (JinlianDead)
        {
            tag[DeadKey] = true;
        }

        if (JinlianAwaitingReviveTalk)
        {
            tag[ReviveTalkKey] = true;
        }

        if (JinlianFirstReviveStorySeen)
        {
            tag[FirstReviveStoryKey] = true;
        }
    }

    /// <summary>
    /// 新世界开始生成时把剧情进度复位
    /// </summary>
    /// <remarks>This must not use ClearWorld: that hook also fires while the subworld is generated, which wiped the in-session "defeat the Earl, then place Jinlian" flag and stopped her from ever showing up.</remarks>
    public override void PreWorldGen()
    {
        FarSeaEarlDefeated = false;
        JinlianSpawned = false;
        JinlianIntroduced = false;
        JinlianStoryVersion = 0;
        JinlianTalkCount = 0;
        JinlianGiftGiven = false;
        JinlianDead = false;
        JinlianAwaitingReviveTalk = false;
        JinlianFirstReviveStorySeen = false;
        _retryTimer = 0;

        // 只有从主菜单开新世界才算「换了一个存档」，子世界生成时必须保留待生成标记
        if (Main.gameMenu)
        {
            PendingDefeat = false;
        }
    }

    /// <summary>
    /// 读取战败与生成记录
    /// </summary>
    /// <param name="tag">世界存档标签</param>
    public override void LoadWorldData(TagCompound tag)
    {
        FarSeaEarlDefeated = tag.GetBool(DefeatedKey);
        JinlianSpawned = tag.GetBool(SpawnedKey);
        JinlianIntroduced = tag.GetBool(IntroducedKey);
        JinlianStoryVersion = tag.ContainsKey(StoryVersionKey) ? tag.GetInt(StoryVersionKey) : 0;
        JinlianTalkCount = tag.ContainsKey(TalkCountKey) ? tag.GetInt(TalkCountKey) : 0;
        JinlianGiftGiven = tag.GetBool(GiftGivenKey);
        JinlianDead = tag.GetBool(DeadKey);
        JinlianAwaitingReviveTalk = tag.GetBool(ReviveTalkKey);
        JinlianFirstReviveStorySeen = tag.GetBool(FirstReviveStoryKey);

        // 旧存档看过的是旧版剧情，这里让新版剧情再播放一次
        if (JinlianIntroduced && JinlianStoryVersion < CurrentStoryVersion)
        {
            JinlianIntroduced = false;
        }
    }

    /// <summary>
    /// 判断当前是否有活跃玩家
    /// </summary>
    /// <returns>存在活跃玩家返回 true</returns>
    private static bool HasActivePlayer()
    {
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            if (Main.player[i].active)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 在浊海左岸找一块可以站人的地面，优先干燥处
    /// </summary>
    /// <param name="tileX">找到的落脚点横坐标</param>
    /// <param name="tileY">找到的落脚点纵坐标</param>
    /// <returns>找到落脚点时返回 true</returns>
    internal static bool TryFindSpawnTile(out int tileX, out int tileY)
    {
        tileX = -1;
        tileY = -1;

        int dryBestY = int.MaxValue;
        int dryX = -1;
        int wetBestY = int.MaxValue;
        int wetX = -1;

        for (int x = SearchStartX; x <= SearchMaxX; x++)
        {
            if (!TryFindSurfaceTile(x, out int surfaceY))
            {
                continue;
            }

            if (IsLiquidFree(x, surfaceY))
            {
                if (surfaceY < dryBestY)
                {
                    dryBestY = surfaceY;
                    dryX = x;
                }

                continue;
            }

            if (surfaceY < wetBestY)
            {
                wetBestY = surfaceY;
                wetX = x;
            }
        }

        if (dryX > 0)
        {
            tileX = dryX;
            tileY = dryBestY;
            return true;
        }

        if (wetX > 0)
        {
            tileX = wetX;
            tileY = wetBestY;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 找出某一列最上面那块上方有两格净空的实心方块
    /// </summary>
    /// <param name="x">列坐标</param>
    /// <param name="surfaceY">找到的地面纵坐标</param>
    /// <returns>找到可站立地面时返回 true</returns>
    private static bool TryFindSurfaceTile(int x, out int surfaceY)
    {
        surfaceY = -1;

        if (!WorldGen.InWorld(x, 10, 2))
        {
            return false;
        }

        for (int y = 10; y < Main.maxTilesY - 30; y++)
        {
            if (!WorldGen.SolidTile(x, y))
            {
                continue;
            }

            if (WorldGen.SolidTile(x, y - 1) || WorldGen.SolidTile(x, y - 2))
            {
                return false;
            }

            surfaceY = y;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 判断地面上方若干格内没有液体
    /// </summary>
    /// <param name="x">列坐标</param>
    /// <param name="surfaceY">地面纵坐标</param>
    /// <returns>上方无液体时返回 true</returns>
    private static bool IsLiquidFree(int x, int surfaceY)
    {
        for (int offset = 1; offset <= SpawnLiquidFreeTiles; offset++)
        {
            Tile tile = Main.tile[x, surfaceY - offset];

            if (tile.LiquidAmount > 0)
            {
                return false;
            }
        }

        return true;
    }
}
