// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-24

using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using ReLogic.Content;

using sea.Common.Utils;
using sea.Content.DustSea.Systems;
using sea.Content.FarSea.Projectiles;
using sea.Content.FarSea.Systems;

using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace sea.Content.FarSea.NPCs;

[AutoloadBossHead]
public class FarSeaEarlNPC : ModNPC
{
    private enum ActionState
    {
        MovingToSide = 0,
        FollowingPlayer = 1,
        Charge = 2,
        Dash = 3,
        SuckAttack = 4,
        ApproachingCenter = 5,
        HatThrowTrack = 6,
        HatCatch = 7,
        HatTeleportDash = 8
    }

    private ref float AIState => ref NPC.ai[0];
    private ref float AITimer => ref NPC.ai[1];
    private ref float TargetSide => ref NPC.ai[2];
    private ref float DashStartY => ref NPC.ai[3];

    private int DashCounter
    {
        get => (int)NPC.localAI[0];
        set => NPC.localAI[0] = value;
    }

    private float SuckAttackX
    {
        get => NPC.localAI[2];
        set => NPC.localAI[2] = value;
    }

    private bool HasTriggeredSuckPhase
    {
        get => NPC.localAI[3] == 1f;
        set => NPC.localAI[3] = value ? 1f : 0f;
    }

    private bool BlastFired
    {
        get => NPC.localAI[1] == 1f;
        set => NPC.localAI[1] = value ? 1f : 0f;
    }

    // =========================================================
    // 弹幕生成控制
    // =========================================================

    private int _jellyfishTimer;
    private int _jellyfishWave;

    // =========================================================
    // 第二阶段网格弹幕
    // =========================================================

    // 血量降到一半后进入第二阶段
    private const float SecondPhaseHealthPercent = 0.5f;

    // 每 5 秒（300 tick）释放一次网格弹幕
    private const int GridAttackInterval = 300;

    // 丢帽动作：帽子被掀起的画面停留 9 tick，帽子飞走的画面停留 3 tick
    private const int HatOffHoldFrames = 30;
    private const int HatOffFlyFrames = 12;

    /// <summary>两套帽子技能之间的冷却。</summary>
    private const int HatAttackInitialCooldown = 45;
    private const int HatAttackCooldown = 480;

    /// <summary>技能帽子至少要飞这么久（10 秒）才允许收回。</summary>
    private const int HatMinAirTicks = 600;

    // 二阶段节奏：冲刺 3 次 → 扔 1 次帽子
    private const int HatDashesBeforeThrow = 3;

    // 帽子飞出去期间「日常躲避」的距离区间（像素，1 格 = 16px）
    // 玩家进入 200 格内就后退，超过约 240 格才靠近，速度给足才看得出跑动
    // 丢帽 10 秒的走位舒适带：玩家近于 60 格就退，远于 75 格就进
    private const float EvadeNearDistance = 60f * 16f;
    private const float EvadeFarDistance = 75f * 16f;
    private const float EvadeSpeed = 12.5f;

    // 躲避时不要跑出世界边界
    private const float EvadeEdgeMargin = 60f;

    /// <summary>丢帽飞出去至少要这么久（2 秒）才允许收回。</summary>
    private const int ExpelMinAirTicks = 120;

    /// <summary>收帽动作每帧停留的 tick 数（约 7 帧 / 秒）。</summary>
    private const int HatCatchNoHatFrames = 12;
    private const int HatCatchHatOffFrames = 30;

    /// <summary>瞬移冲刺的速度。</summary>
    private const float OmniDashSpeed = 34f;

    /// <summary>瞬移冲刺的持续时间。</summary>
    private const int OmniDashDuration = 34;

    /// <summary>瞬移冲刺朝玩家转向的强度。</summary>
    private const float OmniDashHoming = 0.12f;

    /// <summary>吸取态警告圈半径（40 格）。</summary>
    // 吸取态：左右触手尖端各一圈，半径 10 格（160 px）
    private const float SuckAuraRadius = 10f * 16f;

    // 圆心 = FarSeaEarlNPC_Anim10.png 里左右触手尖上的青色球，
    // 下面是在 680x680 贴图上量出来的、相对贴图中心（也就是伯爵中心）的偏移
    private const float SuckAuraLeftOffsetX = -205f;
    private const float SuckAuraLeftOffsetY = -97f;
    private const float SuckAuraRightOffsetX = 214f;
    private const float SuckAuraRightOffsetY = -62f;

    /// <summary>
    /// 吸取态每秒造成的经典难度基础伤害：专家难度、玩家防御 45 时约 10 点。
    /// </summary>

    private int _gridAttackTimer;

    // 转阶段锁血：血量触到一半时若正在释放技能，先把血量锁住，
    // 等这次技能自然放完再解除锁定并进入第二阶段
    private bool _healthLocked;

    // 是否已真正进入第二阶段；置位后不再回退，也不再锁血
    private bool _secondPhaseStarted;

    // 丢帽动作倒计时：先播「帽子被掀起」再播「帽子飞走」，最后回落到无帽常态
    private int _hatOffTimer;

    // 到 50% 时如果正在放技能，先把丢帽动作挂起，等技能结束再播
    private int _hatProjectileIndex = -1;
    private int _hatAttackCooldown;

    private int _hatAirTicks;

    private int _hatMode;

    private bool _hatDied;
    private int _hatCatchTimer;
    private int _hatAttackVariant;

    // 本次帽子技能前摇对应的变体（0 = 追踪帽，1 = 近距帽子 + 瞬移冲刺）
    private int _hatWindupVariant;

    // 本轮投掷是否真的把帽子扔出去过（没扔出就不该触发收帽）
    private bool _hatSpawnedThisThrow;
    private int _hatRespawnAttempts;

    // 距离上一次扔帽子已经完成的冲刺次数
    private int _dashesSinceHat;

    // 累计扔帽次数：第 3、6、9… 次换成激光帽
    private int _hatThrowCount;

    // 帽子最近一次的位置/速度：万一被意外销毁，就在原地补一发，玩家看不出断档
    private Vector2 _hatLastCenter;
    private Vector2 _hatLastVelocity;

    // 传送冲刺用：出手方向在起手那一帧就定死
    private Vector2 _omniDashVelocity;
    private bool _omniDashStarted;
    private float _omniDashRotation;
    private int _suckAuraTimer;

    // =========================================================
    // Boss 动画资源
    // =========================================================

    private const int TotalAnimFrames = 13;

    private static Asset<Texture2D>[] _animationFrames =
        new Asset<Texture2D>[TotalAnimFrames];

    private static Asset<Texture2D>? _idleTexture2;
    private static Asset<Texture2D>? _idleTexture2b;
    private static Asset<Texture2D>? _idleTexture3;
    private static Asset<Texture2D>? _chargeTexture;
    private static Asset<Texture2D>? _dashTexture;

    // 二阶段（血量 50% 以下）使用的无帽贴图：帽子被打掉后露出本来的钟形伞盖
    private static Asset<Texture2D>? _noHatDefaultTexture;
    private static Asset<Texture2D>? _noHatIdleTexture2;
    private static Asset<Texture2D>? _noHatIdleTexture2b;
    private static Asset<Texture2D>? _noHatIdleTexture3;
    private static Asset<Texture2D>? _noHatChargeTexture;
    private static Asset<Texture2D>? _noHatDashTexture;

    // 丢帽动作的两帧过渡图
    private static Asset<Texture2D>? _hatOffTexture0;
    private static Asset<Texture2D>? _hatOffTexture1;

    private static Asset<Texture2D>? _suckAuraTexture;

    // =========================================================
    // 音效
    // =========================================================

    /// <summary>日常移动：水下搅动水声（低频循环触发）。</summary>
    private static readonly SoundStyle MoveWaterSound = new("sea/Assets/Sounds/FarSeaMoveWater")
    {
        Volume = 0.34f,
        PitchVariance = 0.14f,
    };

    /// <summary>吸取态：低频轰鸣 + 上扫吸力（类似万象天引）。</summary>
    private static readonly SoundStyle SuckFieldSound = new("sea/Assets/Sounds/FarSeaSuckLoop")
    {
        Volume = 0.55f,
        PitchVariance = 0.06f,
    };

    // 循环音效的重复间隔（素材时长 × 60 帧）：水声 2.00s、吸力 2.50s、旋转 2.00s
    private const int WaterLoopTicks = 120;

    private const int SuckLoopTicks = 150;

    private const int WhooshLoopTicks = 120;

    /// <summary>帽子扔出后旋转追踪的风声（帽子在场期间循环）。</summary>
    private static readonly SoundStyle HatWhooshSound = new("sea/Assets/Sounds/HatSpinWhoosh")
    {
        Volume = 0.6f,
        PitchVariance = 0.1f,
    };

    /// <summary>丢帽：布料甩出去的「唰」。</summary>
    private static readonly SoundStyle HatThrowSound = new("sea/Assets/Sounds/HatClothThrow")
    {
        Volume = 0.52f,
        PitchVariance = 0.08f,
    };

    /// <summary>收帽：布料收拢落手的「啪嗒簌」。</summary>
    private static readonly SoundStyle HatCatchSound = new("sea/Assets/Sounds/HatClothCatch")
    {
        Volume = 0.58f,
        PitchVariance = 0.07f,
    };

    /// <summary>传送：末影人式的短促下扫。</summary>
    private static readonly SoundStyle HatTeleportSound = new("sea/Assets/Sounds/HatTeleport")
    {
        Volume = 0.72f,
        PitchVariance = 0.05f,
    };

    private int _idleAnimTimer = 0;
    private int _currentAnimFrame = 0;

    // 冲刺时整只 Boss 的倾斜角（弧度，顺时针为正），让头部跟随实际冲刺方向
    private float _dashTiltAngle;

    private float _targetSideDistance = MinChargeSideDistance;

    // 冲刺残影采样：[0] 是最新位置，往后越旧
    private readonly Vector2[] _dashTrailPositions =
        new Vector2[DashTrailLength];

    private readonly float[] _dashTrailRotations =
        new float[DashTrailLength];

    private int _dashTrailCount;

    public static bool IsActive = false;

    // =========================================================
    // 吸取态时间轴
    // =========================================================

    private const int SuckFramesEnd = 130;
    private const int SuckHoldEnd = 610;
    private const int SuckFrame11End = 623;
    private const int SuckAnimDuration = 666;

    // =========================================================
    // 吸引力参数
    // =========================================================

    private const float SuckPullAccelX = 0.40f;
    private const float SuckPullAccelY = 0.75f;
    private const float SuckResistAccel = 0.20f;
    private const float SuckPullMaxSpeed = 14f;
    private const float SuckMinDistance = 80f;

