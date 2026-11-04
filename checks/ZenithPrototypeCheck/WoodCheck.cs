using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 木质族 V2 夹具：0.3.129 起名册只剩 8 把原版木剑（WoodKind 0-7），
// 三把新增剑（王朝/阴森/仙灵）的 ModItem 已彻底删除，与之配套的裂伤/御令/悼魂/孢光
// 四套旧特性、BleedLayers、Time[10]、DynastyOrder 等类型也一并消失，本文件不再引用。
// 森林之力改为 6 层（基础）/12 层（圣林归冠）双成长表，全收集奖励槽改由矿石圆满提供。
internal static class WoodCheck
{
    private static SoulItem Soul(bool fullProgress = true)
    {
        var soul = (SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached = true;
        if (fullProgress) { soul.Kills = SoulProgress.All; soul.Cores = 63; }
        return soul;
    }
    // 8 木全套：6 把进普通主槽、2 把进支援槽，用来验证「基础普通槽＋有效支援槽」的计数口径。
    private static void FullWood(SoulItem soul)
    {
        for (int i = 0; i < 6; i++) soul.Hosts[i] = new Item(WoodTypes.Types[i]);
        for (int i = 0; i < 2; i++) soul.SupportHosts[i] = new Item(WoodTypes.Types[i + 6]);
        soul.RefreshStats();
    }
    // 阶段表：1/2/4/6/8 把 → 强度与生命加成。
    private static (float Strength, int Life) Stage(int count) =>
        count >= 8 ? (2f, 40) : count >= 6 ? (1.5f, 25) : count >= 4 ? (1.35f, 15) :
        count >= 2 ? (1.2f, 10) : count >= 1 ? (1.1f, 5) : (0f, 0);
    internal static void Run(Action<bool, string> check)
    {
        Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer;
        Player player = new() { whoAmI = 0, active = true };
        Main.player[0] = player; player.ResetEffects();
        var soul = Soul(); player.inventory[0] = soul.Item; player.selectedItem = 0;

        // —— 名册：8 把原版木剑，顺序即 WoodKind ——
        int[] expected = [ItemID.WoodenSword, ItemID.BorealWoodSword, ItemID.PalmWoodSword, ItemID.RichMahoganySword,
            ItemID.EbonwoodSword, ItemID.ShadewoodSword, ItemID.AshWoodSword, ItemID.PearlwoodSword];
        check(WoodTypes.Types.Length == 8 && WoodTypes.Types.Distinct().Count() == 8 && WoodTypes.Types.SequenceEqual(expected),
            "Eight unique vanilla wood swords in WoodKind order");
        check((int)WoodKind.Pearl == 7 && Enum.GetValues<WoodKind>().Length == 8 && WoodTypes.Names.Length == 8,
            "WoodKind enum keeps eight contiguous kinds with localized names");
        foreach (int type in WoodTypes.Types)
            check(WoodTypes.IsWood(type) && SoulItem.CanHost(new Item(type)) && WoodTypes.CanSupport(new Item(type)),
                "Each wood supports both slot types: " + type);

        // —— 阶段表：静态构造与实况求值必须一致 ——
        foreach (var stage in new[] { (1, 1), (2, 2), (4, 3), (6, 4), (8, 5) })
        {
            int mask = (1 << stage.Item1) - 1;
            var load = new WoodLoadout(mask, stage.Item2);
            var (strength, life) = Stage(stage.Item1);
            check(load.Count == stage.Item1 && load.SupportCount == stage.Item2 &&
                Math.Abs(load.Strength - strength) < .001 && load.Life == life,
                "Threshold values: " + stage.Item1);
        }
        check(new WoodLoadout(0xFF, 5).Full && !new WoodLoadout(0x7F, 5).Full, "Only mask 0xFF counts as a full set");
        check(new WoodLoadout(0, 0).Strength == 0 && new WoodLoadout(0, 0).Life == 0, "Empty loadout grants nothing");
        check(Math.Abs(new WoodLoadout(0xFF, 5, true).Strength - 3f) < .001 && new WoodLoadout(0xFF, 5, true).Life == 100,
            "Crown loadout overrides to 3.00 strength and +100 life");

        var stageSoul = Soul();
        for (int i = 0; i < 8; i++)
        {
            stageSoul.Hosts[i] = new Item(WoodTypes.Types[i]);
            stageSoul.RefreshStats();
            var load = stageSoul.Wood;
            var (strength, life) = Stage(i + 1);
            check(load.Count == i + 1 && Math.Abs(load.Strength - strength) < .001 && load.Life == life &&
                stageSoul.SupportCapacity == Math.Min(5, new[] { 1, 2, 4, 6, 8 }.Count(n => i + 1 >= n)),
                "Live stage evaluation at " + (i + 1) + " wood swords");
        }

        // —— 槽位引擎：支援/奖励槽不得自举 ——
        var bootstrap = Soul();
        for (int i = 0; i < 5; i++) bootstrap.SupportHosts[i] = new Item(WoodTypes.Types[i]);
        for (int i = 0; i < 2; i++) bootstrap.ForestHosts[i] = new Item(WoodTypes.Types[i + 5]);
        check(bootstrap.Wood.Count == 0 && bootstrap.SupportCapacity == 0 && bootstrap.BonusCapacity == 0,
            "Support and reward slots cannot bootstrap themselves");
        FullWood(soul);
        check(soul.Wood.Full && soul.SupportCapacity == 5 && soul.BonusCapacity == 0 && soul.Active.Count() == 6 && soul.Supports.Count() == 2,
            "Full wood gives five support slots and no bonus slots without ore mastery");
        Item cursor = new(ItemID.GoldBroadsword); cursor.Prefix(PrefixID.Legendary);
        check(soul.TrySwapHost(14, ref cursor, out _) && cursor.IsAir && soul.SupportHosts[4].prefix == PrefixID.Legendary,
            "Real support slot accepts and preserves an actual weapon");
        cursor = new(ItemID.GoldBroadsword);
        check(!soul.TrySwapHost(6, ref cursor, out _) && !cursor.IsAir, "Duplicate rejected across ordinary and support arrays");
        cursor = new();
        check(soul.TrySwapHost(14, ref cursor, out _) && cursor.type == ItemID.GoldBroadsword && cursor.prefix == PrefixID.Legendary,
            "Support item can be retrieved intact");
        var clone = (SoulItem)soul.Item.Clone().ModItem;
        clone.SupportHosts[0].TurnToAir(); clone.CrownHosts[0] = new Item(ItemID.StarWrath);
        check(!soul.SupportHosts[0].IsAir && soul.CrownHosts[0].IsAir, "Support and crown arrays deep-cloned");
        var tag = new TagCompound(); soul.SaveData(tag);
        var loaded = Soul(false); loaded.LoadData(tag);
        check(loaded.Stored.Select(i => (i.type, i.prefix)).SequenceEqual(soul.Stored.Select(i => (i.type, i.prefix))) && loaded.SoulId == soul.SoulId,
            "All slots save and load intact");
        using (var bytes = new MemoryStream())
        {
            soul.NetSend(new BinaryWriter(bytes)); bytes.Position = 0; loaded.NetReceive(new BinaryReader(bytes));
            check(bytes.Position == bytes.Length &&
                loaded.Stored.Select(i => (i.type, i.prefix)).SequenceEqual(soul.Stored.Select(i => (i.type, i.prefix))),
                "All slots network round-trip");
        }
        var legacy = new TagCompound(); soul.SaveData(legacy);
        legacy.Remove("supports"); legacy.Remove("forestHosts"); legacy.Remove("crownHosts"); legacy.Remove("awakenedMemory");
        loaded.LoadData(legacy);
        check(loaded.SupportHosts.All(i => i.IsAir) && loaded.ForestHosts.All(i => i.IsAir) && loaded.CrownHosts.All(i => i.IsAir) &&
            loaded.Hosts[0].type == soul.Hosts[0].type, "Missing V2 arrays migrate to empty without touching ordinary slots");
        soul.PreReforge(); soul.Item.ResetPrefix(); soul.PostReforge();
        check(soul.Wood.Full && soul.SupportHosts[0].type == WoodTypes.Types[6] && soul.CrownHosts.All(i => i.IsAir),
            "Goblin reforge preserves support and crown arrays");

        // —— 森林之力：基础 6 层 / 归冠 12 层，脱战衰减与清零 ——
        var woodPlayer = player.GetModPlayer<WoodPlayer>();
        woodPlayer.GrantHitBuffs(soul);
        check(player.HasBuff(ModContent.BuffType<ForestBreath>()) && player.HasBuff(ModContent.BuffType<BloodFragrance>()),
            "Forest and shade primary-hit buffs still apply");
        // 0 层时只有森息的 0.25×2.00＝0.5/秒，四帧累计 4 个单位。
        player.lifeRegen = 0;
        for (int i = 0; i < 4; i++) woodPlayer.UpdateLifeRegen();
        check(player.lifeRegen == 4, "Fractional regen accumulates without a per-frame minimum");
        woodPlayer.RecordHit(soul);
        for (int frame = 0; frame < 1800; frame++) { woodPlayer.RecordHit(soul); woodPlayer.AdvanceGrowth(); }
        check(woodPlayer.Layers == 6 && woodPlayer.LayersFor(soul) == 6 && ForestPower.Layers(player) == 6,
            "30 seconds of valid combat reaches the six-layer base cap");
        for (int frame = 0; frame < 600; frame++) { woodPlayer.RecordHit(soul); woodPlayer.AdvanceGrowth(); }
        check(woodPlayer.Layers == 6, "Base cap cannot exceed six layers without the crown");
        check(Math.Abs(ForestPower.DamageBonus(player) - .12f) < .001 && Math.Abs(ForestPower.SpeedBonus(player) - .06f) < .001 &&
            ForestPower.DefenseBonus(player) == 12 && Math.Abs(ForestPower.RegenBonus(player) - 1.8f) < .001 &&
            Math.Abs(ForestPower.DotBonusPercent(player) - .18f) < .001 && Math.Abs(ForestPower.DamageReduction(player) - .03f) < .001,
            "Six-layer base growth table matches the documented values");
        player.statLifeMax2 = 100; player.statDefense = Player.DefenseStat.Default; player.endurance = 0;
        woodPlayer.PostUpdateEquips();
        check(player.statLifeMax2 == 140 && player.statDefense == 12 && Math.Abs(player.endurance - .03f) < .001,
            "Full wood life, defense and DR use actual player stats");
        for (int frame = 0; frame < 419; frame++) woodPlayer.AdvanceGrowth();
        check(woodPlayer.Layers == 5, "After grace expires, layers decay one per two seconds");
        Item removed = new();
        check(soul.TrySwapHost(0, ref removed, out _) && !soul.Wood.Full && soul.BonusCapacity == 0,
            "Removing any wood disables the full set");
        woodPlayer.AdvanceGrowth();
        check(woodPlayer.Layers == 0, "Loss of full set immediately resets forest growth");
        soul.Hosts[0] = removed; soul.RefreshStats();
        woodPlayer.RecordHit(soul);
        for (int frame = 0; frame < 300; frame++) woodPlayer.AdvanceGrowth();
        check(woodPlayer.Layers == 1, "Combat resumes growth after restoring the full set");
        var other = Soul(); FullWood(other); player.inventory[0] = other.Item;
        woodPlayer.RecordHit(other);
        check(woodPlayer.Layers == 0, "Switching to another soul resets growth");
        player.inventory[0] = soul.Item;
        woodPlayer.RecordHit(soul);
        for (int frame = 0; frame < 300; frame++) woodPlayer.AdvanceGrowth();
        player.dead = true; woodPlayer.UpdateDead(); player.dead = false;
        check(woodPlayer.Layers == 0, "Death clears forest growth");

        // —— 木质特性：8 条标记、DoT、免疫与减速 ——
        woodPlayer.ResetGrowth();
        NPC npc = new(); npc.SetDefaults(NPCID.Zombie); npc.active = true; npc.whoAmI = 20; Main.npc[20] = npc;
        WoodCombat.Apply(npc, player, soul, 100);
        var combat = npc.GetGlobalNPC<WoodCombat>();
        var contribution = combat.Entry(0, soul.SoulId);
        check(contribution.Time[1] == 240 && contribution.Time[2] == 240 && contribution.Time[3] == 240 &&
            contribution.Time[4] == 240 && contribution.Time[6] == 240 && contribution.Time[7] == 240,
            "All six timed wood marks use four-second durations");
        check(npc.HasBuff(ModContent.BuffType<FrostStill>()) && npc.HasBuff(ModContent.BuffType<SaltCorrosion>()) &&
            npc.HasBuff(ModContent.BuffType<JungleToxin>()) && npc.HasBuff(ModContent.BuffType<WoodWither>()) &&
            npc.HasBuff(ModContent.BuffType<EmberBurn>()) && npc.HasBuff(ModContent.BuffType<HallowRadiance>()),
            "Every wood mark applies its visible debuff");
        check(Math.Abs(contribution.Poison - 6f) < .001 && Math.Abs(contribution.Fire - 7f) < .001,
            "Wood DoT derives from main P and strength 2.00");
        check(Math.Abs(contribution.Rate - 13.52f) < .01, "Wither amplifies only own DoT channels by 2% per strength point");
        contribution.Time[3] = 5;
        WoodCombat.Apply(npc, player, soul, 100);
        check(contribution.Time[3] == 5, "Repeated primary hits share a per-target application cooldown");
        contribution.Time[3] = 240;
        int damage = 0; npc.lifeRegen = 0; combat.UpdateLifeRegen(npc, ref damage);
        check(npc.lifeRegen == -27 && damage == 6, "Owned wood DoT converts to lifeRegen at half rate");
        using (var bytes = new MemoryStream())
        {
            combat.SendExtraAI(npc, null, new BinaryWriter(bytes)); bytes.Position = 0;
            var remote = new NPC(); remote.SetDefaults(NPCID.Zombie);
            var remoteCombat = remote.GetGlobalNPC<WoodCombat>();
            remoteCombat.ReceiveExtraAI(remote, null, new BinaryReader(bytes));
            check(bytes.Position == bytes.Length && Math.Abs(remoteCombat.Contributions[(0, soul.SoulId)].Rate - contribution.Rate) < .001,
                "NPC wood state and remaining durations network round-trip");
        }
        var proj = new Projectile(); proj.SetDefaults(ModContent.ProjectileType<SoulBlade>()); proj.owner = 0; proj.damage = 100;
        proj.GetGlobalProjectile<SoulSource>().SoulId = soul.SoulId;
        var modifiers = npc.GetIncomingStrikeModifiers(DamageClass.Melee, 1);
        WoodCombat.ModifyPrimaryHit(proj, npc, ref modifiers);
        check(Math.Abs(modifiers.ArmorPenetration.Value - 4) < .001 && Math.Abs(modifiers.FinalDamage.ApplyTo(100) - 105) < .01,
            "Palm armor penetration and combined Boreal/Pearl vulnerability");
        NPC immune = new(); immune.SetDefaults(NPCID.Zombie); immune.active = true;
        immune.buffImmune[BuffID.Poisoned] = true; immune.buffImmune[BuffID.OnFire] = true;
        WoodCombat.Apply(immune, player, soul, 100);
        var immuneState = immune.GetGlobalNPC<WoodCombat>().Entry(0, soul.SoulId);
        check(immuneState.Time[3] == 0 && immuneState.Time[6] == 0 && immuneState.Time[4] > 0,
            "Poison and fire immunities respected independently of custom marks");
        NPC dummy = new(); dummy.SetDefaults(NPCID.TargetDummy); WoodCombat.Apply(dummy, player, soul, 100);
        check(dummy.GetGlobalNPC<WoodCombat>().Contributions.Count == 0, "Target dummy excluded from combat proc farm");
        var frozen = new NPC(); frozen.SetDefaults(NPCID.Zombie); frozen.active = true; frozen.velocity = new Vector2(10, 0);
        var frozenState = frozen.GetGlobalNPC<WoodCombat>().Entry(0, soul.SoulId);
        frozenState.Time[1] = 240; frozenState.Power[1] = 2;
        Vector2 before = frozen.position; frozen.GetGlobalNPC<WoodCombat>().PostAI(frozen);
        check(Math.Abs(frozen.position.X - before.X + .8f) < .001 && frozen.velocity.X == 10,
            "Boreal slows movement by 4% per strength point without velocity collapse");
        frozen.boss = true; before = frozen.position; frozen.GetGlobalNPC<WoodCombat>().PostAI(frozen);
        check(frozen.position == before, "Boss is not forcibly slowed");

        // —— 矿石圆满重开奖励槽；泰拉刃在非圣林槽激活归冠 ——
        check(soul.BonusCapacity == 0 && soul.CrownCapacity == 0, "Eight woods alone open no reward or crown slot");
        soul.OreCollectionMask = OreFamily.AllMaterials;
        check(soul.BonusCapacity == 2 && soul.CrownCapacity == 0, "Ore mastery reopens the two reward slots without the crown");
        Item terra = new(ItemID.TerraBlade); terra.Prefix(PrefixID.Legendary);
        check(soul.TrySwapHost(15, ref terra, out _) && terra.IsAir, "Ore-complete reward slot accepts a real weapon");
        check(soul.Wood.Strength == 3f && soul.Wood.Life == 100 && soul.CrownCapacity == 2,
            "Terra blade in a non-crown slot activates the crown override");
        woodPlayer.ResetGrowth();
        // 旧口径（0.3.129 之前夹具）：单次 RecordHit 后纯推进 3600 帧即可满 12 层。
        // 现口径：有效战斗窗口只有 300 帧（ForestPower.CombatGrace），成长按「有效战斗帧」累计，
        // 60 秒满 12 层必须逐帧重报命中，否则脱战衰减会先削层。
        for (int frame = 0; frame < 3600; frame++) { woodPlayer.RecordHit(soul); woodPlayer.AdvanceGrowth(); }
        check(woodPlayer.Layers == 12 && ForestPower.CrownActive(player) && Math.Abs(ForestPower.DamageBonus(player) - .36f) < .001,
            "Crown doubles the cap to twelve layers on the crown growth table");
        Item taken = new();
        check(soul.TrySwapHost(15, ref taken, out _) && taken.type == ItemID.TerraBlade && taken.prefix == PrefixID.Legendary,
            "Crown reward item can be retrieved intact");
        woodPlayer.AdvanceGrowth();
        check(woodPlayer.Layers == 6 && soul.CrownCapacity == 0, "Losing the crown clamps layers back to the six-layer cap");

        // —— UI 命中框与套装丢失后的 buff 撤销 ——
        Main.screenWidth = 1600; Main.screenHeight = 900; var panel = SoulUI.PanelBounds();
        var slots = Enumerable.Range(0, 17).Select(SoulUI.SlotBounds).ToArray();
        check(slots.All(r => panel.Contains(r)) && !slots.Where((r, i) => slots.Skip(i + 1).Any(r.Intersects)).Any(),
            "All seventeen UI slots fit without overlap");
        woodPlayer.PreUpdateBuffs();
        check(player.HasBuff(ModContent.BuffType<ForestPower>()), "Full set grants the forest power buff");
        soul.Hosts[0].TurnToAir(); soul.RefreshStats(); woodPlayer.PreUpdateBuffs();
        check(!player.HasBuff(ModContent.BuffType<ForestPower>()), "Missing wood source revokes the forest power buff");
    }
}
