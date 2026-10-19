using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public class WoodPlayer : ModPlayer
{
    public int Layers { get; private set; }
    public int CombatFrames { get; private set; }
    public int RecentCombat { get; private set; }
    private int decay;
    private string currentSoul = "";
    private readonly Dictionary<string, ulong> cooldowns = [];
    private float regenRemainder;
    public SoulItem HeldSoul => !Player.dead && Player.HeldItem.ModItem is SoulItem soul && soul.Attached ? soul : null;
    public WoodLoadout Loadout => HeldSoul?.Wood ?? default;
    public int LayersFor(SoulItem soul) => currentSoul == soul.SoulId && soul.Wood.Full ? Layers : 0;
    public bool Ready(string key, int frames)
    {
        ulong tick = Main.GameUpdateCount;
        if (cooldowns.TryGetValue(key, out ulong until) && tick < until) return false;
        cooldowns[key] = tick + (ulong)frames;
        return true;
    }
    public void RecordHit(SoulItem soul)
    {
        if (HeldSoul?.SoulId != soul.SoulId || !soul.Wood.Full) return;
        if (currentSoul != soul.SoulId) { ResetGrowth(); currentSoul = soul.SoulId; }
        RecentCombat = 300;
    }
    public void ResetGrowth() { Layers = CombatFrames = RecentCombat = decay = 0; currentSoul = ""; }
    public void AdvanceGrowth()
    {
        SoulItem soul = HeldSoul;
        if (soul == null || !soul.Wood.Full) { ResetGrowth(); return; }
        if (currentSoul != soul.SoulId) { ResetGrowth(); currentSoul = soul.SoulId; }
        if (RecentCombat > 0)
        {
            RecentCombat--; decay = 0;
            if (Layers < 12 && ++CombatFrames >= 300) { Layers++; CombatFrames = 0; }
        }
        else if (++decay >= 120)
        {
            decay = 0;
            Layers = Math.Max(0, Layers - 1);
            if (Layers == 0) CombatFrames = 0;
        }
    }
    public override void PreUpdateBuffs()
    {
        AdvanceGrowth();
        WoodLoadout wood = Loadout;
        if (wood.Full) Player.AddBuff(ModContent.BuffType<ForestPower>(), 2);
        else Player.ClearBuff(ModContent.BuffType<ForestPower>());
        if (!wood.Has(WoodKind.Forest)) Player.ClearBuff(ModContent.BuffType<ForestBreath>());
        if (!wood.Has(WoodKind.Shade)) Player.ClearBuff(ModContent.BuffType<BloodFragrance>());
        if (!wood.Has(WoodKind.Dynasty)) Player.ClearBuff(ModContent.BuffType<DynastyOrder>());
    }
    public static bool IsShortenable(int type) => type is BuffID.Poisoned or BuffID.OnFire or BuffID.Darkness or
        BuffID.Cursed or BuffID.Bleeding or BuffID.Slow or BuffID.Weak or BuffID.Silenced or BuffID.BrokenArmor or
        BuffID.Confused or BuffID.Blackout or BuffID.Chilled or BuffID.Frostburn or BuffID.Venom or BuffID.CursedInferno or BuffID.Ichor;
    public override void PostUpdateEquips()
    {
        var soul = HeldSoul;
        if (soul == null) return;
        Player.statLifeMax2 += soul.Wood.Life;
        int layers = LayersFor(soul);
        Player.statDefense += layers * 3;
        Player.endurance += layers * .01f;
    }
    public override void UpdateLifeRegen()
    {
        var soul = HeldSoul;
        float perSecond = soul == null ? 0 : LayersFor(soul) * .5f;
        if (soul != null && soul.Wood.Has(WoodKind.Forest) && Player.HasBuff(ModContent.BuffType<ForestBreath>()))
            perSecond += .25f * soul.Wood.Strength;
        // Fractional lifeRegen units accumulate; no per-frame minimum heal and no Heal hook.
        if (perSecond <= 0) { regenRemainder = 0; return; }
        regenRemainder += perSecond * 2;
        int whole = (int)regenRemainder;
        regenRemainder -= whole;
        Player.lifeRegen += whole;
    }
    public int BoostSoulHealing(int baseAmount, int remainingBudget)
    {
        float bonus = Loadout.Has(WoodKind.Shade) && Player.HasBuff(ModContent.BuffType<BloodFragrance>()) ? .04f * Loadout.Strength : 0;
        return Math.Clamp((int)(baseAmount * (1 + bonus)), 0, Math.Max(0, remainingBudget));
    }
    // Shared entry for direct-healing sword traits. Never scales unrelated potions or global life steal.
    public int HealFromSoul(SoulItem soul, int baseAmount, ref float remainingBudget)
    {
        if (HeldSoul?.SoulId != soul.SoulId || baseAmount <= 0 || remainingBudget < 1 || Player.dead) return 0;
        int amount = Math.Min(Player.statLifeMax2 - Player.statLife, BoostSoulHealing(baseAmount, (int)remainingBudget));
        if (amount <= 0) return 0;
        Player.Heal(amount);
        remainingBudget -= amount;
        return amount;
    }
    public void GrantHitBuffs(SoulItem soul)
    {
        if (HeldSoul?.SoulId != soul.SoulId) return;
        var wood = soul.Wood;
        if (wood.Has(WoodKind.Forest) && Ready(soul.SoulId + ":breath", 60)) Player.AddBuff(ModContent.BuffType<ForestBreath>(), 360);
        if (wood.Has(WoodKind.Shade) && Ready(soul.SoulId + ":blood", 60)) Player.AddBuff(ModContent.BuffType<BloodFragrance>(), 360);
        if (wood.Has(WoodKind.Dynasty) && Ready(soul.SoulId + ":order", 60)) Player.AddBuff(ModContent.BuffType<DynastyOrder>(), 360);
    }
    public override void UpdateDead() => ResetGrowth();
    public override void OnEnterWorld() { ResetGrowth(); cooldowns.Clear(); }
    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        if (Main.netMode != NetmodeID.Server) return;
        var packet = Mod.GetPacket(); packet.Write((byte)4); packet.Write((byte)Player.whoAmI);
        packet.Write(currentSoul); packet.Write((byte)Layers); packet.Write((ushort)CombatFrames);
        packet.Write((ushort)RecentCombat); packet.Write((byte)decay);
        packet.Send(toWho, fromWho);
    }
    public void ReceiveGrowth(BinaryReader reader)
    {
        currentSoul = reader.ReadString(); Layers = Math.Clamp((int)reader.ReadByte(), 0, 12);
        CombatFrames = Math.Clamp((int)reader.ReadUInt16(), 0, 299);
        RecentCombat = Math.Clamp((int)reader.ReadUInt16(), 0, 300); decay = Math.Clamp((int)reader.ReadByte(), 0, 119);
    }
    public override void PostUpdate()
    {
        if (Main.netMode == NetmodeID.Server && Main.GameUpdateCount % 60 == 0) SyncPlayer(-1, -1, false);
    }
}

