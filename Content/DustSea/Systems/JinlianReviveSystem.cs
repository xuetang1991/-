// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-01

using Microsoft.Xna.Framework;

using sea.Content.DustSea.NPCs;
using sea.Content.DustSea.Projectiles;

using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace sea.Content.DustSea.Systems;

/// <summary>
/// 堇涟的复活动画：镜头推近、绿色粒子聚合成白色剪影、震屏爆发、落地、镜头拉回。
/// </summary>
/// <remarks>Plays a scripted cinematic on the client and spawns the NPC at the end. The camera is driven from ModifyScreenPosition so the transition in and out is a smooth pan instead of a teleport.</remarks>
public class JinlianReviveSystem : ModSystem
{
    /// <summary>
    /// 复活完成后的绿色提示文本键。
    /// </summary>
    private const string HintKey = "ReviveHint";

    private const int PanInTicks = 45;
    private const int GatherTicks = 80;
    private const int ManifestTicks = 40;
    private const int ShakeTicks = 45;
    private const int BurstTicks = 20;
    private const int DescendTicks = 50;
    private const int PanOutTicks = 45;

    private const float HoverHeight = 6f * 16f;
    private const float GatherRadius = 150f;
    private const float SilhouetteScale = 1f;
    private const float MaxShakeOffset = 7f;
    private const float BurstShakeOffset = 11f;
    private const float FlashAlpha = 0.6f;
    private const int GatherDustPerTick = 3;
    private const int BurstDustCount = 60;
    private const int FrameWidthSize = 40;
    private const int FrameHeightSize = 56;

    /// <summary>
    /// 爆发音效资源路径。
    /// </summary>
    private const string BurstSoundPath = "sea/Assets/Sounds/JinlianReviveBurst";

    /// <summary>
    /// 充能音效资源路径。
    /// </summary>
    private const string ChargeSoundPath = "sea/Assets/Sounds/JinlianReviveCharge";

    /// <summary>
    /// 充能音效在震屏阶段倒数多少帧时开始播，正好在爆发那一下收尾。
    /// </summary>
    private const int ChargeLeadTicks = 20;

    private static readonly SoundStyle BurstSound = new(BurstSoundPath);

    private static readonly SoundStyle ChargeSound = new(ChargeSoundPath);

    private RevivePhase _phase;
    private int _timer;
    private int _tileX;
    private int _tileY;
    private Vector2 _groundPosition;
    private Vector2 _hoverPosition;
    private Vector2 _focusCenter;
    private Vector2 _returnCenter;
    private float _shakeStrength;
    private float _flash;
    private float _silhouetteAlpha;
    private float _silhouetteProgress;
    private bool _revealed;

    private float _silhouetteScaleCurrent = SilhouetteScale;

    // 负责绘制剪影的纯表现弹幕
    private int _visualProjectileIndex = -1;

    /// <summary>
    /// 复活动画是否正在播放。
    /// </summary>
    public bool IsRunning => _phase != RevivePhase.None;

    /// <summary>
    /// 开始复活动画。
    /// </summary>
    /// <returns>成功开始返回 true</returns>
    /// <remarks>The shore tile is resolved up front so the whole camera move can be planned before the first frame.</remarks>
    public bool TryStartRevive()
    {
        if (IsRunning || Main.dedServ || Main.gameMenu)
        {
            return false;
        }

        if (!JinlianSpawnSystem.TryFindSpawnTile(out int tileX, out int tileY))
        {
            Mod.Logger.Warn("找不到浊海左岸的落脚点，复活取消 / Jinlian revive cancelled because no dust sea shore tile was found");

            return false;
        }

        _tileX = tileX;
        _tileY = tileY;
        _groundPosition = new Vector2(tileX * 16f + 8f, tileY * 16f);
        _hoverPosition = _groundPosition - new Vector2(0f, HoverHeight);
        _focusCenter = Vector2.Lerp(_groundPosition, _hoverPosition, 0.5f);
        _returnCenter = Main.screenPosition + new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);