    // =========================================================
    // 结束爆推参数
    // =========================================================

    private const float BlastOutSpeed = 38f;
    private const float BlastUpwardBias = 0.28f;

    // =========================================================
    // 粒子参数
    // =========================================================

    private const float DustInfluenceRadius = 1000f;
    private const float DustSwallowRadius = 50f;
    private const float DustMaxInwardSpeed = 19f;
    private const float BlastDustOutSpeed = 24f;
    private const float BlastDustRadius = 900f;

    private static readonly int[] SuckDustTypes =
    [
        DustID.GemSapphire,
        DustID.GemDiamond,
        DustID.BlueFlare
    ];

    // =========================================================
    // 冲刺参数
    // =========================================================

    private const int DashDuration = 40;

    // 冲刺速度放慢，给玩家更多反应与走位空间
    private const float DashSpeed = 45f;

    // 判定框：平时竖版站姿用「窄高」框，横放贴图（蓄力 / 冲刺 / 传送冲刺）改成「宽扁」框，
    // 否则 120x600 的高框会在横着冲的时候比贴图高出/低下一大截。
    /// <summary>经典难度目标血量。</summary>
    private const int ClassicLifeMax = 60000;

    /// <summary>专家难度目标血量（90000）。</summary>
    private const int ExpertLifeMax = 90000;

    /// <summary>大师难度目标血量。</summary>
    private const int MasterLifeMax = 114749;

    private const int UprightHitboxWidth = 120;
    private const int UprightHitboxHeight = 600;
    private const float DashHitboxLength = 560f;
    private const float DashHitboxThickness = 130f;

    /// <summary>
    /// 冲刺撞击玩家的经典难度基础伤害：专家难度、玩家防御 45 时结算约 150 点。
    /// </summary>

    // 激光帽打满 3 轮脉冲后（每轮 180 帧）触发「传送 + 紫爆 + 全向冲刺」
    private const int LaserTeleportTicks = 540;

    // 传送冲刺：无蓄力、方向就是玩家方向（速度复用上方的 OmniDashSpeed）
    private const int OmniDashTicks = 26;

    // 蓄力时长：原 18 tick 再延长 0.5 秒（30 tick）
    private const int ChargeDuration = 48;

    // 蓄力起手位置：只在离玩家最近的一侧，且最少 / 最多保持这么远，
    // 避免为了蓄力绕到对面或横穿大半个场地
    private const float MinChargeSideDistance = 320f;

    private const float MaxChargeSideDistance = 800f;

    // 追踪范围：扇形 30 度，即冲刺轴左右各 15 度
    private const float DashHomingHalfAngle = 0.26179939f;

    // 追踪强度：每 tick 朝玩家方向修正的比例，越小越迟钝
    private const float DashHomingStrength = 0.05f;

    // 倾斜幅度：只取扇形内偏转角的 75%，避免整个身体转得过头
    private const float DashTiltStrength = 0.75f;

    // 倾斜收敛速度：数值越大倾得越快
    private const float DashTiltLerp = 0.15f;

    // =========================================================
    // 冲刺残影参数
    // =========================================================

    // 采样长度：12 tick 约 0.2 秒的拖尾
    private const int DashTrailLength = 12;

    // 紧贴本体那一层的最大不透明度，往后逐层衰减
    private const float DashTrailMaxAlpha = 0.34f;

    /// <summary>非冲刺状态下的残影浓度倍数。</summary>
    private const float MoveTrailAlphaScale = 0.5f;

    // 越旧的残影缩得越小，避免纯复制粘贴的僵硬感
    private const float DashTrailShrink = 0.3f;

    // =========================================================
    // 跟随锁定时间
    // =========================================================

    private const int FollowDuration = 45;

    // =========================================================
    // 趋近态参数
    // =========================================================

    private const float ApproachMaxSpeed = 20f;
    private const float ApproachAccel = 0.1f;
    private const float ApproachThreshold = 80f;

    private static Vector2 GetSubworldCenter()
    {
        return new Vector2(
            Main.maxTilesX / 2f * 16f,
            Main.maxTilesY / 2f * 16f);
    }

    // =========================================================
    // 加载资源
    // =========================================================

    public override void Load()
    {
        if (Main.dedServ)
        {
            return;
        }

        for (int i = 0; i < TotalAnimFrames; i++)
        {
            string path = $"sea/Content/FarSea/NPCs/FarSeaEarlNPC_Anim{i}";

            if (ModContent.RequestIfExists<Texture2D>(
                path,
                out Asset<Texture2D> texture))
            {
                _animationFrames[i] = texture;
            }
        }

        _idleTexture2 =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_Idle2");

        _idleTexture2b =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_Idle2b");

        _idleTexture3 =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_Idle3");

        _chargeTexture =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_Charge");

        _dashTexture =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_Dash");

        _noHatDefaultTexture =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_NoHat",
                AssetRequestMode.ImmediateLoad);