public class WoodBuffHooks : ModSystem
{
    public override void Load() => On_Player.AddBuff += ShortenDebuff;
    public override void Unload() => On_Player.AddBuff -= ShortenDebuff;
    private static void ShortenDebuff(On_Player.orig_AddBuff orig, Player player, int type, int timeToAdd, bool quiet, bool foodHack)
    {
        var wood = player.GetModPlayer<WoodPlayer>();
        if (WoodPlayer.IsShortenable(type) && wood.Loadout.Has(WoodKind.Dynasty) && player.HasBuff(ModContent.BuffType<DynastyOrder>()))
            timeToAdd = Math.Max(1, (int)(timeToAdd * (1 - .04f * wood.Loadout.Strength)));
        orig(player, type, timeToAdd, quiet, foodHack);
    }
}

public abstract class WoodPlayerBuff : ModBuff
{
    public override string Texture => "Terraria/Images/Buff_" + BuffID.DryadsWard;
    public override void SetStaticDefaults() { Main.buffNoSave[Type] = true; }
}
public class ForestBreath : WoodPlayerBuff { }
public class BloodFragrance : WoodPlayerBuff { }
public class DynastyOrder : WoodPlayerBuff { }
public class ForestPower : WoodPlayerBuff
{
    public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
    {
        var wood = Main.LocalPlayer.GetModPlayer<WoodPlayer>();
        int n = wood.Layers;
        tip = $"森林之力 {n}/12层 · 生命＋100 · 真实剑位＋2\n主攻伤害＋{n*3}% · 出剑速度＋{n*2}% · 防御＋{n*3}\n自然回复＋{n*.5f:0.#}/秒 · 自有持续伤害＋{n*5}% · 减伤＋{n}个百分点\n有效战斗60秒满层，脱战衰减；失去全套立即失效";
    }
}
