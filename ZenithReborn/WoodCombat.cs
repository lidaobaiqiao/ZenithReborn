using System.IO;
using Microsoft.Xna.Framework;
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
    public int[] Time = new int[12];
    public float[] Power = new float[12];
    public ulong[] Next = new ulong[12];
    public float[] Bleed = new float[3];
    public int BleedLayers;
    public float Poison, Fire, Growth;
    public List<WoodTransfer> Transfers = [];
    public bool Alive => Time.Any(t => t > 0) || Transfers.Any(t => t.Frames > 0);
    public float Bonus => 1 + Growth + (Time[5] > 0 ? .02f * Power[5] : 0) + (Time[10] > 0 ? .10f * Power[10] : 0);
    public float Rate => ((Time[3] > 0 ? Poison : 0) + (Time[7] > 0 ? Fire : 0) +
        (Time[4] > 0 ? Bleed.Take(BleedLayers).Sum() : 0)) * Bonus + Transfers.Where(t => t.Frames > 0).Sum(t => t.Rate);
    // Compute the remaining payable original budget with each mark's actual expiry.
    public float RemainingBudget()
    {
        double total = 0;
        int end = Math.Max(Time[3], Math.Max(Time[4], Time[7]));
        for (int t = 0; t < end; t++)
        {
            float raw = (t < Time[3] ? Poison : 0) + (t < Time[7] ? Fire : 0) +
                (t < Time[4] ? Bleed.Take(BleedLayers).Sum() : 0);
            total += raw * (1 + Growth + (t < Time[5] ? .02f * Power[5] : 0) + (t < Time[10] ? .10f * Power[10] : 0)) / 60;
        }
        return (float)total;
    }
}
public class WoodTransfer { public float Rate; public int Frames; public int Family; }

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
        packet.Write(target.netID); packet.Write(finished); packet.Send();
    }
    public static void ReceiveHit(BinaryReader reader, int who)
    {
        int identity = reader.ReadInt32(), index = reader.ReadInt16(), netId = reader.ReadInt32();
        bool finished = reader.ReadBoolean();
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
        var wood = soul.Wood;
        if (wood.Count == 0) return;
        WoodCombat combat = root.GetGlobalNPC<WoodCombat>();
        WoodContribution state = combat.Entry(player.whoAmI, soul.SoulId);
        if (state.Time[3] <= 0 && state.Time[4] <= 0 && state.Time[7] <= 0) state.Growth = 0;
        float strength = wood.Strength, p = Math.Max(1, primaryDamage);
        bool changed = false;
        // Marks first, so all DoT channels see the same enhancement on this hit.
        int[] order = [1, 2, 5, 8, 10, 3, 4, 7];
        foreach (int i in order)
        {
            if (!wood.Has((WoodKind)i) || Main.GameUpdateCount < state.Next[i]) continue;
            if (i == 3 && (root.buffImmune[BuffID.Poisoned] || target.buffImmune[BuffID.Poisoned])) continue;
            if (i == 7 && (root.buffImmune[BuffID.OnFire] || target.buffImmune[BuffID.OnFire])) continue;
            int buff = BuffFor(i);
            if (buff > 0 && (root.buffImmune[buff] || target.buffImmune[buff])) continue;
            int frames = i == 4 ? 300 : i == 10 ? 360 : 240;
            if (i is 3 or 4 or 7 && wood.Has(WoodKind.Fey)) frames = Math.Min(480, frames + (int)(30 * strength));
            bool expired = state.Time[i] <= 0;
            state.Time[i] = frames;
            state.Power[i] = expired ? strength : Math.Max(state.Power[i], strength);
            state.Next[i] = Main.GameUpdateCount + (ulong)(i == 4 ? 60 : 30);
            if (i == 3) state.Poison = expired ? (1 + .02f * p) * strength : Math.Max(state.Poison, (1 + .02f * p) * strength);
            if (i == 7) state.Fire = expired ? (1 + .025f * p) * strength : Math.Max(state.Fire, (1 + .025f * p) * strength);
            if (i == 4)
            {
                if (expired) { state.BleedLayers = 0; Array.Clear(state.Bleed); }
                int layer = state.BleedLayers < 3 ? state.BleedLayers++ : Array.IndexOf(state.Bleed, state.Bleed.Min());
                state.Bleed[layer] = Math.Max(state.Bleed[layer], (.5f + .015f * p) * strength);
            }
            if (i is 3 or 4 or 7) state.Growth = Math.Max(state.Growth, player.GetModPlayer<WoodPlayer>().LayersFor(soul) * .05f);
            if (buff > 0) root.AddBuff(buff, frames);
            changed = true;
        }
        if (changed) root.netUpdate = true;
    }
    public static int BuffFor(int kind) => kind switch {
        1 => ModContent.BuffType<FrostStill>(), 2 => ModContent.BuffType<SaltCorrosion>(),
        3 => ModContent.BuffType<JungleToxin>(), 4 => ModContent.BuffType<CactusBleed>(),
        5 => ModContent.BuffType<WoodWither>(), 7 => ModContent.BuffType<EmberBurn>(),
        8 => ModContent.BuffType<HallowRadiance>(), 10 => ModContent.BuffType<MourningMark>(), _ => 0
    };
    public static void ModifyPrimaryHit(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
    {
        if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;
        var combat = Root(target).GetGlobalNPC<WoodCombat>();
        string id = projectile.GetGlobalProjectile<SoulSource>().SoulId;
        if (!combat.Contributions.TryGetValue((projectile.owner, id), out var state)) return;
        SoulItem soul = FindSoul(Main.player[projectile.owner], id);
        if (soul == null) return;
        WoodLoadout wood = soul.Wood;
        float bonus = 0;
        if (state.Time[1] > 0 && wood.Has(WoodKind.Boreal)) bonus += .01f * state.Power[1];
        if (state.Time[8] > 0 && wood.Has(WoodKind.Pearl)) bonus += .015f * state.Power[8];
        modifiers.FinalDamage *= 1 + bonus;
        if (state.Time[2] > 0 && wood.Has(WoodKind.Palm)) modifiers.ArmorPenetration += 2 * state.Power[2];
    }
    public override void PostAI(NPC npc)
    {
        NPC root = Root(npc);
        var combat = root.GetGlobalNPC<WoodCombat>();
        if (!npc.boss && !root.boss && npc.knockBackResist > 0 && !npc.friendly)
        {
            float slow = combat.Contributions.Values.Where(s => s.Time[1] > 0).Select(s => .04f * s.Power[1]).DefaultIfEmpty(0).Max();
            // Offset movement once; don't multiply stored velocity every frame into a permanent stun.
            npc.position -= npc.velocity * Math.Clamp(slow, 0, .12f);
        }
    }
    public override void UpdateLifeRegen(NPC npc, ref int damage)
    {
        if (Root(npc) != npc || Contributions.Count == 0) return;
        double dps = 0;
        foreach (var pair in Contributions.ToArray())
        {
            var state = pair.Value;
            if (state.Player < 0 || state.Player >= Main.maxPlayers || !Main.player[state.Player].active) { Contributions.Remove(pair.Key); continue; }
            if (npc.buffImmune[BuffID.Poisoned]) { state.Time[3] = 0; state.Transfers.RemoveAll(t => t.Family == BuffID.Poisoned); }
            if (npc.buffImmune[BuffID.OnFire]) { state.Time[7] = 0; state.Transfers.RemoveAll(t => t.Family == BuffID.OnFire); }
            dps += state.Rate;
            int boss = SoulProgress.BossBit(npc.type);
            if (state.Rate > 0 && boss != 0 && Eligible(npc) && Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (!SoulEncounters.Hits.TryGetValue(boss, out var participants)) SoulEncounters.Hits[boss] = participants = [];
                participants.Add((state.Player, state.Soul));
            }
            for (int i = 0; i < 12; i++) if (state.Time[i] > 0) state.Time[i]--;
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
    public override void OnKill(NPC npc)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || Root(npc) != npc) return;
        foreach (var state in Contributions.Values)
        {
            if (state.Time[10] <= 0 || state.Player < 0 || state.Player >= Main.maxPlayers) continue;
            Player player = Main.player[state.Player];
            var soul = FindSoul(player, state.Soul);
            if (!player.active || soul == null || !soul.Wood.Has(WoodKind.Spooky)) continue;
            NPC[] targets = Main.npc.Where(n => n.active && n.whoAmI != npc.whoAmI && Root(n) == n && Eligible(n) &&
                Vector2.DistanceSquared(n.Center, npc.Center) <= 360 * 360 && Collision.CanHitLine(npc.position, npc.width, npc.height, n.position, n.width, n.height))
                .OrderBy(n => Vector2.DistanceSquared(n.Center, npc.Center)).Take(2).ToArray();
            if (targets.Length == 0 || state.RemainingBudget() <= 0 || !player.GetModPlayer<WoodPlayer>().Ready("mourn", 120)) continue;
            // Preserve per-channel remaining duration and immunity. Splitting never duplicates the budget.
            foreach (NPC next in targets)
            {
                var receiver = next.GetGlobalNPC<WoodCombat>().Entry(state.Player, state.Soul);
                AddTransfer(next, receiver, state, 3, state.Poison, BuffID.Poisoned, targets.Length);
                AddTransfer(next, receiver, state, 4, state.Bleed.Take(state.BleedLayers).Sum(), 0, targets.Length);
                AddTransfer(next, receiver, state, 7, state.Fire, BuffID.OnFire, targets.Length);
                next.netUpdate = true;
            }
        }
    }
    private static void AddTransfer(NPC target, WoodContribution receiver, WoodContribution source, int kind, float rate, int family, int count)
    {
        if (source.Time[kind] <= 0 || rate <= 0 || family > 0 && target.buffImmune[family] || target.buffImmune[BuffFor(kind)]) return;
        // Split at mark expiry; do not extend or re-amplify the transferred snapshot.
        int end = source.Time[kind], begin = 0;
        foreach (int stop in new[] { source.Time[5], source.Time[10], end }.Where(t => t > 0 && t <= end).Distinct().Order())
        {
            if (stop <= begin) continue;
            float bonus = 1 + source.Growth + (begin < source.Time[5] ? .02f * source.Power[5] : 0) + (begin < source.Time[10] ? .10f * source.Power[10] : 0);
            // A delayed segment's budget is spread over its original remaining window, preserving total damage and final expiry.
            receiver.Transfers.Add(new() { Rate = rate * bonus * (.25f * source.Power[10]) * (stop - begin) / stop / count, Frames = stop, Family = family });
            begin = stop;
        }
        target.AddBuff(BuffFor(kind), end);
    }
    public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter writer)
    {
        writer.Write((ushort)Contributions.Count);
        foreach (var s in Contributions.Values)
        {
            writer.Write((byte)s.Player); writer.Write(s.Soul);
            for (int i = 0; i < 12; i++) { writer.Write((ushort)Math.Clamp(s.Time[i], 0, 480)); writer.Write(s.Power[i]); }
            writer.Write((byte)s.BleedLayers); foreach (float rate in s.Bleed) writer.Write(rate);
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
            for (int i = 0; i < 12; i++) { s.Time[i] = reader.ReadUInt16(); s.Power[i] = reader.ReadSingle(); }
            s.BleedLayers = Math.Clamp((int)reader.ReadByte(), 0, 3);
            for (int i = 0; i < 3; i++) s.Bleed[i] = reader.ReadSingle();
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
public class CactusBleed : WoodDebuff { }
public class WoodWither : WoodDebuff { }
public class EmberBurn : WoodDebuff { }
public class HallowRadiance : WoodDebuff { }
public class MourningMark : WoodDebuff { }
