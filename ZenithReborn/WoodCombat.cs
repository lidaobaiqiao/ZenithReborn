using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

// Each contribution belongs to one player and one soul, not the NPC's global regen pool.
public class WoodContribution
{
    public int Player;
    public string Soul = "";
    // 数组按新 WoodKind 枚举（8 种）索引：0森息 1霜寂 2盐蚀 3瘴毒 4蚀枯 5血馥 6烬燃 7净辉。
    public int[] Time = new int[8];
    public float[] Power = new float[8];
    public ulong[] Next = new ulong[8];
    public float Poison, Fire, Growth;
    public List<WoodTransfer> Transfers = [];
    public bool Alive => Time.Any(t => t > 0) || Transfers.Any(t => t.Frames > 0);
    public float Bonus => 1 + Growth + (Time[4] > 0 ? .02f * Power[4] : 0);
    public float Rate => ((Time[3] > 0 ? Poison : 0) + (Time[6] > 0 ? Fire : 0)) * Bonus +
        Transfers.Where(t => t.Frames > 0).Sum(t => t.Rate);
}
public class WoodTransfer { public float Rate; public int Frames; public int Family; }

// 木质族 V2 · 8 特性实现点对照表（S=wood.Strength，环境倍率，1.10/1.20/1.35/1.50/2.00，归冠 3.00；
// P=主剑影伤害快照，取命中瞬间 projectile.damage 且下限 1；全部特性只吃 S，不吃接力倍率 TraitPower）。
// W01 森息 6 秒：WoodPlayer.GrantHitBuffs 授予 ForestBreath 360 帧（60 帧刷新门槛，只刷新不叠层）；
//     WoodPlayer.UpdateLifeRegen 每秒 0.25×S，走 lifeRegen 自然回复通道。
// W02 霜寂 4 秒：Apply(index 1) 写 Time[1]/Power[1] 并挂 FrostStill；ModifyPrimaryHit 本剑魂主攻 +1%×S；
//     PostAI 普通敌人减速 4%×S（仅该 (玩家,剑魂) 且剑魂当前手持；Boss 不减速，增伤仍生效）。
// W03 盐蚀 4 秒：Apply(index 2)；ModifyPrimaryHit 来源穿甲 +2×S（ArmorPenetration，不改敌人全局防御）。
// W04 瘴毒 4 秒：Apply(index 3) 每秒 (1+0.02P)×S；独立快照；root/target 中毒免疫双重检查；自定义 DoT 通道不与原版重复。
// W05 蚀枯 4 秒：Apply(index 4)；WoodContribution.Bonus 只放大本贡献自有 Poison/Fire 通道 ×(1+2%×S+森林成长)，
//     Transfers（他人转移预算）在 Bonus 之外累加，不被放大。
// W06 血馥 6 秒：WoodPlayer.GrantHitBuffs 授予 BloodFragrance 360 帧（60 帧刷新门槛）；
//     WoodPlayer.BoostSoulHealing 直接治疗 +4%×S，S 与战斗侧同源取 soul.CombatWood.Strength。
// W07 烬燃 4 秒：Apply(index 6) 每秒 (1+0.025P)×S；root/target 着火免疫双重检查。
// W08 净辉 4 秒：Apply(index 7)；ModifyPrimaryHit 主攻 FinalDamage +1.5%×S；只在 SoulBlade.ModifyHitNPC
//     的主剑影直伤结算，DoT（lifeRegen）与衍生直伤弹幕不经过本入口，天然不放大。
public class WoodCombat : GlobalNPC
{
    public override bool InstancePerEntity => true;
    public readonly Dictionary<(int Player, string Soul), WoodContribution> Contributions = [];
    private double regenUnits;
    public static NPC Root(NPC npc) => npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].active ? Main.npc[npc.realLife] : npc;
    public static SoulItem FindSoul(Player player, string id) => player.inventory.Select(i => i.ModItem).OfType<SoulItem>().FirstOrDefault(s => s.SoulId == id && s.Attached);
    public static bool Eligible(NPC npc) => npc.type != NPCID.TargetDummy && !npc.friendly && !npc.dontTakeDamage && !npc.immortal;
    public override void OnSpawn(NPC npc, IEntitySource source) { Contributions.Clear(); regenUnits = 0; }
    public WoodContribution Entry(int player, string soul)
    {
        if (!Contributions.TryGetValue((player, soul), out var state))
            Contributions[(player, soul)] = state = new() { Player = player, Soul = soul };
        return state;
    }
    public static void PreparePrimaryHit(Projectile projectile, NPC target)
    {
        if (projectile.ModProjectile is not SoulBlade || !Eligible(target)) return;
        if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;
        Player player = Main.player[projectile.owner];
        string id = projectile.GetGlobalProjectile<SoulSource>().SoulId;
        SoulItem soul = FindSoul(player, id);
        if (soul == null || player.HeldItem.ModItem is not SoulItem held || held.SoulId != id) return;
        Apply(target, player, soul, projectile.damage);
        if (Main.netMode == NetmodeID.MultiplayerClient && projectile.owner == Main.myPlayer)
            SendHit(projectile, target, false);
    }
    public static void FinishPrimaryHit(Projectile projectile, NPC target, int damageDone)
    {
        if (damageDone <= 0 || !Eligible(target) || projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;
        Player player = Main.player[projectile.owner];
        SoulItem soul = FindSoul(player, projectile.GetGlobalProjectile<SoulSource>().SoulId);
        if (soul == null) return;
        player.GetModPlayer<WoodPlayer>().RecordHit(soul);
        player.GetModPlayer<WoodPlayer>().GrantHitBuffs(soul);
        if (Main.netMode == NetmodeID.MultiplayerClient && projectile.owner == Main.myPlayer) SendHit(projectile, target, true);
    }
    private static void SendHit(Projectile projectile, NPC target, bool finished)
    {
        var packet = ModContent.GetInstance<ZenithReborn>().GetPacket();
        packet.Write((byte)3); packet.Write(projectile.identity); packet.Write((short)target.whoAmI);
        packet.Write(target.netID); packet.Write(finished);
        // 末位浮点为历史帧字段（原接力倍率 TraitPower）：保留原值写入以维持网络帧布局，
        // 接收侧只做有限性校验、不参与任何结算；特性数值一律只取 S。
        packet.Write(projectile.ModProjectile is SoulBlade blade ? blade.Blessings.TraitPower : 1f); packet.Send();
    }
    public static void ReceiveHit(BinaryReader reader, int who)
    {
        int identity = reader.ReadInt32(), index = reader.ReadInt16(), netId = reader.ReadInt32();
        bool finished = reader.ReadBoolean();
        float relayPower=reader.ReadSingle(); // 历史帧字段，仅消费不结算，见 SendHit 注释。
        if(!float.IsFinite(relayPower))return;
        if (Main.netMode != NetmodeID.Server || who < 0 || who >= Main.maxPlayers || index < 0 || index >= Main.maxNPCs) return;
        Player player = Main.player[who]; NPC npc = Main.npc[index];
        Projectile blade = Main.projectile.FirstOrDefault(p => p.active && p.owner == who && p.identity == identity && p.ModProjectile is SoulBlade);
        if (!player.active || player.dead || blade == null || npc.netID != netId || !Eligible(npc)) return;
        SoulItem soul = FindSoul(player, blade.GetGlobalProjectile<SoulSource>().SoulId);
        if (soul == null || player.HeldItem.ModItem is not SoulItem held || held.SoulId != soul.SoulId) return;
        if (!finished && npc.active) Apply(npc, player, soul, blade.damage);
        else if (finished)
        {
            player.GetModPlayer<WoodPlayer>().RecordHit(soul);
            player.GetModPlayer<WoodPlayer>().GrantHitBuffs(soul);
        }
    }
    public static void Apply(NPC target, Player player, SoulItem soul, int primaryDamage)
    {
        if (!Eligible(target)) return;
        NPC root = Root(target);
        var wood = soul.CombatWood;
        if (wood.Count == 0) return;
        WoodCombat combat = root.GetGlobalNPC<WoodCombat>();
        WoodContribution state = combat.Entry(player.whoAmI, soul.SoulId);
        if (state.Time[3] <= 0 && state.Time[6] <= 0) state.Growth = 0;
        // 特性强度只取 S（wood.Strength）。原实现额外乘了 TraitPower 接力倍率（1~1.5），
        // 那是「切换」诅咒下的祝福接力增伤，属祝福伤害通道（SoulBlessings/HardOrePlayer 侧结算），
        // 不应放大木质特性，故在此处断开；接力机制本身未改动。
        // 哪些剑的特性开火看 CombatWood（受切换诅咒压制）；特性强度 S 是族倍率，属数值口径，取 HostedWood。
        float strength = soul.HostedWood.Strength, p = Math.Max(1, primaryDamage);
        bool changed = false;
        // 标记先于持续伤害通道施加，保证本跳命中时所有通道看到同一增幅。
        int[] order = [1, 2, 4, 7, 3, 6];
        foreach (int i in order)
        {
            if (!wood.Has((WoodKind)i) || Main.GameUpdateCount < state.Next[i]) continue;
            if (i == 3 && (root.buffImmune[BuffID.Poisoned] || target.buffImmune[BuffID.Poisoned])) continue;
            if (i == 6 && (root.buffImmune[BuffID.OnFire] || target.buffImmune[BuffID.OnFire])) continue;
            int buff = BuffFor(i);
            if (buff > 0 && (root.buffImmune[buff] || target.buffImmune[buff])) continue;
            int frames = FramesFor(i);
            bool expired = state.Time[i] <= 0;
            state.Time[i] = frames;
            state.Power[i] = expired ? strength : Math.Max(state.Power[i], strength);
            state.Next[i] = Main.GameUpdateCount + 30ul;
            if (i == 3) state.Poison = expired ? (1 + .02f * p) * strength : Math.Max(state.Poison, (1 + .02f * p) * strength);
            if (i == 6) state.Fire = expired ? (1 + .025f * p) * strength : Math.Max(state.Fire, (1 + .025f * p) * strength);
            if (i is 3 or 6) state.Growth = Math.Max(state.Growth, ForestPower.DotBonusPercent(player));
            if (buff > 0) root.AddBuff(buff, frames);
            changed = true;
        }
        if (changed) root.netUpdate = true;
    }
    // 特性持续帧数：森息（index 0）与血馥（index 5）为 6 秒＝360 帧，其余 6 条为 4 秒＝240 帧。
    // 森息/血馥的可见时长由 WoodPlayer 的 ForestBreath/BloodFragrance buff 承载，此处仅保持状态表口径一致。
    private static int FramesFor(int kind) => kind is 0 or 5 ? 360 : 240;
    public static int BuffFor(int kind) => kind switch {
        1 => ModContent.BuffType<FrostStill>(), 2 => ModContent.BuffType<SaltCorrosion>(),
        3 => ModContent.BuffType<JungleToxin>(), 4 => ModContent.BuffType<WoodWither>(),
        6 => ModContent.BuffType<EmberBurn>(), 7 => ModContent.BuffType<HallowRadiance>(), _ => 0
    };
    public static void ModifyPrimaryHit(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
    {
        if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;
        var combat = Root(target).GetGlobalNPC<WoodCombat>();
        string id = projectile.GetGlobalProjectile<SoulSource>().SoulId;
        if (!combat.Contributions.TryGetValue((projectile.owner, id), out var state)) return;
        SoulItem soul = FindSoul(Main.player[projectile.owner], id);
        if (soul == null) return;
        WoodLoadout wood = soul.CombatWood;
        float bonus = 0;
        if (state.Time[1] > 0 && wood.Has(WoodKind.Boreal)) bonus += .01f * state.Power[1];
        if (state.Time[7] > 0 && wood.Has(WoodKind.Pearl)) bonus += .015f * state.Power[7];
        modifiers.FinalDamage *= 1 + bonus;
        if (state.Time[2] > 0 && wood.Has(WoodKind.Palm)) modifiers.ArmorPenetration += 2 * state.Power[2];
    }
    // ==== V2 复合羁绊最小查询接口（只加不改：以下均为新增方法，不触碰上方任何既有逻辑） ====
    // 查询统一按“目标根NPC 贡献表 + (玩家, 剑魂)”粒度读取，与 Apply/ModifyPrimaryHit 同一存储，不重构。

    // 盐蚀存在性：本剑魂在目标根NPC 上的盐蚀通道（index 2）仍在持续（Time>0）。
    public static bool HasSalt(NPC target, int player, string soulId) =>
        Root(target).GetGlobalNPC<WoodCombat>().Contributions.TryGetValue((player, soulId), out var salt) && salt.Time[2] > 0;

    // 瘴毒存在性：本剑魂的瘴毒通道（index 3）仍在持续。
    public static bool HasToxin(NPC target, int player, string soulId) =>
        Root(target).GetGlobalNPC<WoodCombat>().Contributions.TryGetValue((player, soulId), out var toxin) && toxin.Time[3] > 0;

    // 瘴毒＋烬燃同时存在：index 3 与 index 6 均在持续（灰烬繁生触发条件）。
    public static bool HasToxinAndEmber(NPC target, int player, string soulId) =>
        Root(target).GetGlobalNPC<WoodCombat>().Contributions.TryGetValue((player, soulId), out var both) && both.Time[3] > 0 && both.Time[6] > 0;

    // 本剑魂自有 DoT 剩余预算：瘴毒通道 Poison×Time[3] + 烬燃通道 Fire×Time[6]（速率×剩余帧＝未结算总量）。
    // 只算自有通道，不含 Transfers 转移来的他人预算；通道被免疫清零（UpdateLifeRegen）后预算天然为 0，无法绕过免疫。
    public static float OwnDotBudget(NPC target, int player, string soulId)
    {
        if (!Root(target).GetGlobalNPC<WoodCombat>().Contributions.TryGetValue((player, soulId), out var state)) return 0;
        float budget = 0;
        if (state.Time[3] > 0) budget += state.Poison * state.Time[3];
        if (state.Time[6] > 0) budget += state.Fire * state.Time[6];
        return budget;
    }

    // 凋蚀释放结算扣除：从瘴毒/烬燃两通道按各自预算占比等比扣除 amount 点未结算伤害。
    // 折算算法：预算＝通道速率(Poison/Fire)×剩余帧(Time)，保持 Time（持续表现/到期时间）不变，
    // 仅把通道速率缩为 (预算-分摊)/剩余帧——即从未来每帧 DoT 中匀速收回已提前结算的等量伤害；
    // 扣后速率单调下降，与 Apply 的 Math.Max 再施加语义互不干扰（只会被更强的重新施加覆盖）。
    // 与 Apply 一致置 root.netUpdate，让既有 SendExtraAI 把扣减后的贡献表同步到客户端。
    public static void ConsumeOwnDot(NPC target, int player, string soulId, float amount)
    {
        NPC root = Root(target);
        if (!root.GetGlobalNPC<WoodCombat>().Contributions.TryGetValue((player, soulId), out var state)) return;
        float budget = 0;
        if (state.Time[3] > 0) budget += state.Poison * state.Time[3];
        if (state.Time[6] > 0) budget += state.Fire * state.Time[6];
        if (budget <= 0 || amount <= 0) return;
        float scale = 1 - Math.Min(amount, budget) / budget; // 等比缩减系数，实扣量不超过剩余预算
        if (state.Time[3] > 0) state.Poison *= scale;
        if (state.Time[6] > 0) state.Fire *= scale;
        root.netUpdate = true;
    }
    public override void PostAI(NPC npc)
    {
        NPC root = Root(npc);
        var combat = root.GetGlobalNPC<WoodCombat>();
        if (!npc.boss && !root.boss && npc.knockBackResist > 0 && !npc.friendly)
        {
            // 只统计「本剑魂」的霜寂：贡献必须仍由其归属玩家手持同一剑魂（换魂/脱手后的残留不参与），
            // 不再把其他玩家、其他剑魂的贡献混算成一个全局 Max。
            float slow = 0;
            foreach (var s in combat.Contributions.Values)
                if (s.Time[1] > 0 && SoulStillHeld(s.Player, s.Soul)) slow = Math.Max(slow, .04f * s.Power[1]);
            // Offset movement once; don't multiply stored velocity every frame into a permanent stun.
            npc.position -= npc.velocity * Math.Clamp(slow, 0, .12f);
        }
    }
    // 贡献是否仍属「当前持有的本剑魂」：玩家存活且手持同 id 的已寄宿剑魂。
    private static bool SoulStillHeld(int player, string soulId) =>
        player >= 0 && player < Main.maxPlayers && Main.player[player].active && !Main.player[player].dead &&
        Main.player[player].HeldItem.ModItem is SoulItem held && held.Attached && held.SoulId == soulId;
    public override void UpdateLifeRegen(NPC npc, ref int damage)
    {
        if (Root(npc) != npc || Contributions.Count == 0) return;
        double dps = 0;
        foreach (var pair in Contributions.ToArray())
        {
            var state = pair.Value;
            if (state.Player < 0 || state.Player >= Main.maxPlayers || !Main.player[state.Player].active) { Contributions.Remove(pair.Key); continue; }
            if (npc.buffImmune[BuffID.Poisoned]) { state.Time[3] = 0; state.Transfers.RemoveAll(t => t.Family == BuffID.Poisoned); }
            if (npc.buffImmune[BuffID.OnFire]) { state.Time[6] = 0; state.Transfers.RemoveAll(t => t.Family == BuffID.OnFire); }
            dps += state.Rate;
            int boss = SoulCurses.BossSeal(npc.type);
            if (state.Rate > 0 && boss != 0 && Eligible(npc) && Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (!SoulEncounters.Hits.TryGetValue(boss, out var participants)) SoulEncounters.Hits[boss] = participants = [];
                participants.Add((state.Player, state.Soul));
            }
            for (int i = 0; i < 8; i++) if (state.Time[i] > 0) state.Time[i]--;
            foreach (var transfer in state.Transfers) transfer.Frames--;
            state.Transfers.RemoveAll(t => t.Frames <= 0);
            // Retain recent cooldowns, so removal/reinsertion cannot reset application cadence.
            if (!state.Alive && state.Next.All(n => n <= Main.GameUpdateCount)) Contributions.Remove(pair.Key);
        }
        if (dps <= 0 || !Eligible(npc)) return;
        regenUnits += dps * 2;
        int units = (int)Math.Min(int.MaxValue / 4, Math.Floor(regenUnits));
        regenUnits -= units;
        npc.lifeRegen -= units;
        damage = Math.Max(damage, Math.Max(1, (int)(dps / 2)));
    }
    public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter writer)
    {
        writer.Write((ushort)Contributions.Count);
        foreach (var s in Contributions.Values)
        {
            writer.Write((byte)s.Player); writer.Write(s.Soul);
            for (int i = 0; i < 8; i++) { writer.Write((ushort)Math.Clamp(s.Time[i], 0, 480)); writer.Write(s.Power[i]); }
            writer.Write(s.Poison); writer.Write(s.Fire); writer.Write(s.Growth);
            writer.Write((ushort)s.Transfers.Count);
            foreach (var t in s.Transfers) { writer.Write(t.Rate); writer.Write((ushort)t.Frames); writer.Write(t.Family); }
        }
    }
    public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader reader)
    {
        Contributions.Clear();
        int count = reader.ReadUInt16();
        for (int n = 0; n < count; n++)
        {
            var s = new WoodContribution { Player = reader.ReadByte(), Soul = reader.ReadString() };
            for (int i = 0; i < 8; i++) { s.Time[i] = reader.ReadUInt16(); s.Power[i] = reader.ReadSingle(); }
            s.Poison = reader.ReadSingle(); s.Fire = reader.ReadSingle(); s.Growth = reader.ReadSingle();
            int transfers = reader.ReadUInt16();
            for (int i = 0; i < transfers; i++) s.Transfers.Add(new() { Rate = reader.ReadSingle(), Frames = reader.ReadUInt16(), Family = reader.ReadInt32() });
            Contributions[(s.Player, s.Soul)] = s;
        }
    }
}

public abstract class WoodDebuff : ModBuff
{
    public override string Texture => "Terraria/Images/Buff_" + BuffID.DryadsWard;
    public override void SetStaticDefaults() { Main.debuff[Type] = true; Main.buffNoSave[Type] = true; }
}
public class FrostStill : WoodDebuff { }
public class SaltCorrosion : WoodDebuff { }
public class JungleToxin : WoodDebuff { }
public class WoodWither : WoodDebuff { }
public class EmberBurn : WoodDebuff { }
public class HallowRadiance : WoodDebuff { }
