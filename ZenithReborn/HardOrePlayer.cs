using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public class HardOreTarget : GlobalNPC
{
    private static long serial;
    private long id;
    public override bool InstancePerEntity => true;
    public long Id => id == 0 ? id = System.Threading.Interlocked.Increment(ref serial) : id;
    public override void OnSpawn(NPC npc, Terraria.DataStructures.IEntitySource source) =>
        id = System.Threading.Interlocked.Increment(ref serial);
}

public class HardOrePlayer : ModPlayer
{
    private readonly record struct Channel(string Soul, HardOreKind Kind, int Source);
    private class Combo { public int Count; public long Last = -1000, Next; }
    private record Mark(Channel Channel, long Target, int Flight, long Until);
    private Dictionary<(Channel Channel, long Target), Combo> combos;
    private Dictionary<Channel, long> cooldowns;
    private List<Mark> marks;
    private Queue<(long Tick, int Amount)> heals;
    public long Clock { get; private set; }
    private bool Owner => Player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server;
    public override void Initialize() { combos = []; cooldowns = []; marks = []; heals = []; }
    private SoulItem Held => !Player.dead && Player.HeldItem.ModItem is SoulItem s && s.Attached ? s : null;
    private static long TargetId(NPC target) => WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id;
    public override void PostUpdate()
    {
        Clock++;
        var soul = Held;
        foreach (var key in combos.Keys.ToArray())
            if (Clock - combos[key].Last > 180 || soul == null || key.Channel.Soul != soul.SoulId ||
                !HardOreTypes.Enabled(soul, key.Channel.Source, key.Channel.Kind)) combos.Remove(key);
        marks.RemoveAll(m => Clock >= m.Until || soul == null || m.Channel.Soul != soul.SoulId ||
            !HardOreTypes.Enabled(soul, m.Channel.Source, m.Channel.Kind));
        foreach (var key in cooldowns.Keys.ToArray()) if (Clock >= cooldowns[key]) cooldowns.Remove(key);
        TrimHeals();
    }
    private void TrimHeals() { while (heals.Count > 0 && Clock - heals.Peek().Tick >= 360) heals.Dequeue(); }
    private void Clear() { combos.Clear(); cooldowns.Clear(); marks.Clear(); heals.Clear(); }
    public override void UpdateDead() { Clock++; Clear(); }
    public override void OnEnterWorld() { Clock = 0; Clear(); }
    public static bool ValidTarget(NPC target, bool requireAlive = true) => (!requireAlive || target.active) && WoodCombat.Eligible(target) &&
        WoodCombat.Root(target).type != NPCID.TargetDummy && WoodCombat.Root(target).lifeMax > 5;
    private SoulItem Source(Projectile p) => Owner && p.ModProjectile is SoulBlade &&
        Held is SoulItem s && s.SoulId == p.GetGlobalProjectile<SoulSource>().SoulId &&
        s.Active.Any(i => i.type == (int)p.ai[0]) ? s : null;

    public void Modify(Projectile p, NPC target, ref NPC.HitModifiers modifiers)
    {
        var soul = Source(p);
        if (soul == null || !ValidTarget(target)) return;
        int host = (int)p.ai[0];
        if (HardOreTypes.Traits(soul, host).Contains(HardOreKind.Adamantite) && target.defense >= 30)
            modifiers.SourceDamage += .12f;
        long root = TargetId(target);
        var mark = marks.FirstOrDefault(m => m.Channel.Soul == soul.SoulId && m.Target == root && Clock < m.Until &&
            HardOreTypes.Enabled(soul, m.Channel.Source, HardOreKind.Mythril) &&
            (host != m.Channel.Source || soul.Active.Count() == 1 && p.identity != m.Flight));
        if (mark == null) return;
        modifiers.SourceDamage += .12f;
        marks.Remove(mark);
    }

