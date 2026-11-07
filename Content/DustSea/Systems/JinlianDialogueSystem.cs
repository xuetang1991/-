// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-31

using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using ReLogic.Content;
using ReLogic.Graphics;
using ReLogic.Utilities;

using sea.Content.DustSea.NPCs;

using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace sea.Content.DustSea.Systems;

/// <summary>
/// 堇涟的专属对话框 - 首次剧情不再使用原版聊天框，而是自绘一套紫黑色调的对话面板
/// </summary>
/// <remarks>The vanilla chat panel is skipped while a Jinlian intro dialogue is active and a custom panel is drawn instead. The panel shows a portrait, the name plate, the wrapped dialogue line and up to two option buttons; clicks are handled here so the vanilla buttons are not needed. Closing the chat with ESC or the right mouse button returns to the regular chat state.</remarks>
public class JinlianDialogueSystem : ModSystem
{
    private const string BoxTexturePath = "sea/Assets/UI/JinlianDialogueBox";
    private const string ButtonTexturePath = "sea/Assets/UI/JinlianDialogueButton";
    private const string ButtonHoverTexturePath = "sea/Assets/UI/JinlianDialogueButtonHover";
    // 表情差分贴图：JinlianFace1 ~ JinlianFace8
    private const string FaceTexturePathFormat = "sea/Assets/UI/JinlianFace{0}";
    private const int FaceTextureCount = 8;

    private const int NamePlateCenterX = 100;
    private const int NamePlateCenterY = 26;

    private const float PortraitCenterX = 100f;
    // 立绘底边贴住对话框内沿（面板高 280，底部边框 3 像素）
    private const float PortraitBottomY = 327f;

    private const float TextLeft = 196f;
    private const float TextTop = 42f;
    private const float TextWidth = 440f;
    private const float TextScale = 0.85f;
    private const float TextLineHeight = 24f;
    private const float NameScale = 0.9f;

    private const float ButtonLeft = 196f;
    private const float ButtonTopOffset = 152f;
    private const float ButtonHeight = 32f;
    private const float ButtonSpacing = 8f;
    private const float ButtonLabelScale = 0.75f;

    private const float PanelBottomGap = 150f;

    // 打字机效果：每秒浮现多少字，以及每浮现多少个字播一次对白音
    private const float TypewriterCharsPerSecond = 46f;
    private const int TypewriterBlipEvery = 3;

    // 死亡态面板：两个按钮竖排居中，位置抬高到面板中部
    private const float ReviveButtonTop = 118f;
    private const float DeathTitleTop = 48f;

    // 赠礼物品框：落在对话框右上角空白处，避开下方按钮
    private const float GiftSlotLeft = 552f;
    private const float GiftSlotTop = 54f;
    private const float GiftIconLimit = 32f;
    private const float GiftCaptionGap = 2f;

    private const float MaxDialogueDistance = 60f * 16f;

    // 原版聊天框相关音效：自绘面板期间要静音，避免两套界面音混在一起
    private const int SuppressChatSoundTicks = 12;

    private static readonly string[] SuppressedChatSoundPaths =
    [
        "Terraria/Sounds/Chat",
        "Terraria/Sounds/Menu_Open",
        "Terraria/Sounds/Menu_Close",
        "Terraria/Sounds/Menu_Tick"
    ];

    private static readonly Color NameColor = new(212, 178, 255);
    private static readonly Color DeathTitleColor = new(255, 140, 150);
    private static readonly Color TextColor = new(232, 226, 244);
    private static readonly Color ButtonTextColor = new(224, 214, 242);

    // 对白音与按钮音都是脚本合成的 44100Hz 单声道 PCM，音高略带随机避免机械感
    private static readonly SoundStyle TypewriterSound = new("sea/Assets/Sounds/JinlianTypewriter")
    {
        Volume = 0.5f,
        PitchVariance = 0.09f,
    };

    private static readonly SoundStyle ButtonSound = new("sea/Assets/Sounds/JinlianButton")
    {
        Volume = 0.6f,
        PitchVariance = 0.05f,
    };

    private static readonly SoundStyle HoverSound = new("sea/Assets/Sounds/JinlianHover")
    {
        Volume = 0.45f,
        PitchVariance = 0.04f,
    };

    private static readonly SoundStyle OpenSound = new("sea/Assets/Sounds/JinlianOpen")
    {
        Volume = 0.5f,
        PitchVariance = 0.03f,
    };

    private static readonly SoundStyle CloseSound = new("sea/Assets/Sounds/JinlianClose")
    {
        Volume = 0.45f,
        PitchVariance = 0.03f,
    };

