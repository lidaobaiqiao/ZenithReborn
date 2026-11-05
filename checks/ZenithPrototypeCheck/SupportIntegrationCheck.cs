using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class SupportIntegrationCheck
{
    private static SoulItem Soul()
    {
        var s=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        s.Attached=true;s.Kills=SoulProgress.All;s.Cores=63;s.Seals=SoulCurses.All;s.LegacyReleased=0;
        return s;
    }
    internal static void Run(Action<bool,string> check)
    {
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        var player=new Player{active=true,whoAmI=0};Main.player[0]=player;
        int[] shorts=[ItemID.CopperShortsword,ItemID.TinShortsword,ItemID.IronShortsword,ItemID.LeadShortsword,
            ItemID.SilverShortsword,ItemID.TungstenShortsword,ItemID.GoldShortsword,ItemID.PlatinumShortsword];
        foreach(int type in OreFamily.DefaultType.Concat(shorts).Append(ItemID.FieryGreatsword).Append(ItemID.Cutlass))
        {
            var s=Soul();s.Hosts[0]=new Item(ItemID.WoodenSword);player.inventory[0]=s.Item;
            Item incoming=new(type);incoming.Prefix(PrefixID.Legendary);Item actual=incoming;
            check(s.SupportCapacity==1 && s.TrySwapHost(10,ref incoming,out _) && incoming.IsAir &&
                ReferenceEquals(s.SupportHosts[0],actual) && actual.prefix==PrefixID.Legendary,
                "Any registered or plain melee sword uses the real common support transfer through the shared pool: "+type);
            if(OreFamily.IsOre(type))check(OreFamily.Count(s)==1 && OreFamily.Materials(s).Single()==OreFamily.MaterialOf(type) &&
                OreFamily.TriggerEligible(s,ItemID.WoodenSword),"Ore support contributes its material and unlocks flight at one, not ten: "+type);
        }
        foreach(int count in new[]{1,2,4,6,8,10})
        {
            var s=Soul();for(int i=0;i<count;i++)s.Hosts[i]=new Item(OreFamily.DefaultType[i]);
            check(s.SupportCapacity==Array.IndexOf(SoulSupport.Grants.Single(g=>g.Id=="ore").Thresholds,count)+1 && s.Wood.Count==0,
                "Non-wood loadout opens the common support threshold without creating wood buffs: "+count);
        }
        var full=Soul();for(int i=0;i<10;i++)full.Hosts[i]=new Item(OreFamily.DefaultType[i]);
        for(int i=0;i<4;i++)
        {
            Item incoming=new(OreFamily.DefaultType[10+i]);
            check(full.TrySwapHost(10+i,ref incoming,out _) && incoming.IsAir,"Actual ore-only support insertion accepts material "+(10+i));
        }
        full.RefreshStats();
        check(full.SupportCapacity==6 && full.Supports.Count()==4 && OreFamily.Count(full)==14 && full.OreMastered &&
            full.BonusCapacity==2 && full.Wood.Count==0 && !full.CombatWood.Full,
            "Ten ore primaries plus four real supports produce all fourteen materials and the shared reward slots with no wood");
        var text=SoulTraits.Active(full).Single(t=>t.Key=="ore-wave").Description;
        check(text.Contains("主攻10、支援4") && text.Contains("150%"),"Active UI reports real primary/support material contributions and the current confirmed damage base");
        full.SupportHosts[5]=new Item(ItemID.EnchantedSword); // Deliberately retired storage, not S6.
        Item sixth=new(ItemID.FieryGreatsword);sixth.Prefix(PrefixID.Legendary);
        check(full.TrySwapHost(SoulItem.SupportSlotId(5),ref sixth,out _) && sixth.IsAir && full.AdditionalSupports.Count==1 &&
            full.Supports.Any(i=>i.type==ItemID.FieryGreatsword) && !full.Supports.Any(i=>i.type==ItemID.EnchantedSword),
            "The sixth earned support uses new storage; old legacy S6 never silently activates");
        var tag=new TagCompound();full.SaveData(tag);var restored=Soul();restored.LoadData(tag);
        check(OreFamily.Count(restored)==14 && restored.SupportCapacity==6 && restored.SoulId==full.SoulId,
            "Generic capacity and all support materials survive actual item save/load");
        check(restored.AdditionalSupports.Single().prefix==PrefixID.Legendary && restored.SupportHosts[5].type==ItemID.EnchantedSword,
            "New S6 and retired S6 remain separate across save/load with exact prefixes");
        using(var bytes=new MemoryStream())
        {
            full.NetSend(new BinaryWriter(bytes));bytes.Position=0;restored.NetReceive(new BinaryReader(bytes));
            check(OreFamily.Count(restored)==14 && restored.SupportCapacity==6 && bytes.Position==bytes.Length,
                "Shared support round-trips exact material totals and consumes the extended network schema");
        }
        var clone=(SoulItem)full.Item.Clone().ModItem;clone.AdditionalSupports[0].TurnToAir();
        check(!full.AdditionalSupports[0].IsAir,"New support storage deep-clones its items");
        full.PreReforge();full.Item.ResetPrefix();full.PostReforge();
        check(full.AdditionalSupports[0].prefix==PrefixID.Legendary && full.SupportCapacity==6,"Real reforge preserves the new sixth support and its prefix");
        var compat=typeof(SoulItem).Assembly.GetType("ZenithReborn.HeroPrefixCompat");
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
        compat.GetMethod("Remember",flags).Invoke(null,[full]);var rebuilt=Soul();rebuilt.Attached=false;rebuilt.Kills=rebuilt.Cores=rebuilt.Seals=0;
        bool restoredHero=(bool)compat.GetMethod("TryRestore",flags).Invoke(null,[rebuilt]);
        check(restoredHero && rebuilt.AdditionalSupports[0].prefix==PrefixID.Legendary && rebuilt.SupportHosts[5].type==ItemID.EnchantedSword,
            "HERO reconstruction keeps newly earned support separate from retired storage and preserves its prefix");
        compat.GetMethod("Clear",flags).Invoke(null,null);
        var mixed=Soul();mixed.OreCollectionMask=OreFamily.AllMaterials;
        // 旧口径：7 主槽 + 5 支援槽共 12 木；现名册 8 把 → 3 主槽 + 5 支援槽，
        // 保持「5 个木质支援 + 6 个矿石支援 = 11 格」的原断言结构不变。
        for(int i=0;i<3;i++)mixed.Hosts[i]=new Item(WoodTypes.Types[i]);
        for(int i=0;i<3;i++)mixed.Hosts[i+7]=new Item(OreFamily.DefaultType[i]);
        mixed.ForestHosts[0]=new Item(OreFamily.DefaultType[9]);mixed.ForestHosts[1]=new Item(OreFamily.DefaultType[10]);
        for(int i=0;i<5;i++){Item wood=new(WoodTypes.Types[i+3]);check(mixed.TrySwapHost(SoulItem.SupportSlotId(i),ref wood,out _),"Mixed race pool admits wood support "+i);}
        for(int i=0;i<6;i++){Item ore=new(OreFamily.DefaultType[i+3]);check(mixed.TrySwapHost(SoulItem.SupportSlotId(i+5),ref ore,out _),"Mixed race pool admits additional ore support "+i);}
        check(mixed.SupportCapacity==11 && SoulSupport.GrantedBy(mixed,"wood")==5 && SoulSupport.GrantedBy(mixed,"ore")==6 &&
            mixed.Supports.Count()==11 && mixed.Wood.Full && OreFamily.Count(mixed)==11,
            "Five wood and six ore grants add to eleven shared supports instead of taking a maximum or clamping to five");
        var mixedTag=new TagCompound();mixed.SaveData(mixedTag);restored.LoadData(mixedTag);
        check(restored.SupportCapacity==11 && restored.AdditionalSupports.Count==6 && restored.Supports.Count()==11,
            "All six additional support slots survive serialization rather than truncating at the original five");
        foreach(var item in mixed.Hosts)item.TurnToAir();foreach(var item in mixed.ForestHosts)item.TurnToAir();
        mixed.OreCollectionMask=0;
        check(mixed.SupportCapacity==0 && mixed.Supports.Count()==0 && mixed.AdditionalSupports.Count==6,
            "Losing all anchors pauses every grant but retains the extra items for safe recovery");
        Item recovered=new();check(mixed.TrySwapHost(SoulItem.SupportSlotId(10),ref recovered,out _) && !recovered.IsAir,
            "Paused eleventh support has a real empty-cursor retrieval path");
        var oldTag=new TagCompound();full.SaveData(oldTag);oldTag.Remove("additionalSupports");restored.LoadData(oldTag);
        check(restored.AdditionalSupports.Count==0 && restored.SupportHosts[5].type==ItemID.EnchantedSword &&
            !restored.Supports.Any(i=>i.type==ItemID.EnchantedSword),"Old saves do not reinterpret disabled expansion contents as earned new support items");
        full.SupportHosts[0]=new Item(ItemID.CopperShortsword);
        check(OreFamily.Count(full)==13,"Ore short/broad variants count as one material across the shared support boundary");
        var paused=Soul();for(int i=0;i<5;i++)paused.SupportHosts[i]=new Item(OreFamily.DefaultType[i]);
        paused.Hosts[10]=new Item(ItemID.EnchantedSword);paused.ForestHosts[0]=new Item(ItemID.IceBlade);
        check(paused.SupportCapacity==0 && OreFamily.Count(paused)==0,"Locked, reward and retired storage cannot bootstrap common supports or material totals");
        paused.Hosts[0]=new Item(ItemID.WoodenSword);
        check(paused.SupportCapacity==3 && OreFamily.Count(paused)==3 && !paused.SupportHosts[4].IsAir,
            "Least active prefix includes only three reachable supports and safely retains later paused items");
        paused.Seals&=~(SoulCurses.Brain|SoulCurses.Cultist);
        check(paused.SupportCapacity==3 && OreFamily.Count(paused)>0,
            "Deviation no longer suppresses support traits while structural support unlocks stay intact");
        var forest=Soul();forest.OreCollectionMask=OreFamily.AllMaterials;
        // 裁决：奖励位本身是给玩家的回报，不设例外——森林奖励槽同样计入族数量。
        // 森林槽由矿石圆满解锁（BonusCapacity=OreMastered），与木质数量无关，因此不存在自举。
        for(int i=0;i<6;i++)forest.Hosts[i]=new Item(WoodTypes.Types[i]);
        forest.ForestHosts[0]=new Item(WoodTypes.Types[6]);forest.ForestHosts[1]=new Item(WoodTypes.Types[7]);
        check(forest.Wood.Full,"Forest reward slots count toward the family total with no reward-slot exception");
        // 口径合一：TraitEnabled 恒 true，特性计数不再依赖偏锋诅咒是否解除，始终与数值口径一致。
        forest.Seals=SoulCurses.Brain|SoulCurses.Cultist;
        check(forest.CombatWood.Full,"Combat counting agrees with the full set while traits are unconditional");
        check(OreFamily.PerSwordDamage(100,1)==150 && OreFamily.PerSwordDamage(100,10)==326 &&
            OreFamily.PerSwordDamage(100,14,true)==482,"Confirmed independent flight snapshots are 150, 326 and 482 for a 100-damage source");
    }
}
