// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-03

using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using ReLogic.Content;
using ReLogic.Graphics;

using sea.Content.DustSea.Items;

using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace sea.Content.DustSea.Systems;

/// <summary>
/// 堇涟的商店：分类标签 + 逐条罗列的商品，以及购买逻辑。
/// </summary>
/// <remarks>The panel is drawn as a legacy interface layer so it can sit above the world HUD without the vanilla NPC chat window.</remarks>
public class JinlianShopSystem : ModSystem
{
    /// <summary>
    /// 商品分类：酒类。
    /// </summary>
    public const int CategoryAlcohol = 0;

    /// <summary>
    /// 分类总数。
    /// </summary>
    public const int CategoryCount = 1;

    private const string BoxTexturePath = "sea/Assets/UI/JinlianShopBox";
    private const string ButtonTexturePath = "sea/Assets/UI/JinlianDialogueButton";
    private const string ButtonHoverTexturePath = "sea/Assets/UI/JinlianDialogueButtonHover";

    private const float PanelBottomGap = 96f;
    private const float PanelTitleLeft = 26f;
    private const float PanelTitleTop = 18f;
    private const float TabLeft = 26f;
    private const float TabTop = 54f;
    private const float TabWidth = 118f;
    private const float TabHeight = 30f;
    private const float TabSpacing = 8f;
    private const float RowLeft = 22f;
    private const float RowTop = 100f;
    private const float RowWidth = 596f;
    private const float RowHeight = 68f;
    private const float RowSpacing = 8f;
    private const float RowIconLeft = 18f;
    private const float RowIconSize = 32f;
    private const float RowTextLeft = 64f;
    private const float RowNameTop = 10f;
    private const float RowDescriptionTop = 30f;
    private const float RowPriceTop = 44f;
    private const float CloseButtonLeft = 486f;
    private const float CloseButtonTop = 372f;
    private const float CloseButtonWidth = 126f;
    private const float CloseButtonHeight = 30f;
    private const float NoticeTop = 344f;
    private const int NoticeDurationTicks = 180;
    private const int DescriptionLimit = 20;
    private const float TitleScale = 1.05f;
    private const float TabScale = 0.82f;
    private const float NameScale = 0.95f;
    private const float DescriptionScale = 0.72f;
    private const float PriceScale = 0.82f;
    private const float NoticeScale = 0.82f;

    private static readonly Color PanelTitleColor = new(216, 190, 255);
    private static readonly Color NameColor = new(238, 232, 250);
    private static readonly Color DescriptionColor = new(176, 166, 200);
    private static readonly Color PriceColor = new(246, 214, 132);
    private static readonly Color NoticeColor = new(168, 236, 176);
    private static readonly Color TabIdleText = new(196, 184, 224);
    private static readonly Color TabFill = new(38, 27, 62);
    private static readonly Color TabFillHover = new(64, 46, 100);
    private static readonly Color TabFillActive = new(88, 62, 138);
    private static readonly Color TabBorder = new(140, 106, 202);
    private static readonly Color RowFill = new(34, 24, 56, 210);
    private static readonly Color RowFillHover = new(58, 42, 92, 220);
    private static readonly Color RowBorder = new(96, 74, 142);
    private static readonly SoundStyle PanelOpenSound = new("sea/Assets/Sounds/JinlianOpen")
    {
        Volume = 0.62f,
        PitchVariance = 0.04f,
    };
    private static readonly SoundStyle PanelCloseSound = new("sea/Assets/Sounds/JinlianClose")
    {
        Volume = 0.55f,
        PitchVariance = 0.04f,
    };
    private static readonly SoundStyle ButtonClickSound = new("sea/Assets/Sounds/JinlianButton")
    {
        Volume = 0.5f,
        PitchVariance = 0.05f,
    };
    private static readonly SoundStyle HoverSound = new("sea/Assets/Sounds/JinlianHover")
    {
        Volume = 0.45f,
        PitchVariance = 0.04f,
    };

    private Asset<Texture2D>? _boxTexture;
    private Asset<Texture2D>? _buttonTexture;
    private Asset<Texture2D>? _buttonHoverTexture;
    private LegacyGameInterfaceLayer? _shopLayer;

    /// <summary>
    /// 商店面板是否处于打开状态。
    /// </summary>
    /// <remarks>Read by the interface layer and by the NPC so the dialogue panel can hand over control.</remarks>
    public bool ShopOpen { get; private set; }

    /// <summary>
    /// 当前选中的分类。
    /// </summary>
    public int ActiveCategory { get; private set; } = CategoryAlcohol;

