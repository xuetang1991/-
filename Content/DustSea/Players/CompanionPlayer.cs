// sea - Copyright (c) 2026 血堂在何方
// Created: 2026-11-01

using sea.Content.DustSea.Buffs;
using sea.Content.DustSea.NPCs;
using sea.Content.DustSea.Systems;

using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
namespace sea.Content.DustSea.Players;
/// <summary>
/// 「并肩作战」的玩家侧逻辑：挂增益、转移伤害、把回血分给堇涟。
/// </summary>
/// <remarks>
/// Player side of the Stand Together buff: it keeps the buff applied, redirects incoming damage to Jinlian
/// and forwards the player's healing to her.
/// </remarks>
public class CompanionPlayer : ModPlayer
{
    /// <summary>
    /// 单次转移回血的上限，避免复活、生命果之类的瞬间跳变把她的血直接灌满。
    /// </summary>
    private const int MaxHealTransfer = 200;

    /// <summary>
    /// 免伤成功后给玩家的无敌帧长度，和原版受击无敌时间一致。
    /// </summary>
    private const int CompanionDodgeImmuneTicks = 60;
    /// <summary>
    /// 上一帧玩家的生命值，用来算「这一帧回了多少血」。
    /// </summary>
    private int _lastLife = -1;
    /// <summary>
    /// 缓存的增益类型 id。
    /// </summary>
    private int _buffType = -1;
    /// <summary>
    /// 每帧刷新增益、计算回血转移。
    /// </summary>
    public override void PostUpdate()
    {
        JinlianNPC companion = GetCompanion();
        if (companion is null)
        {
            _lastLife = Player.statLife;
            return;
        }
        if (_buffType < 0)
        {
            _buffType = ModContent.BuffType<CompanionBuff>();
        }
        Player.AddBuff(_buffType, 2);
        int healed = Player.statLife - _lastLife;
        if (_lastLife >= 0 && healed > 0)
        {
            companion.ReceiveCompanionHeal(System.Math.Min(healed, MaxHealTransfer));
        }
        _lastLife = Player.statLife;
    }
    /// <summary>
    /// 替玩家挡下这一击：玩家不受伤，伤害转到堇涟身上。
    /// </summary>
    /// <param name="info">本次受伤信息。</param>
    /// <returns>是否免伤。</returns>
    public override bool FreeDodge(Player.HurtInfo info)
    {
        JinlianNPC companion = GetCompanion();
        if (companion is null || info.Damage <= 0)
        {
            return false;
        }
        companion.TakeCompanionDamage(info.Damage);

        // 挡下来也算「挨过这一下」：给玩家无敌帧，否则 Boss 的接触伤害会每 tick 继续触发转移，
        // 她的血就会像被机关枪扫一样掉光
        Player.SetImmuneTimeForAllTypes(CompanionDodgeImmuneTicks);

        return true;
    }
    /// <summary>
    /// 玩家按下官方「快速治疗」键时，如果堇涟在掉血就喂她一瓶治疗药水。
    /// </summary>
    public override void PreUpdate()
    {
        if (Main.dedServ || Main.myPlayer != Player.whoAmI || !Player.active || Player.dead || Main.gameMenu || !Main.hasFocus)
        {
            return;
        }
        if (!PlayerInput.Triggers.JustPressed.QuickHeal)
        {
            return;
        }
        JinlianNPC companion = GetCompanion();
        if (companion is null || companion.NPC.life >= companion.NPC.lifeMax)
        {
            return;
        }
        TryUseHealingPotion(companion);
    }
    /// <summary>
    /// 玩家背包里有治疗药水时，替掉血的堇涟喝一瓶，把药水能回的血量补给她。
    /// </summary>
    /// <param name="companion">正在跟随的堇涟。</param>
    private void TryUseHealingPotion(JinlianNPC companion)
    {
        if (Player.dead || Player.cursed || Player.CCed || Player.potionDelay > 0)
        {
            return;
        }
        if (companion.NPC.life >= companion.NPC.lifeMax)
        {
            return;
        }
        int missingLife = companion.NPC.lifeMax - companion.NPC.life;
        Item potion = FindHealingPotion(missingLife);
        if (potion is null)
        {
            return;
        }
        int healAmount = potion.healLife;
        if (potion.UseSound is not null)
        {
            SoundEngine.PlaySound(potion.UseSound, Player.position);
        }
        int delay = Player.potionDelayTime > 0 ? Player.potionDelayTime : 3600;
        Player.potionDelay = delay;
        Player.AddBuff(BuffID.PotionSickness, delay);
        if (potion.consumable && ItemLoader.ConsumeItem(potion, Player))
        {
            potion.stack--;
        }
        if (potion.stack <= 0)
        {
            potion.TurnToAir();
        }
        Recipe.FindRecipes();
        companion.ReceiveCompanionHeal(healAmount);
    }
    /// <summary>
    /// 在背包里挑一瓶最合适的治疗药水：优先刚好够补上缺口的那档，避免浪费高级药。
    /// </summary>
    /// <param name="missingLife">堇涟当前缺的生命值。</param>
    /// <returns>挑中的药水，没有则返回 null。</returns>
    private Item FindHealingPotion(int missingLife)
    {
        Item? best = null;
        Item? fallback = null;
        for (int index = 0; index < Player.inventory.Length; index++)
        {
            Item item = Player.inventory[index];
            if (item is null || item.IsAir || item.stack <= 0 || !item.potion || item.healLife <= 0)
            {
                continue;
            }
            if (fallback is null || item.healLife < fallback.healLife)
            {
                fallback = item;
            }
            if (item.healLife >= missingLife && (best is null || item.healLife < best.healLife))
            {
                best = item;
            }
        }
        return best ?? fallback;
    }
    /// <summary>
    /// 取本玩家正在跟随的堇涟。
    /// </summary>
    /// <returns>正在跟随的堇涟，没有则返回 null。</returns>
    private JinlianNPC? GetCompanion()
    {
        if (Main.dedServ || !Player.active || Player.dead)
        {
            return null;
        }
        NPC npc = JinlianSpawnSystem.FindLiveJinlian();
        if (npc is not null && npc.ModNPC is JinlianNPC candidate && candidate.IsCompanionFollowing)
        {
            return candidate;
        }
        return null;
    }
}
