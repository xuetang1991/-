// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-01

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using ReLogic.Content;

using sea.Content.DustSea.NPCs;

using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace sea.Content.DustSea.Projectiles;

/// <summary>
/// 复活动画的纯表现弹幕：负责画白色剪影、显形后的本体贴图与爆发闪光。
/// </summary>
/// <remarks>This projectile only exists on the client, it lives for the whole animation and is killed explicitly by the revive system.</remarks>
public class JinlianReviveVisualProjectile : ModProjectile
{
    private const string SilhouetteTexturePath = "sea/Assets/UI/JinlianSilhouette";
    private const int FrameWidthSize = 40;
    private const int FrameHeightSize = 56;
    private const float FlashLightRadius = 420f;

    private Asset<Texture2D> _silhouetteTexture;

    /// <summary>
    /// 当前的剪影透明度，0 到 1。
    /// </summary>
    public float SilhouetteAlpha
    {
        get => Projectile.ai[0];
        set => Projectile.ai[0] = value;
    }

    /// <summary>
    /// 是否已经显形为本人。
    /// </summary>
    public bool Revealed
    {
        get => Projectile.ai[1] >= 0.5f;
        set => Projectile.ai[1] = value ? 1f : 0f;
    }

    /// <summary>
    /// 爆发闪光的强度，0 到 1。
    /// </summary>
    public float FlashStrength
    {
        get => Projectile.ai[2];
        set => Projectile.ai[2] = value;
    }

    /// <summary>
    /// 缩放，落地时会从大收到 1。
    /// </summary>
    public float VisualScale
    {
        get => Projectile.localAI[0];
        set => Projectile.localAI[0] = value;
    }

    public override void SetDefaults()
    {
        Projectile.width = FrameWidthSize;
        Projectile.height = FrameHeightSize;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.damage = 0;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.aiStyle = -1;
        Projectile.timeLeft = 900;
        Projectile.netImportant = false;
        Projectile.usesLocalNPCImmunity = true;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        // 纯表现弹幕，位置与各项参数都由复活系统直接写入
        Projectile.velocity = Vector2.Zero;
        Projectile.damage = 0;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (Main.dedServ)
        {
            return false;
        }

        _silhouetteTexture ??= ModContent.Request<Texture2D>(SilhouetteTexturePath);

        Texture2D texture = GetCurrentTexture();

        if (texture is not null)
        {
            float alpha = SilhouetteAlpha;
            float scale = VisualScale <= 0f ? 1f : VisualScale;
            Vector2 origin = new(FrameWidthSize * 0.5f, FrameHeightSize);
            Rectangle frame = new(0, 0, FrameWidthSize, FrameHeightSize);
            Vector2 position = Projectile.Center - Main.screenPosition;

            if (!Revealed)
            {
                // 先垫一圈略大的深色影子，海面这种亮背景上白剪影才看得清
                Main.EntitySpriteDraw(
                    texture,
                    position,
                    frame,
                    new Color(18, 12, 30) * (alpha * 0.9f),
                    0f,
                    origin,
                    scale * 1.08f,
                    SpriteEffects.None,
                    0
                );
            }

            Main.EntitySpriteDraw(
                texture,
                position,
                frame,
                Color.White * alpha,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0
            );
        }

        DrawFlash();

        return false;
    }

    /// <summary>
    /// 取这一帧该画的贴图：显形前是白剪影，显形后是她本人的彩色贴图。
    /// </summary>
    /// <returns>可用的贴图，取不到时返回 null</returns>
    private Texture2D? GetCurrentTexture()
    {
        if (!Revealed)
        {
            return _silhouetteTexture?.IsLoaded == true ? _silhouetteTexture.Value : null;
        }

        int npcType = ModContent.NPCType<JinlianNPC>();

        if (npcType < 0 || npcType >= TextureAssets.Npc.Length || !TextureAssets.Npc[npcType].IsLoaded)
        {
            return null;
        }

        return TextureAssets.Npc[npcType].Value;
    }

    /// <summary>
    /// 爆发瞬间在世界坐标里画一团加法的白色爆光。
    /// </summary>
    private void DrawFlash()
    {
        float flash = FlashStrength;

        if (flash <= 0.01f)
        {
            return;
        }

        Vector2 center = Projectile.Center - Main.screenPosition;
        float radius = FlashLightRadius * (1.2f - flash * 0.2f);

        Main.EntitySpriteDraw(
            TextureAssets.MagicPixel.Value,
            center,
            new Rectangle(0, 0, 1, 1),
            Color.White * (flash * 0.55f),
            0f,
            new Vector2(0.5f, 0.5f),
            new Vector2(radius, radius),
            SpriteEffects.None,
            0
        );
    }
}
