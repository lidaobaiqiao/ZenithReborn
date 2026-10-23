using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class SpiritFamilyCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var members=SpiritFamily.Members;
        check(members.Count==22 && members.Select(m=>m.Key).Distinct().Count()==22,
            "Spirit foundation registers the 22 distinct R2 member identities");
        check(members.Count(m=>m.Scope==SpiritMemberScope.Regular)==14 &&
            members.Count(m=>m.Scope==SpiritMemberScope.Unconventional)==5 &&
            members.Count(m=>m.Scope==SpiritMemberScope.Deferred)==3,
            "Spirit member scope preserves regular, unconventional and deferred development boundaries");
        check(SpiritFamily.Values.Count==0 && SpiritFamily.Traits.Count==0 &&
            SpiritFamily.SupportThresholds.Count==0 && SpiritFamily.CollectionRewards.Count==0,
            "Unspecified spirit mechanics, values, support tiers and rewards stay empty");
        var types=members.SelectMany(m=>m.ItemTypes).ToArray();
        check(types.Distinct().Count()==types.Length,"No weapon type belongs to two spirit member identities");
        foreach(var member in members)
        foreach(int type in member.ItemTypes)
        {
            var item=new Item(type);
            check(!item.IsAir && item.damage>0 && item.CountsAsClass(DamageClass.Melee) &&
                SpiritFamily.TryGet(type,out var found) && ReferenceEquals(found,member) &&
                ReferenceEquals(SpiritFamily.For(item),member),"Spirit registration resolves a real melee weapon: "+member.Key);
        }
        check(SpiritFamily.For(null)==null && SpiritFamily.For(new Item())==null &&
            !SpiritFamily.TryGet(-1,out _) && !SpiritFamily.IsMember(ItemID.CopperBroadsword) &&
            !SpiritFamily.IsMember(ItemID.WoodenSword) && !SpiritFamily.IsMember(ItemID.WoodenBow),
            "Null, air, unknown and other families do not acquire invented spirit membership");
        var phase=SpiritFamily.ForType(ItemID.BluePhaseblade);
        check(phase.ItemTypes.Count==7 && phase.ItemTypes.All(t=>ReferenceEquals(SpiritFamily.ForType(t),phase)),
            "Phaseblade colors share one member identity rather than seven future synergy contributions");
        var saber=SpiritFamily.ForType(ItemID.BluePhasesaber);
        check(saber.ItemTypes.Count==7 && saber!=phase && saber.ItemTypes.All(t=>ReferenceEquals(SpiritFamily.ForType(t),saber)),
            "Phasesaber upgrades have their own shared identity, separate from pre-hardmode phaseblades");
        bool immutable=false;try{((IList<int>)phase.ItemTypes)[0]=ItemID.CopperBroadsword;}
        catch(NotSupportedException){immutable=true;}
        check(immutable && SpiritFamily.IsMember(ItemID.BluePhaseblade),"Published membership cannot be mutated by consumers");
        var sword=new Item(ItemID.EnchantedSword);sword.Prefix(PrefixID.Legendary);
        var loaded=ItemIO.Load(ItemIO.Save(sword));
        check(loaded.prefix==sword.prefix && ReferenceEquals(SpiritFamily.For(sword),SpiritFamily.For(loaded)) &&
            ReferenceEquals(SpiritFamily.For(sword),SpiritFamily.For(sword.Clone())),
            "Type metadata survives actual item save, load, clone and prefix without per-item state");
        using(var bytes=new MemoryStream())
        {
            ItemIO.Send(sword,new BinaryWriter(bytes));bytes.Position=0;
            var remote=ItemIO.Receive(new BinaryReader(bytes));
            check(ReferenceEquals(SpiritFamily.For(sword),SpiritFamily.For(remote)) && bytes.Position==bytes.Length,
                "Item network roundtrip resolves spirit membership with the unchanged payload");
        }
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.Seals=SoulCurses.All;
        soul.Hosts[0]=sword;soul.Hosts[1]=new Item(ItemID.WoodenSword);
        soul.SupportHosts[0]=new Item(ItemID.IceBlade);
        soul.Hosts[10]=new Item(ItemID.BeamSword);soul.SupportHosts[5]=new Item(ItemID.Frostbrand);
        soul.AdditionalSupports.Add(new Item(ItemID.StarWrath));
        var player=new Player{whoAmI=0,active=true};player.ResetEffects();
        int capacity=soul.SupportCapacity,damage=soul.BladeDamage(player,sword);
        int ore=OreFamily.Count(soul),wood=soul.Wood.Count;
        var environments=sword.GetEnvironmentAttributes().Select(d=>d.Id).ToArray();
        var view=soul.InspectSpiritMembers().ToArray();
        check(view.Length==2 && view.Any(v=>ReferenceEquals(v.Weapon,sword) && !v.IsSupport) &&
            view.Any(v=>v.Weapon.type==ItemID.IceBlade && v.IsSupport),
            "Spirit inspection includes actual active main and generic support sources only");
        check(!view.Any(v=>v.Weapon.type is ItemID.BeamSword or ItemID.Frostbrand or ItemID.StarWrath),
            "Legacy recovery and unopened additional support items stay outside the spirit view");
        check(capacity==soul.SupportCapacity && damage==soul.BladeDamage(player,sword) &&
            ore==OreFamily.Count(soul) && wood==soul.Wood.Count &&
            environments.SequenceEqual(sword.GetEnvironmentAttributes().Select(d=>d.Id)) &&
            WeaponFamilies.Of(sword.type)==BladeFamily.Arcane && SoulSupport.Grants.All(g=>g.Id!=SpiritFamily.Key),
            "Spirit metadata does not alter old combat profiles, damage, environments, families or support grants");
        int oldMode=Main.GameMode;bool oldFtw=Main.getGoodWorld;
        try
        {
            Main.GameMode=2;Main.getGoodWorld=true;soul.LegendaryHost=new Item(ItemID.Starfury);
            check(soul.InspectSpiritMembers().Any(v=>ReferenceEquals(v.Weapon,soul.LegendaryHost) && !v.IsSupport),
                "Existing active legendary primary appears as a main source without a special spirit slot");
            Main.getGoodWorld=false;
            check(!soul.InspectSpiritMembers().Any(v=>ReferenceEquals(v.Weapon,soul.LegendaryHost)),
                "Paused legendary weapon does not appear in current spirit inspection");
        }
        finally{Main.GameMode=oldMode;Main.getGoodWorld=oldFtw;}
    }
}
