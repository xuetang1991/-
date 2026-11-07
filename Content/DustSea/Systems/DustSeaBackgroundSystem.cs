// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-31

using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using ReLogic.Content;

using SubworldLibrary;

using Terraria;
using Terraria.ModLoader;

using FarSeaSubworld = sea.Content.FarSea.Subworlds.FarSea;

namespace sea.Content.DustSea.Systems;

/// <summary>
/// 浊海环境背景 - 身处浊海时用专用背景替换原版环境背景，并带视差滚动与淡入淡出过渡
/// </summary>
/// <remarks>Only the Dust Sea biome is affected. The background only moves with the player through a parallax factor, is mirrored for seamless horizontal looping and cross-fades with the vanilla background so entering and leaving the biome looks smooth. Three paint variants (day, dusk and night) are blended by the current time of day.</remarks>
public class DustSeaBackgroundSystem : ModSystem
{
    private const int VerticalSlack = 260;
    private const float Brightness = 1f;

    private const float ParallaxX = 0.2f;
    private const float ParallaxY = 0.03f;

    private const float MaxParallaxOffsetY = 100f;

    private const float FadeSpeed = 0.02f;

    // 时间轴按游戏内小时算（04:30 天亮、19:30 天黑，3600 tick = 1 小时）
    // 00:00 最黑 → 05:00 起转向微黑 → 07:00 最亮 → 17:00 起转回微黑 → 19:30 最黑
    private const float NightEndHour = 5f;
    private const float DawnToDayEndHour = 7f;
    private const float DayToDuskStartHour = 17f;
    private const float DuskToNightEndHour = 19.5f;
    private const float TicksPerHour = 3600f;
    // 显示权重每秒最多移动的分量：时间被直接改（/time、日夜跳变）时也用小段过渡而不是硬切
    private const float WeightDisplaySpeed = 0.012f;

    private Asset<Texture2D>? _dayTexture;
    private Asset<Texture2D>? _duskTexture;
    private Asset<Texture2D>? _nightTexture;

    private float _fade;

    private float _displayDayWeight;
    private float _displayDuskWeight;
    private float _displayNightWeight;

    private int _logTimer;

    public override void Load()
    {
        if (Main.dedServ)
        {
            return;
        }

        _dayTexture = ModContent.Request<Texture2D>(
            "sea/Assets/Textures/DustSeaBackground"
        );
        _duskTexture = ModContent.Request<Texture2D>(
            "sea/Assets/Textures/DustSeaBackgroundDusk"
        );
        _nightTexture = ModContent.Request<Texture2D>(
            "sea/Assets/Textures/DustSeaBackgroundNight"
        );

        On_Main.DrawSurfaceBG += HookDrawSurfaceBG;
    }

    public override void Unload()
    {
        if (!Main.dedServ)
        {
            On_Main.DrawSurfaceBG -= HookDrawSurfaceBG;
        }

        _dayTexture = null;
        _duskTexture = null;
        _nightTexture = null;
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gameMenu)
        {
            return;
        }

        // 淡入淡出：与原版背景交叉过渡，进出浊海不会瞬间切换
        float target = IsDustSeaActive() ? 1f : 0f;

        if (_fade < target)
        {
            _fade = MathHelper.Clamp(_fade + FadeSpeed, 0f, target);
        }
        else if (_fade > target)
        {
            _fade = MathHelper.Clamp(_fade - FadeSpeed, target, 1f);
        }

        GetTimeWeights(out float dayWeight, out float duskWeight, out float nightWeight);

        // 显示权重缓动：正常时间流逝时几乎零延迟，时间被直接改写时会补出一段可见的过渡
        _displayDayWeight = MoveTowards(_displayDayWeight, dayWeight, WeightDisplaySpeed);
        _displayDuskWeight = MoveTowards(_displayDuskWeight, duskWeight, WeightDisplaySpeed);
        _displayNightWeight = MoveTowards(_displayNightWeight, nightWeight, WeightDisplaySpeed);

