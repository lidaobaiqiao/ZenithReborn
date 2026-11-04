using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public class WoodPlayer : ModPlayer
{
    // 森林之力成长状态：成长引擎与对外查询契约在 ForestPower（ForestPower.cs），
    // 此处仅存放字段以沿用 ModPlayer 同步链路。
    public int Layers { get; internal set; }
    public int CombatFrames { get; internal set; }
    public int RecentCombat { get; internal set; }
    internal int Decay { get; set; }
    internal string CurrentSoul { get; set; } = "";
    private readonly Dictionary<string, ulong> cooldowns = [];
    private float regenRemainder;
    public SoulItem HeldSoul => !Player.dead && Player.HeldItem.ModItem is SoulItem soul && soul.Attached ? soul : null;
    public WoodLoadout Loadout => HeldSoul?.CombatWood ?? default;
    // 当前生效层数：手持魂与成长魂一致且 8 木全套时才非零。
    internal int EffectiveLayers => HeldSoul is { } soul && CurrentSoul == soul.SoulId && ForestPower.FullSet(soul) ? Layers : 0;
    public int LayersFor(SoulItem soul) => HeldSoul?.SoulId == soul.SoulId ? EffectiveLayers : 0;
    public bool Ready(string key, int frames)
    {
        ulong tick = Main.GameUpdateCount;
        if (cooldowns.TryGetValue(key, out ulong until) && tick < until) return false;
        cooldowns[key] = tick + (ulong)frames;
        return true;
    }
    // 对外入口保留：WoodCombat.FinishPrimaryHit/ReceiveHit 仍调 RecordHit，换魂/死亡清零仍调 ResetGrowth；
    // 成长规则（基础6层/归冠12层、脱战衰减、降级钳制）见 ForestPower。
    public void RecordHit(SoulItem soul) => ForestPower.RecordHit(this, soul);
    public void ResetGrowth() => ForestPower.Reset(this);
    public void AdvanceGrowth()
    {
        // 森林之力 V2 清除时机只有三种：换魂（HeldSoul.soulId 变化）、死亡（UpdateDead→ResetGrowth）、
        // 失去 8 木 Full（持魂但不再全套）；由 ForestPower.Advance 内部统一判定。
        // 手持非剑魂物品（镐子等）不再清零：HeldSoul 为空的帧直接跳过推进，保留层数与进度；
        // 此时 EffectiveLayers 因 HeldSoul 为空天然为 0，收益不生效（仅冻结积累与脱战衰减）。
        if (HeldSoul == null) return;
        ForestPower.Advance(this);
    }
    public override void PreUpdateBuffs()
    {
        AdvanceGrowth();
        WoodLoadout wood = Loadout;
        if (wood.Full) Player.AddBuff(ModContent.BuffType<ForestPower>(), 2);
        else Player.ClearBuff(ModContent.BuffType<ForestPower>());
        if (!wood.Has(WoodKind.Forest)) Player.ClearBuff(ModContent.BuffType<ForestBreath>());
        if (!wood.Has(WoodKind.Shade)) Player.ClearBuff(ModContent.BuffType<BloodFragrance>());
    }
    // 供 EnvironmentPlayer 的减益抗性判定使用；原御令的持续时间缩短钩子已随 WoodBuffHooks 一并移除。
    public static bool IsShortenable(int type) => type is BuffID.Poisoned or BuffID.OnFire or BuffID.Darkness or
        BuffID.Cursed or BuffID.Bleeding or BuffID.Slow or BuffID.Weak or BuffID.Silenced or BuffID.BrokenArmor or
        BuffID.Confused or BuffID.Blackout or BuffID.Chilled or BuffID.Frostburn or BuffID.Venom or BuffID.CursedInferno or BuffID.Ichor;
    public override void PostUpdateEquips()
    {
        var soul = HeldSoul;
        if (soul == null) return;
        Player.statLifeMax2 += soul.CombatWood.Life;
        Player.statDefense += ForestPower.DefenseBonus(Player);
        Player.endurance += ForestPower.DamageReduction(Player);
    }
    public override void UpdateLifeRegen()
    {
        var soul = HeldSoul;
        float perSecond = soul == null ? 0 : ForestPower.RegenBonus(Player);
        if (soul != null && soul.CombatWood.Has(WoodKind.Forest) && Player.HasBuff(ModContent.BuffType<ForestBreath>()))
            perSecond += .25f * soul.CombatWood.Strength;
        // Fractional lifeRegen units accumulate; no per-frame minimum heal and no Heal hook.
        if (perSecond <= 0) { regenRemainder = 0; return; }
        regenRemainder += perSecond * 2;
        int whole = (int)regenRemainder;
        regenRemainder -= whole;
        Player.lifeRegen += whole;
    }
    public int BoostSoulHealing(SoulItem soul, int baseAmount, int remainingBudget)
    {
        // S 与战斗侧同源：取该剑魂自身的 CombatWood.Strength，而不是手持 Loadout 的缓存值。
        float bonus = soul.CombatWood.Has(WoodKind.Shade) && Player.HasBuff(ModContent.BuffType<BloodFragrance>()) ? .04f * soul.CombatWood.Strength : 0;
        return Math.Clamp((int)(baseAmount * (1 + bonus)*Player.GetModPlayer<EnvironmentPlayer>().HealingMultiplier), 0, Math.Max(0, remainingBudget));
    }
    // Shared entry for direct-healing sword traits. Never scales unrelated potions or global life steal.
    public int HealFromSoul(SoulItem soul, int baseAmount, ref float remainingBudget)
    {
        if (HeldSoul?.SoulId != soul.SoulId || baseAmount <= 0 || remainingBudget < 1 || Player.dead) return 0;
        int amount = Math.Min(Player.statLifeMax2 - Player.statLife, BoostSoulHealing(soul, baseAmount, (int)remainingBudget));
        if (amount <= 0) return 0;
        Player.Heal(amount);
        remainingBudget -= amount;
        return amount;
    }
    public void GrantHitBuffs(SoulItem soul)
    {
        if (HeldSoul?.SoulId != soul.SoulId) return;
        var wood = soul.CombatWood;
        if (wood.Has(WoodKind.Forest) && Ready(soul.SoulId + ":breath", 60)) Player.AddBuff(ModContent.BuffType<ForestBreath>(), 360);
        if (wood.Has(WoodKind.Shade) && Ready(soul.SoulId + ":blood", 60)) Player.AddBuff(ModContent.BuffType<BloodFragrance>(), 360);
    }
    public override void UpdateDead() => ResetGrowth();
    public override void OnEnterWorld() { ResetGrowth(); cooldowns.Clear(); }
    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        if (Main.netMode != NetmodeID.Server) return;
        var packet = Mod.GetPacket(); packet.Write((byte)4); packet.Write((byte)Player.whoAmI);
        packet.Write(CurrentSoul); packet.Write((byte)Layers); packet.Write((ushort)CombatFrames);
        packet.Write((ushort)RecentCombat); packet.Write((byte)Decay);
        packet.Send(toWho, fromWho);
    }
    public void ReceiveGrowth(BinaryReader reader)
    {
        // 层数绝对上限取归冠上限（12）；失去归冠时 Advance 会钳制回 6。
        CurrentSoul = reader.ReadString(); Layers = Math.Clamp((int)reader.ReadByte(), 0, ForestPower.CrownCap);
        CombatFrames = Math.Clamp((int)reader.ReadUInt16(), 0, 299);
        RecentCombat = Math.Clamp((int)reader.ReadUInt16(), 0, 300); Decay = Math.Clamp((int)reader.ReadByte(), 0, 119);
    }
    public override void PostUpdate()
    {
        if (Main.netMode == NetmodeID.Server && Main.GameUpdateCount % 60 == 0) SyncPlayer(-1, -1, false);
    }
}

public abstract class WoodPlayerBuff : ModBuff
{
    public override string Texture => "Terraria/Images/Buff_" + BuffID.DryadsWard;
    public override void SetStaticDefaults() { Main.buffNoSave[Type] = true; }
}
public class ForestBreath : WoodPlayerBuff { }
public class BloodFragrance : WoodPlayerBuff { }
// ForestPower buff 类已迁至 ForestPower.cs（与森林之力查询契约、成长引擎同文件）。