    private static readonly SoundStyle HealSound = new("sea/Assets/Sounds/JinlianHeal")
    {
        Volume = 0.55f,
        PitchVariance = 0.02f,
    };

    private static readonly SoundStyle FollowSound = new("sea/Assets/Sounds/JinlianFollow")
    {
        Volume = 0.5f,
        PitchVariance = 0.03f,
    };

    private static readonly SoundStyle UnfollowSound = new("sea/Assets/Sounds/JinlianUnfollow")
    {
        Volume = 0.5f,
        PitchVariance = 0.03f,
    };

    private Asset<Texture2D>? _boxTexture;
    private Asset<Texture2D>? _buttonTexture;
    private Asset<Texture2D>? _buttonHoverTexture;
    private Asset<Texture2D>[]? _faceTextures;

    private LegacyGameInterfaceLayer? _dialogueLayer;
    private LegacyGameInterfaceLayer? _recallLayer;
    private bool _dialogueActiveLogged;

    // 上一次绘制的选项组合，用来只记录变化
    private string _lastOptionsSignature = string.Empty;

    // 打字机进度：当前文本、已浮现的字数与上一次播放音效的步进
    private string _typewriterText = string.Empty;
    private float _typewriterProgress;
    private int _typewriterBlipStep;

    // 剩余需要静音原版聊天音效的帧数
    private int _suppressChatSoundTicks;

    // 本帧鼠标悬停的按钮序号，-1 表示没有悬停任何按钮
    private int _hoveredButtonIndex = -1;

    // 上一次已经播过悬停音的按钮序号，用来避免每帧重复播放
    private int _lastHoverSoundIndex = -1;

    // 本帧是否有面板被绘制，用来在面板关闭后复位悬停状态
    private bool _panelDrawnThisFrame;

    // 上一帧是否正在与堇涟对话，用来播放打开 / 关闭音效
    private bool _wasTalkingToJinlian;

    public override void Load()
    {
        if (Main.dedServ)
        {
            return;
        }

        _boxTexture = ModContent.Request<Texture2D>(BoxTexturePath);
        _buttonTexture = ModContent.Request<Texture2D>(ButtonTexturePath);
        _buttonHoverTexture = ModContent.Request<Texture2D>(ButtonHoverTexturePath);

        _faceTextures = new Asset<Texture2D>[FaceTextureCount];

        for (int index = 0; index < FaceTextureCount; index++)
        {
            _faceTextures[index] = ModContent.Request<Texture2D>(
                string.Format(FaceTexturePathFormat, index + 1)
            );
        }

        _dialogueLayer = new LegacyGameInterfaceLayer(
            "sea: Jinlian Dialogue",
            DrawCustomDialogueLayer,
            InterfaceScaleType.UI
        );

        _recallLayer = new LegacyGameInterfaceLayer(
            "sea: Jinlian Recall",
            DrawRecallPanel,
            InterfaceScaleType.UI
        );

        On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback += HookPlaySound;
    }

    public override void Unload()
    {
        _dialogueLayer = null;
        _recallLayer = null;
        On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback -= HookPlaySound;

        _boxTexture = null;
        _buttonTexture = null;
        _buttonHoverTexture = null;
        _faceTextures = null;
    }

    /// <summary>
    /// 把原版的 NPC / 告示牌对话层替换成自绘对话框层
    /// </summary>
    /// <param name="layers">当前这一帧的界面层列表</param>
    /// <remarks>The vanilla layer named "Vanilla: NPC / Sign Dialog" is swapped for the custom layer only while the local player is talking to Jinlian, so every other NPC and sign keeps the vanilla dialog.</remarks>
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (Main.dedServ || Main.gameMenu)
        {
            return;
        }

        if (_dialogueLayer is not null)
        {
            if (TryGetDialogueNpc(Main.LocalPlayer, out _, out _))
            {
                int index = layers.FindIndex(layer => layer.Name == "Vanilla: NPC / Sign Dialog");

                if (index >= 0)
                {
                    layers[index] = _dialogueLayer;
                }
            }
            else
            {
                _dialogueActiveLogged = false;
            }
        }

