using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 木质族 V2 专项断言：名册与阶段、四组复合羁绊、槽位编号、森林之力双成长表、
// 以及三把已删除新增剑确实不再注册。只读模组公开契约，不修改任何模组状态。
internal static class WoodV2Check
{
    private static SoulItem Soul()
    {
        var soul = (SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached = true; soul.Kills = SoulProgress.All; soul.Cores = 63;
        return soul;
    }
    // 按 WoodKind 顺序占用普通主槽；满进度下容量 10，足够放下 8 木＋2 把羁绊武器。
    private static void Host(SoulItem soul, params int[] kinds)
    {
        for (int i = 0; i < kinds.Length; i++) soul.Hosts[i] = new Item(WoodTypes.Types[kinds[i]]);
        soul.RefreshStats();
    }
    // WoodBondCore/WoodCrown 为模组内部类型；夹具改走公开的图鉴契约读取同一判定结果。
    private static bool Bond(SoulItem soul, string key) => SoulTraits.All(soul).First(t => t.Key == key).Active;
    internal static void Run(Action<bool, string> check)
    {
        int oldMode = Main.netMode, oldPlayer = Main.myPlayer;
        try
        {
            Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer;
            Player player = new() { whoAmI = 0, active = true };
            Main.player[0] = player; player.ResetEffects();

            // —— a 名册与阶段 ——
            check(WoodTypes.Types.Length == 8 && Enum.GetValues<WoodKind>().Length == 8 && WoodTypes.Names.Length == 8,
                "V2 roster is exactly eight vanilla wood swords");
            foreach (var stage in new[] { (1, 1, 1.10f, 5), (2, 2, 1.20f, 10), (4, 3, 1.35f, 15), (6, 4, 1.50f, 25), (8, 5, 2.00f, 40) })
            {
                var staged = Soul();
                Host(staged, Enumerable.Range(0, stage.Item1).ToArray());
                var load = staged.Wood;
                check(load.Count == stage.Item1 && load.SupportCount == stage.Item2 &&
                    Math.Abs(load.Strength - stage.Item3) < .001 && load.Life == stage.Item4,
                    "Live loadout stage " + stage.Item1 + " keeps strength, life and support count");
            }
            var full = Soul(); Host(full, 0, 1, 2, 3, 4, 5, 6, 7);
            check(full.Wood.Mask == 0xFF && full.Wood.Full && full.SupportCapacity == 5,
                "Eight woods give mask 0xFF, Full and five support slots");
            check(Math.Abs(new WoodLoadout(0xFF, 5, true).Strength - 3f) < .001 && new WoodLoadout(0xFF, 5, true).Life == 100 &&
                Math.Abs(new WoodLoadout(0xFF, 5).Strength - 2f) < .001 && new WoodLoadout(0xFF, 5).Life == 40,
                "Crown flag overrides 2.00/+40 with 3.00/+100");

            // —— b 四组复合羁绊 ——
            var forge = Soul(); Host(forge, 0, 1, 2, 3);
            forge.Hosts[4] = new Item(ItemID.CopperBroadsword); forge.Hosts[5] = new Item(ItemID.IronBroadsword);
            check(Bond(forge, "wood-bond-forge"), "Forge: four woods with palm plus copper and iron broadswords");
            forge.Hosts[5].TurnToAir();
            check(!Bond(forge, "wood-bond-forge"), "Forge: missing iron broadsword breaks the bond");
            var forgeThin = Soul(); Host(forgeThin, 0, 1, 2);
            forgeThin.Hosts[3] = new Item(ItemID.CopperBroadsword); forgeThin.Hosts[4] = new Item(ItemID.IronBroadsword);
            check(!Bond(forgeThin, "wood-bond-forge"), "Forge: three woods stay below the fourth stage");

            var rot = Soul(); Host(rot, 0, 1, 2, 3, 4, 5);
            rot.Hosts[6] = new Item(ItemID.Muramasa); rot.Hosts[7] = new Item(ItemID.NightsEdge);
            check(Bond(rot, "wood-bond-rot"), "Rot: six woods with mahogany and ebon plus Muramasa and Night's Edge");
            rot.Hosts[3] = new Item(WoodTypes.Types[7]);
            check(!Bond(rot, "wood-bond-rot"), "Rot: losing mahogany breaks the bond");

            var ember = Soul(); Host(ember, 0, 1, 2, 3, 4, 6);
            ember.Hosts[6] = new Item(ItemID.FieryGreatsword);
            check(Bond(ember, "wood-bond-ember"), "Ember: six woods with mahogany and ash plus Fiery Greatsword");
            ember.Hosts[6].TurnToAir();
            check(!Bond(ember, "wood-bond-ember"), "Ember: missing Fiery Greatsword breaks the bond");

            var crown = Soul(); Host(crown, 0, 1, 2, 3, 4, 5, 6, 7);
            crown.Hosts[8] = new Item(ItemID.TerraBlade);
            check(Bond(crown, "wood-bond-crown") && crown.CrownCapacity == 2 && crown.Wood.Strength == 3f,
                "Crown: eight woods plus a terra blade hosted outside the crown slots");
            var selfish = Soul(); Host(selfish, 0, 1, 2, 3, 4, 5, 6, 7);
            selfish.CrownHosts[0] = new Item(ItemID.TerraBlade);
            check(!Bond(selfish, "wood-bond-crown") && selfish.CrownCapacity == 0,
                "Crown: a terra blade in a crown slot cannot unlock itself");
            check(!Bond(full, "wood-bond-crown"), "Crown: eight woods alone are not enough");

            // —— c 槽位编号与容量 ——
            check(SoulItem.SupportOrdinal(35) == -1 && SoulItem.SupportOrdinal(36) == -1, "Crown slots 35/36 are never support ordinals");
            check(SoulItem.SupportOrdinal(SoulItem.LegendarySlotId) == -1, "Legendary slot 34 is skipped by the ordinal chain");
            check(SoulItem.SupportSlotId(14) == 37 && SoulItem.SupportOrdinal(37) == 14, "Ordinal 14 maps to slot 37 in both directions");
            check(SoulItem.SupportSlotId(4) == 14 && SoulItem.SupportOrdinal(14) == 4, "Base ordinals 0-4 stay at slots 10-14");
            check(SoulItem.SupportSlotId(5) == 25 && SoulItem.SupportOrdinal(25) == 5, "Extra ordinals start at slot 25");
            check(crown.VisibleSlotIds.Contains(SoulItem.CrownSlotBase) && crown.VisibleSlotIds.Contains(SoulItem.CrownSlotBase + 1),
                "Active crown exposes both crown slots");
            check(!full.VisibleSlotIds.Contains(SoulItem.CrownSlotBase), "Inactive crown hides the crown slots");
            Item crownCursor = new(ItemID.StarWrath);
            check(crown.TrySwapHost(SoulItem.CrownSlotBase, ref crownCursor, out _) && crownCursor.IsAir,
                "Active crown accepts a real weapon in slot 35");
            Item blocked = new(ItemID.StarWrath);
            check(!full.TrySwapHost(SoulItem.CrownSlotBase, ref blocked, out _) && !blocked.IsAir,
                "Inactive crown rejects insertion and keeps the cursor");
            check(full.BonusCapacity == 0 && crown.BonusCapacity == 0, "Eight woods open no bonus slot");
            var ore = Soul(); Host(ore, 0, 1, 2, 3, 4, 5, 6, 7); ore.OreCollectionMask = OreFamily.AllMaterials;
            check(ore.BonusCapacity == 2, "Full ore collection opens the two bonus slots");

            // —— d 森林之力双成长表 ——
            player.inventory[0] = full.Item; player.selectedItem = 0;
            var woodPlayer = player.GetModPlayer<WoodPlayer>();
            check(ForestPower.BaseCap == 6 && ForestPower.CrownCap == 12, "Forest power caps are six and twelve layers");
            check(ForestPower.Layers(player) == 0 && ForestPower.DamageBonus(player) == 0, "No layers means no growth bonus");
            woodPlayer.RecordHit(full);
            for (int frame = 0; frame < 300; frame++) woodPlayer.AdvanceGrowth();
            check(ForestPower.Layers(player) == 1, "One layer after 300 valid combat frames");
            check(Math.Abs(ForestPower.DamageBonus(player) - .02f) < .001 && Math.Abs(ForestPower.SpeedBonus(player) - .01f) < .001 &&
                ForestPower.DefenseBonus(player) == 2 && Math.Abs(ForestPower.RegenBonus(player) - .3f) < .001 &&
                Math.Abs(ForestPower.DotBonusPercent(player) - .03f) < .001 && Math.Abs(ForestPower.DamageReduction(player) - .005f) < .001,
                "Base per layer: 2% damage, 1% speed, 2 defense, 0.3 regen, 3% DoT, 0.5% DR");
            for (int frame = 0; frame < 1800; frame++) { woodPlayer.RecordHit(full); woodPlayer.AdvanceGrowth(); }
            check(ForestPower.Layers(player) == 6 && !ForestPower.CrownActive(player), "Base growth stops at six layers");

            player.inventory[0] = crown.Item;
            check(ForestPower.CrownActive(player), "Crown is active while the terra blade stays hosted");
            woodPlayer.RecordHit(crown);
            for (int frame = 0; frame < 300; frame++) woodPlayer.AdvanceGrowth();
            check(ForestPower.Layers(player) == 1 && Math.Abs(ForestPower.DamageBonus(player) - .03f) < .001 &&
                Math.Abs(ForestPower.SpeedBonus(player) - .02f) < .001 && ForestPower.DefenseBonus(player) == 3 &&
                Math.Abs(ForestPower.RegenBonus(player) - .5f) < .001 && Math.Abs(ForestPower.DotBonusPercent(player) - .05f) < .001 &&
                Math.Abs(ForestPower.DamageReduction(player) - .01f) < .001,
                "Crown per layer: 3% damage, 2% speed, 3 defense, 0.5 regen, 5% DoT, 1% DR");
            for (int frame = 0; frame < 3300; frame++) { woodPlayer.RecordHit(crown); woodPlayer.AdvanceGrowth(); }
            check(ForestPower.Layers(player) == 12, "Crown growth reaches twelve layers");

            // —— e 三把已删除新增剑 ——
            foreach (string name in new[] { "DynastyWoodSword", "SpookyWoodSword", "FeyWoodSword" })
                check(!ModContent.TryFind<ModItem>("ZenithReborn", name, out _), "Removed wood sword is not registered: " + name);
            var leftovers = ModContent.GetContent<ModItem>()
                .Where(m => m.Mod is { } owner && owner.Name == "ZenithReborn" && (m.Name.Contains("WoodSword") || m.Name.Contains("Fey")))
                .ToArray();
            check(leftovers.Length == 0, "No custom wood sword item classes remain in ZenithReborn");
            check(!Enum.GetNames<WoodKind>().Contains("Fey") && WoodTypes.Types.All(t => t < ItemID.Count),
                "WoodKind has no Fey member and the roster is vanilla-only");

            // —— f 族内计数口径：传奇位计入，但支援解锁与森林/冠位不得被带动 ——
            // 现有断言都在“传奇位为空”前提下跑，这里自行构造传奇位场景。
            int oldGameMode = Main.GameMode; bool oldGoodWorld = Main.getGoodWorld;
            try
            {
                Main.GameMode = 2; Main.getGoodWorld = true;   // SoulDifficulty.Legendary = 大师(2) 且 getGoodWorld
                // 主攻槽放 7 把不同木剑，传奇位放第 8 种（珍珠木剑）。
                // 同时解除切换诅咒：CombatWood 会按 TraitEnabled 过滤，未解除时只保留首把主剑的特性，
                // 木族倍率会被砍成 1.10，测不出归冠覆盖。
                var legend = Soul(); Host(legend, 0, 1, 2, 3, 4, 5, 6);
                legend.Seals = SoulCurses.Brain | SoulCurses.Cultist;
                int baseCount = BondSnapshot.Wood(legend).Count;
                int baseUnlock = SoulSupport.UnlockCount(legend);
                int baseGrant = SoulSupport.GrantedBy(legend, "wood");
                legend.LegendaryHost = new Item(WoodTypes.Types[7]);
                check(legend.LegendaryOn && baseCount == 7 && BondSnapshot.Wood(legend).Count == 8,
                    "Legendary: eighth wood in the legendary slot raises the family count 7 -> 8");
                legend.LegendaryHost.TurnToAir();
                check(BondSnapshot.Wood(legend).Count == 7, "Legendary: clearing the legendary slot returns the count to 7");

                // 暂停态：槽未生效时即便有物品也不计入。
                legend.LegendaryHost = new Item(WoodTypes.Types[7]);
                Main.getGoodWorld = false;
                check(!legend.LegendaryOn && BondSnapshot.Wood(legend).Count == 7,
                    "Legendary: an inactive legendary slot is not counted");
                Main.getGoodWorld = true;
                legend.LegendaryHost.TurnToAir();

                // 支援解锁与种族授予仍只看主攻+已生效支援：传奇位不得白送支援格。
                legend.LegendaryHost = new Item(WoodTypes.Types[7]);
                check(SoulSupport.UnlockCount(legend) == baseUnlock && SoulSupport.GrantedBy(legend, "wood") == baseGrant,
                    "Legendary: the legendary slot grants no extra support slot");

                // 本次改动唯一有玩法分量的后果：传奇位补满第 8 种后，8/8 全套成立，
                // 配合主攻槽里的泰拉刃应真正进入圣林归冠（3.00 倍 / +100 生命 / 开 2 个圣林主攻位）。
                legend.Hosts[7] = new Item(ItemID.TerraBlade);
                player.inventory[0] = legend.Item; player.selectedItem = 0;
                check(WoodTypes.Mask(SoulSupport.FamilyItems(legend)) == 0xFF && legend.CrownCapacity == 2 &&
                    Math.Abs(legend.CombatWood.Strength - 3f) < .001f && ForestPower.CrownActive(player),
                    "Legendary: seventh wood plus the legendary eighth and a terra blade reach 8/8 and crown");
                // 取下传奇位后回落到 7 种：归冠失效、圣林位关闭，倍率按 WoodLoadout 的 6 种档取 1.50。
                legend.LegendaryHost.TurnToAir();
                check(BondSnapshot.Wood(legend).Count == 7 && legend.CrownCapacity == 0 &&
                    Math.Abs(legend.CombatWood.Strength - 1.5f) < .001f && !ForestPower.CrownActive(player),
                    "Legendary: clearing the legendary slot drops 8/8 back to seven woods and 1.50 strength");
                legend.Hosts[7].TurnToAir();
                player.selectedItem = 0;

                // 森林奖励槽：奖励位不设例外，全部计入族数量。先真正开槽（矿石圆满 → BonusCapacity 2）。
                var bonus = Soul(); Host(bonus, 0, 1, 2, 3, 4, 5);
                bonus.OreCollectionMask = OreFamily.AllMaterials;
                check(bonus.BonusCapacity == 2, "Forest slots: full ore collection really opens the two forest slots");
                int bonusUnlock = SoulSupport.UnlockCount(bonus), bonusGrant = SoulSupport.GrantedBy(bonus, "wood");
                bonus.ForestHosts[0] = new Item(WoodTypes.Types[6]);
                bonus.ForestHosts[1] = new Item(WoodTypes.Types[7]);
                check(BondSnapshot.Wood(bonus).Count == 8 &&
                    WoodTypes.Mask(SoulSupport.FamilyItems(bonus)) == 0xFF,
                    "Forest slots: six primary woods plus two forest woods reach 8/8");
                check(SoulSupport.UnlockCount(bonus) == bonusUnlock &&
                    SoulSupport.GrantedBy(bonus, "wood") == bonusGrant,
                    "Forest slots: forest woods never grant extra support slots or race tiers");
                bonus.ForestHosts[0].TurnToAir(); bonus.ForestHosts[1].TurnToAir();

                // 圣林槽自举防线：7 种木剑 + 第 8 种塞进圣林槽 + 泰拉刃在主攻槽。
                // 圣林槽本身由 8/8 解锁，重入护盾让判定途中 CrownCapacity=0，这把剑不能反向凑满 8/8。
                var crownSelf = Soul(); Host(crownSelf, 0, 1, 2, 3, 4, 5, 6);
                crownSelf.Hosts[7] = new Item(ItemID.TerraBlade);
                crownSelf.CrownHosts[0] = new Item(WoodTypes.Types[7]);
                check(crownSelf.CrownCapacity == 0 && BondSnapshot.Wood(crownSelf).Count == 7 &&
                    WoodTypes.Mask(SoulSupport.FamilyItems(crownSelf)) == 0x7F,
                    "Crown slots: a wood sword in a crown slot cannot unlock the crown nor feed the count");
                crownSelf.CrownHosts[0].TurnToAir(); crownSelf.Hosts[7].TurnToAir();

                // 圣林槽的已知约束（本次未解，需裁决）：8/8 达成后把一把木剑从主攻槽挪进圣林槽，
                // 归冠会立刻失效。原因是 WoodCrown.Active → FamilyItems → Active → CrownCapacity →
                // WoodCrown.Active 这条环上的重入护盾在判定途中返回 false，CrownCapacity 归零，
                // 圣林槽内那把剑因此无法参与它自己的 8/8 判定。放开它必须改 evaluating（本轮禁止）。
                var crownSwap = Soul(); Host(crownSwap, 0, 1, 2, 3, 4, 5, 6, 7);
                crownSwap.Hosts[8] = new Item(ItemID.TerraBlade);
                check(crownSwap.CrownCapacity == 2, "Crown slots: baseline 8/8 really opens the two crown slots");
                crownSwap.CrownHosts[0] = crownSwap.Hosts[7].Clone();
                crownSwap.Hosts[7].TurnToAir();
                check(crownSwap.CrownCapacity == 0 && BondSnapshot.Wood(crownSwap).Count == 7,
                    "Crown slots: a wood moved into a crown slot collapses the crown (self-reference guard)");
                crownSwap.CrownHosts[0].TurnToAir(); crownSwap.Hosts[8].TurnToAir();
            }
            finally
            {
                Main.GameMode = oldGameMode; Main.getGoodWorld = oldGoodWorld;
            }
        }
        finally
        {
            Main.netMode = oldMode; Main.myPlayer = oldPlayer;
        }
    }
}