        _noHatIdleTexture2 =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_Idle2_NoHat",
                AssetRequestMode.ImmediateLoad);

        _noHatIdleTexture2b =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_Idle2b_NoHat",
                AssetRequestMode.ImmediateLoad);

        _noHatIdleTexture3 =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_Idle3_NoHat",
                AssetRequestMode.ImmediateLoad);

        _noHatChargeTexture =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_Charge_NoHat",
                AssetRequestMode.ImmediateLoad);

        _noHatDashTexture =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_Dash_NoHat",
                AssetRequestMode.ImmediateLoad);

        _hatOffTexture0 =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_HatOff0",
                AssetRequestMode.ImmediateLoad);

        _hatOffTexture1 =
            ModContent.Request<Texture2D>(
                "sea/Content/FarSea/NPCs/FarSeaEarlNPC_HatOff1",
                AssetRequestMode.ImmediateLoad);

        _suckAuraTexture =
            ModContent.Request<Texture2D>(
                "sea/Assets/Textures/SuckAura",
                AssetRequestMode.ImmediateLoad);
    }

    public override void Unload()
    {
        for (int i = 0; i < TotalAnimFrames; i++)
        {
            _animationFrames[i] = null;
        }

        _idleTexture2 = null;
        _idleTexture2b = null;
        _idleTexture3 = null;
        _chargeTexture = null;
        _dashTexture = null;

        _noHatDefaultTexture = null;
        _noHatIdleTexture2 = null;
        _noHatIdleTexture2b = null;
        _noHatIdleTexture3 = null;
        _noHatChargeTexture = null;
        _noHatDashTexture = null;
        _hatOffTexture0 = null;
        _hatOffTexture1 = null;
        _suckAuraTexture = null;
        _hatOffTimer = 0;

        IsActive = false;
    }

    // =========================================================
    // 基础设置
    // =========================================================

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;

        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);
    }

    /// <summary>
    /// 当前难度下的目标血量：经典 60000 / 专家 90000 / 大师 114749。
    /// tModLoader 2026.08 实测不会给 mod NPC 再乘 <c>GameModeData.EnemyMaxLifeMultiplier</c>，
    /// 所以这里直接把目标值写进 <c>NPC.lifeMax</c>，不做任何除法补偿。
    /// </summary>
    private static int GetTargetLifeMax()
    {
        if (Main.masterMode)
        {
            return MasterLifeMax;
        }

        return Main.expertMode ? ExpertLifeMax : ClassicLifeMax;
    }

    /// <summary>
    /// 兜底：若运行时又按难度调整了一次 <c>NPC.lifeMax</c>，在这里拉回目标值。
    /// </summary>
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        int targetLifeMax = GetTargetLifeMax();
        if (NPC.lifeMax == targetLifeMax)
        {
            return;
        }

        bool wasFullHealth = NPC.life >= NPC.lifeMax;
        NPC.lifeMax = targetLifeMax;
        if (wasFullHealth)
        {
            NPC.life = NPC.lifeMax;
        }
    }

    public override void SetDefaults()
    {
        NPC.width = 120;
        NPC.height = 600;
        NPC.scale = 1f;

        // 本体任何时候都没有碰撞伤害，伤害全部由扔出去的帽子与技能负责
        NPC.damage = 0;
        NPC.defense = 30;
        NPC.lifeMax = GetTargetLifeMax();

        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;

        NPC.knockBackResist = 0f;
        NPC.noGravity = true;
        NPC.noTileCollide = true;

        NPC.value = Item.buyPrice(gold: 5);
        NPC.boss = true;
        NPC.aiStyle = -1;
        _hatAttackCooldown = HatAttackInitialCooldown;
    }

    public override void SetBestiary(
        BestiaryDatabase database,
        BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.AddRange(
            new IBestiaryInfoElement[]
            {
                new FlavorTextBestiaryInfoElement(
                    "远海深处的恐怖守护者，会像蜂王一样发起致命的横向冲刺。")
            });
    }

    // =========================================================
    // 主 AI
    // =========================================================

    public override void AI()
    {
        IsActive = true;

        NPC.TargetClosest(true);

        Player player = Main.player[NPC.target];

        if (player.dead || !player.active)
        {
            NPC.velocity.Y -= 0.4f;

            if (NPC.timeLeft > 10)
            {
                NPC.timeLeft = 10;
            }

            return;
        }

        AITimer++;

        float healthPercent =
            (float)NPC.life / NPC.lifeMax;

        UpdatePhaseTransition(healthPercent);

        UpdateHatOffState();

        UpdateGridAttack(player);

        UpdateHatProjectileUpkeep();

        if (_hatSpawnedThisThrow)
        {
            // 只要本次技能里帽子已经扔出去过就一直计时，
            // 避免弹幕被意外销毁时计时中断（否则会被立刻判定成「飞够了」）
            _hatAirTicks++;

            if (_hatProjectileIndex >= 0
                && _hatProjectileIndex < Main.maxProjectiles
                && Main.projectile[_hatProjectileIndex].active)
            {
                _hatLastCenter = Main.projectile[_hatProjectileIndex].Center;
                _hatLastVelocity = Main.projectile[_hatProjectileIndex].velocity;
            }
        }

        UpdateHatRecallWatch();

        if (_hatOffTimer > 0)
        {
            // 丢帽动作播放期间原地悬停，不接受也不触发任何其他动作
            NPC.velocity *= 0.9f;

            return;
        }

        if (healthPercent <= 0.8f
            && healthPercent > SecondPhaseHealthPercent
            && !HasTriggeredSuckPhase
            && AIState != (float)ActionState.SuckAttack
            && AIState != (float)ActionState.ApproachingCenter
            && AIState != (float)ActionState.HatThrowTrack
            && AIState != (float)ActionState.HatCatch)
        {
            AIState =
                (float)ActionState.ApproachingCenter;

            AITimer = 0;
            _currentAnimFrame = 0;

            HasTriggeredSuckPhase = true;

            NPC.velocity *= 0.2f;
            NPC.netUpdate = true;
        }

        NPC.dontTakeDamage =
            AIState == (float)ActionState.ApproachingCenter
            || _healthLocked;

        // 平时不造成接触伤害，冲刺期间由 UpdateContactDamage() 临时给上。
        NPC.damage = 0;

        if (AIState == (float)ActionState.MovingToSide
            || AIState == (float)ActionState.FollowingPlayer
            || AIState == (float)ActionState.ApproachingCenter
            || AIState == (float)ActionState.HatThrowTrack)
        {
            _idleAnimTimer =
                (_idleAnimTimer + 1) % 75;
        }
        else
        {
            _idleAnimTimer = 0;
        }

        // 帽子技能只在「50% 以下」解锁：必须先进入第二阶段（丢帽过场做完），并且当前血量低于 50%。
        // The hat skill unlocks only below 50% health, after the second phase has started.
        if (_secondPhaseStarted
            && healthPercent < SecondPhaseHealthPercent)
        {
            TryStartHatAttack();
        }

        switch ((ActionState)AIState)
        {
            case ActionState.MovingToSide:
                MovingToSideLogic(player);
                break;

            case ActionState.FollowingPlayer:
                FollowingPlayerLogic(player);
                break;

            case ActionState.Charge:
                ChargePreparation(player);
                break;

            case ActionState.Dash:
                DashAttack(player);
                break;

            case ActionState.SuckAttack:
                SuckAttackLogic(player);
                break;

            case ActionState.ApproachingCenter:
                ApproachingCenterLogic(player);
                break;

            case ActionState.HatThrowTrack:
                HatThrowTrackLogic(player);
                break;

            case ActionState.HatCatch:
                HatCatchLogic(player);
                break;

            case ActionState.HatTeleportDash:
                HatTeleportDashLogic(player);
                break;
        }

        bool hatState =
            AIState == (float)ActionState.HatThrowTrack
            || AIState == (float)ActionState.HatCatch
            || AIState == (float)ActionState.HatTeleportDash;

        if (hatState)
        {
            // 帽子技能的贴图与姿态都是设计好的，本体不许有任何倾斜
            _dashTiltAngle = 0f;
        }
        else if (AIState != (float)ActionState.Dash)
        {
            // 冲刺结束后把倾斜慢慢收回原位，避免硬切回 0 度
            _dashTiltAngle =
                MathHelper.Lerp(_dashTiltAngle, 0f, 0.12f);
        }

        NPC.rotation = _dashTiltAngle;

        PlayMovementSound();

        // 传送冲刺要头朝玩家，不能被上面的「帽子技能统一归零」覆盖掉
        if (AIState == (float)ActionState.HatTeleportDash)
        {
            NPC.rotation = _omniDashRotation;
        }

        // 只要在移动就留残影（冲刺时更浓），停下后迅速淡出
        if (NPC.velocity.Length() > 3.5f
            && AIState != (float)ActionState.HatCatch)
        {
            UpdateDashTrail();
        }
        else if (AIState != (float)ActionState.Dash
            && _dashTrailCount > 0)
        {
            _dashTrailCount--;
        }
    }

    public override void PostAI()
    {
        UpdateContactHitbox();

        UpdateContactDamage();
    }

    /// <summary>
    /// 冲刺 / 传送冲刺期间给上接触伤害，其余时间不给。
    /// </summary>
    /// <remarks>
    /// 只写 <see cref="NPC.damage"/>，命中后的防御扣减、难度换算与无敌帧都交给原版。
    /// </remarks>
    private void UpdateContactDamage()
    {
        bool isDashing =
            AIState == (float)ActionState.Dash
            || AIState == (float)ActionState.HatTeleportDash;

        NPC.damage = isDashing
            ? GameModeDamage.Apply(FarSeaDamage.Dash)
            : 0;
    }

    /// <summary>
    /// 让碰撞/伤害判定框跟着贴图姿态走：竖版站姿 120x600，横放贴图按旋转后的横条包围盒。
    /// </summary>
    private void UpdateContactHitbox()
    {
        bool horizontal =
            AIState == (float)ActionState.Dash
            || AIState == (float)ActionState.Charge
            || AIState == (float)ActionState.HatTeleportDash;

        Vector2 center = NPC.Center;

        int targetWidth;
        int targetHeight;

        if (horizontal)
        {
            float angle = NPC.rotation;
            float cos = (float)Math.Abs(Math.Cos(angle));
            float sin = (float)Math.Abs(Math.Sin(angle));

            targetWidth =
                (int)Math.Round(DashHitboxLength * cos + DashHitboxThickness * sin);
            targetHeight =
                (int)Math.Round(DashHitboxLength * sin + DashHitboxThickness * cos);
        }
        else
        {
            targetWidth = UprightHitboxWidth;
            targetHeight = UprightHitboxHeight;
        }

        if (NPC.width == targetWidth && NPC.height == targetHeight)
        {
            return;
        }

        NPC.width = targetWidth;
        NPC.height = targetHeight;
        // 改尺寸后把中心点钉回去，避免整只往右下角漂
        NPC.Center = center;
    }

    // =========================================================
    // 冲刺残影
    // =========================================================

    // 每 tick 把当前位置与倾斜角压入环形采样，冲刺结束后在 PreDraw 里淡出绘制
    private void UpdateDashTrail()
    {
        for (int i = DashTrailLength - 1; i > 0; i--)
        {
            _dashTrailPositions[i] =
                _dashTrailPositions[i - 1];

            _dashTrailRotations[i] =
                _dashTrailRotations[i - 1];
        }

        _dashTrailPositions[0] = NPC.Center;
        _dashTrailRotations[0] = NPC.rotation;

        if (_dashTrailCount < DashTrailLength)
        {
            _dashTrailCount++;
        }
    }

    private void DrawDashTrail(
        SpriteBatch spriteBatch,
        Texture2D texture,
        Vector2 screenPos)
    {
        if (_dashTrailCount <= 1)
        {
            return;
        }

        Vector2 origin =
            new(
                texture.Width / 2f,
                texture.Height / 2f);

        SpriteEffects effects =
            NPC.spriteDirection == 1
                ? SpriteEffects.FlipHorizontally
                : SpriteEffects.None;

        for (int i = _dashTrailCount - 1; i >= 1; i--)
        {
            // 越旧的采样越淡、越小，形成向后拖的残影
            float progress =
                (float)i / _dashTrailCount;

            float alpha =
                DashTrailMaxAlpha
                * (1f - progress)
                * (AIState == (float)ActionState.Dash ? 1f : MoveTrailAlphaScale);

            if (alpha <= 0f)
            {
                continue;
            }

            spriteBatch.Draw(
                texture,
                _dashTrailPositions[i] - screenPos,
                null,
                new Color(190, 150, 255) * alpha,
                _dashTrailRotations[i],
                origin,
                NPC.scale * (1f - DashTrailShrink * progress),
                effects,
                0f);
        }
    }

    // =========================================================
    // 趋近中央
    // =========================================================

    private void ApproachingCenterLogic(Player player)
    {
        Vector2 center = GetSubworldCenter();
        Vector2 toTarget = center - NPC.Center;

        Vector2 desired =
            toTarget.SafeNormalize(Vector2.Zero)
            * ApproachMaxSpeed;

        NPC.velocity =
            Vector2.Lerp(
                NPC.velocity,
                desired,
                ApproachAccel);

        NPC.spriteDirection =
            player.Center.X < NPC.Center.X
                ? 1
                : -1;

        if (Vector2.Distance(NPC.Center, center)
            < ApproachThreshold)
        {
            NPC.Center = center;
            NPC.velocity = Vector2.Zero;

            SuckAttackX = center.X;

            // 第二阶段不再进入吸取态，直接回到常规走位
            bool enterSuck = !_secondPhaseStarted;

            AIState = enterSuck
                ? (float)ActionState.SuckAttack
                : (float)ActionState.MovingToSide;

            if (enterSuck
                && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Mod.Logger.Info("远海伯爵开始吸取 / Far Sea Earl started the suck attack");
            }

            AITimer = 0;
            BlastFired = false;
            _currentAnimFrame = 0;

            _jellyfishTimer = 0;
            _jellyfishWave = 0;

            NPC.netUpdate = true;
        }
    }

    // =========================================================
    // 第二阶段网格弹幕
    // =========================================================

    // 转阶段处理：血量第一次掉到一半时进入第二阶段；如果那一刻正在释放技能，
    // 就先锁血，等这次技能自然放完再解锁，绝不打断技能。
    private void UpdatePhaseTransition(float healthPercent)
    {
        if (_secondPhaseStarted)
        {
            // 第二阶段已确认：锁血只维持到这次技能结束
            if (_healthLocked
                && !IsSkillInProgress())
            {
                _healthLocked = false;
            }

            return;
        }

        if (healthPercent > SecondPhaseHealthPercent)
        {
            return;
        }

        _secondPhaseStarted = true;

        // 转阶段本身不播任何过场、也不无敌：丢帽动作是「帽子技能的前摇」，
        // 只在真正起手扔帽子时才播（见 TryStartHatAttack / UpdateHatOffState）

        // 进入第二阶段的那一刻立刻来一发网格，之后按 5 秒间隔循环
        _gridAttackTimer = GridAttackInterval;

        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            Mod.Logger.Info(
                $"远海伯爵进入第二阶段（当前状态 {AIState}） / Far Sea Earl entered phase 2 (state {AIState})");
        }

        // 吸取 / 趋近中心途中被打到转阶段：保持无敌，等这次吸取自然放完再解除
        if (AIState == (float)ActionState.SuckAttack
            || AIState == (float)ActionState.ApproachingCenter)
        {
            _healthLocked = true;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Mod.Logger.Info("转阶段时正在吸取，保持无敌直到吸取结束 / phase 2 shift during suck keeps invulnerability");
            }

            return;
        }

        _healthLocked = false;
    }

    // 当前是否正在释放技能；只有这些状态算技能，移动 / 跟随不算
    private bool IsSkillInProgress()
    {
        return AIState == (float)ActionState.Charge
            || AIState == (float)ActionState.Dash
            || AIState == (float)ActionState.SuckAttack
            || AIState == (float)ActionState.ApproachingCenter
            || AIState == (float)ActionState.HatThrowTrack
            || AIState == (float)ActionState.HatCatch
            || AIState == (float)ActionState.HatTeleportDash;
    }

    // 帽子技能前摇：帽子被掀起（HatOffHoldFrames）→ 帽子飞走（HatOffFlyFrames），
    // 播完之后才真正进入扔帽子的技能状态（由状态机在 AITimer == 1 时生成弹幕）
    private void UpdateHatOffState()
    {
        if (_hatOffTimer <= 0)
        {
            return;
        }

        // 先递减再判定，保证一定会走到 0（用 == 1 判断时只要有一帧没被调用就会永远错过）
        _hatOffTimer--;

        if (_hatOffTimer > 0)
        {
            return;
        }

        AIState = (float)ActionState.HatThrowTrack;

        AITimer = 0;
        _currentAnimFrame = 0;

        if (!Main.dedServ)
        {
            Mod.Logger.Info(
                "帽子前摇结束 / hat wind-up finished, variant="
                + _hatWindupVariant);
        }

        // 前摇结束的这一瞬间就把帽子甩出去，不再等状态机下一帧
        if (!_hatSpawnedThisThrow)
        {
            SpawnHatProjectile(
                _hatWindupVariant == 1
                    ? Projectiles.FarSeaHatProjectile.ModeLaser
                    : Projectiles.FarSeaHatProjectile.ModeTrack);
        }

        NPC.netUpdate = true;
    }

    private void UpdateGridAttack(Player player)
    {
        // 进入第二阶段且不再锁血（那次技能已放完）后，每 5 秒放一次网格
        if (!_secondPhaseStarted || _healthLocked)
        {
            _gridAttackTimer = 0;

            return;
        }

        // 丢帽动画期间暂停计数，避免预警弹幕和过场重叠
        if (_hatOffTimer > 0)
        {
            return;
        }

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        _gridAttackTimer++;

        if (_gridAttackTimer < GridAttackInterval)
        {
            return;
        }

        _gridAttackTimer = 0;

        Mod.Logger.Info("释放网格弹幕 / grid attack fired");

        Projectile.NewProjectile(
            NPC.GetSource_FromAI(),
            player.Center,
            Vector2.Zero,
            ModContent.ProjectileType<FarSeaGridProjectile>(),
            0,
            0f,
            Main.myPlayer);
    }

    // =========================================================
    // 吸取态
    // =========================================================

    private void SuckAttackLogic(Player player)
    {
        NPC.velocity = Vector2.Zero;

        if (AITimer <= SuckFramesEnd)
        {
            _currentAnimFrame =
                Math.Min(
                    (int)((AITimer - 1f) / 13f),
                    9);
        }
        else if (AITimer <= SuckHoldEnd)
        {
            _currentAnimFrame = 10;
        }
        else if (AITimer <= SuckFrame11End)
        {
            _currentAnimFrame = 11;
        }
        else
        {
            _currentAnimFrame = 12;
        }

        NPC.spriteDirection =
            player.Center.X < NPC.Center.X
                ? 1
                : -1;

        UpdateSuckParticles();

        UpdateSuckAura();

        SpawnFarSeaSuckOrbs(player);

        if (Main.myPlayer == NPC.target)
        {
            if (AITimer > SuckFrame11End
                && !BlastFired)
            {
                BlastFired = true;

                Vector2 blastDir =
                    (player.Center - NPC.Center)
                    .SafeNormalize(Vector2.UnitX);

                blastDir.Y -= BlastUpwardBias;
                blastDir.Normalize();

                player.velocity =
                    blastDir * BlastOutSpeed;

                if (!Main.dedServ)
                {
                    BlastOutExistingDust();
                    SpawnBlastOutDust();
                }
            }

            if (AITimer <= SuckFrame11End)
            {
                ApplySuckForce(player);
            }
        }

        if (AITimer >= SuckAnimDuration)
        {
            BlastFired = false;
            _suckAuraTimer = 0;

            AIState =
                (float)ActionState.MovingToSide;

            AITimer = 0;
            DashCounter = 0;
            _currentAnimFrame = 0;

            _jellyfishTimer = 0;
            _jellyfishWave = 0;

            NPC.netUpdate = true;
        }
    }

    // =========================================================
    // 生成横纵交替弹幕 + 追踪弹幕
    // =========================================================

    private void SpawnFarSeaSuckOrbs(Player target)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        if (target == null ||
            !target.active ||
            target.dead)
        {
            return;
        }

        _jellyfishTimer++;

        if (_jellyfishTimer < 32)
        {
            return;
        }

        _jellyfishTimer = 0;

        _jellyfishWave++;

        const float horizontalOffsetY = 105f;

        const float verticalOffsetX = 105f;

        const float horizontalSpawnDistance = 520f;

        const float verticalSpawnDistance = 420f;

        const float homingSpawnDistance = 600f;

        bool reversePattern =
            _jellyfishWave % 2 == 0;

        Vector2 horizontalSpawnPosition;

        Vector2 verticalSpawnPosition;

        float horizontalDirection;

        float verticalDirection;

        if (!reversePattern)
        {
            horizontalSpawnPosition =
                target.Center
                + new Vector2(
                    -horizontalSpawnDistance,
                    -horizontalOffsetY);

            horizontalDirection = 1f;

            verticalSpawnPosition =
                target.Center
                + new Vector2(
                    verticalOffsetX,
                    verticalSpawnDistance);

            verticalDirection = -1f;
        }
        else
        {
            horizontalSpawnPosition =
                target.Center
                + new Vector2(
                    horizontalSpawnDistance,
                    horizontalOffsetY);

            horizontalDirection = -1f;

            verticalSpawnPosition =
                target.Center
                + new Vector2(
                    -verticalOffsetX,
                    -verticalSpawnDistance);

            verticalDirection = 1f;
        }

        // 追踪弹幕从随机角度生成
        float homingAngle =
            Main.rand.NextFloat(MathHelper.TwoPi);

        Vector2 homingSpawnPosition =
            target.Center
            + homingAngle.ToRotationVector2()
            * homingSpawnDistance;

        int projectileType =
            ModContent.ProjectileType<
                FarSeaJellyfishProjectile>();

        // 生成横向弹幕
        int horizontalProjectile =
            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                horizontalSpawnPosition,
                Vector2.Zero,
                projectileType,
                0,
                0f,
                Main.myPlayer,
                0f,
                horizontalDirection);

        // 生成纵向弹幕
        int verticalProjectile =
            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                verticalSpawnPosition,
                Vector2.Zero,
                projectileType,
                0,
                0f,
                Main.myPlayer,
                1f,
                verticalDirection);

        // 生成追踪弹幕
        int homingProjectile =
            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                homingSpawnPosition,
                Vector2.Zero,
                projectileType,
                0,
                0f,
                Main.myPlayer,
                2f,
                NPC.target);

        if (horizontalProjectile >= 0 &&
            horizontalProjectile < Main.maxProjectiles)
        {
            Main.projectile[horizontalProjectile].netUpdate = true;
        }

        if (verticalProjectile >= 0 &&
            verticalProjectile < Main.maxProjectiles)
        {
            Main.projectile[verticalProjectile].netUpdate = true;
        }

        if (homingProjectile >= 0 &&
            homingProjectile < Main.maxProjectiles)
        {
            Main.projectile[homingProjectile].netUpdate = true;
        }
    }

    // =========================================================
    // 爆推出已有吸取粒子
    // =========================================================

    private void BlastOutExistingDust()
    {
        Vector2 center = NPC.Center;

        for (int i = 0; i < Main.maxDust; i++)
        {
            Dust dust = Main.dust[i];

            if (!dust.active)
            {
                continue;
            }

            bool isSuckDust = false;

            for (int t = 0; t < SuckDustTypes.Length; t++)
            {
                if (dust.type == SuckDustTypes[t])
                {
                    isSuckDust = true;
                    break;
                }
            }

            if (!isSuckDust)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    dust.position,
                    center);

            if (distance > BlastDustRadius)
            {
                continue;
            }

            float closeness =
                MathHelper.Clamp(
                    1f - distance / BlastDustRadius,
                    0f,
                    1f);

            float outSpeed =
                MathHelper.Lerp(
                    8f,
                    BlastDustOutSpeed,
                    closeness);

            Vector2 outDirection =
                distance > 1f
                    ? (dust.position - center) / distance
                    : Main.rand.NextVector2Unit();

            dust.velocity =
                outDirection * outSpeed;

            dust.noGravity = true;
            dust.scale *= Main.rand.NextFloat(1.1f, 1.5f);
            dust.fadeIn = 0f;
        }
    }

    // =========================================================
    // 爆推出新粒子
    // =========================================================

    private void SpawnBlastOutDust()
    {
        for (int i = 0; i < 40; i++)
        {
            float angle =
                MathHelper.TwoPi * i / 40f
                + Main.rand.NextFloat(-0.08f, 0.08f);

            Vector2 direction =
                angle.ToRotationVector2();

            float speed =
                Main.rand.NextFloat(12f, 28f);

            int dustType;

            float roll =
                Main.rand.NextFloat();

            if (roll < 0.5f)
            {
                dustType = DustID.GemSapphire;
            }
            else if (roll < 0.85f)
            {
                dustType = DustID.GemDiamond;
            }
            else
            {
                dustType = DustID.BlueFlare;
            }

            Color tint =
                dustType == DustID.GemDiamond
                    ? new Color(215, 240, 255)
                    : new Color(95, 165, 255);

            Dust dust =
                Dust.NewDustPerfect(
                    NPC.Center
                    + direction
                    * Main.rand.NextFloat(20f, 80f),
                    dustType,
                    direction * speed,
                    Alpha: 0,
                    newColor: tint,
                    Scale: Main.rand.NextFloat(1.2f, 2.2f));

            dust.noGravity = true;
            dust.fadeIn = 0.4f;
        }
    }

    // =========================================================
    // 吸取态粒子
    // =========================================================

    private void UpdateSuckParticles()
    {
        if (Main.dedServ)
        {
            return;
        }

        float intensity;

        if (_currentAnimFrame == 10)
        {
            intensity = 1f;
        }
        else if (_currentAnimFrame < 10)
        {
            intensity =
                MathHelper.Lerp(
                    0.35f,
                    0.9f,
                    _currentAnimFrame / 9f);
        }
        else
        {
            intensity =
                _currentAnimFrame == 11
                    ? 0.7f
                    : 0.4f;
        }

        SpawnSuckDust(intensity);
        PullSuckDust(intensity);

        float pulse =
            0.85f
            + 0.15f
            * (float)Math.Sin(
                Main.GlobalTimeWrappedHourly * 6f);

        float strength =
            intensity * pulse;

        Lighting.AddLight(
            NPC.Center,
            new Vector3(0.20f, 0.42f, 0.85f)
            * strength);

        Lighting.AddLight(
            NPC.Top
            + new Vector2(0f, NPC.height * 0.25f),
            new Vector3(0.16f, 0.36f, 0.75f)
            * strength
            * 0.8f);

        Lighting.AddLight(
            NPC.Bottom
            - new Vector2(0f, NPC.height * 0.25f),
            new Vector3(0.16f, 0.36f, 0.75f)
            * strength
            * 0.8f);
    }

    private void SpawnSuckDust(float intensity)
    {
        int count =
            (int)Math.Round(
                MathHelper.Lerp(
                    2f,
                    6f,
                    intensity));

        if (Main.rand.NextFloat() < intensity - 0.5f)
        {
            count++;
        }

        for (int i = 0; i < count; i++)
        {
            float angle =
                Main.rand.NextFloat(
                    MathHelper.TwoPi);

            Vector2 direction =
                angle.ToRotationVector2();

            float ringX =
                NPC.width * 0.5f
                + Main.rand.NextFloat(140f, 560f);

            float ringY =
                NPC.height * 0.5f
                + Main.rand.NextFloat(140f, 460f);

            Vector2 spawnPosition =
                NPC.Center
                + new Vector2(
                    direction.X * ringX,
                    direction.Y * ringY);

            Vector2 toCenter =
                NPC.Center - spawnPosition;

            float distance =
                toCenter.Length();

            Vector2 velocity =
                toCenter.SafeNormalize(Vector2.Zero)
                * MathHelper.Lerp(
                    4f,
                    12f,
                    MathHelper.Clamp(
                        distance / 700f,
                        0f,
                        1f));

            int dustType;

            float roll =
                Main.rand.NextFloat();

            if (roll < 0.5f)
            {
                dustType = DustID.GemSapphire;
            }
            else if (roll < 0.85f)
            {
                dustType = DustID.GemDiamond;
            }
            else
            {
                dustType = DustID.BlueFlare;
            }

            Color tint =
                dustType == DustID.GemDiamond
                    ? new Color(215, 240, 255)
                    : new Color(95, 165, 255);

            Dust dust =
                Dust.NewDustPerfect(
                    spawnPosition,
                    dustType,
                    velocity,
                    Alpha: 0,
                    newColor: tint,
                    Scale:
                        Main.rand.NextFloat(1f, 1.9f)
                        * MathHelper.Lerp(
                            0.85f,
                            1.15f,
                            intensity));

            dust.noGravity = true;
            dust.fadeIn = 0.5f;
            dust.noLight = false;
        }
    }

    private void PullSuckDust(float intensity)
    {
        Vector2 center = NPC.Center;

        for (int i = 0; i < Main.maxDust; i++)
        {
            Dust dust = Main.dust[i];

            if (!dust.active)
            {
                continue;
            }

            bool isSuckDust = false;

            for (int t = 0; t < SuckDustTypes.Length; t++)
            {
                if (dust.type == SuckDustTypes[t])
                {
                    isSuckDust = true;
                    break;
                }
            }

            if (!isSuckDust)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    dust.position,
                    center);

            if (distance > DustInfluenceRadius)
            {
                continue;
            }

            dust.noGravity = true;

            float closeness =
                MathHelper.Clamp(
                    1f - distance / DustInfluenceRadius,
                    0f,
                    1f);

            float targetSpeed =
                MathHelper.Lerp(
                    8f,
                    DustMaxInwardSpeed,
                    closeness)
                * intensity;

            Vector2 inward =
                (center - dust.position)
                .SafeNormalize(Vector2.Zero)
                * targetSpeed;

            dust.velocity =
                Vector2.Lerp(
                    dust.velocity,
                    inward,
                    MathHelper.Lerp(
                        0.06f,
                        0.22f,
                        closeness));

            if (distance < DustSwallowRadius)
            {
                dust.scale *= 0.88f;

                if (dust.scale < 0.45f)
                {
                    dust.active = false;
                }
            }
        }
    }

    // =========================================================
    // 玩家吸引力
    // =========================================================

    private void ApplySuckForce(Player player)
    {
        Vector2 toNPC =
            NPC.Center - player.Center;

        float distance =
            toNPC.Length();

        if (distance < SuckMinDistance)
        {
            return;
        }

        Vector2 pullDirection =
            toNPC / distance;

        player.velocity.X +=
            pullDirection.X * SuckPullAccelX;

        player.velocity.Y +=
            pullDirection.Y * SuckPullAccelY;

        Vector2 inputDirection =
            Vector2.Zero;

        if (player.controlLeft)
        {
            inputDirection.X -= 1f;
        }

        if (player.controlRight)
        {
            inputDirection.X += 1f;
        }

        if (player.controlUp)
        {
            inputDirection.Y -= 1f;
        }

        if (player.controlDown)
        {
            inputDirection.Y += 1f;
        }

        if (inputDirection != Vector2.Zero)
        {
            inputDirection.Normalize();

            float dot =
                Vector2.Dot(
                    inputDirection,
                    pullDirection);

            if (dot < 0f)
            {
                float resistScale =
                    SuckResistAccel * -dot;

                player.velocity.X -=
                    pullDirection.X * resistScale;

                player.velocity.Y -=
                    pullDirection.Y * resistScale;
            }
        }

        float speedToward =
            Vector2.Dot(
                player.velocity,
                pullDirection);

        if (speedToward > SuckPullMaxSpeed)
        {
            player.velocity -=
                pullDirection
                * (speedToward - SuckPullMaxSpeed);
        }
    }

    // =========================================================
    // 移动到玩家侧面
    // =========================================================

    private void MovingToSideLogic(Player player)
    {
        if (AITimer == 1)
        {
            // 哪一侧近就在哪一侧起手蓄力，不再随机挑边绕到对面
            TargetSide =
                NPC.Center.X <= player.Center.X
                    ? -1f
                    : 1f;

            // 间距只在这一刻定一次，之后不再按当前位置反复重算（那会造成一卡一卡）
            float currentSideDistance =
                Math.Abs(NPC.Center.X - player.Center.X);

            _targetSideDistance =
                MathHelper.Clamp(
                    currentSideDistance,
                    MinChargeSideDistance,
                    MaxChargeSideDistance);
        }

        Vector2 targetPosition =
            new(
                player.Center.X + TargetSide * _targetSideDistance,
                player.Center.Y);

        SmoothMoveTo(targetPosition, 16f, 0.08f);

        NPC.spriteDirection =
            player.Center.X < NPC.Center.X
                ? 1
                : -1;

        // 不再要求精确对准：位置大差不差、或者等够时间就直接进跟随/蓄力
        if (AITimer >= 75
            || (Vector2.Distance(NPC.Center, targetPosition) <= 260f
                && AITimer >= 24))
        {
            AIState =
                (float)ActionState.FollowingPlayer;

            AITimer = 0;
            NPC.netUpdate = true;
        }
    }

    // 统一的速度插值移动：不再有「距离阈值切换 + 速度硬重置」，日常移动是连续的
    private void SmoothMoveTo(Vector2 target, float maxSpeed, float damping)
    {
        Vector2 offset = target - NPC.Center;

        float distance = offset.Length();

        Vector2 desired =
            distance > 1f
                ? offset / distance
                    * MathHelper.Clamp(distance * 0.08f, 0f, maxSpeed)
                : Vector2.Zero;

        NPC.velocity =
            Vector2.Lerp(NPC.velocity, desired, damping);
    }

    // =========================================================
    // 跟随玩家
    // =========================================================

    private void FollowingPlayerLogic(Player player)
    {
        // 跟随阶段沿用移动阶段定下的间距，用同一套插值移动
        Vector2 targetPosition =
            new(
                player.Center.X + TargetSide * _targetSideDistance,
                player.Center.Y);

        SmoothMoveTo(targetPosition, 20f, 0.12f);

        NPC.spriteDirection =
            player.Center.X < NPC.Center.X
                ? 1
                : -1;

        if (AITimer >= FollowDuration)
        {
            AIState =
                (float)ActionState.Charge;

            AITimer = 0;
            DashStartY = NPC.Center.Y;

            NPC.netUpdate = true;
        }
    }

    // =========================================================
    // 冲刺蓄力
    // =========================================================

    private void ChargePreparation(Player player)
    {
        NPC.velocity.Y *= 0.8f;
        NPC.velocity.X *= 0.95f;

        float yDifference =
            DashStartY - NPC.Center.Y;

        if (Math.Abs(yDifference) > 5f)
        {
            NPC.velocity.Y += yDifference * 0.1f;
        }

        NPC.spriteDirection =
            player.Center.X < NPC.Center.X
                ? 1
                : -1;

        if (AITimer >= ChargeDuration)
        {
            AIState =
                (float)ActionState.Dash;

            AITimer = 0;

            SoundEngine.PlaySound(
                new SoundStyle("sea/Assets/Sounds/Rush")
                {
                    Volume = 0.7f,
                    PitchVariance = 0.2f
                },
                NPC.Center);

            NPC.netUpdate = true;
        }
    }

    // =========================================================
    // 冲刺
    // =========================================================

    // =========================================================
    // 帽子技能
    // =========================================================

    private void UpdateHatProjectileUpkeep()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        if (_hatProjectileIndex < 0)
        {
            return;
        }

        if (_hatProjectileIndex >= Main.maxProjectiles)
        {
            _hatProjectileIndex = -1;
            _hatDied = true;

            return;
        }

        Projectile hat = Main.projectile[_hatProjectileIndex];

        if (!hat.active || hat.type != ModContent.ProjectileType<Projectiles.FarSeaHatProjectile>())
        {
            _hatProjectileIndex = -1;
            _hatDied = true;

            if (!Main.dedServ)
            {
                Mod.Logger.Info(
                    "帽子弹幕已消失 / hat projectile gone, airTicks="
                    + _hatAirTicks);
            }
        }
    }

    // 帽子飞回来 / 消失之后：飞够时间才收回戴上，飞不够就当丢了
    private void PlayMovementSound()
    {
        if (Main.dedServ || !NPC.active)
        {
            return;
        }

        // 帽子在外面旋转期间一直有风声（激光帽有自己的激光音，不放这条）
        if (_hatProjectileIndex >= 0
            && _hatMode == Projectiles.FarSeaHatProjectile.ModeTrack
            && Main.GameUpdateCount % WhooshLoopTicks == 0)
        {
            SoundEngine.PlaySound(HatWhooshSound, NPC.Center);
        }

        // 吸取态另有一套音效，避免两条声音叠在一起
        if (AIState == (float)ActionState.SuckAttack)
        {
            if (Main.GameUpdateCount % SuckLoopTicks == 0)
            {
                SoundEngine.PlaySound(SuckFieldSound, NPC.Center);
            }

            return;
        }

        if (Main.GameUpdateCount % WaterLoopTicks != 0)
        {
            return;
        }

        bool movingState =
            AIState == (float)ActionState.MovingToSide
            || AIState == (float)ActionState.FollowingPlayer
            || AIState == (float)ActionState.ApproachingCenter;

        if (!movingState)
        {
            return;
        }

        if (NPC.velocity.Length() < 1.2f)
        {
            return;
        }

        SoundEngine.PlaySound(MoveWaterSound, NPC.Center);
    }

    private void UpdateHatRecallWatch()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        if (!_hatDied)
        {
            return;
        }

        _hatDied = false;

        // 帽子压根没飞出去（只是播了前摇）就不要触发收帽动画
        if (!_hatSpawnedThisThrow)
        {
            return;
        }

        if (_hatOffTimer > 0
            || AIState == (float)ActionState.HatCatch)
        {
            return;
        }

        int minTicks =
            _hatMode == Projectiles.FarSeaHatProjectile.ModeExpel
                ? ExpelMinAirTicks
                : HatMinAirTicks;

        if (_hatAirTicks < minTicks)
        {
            return;
        }

        AIState = (float)ActionState.HatCatch;
        AITimer = 0;
        _hatCatchTimer = 0;
        _hatSpawnedThisThrow = false;
        _hatAttackCooldown = HatAttackCooldown;

        if (!Main.dedServ)
        {
            Mod.Logger.Info(
                "伯爵开始收帽子 / earl is putting the hat back on, airTicks="
                + _hatAirTicks);
        }

        NPC.netUpdate = true;
    }

    // 帽子技能：第一阶段专用（50% 后帽子已经飞出去，不再起手）
    private void TryStartHatAttack()
    {
        if (_hatAttackCooldown > 0)
        {
            _hatAttackCooldown--;
        }

        if (!_secondPhaseStarted
            || _hatProjectileIndex >= 0
            || _hatCatchTimer > 0
            || _hatOffTimer > 0
            || _healthLocked
            || _hatAttackCooldown > 0
            || AITimer < 10
            || IsSkillInProgress())
        {
            return;
        }

        if (AIState != (float)ActionState.MovingToSide
            && AIState != (float)ActionState.FollowingPlayer)
        {
            return;
        }

        if (Vector2.Distance(NPC.Center, Main.player[NPC.target].Center)
            > 80f * 16f)
        {
            return;
        }

        // 二阶段节奏：冲刺满 3 次才允许扔帽子
        if (_dashesSinceHat < HatDashesBeforeThrow)
        {
            return;
        }

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        _hatThrowCount++;

        // 每第 3 次扔帽改成激光帽：0 = 追踪帽，1 = 激光帽
        _hatAttackVariant = _hatThrowCount % 3 == 0 ? 1 : 0;
        _hatAttackCooldown = HatAttackCooldown;
        _dashesSinceHat = 0;

        // 先播丢帽前摇，播完（UpdateHatOffState）再切进真正的技能状态
        _hatWindupVariant = _hatAttackVariant;
        _hatSpawnedThisThrow = false;
        _hatAirTicks = 0;
        _hatLastCenter = Vector2.Zero;
        _hatLastVelocity = Vector2.Zero;
        _hatOffTimer = HatOffHoldFrames + HatOffFlyFrames;
        NPC.velocity = Vector2.Zero;

        if (!Main.dedServ)
        {
            Mod.Logger.Info("远海伯爵掀起帽子准备投掷 / earl winds up the hat throw");
        }

        NPC.netUpdate = true;
    }

    /// <summary>
    ///     直接找一个空槽位手动装配帽子弹幕。
    ///     <c>Projectile.NewProjectile</c> 偶发返回无效索引（同帧被顶替/容量判定），
    ///     而帽子又是技能的关键物件，所以留一条完全绕过它的通道。
    /// </summary>
    /// <param name="spawnPosition">出手位置。</param>
    /// <param name="velocity">初始速度。</param>
    /// <param name="mode">帽子模式。</param>
    /// <returns>分配到的槽位索引，失败返回 -1。</returns>
    private int TryAllocateHatManually(
        Vector2 spawnPosition,
        Vector2 velocity,
        int mode)
    {
        int hatType = ModContent.ProjectileType<Projectiles.FarSeaHatProjectile>();

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile candidate = Main.projectile[i];

            if (candidate.active)
            {
                continue;
            }

            candidate.SetDefaults(hatType);

            candidate.position = spawnPosition - candidate.Size * 0.5f;
            candidate.velocity = velocity;
            candidate.owner = Main.myPlayer;
            candidate.damage = 0;
            candidate.knockBack = 0f;
            candidate.ai[0] = mode;
            candidate.ai[1] = NPC.whoAmI;
            candidate.netUpdate = true;
            candidate.active = true;

            return i;
        }

        return -1;
    }

    private void SpawnHatProjectile(int mode)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        Player player = Main.player[NPC.target];

        Vector2 toPlayer = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitX);

        Vector2 velocity = toPlayer * 13f;

        // 从身体前方一点点出手，避免和贴身的玩家/本体完全重叠
        Vector2 spawnPosition = NPC.Center + toPlayer * 26f;

        // 补发时沿用上一帧帽子的位置和方向，视觉上无缝衔接
        if (_hatLastCenter != Vector2.Zero)
        {
            spawnPosition = _hatLastCenter;

            Vector2 lastDirection = _hatLastVelocity.SafeNormalize(Vector2.UnitX);

            velocity = lastDirection * MathHelper.Max(9f, _hatLastVelocity.Length());
        }

        int index = Projectile.NewProjectile(
            NPC.GetSource_FromAI(),
            spawnPosition,
            velocity,
            ModContent.ProjectileType<Projectiles.FarSeaHatProjectile>(),
            0,
            0f,
            Main.myPlayer,
            mode,
            NPC.whoAmI);

        int hatType = ModContent.ProjectileType<Projectiles.FarSeaHatProjectile>();

        // NewProjectile 在弹幕满了的时候会返回 Main.maxProjectiles（无效索引）
        if (index < 0 || index >= Main.maxProjectiles)
        {
            // 兜底：自己找一个空槽位手动装配，绕开 NewProjectile 的各种拒绝
            index = TryAllocateHatManually(spawnPosition, velocity, mode);

            if (index < 0)
            {
                _hatProjectileIndex = -1;
                _hatSpawnedThisThrow = false;

                Mod.Logger.Warn(
                    "帽子弹幕创建失败（索引 "
                    + index
                    + "），下一帧重试 / hat projectile creation failed");

                return;
            }

            Mod.Logger.Info(
                "帽子弹幕手动占位成功 / hat projectile allocated manually, index "
                + index);
        }

        // 索引有效但那一格已经不是我们的帽子（同帧被顶替）：扫一遍把刚生成的那发认领回来
        if (!Main.projectile[index].active
            || Main.projectile[index].type != hatType)
        {
            int adopted = -1;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile candidate = Main.projectile[i];

                if (!candidate.active
                    || candidate.type != hatType
                    || candidate.owner != Main.myPlayer)
                {
                    continue;
                }

                if (Vector2.DistanceSquared(candidate.Center, spawnPosition) > 64f * 64f)
                {
                    continue;
                }

                adopted = i;

                break;
            }

            if (adopted < 0)
            {
                _hatProjectileIndex = -1;
                _hatSpawnedThisThrow = false;

                Mod.Logger.Warn(
                    "帽子弹幕创建后被顶替且未找到（索引 "
                    + index
                    + "），下一帧重试 / hat projectile creation failed");

                return;
            }

            index = adopted;
        }

        _hatProjectileIndex = index;
        _hatSpawnedThisThrow = true;
        _hatRespawnAttempts = 0;
        _hatAirTicks = 0;
        _hatMode = mode;
        _hatDied = false;

        if (!Main.dedServ)
        {
            SoundEngine.PlaySound(HatThrowSound, NPC.Center);
        }

        Mod.Logger.Info(
            "远海伯爵扔出帽子 / Far Sea Earl hurled the hat, mode "
            + mode
            + ", index "
            + index);

        NPC.netUpdate = true;
    }

    private void KillHatProjectile()
    {
        if (_hatProjectileIndex < 0)
        {
            return;
        }

        Projectile hat = Main.projectile[_hatProjectileIndex];

        if (hat.active)
        {
            hat.Kill();
        }

        _hatProjectileIndex = -1;
    }

    // 帽子飞出去期间：玩家靠近就后退，玩家远离就靠近，始终保持中距离
    private void EvadeWhileHatAway(Player player)
    {
        Vector2 toPlayer = player.Center - NPC.Center;
        float distance = toPlayer.Length();
        Vector2 direction = toPlayer.SafeNormalize(Vector2.UnitX);

        Vector2 desired;

        if (distance < EvadeNearDistance)
        {
            desired = -direction * EvadeSpeed;
        }
        else if (distance > EvadeFarDistance)
        {
            desired = direction * EvadeSpeed;
        }
        else
        {
            // 处在舒适距离内：缓慢平移保持滞空，不贴脸也不走远
            desired = new Vector2(direction.Y, -direction.X) * (EvadeSpeed * 0.35f);
        }

        NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.14f);

        // 别把自己甩到世界外面去
        float minX = EvadeEdgeMargin;
        float maxX = (Main.maxTilesX * 16f) - EvadeEdgeMargin;
        float minY = EvadeEdgeMargin;
        float maxY = (Main.maxTilesY * 16f) - EvadeEdgeMargin;

        if (NPC.Center.X < minX && NPC.velocity.X < 0f)
        {
            NPC.velocity.X = 0f;
        }
        else if (NPC.Center.X > maxX && NPC.velocity.X > 0f)
        {
            NPC.velocity.X = 0f;
        }

        if (NPC.Center.Y < minY && NPC.velocity.Y < 0f)
        {
            NPC.velocity.Y = 0f;
        }
        else if (NPC.Center.Y > maxY && NPC.velocity.Y > 0f)
        {
            NPC.velocity.Y = 0f;
        }

        NPC.spriteDirection =
            NPC.velocity.X < 0
                ? 1
                : -1;
    }

    private void HatThrowTrackLogic(Player player)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        if (!_hatSpawnedThisThrow)
        {
            SpawnHatProjectile(Projectiles.FarSeaHatProjectile.ModeTrack);

            // 生成失败兜底：超过 30 帧仍未扔出就再来一次
            if (!_hatSpawnedThisThrow
                && AITimer > 30)
            {
                SpawnHatProjectile(Projectiles.FarSeaHatProjectile.ModeTrack);
            }

            // 真的怎么都扔不出去：退出该状态，别把 BOSS 卡死在这里
            if (!_hatSpawnedThisThrow
                && AITimer > 90)
            {
                Mod.Logger.Warn(
                    "帽子始终无法生成，退出帽子状态 / hat could not be spawned, leaving state");

                AIState = (float)ActionState.MovingToSide;
                AITimer = 0;

                NPC.netUpdate = true;

                return;
            }
        }

        // 激光帽打满 3 轮脉冲：传送到帽子处 → 紫爆 → 无蓄力全向冲刺
        if (_hatMode == Projectiles.FarSeaHatProjectile.ModeLaser
            && _hatProjectileIndex >= 0
            && _hatAirTicks >= LaserTeleportTicks)
        {
            BeginHatTeleportDash();

            return;
        }

        // 帽子在外追踪的 10 秒里：只做「日常移动」躲玩家，不冲刺也不吸取
        if (_hatProjectileIndex >= 0
            && _hatAirTicks < HatMinAirTicks)
        {
            EvadeWhileHatAway(player);

            return;
        }

        if (_hatProjectileIndex < 0
            && _hatAirTicks < HatMinAirTicks)
        {
            // 帽子还没飞够就没了（出界 / 被销毁）：重新补一发，保证满 10 秒
            _hatRespawnAttempts++;

            if (_hatRespawnAttempts <= 3)
            {
                // 用原来那一发的模式补发（激光帽丢了还是补激光帽）
                SpawnHatProjectile(_hatMode);

                if (!Main.dedServ)
                {
                    Mod.Logger.Info(
                        "帽子提前消失，重新扔出 / hat vanished early, re-thrown, attempt="
                        + _hatRespawnAttempts);
                }

                // 补发期间仍然保持日常躲避移动，别僵在原地
                EvadeWhileHatAway(player);

                return;
            }

            // 反复补发也失败：退出帽子状态，避免 BOSS 永久卡在原地
            Mod.Logger.Warn(
                "帽子反复补发失败，退出帽子状态 / hat respawn kept failing, leaving state");

            _hatSpawnedThisThrow = false;
            _hatRespawnAttempts = 0;
            _hatAttackCooldown = HatAttackCooldown;

            AIState = (float)ActionState.MovingToSide;
            AITimer = 0;

            NPC.netUpdate = true;

            return;
        }

        // 追踪结束后原地静止，等帽子飞回来再进收帽动画
        NPC.velocity *= 0.85f;
    }
    // 激光帽三轮回合结束：传送到帽子处，紫爆一圈，随即无蓄力朝玩家冲刺
    private void BeginHatTeleportDash()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        Projectile hat = Main.projectile[_hatProjectileIndex];

        NPC.Center = hat.Center;

        SoundEngine.PlaySound(HatTeleportSound, NPC.Center);

        TeleportBurst(NPC.Center);

        hat.Kill();

        // 传送即戴帽：清空帽子在外的一切状态，贴图立刻回到「戴帽冲刺」
        _hatProjectileIndex = -1;
        _hatSpawnedThisThrow = false;
        _hatDied = false;
        _hatAirTicks = 0;
        _hatRespawnAttempts = 0;
        _omniDashStarted = false;
        _omniDashVelocity = Vector2.Zero;

        AIState = (float)ActionState.HatTeleportDash;
        AITimer = 0;

        NPC.velocity = Vector2.Zero;
        NPC.netUpdate = true;

        if (!Main.dedServ)
        {
            Mod.Logger.Info("伯爵传送至帽子处并瞬发冲刺 / earl teleported to the hat and lunged");
        }
    }

    private void HatTeleportDashLogic(Player player)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NPC.velocity *= 0.9f;

            return;
        }

        if (!_omniDashStarted)
        {
            Vector2 direction =
                (player.Center - NPC.Center).SafeNormalize(Vector2.UnitX);

            _omniDashVelocity = direction * OmniDashSpeed;
            _omniDashStarted = true;

            NPC.netUpdate = true;
        }

        NPC.velocity = _omniDashVelocity;

        // 头朝冲刺方向：贴图是横放、头在右，所以右半平面直接转，左半平面先左右翻转再补 π
        AimSpriteAt(_omniDashVelocity);

        if (AITimer >= OmniDashTicks)
        {
            NPC.velocity *= 0.3f;
            NPC.rotation = 0f;
            _omniDashStarted = false;
            _omniDashVelocity = Vector2.Zero;

            AIState = (float)ActionState.MovingToSide;
            AITimer = 0;
            DashCounter = 0;
            _hatAttackCooldown = HatAttackCooldown;

            NPC.netUpdate = true;
        }
    }

    private void AimSpriteAt(Vector2 direction)
    {
        if (direction.LengthSquared() < 0.0001f)
        {
            return;
        }

        float angle = direction.ToRotation();

        if (Math.Abs(angle) <= MathHelper.PiOver2)
        {
            NPC.spriteDirection = -1;
            _omniDashRotation = angle;

            return;
        }

        NPC.spriteDirection = 1;
        _omniDashRotation = MathHelper.WrapAngle(angle - MathHelper.Pi);
    }

    private void TeleportBurst(Vector2 center)
    {
        if (Main.dedServ)
        {
            return;
        }

        for (int i = 0; i < 48; i++)
        {
            float angle = MathHelper.TwoPi * i / 48f;
            Vector2 direction = angle.ToRotationVector2();

            Dust dust = Dust.NewDustPerfect(
                center + direction * 12f,
                DustID.PurpleTorch,
                direction * Main.rand.NextFloat(6f, 13f),
                100,
                new Color(206, 156, 255),
                Main.rand.NextFloat(1.4f, 2.4f));

            dust.noGravity = true;
        }

        for (int i = 0; i < 24; i++)
        {
            Dust dust = Dust.NewDustPerfect(
                center + Main.rand.NextVector2Circular(20f, 20f),
                DustID.VioletMoss,
                Main.rand.NextVector2Circular(7f, 7f),
                80,
                new Color(180, 120, 255),
                Main.rand.NextFloat(1.2f, 2.6f));

            dust.noGravity = true;
        }
    }

    private void HatCatchLogic(Player player)
    {
        NPC.velocity *= 0.9f;

        // 收帽动作刚开始的那一帧播布料声
        if (_hatCatchTimer == 0 && !Main.dedServ)
        {
            SoundEngine.PlaySound(HatCatchSound, NPC.Center);
        }

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        _hatCatchTimer++;

        if (_hatCatchTimer < HatCatchNoHatFrames + HatCatchHatOffFrames)
        {
            return;
        }

        _hatCatchTimer = 0;

        if (!Main.dedServ)
        {
            Mod.Logger.Info("伯爵戴好帽子 / earl has the hat back on");
        }

        AIState = (float)ActionState.MovingToSide;
        AITimer = 0;
        DashCounter = 0;

        NPC.netUpdate = true;
    }
    private void UpdateSuckAura()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        _suckAuraTimer++;

        Vector2 leftAura = GetSuckAuraCenter(-1f);
        Vector2 rightAura = GetSuckAuraCenter(1f);

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player target = Main.player[i];

            if (!target.active || target.dead)
            {
                continue;
            }

            // 按碰撞伤害结算：受击后 40 帧无敌由原版 Player.Hurt 提供，
            // 无敌期间不再重复结算，节奏和贴脸挨打一致。
            if (target.immune || target.immuneTime > 0)
            {
                continue;
            }

            float leftDistance =
                Vector2.Distance(target.Center, leftAura);

            float rightDistance =
                Vector2.Distance(target.Center, rightAura);

            if (leftDistance > SuckAuraRadius
                && rightDistance > SuckAuraRadius)
            {
                continue;
            }

            DamagePlayerWithSuckAura(target);
        }
    }

    /// <summary>
    /// 用警告圈结算一次碰撞伤害。
    /// </summary>
    /// <remarks>
    /// 走原版 <see cref="Player.Hurt"/>：难度倍率、防御、40 帧无敌都按原版规则来，
    /// 所以圈里站着只会按受击节奏掉血，不会每帧狂掉。
    /// </remarks>
    /// <param name="target">圈内的玩家。</param>
    private void DamagePlayerWithSuckAura(Player target)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        int baseDamage = (int)MathF.Round(
            FarSeaDamage.SuckAura * Main.GameModeInfo.EnemyDamageMultiplier);

        target.Hurt(
            PlayerDeathReason.ByNPC(NPC.whoAmI),
            baseDamage,
            target.Center.X < NPC.Center.X ? -1 : 1);
    }

    /// <summary>
    /// 左右触手那两圈警告圈的圆心偏移。<paramref name="side"/> 取 -1 是左、1 是右。
    /// </summary>
    /// <remarks>
    /// spriteDirection == 1 时贴图会水平翻转，圆心也要跟着镜像，否则圈会跑到反方向。
    /// </remarks>
    private Vector2 GetSuckAuraOffset(float side)
    {
        float flip =
            NPC.spriteDirection == 1
                ? -1f
                : 1f;

        Vector2 offset =
            side < 0f
                ? new Vector2(SuckAuraLeftOffsetX, SuckAuraLeftOffsetY)
                : new Vector2(SuckAuraRightOffsetX, SuckAuraRightOffsetY);

        offset.X *= flip;

        return offset;
    }

    /// <summary>
    /// 左右触手那两圈警告圈的中心点（世界坐标）。
    /// </summary>
    private Vector2 GetSuckAuraCenter(float side)
    {
        return NPC.Center + GetSuckAuraOffset(side);
    }

    private void DrawSuckAura(SpriteBatch spriteBatch, Vector2 center)
    {
        if (_suckAuraTexture is null || !_suckAuraTexture.IsLoaded)
        {
            return;
        }

        Texture2D texture = _suckAuraTexture.Value;

        float scale = SuckAuraRadius * 2f / texture.Width;
        Vector2 origin =
            new(texture.Width / 2f, texture.Height / 2f);

        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 auraCenter =
                center + GetSuckAuraOffset(side);

            spriteBatch.Draw(
                texture,
                auraCenter,
                null,
                Color.White * 0.9f,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0f);
        }
    }

    private void DashAttack(Player player)
    {
        float dashDirection =
            NPC.velocity.X < 0f ? -1f : 1f;

        if (AITimer == 1)
        {
            dashDirection =
                player.Center.X > NPC.Center.X
                    ? 1f
                    : -1f;

            NPC.velocity.X =
                dashDirection * DashSpeed;

            NPC.velocity.Y = 0f;
        }

        // 只在冲刺轴左右各 15 度的扇形内做小幅追踪，属于「一点点」的修正
        float axisAngle =
            dashDirection > 0f
                ? 0f
                : MathHelper.Pi;

        float desiredAngle =
            (float)Math.Atan2(
                player.Center.Y - NPC.Center.Y,
                player.Center.X - NPC.Center.X);

        float offset =
            MathHelper.WrapAngle(
                desiredAngle - axisAngle);

        offset = MathHelper.Clamp(
            offset,
            -DashHomingHalfAngle,
            DashHomingHalfAngle);

        float targetAngle = axisAngle + offset;

        float currentAngle =
            (float)Math.Atan2(
                NPC.velocity.Y,
                NPC.velocity.X);

        float steerOffset =
            MathHelper.WrapAngle(
                targetAngle - currentAngle);

        float newAngle =
            currentAngle + steerOffset * DashHomingStrength;

        // 头部跟随：旋转方向直接由「冲刺轴 → 实际方向」的偏转角决定。
        // 贴图水平翻转后头部换到另一侧，绕中心旋转时几何上会自动对齐，
        // 所以这里不能再按左右取反，否则头会朝反方向摆。
        float tiltOffset =
            MathHelper.WrapAngle(newAngle - axisAngle);

        float targetTilt =
            tiltOffset * DashTiltStrength;

        _dashTiltAngle =
            MathHelper.Lerp(
                _dashTiltAngle,
                targetTilt,
                DashTiltLerp);

        NPC.velocity.X =
            (float)Math.Cos(newAngle) * DashSpeed;

        NPC.velocity.Y =
            (float)Math.Sin(newAngle) * DashSpeed;

        NPC.spriteDirection =
            NPC.velocity.X < 0
                ? 1
                : -1;

        if (AITimer >= DashDuration)
        {
            DashCounter++;
            _dashesSinceHat++;

            if (DashCounter >= 4)
            {
                // 80% 血之前不派生吸取态；掉到 80% 以下之后（HasTriggeredSuckPhase）
                // 且还没进第二阶段时，4 次冲刺才接「趋近中心 → 吸取」的老循环
                bool deriveSuck =
                    HasTriggeredSuckPhase
                    && !_secondPhaseStarted;

                if (!deriveSuck)
                {
                    // 不派生吸取时把计数清零，让 Boss 继续正常循环冲刺
                    DashCounter = 0;
                }

                AIState = deriveSuck
                    ? (float)ActionState.ApproachingCenter
                    : (float)ActionState.MovingToSide;

                AITimer = 0;
                _currentAnimFrame = 0;

                NPC.netUpdate = true;
            }
            else
            {
                AIState =
                    (float)ActionState.MovingToSide;

                AITimer = 0;
                NPC.netUpdate = true;
            }
        }
    }

    // =========================================================
    // 动画
    // =========================================================

    public override void FindFrame(int frameHeight)
    {
    }

    /// <summary>
    /// 按当前阶段挑选贴图：二阶段用无帽版本，其余情况用带帽版本。
    /// </summary>
    /// <param name="hatAsset">带帽贴图，可为 null 表示直接用默认贴图</param>
    /// <param name="noHatAsset">无帽贴图</param>
    /// <param name="noHat">是否处于无帽阶段</param>
    /// <param name="fallback">兜底贴图</param>
    /// <returns>本帧要绘制的贴图</returns>
    private static Texture2D ResolveTexture(
        Asset<Texture2D>? hatAsset,
        Asset<Texture2D> noHatAsset,
        bool noHat,
        Texture2D fallback)
    {
        Asset<Texture2D> picked = noHat && noHatAsset != null ? noHatAsset : hatAsset;

        if (picked == null || !picked.IsLoaded)
        {
            return fallback;
        }

        return picked.Value;
    }

    private Texture2D? GetCurrentTexture()
    {
        if (Main.dedServ)
        {
            return null;
        }

        Texture2D defaultTexture =
            Terraria.GameContent.TextureAssets.Npc[Type].Value;
        // 只有「帽子不在头上」时才是无帽形态：丢帽动画播放中，或者帽子正飞在外面
        bool hatRemoved =
            _secondPhaseStarted
            && (_hatOffTimer > 0 || _hatProjectileIndex >= 0);

        // 冲刺 / 蓄力是横放贴图，这时候播竖版的丢帽动作会很怪，直接切无帽
        bool horizontalState =
            AIState == (float)ActionState.Dash
            || AIState == (float)ActionState.Charge;

        if (_secondPhaseStarted && _hatOffTimer > 0 && !horizontalState)
        {
            return ResolveTexture(
                _hatOffTimer > HatOffFlyFrames ? _hatOffTexture0 : _hatOffTexture1,
                _hatOffTimer > HatOffFlyFrames ? _hatOffTexture0 : _hatOffTexture1,
                true,
                defaultTexture);
        }

        if (AIState == (float)ActionState.HatTeleportDash)
        {
            // 传送过去的同时帽子就算戴上了：直接用有帽子的冲刺贴图
            return ResolveTexture(_dashTexture, _dashTexture, false, defaultTexture);
        }

        if (AIState == (float)ActionState.HatCatch)
        {
            if (_hatCatchTimer < HatCatchNoHatFrames)
            {
                return ResolveTexture(null, _noHatDefaultTexture, true, defaultTexture);
            }

            if (_hatCatchTimer < HatCatchNoHatFrames + HatCatchHatOffFrames)
            {
                return ResolveTexture(_hatOffTexture0, _hatOffTexture0, true, defaultTexture);
            }

            return ResolveTexture(null, _noHatDefaultTexture, hatRemoved, defaultTexture);
        }

        if (AIState == (float)ActionState.HatThrowTrack)
        {
            // 帽子飞在外面：本体是无帽形态，并且要播日常移动的那套动作
            if (_idleAnimTimer < 15)
            {
                return ResolveTexture(null, _noHatDefaultTexture, true, defaultTexture);
            }

            if (_idleAnimTimer < 30)
            {
                return ResolveTexture(null, _noHatIdleTexture2, true, defaultTexture);
            }

            if (_idleAnimTimer < 45)
            {
                return ResolveTexture(null, _noHatIdleTexture2b, true, defaultTexture);
            }

            if (_idleAnimTimer < 60)
            {
                return ResolveTexture(null, _noHatIdleTexture3, true, defaultTexture);
            }

            return ResolveTexture(null, _noHatDefaultTexture, true, defaultTexture);
        }

        switch ((ActionState)AIState)
        {
            case ActionState.SuckAttack:
                {
                    if (_currentAnimFrame >= 0
                        && _currentAnimFrame < TotalAnimFrames)
                    {
                        Asset<Texture2D> frame =
                            _animationFrames[_currentAnimFrame];

                        if (frame != null && frame.IsLoaded)
                        {
                            return frame.Value;
                        }
                    }

                    return defaultTexture;
                }

            case ActionState.MovingToSide:
            case ActionState.FollowingPlayer:
            case ActionState.ApproachingCenter:
                {
                    if (_idleAnimTimer < 15)
                    {
                        return ResolveTexture(
                            null,
                            _noHatDefaultTexture,
                            hatRemoved,
                            defaultTexture);
                    }

                    if (_idleAnimTimer < 30)
                    {
                        return ResolveTexture(
                            _idleTexture2,
                            _noHatIdleTexture2,
                            hatRemoved,
                            defaultTexture);
                    }

                    if (_idleAnimTimer < 45)
                    {
                        return ResolveTexture(
                            _idleTexture2b,
                            _noHatIdleTexture2b,
                            hatRemoved,
                            defaultTexture);
                    }

                    if (_idleAnimTimer < 60)
                    {
                        return ResolveTexture(
                            _idleTexture3,
                            _noHatIdleTexture3,
                            hatRemoved,
                            defaultTexture);
                    }

                    return ResolveTexture(
                        null,
                        _noHatDefaultTexture,
                        hatRemoved,
                        defaultTexture);
                }

            case ActionState.Charge:
                return ResolveTexture(
                    _chargeTexture,
                    _noHatChargeTexture,
                    hatRemoved,
                    defaultTexture);

            case ActionState.Dash:
                return ResolveTexture(
                    _dashTexture,
                    _noHatDashTexture,
                    hatRemoved,
                    defaultTexture);

            default:
                return defaultTexture;
        }
    }

    // =========================================================
    // Boss 绘制
    // =========================================================

    public override bool PreDraw(
        SpriteBatch spriteBatch,
        Vector2 screenPos,
        Color drawColor)
    {
        Texture2D texture =
            GetCurrentTexture()
            ?? Terraria.GameContent.TextureAssets.Npc[Type].Value;

        Vector2 drawPosition =
            NPC.Center - screenPos;

        Vector2 origin =
            new(
                texture.Width / 2f,
                texture.Height / 2f);

        SpriteEffects effects =
            NPC.spriteDirection == 1
                ? SpriteEffects.FlipHorizontally
                : SpriteEffects.None;

        if (AIState == (float)ActionState.SuckAttack)
        {
            DrawSuckAura(spriteBatch, drawPosition);
        }

        // 残影画在本体之前，看起来是拖在身后
        DrawDashTrail(spriteBatch, texture, screenPos);

        spriteBatch.Draw(
            texture,
            drawPosition,
            null,
            drawColor,
            NPC.rotation,
            origin,
            NPC.scale,
            effects,
            0f);

        Color glowColor =
            new(180, 120, 220, 80);

        spriteBatch.Draw(
            texture,
            drawPosition,
            null,
            glowColor,
            NPC.rotation,
            origin,
            NPC.scale * 1.05f,
            effects,
            0f);

        return false;
    }

    // =========================================================
    // 掉落和死亡
    // =========================================================

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
    }

    public override void OnKill()
    {
        IsActive = false;
        FarSeaMusicSystem.TriggerVictoryMusic();
        JinlianSpawnSystem.MarkFarSeaEarlDefeated();
    }

    [Obsolete]
    public override void BossLoot(
        ref string name,
        ref int potionType)
    {
        potionType =
            ItemID.GreaterHealingPotion;
    }

    public override bool CheckActive() => false;

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (NPC.life <= 0)
        {
            for (int i = 0; i < 50; i++)
            {
                Dust dust =
                    Dust.NewDustDirect(
                        NPC.position,
                        NPC.width,
                        NPC.height,
                        DustID.Electric,
                        Scale: 2f);

                dust.velocity *= 3f;
                dust.noGravity = true;
            }
        }
    }
}