        LogTimeWeights(dayWeight, duskWeight, nightWeight);
    }

    /// <summary>
    /// 取当前用于绘制的权重（把缓动后的三个分量归一化）
    /// </summary>
    /// <param name="dayWeight">白天权重</param>
    /// <param name="duskWeight">傍晚权重</param>
    /// <param name="nightWeight">夜晚权重</param>
    /// <remarks>Falls back to the exact time based weights during the very first frames before the eased values have been initialised.</remarks>
    private void GetDisplayWeights(
        out float dayWeight,
        out float duskWeight,
        out float nightWeight
    )
    {
        float total = _displayDayWeight + _displayDuskWeight + _displayNightWeight;

        if (total <= 0.0001f)
        {
            GetTimeWeights(out dayWeight, out duskWeight, out nightWeight);
            return;
        }

        dayWeight = _displayDayWeight / total;
        duskWeight = _displayDuskWeight / total;
        nightWeight = _displayNightWeight / total;
    }

    /// <summary>
    /// 临时诊断：在浊海内每 10 秒输出一次背景时段权重，确认时段真的在切换
    /// </summary>
    /// <remarks>Diagnostic only: reports the background time weights every ten seconds while the player is inside the biome.</remarks>
    private void LogTimeWeights(float dayWeight, float duskWeight, float nightWeight)
    {
        _logTimer++;

        if (_logTimer < 600 || _fade <= 0f)
        {
            return;
        }

        _logTimer = 0;

        Mod.Logger.Info(
            $"浊海背景权重 时间={GetCurrentHour():0.0}时 白天={dayWeight:0.00} 傍晚={duskWeight:0.00} 夜晚={nightWeight:0.00} / Dust Sea background weights hour={GetCurrentHour():0.0} day={dayWeight:0.00} dusk={duskWeight:0.00} night={nightWeight:0.00}"
        );
    }

    private void HookDrawSurfaceBG(
        On_Main.orig_DrawSurfaceBG orig,
        Main self
    )
    {
        if (_fade <= 0f || !IsAnyTextureLoaded())
        {
            // 不在浊海（或正在完全淡出）时保持原版环境背景
            orig(self);
            return;
        }

        if (_fade < 1f)
        {
            // 过渡中先画原版背景，再把浊海背景叠上去淡入 / 淡出
            orig(self);
        }

        GetDisplayWeights(out float dayWeight, out float duskWeight, out float nightWeight);

        DrawTimeBlendedBackground(dayWeight, duskWeight, nightWeight);
    }

    /// <summary>
    /// 按权重把三张时段贴图合成一次真正的线性交叉淡化
    /// </summary>
    /// <param name="dayWeight">白天权重</param>
    /// <param name="duskWeight">傍晚权重</param>
    /// <param name="nightWeight">夜晚权重</param>
    /// <remarks>SpriteBatch draws with source-over blending, so layers are stacked with corrected alphas: the first layer stays opaque and the following ones use the ratio of their own weight over the accumulated weight. This keeps the result exactly equal to a weighted average of the three images instead of the quadratic falloff a naive stack would produce.</remarks>
    private void DrawTimeBlendedBackground(
        float dayWeight,
        float duskWeight,
        float nightWeight
    )
    {
        // 叠放顺序：白天（底层，不透明）→ 傍晚 → 夜晚
        float dayAlpha = _fade;
        float dayDuskTotal = dayWeight + duskWeight;
        float duskAlpha = dayDuskTotal > 0.0001f ? _fade * duskWeight / dayDuskTotal : 0f;
        float nightAlpha = _fade * nightWeight;

        if (dayWeight > 0f || duskWeight > 0f)
        {
            DrawDustSeaBackground(_dayTexture, dayAlpha);
        }

        if (duskWeight > 0f)
        {
            DrawDustSeaBackground(_duskTexture, duskAlpha);
        }

        if (nightWeight > 0f)
        {
            DrawDustSeaBackground(_nightTexture, nightAlpha);
        }
    }

    private static bool IsDustSeaActive()
    {
        if (Main.dedServ)
        {
            return false;
        }

        // 远海子世界有自己的背景，这里不接管
        if (SubworldSystem.IsActive<FarSeaSubworld>())
        {
            return false;
        }

        return Main.LocalPlayer.InModBiome<DustSeaBiome>();
    }

    private bool IsAnyTextureLoaded()
    {
        if (
            _dayTexture == null
            || _duskTexture == null
            || _nightTexture == null
        )
        {
            return false;
        }

        return _dayTexture.IsLoaded && _duskTexture.IsLoaded && _nightTexture.IsLoaded;
    }

    /// <summary>
    /// 按当前游戏内时间算出白天 / 傍晚 / 夜晚三种画风的权重
    /// </summary>
    /// <param name="dayWeight">白天权重</param>
    /// <param name="duskWeight">傍晚权重</param>
    /// <param name="nightWeight">夜晚权重</param>
    /// <remarks>Weights always sum to one so the three variants can be layered without changing the overall brightness.</remarks>
    private static void GetTimeWeights(
        out float dayWeight,
        out float duskWeight,
        out float nightWeight
    )
    {
        float hour = GetCurrentHour();

        dayWeight = 0f;
        duskWeight = 0f;
        nightWeight = 1f;

        // 凌晨到清晨五点保持最黑，避免别处天还没亮浊海先亮起来
        if (hour < NightEndHour)
        {
            return;
        }

        // 清晨：最黑 → 微黑 → 最亮
        if (hour < DawnToDayEndHour)
        {
            float progress = SmoothStep(InverseLerp(NightEndHour, DawnToDayEndHour, hour));

            if (progress < 0.5f)
            {
                nightWeight = 1f - progress * 2f;
                duskWeight = progress * 2f;
                return;
            }

            duskWeight = 2f - progress * 2f;
            dayWeight = progress * 2f - 1f;
            return;
        }

        // 白天正午前后保持最亮
        if (hour < DayToDuskStartHour)
        {
            dayWeight = 1f;
            duskWeight = 0f;
            nightWeight = 0f;
            return;
        }

        // 黄昏：最亮 → 微黑 → 最黑
        if (hour < DuskToNightEndHour)
        {
            float progress = SmoothStep(InverseLerp(DayToDuskStartHour, DuskToNightEndHour, hour));

            if (progress < 0.5f)
            {
                dayWeight = 1f - progress * 2f;
                duskWeight = progress * 2f;
                return;
            }

            duskWeight = 2f - progress * 2f;
            nightWeight = progress * 2f - 1f;
            return;
        }

        // 入夜之后到午夜维持最黑
        nightWeight = 1f;
    }

    /// <summary>
    /// 取当前游戏内小时（04:30 天亮、19:30 天黑，一天二十四小时对应 3600 tick 每小时）
    /// </summary>
    /// <returns>0 到 24 之间的小时数</returns>
    /// <remarks>Terraria counts day time from 04:30 and night time from 19:30, so both branches are converted to a normalised hour of day.</remarks>
    private static float GetCurrentHour()
    {
        float hoursIntoPhase = (float)(Main.time / TicksPerHour);

        if (Main.dayTime)
        {
            return 4.5f + hoursIntoPhase;
        }

        return (19.5f + hoursIntoPhase) % 24f;
    }

    private static float InverseLerp(float from, float to, float value)
    {
        if (Math.Abs(to - from) < 0.0001f)
        {
            return value >= to ? 1f : 0f;
        }

        return MathHelper.Clamp((value - from) / (to - from), 0f, 1f);
    }

    /// <summary>
    /// 平滑插值，让过渡两端的变化率趋近于零，避免看起来像硬切换
    /// </summary>
    /// <param name="value">0 到 1 之间的进度</param>
    /// <returns>缓入缓出后的进度</returns>
    /// <remarks>Standard smoothstep curve, used so the cross-fade eases in and out instead of ramping linearly.</remarks>
    private static float SmoothStep(float value) => value * value * (3f - 2f * value);

    /// <summary>
    /// 按固定步长接近目标值
    /// </summary>
    /// <param name="current">当前值</param>
    /// <param name="target">目标值</param>
    /// <param name="maxDelta">每次最多移动的步长</param>
    /// <returns>接近一步之后的值</returns>
    /// <remarks>Used to ease the displayed weights so a sudden time jump still fades in over a short, visible interval.</remarks>
    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + Math.Sign(target - current) * maxDelta;
    }

    private static void DrawDustSeaBackground(Asset<Texture2D> asset, float alpha)
    {
        if (asset == null || !asset.IsLoaded || alpha <= 0f)
        {
            return;
        }

        Texture2D texture = asset.Value;
        int screenW = Main.screenWidth;
        int screenH = Main.screenHeight;

        if (
            texture.Width <= 0
            || texture.Height <= 0
            || screenW <= 0
            || screenH <= 0
        )
        {
            return;
        }

        // 背景绘制处于背景矩阵坐标系，用逆矩阵把屏幕矩形换算回去
        Matrix inverse = Matrix.Invert(
            Main.BackgroundViewMatrix.TransformationMatrix
        );

        Vector2 topLeft = Vector2.Transform(
            new Vector2(0f, -VerticalSlack / 2f),
            inverse
        );
        Vector2 bottomRight = Vector2.Transform(
            new Vector2(screenW, screenH + VerticalSlack / 2f),
            inverse
        );

        int areaWidth = (int)(bottomRight.X - topLeft.X);
        int areaHeight = (int)(bottomRight.Y - topLeft.Y);

        if (areaWidth <= 0 || areaHeight <= 0)
        {
            return;
        }

        // 等比放大到至少覆盖整个屏幕，避免出现空边
        float scale = MathHelper.Max(
            (float)areaWidth / texture.Width,
            (float)areaHeight / texture.Height
        );

        int drawWidth = (int)(texture.Width * scale);
        int drawHeight = (int)(texture.Height * scale);

        if (drawWidth <= 0 || drawHeight <= 0)
        {
            return;
        }

        // 视差：背景不自己动，只随玩家移动反向滚动（玩家往左走、背景往右走）；
        // 纵向偏移做上限钳制，避免露出边缘
        float parallaxX = -(Main.screenPosition.X * ParallaxX);
        float parallaxY = MathHelper.Clamp(
            -(Main.screenPosition.Y * ParallaxY),
            -MaxParallaxOffsetY,
            MaxParallaxOffsetY
        );

        int drawY = (int)(topLeft.Y + parallaxY);

        // 镜像平铺：相邻两块左右翻转，接缝处像素连续，可无限横向滚动
        int parity = (int)Math.Floor(parallaxX / drawWidth);
        float fraction = parallaxX - parity * drawWidth;
        float startX = topLeft.X + fraction - drawWidth;

        int tileCount = (int)Math.Ceiling(areaWidth / (float)drawWidth) + 2;

        for (int i = 0; i < tileCount; i++)
        {
            int tileIndex = parity + i;

            SpriteEffects effects =
                (tileIndex % 2 + 2) % 2 == 1
                    ? SpriteEffects.FlipHorizontally
                    : SpriteEffects.None;

            Rectangle destination = new(
                (int)(startX + i * drawWidth),
                drawY,
                drawWidth,
                drawHeight
            );

            Main.spriteBatch.Draw(
                texture,
                destination,
                null,
                Color.White * (alpha * Brightness),
                0f,
                Vector2.Zero,
                effects,
                0f
            );
        }
    }
}