        InsertRecallLayer(layers);
    }

    /// <summary>
    /// 把召回面板层插到界面层列表里（每帧列表都会重建，所以先查重）
    /// </summary>
    /// <param name="layers">当前这一帧的界面层列表</param>
    /// <remarks>The recall layer only draws while the panel is open, but it has to exist in the layer list so the hotkey feels instant.</remarks>
    private void InsertRecallLayer(List<GameInterfaceLayer> layers)
    {
        if (_recallLayer is null || layers.Contains(_recallLayer))
        {
            return;
        }

        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

        if (index < 0)
        {
            layers.Add(_recallLayer);

            return;
        }

        layers.Insert(index, _recallLayer);
    }

    /// <summary>
    /// 自绘对话框层的绘制入口
    /// </summary>
    /// <returns>始终返回 true，表示该层已经绘制完毕</returns>
    private bool DrawCustomDialogueLayer()
    {
        Player player = Main.LocalPlayer;

        if (!TryGetDialogueNpc(player, out NPC npc, out JinlianNPC jinlian))
        {
            return true;
        }

        if (!npc.active || Vector2.DistanceSquared(player.Center, npc.Center) > MaxDialogueDistance * MaxDialogueDistance)
        {
            CloseChat();

            return true;
        }

        if (!_dialogueActiveLogged)
        {
            _dialogueActiveLogged = true;
            Mod.Logger.Info(
                $"堇涟专属对话框已接管 talkNPC={player.talkNPC} 剧情中={jinlian.IsDialogueActive} / Jinlian custom dialogue panel is active"
            );
        }

        DrawDialoguePanel(npc, jinlian, player);

        return true;
    }

    /// <summary>
    /// 绘制快捷键打开的召回面板
    /// </summary>
    /// <returns>始终返回 true，表示这一层已经画完</returns>
    private bool DrawRecallPanel()
    {
        JinlianRecallSystem recall = ModContent.GetInstance<JinlianRecallSystem>();
        _panelDrawnThisFrame = true;

        if (!recall.PanelOpen || _boxTexture is null)
        {
            return true;
        }

        Texture2D box = _boxTexture.Value;
        Texture2D button = _buttonTexture.Value;
        Texture2D buttonHover = _buttonHoverTexture.Value;

        Vector2 panelPosition = new(
            (Main.screenWidth - box.Width) * 0.5f,
            Main.screenHeight - box.Height - PanelBottomGap
        );

        Rectangle panelArea = new(
            (int)panelPosition.X,
            (int)panelPosition.Y,
            box.Width,
            box.Height
        );

        Main.spriteBatch.Draw(box, panelPosition, Color.White);

        // 死亡态不提立绘也不写名字，改成正上方一行「堇涟死亡」，按钮竖排在正中
        bool reviveMode = recall.JinlianIsDead;
        NPC npc = recall.FindJinlian();
        JinlianNPC? jinlian = npc?.ModNPC as JinlianNPC;

        if (reviveMode)
        {
            DrawDeathTitle(panelPosition, box.Width);
        }
        else
        {
            if (jinlian is not null)
            {
                DrawPortrait(JinlianNPC.DoubtFaceIndex, panelPosition);
            }

            // 还没自报家门的时候，召回面板也跟着显示「？？？」
            string displayName = jinlian is not null
                ? jinlian.GetDisplayName()
                : Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.DisplayName");

            DrawNameText(displayName, panelPosition);
        }

        // 死亡态只留两个按钮，不写那行提示文字
        float textBottom = panelPosition.Y + ButtonTopOffset;

        if (!reviveMode)
        {
            string bodyText = Language.GetTextValue("Mods.sea.NPCs.JinlianNPC." + recall.PanelTextKey);

            textBottom = DrawDialogueText(bodyText, panelPosition);
        }

        float buttonLeft = reviveMode
            ? panelPosition.X + (box.Width - button.Width) * 0.5f
            : panelPosition.X + ButtonLeft;
        float buttonY = reviveMode
            ? panelPosition.Y + ReviveButtonTop
            : MathHelper.Max(panelPosition.Y + ButtonTopOffset, textBottom + 8f);

        string[] labels = reviveMode
            ?
            [
                Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ButtonRevive"),
                Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ButtonClose")
            ]
            :
            [
                Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ButtonRecall"),
                Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ButtonClose")
            ];

        _hoveredButtonIndex = -1;

        for (int index = 0; index < labels.Length; index++)
        {
            Vector2 buttonPosition = new(
                buttonLeft,
                buttonY + index * (ButtonHeight + ButtonSpacing)
            );

            bool clicked = DrawButton(
                button,
                buttonHover,
                buttonPosition,
                labels[index],
                out bool hovered
            );

            if (hovered)
            {
                _hoveredButtonIndex = index;
            }

            if (!clicked)
            {
                continue;
            }

            SoundEngine.PlaySound(index == 0 ? ButtonSound : CloseSound);

            if (index == 0)
            {
                if (reviveMode)
                {
                    recall.TryReviveJinlian();
                }
                else
                {
                    recall.TryRecallJinlian(Main.LocalPlayer);
                }
            }
            else
            {
                recall.ClosePanel();
            }

            break;
        }

        bool mouseOverPanel = panelArea.Contains(Main.MouseScreen.ToPoint());

        if (mouseOverPanel)
        {
            Main.LocalPlayer.mouseInterface = true;
        }

        bool escapePressed = Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);
        bool rightClicked = mouseOverPanel && Main.mouseRight && Main.mouseRightRelease;

        if (escapePressed || rightClicked)
        {
            SoundEngine.PlaySound(CloseSound);
            recall.ClosePanel();
        }

        return true;
    }

    /// <summary>
    /// 在名牌上绘制一行名字文本
    /// </summary>
    /// <param name="name">要显示的名字</param>
    /// <param name="panelPosition">对话框左上角屏幕坐标</param>
    private static void DrawNameText(string name, Vector2 panelPosition)
    {
        DynamicSpriteFont font = FontAssets.MouseText.Value;
        float width = font.MeasureString(name).X * NameScale;
        Vector2 position = panelPosition
            + new Vector2(NamePlateCenterX - width * 0.5f, NamePlateCenterY - font.LineSpacing * NameScale * 0.5f);

        ChatManager.DrawColorCodedStringWithShadow(
            Main.spriteBatch,
            font,
            name,
            position,
            NameColor,
            0f,
            Vector2.Zero,
            new Vector2(NameScale)
        );
    }

    /// <summary>
    /// 在死亡态面板正上方居中画一行「堇涟死亡」。
    /// </summary>
    /// <param name="panelPosition">对话框左上角屏幕坐标</param>
    /// <param name="boxWidth">对话框宽度</param>
    private static void DrawDeathTitle(Vector2 panelPosition, int boxWidth)
    {
        DynamicSpriteFont font = FontAssets.MouseText.Value;
        string title = Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.DeathTitle");
        float scale = NameScale * 1.15f;
        float width = font.MeasureString(title).X * scale;
        Vector2 position = panelPosition
            + new Vector2((boxWidth - width) * 0.5f, DeathTitleTop - font.LineSpacing * scale * 0.5f);

        ChatManager.DrawColorCodedStringWithShadow(
            Main.spriteBatch,
            font,
            title,
            position,
            DeathTitleColor,
            0f,
            Vector2.Zero,
            new Vector2(scale)
        );
    }

    /// <summary>
    /// 找出正在与玩家对话的堇涟
    /// </summary>
    /// <param name="player">当前玩家</param>
    /// <param name="npc">对应的 NPC</param>
    /// <param name="jinlian">堇涟实例</param>
    /// <returns>是否正在与堇涟对话</returns>
    private static bool TryGetDialogueNpc(Player player, out NPC? npc, out JinlianNPC? jinlian)
    {
        npc = null;
        jinlian = null;

        if (Main.gameMenu || player.talkNPC < 0 || player.talkNPC >= Main.maxNPCs)
        {
            return false;
        }

        npc = Main.npc[player.talkNPC];

        if (!npc.active || npc.ModNPC is not JinlianNPC candidate)
        {
            npc = null;

            return false;
        }

        jinlian = candidate;

        return true;
    }

    /// <summary>
    /// 原版聊天音效的拦截钩子
    /// </summary>
    /// <param name="orig">原始播放方法</param>
    /// <param name="style">音效样式</param>
    /// <param name="position">播放位置</param>
    /// <param name="updateCallback">更新回调</param>
    /// <returns>音效槽位；被静音时返回默认值</returns>
    /// <remarks>Suppresses the vanilla chat open / close / tick sounds while the custom panel owns the dialogue.</remarks>
    private SlotId HookPlaySound(
        On_SoundEngine.orig_PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback orig,
        ref SoundStyle style,
        Vector2? position,
        SoundUpdateCallback updateCallback)
    {
        if (ShouldSuppressChatSound(in style))
        {
            return default;
        }

        return orig(ref style, position, updateCallback);
    }
    /// <summary>
    /// 这条音效是否属于原版聊天框且此刻应被静音
    /// </summary>
    /// <param name="style">音效样式</param>
    /// <returns>需要静音返回 true</returns>
    /// <remarks>The vanilla chat close sound is played after the talk NPC is cleared, so a short countdown keeps it muted too.</remarks>
    private bool ShouldSuppressChatSound(in SoundStyle style)
    {
        // 打开音效在 SetTalkNPC 之后、我们的 update 之前播放，所以要直接看当前对话目标；
        // 关闭音效在清空对话目标之后播放，只能靠这段时间窗覆盖。
        if (_suppressChatSoundTicks <= 0 && !IsTalkingToJinlianNow())
        {
            return false;
        }

        foreach (string path in SuppressedChatSoundPaths)
        {
            if (string.Equals(style.SoundPath, path, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
    /// <summary>
    /// 维护「自绘面板正在接管对话」的计时器
    /// </summary>
    /// <remarks>Refreshed while talking to Jinlian and counted down afterwards, so both the open and the close sound are covered.</remarks>
    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gameMenu)
        {
            _suppressChatSoundTicks = 0;
            _hoveredButtonIndex = -1;
            _lastHoverSoundIndex = -1;
            _panelDrawnThisFrame = false;

            return;
        }

        if (!_panelDrawnThisFrame)
        {
            _hoveredButtonIndex = -1;
            _lastHoverSoundIndex = -1;
        }

        _panelDrawnThisFrame = false;

        bool talkingNow = IsTalkingToJinlianNow();

        if (talkingNow && !_wasTalkingToJinlian)
        {
            SoundEngine.PlaySound(OpenSound);
        }
        else if (!talkingNow && _wasTalkingToJinlian)
        {
            SoundEngine.PlaySound(CloseSound);
        }

        _wasTalkingToJinlian = talkingNow;

        if (IsTalkingToJinlianNow())
        {
            _suppressChatSoundTicks = SuppressChatSoundTicks;

            return;
        }

        if (_suppressChatSoundTicks > 0)
        {
            _suppressChatSoundTicks--;
        }
    }
    /// <summary>
    /// 当前是否正在和堇涟对话
    /// </summary>
    /// <returns>正在对话返回 true</returns>
    /// <remarks>Checked before the vanilla chat sounds play; the vanilla code sets the talk target first, so this is safe.</remarks>
    private static bool IsTalkingToJinlianNow()
    {
        Player player = Main.LocalPlayer;

        if (player is null || player.talkNPC < 0 || player.talkNPC >= Main.maxNPCs)
        {
            return false;
        }

        NPC npc = Main.npc[player.talkNPC];

        return npc.active && npc.ModNPC is JinlianNPC;
    }
    /// <summary>
    /// 绘制专属对话框
    /// </summary>
    /// <param name="npc">堇涟 NPC</param>
    /// <param name="jinlian">堇涟实例</param>
    /// <param name="player">当前玩家</param>
    private void DrawDialoguePanel(NPC npc, JinlianNPC jinlian, Player player)
    {
        _panelDrawnThisFrame = true;

        Texture2D box = _boxTexture.Value;
        Texture2D button = _buttonTexture.Value;
        Texture2D buttonHover = _buttonHoverTexture.Value;

        Vector2 panelPosition = new(
            (Main.screenWidth - box.Width) * 0.5f,
            Main.screenHeight - box.Height - PanelBottomGap
        );

        Rectangle panelArea = new(
            (int)panelPosition.X,
            (int)panelPosition.Y,
            box.Width,
            box.Height
        );

        Main.spriteBatch.Draw(box, panelPosition, Color.White);

        DrawPortrait(jinlian.GetDialogueFaceIndex(), panelPosition);
        DrawName(jinlian.GetDisplayName(), panelPosition);

        float textBottom = DrawDialogueText(GetPanelText(jinlian), panelPosition);

        int giftItemType = jinlian.ActiveGiftItemType;

        if (giftItemType > 0)
        {
            DrawGiftSlot(giftItemType, panelPosition);
        }

        GetPanelOptions(
            jinlian,
            out string firstLabel,
            out string secondLabel,
            out string thirdLabel,
            out string fourthLabel
        );

        string signature =
            $"{(jinlian.IsDialogueActive ? "剧情" : "日常")}|{firstLabel}|{secondLabel}|{thirdLabel}|{fourthLabel}";

        if (!string.Equals(signature, _lastOptionsSignature, System.StringComparison.Ordinal))
        {
            _lastOptionsSignature = signature;
            Mod.Logger.Info($"堇涟面板选项 {signature} / Jinlian panel options {signature}");
        }

        float buttonY = MathHelper.Max(panelPosition.Y + ButtonTopOffset, textBottom + 8f);
        string[] labels = [firstLabel, secondLabel, thirdLabel, fourthLabel];
        int clickedIndex = -1;
        _hoveredButtonIndex = -1;

        for (int index = 0; index < labels.Length; index++)
        {
            if (string.IsNullOrEmpty(labels[index]))
            {
                continue;
            }

            Vector2 buttonPosition = new(
                panelPosition.X + ButtonLeft,
                buttonY + index * (ButtonHeight + ButtonSpacing)
            );

            bool clicked = DrawButton(
                button,
                buttonHover,
                buttonPosition,
                labels[index],
                out bool hovered
            );

            if (hovered)
            {
                _hoveredButtonIndex = index;
            }

            if (clicked && clickedIndex < 0)
            {
                clickedIndex = index;
            }
        }

        PlayHoverSoundIfChanged();

        bool mouseOverPanel = panelArea.Contains(Main.MouseScreen.ToPoint());

        if (mouseOverPanel)
        {
            Main.LocalPlayer.mouseInterface = true;
        }

        // 台词还在浮现时点面板：先补全文字，这一下不触发按钮
        if (IsTypewriterRunning() && mouseOverPanel && Main.mouseLeft && Main.mouseLeftRelease)
        {
            RevealTypewriter();
            Main.mouseLeftRelease = false;

            return;
        }

        if (clickedIndex >= 0)
        {
            ActivateOption(jinlian, clickedIndex);

            return;
        }

        bool escapePressed = Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);
        bool rightClicked = mouseOverPanel && Main.mouseRight && Main.mouseRightRelease;

        if (escapePressed || rightClicked)
        {
            CloseChat();
        }
    }

    /// <summary>
    /// 取面板上要显示的文本
    /// </summary>
    /// <param name="jinlian">堇涟实例</param>
    /// <returns>当前聊天文本</returns>
    /// <remarks>The vanilla chat text is kept in sync by GetChat, SetDialogueStep and the follow toggle, so the panel can simply reuse it.</remarks>
    private static string GetPanelText(JinlianNPC jinlian)
    {
        if (!string.IsNullOrEmpty(Main.npcChatText))
        {
            return Main.npcChatText;
        }

        return jinlian.GetActiveDialogueText();
    }

    /// <summary>
    /// 取面板上要显示的选项按钮文本
    /// </summary>
    /// <param name="jinlian">堇涟实例</param>
    /// <param name="firstLabel">第一个按钮文本</param>
    /// <param name="secondLabel">第二个按钮文本，没有时为空串</param>
    /// <param name="thirdLabel">第三个按钮文本，没有时为空串</param>
    /// <remarks>Every non empty label is drawn as its own button, from top to bottom, so a page can offer up to three answers at once.</remarks>
    private static void GetPanelOptions(
        JinlianNPC jinlian,
        out string firstLabel,
        out string secondLabel,
        out string thirdLabel,
        out string fourthLabel
    )
    {
        if (jinlian.IsDialogueActive)
        {
            jinlian.GetActiveDialogueOptions(out firstLabel, out secondLabel, out thirdLabel);
            fourthLabel = string.Empty;

            return;
        }

        firstLabel = jinlian.GetFollowButtonLabel();

        // 第一次剧情结束后，商店和跟随一起出现
        secondLabel = JinlianSpawnSystem.JinlianIntroduced
            ? Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ButtonShop")
            : string.Empty;

        // 看完第一次复活的新剧情之后才解锁「治疗」按钮
        if (JinlianSpawnSystem.JinlianFirstReviveStorySeen)
        {
            thirdLabel = Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ButtonHeal");
            fourthLabel = Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ButtonClose");

            return;
        }

        thirdLabel = Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ButtonClose");
        fourthLabel = string.Empty;
    }

    /// <summary>
    /// 处理面板按钮点击
    /// </summary>
    /// <param name="jinlian">堇涟实例</param>
    /// <param name="optionIndex">被点击的按钮序号，从上到下从 0 开始</param>
    private static void ActivateOption(JinlianNPC jinlian, int optionIndex)
    {
        if (jinlian.IsDialogueActive)
        {
            SoundEngine.PlaySound(ButtonSound);
            jinlian.ClickActiveDialogueOption(optionIndex);

            return;
        }

        if (optionIndex == 0)
        {
            SoundEngine.PlaySound(jinlian.IsFollowEnabled ? UnfollowSound : FollowSound);
            jinlian.ToggleFollow();

            return;
        }

        if (optionIndex == 1 && JinlianSpawnSystem.JinlianIntroduced)
        {
            OpenShopFromDialogue();

            return;
        }

        if (optionIndex == 2 && JinlianSpawnSystem.JinlianFirstReviveStorySeen)
        {
            SoundEngine.PlaySound(HealSound);
            jinlian.HealFromDialogue();

            return;
        }

        CloseChat();
    }

    /// <summary>
    /// 关掉对话框并打开堇涟的商店
    /// </summary>
    /// <remarks>The shop owns its own large panel, so the vanilla chat window has to be closed first.</remarks>
    private static void OpenShopFromDialogue()
    {
        Main.npcChatText = string.Empty;
        Main.CloseNPCChatOrSign();

        ModContent.GetInstance<JinlianShopSystem>().OpenShop();
    }

    /// <summary>
    /// 在对话框左下角绘制堇涟的表情差分
    /// </summary>
    /// <param name="faceIndex">表情序号（1 到 8）</param>
    /// <param name="panelPosition">对话框左上角屏幕坐标</param>
    /// <remarks>The portrait is drawn at its native size and anchored to the panel bottom so it never spills out of the frame.</remarks>
    private void DrawPortrait(int faceIndex, Vector2 panelPosition)
    {
        if (_faceTextures is null)
        {
            return;
        }

        int index = Math.Clamp(faceIndex, 1, FaceTextureCount) - 1;
        Asset<Texture2D> asset = _faceTextures[index];

        if (asset is null || !asset.IsLoaded)
        {
            return;
        }

        Texture2D portrait = asset.Value;
        Vector2 position = panelPosition
            + new Vector2(PortraitCenterX - portrait.Width * 0.5f, PortraitBottomY - portrait.Height);

        Main.spriteBatch.Draw(portrait, position, Color.White);
    }

    /// <summary>
    /// 在名牌上绘制名字
    /// </summary>
    /// <param name="name">要显示的名字（自我介绍前是「？？？」）</param>
    /// <param name="panelPosition">对话框左上角屏幕坐标</param>
    private static void DrawName(string name, Vector2 panelPosition)
    {
        DynamicSpriteFont font = FontAssets.MouseText.Value;
        float width = font.MeasureString(name).X * NameScale;
        Vector2 position = panelPosition
            + new Vector2(NamePlateCenterX - width * 0.5f, NamePlateCenterY - font.LineSpacing * NameScale * 0.5f);

        ChatManager.DrawColorCodedStringWithShadow(
            Main.spriteBatch,
            font,
            name,
            position,
            NameColor,
            0f,
            Vector2.Zero,
            new Vector2(NameScale)
        );
    }

    /// <summary>
    /// 推进打字机进度，换台词时自动从头开始
    /// </summary>
    /// <param name="text">本帧要显示的完整台词</param>
    /// <returns>当前应该显示的字数</returns>
    /// <remarks>Each new line restarts the reveal; the blip is played every few characters instead of every character so it never turns into a buzz.</remarks>
    private int UpdateTypewriter(string text)
    {
        if (!string.Equals(_typewriterText, text, StringComparison.Ordinal))
        {
            _typewriterText = text;
            _typewriterProgress = 0f;
            _typewriterBlipStep = 0;
        }

        if (_typewriterProgress < text.Length)
        {
            _typewriterProgress = MathHelper.Min(
                _typewriterProgress + TypewriterCharsPerSecond / 60f,
                text.Length
            );

            int step = (int)_typewriterProgress / TypewriterBlipEvery;

            if (step > _typewriterBlipStep)
            {
                _typewriterBlipStep = step;
                SoundEngine.PlaySound(TypewriterSound);
            }
        }

        return (int)_typewriterProgress;
    }
    /// <summary>
    /// 台词是否还在逐字浮现
    /// </summary>
    /// <returns>还在浮现返回 true</returns>
    /// <remarks>Used to let a click finish the line instantly, the same way Undertale skips text.</remarks>
    private bool IsTypewriterRunning() => _typewriterProgress < _typewriterText.Length;
    /// <summary>
    /// 直接把整段台词显示完
    /// </summary>
    private void RevealTypewriter() => _typewriterProgress = _typewriterText.Length;
    /// <summary>
    /// 绘制自动换行的台词
    /// </summary>
    /// <param name="text">台词</param>
    /// <param name="panelPosition">对话框左上角屏幕坐标</param>
    /// <returns>最后一行的底部屏幕 Y 坐标</returns>
    private float DrawDialogueText(string text, Vector2 panelPosition)
    {
        DynamicSpriteFont font = FontAssets.MouseText.Value;
        List<string> lines = WrapText(text, font, TextScale, TextWidth);
        Vector2 position = panelPosition + new Vector2(TextLeft, TextTop);
        int remaining = UpdateTypewriter(text);

        foreach (string line in lines)
        {
            if (remaining > 0)
            {
                int shown = Math.Min(remaining, line.Length);

                ChatManager.DrawColorCodedStringWithShadow(
                    Main.spriteBatch,
                    font,
                    line[..shown],
                    position,
                    TextColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(TextScale)
                );

                remaining -= shown;
            }

            position.Y += TextLineHeight;
        }

        return position.Y;
    }

    /// <summary>
    /// 按像素宽度逐字换行（中英文都适用）
    /// </summary>
    /// <param name="text">原文</param>
    /// <param name="font">字体</param>
    /// <param name="scale">缩放</param>
    /// <param name="maxWidth">行宽上限</param>
    /// <returns>分行结果</returns>
    private static List<string> WrapText(string text, DynamicSpriteFont font, float scale, float maxWidth)
    {
        List<string> lines = [];
        string[] paragraphs = text.Split('\n');

        foreach (string paragraph in paragraphs)
        {
            string current = string.Empty;

            foreach (char character in paragraph)
            {
                string candidate = current + character;

                if (current.Length > 0 && font.MeasureString(candidate).X * scale > maxWidth)
                {
                    lines.Add(current);
                    current = character.ToString();
                }
                else
                {
                    current = candidate;
                }
            }

            lines.Add(current);
        }

        return lines;
    }

    /// <summary>
    /// 绘制赠礼用的小物品框
    /// </summary>
    /// <param name="itemType">要展示的物品类型</param>
    /// <param name="panelPosition">对话框左上角屏幕坐标</param>
    /// <remarks>The frame reuses the vanilla inventory slot texture so the reward reads as an item pickup instead of decoration.</remarks>
    private void DrawGiftSlot(int itemType, Vector2 panelPosition)
    {
        Texture2D slot = TextureAssets.InventoryBack9.Value;
        Vector2 slotPosition = panelPosition + new Vector2(GiftSlotLeft, GiftSlotTop);

        Main.spriteBatch.Draw(slot, slotPosition, Color.White);

        Main.instance.LoadItem(itemType);

        Texture2D itemTexture = TextureAssets.Item[itemType].Value;
        Rectangle frame = Main.itemAnimations[itemType] is null
            ? itemTexture.Frame()
            : Main.itemAnimations[itemType].GetFrame(itemTexture);

        float longestSide = MathHelper.Max(frame.Width, frame.Height);
        float scale = MathHelper.Min(1f, GiftIconLimit / longestSide);
        Vector2 itemPosition = slotPosition + slot.Size() * 0.5f - frame.Size() * scale * 0.5f;

        Main.spriteBatch.Draw(
            itemTexture,
            itemPosition,
            frame,
            Color.White,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f
        );

        string caption = Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.GiftCaption");
        DynamicSpriteFont font = FontAssets.MouseText.Value;
        float captionWidth = font.MeasureString(caption).X * ButtonLabelScale;
        Vector2 captionPosition = new(
            slotPosition.X + (slot.Width - captionWidth) * 0.5f,
            slotPosition.Y + slot.Height + GiftCaptionGap
        );

        ChatManager.DrawColorCodedStringWithShadow(
            Main.spriteBatch,
            font,
            caption,
            captionPosition,
            ButtonTextColor,
            0f,
            Vector2.Zero,
            new Vector2(ButtonLabelScale)
        );
    }

    /// <summary>
    /// 悬停目标变化时播放一次悬停音效
    /// </summary>
    /// <remarks>Plays the soft tick only when the pointer enters another button, so hovering costs one sound instead of one per frame.</remarks>
    private void PlayHoverSoundIfChanged()
    {
        if (_hoveredButtonIndex == _lastHoverSoundIndex)
        {
            return;
        }

        _lastHoverSoundIndex = _hoveredButtonIndex;

        if (_hoveredButtonIndex >= 0)
        {
            SoundEngine.PlaySound(HoverSound);
        }
    }

    /// <summary>
    /// 绘制一个选项按钮并处理点击
    /// </summary>
    /// <param name="button">常态贴图</param>
    /// <param name="buttonHover">悬停贴图</param>
    /// <param name="position">按钮左上角屏幕坐标</param>
    /// <param name="label">按钮文本</param>
    /// <returns>本帧是否被点击</returns>
    private static bool DrawButton(
        Texture2D button,
        Texture2D buttonHover,
        Vector2 position,
        string label,
        out bool hovered
    )
    {
        Rectangle area = new((int)position.X, (int)position.Y, button.Width, button.Height);
        hovered = area.Contains(Main.MouseScreen.ToPoint());
        bool clicked = hovered && Main.mouseLeft && Main.mouseLeftRelease;

        Main.spriteBatch.Draw(hovered ? buttonHover : button, position, Color.White);

        DynamicSpriteFont font = FontAssets.MouseText.Value;
        float width = font.MeasureString(label).X * ButtonLabelScale;
        Vector2 labelPosition = new(
            position.X + (button.Width - width) * 0.5f,
            position.Y + (button.Height - font.LineSpacing * ButtonLabelScale) * 0.5f
        );

        ChatManager.DrawColorCodedStringWithShadow(
            Main.spriteBatch,
            font,
            label,
            labelPosition,
            hovered ? NameColor : ButtonTextColor,
            0f,
            Vector2.Zero,
            new Vector2(ButtonLabelScale)
        );

        if (clicked)
        {
            Main.mouseLeftRelease = false;
        }

        return clicked;
    }

    /// <summary>
    /// 关闭聊天框
    /// </summary>
    private static void CloseChat()
    {
        SoundEngine.PlaySound(CloseSound);
        Main.npcChatText = string.Empty;
        Main.CloseNPCChatOrSign();
    }
}
