using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class EnvironmentAttributeCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var catalog=EnvironmentAttributes.All;
        check(catalog.Count==21 && catalog.Select(d=>d.Id).Distinct().Count()==21 && catalog.Select(d=>d.Key).Distinct().Count()==21,
            "Environment foundation has exactly the user's twenty-one unique stable definitions");
        for(int i=0;i<catalog.Count;i++)
        {
            var d=catalog[i];
            check((byte)d.Id==i+1 && EnvironmentAttributes.Get(d.Id)==d,"Environment ID round-trip agrees with E01-E21: "+d.Key);
            check(d.Values.Count>0 && d.Traits.Count==1,"Environment publishes its tuned values and named mechanism: "+d.Key);
        }
        check(!EnvironmentAttributes.TryGet(SwordEnvironment.None,out _) && !EnvironmentAttributes.TryGet((SwordEnvironment)255,out _),
            "Unset or unknown IDs are not real environment attributes");
        check(new Item(ItemID.BorealWoodSword).GetEnvironmentAttributes().Single().Id==SwordEnvironment.Snow &&
            new Item(ItemID.AshWoodSword).GetEnvironmentAttributes().Single().Id==SwordEnvironment.Underworld,
            "Confirmed wooden environment themes are registered by actual load hooks");
        check(new Item().GetEnvironmentAttributes().Count==0 && WeaponEnvironmentAttributes.For(null).Count==0 &&
            new Item(ItemID.WoodenBow).GetEnvironmentAttributes().Count==0,"Air and unassigned bows do not invent environment tags");
        var original=WeaponEnvironmentAttributes.ForType(ItemID.CopperBroadsword).Select(d=>d.Id).ToArray();
        var tags=new[]{SwordEnvironment.Caverns,SwordEnvironment.Underground,SwordEnvironment.Caverns};
        int previousGameMode=Main.GameMode;
        try
        {
            WeaponEnvironmentAttributes.SetForType(ItemID.CopperBroadsword,tags);tags[0]=SwordEnvironment.Ocean;
            var sword=new Item(ItemID.CopperBroadsword);sword.Prefix(PrefixID.Legendary);
            check(sword.GetEnvironmentAttributes().Select(d=>d.Id).SequenceEqual(new[]{SwordEnvironment.Underground,SwordEnvironment.Caverns}),
                "One real ore sword accepts multiple independently stored environment attributes with copied, deduplicated authoring input");
            bool rejected=false;try{WeaponEnvironmentAttributes.SetForType(sword.type,SwordEnvironment.Forest,(SwordEnvironment)255);}
            catch(ArgumentOutOfRangeException){rejected=true;}
            check(rejected && sword.GetEnvironmentAttributes().Count==2,"Invalid authoring input cannot partially replace an existing weapon binding");
            var loaded=ItemIO.Load(ItemIO.Save(sword));
            check(loaded.prefix==sword.prefix && loaded.GetEnvironmentAttributes().Count==2 && sword.Clone().GetEnvironmentAttributes().Count==2,
                "Type metadata survives actual item save/load, clone and prefixes without new per-item save state");
            using(var bytes=new MemoryStream())
            {
                ItemIO.Send(sword,new BinaryWriter(bytes));bytes.Position=0;var remote=ItemIO.Receive(new BinaryReader(bytes));
                check(remote.GetEnvironmentAttributes().Count==2 && bytes.Position==bytes.Length,"Actual item network read sees the same environment metadata without extending the payload");
            }
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.Seals=SoulCurses.All;
            soul.Hosts[0]=sword;soul.Hosts[1]=new Item(ItemID.WoodenSword);soul.SupportHosts[0]=new Item(ItemID.BorealWoodSword);
            soul.Hosts[10]=new Item(ItemID.AshWoodSword);soul.SupportHosts[5]=new Item(ItemID.PearlwoodSword);
            var p=new Player{whoAmI=0,active=true};p.ResetEffects();int damage=soul.BladeDamage(p,sword);
            int ore=OreFamily.Count(soul),wood=soul.Wood.Count,capacity=soul.SupportCapacity;
            var view=soul.InspectEnvironmentAttributes().ToArray();
            check(view.Any(v=>ReferenceEquals(v.Weapon,sword) && !v.IsSupport && v.Attributes.Count==2) &&
                view.Any(v=>v.IsSupport && v.Weapon.type==ItemID.BorealWoodSword),"Hosted attribute view covers both ordinary active swords and generic supports");
            check(!view.Any(v=>v.Weapon.type==ItemID.AshWoodSword || v.Weapon.type==ItemID.PearlwoodSword),
                "Retired recovery storage does not silently join the active metadata view");
            WeaponEnvironmentAttributes.SetForType(sword.type,SwordEnvironment.Space);
            check(OreFamily.Count(soul)==ore && soul.Wood.Count==wood && soul.SupportCapacity==capacity && soul.BladeDamage(p,sword)==damage &&
                WoodTypes.IsWood(soul.Hosts[1].type) && OreFamily.IsOre(sword.type),
                "Changing environment metadata does not replace race identity or change damage, set counts or support capacity");
            Main.GameMode=2;Main.getGoodWorld=true;soul.LegendaryHost=new Item(ItemID.AshWoodSword);
            check(soul.InspectEnvironmentAttributes().Any(v=>ReferenceEquals(v.Weapon,soul.LegendaryHost) && !v.IsSupport),
                "Legendary primary participates through the existing active source enumeration");
        }
        finally{WeaponEnvironmentAttributes.SetForType(ItemID.CopperBroadsword,original);Main.GameMode=previousGameMode;Main.getGoodWorld=false;}
        check(WeaponEnvironmentAttributes.ForType(ItemID.CopperBroadsword).Select(d=>d.Id).SequenceEqual(original),"Metadata authoring test restores the original ore environment binding");
    }
}