    private string _noticeText = string.Empty;
    private int _noticeTimer;
    private int _hoveredRow = -1;
    private int _hoveredTab = -1;
    private string _hoverKey = string.Empty;
    private string _pendingHoverKey = string.Empty;

    public override void Load()
    {
        _boxTexture = ModContent.Request<Texture2D>(BoxTexturePath);
        _buttonTexture = ModContent.Request<Texture2D>(ButtonTexturePath);
        _buttonHoverTexture = ModContent.Request<Texture2D>(ButtonHoverTexturePath);
        _shopLayer = new LegacyGameInterfaceLayer("sea: Jinlian Shop", DrawShopLayer, InterfaceScaleType.UI);
    }

    public override void Unload()
    {
        _boxTexture = null;
        _buttonTexture = null;
        _buttonHoverTexture = null;
        _shopLayer = null;
    }

    public override void PostUpdateEverything()
    {
        if (!ShopOpen)
        {
            return;
        }

        if (Main.gameMenu || Main.dedServ || Main.LocalPlayer.dead)
        {
            CloseShop();

            return;
        }

        if (_noticeTimer > 0)
        {
            _noticeTimer--;
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (_shopLayer is null || Main.dedServ || Main.gameMenu)
        {
            return;
        }

        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

        if (index < 0)
        {
            layers.Add(_shopLayer);

            return;
        }

        layers.Insert(index, _shopLayer);
    }

    /// <summary>
    /// 打开商店面板。
    /// </summary>
    /// <param name="category">初始分类</param>
    public void OpenShop(int category = CategoryAlcohol)
    {
        ActiveCategory = category;
        ShopOpen = true;
        _noticeText = string.Empty;
        _noticeTimer = 0;
        _hoveredRow = -1;
        _hoveredTab = -1;
        _hoverKey = string.Empty;
        _pendingHoverKey = string.Empty;

        SoundEngine.PlaySound(PanelOpenSound);
    }

    /// <summary>
    /// 关闭商店面板。
    /// </summary>
    public void CloseShop()
    {
        if (!ShopOpen)
        {
            return;
        }

        ShopOpen = false;
        _hoveredRow = -1;
        _hoveredTab = -1;
        _hoverKey = string.Empty;
        _pendingHoverKey = string.Empty;

        SoundEngine.PlaySound(PanelCloseSound);
    }

    /// <summary>
    /// 切换分类。
    /// </summary>
    /// <param name="category">目标分类</param>
    public void SetCategory(int category)
    {
        if (category < 0 || category >= CategoryCount || category == ActiveCategory)
        {
            return;
        }

        ActiveCategory = category;
        _hoveredRow = -1;

        SoundEngine.PlaySound(ButtonClickSound);
    }

    /// <summary>
    /// 当前分类下的商品数量。
    /// </summary>
    public int EntryCount => ActiveCategory == CategoryAlcohol ? 1 : 0;

    /// <summary>
    /// 取某个商品对应的物品类型。
    /// </summary>
    /// <param name="index">商品序号</param>
    public int GetEntryItemType(int index) => index == 0 ? ModContent.ItemType<GrayTrailItem>() : 0;

    /// <summary>
    /// 取某个商品的价格（铜币计）。
    /// </summary>
    /// <param name="index">商品序号</param>
    public int GetEntryPrice(int index) => index == 0 ? GrayTrailItem.ShopPrice : 0;

    /// <summary>
    /// 尝试购买某个商品。
    /// </summary>
    /// <param name="index">商品序号</param>
    /// <returns>购买成功返回 true</returns>
    public bool TryBuy(int index)
    {
        if (!ShopOpen || index < 0 || index >= EntryCount)
        {
            return false;
        }

        Player player = Main.LocalPlayer;
        int price = GetEntryPrice(index);

        if (!player.BuyItem(price))
        {
            SetNotice(Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ShopNotEnoughMoney"));
            SoundEngine.PlaySound(ButtonClickSound);

            return false;
        }

        player.QuickSpawnItem(player.GetSource_Misc("JinlianShop"), GetEntryItemType(index), 1);
        SetNotice(Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ShopBought"));
        SoundEngine.PlaySound(SoundID.Coins);

        return true;
    }

    private void SetNotice(string text)
    {
        _noticeText = text;
        _noticeTimer = NoticeDurationTicks;
    }

    private bool DrawShopLayer()
    {
        if (!ShopOpen || _boxTexture is null || !_boxTexture.IsLoaded)
        {
            return true;
        }

        DrawShopPanel();

        return true;
    }

    private void DrawShopPanel()
    {
        Texture2D box = _boxTexture.Value;
        SpriteBatch spriteBatch = Main.spriteBatch;

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

        spriteBatch.Draw(box, panelPosition, Color.White);

        _pendingHoverKey = string.Empty;

        DrawText(
            Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ShopTitle"),
            panelPosition + new Vector2(PanelTitleLeft, PanelTitleTop),
            PanelTitleColor,
            TitleScale
        );

        DrawTabs(panelPosition);
        DrawRows(panelPosition);

        if (_noticeTimer > 0 && !string.IsNullOrEmpty(_noticeText))
        {
            DrawText(
                _noticeText,
                panelPosition + new Vector2(RowLeft, NoticeTop),
                NoticeColor,
                NoticeScale
            );
        }

        bool closeClicked = DrawButton(
            panelPosition + new Vector2(CloseButtonLeft, CloseButtonTop),
            CloseButtonWidth,
            CloseButtonHeight,
            Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ButtonClose")
        );

        PlayHoverSoundIfChanged();

        bool mouseOverPanel = panelArea.Contains(Main.MouseScreen.ToPoint());

        if (mouseOverPanel)
        {
            Main.LocalPlayer.mouseInterface = true;
        }

        bool escapePressed = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)
            && !Main.oldKeyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape);
        bool rightClicked = mouseOverPanel && Main.mouseRight && Main.mouseRightRelease;

        if (closeClicked || escapePressed || rightClicked)
        {
            CloseShop();
        }
    }

    /// <summary>
    /// 鼠标移到新元素上时才响一次悬停音，避免停在同一处反复播放。
    /// </summary>
    private void PlayHoverSoundIfChanged()
    {
        if (_pendingHoverKey == _hoverKey)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_pendingHoverKey))
        {
            SoundEngine.PlaySound(HoverSound);
        }

