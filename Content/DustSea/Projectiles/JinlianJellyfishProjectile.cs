// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-01

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace sea.Content.DustSea.Projectiles;

/// <summary>
/// 堇涟召唤的水母弹幕。
/// </summary>
/// <remarks>Homing jellyfish projectile summoned by Jinlian.</remarks>
public class JinlianJellyfishProjectile : ModProjectile
{
    /// <summary>
    /// 水母命中造成的固定伤害（10 点真实伤害，无视护甲）。
    /// </summary>
    public const int DamageAmount = 1000;

    /// <summary>
    /// 追踪目标的最大搜索范围（像素），约 160 格，覆盖一屏以上。
    /// </summary>
    private const float HomingRange = 160f * 16f;

    /// <summary>
    /// 水母的巡航速度。
    /// </summary>
    private const float SwimSpeed = 15f;

    /// <summary>
    /// 每帧朝目标方向调整速度的比例。
    /// </summary>
    private const float TurnRate = 0.22f;

    /// <summary>
    /// 水母的加速上限，避免生成瞬间速度爆炸。
    /// </summary>
    private const float MaxSpeed = 17f;

    /// <summary>
    /// 每帧动画停留的帧数。
    /// </summary>
    private const int FrameInterval = 6;

    /// <summary>
    /// 残影保留的采样点数量。
    /// </summary>
    private const int TrailLength = 8;

    /// <summary>
    /// 残影起始不透明度。
    /// </summary>
    private const float TrailMaxAlpha = 0.42f;

    /// <summary>
    /// 残影颜色。
    /// </summary>
    private static readonly Color TrailColor = new(178, 132, 255);

    /// <summary>
    /// 残影历史位置，从旧到新。
    /// </summary>
    private readonly Vector2[] _trailPositions = new Vector2[TrailLength];

    /// <summary>
    /// 残影历史缩放，和位置一一对应。
    /// </summary>
    private readonly float[] _trailScales = new float[TrailLength];

    /// <summary>
    /// 已经记录的残影数量。
    /// </summary>
    private int _trailCount;

    /// <summary>
    /// 当前帧计时器。
    /// </summary>
    private int _frameTimer;

    /// <summary>
    /// 注册贴图帧数。
    /// </summary>
    public override void SetStaticDefaults() => Main.projFrames[Type] = 2;

    /// <summary>
    /// 设置水母弹幕的初始数值。
    /// </summary>
    public override void SetDefaults()
    {
        Projectile.width = 24;
        Projectile.height = 24;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.aiStyle = -1;
        // 存活时间要够飞完整个仇恨范围，否则还没追上目标就消失了
        Projectile.timeLeft = 420;
        Projectile.DamageType = DamageClass.Generic;
    }

    /// <summary>
    /// 每帧追踪最近的敌人。
    /// </summary>
    public override void AI()
    {
        // 伤害固定为 DamageAmount，不吃玩家加成也不吃召唤物重算。
        Projectile.damage = DamageAmount;

        _frameTimer++;
        if (_frameTimer >= FrameInterval)
        {
            _frameTimer = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        UpdateTrail();

        NPC target = FindTarget();
        if (target is null)
        {
            // 没有目标时沿当前方向缓慢漂移，靠 timeLeft 自然消失。
            if (Projectile.velocity.LengthSquared() > 0.01f)
            {
                Projectile.velocity = Vector2.Normalize(Projectile.velocity) * SwimSpeed;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            return;
        }

        Vector2 desired = Vector2.Normalize(target.Center - Projectile.Center) * SwimSpeed;
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, TurnRate);
        if (Projectile.velocity.Length() > MaxSpeed)
        {
            Projectile.velocity = Vector2.Normalize(Projectile.velocity) * MaxSpeed;
        }

        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
    }

    /// <summary>
    /// 命中后放出一点紫色火花并直接销毁水母。
    /// </summary>
    /// <param name="target">被命中的敌人。</param>
    /// <param name="hit">命中信息。</param>
    /// <param name="damageDone">实际伤害。</param>
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Circular(3f, 3f);
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, velocity, 0, TrailColor, 1.2f);
            dust.noGravity = true;
        }

        Projectile.Kill();
    }

    /// <summary>
    /// 自行绘制水母本体与残影。
    /// </summary>
    /// <param name="lightColor">环境光。</param>
    /// <returns>是否取消原版绘制。</returns>
    public override bool PreDraw(ref Color lightColor)
    {
        if (Main.dedServ)
        {
            return false;
        }

        Texture2D texture = TextureAssets.Projectile[Type].Value;
        Rectangle frame = texture.Frame(1, Main.projFrames[Type], 0, Projectile.frame);
        Vector2 origin = frame.Size() / 2f;
        Vector2 screenCenter = Projectile.Center - Main.screenPosition;

        int trailStart = TrailLength - _trailCount;

        for (int i = trailStart; i < TrailLength; i++)
        {
            float alpha = TrailMaxAlpha * (i - trailStart + 1) / TrailLength;
            Main.spriteBatch.Draw(
                texture,
                _trailPositions[i] - Main.screenPosition,
                frame,
                TrailColor * alpha,
                Projectile.rotation,
                origin,
                _trailScales[i],
                SpriteEffects.None,
                0f);
        }

        Main.spriteBatch.Draw(
            texture,
            screenCenter,
            frame,
            Color.White,
            Projectile.rotation,
            origin,
            Projectile.scale,
            SpriteEffects.None,
            0f);

        return false;
    }

    /// <summary>
    /// 找到距离最近且可以被攻击的敌人。
    /// </summary>
    /// <returns>最近的敌人，没有则返回 null。</returns>
    private NPC FindTarget()
    {
        NPC? closest = null;
        float closestDistance = HomingRange * HomingRange;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active || npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage)
            {
                continue;
            }

            if (npc.damage <= 0 && !npc.boss)
            {
                continue;
            }

            float distance = Vector2.DistanceSquared(npc.Center, Projectile.Center);
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closest = npc;
        }

        return closest;
    }

    /// <summary>
    /// 记录残影采样点。
    /// </summary>
    private void UpdateTrail()
    {
        for (int i = 0; i < TrailLength - 1; i++)
        {
            _trailPositions[i] = _trailPositions[i + 1];
            _trailScales[i] = _trailScales[i + 1];
        }

        _trailPositions[TrailLength - 1] = Projectile.Center;
        _trailScales[TrailLength - 1] = Projectile.scale;

        if (_trailCount < TrailLength)
        {
            _trailCount++;
        }
    }
}
