// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-10-31

using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using sea.Content.DustSea.Items;
using sea.Content.DustSea.Projectiles;
using sea.Content.DustSea.Systems;

using SubworldLibrary;

using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
namespace sea.Content.DustSea.NPCs;
/// <summary>
/// 浊海旅人「堇涟」- 仅在击败远海伯爵后出现在世界左侧浊海的陆地上
/// </summary>
/// <remarks>The sprite sheet holds twelve 40x56 frames: front, back, facing left side idle, facing left walk with the right foot forward, facing left walk with the left foot forward, facing right side idle, facing right walk with the right foot forward, facing right walk with the left foot forward and four floating frames used when the NPC is more than five tiles above the ground. Talking to it for the first time unlocks a small wandering routine.</remarks>
[AutoloadHead]
public class JinlianNPC : ModNPC
{
    private const int FrameWidth = 40;
    private const int FrameHeight = 56;
    private const int FrontFrame = 0;
    private const int BackFrame = 1;
    // 帧序（作者标注）：0 正面（面朝屏幕）/ 1 背面 / 2 左侧站立 / 3 左侧右脚前 / 4 左侧左脚前 /
    // 5 右侧站立 / 6 右侧右脚前 / 7 右侧左脚前；即帧 2/3/4 面朝左，帧 5/6/7 面朝右。
    private const int FacingRightIdleFrame = 5;
    private const int FacingRightRightFootFrame = 6;
    private const int FacingRightLeftFootFrame = 7;
    private const int FacingLeftIdleFrame = 2;
    private const int FacingLeftRightFootFrame = 3;
    private const int FacingLeftLeftFootFrame = 4;
    private const int WalkFrameInterval = 8;
    // 漂浮动作只在跟随玩家时使用：帧 8/9/10/11 依次是作者的 1/2/3/4，播放顺序为 2→3→4→1
    private const int FloatFrameInterval = 8;
    private const int FloatFrame1 = 8;
    private const int FloatFrame2 = 9;
    private const int FloatFrame3 = 10;
    private const int FloatFrame4 = 11;
    private const float Gravity = 0.4f;
    private const float MaxFallSpeed = 10f;
    // 一步一格：每 16 tick 一个步幅（帧间隔 8、两帧一组），1.0 px/tick × 16 = 16px = 一格
    private const float WalkSpeed = 1f;
    private const float WanderRadius = 12f * 16f;
    private const int MinPauseTime = 40;
    private const int MaxPauseTime = 120;
    // 每段闲逛走三到十格，按实际走过的水平距离结算，不受速度影响
    private const float MinWalkDistance = 3f * 16f;
    private const float MaxWalkDistance = 10f * 16f;
    // 同一方向最多连走两段，第三段一定回头
    private const int MaxSameDirectionWalks = 2;
    // 未达上限时倾向于回头，让路线更随机但不会一直朝一边漂
    private const float BacktrackChance = 0.65f;
    // 站在孤立方块或小台阶上时可以走下边缘，落差超过这个格数就不往下跳（避免在深坑里来回反复）
    private const int MaxStepOffDropTiles = 4;
    // 提前三格发现坑或悬崖就掉头，不往坑里走
    private const int PitLookAheadTiles = 3;
    // 掉头之后至少要走这么久才允许再次掉头，避免在边缘原地反复转身
    private const int TurnCooldownTicks = 20;
    private const float TalkingFaceRange = 16f * 16f;
    // 跳跃高度约五格：重力 0.4，起跳速度 8 时最高点约 8²/(2×0.4) = 80px = 五格
    private const float JumpVelocity = 8f;
    private const int JumpMaxTiles = 5;
    private const int JumpCooldownTicks = 20;
    // 首次对话剧情步骤：0 为不在剧情中
    private const int DialogueNone = 0;
    private const int DialogueIntroLine1 = 1;
    private const int DialogueIntroLine2 = 2;
    private const int DialogueIntroLine3 = 3;
    private const int DialogueQuestion = 4;
    private const int DialogueAcceptStart = 5;
    private const int DialogueAcceptPolitely1 = 6;
    private const int DialogueAcceptPolitely2 = 7;
    private const int DialogueAcceptDoubting1 = 8;
    private const int DialogueAcceptDoubting2 = 9;
    private const int DialogueAcceptDoubting3 = 10;
    // 首次对话中间的自我介绍（说完名字才不再显示「？？？」）
    private const int DialogueIntroLineIdentity = 20;
    // 第三次搭话时触发的赠礼剧情
    private const int DialogueGiftLine1 = 11;
    private const int DialogueGiftLine2 = 12;
    // 复活后重新搭话触发的一句短剧情
    private const int DialogueReviveLine1 = 13;
    // 第一次复活后重新搭话触发的剧情（多句，中间一个玩家选项）
    private const int DialogueFirstReviveLine1 = 14;
    private const int DialogueFirstReviveLine2 = 15;
    private const int DialogueFirstReviveLine3 = 16;
    private const int DialogueFirstReviveLine4 = 17;
    private const int DialogueFirstReviveLine5 = 18;
    // 日常对话里点「治疗」之后随机冒出来的一句
    private const int DialogueHealLine = 19;
    // 治疗台词总数
    private const int HealLineCount = 6;
    // 触发赠礼需要的搭话次数（含首次剧情那次）
    private const int GiftTalkThreshold = 3;
    // 日常闲聊台词总数，每次对话等概率抽一句，且不会连续两次重复
    private const int DailyChatCount = 7;
    // 剧情推进后的点击冷却帧数
    private const int DialogueClickCooldown = 8;
    // 表情差分：日常闲聊固定用第 8 张，质疑/追问用第 7 张
    internal const int DailyFaceIndex = 8;
    internal const int DoubtFaceIndex = 7;
    // 跟随模式：飞行时悬浮在玩家侧边，贴地时和玩家一起走，全程不瞬移
    private const float FollowStopDistance = 1.2f * 16f;
    private const float FollowMinSpeed = 2.2f;
    private const float FollowMaxSpeed = 14f;
    private const float FollowSpeedScale = 0.2f;
    private const float FollowPlayerSpeedScale = 1.15f;
    // 速度插值系数：越小惯性越明显
    private const float FollowAcceleration = 0.1f;
    private const float FollowHoverOffsetX = 3f * 16f;
    private const float FollowHoverOffsetY = -1.2f * 16f;
    // 贴地跟随：横向保持两格，速度随玩家速度变化，重力与碰撞照常走原版
    private const float FollowGroundOffsetX = 2f * 16f;
    private const float FollowGroundMaxSpeed = 6f;
    private const float FollowGroundAcceleration = 0.12f;
    // 和玩家高度差超过两格半才起飞；玩家连续离地若干 tick 才算真的起飞
    private const float FollowTakeOffHeight = 2.5f * 16f;
    private const int FollowTakeOffDelay = 8;
    private const int FollowGroundScanTiles = 6;
    private const int LifeMaxValue = 800;
    // 头顶血条：平时不显示，受伤后出现并在 5 秒内渐隐
    private const int HealthBarShowTicks = 300;
    private const int HealthBarFadeTicks = 45;

    // 她挨打后的最短间隔，配合玩家的无敌帧一起防住接触伤害刷屏
    private const int CompanionHurtCooldownTicks = 20;
    private const float HealthBarWidth = 68f;
    private const float HealthBarHeight = 8f;
    private const float HealthBarOffsetY = 74f;
    private const float HealthBarGhostLerp = 0.035f;
    // 每隔一段时间召唤水母去追附近的敌人
    // 约 0.7 秒一发水母
    private const int JellyfishSummonInterval = 48;
    private const int JellyfishSummonCount = 1;
    // 仇恨范围：约 200 格，比一屏还大，敌人隔着一段距离也会被追
    private const float JellyfishSearchRange = 200f * 16f;
    private int _chatLogTimer;
    private int _jumpCooldown;
    private float _jumpDirection;
    // 跟随时的物理状态：true 表示悬浮飞行，false 表示贴地走路
    private bool _followHovering;
    private int _followPlayerAirTicks;
    private int _summonTimer;
    private int _walkAnimationTimer;
    private int _floatAnimationTimer;
    private float _walkedDistance;
    private float _walkTargetDistance;
    private float _lastWalkDirection;
    private int _sameDirectionWalks;
    private int _turnCooldown;
    // 被坑或墙逼着回头时记下这个方向，下一段不允许再朝这里走
    private float _forbiddenDirection;
    // 首次对话的剧情步骤，0 表示不在剧情中
    private int _dialogueStep;
    // 剧情刚推进后的短暂冷却，避免一次点击被同帧处理两次
    private int _dialogueClickCooldown;
    // 上一次用过的日常闲聊序号，用来避免连续重复
    private int _lastDailyChatIndex = -1;
    // 是否已经在她自报家门之后（未开启首次剧情前都显示「？？？」）
    private bool _nameRevealed;
    // 上一条治疗台词，避免连续两次重复
    private int _lastHealLineIndex = -1;
    // 本次治疗要显示的台词序号
    private int _healLineIndex;
    // 头顶血条的配色：青绿 → 琥珀 → 红，底板与描边用浊海紫
    private static readonly Color HealthBarHealthyColor = new(126, 232, 168);
    private static readonly Color HealthBarAmberColor = new(238, 198, 108);
    private static readonly Color HealthBarLowColor = new(232, 104, 108);
    private static readonly Color HealthBarGhostColor = new Color(255, 244, 214) * 0.65f;
    private static readonly Color HealthBarBackColor = new(22, 16, 34);
    private static readonly Color HealthBarBorderColor = new(158, 122, 214);
    // 上一帧是否有玩家正在和她对话，用来识别「刚打开聊天框」的上升沿
    private bool _wasTalking;
    // 头顶血条：受伤后继续显示的剩余帧数（跟随状态下常显）
    private int _healthBarTimer;