        _phase = RevivePhase.PanIn;
        _timer = 0;
        _shakeStrength = 0f;
        _flash = 0f;
        _silhouetteAlpha = 0f;
        _silhouetteProgress = 0f;
        SpawnVisualProjectile();

        SoundEngine.PlaySound(SoundID.Item8, _focusCenter);
        Mod.Logger.Info($"开始复活堇涟 {tileX}, {tileY} / Jinlian revive started at {tileX}, {tileY}");

        return true;
    }

    /// <summary>
    /// 推进复活动画。
    /// </summary>
    public override void PostUpdateEverything()
    {
        if (Main.dedServ || _phase == RevivePhase.None)
        {
            return;
        }

        // 中途退回主菜单就直接放弃动画，避免下次进世界接着播
        if (Main.gameMenu)
        {
            KillVisualProjectile();
            _phase = RevivePhase.None;
            _shakeStrength = 0f;
            _flash = 0f;
            _silhouetteAlpha = 0f;
            _silhouetteProgress = 0f;

            return;
        }

        _timer++;
        SyncVisualProjectile();

        switch (_phase)
        {
            case RevivePhase.PanIn:
                if (_timer >= PanInTicks)
                {
                    Advance(RevivePhase.Gather);
                }

                break;
            case RevivePhase.Gather:
                SpawnGatherDust(GatherDustPerTick);
                _silhouetteAlpha = MathHelper.Clamp(_timer / (float)GatherTicks, 0f, 1f);

                if (_timer >= GatherTicks)
                {
                    Advance(RevivePhase.Manifest);
                }

                break;
            case RevivePhase.Manifest:
                _silhouetteAlpha = 1f;
                SpawnGatherDust(1);

                if (_timer >= ManifestTicks)
                {
                    Advance(RevivePhase.Shake);
                }

                break;
            case RevivePhase.Shake:
                _silhouetteAlpha = 1f;
                _shakeStrength = MaxShakeOffset * MathHelper.Clamp(_timer / (float)ShakeTicks, 0f, 1f);

                if (_timer == ShakeTicks - ChargeLeadTicks)
                {
                    SoundEngine.PlaySound(ChargeSound with { Volume = 0.85f }, _hoverPosition);
                }

                if (Main.rand.NextBool(3))
                {
                    SpawnGatherDust(1);
                }

                if (_timer >= ShakeTicks)
                {
                    Advance(RevivePhase.Burst);
                }

                break;
            case RevivePhase.Burst:
                if (_timer == 1)
                {
                    // 爆发的那一刻由白剪影变成她本人
                    _revealed = true;
                    PlayBurst();
                }

                float burstProgress = MathHelper.Clamp(_timer / (float)BurstTicks, 0f, 1f);
                _flash = FlashAlpha * (1f - burstProgress);
                _shakeStrength = BurstShakeOffset * (1f - burstProgress);

                if (_timer >= BurstTicks)
                {
                    Advance(RevivePhase.Descend);
                }

                break;
            case RevivePhase.Descend:
                _silhouetteAlpha = 1f;
                _silhouetteProgress = MathHelper.SmoothStep(0f, 1f, _timer / (float)DescendTicks);
                _silhouetteScaleCurrent = MathHelper.Lerp(SilhouetteScale, 1f, _silhouetteProgress);

                if (_timer >= DescendTicks)
                {
                    SpawnJinlian();
                    Advance(RevivePhase.PanOut);
                }

                break;
            case RevivePhase.PanOut:
                _shakeStrength = 0f;
                _silhouetteAlpha = MathHelper.Clamp(1f - _timer / (float)PanOutTicks, 0f, 1f);

                if (_timer >= PanOutTicks)
                {
                    Finish();
                }

                break;
        }
    }

    /// <summary>
    /// 把镜头拉到复活动画的位置，再平滑拉回玩家。
    /// </summary>
    /// <remarks>Setting Main.screenPosition here overrides the vanilla camera for the whole cinematic; the pan in and out use SmoothStep so neither end snaps.</remarks>
    public override void ModifyScreenPosition()
    {
        if (Main.dedServ || _phase == RevivePhase.None)
        {
            return;
        }

        Vector2 halfScreen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);

        Vector2 center = _phase switch
        {
            RevivePhase.PanIn => Vector2.Lerp(
                _returnCenter,
                _focusCenter,
                MathHelper.SmoothStep(0f, 1f, _timer / (float)PanInTicks)
            ),
            RevivePhase.PanOut => Vector2.Lerp(
                _focusCenter,
                Main.LocalPlayer.Center,
                MathHelper.SmoothStep(0f, 1f, _timer / (float)PanOutTicks)
            ),
            _ => _focusCenter,
        };

        Vector2 shake = Vector2.Zero;

        if (_shakeStrength > 0f)
        {
            shake = Main.rand.NextVector2Circular(_shakeStrength, _shakeStrength);
        }

        Main.screenPosition = center - halfScreen + shake;
    }

    /// <summary>
    /// 让绿色粒子朝剪影中心汇聚。
    /// </summary>
    /// <param name="count">这次生成的粒子数量</param>
    private void SpawnGatherDust(int count)
    {
        // 剪影本体的世界尺寸：贴图 40x56 按当前缩放铺开，脚底贴在悬空点上
        float bodyWidth = FrameWidthSize * _silhouetteScaleCurrent;
        float bodyHeight = FrameHeightSize * _silhouetteScaleCurrent;

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Main.rand.NextVector2Circular(GatherRadius, GatherRadius);
            Vector2 position = _hoverPosition + offset;

            // 汇聚目标取在剪影身体范围内，粒子贴着人形轮廓收拢而不是缩成一个点
            Vector2 target = _hoverPosition + new Vector2(
                Main.rand.NextFloat(-bodyWidth * 0.45f, bodyWidth * 0.45f),
                Main.rand.NextFloat(-bodyHeight, 0f)
            );

            Vector2 velocity = (target - position).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(2.4f, 6.5f);
            int dustIndex = Dust.NewDust(position, 1, 1, DustID.GreenTorch, velocity.X, velocity.Y, 0, Color.White, 1.4f);

            Main.dust[dustIndex].noGravity = true;
            Main.dust[dustIndex].velocity = velocity;
        }
    }

    /// <summary>
    /// 爆发：向外炸开一圈绿色粒子。
    /// </summary>
    private void PlayBurst()
    {
        for (int i = 0; i < BurstDustCount; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Circular(9f, 9f);
            int dustIndex = Dust.NewDust(_hoverPosition, 1, 1, DustID.GreenTorch, velocity.X, velocity.Y, 0, Color.White, 1.8f);

            Main.dust[dustIndex].noGravity = true;
        }

        SoundEngine.PlaySound(BurstSound with { Volume = 0.9f, Pitch = 0.15f }, _hoverPosition);
    }

    /// <summary>
    /// 在落脚点生成复活的堇涟。
    /// </summary>
    private void SpawnJinlian()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        int npcType = ModContent.NPCType<JinlianNPC>();
        int npcIndex = NPC.NewNPC(
            new EntitySource_SpawnNPC(),
            (int)_groundPosition.X,
            (int)(_groundPosition.Y - 3f * 16f),
            npcType
        );

        if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
        {
            return;
        }

        NPC jinlian = Main.npc[npcIndex];

        jinlian.ai[0] = 1f;
        jinlian.ai[1] = _tileX;
        jinlian.ai[2] = _tileY;
        jinlian.direction = -1;
        jinlian.spriteDirection = -1;
        jinlian.velocity = Vector2.Zero;
        jinlian.netUpdate = true;

        JinlianSpawnSystem.MarkJinlianRevived();
    }

    /// <summary>
    /// 生成画剪影的纯表现弹幕。
    /// </summary>
    private void SpawnVisualProjectile()
    {
        _visualProjectileIndex = -1;

        if (Main.dedServ || Main.netMode == NetmodeID.Server)
        {
            return;
        }

        _visualProjectileIndex = Projectile.NewProjectile(
            new EntitySource_SpawnNPC(),
            _hoverPosition,
            Vector2.Zero,
            ModContent.ProjectileType<JinlianReviveVisualProjectile>(),
            0,
            0f,
            Main.myPlayer
        );

        Mod.Logger.Info($"生成剪影弹幕 索引={_visualProjectileIndex} 位置={_hoverPosition} / Jinlian silhouette projectile spawned");
    }

    /// <summary>
    /// 把透明度、显形状态、闪光与位置同步给表现弹幕。
    /// </summary>
    private void SyncVisualProjectile()
    {
        if (_visualProjectileIndex < 0 || _visualProjectileIndex >= Main.maxProjectiles)
        {
            return;
        }

        Projectile projectile = Main.projectile[_visualProjectileIndex];

        if (!projectile.active || projectile.type != ModContent.ProjectileType<JinlianReviveVisualProjectile>())
        {
            _visualProjectileIndex = -1;

            return;
        }

        projectile.Center = Vector2.Lerp(_hoverPosition, _groundPosition, _silhouetteProgress);
        projectile.timeLeft = 120;

        if (projectile.ModProjectile is JinlianReviveVisualProjectile visual)
        {
            visual.SilhouetteAlpha = _silhouetteAlpha;
            visual.Revealed = _revealed;
            visual.FlashStrength = _flash;
            visual.VisualScale = _silhouetteScaleCurrent;
        }
    }

    /// <summary>
    /// 收掉表现弹幕。
    /// </summary>
    private void KillVisualProjectile()
    {
        if (_visualProjectileIndex >= 0 && _visualProjectileIndex < Main.maxProjectiles)
        {
            Projectile projectile = Main.projectile[_visualProjectileIndex];

            if (projectile.active && projectile.type == ModContent.ProjectileType<JinlianReviveVisualProjectile>())
            {
                projectile.Kill();
            }
        }

        _visualProjectileIndex = -1;
    }

    /// <summary>
    /// 结束复活动画并提示玩家。
    /// </summary>
    private void Finish()
    {
        _phase = RevivePhase.None;
        _shakeStrength = 0f;
        _flash = 0f;
        _silhouetteAlpha = 0f;
        _silhouetteProgress = 0f;
        _silhouetteScaleCurrent = SilhouetteScale;
        _revealed = false;
        KillVisualProjectile();

        Main.NewText(
            Language.GetTextValue("Mods.sea.NPCs.JinlianNPC." + HintKey),
            (byte)120,
            (byte)230,
            (byte)150
        );
    }

    /// <summary>
    /// 切到下一个动画阶段。
    /// </summary>
    /// <param name="phase">下一个阶段</param>
    private void Advance(RevivePhase phase)
    {
        _phase = phase;
        _timer = 0;

        Mod.Logger.Info(
            $"复活动画阶段 {phase} 剪影透明度={_silhouetteAlpha:0.00} "
                + $"视口={Main.screenPosition.X:0},{Main.screenPosition.Y:0} 悬空点={_hoverPosition.X:0},{_hoverPosition.Y:0} "
                + $"/ Jinlian revive phase {phase} alpha={_silhouetteAlpha:0.00}");
    }

    /// <summary>
    /// 复活动画阶段。
    /// </summary>
    private enum RevivePhase
    {
        None = 0,
        PanIn = 1,
        Gather = 2,
        Manifest = 3,
        Shake = 4,
        Burst = 5,
        Descend = 6,
        PanOut = 7,
    }
}
