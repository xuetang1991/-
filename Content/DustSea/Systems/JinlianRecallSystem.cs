// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-01

using Microsoft.Xna.Framework;

using sea.Content.DustSea.Items;
using sea.Content.DustSea.NPCs;

using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace sea.Content.DustSea.Systems;

/// <summary>
/// 堇涟召回面板：快捷键开关面板，并把堇涟传送到玩家身边。
/// </summary>
/// <remarks>Hotkey panel that can teleport Jinlian next to the local player. The key can be rebound in the controls menu.</remarks>
public class JinlianRecallSystem : ModSystem
{
    /// <summary>
    /// 快捷键的内部名字，设置界面的本地化键由它决定。
    /// </summary>
    private const string KeybindName = "RecallJinlian";

    /// <summary>
    /// 默认按键。
    /// </summary>
    private const string DefaultKey = "P";

    /// <summary>
    /// 召回后面板显示的文本键。
    /// </summary>
    private const string TextKeyPrompt = "RecallPrompt";

    /// <summary>
    /// 召回成功后面板显示的文本键。
    /// </summary>
    private const string TextKeyReady = "RecallReady";

    /// <summary>
    /// 世界上还没有堇涟时面板显示的文本键。
    /// </summary>
    private const string TextKeyMissing = "RecallMissing";

    /// <summary>
    /// 堇涟死亡时面板显示的文本键。
    /// </summary>
    private const string TextKeyDead = "RevivePrompt";

    /// <summary>
    /// 传送到玩家侧边的水平偏移。
    /// </summary>
    private const float RecallOffsetX = 48f;

    /// <summary>
    /// 召回快捷键。
    /// </summary>
    private ModKeybind? _recallKeybind;

    /// <summary>
    /// 面板是否处于打开状态。
    /// </summary>
    public bool PanelOpen { get; private set; }

    /// <summary>
    /// 面板正文对应的本地化键后缀。
    /// </summary>
    public string PanelTextKey { get; private set; } = TextKeyPrompt;

    /// <summary>
    /// 堇涟是否处于死亡状态（此时面板改成复活 / 关闭）。
    /// </summary>
    public bool JinlianIsDead => JinlianSpawnSystem.JinlianDead;

    /// <summary>
    /// 注册可以在设置里改绑的快捷键。
    /// </summary>
    public override void Load() => _recallKeybind = KeybindLoader.RegisterKeybind(Mod, KeybindName, DefaultKey);

    /// <summary>
    /// 卸载时清理状态。
    /// </summary>
    public override void Unload()
    {
        _recallKeybind = null;
        PanelOpen = false;
        PanelTextKey = TextKeyPrompt;
    }

    /// <summary>
    /// 处理快捷键按下。
    /// </summary>
    /// <remarks>The key toggles the panel and is ignored while another conversation or text input is active.</remarks>
    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gameMenu || _recallKeybind is null)
        {
            return;
        }

        if (!_recallKeybind.JustPressed)
        {
            return;
        }

        if (PanelOpen)
        {
            ClosePanel();

            return;
        }

        Player player = Main.LocalPlayer;

        // 平时要带小水母才弹面板；她已经死了的话例外，没信物也能开面板救人
        if (!JinlianSpawnSystem.JinlianDead && !HasRecallToken(player))
        {
            return;
        }

        if (player.talkNPC >= 0 || Main.drawingPlayerChat || Main.editSign || Main.editChest)
        {
            return;
        }

        // 复活动画播放期间不弹面板，避免镜头和界面打架
        if (ModContent.GetInstance<JinlianReviveSystem>().IsRunning)
        {
            return;
        }

        PanelOpen = true;
        PanelTextKey = JinlianSpawnSystem.JinlianDead ? TextKeyDead : TextKeyPrompt;
    }

    /// <summary>
    /// 关闭面板。
    /// </summary>
    public void ClosePanel() => PanelOpen = false;

    /// <summary>
    /// 在召回面板上点击复活，开始复活动画。
    /// </summary>
    /// <returns>成功开始复活动画返回 true</returns>
    /// <remarks>The panel closes first so the cinematic gets the whole screen, and the NPC itself is spawned by the revive system when the animation ends.</remarks>
    public bool TryReviveJinlian()
    {
        if (!JinlianSpawnSystem.JinlianDead)
        {
            return false;
        }

        ClosePanel();

        return ModContent.GetInstance<JinlianReviveSystem>().TryStartRevive();
    }

    /// <summary>
    /// 把堇涟传送到玩家身边。
    /// </summary>
    /// <param name="player">接收召回的玩家</param>
    /// <returns>成功传送返回 true</returns>
    public bool TryRecallJinlian(Player player)
    {
        NPC jinlian = FindJinlian();

        if (jinlian is null)
        {
            PanelTextKey = TextKeyMissing;

            return false;
        }

        float side = player.direction >= 0 ? -1f : 1f;
        Vector2 target = new(player.Center.X + side * RecallOffsetX, player.Bottom.Y);

        jinlian.Bottom = target;
        jinlian.velocity = Vector2.Zero;
        jinlian.ai[0] = 1f;
        jinlian.netUpdate = true;

        SoundEngine.PlaySound(SoundID.Item8, player.Center);

        PanelTextKey = TextKeyReady;

        return true;
    }

    /// <summary>
    /// 找到世界上的堇涟。
    /// </summary>
    /// <returns>堇涟 NPC，没有则返回 null</returns>
    public NPC? FindJinlian()
    {
        int type = ModContent.NPCType<JinlianNPC>();

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC candidate = Main.npc[i];

            if (candidate.active && candidate.type == type)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 判断玩家背包里有没有召回用的信物
    /// </summary>
    /// <param name="player">目标玩家</param>
    /// <returns>背包里有小水母返回 true</returns>
    /// <remarks>The jellyfish keepsake is the unlock condition of the recall panel, so losing it also disables the hotkey.</remarks>
    public static bool HasRecallToken(Player player)
    {
        int itemType = ModContent.ItemType<JinlianJellyfishItem>();

        for (int i = 0; i < player.inventory.Length; i++)
        {
            if (player.inventory[i].type == itemType && player.inventory[i].stack > 0)
            {
                return true;
            }
        }

        return false;
    }
}