    public void Hit(Projectile p, NPC target, int damageDone)
    {
        var soul = Source(p);
        if (soul == null || damageDone <= 0 || !ValidTarget(target, false)) return;
        int host = (int)p.ai[0]; long root = TargetId(target);
        foreach (HardOreKind kind in HardOreTypes.Traits(soul, host))
        {
            if (kind == HardOreKind.Adamantite) continue;
            var channel = new Channel(soul.SoulId, kind, host);
            var key = (channel, root);
            if (!combos.TryGetValue(key, out var combo)) combos[key] = combo = new Combo();
            if (Clock < combo.Next) continue;
            if (Clock - combo.Last > 180) combo.Count = 0;
            combo.Last = Clock; combo.Next = Clock + 30; combo.Count++;
            int need = kind switch { HardOreKind.Cobalt => 3, HardOreKind.Palladium => 5,
                HardOreKind.Mythril or HardOreKind.Titanium => 4, _ => 1 };
            if (combo.Count < need) continue;
            if (cooldowns.TryGetValue(channel, out long until) && Clock < until) { combo.Count = 0; continue; }
            combo.Count = 0;
            if (kind == HardOreKind.Mythril)
            {
                marks.RemoveAll(m => m.Channel == channel && m.Target == root);
                marks.Add(new(channel, root, p.identity, Clock + 180));
            }
            else if (kind == HardOreKind.Palladium)
            { cooldowns[channel] = Clock + 180; Heal(soul); }
            else if (kind == HardOreKind.Titanium)
            {
                bool existing = Main.projectile.Any(q => q.active && q.owner == Player.whoAmI && q.ModProjectile is HardOreShot &&
                    (HardOreKind)(int)q.ai[1] == kind && q.GetGlobalProjectile<SoulSource>().SoulId == soul.SoulId &&
                    q.GetGlobalProjectile<SoulSource>().SourceWeapon == host);
                if (!existing) Spawn(p, target, kind);
            }
            else
            {
                cooldowns[channel] = Clock + (kind == HardOreKind.Cobalt ? 60 : 120);
                Spawn(p, target, kind);
            }
        }
    }
    private void Heal(SoulItem soul)
    {
        if (Player.moonLeech || Player.statLife >= Player.statLifeMax2) return;
        TrimHeals();
        int allowance = Math.Clamp((int)Math.Ceiling(Player.statLifeMax2 * .02), 1, 8) - heals.Sum(h => h.Amount);
        int amount = Math.Min(Math.Clamp((int)Math.Ceiling(Player.statLifeMax2 * .005), 1, 3), allowance);
        amount = Math.Min(amount, (int)MathF.Floor(Player.lifeSteal));
        if (amount <= 0) return;
        float budget = amount;
        int paid = Player.GetModPlayer<WoodPlayer>().HealFromSoul(soul, amount, ref budget);
        if (paid <= 0) return;
        Player.lifeSteal -= paid; heals.Enqueue((Clock, paid));
    }
    private void Spawn(Projectile parent, NPC target, HardOreKind kind)
    {
        int basis = ((SoulBlade)parent.ModProjectile).SpecialDamage;
        if (basis <= 0) basis = parent.damage;
        int budget = Math.Max(1, (int)(basis * (kind == HardOreKind.Cobalt ? .4f : .3f)));
        int count = kind == HardOreKind.Orichalcum ? 3 : 1;
        for (int i = 0; i < count; i++)
        {
            int damage = budget / count + (i < budget % count ? 1 : 0);
            Vector2 velocity = (target.Center - parent.Center).SafeNormalize(Vector2.UnitX * Player.direction)
                .RotatedBy((i - (count - 1) * .5f) * .65f) * 10;
            int index = Projectile.NewProjectile(parent.GetSource_FromThis(), parent.Center, velocity,
                ModContent.ProjectileType<HardOreShot>(), damage, parent.knockBack * .5f, Player.whoAmI,
                target.whoAmI, (int)kind, parent.identity);
            if (index >= Main.maxProjectiles) continue;
            var shot = Main.projectile[index];
            shot.friendly = damage > 0; shot.CritChance = parent.CritChance;
            if (kind == HardOreKind.Titanium) { shot.timeLeft = 30; shot.velocity = Vector2.Zero; }
            shot.netUpdate = true;
        }
    }
}