        _hoverKey = _pendingHoverKey;
    }

    private void DrawTabs(Vector2 panelPosition)
    {
        _hoveredTab = -1;

        for (int index = 0; index < CategoryCount; index++)
        {
            Vector2 tabPosition = panelPosition + new Vector2(TabLeft + index * (TabWidth + TabSpacing), TabTop);
            Rectangle tabArea = new((int)tabPosition.X, (int)tabPosition.Y, (int)TabWidth, (int)TabHeight);
            bool hovered = tabArea.Contains(Main.MouseScreen.ToPoint());

            if (hovered)
            {
                _hoveredTab = index;
                _pendingHoverKey = $"tab:{index}";
            }

            Color fill = index == ActiveCategory ? TabFillActive : (hovered ? TabFillHover : TabFill);

            DrawSolidRect(tabArea, fill);
            DrawRectOutline(tabArea, TabBorder);

            string label = index == CategoryAlcohol
                ? Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ShopCategoryAlcohol")
                : string.Empty;

            Vector2 labelSize = MeasureText(label, TabScale);
            DrawText(
                label,
                tabPosition + new Vector2((TabWidth - labelSize.X) * 0.5f, (TabHeight - labelSize.Y) * 0.5f),
                index == ActiveCategory ? NameColor : TabIdleText,
                TabScale
            );

            if (hovered && Main.mouseLeft && Main.mouseLeftRelease)
            {
                Main.mouseLeftRelease = false;
                SetCategory(index);
            }
        }
    }

    private void DrawRows(Vector2 panelPosition)
    {
        _hoveredRow = -1;

        int count = EntryCount;

        for (int index = 0; index < count; index++)
        {
            Vector2 rowPosition = panelPosition + new Vector2(RowLeft, RowTop + index * (RowHeight + RowSpacing));
            Rectangle rowArea = new((int)rowPosition.X, (int)rowPosition.Y, (int)RowWidth, (int)RowHeight);
            bool hovered = rowArea.Contains(Main.MouseScreen.ToPoint());

            if (hovered)
            {
                _hoveredRow = index;
                _pendingHoverKey = $"row:{index}";
            }

            DrawSolidRect(rowArea, hovered ? RowFillHover : RowFill);
            DrawRectOutline(rowArea, RowBorder);

            int itemType = GetEntryItemType(index);
            Main.instance.LoadItem(itemType);

            Texture2D icon = TextureAssets.Item[itemType].Value;
            Vector2 iconPosition = rowPosition + new Vector2(RowIconLeft, (RowHeight - RowIconSize) * 0.5f);

            Main.spriteBatch.Draw(
                icon,
                new Rectangle((int)iconPosition.X, (int)iconPosition.Y, (int)RowIconSize, (int)RowIconSize),
                Color.White
            );

            string name = Lang.GetItemNameValue(itemType);
            DrawText(name, rowPosition + new Vector2(RowTextLeft, RowNameTop), NameColor, NameScale);

            string description = GetShortDescription(itemType);
            DrawText(
                description,
                rowPosition + new Vector2(RowTextLeft, RowDescriptionTop),
                DescriptionColor,
                DescriptionScale
            );

            string price = FormatPrice(GetEntryPrice(index));
            Vector2 priceSize = MeasureText(price, PriceScale);
            DrawText(
                price,
                rowPosition + new Vector2(RowWidth - priceSize.X - 16f, RowPriceTop),
                PriceColor,
                PriceScale
            );

            if (hovered && Main.mouseLeft && Main.mouseLeftRelease)
            {
                Main.mouseLeftRelease = false;
                TryBuy(index);
            }
        }
    }

    /// <summary>
    /// 取商品的简介，超过 20 个字时截断并补省略号。
    /// </summary>
    /// <param name="itemType">物品类型</param>
    private static string GetShortDescription(int itemType)
    {
        ModItem modItem = ItemLoader.GetItem(itemType);

        if (modItem is null)
        {
            return string.Empty;
        }

        string raw = Language.GetTextValue($"Mods.sea.Items.{modItem.Name}.Tooltip");

        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        string firstLine = raw.Split('\n')[0];
        int count = 0;
        int cut = firstLine.Length;

        for (int index = 0; index < firstLine.Length; index++)
        {
            count++;

            if (count > DescriptionLimit)
            {
                cut = index;

                break;
            }
        }

        return cut >= firstLine.Length ? firstLine : firstLine[..cut] + "...";
    }

    /// <summary>
    /// 把铜币价格格式化成「x 金 y 银 z 铜」。
    /// </summary>
    /// <param name="copper">铜币总额</param>
    private static string FormatPrice(int copper)
    {
        int gold = copper / 10000;
        int silver = copper % 10000 / 100;
        int copperLeft = copper % 100;

        List<string> parts = [];

        if (gold > 0)
        {
            parts.Add($"{gold} {Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ShopUnitGold")}");
        }

        if (silver > 0)
        {
            parts.Add($"{silver} {Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ShopUnitSilver")}");
        }

        if (copperLeft > 0 || parts.Count == 0)
        {
            parts.Add($"{copperLeft} {Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.ShopUnitCopper")}");
        }

        return string.Join(" ", parts);
    }

    private bool DrawButton(Vector2 position, float width, float height, string label)
    {
        Texture2D button = _buttonTexture.Value;
        Texture2D hover = _buttonHoverTexture.Value;
        Rectangle area = new((int)position.X, (int)position.Y, (int)width, (int)height);
        bool hovered = area.Contains(Main.MouseScreen.ToPoint());
        bool clicked = hovered && Main.mouseLeft && Main.mouseLeftRelease;

        Main.spriteBatch.Draw(
            hovered ? hover : button,
            new Rectangle((int)position.X, (int)position.Y, (int)width, (int)height),
            Color.White
        );

        Vector2 labelSize = MeasureText(label, TabScale);
        DrawText(
            label,
            position + new Vector2((width - labelSize.X) * 0.5f, (height - labelSize.Y) * 0.5f),
            NameColor,
            TabScale
        );

        if (clicked)
        {
            Main.mouseLeftRelease = false;
        }

        if (hovered)
        {
            _pendingHoverKey = "button";
        }

        return clicked;
    }

    private static void DrawSolidRect(Rectangle area, Color color) => Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, color);

    private static void DrawRectOutline(Rectangle area, Color color)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Main.spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y, area.Width, 1), color);
        Main.spriteBatch.Draw(pixel, new Rectangle(area.X, area.Bottom - 1, area.Width, 1), color);
        Main.spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y, 1, area.Height), color);
        Main.spriteBatch.Draw(pixel, new Rectangle(area.Right - 1, area.Y, 1, area.Height), color);
    }

    private static Vector2 MeasureText(string text, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Vector2.Zero;
        }

        DynamicSpriteFont font = FontAssets.MouseText.Value;

        return font.MeasureString(text) * scale;
    }

    private static void DrawText(string text, Vector2 position, Color color, float scale)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        DynamicSpriteFont font = FontAssets.MouseText.Value;

        ChatManager.DrawColorCodedStringWithShadow(
            Main.spriteBatch,
            font,
            text,
            position,
            color,
            0f,
            Vector2.Zero,
            new Vector2(scale)
        );
    }
}