    private int _companionHurtCooldown;
    // 头顶血条的掉血残影，缓慢追上当前血量，用来交代「刚掉了多少」
    private float _healthBarGhost = 1f;
    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 1;
    public override void SetDefaults()
    {
        NPC.width = 22;
        NPC.height = 44;
        NPC.aiStyle = NPCAIStyleID.Passive;
        NPC.townNPC = true;
        NPC.friendly = true;
        NPC.damage = 0;
        NPC.defense = 0;
        NPC.lifeMax = LifeMaxValue;
        NPC.knockBackResist = 0.5f;
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;
        NPC.direction = -1;
        NPC.spriteDirection = -1;
        NPC.ai[0] = 0f;
    }
    /// <summary>
    /// 是否处于跟随模式
    /// </summary>
    /// <remarks>The follow flag lives in the fourth AI slot so it travels with the NPC synchronization instead of a client-only field.</remarks>
    private bool IsFollowing => NPC.ai[3] == 1f;
    public override bool PreAI()
    {
        UpdateJellyfishSummon();
        if (_dialogueClickCooldown > 0)
        {
            _dialogueClickCooldown--;
        }
        // 剧情结束后她一直是醒着的，不需要再点一次唤醒
        if (JinlianSpawnSystem.JinlianIntroduced)
        {
            NPC.ai[0] = 1f;
        }
        UpdateHealthBar();
        // 跟随分「贴地走路」和「悬浮飞行」两种物理状态：靠近地面就走，玩家飞起来才跟着飞
        UpdateFollowPhysicsState();
        bool hoveringFollow = IsFollowing && _followHovering;
        NPC.noGravity = hoveringFollow;
        NPC.noTileCollide = hoveringFollow;
        // 跟随状态下她替玩家挨打，本体不再受任何来源的伤害
        NPC.dontTakeDamage = IsFollowing;
        // 并肩作战：跟随时她的防御力和玩家保持一致
        if (IsFollowing)
        {
            NPC.defense = GetOwnerDefense();
        }
        if (_jumpCooldown > 0)
        {
            _jumpCooldown--;
        }

        if (_companionHurtCooldown > 0)
        {
            _companionHurtCooldown--;
        }
        // 贴地时每 tick 施加重力，交给原版碰撞去处理台阶与地面
        if (!hoveringFollow)
        {
            NPC.velocity.Y = MathHelper.Clamp(
                NPC.velocity.Y + Gravity,
                -MaxFallSpeed,
                MaxFallSpeed
            );
        }
        // 独立推进走路动画：只要在横向移动就换帧，停下立刻回到站立帧，避免只用一个动作平移
        if (Math.Abs(NPC.velocity.X) > 0.05f)
        {
            _walkAnimationTimer++;
        }
        else
        {
            _walkAnimationTimer = 0;
        }
        // 漂浮动作只在跟随且飞在空中的时候播放，日常闲逛和贴地跟随都用地面帧
        if (hoveringFollow)
        {
            _floatAnimationTimer++;
        }
        else
        {
            _floatAnimationTimer = 0;
        }
        Player talkingPlayer = FindTalkingPlayer();
        LogChatState(talkingPlayer);
        if (talkingPlayer != null)
        {
            // 被搭话之后解锁走动，并转身面向玩家；对话期间完全站定
            // 站定时把 velocity.Y 归零：原版 NPC.CanBeTalkedTo 要求 velocity.Y == 0f
            NPC.ai[0] = 1f;
            NPC.velocity.X = 0f;
            if (hoveringFollow)
            {
                NPC.velocity = Vector2.Zero;
            }
            KeepVelocityYZeroOnGround();
            FaceTowards(talkingPlayer.Center.X);
            OnChatStarted(talkingPlayer);
            EnsureIntroductionStarted();
            EnsureChatTextPresent();
            return false;
        }
        _wasTalking = false;
        // 跟随模式优先于日常闲逛：对话结束后立刻开始跟随玩家
        if (IsFollowing)
        {
            UpdateFollow();
            return false;
        }
        if (NPC.ai[0] <= 0f)
        {
            // 未对话前始终原地静止，同样要把 velocity.Y 归零以便玩家右键对话
            NPC.velocity.X = 0f;
            KeepVelocityYZeroOnGround();
            return false;
        }
        // 空中不跑闲逛状态机：起跳时赋予的水平速度必须留到落地，
        // 否则「前方不通→原地休息」会把速度清零，看起来就是原地起跳。
        // 贴墙起跳时碰撞会立刻清掉水平速度，所以空中每 tick 重新推向起跳方向。
        if (!IsOnGround())
        {
            if (_jumpDirection != 0f)
            {
                NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, _jumpDirection * WalkSpeed, 0.25f);
            }
            // 漂浮动画由上方统一计时，这里不再单独推进
            return false;
        }
        _jumpDirection = 0f;
        Wander();
        return false;
    }
    /// <summary>
    /// 站在地面时把竖直速度归零
    /// </summary>
    /// <remarks>The vanilla town NPC talk check requires the vertical velocity to be exactly zero, so standing still must not keep a gravity step.</remarks>
    private void KeepVelocityYZeroOnGround()
    {
        if (IsOnGround())
        {
            NPC.velocity.Y = 0f;
        }
    }
    /// <summary>
    /// 决定跟随时的物理状态：贴地走路还是悬浮飞行
    /// </summary>
    /// <remarks>Keeps the NPC walking with vanilla gravity and collisions while the player stays near the ground, and only takes off after the player has been airborne for a few ticks, so short hops do not make it flap around.</remarks>
    private void UpdateFollowPhysicsState()
    {
        if (!IsFollowing)
        {
            _followHovering = false;
            _followPlayerAirTicks = 0;
            return;
        }
        Player target = Main.player[Player.FindClosest(NPC.position, NPC.width, NPC.height)];
        if (target == null || !target.active || target.dead)
        {
            // 没有跟随目标就贴着地面待机，不要凭空悬在天上
            _followHovering = false;
            _followPlayerAirTicks = 0;
            return;
        }
        bool playerGrounded = GetGroundClearanceTilesAt(target.Bottom, 2) <= 2;
        if (playerGrounded)
        {
            _followPlayerAirTicks = 0;
        }
        else
        {
            _followPlayerAirTicks++;
        }
        float heightGap = target.Center.Y - NPC.Center.Y;
        if (_followHovering)
        {
            bool canLand = playerGrounded
                && Math.Abs(heightGap) <= FollowTakeOffHeight
                && GetGroundClearanceTiles() <= FollowGroundScanTiles;
            if (canLand)
            {
                // 落地时把向上的残余速度抹掉，避免落地瞬间再弹一下
                _followHovering = false;
                if (NPC.velocity.Y < 0f)
                {
                    NPC.velocity.Y = 0f;
                }
            }
            return;
        }
        bool mustFly = _followPlayerAirTicks >= FollowTakeOffDelay
            || Math.Abs(heightGap) > FollowTakeOffHeight;
        if (mustFly)
        {
            _followHovering = true;
        }
    }
    /// <summary>
    /// 跟随模式下按当前物理状态选择贴地或飞行
    /// </summary>
    /// <remarks>The mode itself is decided once per tick before the physics step, this only steers the NPC once the mode is known.</remarks>
    private void UpdateFollow()
    {
        Player target = Main.player[Player.FindClosest(NPC.position, NPC.width, NPC.height)];
        if (target == null || !target.active || target.dead || SubworldSystem.IsActive<sea.Content.FarSea.Subworlds.FarSea>())
        {
            // 目标失效或玩家进了子世界：原地减速，贴地时不要继续往上飞
            float damping = _followHovering ? FollowAcceleration : FollowGroundAcceleration;
            NPC.velocity = Vector2.Lerp(NPC.velocity, Vector2.Zero, damping);
            return;
        }
        if (_followHovering)
        {
            UpdateFollowHover(target);
        }
        else
        {
            UpdateFollowGround(target);
        }
    }
    /// <summary>
    /// 悬浮跟随：速度带惯性，随距离和玩家速度变化
    /// </summary>
    /// <param name="target">跟随目标</param>
    /// <remarks>Velocity is blended towards the wanted velocity instead of assigned, so the NPC accelerates and drifts like a floating follower rather than snapping to the target.</remarks>
    private void UpdateFollowHover(Player target)
    {
        float side = GetBehindSide(target);
        Vector2 hoverPoint = new(
            target.Center.X + side * FollowHoverOffsetX,
            target.Center.Y + FollowHoverOffsetY
        );
        Vector2 toTarget = hoverPoint - NPC.Center;
        float distance = toTarget.Length();
        if (distance <= FollowStopDistance)
        {
            // 到位置就完全停稳：原版 CanBeTalkedTo 要求 velocity.Y 精确为 0
            NPC.velocity = Vector2.Lerp(NPC.velocity, Vector2.Zero, 0.3f);
            if (NPC.velocity.LengthSquared() < 0.05f)
            {
                NPC.velocity = Vector2.Zero;
            }
            FaceTowards(target.Center.X);
            return;
        }
        // 速度同时受「离玩家的距离」和「玩家自身速度」影响，玩家跑得快就跟得快
        float desiredSpeed = MathHelper.Clamp(
            distance * FollowSpeedScale + Math.Abs(target.velocity.X) * FollowPlayerSpeedScale,
            FollowMinSpeed,
            FollowMaxSpeed
        );
        Vector2 desiredVelocity = toTarget / distance * desiredSpeed;
        NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, FollowAcceleration);
        FaceTowards(target.Center.X);
    }
    /// <summary>
    /// 贴地跟随：和玩家一起走，遇到台阶就跳
    /// </summary>
    /// <param name="target">跟随目标</param>
    /// <remarks>Horizontal speed is blended with inertia and the vertical axis is left to vanilla gravity plus collisions, which is what keeps the NPC on the ground like the vanilla pets.</remarks>
    private void UpdateFollowGround(Player target)
    {
        float side = GetBehindSide(target);
        float targetX = target.Center.X + side * FollowGroundOffsetX;
        float dx = targetX - NPC.Center.X;
        if (Math.Abs(dx) <= FollowStopDistance)
        {
            // 到位后用摩擦减速，不让它急停；站定时把 velocity.Y 抹零，跟随状态下依然能被右键对话
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, 0f, FollowGroundAcceleration);
            KeepVelocityYZeroOnGround();
            FaceTowards(target.Center.X);
            return;
        }
        float direction = Math.Sign(dx);
        float desiredSpeed = MathHelper.Clamp(
            Math.Abs(dx) * FollowSpeedScale + Math.Abs(target.velocity.X) * FollowPlayerSpeedScale,
            FollowMinSpeed,
            FollowGroundMaxSpeed
        );
        NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, direction * desiredSpeed, FollowGroundAcceleration);
        FaceTowards(target.Center.X);
        // 只有真的被挡住才跳：墙、台阶、或一格宽的小沟；平地前进不要一直蹦
        if (IsOnGround())
        {
            bool wallAhead = HasWallAhead(direction);
            bool stepUpAhead = HasLandingAhead(direction);
            bool narrowGapAhead = !HasGroundWithinTiles(direction, 1) && HasGroundWithinTiles(direction, 2);
            if (wallAhead || stepUpAhead || narrowGapAhead)
            {
                TryJumpOverObstacle(direction);
            }
        }
    }
    /// <summary>
    /// 返回玩家身后那一侧的方向
    /// </summary>
    /// <param name="player">跟随目标</param>
    /// <returns>1 表示应站在玩家右侧，-1 表示应站在玩家左侧</returns>
    /// <remarks>Faces opposite to the player heading so the follower trails behind instead of drifting in front, and falls back to the player facing when standing still.</remarks>
    private static float GetBehindSide(Player player)
    {
        float facing = player.direction;
        if (player.velocity.X > 0.5f)
        {
            facing = 1f;
        }
        else if (player.velocity.X < -0.5f)
        {
            facing = -1f;
        }
        return -facing;
    }
    /// <summary>
    /// 返回某个底边往下到第一块实心方块的格数
    /// </summary>
    /// <param name="bottom">底边中心点</param>
    /// <param name="maxTiles">最多往下找几格</param>
    /// <returns>格数；范围内没有地面时返回 <paramref name="maxTiles"/> + 1</returns>
    /// <remarks>Used to tell whether the player and the NPC are standing near the ground, so the follow mode can switch between walking and flying.</remarks>
    private static int GetGroundClearanceTilesAt(Vector2 bottom, int maxTiles)
    {
        int tileX = Math.Clamp((int)(bottom.X / 16f), 0, Main.maxTilesX - 1);
        // 从脚底所在那一行开始数（+4 与 IsOnGround 一致），正踩在地面上时返回 0
        int tileY = (int)((bottom.Y + 4f) / 16f);
        for (int step = 0; step <= maxTiles; step++)
        {
            if (IsGroundTile(tileX, tileY + step))
            {
                return step;
            }
        }
        return maxTiles + 1;
    }
    /// <summary>
    /// 判断某一格能不能站
    /// </summary>
    /// <param name="tileX">格坐标 X</param>
    /// <param name="tileY">格坐标 Y</param>
    /// <returns>实心方块、平台（玩家可以站在上面）或世界底部时为 true</returns>
    /// <remarks>Platforms are not solid tiles but players stand on them, and a player standing on platforms must not be treated as airborne, otherwise the follower keeps taking off for no reason.</remarks>
    private static bool IsGroundTile(int tileX, int tileY)
    {
        if (tileY >= Main.maxTilesY)
        {
            return true;
        }
        Tile tile = Main.tile[tileX, tileY];
        if (!tile.HasTile)
        {
            return false;
        }
        return WorldGen.SolidTile(tileX, tileY) || TileID.Sets.Platforms[tile.TileType];
    }
    /// <summary>
    /// 返回堇涟脚下到第一块实心方块的格数
    /// </summary>
    /// <returns>格数；扫描范围内没有地面时返回 <see cref="FollowGroundScanTiles"/> + 1</returns>
    /// <remarks>Only the follow landing check uses this, the walk state machine has its own ground probes.</remarks>
    private int GetGroundClearanceTiles() => GetGroundClearanceTilesAt(NPC.Bottom, FollowGroundScanTiles);
    /// <summary>
    /// 死亡时记录下来，等待玩家在召回面板里复活
    /// </summary>
    /// <remarks>Marking the death here is exact, unlike polling for a missing NPC which would also fire while the world is still loading and the NPC list is not ready.</remarks>
    public override void OnKill() => JinlianSpawnSystem.MarkJinlianDead();
    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[NPC.type].Value;
        Rectangle source = new(GetFrameIndex() * FrameWidth, 0, FrameWidth, FrameHeight);
        Vector2 origin = new(FrameWidth / 2f, FrameHeight);
        Vector2 position = NPC.Bottom - screenPos + new Vector2(0f, NPC.gfxOffY);
        Color color = NPC.IsABestiaryIconDummy ? Color.White : drawColor;
        spriteBatch.Draw(
            texture,
            position,
            source,
            color,
            NPC.rotation,
            origin,
            NPC.scale,
            SpriteEffects.None,
            0f
        );
        return false;
    }
    /// <summary>
    /// 在堇涟头顶补一条血条：平时不显示，掉血后出现并在 5 秒内渐隐。
    /// </summary>
    /// <param name="spriteBatch">当前批次</param>
    /// <param name="screenPos">摄像机位置</param>
    /// <param name="drawColor">绘制颜色</param>
    public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (NPC.IsABestiaryIconDummy || NPC.Opacity <= 0.05f || _healthBarTimer <= 0)
        {
            return;
        }
        // 计时末尾一段时间用来渐隐，避免血条突然消失
        float barAlpha = MathHelper.Clamp(_healthBarTimer / (float)HealthBarFadeTicks, 0f, 1f);
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        float ratio = MathHelper.Clamp(NPC.life / (float)NPC.lifeMax, 0f, 1f);
        float ghost = MathHelper.Clamp(Math.Max(_healthBarGhost, ratio), 0f, 1f);
        int left = (int)Math.Round(NPC.Center.X - HealthBarWidth * 0.5f - screenPos.X);
        int top = (int)Math.Round(NPC.Bottom.Y - HealthBarOffsetY - screenPos.Y);
        int barWidth = (int)HealthBarWidth;
        int barHeight = (int)HealthBarHeight;
        // 深色底板 + 淡紫描边，保证在海面这种亮背景上也看得清
        spriteBatch.Draw(pixel, new Rectangle(left - 2, top - 2, barWidth + 4, barHeight + 4), HealthBarBackColor * (0.85f * barAlpha));
        spriteBatch.Draw(pixel, new Rectangle(left - 2, top - 2, barWidth + 4, 1), HealthBarBorderColor * barAlpha);
        spriteBatch.Draw(pixel, new Rectangle(left - 2, top + barHeight + 1, barWidth + 4, 1), HealthBarBorderColor * barAlpha);
        spriteBatch.Draw(pixel, new Rectangle(left - 2, top - 2, 1, barHeight + 4), HealthBarBorderColor * barAlpha);
        spriteBatch.Draw(pixel, new Rectangle(left + barWidth + 1, top - 2, 1, barHeight + 4), HealthBarBorderColor * barAlpha);
        // 掉血残影：先画在下面，主填充会盖住它
        if (ghost > ratio)
        {
            spriteBatch.Draw(
                pixel,
                new Rectangle(left, top, (int)(barWidth * ghost), barHeight),
                HealthBarGhostColor * barAlpha
            );
        }
        int fillWidth = (int)(barWidth * ratio);
        if (fillWidth > 0)
        {
            int upperHeight = barHeight / 2;
            int lowerHeight = barHeight - upperHeight;
            Color fill = GetHealthFillColor(ratio);
            spriteBatch.Draw(pixel, new Rectangle(left, top, fillWidth, upperHeight), fill * barAlpha);
            spriteBatch.Draw(pixel, new Rectangle(left, top + upperHeight, fillWidth, lowerHeight), fill * (0.72f * barAlpha));
            // 顶部一条高光，让填充看起来有体积
            spriteBatch.Draw(pixel, new Rectangle(left, top, fillWidth, 1), Color.White * (0.55f * barAlpha));
        }
        // 四等分刻度，方便粗估剩余比例
        for (int index = 1; index < 4; index++)
        {
            int tickX = left + barWidth * index / 4;
            spriteBatch.Draw(pixel, new Rectangle(tickX, top, 1, barHeight), HealthBarBackColor * (0.75f * barAlpha));
        }
    }
    /// <summary>
    /// 推进血条的显示计时与掉血残影。
    /// </summary>
    private void UpdateHealthBar()
    {
        if (_healthBarTimer > 0)
        {
            _healthBarTimer--;
        }
        float ratio = MathHelper.Clamp(NPC.life / (float)NPC.lifeMax, 0f, 1f);
        _healthBarGhost = MathHelper.Lerp(_healthBarGhost, ratio, HealthBarGhostLerp);
        if (_healthBarGhost - ratio < 0.002f)
        {
            _healthBarGhost = ratio;
        }
    }
    /// <summary>
    /// 按剩余比例给血条上色：健康是青绿，半数转琥珀，濒死转红。
    /// </summary>
    /// <param name="ratio">剩余血量比例</param>
    /// <returns>填充颜色</returns>
    private static Color GetHealthFillColor(float ratio)
    {
        if (ratio > 0.6f)
        {
            return Color.Lerp(HealthBarAmberColor, HealthBarHealthyColor, (ratio - 0.6f) / 0.4f);
        }
        return Color.Lerp(HealthBarLowColor, HealthBarAmberColor, ratio / 0.6f);
    }
    public override string GetChat()
    {
        if (_dialogueStep != DialogueNone)
        {
            return GetCurrentDialogueText();
        }
        // 复活后的第一次搭话单独走一句短剧情
        if (JinlianSpawnSystem.JinlianAwaitingReviveTalk)
        {
            // 第一次复活说一整套新剧情，之后的复活才用那句短台词
            _dialogueStep = JinlianSpawnSystem.JinlianFirstReviveStorySeen
                ? DialogueReviveLine1
                : DialogueFirstReviveLine1;
            return GetDialogueText(_dialogueStep);
        }
        // 这个世界的第一次对话进入剧情
        if (!JinlianSpawnSystem.JinlianIntroduced)
        {
            _dialogueStep = DialogueIntroLine1;
            return GetDialogueText(_dialogueStep);
        }
        return GetDailyChatText();
    }
    /// <summary>
    /// 玩家刚打开聊天框时结算搭话次数，并在第三次搭话时切入赠礼剧情
    /// </summary>
    /// <param name="player">正在对话的玩家</param>
    /// <remarks>The rising edge is detected per NPC instead of inside GetChat because vanilla calls GetChat on every redraw.</remarks>
    private void OnChatStarted(Player player)
    {
        if (_wasTalking)
        {
            return;
        }
        _wasTalking = true;
        // 剧情进行中被重新打开不算新的一次搭话
        if (_dialogueStep != DialogueNone)
        {
            return;
        }
        // 复活后的那句短剧情优先播，这次搭话不计入次数也不抢赠礼
        if (JinlianSpawnSystem.JinlianAwaitingReviveTalk)
        {
            return;
        }
        JinlianSpawnSystem.MarkJinlianTalked();
        // 首次剧情没看完之前不派发赠礼，避免抢掉剧情入口
        if (!JinlianSpawnSystem.JinlianIntroduced)
        {
            return;
        }
        if (!JinlianSpawnSystem.JinlianGiftGiven && JinlianSpawnSystem.JinlianTalkCount >= GiftTalkThreshold)
        {
            SetDialogueStep(DialogueGiftLine1);
            FaceTowards(player.Center.X);
        }
    }
    /// <summary>
    /// 面板需要显示物品框时返回物品类型，否则返回 0
    /// </summary>
    /// <remarks>The reward slot is only shown on the line that hands the jellyfish over.</remarks>
    internal int ActiveGiftItemType => _dialogueStep == DialogueGiftLine2 ? ModContent.ItemType<JinlianJellyfishItem>() : 0;
    /// <summary>
    /// 把小水母放进玩家背包，背包满时掉在地上并提示
    /// </summary>
    /// <remarks>QuickSpawnItem already drops the item on the ground when the inventory has no room, so the extra check only drives the chat hint.</remarks>
    private void GrantJellyfishGift()
    {
        if (JinlianSpawnSystem.JinlianGiftGiven)
        {
            return;
        }
        Player player = FindTalkingPlayer();
        if (player is null)
        {
            return;
        }
        JinlianSpawnSystem.MarkJinlianGiftGiven();
        bool hasRoom = HasInventoryRoom(player);
        int itemType = ModContent.ItemType<JinlianJellyfishItem>();
        player.QuickSpawnItem(NPC.GetSource_Loot(), itemType, 1);
        if (!hasRoom)
        {
            Main.NewText(
                Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.InventoryFull"),
                (byte)255,
                (byte)170,
                (byte)120
            );
        }
    }
    /// <summary>
    /// 判断玩家背包是否还有空位
    /// </summary>
    /// <param name="player">目标玩家</param>
    /// <returns>有空位返回 true</returns>
    private static bool HasInventoryRoom(Player player)
    {
        for (int i = 0; i < player.inventory.Length; i++)
        {
            if (player.inventory[i].IsAir)
            {
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// 结束赠礼剧情，剧情打完直接关掉对话框
    /// </summary>
    private void FinishGift()
    {
        _dialogueStep = DialogueNone;
        NPC.ai[0] = 1f;
        NPC.netUpdate = true;
        CloseChatAfterStory();
    }
    /// <summary>
    /// 结束第一次复活后的新剧情，并把「已经看过」写进存档
    /// </summary>
    /// <remarks>Subsequent revives fall back to the short line because the flag is set from now on.</remarks>
    private void FinishFirstReviveStory()
    {
        JinlianSpawnSystem.MarkReviveTalked();
        JinlianSpawnSystem.MarkFirstReviveStorySeen();
        _dialogueStep = DialogueNone;
        NPC.ai[0] = 1f;
        NPC.netUpdate = true;
        CloseChatAfterStory();
    }
    /// <summary>
    /// 看完治疗台词后关掉对话框
    /// </summary>
    private void FinishHealLine()
    {
        _dialogueStep = DialogueNone;
        NPC.ai[0] = 1f;
        NPC.netUpdate = true;
        CloseChatAfterStory();
    }
    /// <summary>
    /// 日常对话里点「治疗」：回满血并随机说一句治疗台词
    /// </summary>
    /// <remarks>The panel reads the chat buffer, so the picked line is pushed there directly instead of going through SetDialogueStep.</remarks>
    internal void HealFromDialogue()
    {
        NPC.life = NPC.lifeMax;
        NPC.netUpdate = true;
        _healthBarTimer = HealthBarShowTicks;
        _healthBarGhost = 1f;
        _healLineIndex = NextHealLineIndex();
        _dialogueStep = DialogueHealLine;
        Main.npcChatText = GetDialogueOption("HealLine" + (_healLineIndex + 1));
        Mod.Logger.Info($"堇涟治疗 台词={_healLineIndex + 1} / Jinlian healed with line {_healLineIndex + 1}");
        if (Main.dedServ)
        {
            return;
        }
        for (int i = 0; i < 12; i++)
        {
            Dust dust = Dust.NewDustPerfect(
                NPC.Center,
                DustID.GreenTorch,
                Main.rand.NextVector2Circular(2.2f, 2.2f),
                0,
                default,
                1.2f);
            dust.noGravity = true;
        }
    }
    /// <summary>
    /// 抽一句治疗台词，避免和上一次重复
    /// </summary>
    /// <returns>台词序号</returns>
    private int NextHealLineIndex()
    {
        int index = Main.rand.Next(HealLineCount);
        if (index == _lastHealLineIndex)
        {
            index = (index + 1 + Main.rand.Next(HealLineCount - 1)) % HealLineCount;
        }
        _lastHealLineIndex = index;
        return index;
    }
    /// <summary>
    /// 结束复活后的短剧情
    /// </summary>
    /// <remarks>Also clears the "revive line pending" flag so every later conversation falls back to the daily chat pool.</remarks>
    private void FinishReviveStory()
    {
        JinlianSpawnSystem.MarkReviveTalked();
        _dialogueStep = DialogueNone;
        NPC.ai[0] = 1f;
        NPC.netUpdate = true;
        CloseChatAfterStory();
    }
    /// <summary>
    /// 剧情结束后关闭对话框，不再衔接到日常闲聊
    /// </summary>
    /// <remarks>The custom panel is drawn from the chat state, so closing the chat removes the panel and hands the screen back to the world right away.</remarks>
    private static void CloseChatAfterStory()
    {
        Main.npcChatText = string.Empty;
        if (Main.dedServ)
        {
            return;
        }
        Main.CloseNPCChatOrSign();
    }
    /// <summary>
    /// 兜底：搭话时如果还没看过剧情就立刻进入第一句，避免只显示日常按钮
    /// </summary>
    /// <remarks>The chat text is normally filled by GetChat, but the custom panel replaces the vanilla draw layer, so the first line is started here as well to keep the two paths in sync.</remarks>
    private void EnsureIntroductionStarted()
    {
        if (_dialogueStep != DialogueNone || JinlianSpawnSystem.JinlianIntroduced)
        {
            return;
        }
        // 复活后的那句台词要先说，别被首次剧情抢掉
        if (JinlianSpawnSystem.JinlianAwaitingReviveTalk)
        {
            return;
        }
        _dialogueStep = DialogueIntroLine1;
        Main.npcChatText = GetDialogueText(_dialogueStep);
    }
    /// <summary>
    /// 兜底：自绘面板要求聊天框里有文本，空了就补一句
    /// </summary>
    /// <remarks>The custom panel draws whatever the chat buffer holds, so an empty buffer would show a blank line instead of the daily chatter.</remarks>
    private void EnsureChatTextPresent()
    {
        if (!string.IsNullOrEmpty(Main.npcChatText))
        {
            return;
        }
        if (_dialogueStep != DialogueNone)
        {
            Main.npcChatText = GetCurrentDialogueText();
            return;
        }
        // 复活后的那句短剧情优先于日常闲聊
        if (JinlianSpawnSystem.JinlianAwaitingReviveTalk)
        {
            _dialogueStep = JinlianSpawnSystem.JinlianFirstReviveStorySeen
                ? DialogueReviveLine1
                : DialogueFirstReviveLine1;
            Main.npcChatText = GetDialogueText(_dialogueStep);
            return;
        }
        Main.npcChatText = GetDailyChatText();
    }
    public override void SetChatButtons(ref string button, ref string button2)
    {
        button2 = string.Empty;
        if (IsDialogueActive)
        {
            GetActiveDialogueOptions(out button, out button2, out _);
            return;
        }
        button = GetFollowButtonLabel();
    }
    public override void OnChatButtonClicked(bool firstButton, ref string shopName)
    {
        if (_dialogueStep != DialogueNone)
        {
            ClickActiveDialogueOption(firstButton ? 0 : 1);
            return;
        }
        if (!firstButton)
        {
            return;
        }
        ToggleFollow();
    }
    /// <summary>
    /// 切换跟随状态（原版按钮与专属对话框面板共用）
    /// </summary>
    internal void ToggleFollow()
    {
        bool following = !IsFollowing;
        NPC.ai[3] = following ? 1f : 0f;
        NPC.netUpdate = true;
        Main.npcChatText = Language.GetTextValue(
            following ? "Mods.sea.NPCs.JinlianNPC.ChatTextFollow" : "Mods.sea.NPCs.JinlianNPC.ChatTextStopFollow"
        );
    }
    /// <summary>
    /// 是否正在以同伴身份跟随玩家（跟随中她替玩家承受伤害）
    /// </summary>
    internal bool IsCompanionFollowing => NPC.active && IsFollowing;

    internal bool IsFollowEnabled => IsFollowing;
    /// <summary>
    /// 替玩家承受一次伤害
    /// </summary>
    /// <param name="damage">已经过玩家防御力减免后的伤害</param>
    internal void TakeCompanionDamage(int damage)
    {
        if (!IsCompanionFollowing || damage <= 0)
        {
            return;
        }

        // 同一段接触伤害可能连着好几 tick 触发，这里再加一道冷却，避免她被瞬间扫空
        if (_companionHurtCooldown > 0)
        {
            return;
        }

        _companionHurtCooldown = CompanionHurtCooldownTicks;
        NPC.life -= damage;
        NPC.netUpdate = true;
        _healthBarTimer = HealthBarShowTicks;
        if (!Main.dedServ)
        {
            CombatText.NewText(NPC.Hitbox, new Color(255, 120, 120), damage);
            for (int i = 0; i < 6; i++)
            {
                Dust dust = Dust.NewDustPerfect(
                    NPC.Center,
                    DustID.PurpleTorch,
                    Main.rand.NextVector2Circular(2.5f, 2.5f),
                    0,
                    default,
                    1.1f);
                dust.noGravity = true;
            }
        }
        if (NPC.life <= 0)
        {
            NPC.life = 0;
            NPC.checkDead();
        }
    }
    /// <summary>
    /// 收到玩家的回血，按同样数值回血
    /// </summary>
    /// <param name="amount">回血量</param>
    internal void ReceiveCompanionHeal(int amount)
    {
        if (!IsCompanionFollowing || amount <= 0 || NPC.life >= NPC.lifeMax)
        {
            return;
        }
        NPC.life = Math.Min(NPC.lifeMax, NPC.life + amount);
        NPC.netUpdate = true;
        _healthBarTimer = HealthBarShowTicks;
        if (!Main.dedServ)
        {
            Dust dust = Dust.NewDustPerfect(
                NPC.Center,
                DustID.GreenTorch,
                Main.rand.NextVector2Circular(1.5f, 1.5f),
                0,
                default,
                1f);
            dust.noGravity = true;
        }
    }
    /// <summary>
    /// 取当前跟随按钮的文本
    /// </summary>
    /// <returns>本地化按钮文本</returns>
    internal string GetFollowButtonLabel()
    {
        return Language.GetTextValue(
            IsFollowing ? "Mods.sea.NPCs.JinlianNPC.ButtonStopFollow" : "Mods.sea.NPCs.JinlianNPC.ButtonFollow"
        );
    }
    /// <summary>
    /// 名字是否已经揭晓：自我介绍之后、或首次剧情已经完结
    /// </summary>
    /// <returns>已揭晓返回 true</returns>
    /// <remarks>Used by every place that renders her name, so hover text and the dialogue plate stay in sync.</remarks>
    internal bool HasRevealedName() => JinlianSpawnSystem.JinlianIntroduced || _nameRevealed;
    /// <summary>
    /// 对话框名牌上要显示的名字：自我介绍之前一律是「？？？」
    /// </summary>
    /// <returns>显示名</returns>
    /// <remarks>The real name only appears once she introduces herself, and stays revealed after the intro is finished.</remarks>
    internal string GetDisplayName() => HasRevealedName() ? Language.GetTextValue("Mods.sea.NPCs.JinlianNPC.DisplayName") : GetDialogueOption("UnknownName");
    /// <summary>
    /// 统一替换 NPC 在鼠标悬停、状态栏等处的名字，避免只有对话框是「？？？」
    /// </summary>
    /// <param name="typeName">原本解析出的类型名</param>
    public override void ModifyTypeName(ref string typeName)
    {
        if (!HasRevealedName())
        {
            typeName = GetDialogueOption("UnknownName");
        }
    }
    /// <summary>
    /// 剧情对话是否正在进行
    /// </summary>
    internal bool IsDialogueActive => _dialogueStep != DialogueNone;
    /// <summary>
    /// 取当前剧情步骤的台词
    /// </summary>
    /// <returns>本地化台词</returns>
    internal string GetActiveDialogueText() => GetCurrentDialogueText();
    /// <summary>
    /// 取当前剧情步骤的选项按钮文本
    /// </summary>
    /// <param name="firstButton">第一个选项文本</param>
    /// <param name="secondButton">第二个选项文本，没有时为空串</param>
    /// <param name="thirdButton">第三个选项文本，没有时为空串</param>
    /// <remarks>The three answers to the request are listed in a single page, so the panel simply draws every non empty label from top to bottom.</remarks>
    internal void GetActiveDialogueOptions(out string firstButton, out string secondButton, out string thirdButton)
    {
        secondButton = string.Empty;
        thirdButton = string.Empty;
        switch (_dialogueStep)
        {
            case DialogueIntroLine1:
                firstButton = GetDialogueOption("IntroWho");
                return;
            case DialogueIntroLineIdentity:
                firstButton = GetDialogueOption("IntroAsk");
                return;
            case DialogueFirstReviveLine2:
                firstButton = GetDialogueOption("FirstReviveOptionBlessing");
                return;
            case DialogueHealLine:
                firstButton = GetDialogueOption("ButtonClose");
                return;
            case DialogueQuestion:
                firstButton = GetDialogueOption("IntroOptionAgree");
                secondButton = GetDialogueOption("IntroOptionReward");
                thirdButton = GetDialogueOption("IntroOptionBlame");
                return;
            case DialogueAcceptStart:
                firstButton = GetDialogueOption("Continue");
                return;
            case DialogueAcceptPolitely2:
            case DialogueAcceptDoubting3:
                firstButton = GetDialogueOption("Accept");
                return;
            default:
                firstButton = GetDialogueOption("Continue");
                return;
        }
    }
    /// <summary>
    /// 点击当前剧情步骤的选项
    /// </summary>
    /// <param name="optionIndex">被点击的选项序号，从上到下从 0 开始</param>
    internal void ClickActiveDialogueOption(int optionIndex)
    {
        // 冷却期内忽略，避免同一次点击推进两步
        if (_dialogueClickCooldown > 0)
        {
            return;
        }
        Mod.Logger.Info($"堇涟剧情点击 步骤={_dialogueStep} 选项={optionIndex} / Jinlian dialogue clicked step={_dialogueStep} option={optionIndex}");
        AdvanceDialogue(optionIndex);
        _dialogueClickCooldown = DialogueClickCooldown;
    }
    /// <summary>
    /// 取对话框立绘使用的表情差分序号
    /// </summary>
    /// <returns>1 到 8 之间的表情序号</returns>
    /// <remarks>Every dialogue step maps to the matching expression sheet: the intro walks 1 -> 8 -> 6 -> 7 and each answer branch switches the face again until the story ends.</remarks>
    internal int GetDialogueFaceIndex()
    {
        return _dialogueStep switch
        {
            DialogueIntroLine1 => 1,
            DialogueIntroLineIdentity => 1,
            DialogueIntroLine2 => DailyFaceIndex,
            DialogueIntroLine3 => 6,
            DialogueQuestion => DoubtFaceIndex,
            DialogueAcceptStart => DailyFaceIndex,
            DialogueAcceptPolitely1 => 1,
            DialogueAcceptPolitely2 => 4,
            DialogueAcceptDoubting1 => 6,
            DialogueAcceptDoubting2 => 4,
            DialogueAcceptDoubting3 => DoubtFaceIndex,
            DialogueGiftLine1 => DailyFaceIndex,
            DialogueGiftLine2 => DoubtFaceIndex,
            DialogueReviveLine1 => 3,
            // 首次复活剧情：#2 -> #7 -> #8 -> #5 -> #7
            DialogueFirstReviveLine1 => 2,
            DialogueFirstReviveLine2 => DoubtFaceIndex,
            DialogueFirstReviveLine3 => DailyFaceIndex,
            DialogueFirstReviveLine4 => 5,
            DialogueFirstReviveLine5 => DoubtFaceIndex,
            // 治疗台词统一用 #5
            DialogueHealLine => 5,
            _ => DailyFaceIndex,
        };
    }
    /// <summary>
    /// 推进首次对话剧情
    /// </summary>
    /// <param name="optionIndex">被点击的选项序号，从上到下从 0 开始</param>
    private void AdvanceDialogue(int optionIndex)
    {
        switch (_dialogueStep)
        {
            case DialogueIntroLine1:
                SetDialogueStep(DialogueIntroLineIdentity);
                return;
            case DialogueIntroLineIdentity:
                SetDialogueStep(DialogueIntroLine2);
                return;
            case DialogueIntroLine2:
                SetDialogueStep(DialogueIntroLine3);
                return;
            case DialogueIntroLine3:
                SetDialogueStep(DialogueQuestion);
                return;
            case DialogueQuestion:
                SetDialogueStep(optionIndex switch
                {
                    0 => DialogueAcceptStart,
                    1 => DialogueAcceptPolitely1,
                    _ => DialogueAcceptDoubting1
                });
                return;
            case DialogueAcceptStart:
                FinishIntro();
                return;
            case DialogueAcceptPolitely1:
                SetDialogueStep(DialogueAcceptPolitely2);
                return;
            case DialogueAcceptPolitely2:
                FinishIntro();
                return;
            case DialogueAcceptDoubting1:
                SetDialogueStep(DialogueAcceptDoubting2);
                return;
            case DialogueAcceptDoubting2:
                SetDialogueStep(DialogueAcceptDoubting3);
                return;
            case DialogueAcceptDoubting3:
                FinishIntro();
                return;
            case DialogueGiftLine1:
                SetDialogueStep(DialogueGiftLine2);
                GrantJellyfishGift();
                return;
            case DialogueGiftLine2:
                FinishGift();
                return;
            case DialogueReviveLine1:
                FinishReviveStory();
                return;
            case DialogueFirstReviveLine1:
                SetDialogueStep(DialogueFirstReviveLine2);
                return;
            case DialogueFirstReviveLine2:
                SetDialogueStep(DialogueFirstReviveLine3);
                return;
            case DialogueFirstReviveLine3:
                SetDialogueStep(DialogueFirstReviveLine4);
                return;
            case DialogueFirstReviveLine4:
                SetDialogueStep(DialogueFirstReviveLine5);
                return;
            case DialogueFirstReviveLine5:
                FinishFirstReviveStory();
                return;
            case DialogueHealLine:
                FinishHealLine();
                return;
            default:
                return;
        }
    }
    /// <summary>
    /// 切到指定剧情步骤并刷新聊天框文本
    /// </summary>
    /// <param name="step">目标步骤</param>
    private void SetDialogueStep(int step)
    {
        Mod.Logger.Info($"堇涟剧情步骤 {_dialogueStep} -> {step} / Jinlian story step {_dialogueStep} -> {step}");
        _dialogueStep = step;

        if (step == DialogueIntroLineIdentity)
        {
            _nameRevealed = true;
        }

        Main.npcChatText = GetDialogueText(step);
    }
    /// <summary>
    /// 结束首次对话，剧情打完直接关掉对话框
    /// </summary>
    private void FinishIntro()
    {
        JinlianSpawnSystem.MarkJinlianIntroduced();
        _dialogueStep = DialogueNone;
        NPC.ai[0] = 1f;
        NPC.netUpdate = true;
        CloseChatAfterStory();
    }
    /// <summary>
    /// 取剧情某一步的台词
    /// </summary>
    /// <param name="step">剧情步骤</param>
    /// <returns>本地化台词</returns>
    /// <summary>
    /// 取当前步骤要显示的台词
    /// </summary>
    /// <returns>本地化台词</returns>
    /// <remarks>The heal line is picked at random and stored in a field, so it cannot go through the static step table.</remarks>
    private string GetCurrentDialogueText()
    {
        if (_dialogueStep == DialogueHealLine)
        {
            return GetDialogueOption("HealLine" + (_healLineIndex + 1));
        }

        return GetDialogueText(_dialogueStep);
    }
    /// <summary>
    /// 按步骤取台词
    /// </summary>
    /// <param name="step">剧情步骤</param>
    /// <returns>本地化台词</returns>
    private static string GetDialogueText(int step)
    {
        return step switch
        {
            DialogueIntroLine1 => GetDialogueOption("IntroLine1"),
            DialogueIntroLineIdentity => GetDialogueOption("IntroLineIdentity"),
            DialogueIntroLine2 => GetDialogueOption("IntroLine2"),
            DialogueIntroLine3 => GetDialogueOption("IntroLine3"),
            DialogueQuestion => GetDialogueOption("IntroLine4"),
            DialogueAcceptStart => GetDialogueOption("IntroAnswerAgree"),
            DialogueAcceptPolitely1 => GetDialogueOption("IntroAnswerReward1"),
            DialogueAcceptPolitely2 => GetDialogueOption("IntroAnswerReward2"),
            DialogueAcceptDoubting1 => GetDialogueOption("IntroAnswerBlame1"),
            DialogueAcceptDoubting2 => GetDialogueOption("IntroAnswerBlame2"),
            DialogueAcceptDoubting3 => GetDialogueOption("IntroAnswerBlame3"),
            DialogueGiftLine1 => GetDialogueOption("GiftLine1"),
            DialogueGiftLine2 => GetDialogueOption("GiftLine2"),
            DialogueReviveLine1 => GetDialogueOption("ReviveLine1"),
            DialogueFirstReviveLine1 => GetDialogueOption("FirstReviveLine1"),
            DialogueFirstReviveLine2 => GetDialogueOption("FirstReviveLine2"),
            DialogueFirstReviveLine3 => GetDialogueOption("FirstReviveLine3"),
            DialogueFirstReviveLine4 => GetDialogueOption("FirstReviveLine4"),
            DialogueFirstReviveLine5 => GetDialogueOption("FirstReviveLine5"),
            DialogueHealLine => string.Empty,
            _ => string.Empty,
        };
    }
    /// <summary>
    /// 抽一句日常闲聊，避免和上一次重复
    /// </summary>
    /// <returns>本地化台词</returns>
    /// <remarks>Every line has the same chance; the line used the previous time is excluded so it can only come back one conversation later.</remarks>
    private string GetDailyChatText()
    {
        int index = Main.rand.Next(DailyChatCount);
        if (index == _lastDailyChatIndex)
        {
            index = (index + 1 + Main.rand.Next(DailyChatCount - 1)) % DailyChatCount;
        }
        _lastDailyChatIndex = index;
        return GetDialogueOption("ChatTextDaily" + (index + 1));
    }
    /// <summary>
    /// 取剧情本地化键
    /// </summary>
    /// <param name="key">键名</param>
    /// <returns>对应文本</returns>
    private static string GetDialogueOption(string key) => Language.GetTextValue("Mods.sea.NPCs.JinlianNPC." + key);
    /// <summary>
    /// 返回当前应该绘制的贴图帧号
    /// </summary>
    /// <returns>帧号（0-7）</returns>
    /// <remarks>Idle shows the back sheet when the last facing was right and the front sheet when it was left, matching the art direction of the character.</remarks>
    private int GetFrameIndex()
    {
        if (NPC.IsABestiaryIconDummy)
        {
            return FrontFrame;
        }
        // 漂浮动作只在跟随玩家且飞在空中时使用，贴地跟随和日常闲逛都用地面帧
        if (IsFollowing && _followHovering)
        {
            return GetFloatFrame();
        }
        Player talkingPlayer = FindTalkingPlayer();
        if (talkingPlayer != null)
        {
            // 玩家在左侧则转身朝左，反之朝右
            return talkingPlayer.Center.X < NPC.Center.X ? FacingLeftIdleFrame : FacingRightIdleFrame;
        }
        bool facingRight = Math.Abs(NPC.velocity.X) > 0.05f ? NPC.velocity.X > 0f : NPC.direction > 0;
        if (Math.Abs(NPC.velocity.X) > 0.05f)
        {
            int step = (_walkAnimationTimer / WalkFrameInterval) % 4;
            return facingRight ? GetFacingRightWalkFrame(step) : GetFacingLeftWalkFrame(step);
        }
        return facingRight ? BackFrame : FrontFrame;
    }
    /// <summary>
    /// 取漂浮动作当前帧
    /// </summary>
    /// <returns>帧号（8-11）</returns>
    /// <remarks>The author plays the floating frames as 2, 3, 4, 1, which are sheet frames 9, 10, 11 and 8.</remarks>
    private int GetFloatFrame()
    {
        int step = (_floatAnimationTimer / FloatFrameInterval) % 4;
        return step switch
        {
            0 => FloatFrame2,
            1 => FloatFrame3,
            2 => FloatFrame4,
            _ => FloatFrame1,
        };
    }
    /// <summary>
    /// 取朝向右侧行走循环中的帧
    /// </summary>
    /// <param name="step">循环步号（0-3）</param>
    /// <returns>帧号</returns>
    /// <remarks>The right facing cycle is right foot forward, side idle, left foot forward, side idle.</remarks>
    private static int GetFacingRightWalkFrame(int step)
    {
        return step switch
        {
            0 => FacingRightRightFootFrame,
            1 => FacingRightIdleFrame,
            2 => FacingRightLeftFootFrame,
            _ => FacingRightIdleFrame,
        };
    }
    /// <summary>
    /// 取朝向左侧行走循环中的帧
    /// </summary>
    /// <param name="step">循环步号（0-3）</param>
    /// <returns>帧号</returns>
    /// <remarks>The left facing cycle is right foot forward, side idle, left foot forward, side idle.</remarks>
    private static int GetFacingLeftWalkFrame(int step)
    {
        return step switch
        {
            0 => FacingLeftRightFootFrame,
            1 => FacingLeftIdleFrame,
            2 => FacingLeftLeftFootFrame,
            _ => FacingLeftIdleFrame,
        };
    }
    /// <summary>
    /// 在原地附近小范围走动
    /// </summary>
    private void Wander()
    {
        NPC.localAI[0] -= 1f;
        if (_turnCooldown > 0)
        {
            _turnCooldown--;
        }
        if (NPC.localAI[0] <= 0f)
        {
            if (NPC.localAI[1] <= 0f)
            {
                BeginWalk();
            }
            else
            {
                BeginRest();
            }
            return;
        }
        if (NPC.localAI[1] <= 0f)
        {
            NPC.velocity.X = 0f;
            return;
        }
        // 方向只在 BeginWalk() 里决定一次，走动过程中不再逐帧改写：
        // 逐帧改写会和她自己的掉头逻辑打架，表现为在边界上来回抖
        float direction = NPC.localAI[2];
        // 紧邻一格没地面说明已经站在坑边，三格内没地面说明前方是坑，两种情况都先尝试跳过、再掉头
        bool blockedAhead = HasWallAhead(direction)
            || !HasGroundWithinTiles(direction, 1)
            || !HasGroundWithinTiles(direction, PitLookAheadTiles);
        if (blockedAhead)
        {
            // 先试着跳过去 / 跳上一格高的台阶，跳不过去再考虑折返
            if (TryJumpOverObstacle(direction))
            {
                return;
            }
            float reversed = -direction;
            // 反方向能走就立刻回身并当帧迈步，不要在边缘站着等下一帧决定
            if (IsDirectionWalkable(reversed))
            {
                bool directionChanged = NPC.localAI[2] != reversed;
                NPC.localAI[2] = reversed;
                NPC.velocity.X = reversed * WalkSpeed;
                FaceTowards(NPC.Center.X + reversed);
                // 冷却只用来限制「连续掉头」的频率，不影响这次回身本身
                if (_turnCooldown <= 0)
                {
                    _lastWalkDirection = reversed;
                    _sameDirectionWalks = 1;
                    _turnCooldown = TurnCooldownTicks;
                    // 记下这个走不通的方向，下一段不再朝它走
                    _forbiddenDirection = direction;
                    // 回身之后把剩下的路程走完，至少再走一段最短距离，
                    // 否则刚转身就到点休息，下一段又朝坑走过来，看起来还是在边缘反复
                    _walkTargetDistance = Math.Max(_walkTargetDistance, _walkedDistance + MinWalkDistance);
                    NPC.localAI[0] = Math.Max(NPC.localAI[0], (int)((_walkTargetDistance - _walkedDistance) / WalkSpeed) + 30);
                    if (directionChanged)
                    {
                        Mod.Logger.Info($"堇涟前方受阻 原方向={direction:0} 改走={reversed:0} / Jinlian blocked dir={direction:0} turns to {reversed:0}");
                    }
                }
            }
            else if (CanStepOff(direction))
            {
                // 站在孤立方块或小台阶上时走下边缘，落到下面的地面继续逛，避免原地站着不动
                NPC.localAI[2] = direction;
                _jumpDirection = direction;
                NPC.velocity.X = direction * WalkSpeed;
            }
            else
            {
                BeginRest();
            }
            return;
        }
        NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, direction * WalkSpeed, 0.1f);
        FaceTowards(NPC.Center.X + direction);
        // 按实际走过的水平距离结算，走满三到十格就停下休息
        _walkedDistance += Math.Abs(NPC.velocity.X);
        if (_walkedDistance >= _walkTargetDistance)
        {
            BeginRest();
        }
    }
    /// <summary>
    /// 开始一段行走
    /// </summary>
    /// <remarks>The direction is random with a bias towards turning back, and the same direction is never used more than twice in a row. Segment length varies between three and ten tiles.</remarks>
    private void BeginWalk()
    {
        _walkTargetDistance = Main.rand.NextFloat(MinWalkDistance, MaxWalkDistance);
        _walkedDistance = 0f;
        float direction = ChooseWalkDirection();
        // 超出活动范围时这一段朝家走，避免越走越远；回家路不通就保持原选择
        float homeX = NPC.ai[1] * 16f + 8f;
        if (Math.Abs(NPC.Center.X - homeX) > WanderRadius)
        {
            float homeDirection = Math.Sign(homeX - NPC.Center.X);
            if (IsDirectionWalkable(homeDirection))
            {
                direction = homeDirection;
            }
        }
        // 上一段被坑或墙逼着回头的方向，这一段不再朝它走
        if (direction == _forbiddenDirection)
        {
            direction = -direction;
        }
        _forbiddenDirection = 0f;
        if (!IsDirectionWalkable(direction))
        {
            float opposite = -direction;
            if (IsDirectionWalkable(opposite))
            {
                direction = opposite;
            }
        }
        if (direction == _lastWalkDirection)
        {
            _sameDirectionWalks++;
        }
        else
        {
            _lastWalkDirection = direction;
            _sameDirectionWalks = 1;
        }
        // 安全上限：万一被地形卡住也不会一直算作「正在走」
        NPC.localAI[0] = (int)(_walkTargetDistance / WalkSpeed) + 90;
        NPC.localAI[1] = 1f;
        NPC.localAI[2] = direction;
        Mod.Logger.Info($"堇涟开始闲逛 方向={direction:0} 目标距离={_walkTargetDistance:0} 同向计数={_sameDirectionWalks} / Jinlian starts wandering dir={direction:0} distance={_walkTargetDistance:0}");
    }
    /// <summary>
    /// 选择下一段行走的方向
    /// </summary>
    /// <remarks>Random, but more likely to turn back; two consecutive segments in the same direction force a turn.</remarks>
    private float ChooseWalkDirection()
    {
        if (_lastWalkDirection == 0f)
        {
            return Main.rand.NextBool() ? 1f : -1f;
        }
        if (_sameDirectionWalks >= MaxSameDirectionWalks)
        {
            return -_lastWalkDirection;
        }
        return Main.rand.NextFloat() < BacktrackChance ? -_lastWalkDirection : _lastWalkDirection;
    }
    /// <summary>
    /// 判断某个方向能不能走
    /// </summary>
    /// <param name="direction">水平方向（1 右，-1 左）</param>
    /// <returns>该方向有地面且没有墙时返回 true</returns>
    private bool IsDirectionWalkable(float direction) => !HasWallAhead(direction) && HasGroundWithinTiles(direction, PitLookAheadTiles);
    /// <summary>
    /// 判断能不能走下边缘
    /// </summary>
    /// <param name="direction">水平方向（1 右，-1 左）</param>
    /// <returns>前方确实是空的、且下方落差在可接受范围内时返回 true</returns>
    private bool CanStepOff(float direction)
    {
        if (HasWallAhead(direction))
        {
            return false;
        }
        int tileX = (int)((NPC.Center.X + direction * (NPC.width * 0.5f + 8f)) / 16f);
        int tileY = (int)((NPC.Bottom.Y + 8f) / 16f);
        // 前方同高度必须有空隙，否则那是墙或连续地面，不叫「边缘」
        if (WorldGen.SolidTile(tileX, tileY))
        {
            return false;
        }
        for (int step = 1; step <= MaxStepOffDropTiles; step++)
        {
            int y = tileY + step;
            if (y >= Main.maxTilesY)
            {
                return false;
            }
            if (WorldGen.SolidTile(tileX, y))
            {
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// 开始一段站立休息
    /// </summary>
    private void BeginRest()
    {
        NPC.localAI[0] = Main.rand.Next(MinPauseTime, MaxPauseTime);
        NPC.localAI[1] = 0f;
        NPC.localAI[3] = 0f;
        NPC.velocity.X = 0f;
    }
    /// <summary>
    /// 取跟随对象（本地玩家）的防御力，作为并肩作战时的共用防御。
    /// </summary>
    /// <returns>应当使用的防御力。</returns>
    private static int GetOwnerDefense()
    {
        Player owner = Main.LocalPlayer;
        if (owner is null || !owner.active || owner.dead)
        {
            return 0;
        }
        return owner.statDefense;
    }
    /// <summary>
    /// 每隔一段时间检查一次，满足条件就召唤水母
    /// </summary>
    private void UpdateJellyfishSummon()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }
        _summonTimer++;
        if (_summonTimer < JellyfishSummonInterval)
        {
            return;
        }
        _summonTimer = 0;
        TrySummonJellyfish();
    }
    /// <summary>
    /// 附近有敌人时召唤几只水母去追击
    /// </summary>
    private void TrySummonJellyfish()
    {
        NPC target = FindAttackTarget();
        if (target is null)
        {
            return;
        }
        int projectileType = ModContent.ProjectileType<JinlianJellyfishProjectile>();
        int owner = Player.FindClosest(NPC.Center, 1, 1);
        for (int i = 0; i < JellyfishSummonCount; i++)
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 spawnPosition = NPC.Center + new Vector2(MathF.Cos(angle) * 48f, MathF.Sin(angle) * 32f - 24f);
            Vector2 velocity = Vector2.Normalize(target.Center - spawnPosition) * 8f;
            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                spawnPosition,
                velocity,
                projectileType,
                JinlianJellyfishProjectile.DamageAmount,
                0f,
                owner);
        }
    }
    /// <summary>
    /// 找到附近最近的敌怪
    /// </summary>
    /// <returns>最近的敌怪，没有则返回 null</returns>
    private NPC FindAttackTarget()
    {
        NPC? closest = null;
        float closestDistance = JellyfishSearchRange * JellyfishSearchRange;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];
            if (!IsAttackTarget(other))
            {
                continue;
            }
            float distance = Vector2.DistanceSquared(other.Center, NPC.Center);
            if (distance >= closestDistance)
            {
                continue;
            }
            closestDistance = distance;
            closest = other;
        }
        return closest;
    }
    /// <summary>
    /// 判断某个 NPC 是否是可以攻击的敌人
    /// </summary>
    /// <returns>可以攻击时返回 true</returns>
    /// <remarks>Friendly NPCs, town NPCs, critters and immune targets are ignored.</remarks>
    private bool IsAttackTarget(NPC other)
    {
        if (!other.active || other.whoAmI == NPC.whoAmI)
        {
            return false;
        }
        if (other.friendly || other.townNPC || other.immortal || other.dontTakeDamage)
        {
            return false;
        }
        if (NPCID.Sets.CountsAsCritter[other.type])
        {
            return false;
        }
        return other.damage > 0 || other.boss;
    }
    /// <summary>
    /// 判断脚下是否有实心方块
    /// </summary>
    /// <returns>脚下有可站立地面时返回 true</returns>
    /// <remarks>Used to zero out the vertical velocity while standing, which the vanilla town NPC talk check requires.</remarks>
    private bool IsOnGround()
    {
        int tileY = (int)((NPC.Bottom.Y + 4f) / 16f);
        int leftTile = (int)(NPC.Left.X / 16f);
        int rightTile = (int)((NPC.Right.X - 1f) / 16f);
        for (int tileX = leftTile; tileX <= rightTile; tileX++)
        {
            if (WorldGen.SolidTile(tileX, tileY))
            {
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// 判断指定方向前方若干格内同一脚高度是否有地面
    /// </summary>
    /// <param name="direction">水平方向（1 右，-1 左）</param>
    /// <param name="tiles">向前检查的格数</param>
    /// <returns>检查范围内存在可站立地面时返回 true</returns>
    /// <remarks>Used both for pit look-ahead and for deciding whether a gap can be jumped across.</remarks>
    private bool HasGroundWithinTiles(float direction, int tiles)
    {
        int startTileX = (int)(NPC.Center.X / 16f);
        int feetTileY = (int)((NPC.Bottom.Y + 8f) / 16f);
        for (int step = 1; step <= tiles; step++)
        {
            int tileX = startTileX + (int)direction * step;
            if (WorldGen.SolidTile(tileX, feetTileY))
            {
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// 判断指定方向的前方是否有墙
    /// </summary>
    /// <param name="direction">水平方向（1 右，-1 左）</param>
    /// <returns>前方有阻挡时返回 true</returns>
    private bool HasWallAhead(float direction)
    {
        Vector2 checkPosition = new(
            NPC.Center.X + direction * (NPC.width / 2f + 6f),
            NPC.Bottom.Y - NPC.height + 10f
        );
        return Collision.SolidCollision(checkPosition, 2, NPC.height - 10);
    }
    /// <summary>
    /// 遇到台阶或沟坎时尝试起跳，跳过去或者跳上去
    /// </summary>
    /// <param name="direction">前进方向（1 右，-1 左）</param>
    /// <returns>成功起跳时返回 true</returns>
    /// <remarks>Jumps up to five tiles high so one tile ledges and small gaps no longer trap the NPC.</remarks>
    private bool TryJumpOverObstacle(float direction)
    {
        if (_jumpCooldown > 0 || !IsOnGround())
        {
            return false;
        }
        if (!HasLandingAhead(direction) && !HasGroundWithinTiles(direction, JumpMaxTiles))
        {
            return false;
        }
        NPC.velocity.Y = -JumpVelocity;
        NPC.velocity.X = direction * WalkSpeed;
        _jumpDirection = direction;
        _jumpCooldown = JumpCooldownTicks;
        NPC.netUpdate = true;
        return true;
    }
    /// <summary>
    /// 判断前方五格以内是否有能落脚的平台
    /// </summary>
    /// <param name="direction">前进方向（1 右，-1 左）</param>
    /// <returns>存在可落脚平台时返回 true</returns>
    /// <remarks>Landing spots need two tiles of headroom so the jump does not bump into a ceiling.</remarks>
    private bool HasLandingAhead(float direction)
    {
        int tileX = (int)((NPC.Center.X + direction * (NPC.width / 2f + 8f)) / 16f);
        int feetTileY = (int)((NPC.Bottom.Y + 8f) / 16f);
        for (int step = 1; step <= JumpMaxTiles; step++)
        {
            int tileY = feetTileY - step;
            if (!WorldGen.SolidTile(tileX, tileY))
            {
                continue;
            }
            if (WorldGen.SolidTile(tileX, tileY - 1) || WorldGen.SolidTile(tileX, tileY - 2))
            {
                continue;
            }
            return true;
        }
        return false;
    }
    /// <summary>
    /// 让 NPC 朝向指定横坐标
    /// </summary>
    /// <param name="targetX">目标横坐标</param>
    private void FaceTowards(float targetX)
    {
        if (Math.Abs(targetX - NPC.Center.X) < 2f)
        {
            return;
        }
        int direction = targetX > NPC.Center.X ? 1 : -1;
        NPC.direction = direction;
        NPC.spriteDirection = direction;
    }
    /// <summary>
    /// 临时诊断：靠近时输出对话相关状态，确认原版是否把玩家和该 NPC 关联起来
    /// </summary>
    /// <param name="talkingPlayer">本次查到的对话玩家</param>
    /// <remarks>Diagnostic only: reports the vanilla talk state every two seconds while a player stands next to the NPC.</remarks>
    private void LogChatState(Player talkingPlayer)
    {
        _chatLogTimer++;
        if (_chatLogTimer < 120)
        {
            return;
        }
        _chatLogTimer = 0;
        if (Math.Abs(Main.LocalPlayer.Center.X - NPC.Center.X) > TalkingFaceRange)
        {
            return;
        }
        string chatText = Main.npcChatText ?? string.Empty;
        Mod.Logger.Info(
            $"堇涟对话诊断 talkNPC={Main.LocalPlayer.talkNPC} whoAmI={NPC.whoAmI} 命中对话={talkingPlayer != null} 聊天框长度={chatText.Length} velocityY={NPC.velocity.Y:0.00} velocityX={NPC.velocity.X:0.00} 当前帧={GetFrameIndex()} 动画计时={_walkAnimationTimer} / Jinlian talk diagnostic"
        );
    }
    /// <summary>
    /// 找出正在和该 NPC 对话的玩家
    /// </summary>
    /// <returns>正在对话的玩家，没有则返回 null</returns>
    private Player? FindTalkingPlayer()
    {
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];
            if (!player.active || player.talkNPC != NPC.whoAmI)
            {
                continue;
            }
            if (Math.Abs(player.Center.X - NPC.Center.X) > TalkingFaceRange)
            {
                continue;
            }
            return player;
        }
        return null;
    }
}
