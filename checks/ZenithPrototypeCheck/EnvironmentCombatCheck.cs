using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;
using E=ZenithReborn.SwordEnvironment;

namespace ZenithPrototypeCheck;

internal static class EnvironmentCombatCheck
{
    private delegate int TextOriginal(Rectangle area,Color color,string text,bool dramatic,bool dot);
    private static int HeadlessText(TextOriginal orig,Rectangle area,Color color,string text,bool dramatic,bool dot)=>Main.dedServ?100:orig(area,color,text,dramatic,dot);
    private static (Player,SoulItem,EnvironmentPlayer,NPC) Setup()
    {
        foreach(var q in Main.projectile)q.active=false;foreach(var enemy in Main.npc)enemy.active=false;
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        var p=new Player{active=true,whoAmI=0,position=new(1000,1000)};Main.player[0]=p;p.ResetEffects();
        var s=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        s.Attached=true;s.Kills=SoulProgress.All;s.Cores=63;s.Seals=SoulCurses.All;s.Hosts[0]=new Item(ItemID.WoodenSword);
        p.inventory[0]=s.Item;s.RefreshStats();p.statLifeMax2=400;p.statLife=200;p.lifeSteal=80;
        var n=new NPC();n.SetDefaults(NPCID.Zombie);n.whoAmI=20;n.active=true;n.life=n.lifeMax=100000;n.defense=0;
        n.Center=p.Center+new Vector2(120,0);n.HideStrikeDamage=true;Main.npc[20]=n;
        return(p,s,p.GetModPlayer<EnvironmentPlayer>(),n);
    }
    private static Projectile Blade(Player p,SoulItem s,int ordinal)
    {
        var q=Main.projectile[s.EmitBlade(p,s.Hosts[0],0,1)];q.damage=100;q.CritChance=0;
        q.GetGlobalProjectile<SoulSource>().HostSlot=ordinal;((SoulBlade)q.ModProjectile).SpecialDamage=100;
        q.ModProjectile.AI();return q;
    }
    internal static void Run(Action<bool,string> check)
    {
        using var textHook=new MonoMod.RuntimeDetour.Hook(
            typeof(CombatText).GetMethod(nameof(CombatText.NewText),[typeof(Rectangle),typeof(Color),typeof(string),typeof(bool),typeof(bool)]),
            typeof(EnvironmentCombatCheck).GetMethod(nameof(HeadlessText),System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static));
        var(p,s,ep,n)=Setup();s.EnvironmentCollection=EnvironmentRules.All;ep.RefreshEnvironmentStats();
        check(Math.Abs(EnvironmentRules.Shared(s.EnvironmentCollection)-5.2f)<.001,"21 distinct environments supply additive 420% growth");
        foreach(var e in Enum.GetValues<E>().Where(e=>e!=E.None))
            check(EnvironmentRules.Adaptation(E.Forest,EnvironmentRules.Bit(e))==1,"Forest has no weakened environment: "+e);
        check(Math.Abs(ep.Strength(E.Forest)-5.98f)<.001,"Forest's fixed fifteen percent factor applies once without self-recursion");
        check(Math.Abs(ep.Stat("Life")-80*5.98f)<.01 && Math.Abs(ep.Stat("Defense")-10*5.98f)<.01,"Typed passive stats combine before one shared growth/forest factor");
        s.SetEnvironmentDomain(E.Forest);ep.RefreshEnvironmentStats();
        check(Math.Abs(ep.Strength(E.Forest)-8.97f)<.001 && Math.Abs(ep.Strength(E.Hallow)-5.98f)<.001,
            "Active domain adds fifty percent only to its corresponding environment");
        check(ep.Mechanism(E.Forest,n.Center) && !ep.Mechanism(E.Hallow,n.Center),"Collecting all environments does not turn on all mechanisms");
        check(s.ToggleEnvironmentBlessing(E.Hallow) && ep.Mechanism(E.Hallow,n.Center),"Chosen blessing runs outside its original biome");
        var q=Blade(p,s,0);q.CritChance=150;var m=n.GetIncomingStrikeModifiers(DamageClass.Melee,1);ep.ModifyHit(q,n,ref m);
        float critCoefficient=2+ep.Stat("CritDamage")+.25f*5.98f;
        check(Math.Abs(m.CritDamage.ApplyTo(100)-100*critCoefficient)<.01,"Real hit modifier applies overflow crit to the damage coefficient rather than adding flat damage");
        check(m.ToHitInfo(100,true,0,false,0).Damage==(int)(100*critCoefficient),"Engine hit-info calculation actually multiplies a critical hit by the overflow coefficient");
        s.ToggleEnvironmentBlessing(E.Hallow);m=n.GetIncomingStrikeModifiers(DamageClass.Melee,1);ep.ModifyHit(q,n,ref m);
        check(Math.Abs(m.CritDamage.ApplyTo(100)-100*(2+ep.Stat("CritDamage")))<.01,"Dormant Hallow keeps passive stats but cannot convert overflow");
        foreach(var e in new[]{E.Snow,E.Ice,E.Jungle,E.Underworld,E.Caverns})s.ToggleEnvironmentBlessing(e);
        check(EnvironmentRules.Count(s.EnvironmentBlessings)==5 && !s.ToggleEnvironmentBlessing(E.Desert),"Persistent mechanism selection respects five actually unlocked slots");
        var clone=(SoulItem)s.Item.Clone().ModItem;check(clone.EnvironmentCollection==s.EnvironmentCollection && clone.EnvironmentBlessings==s.EnvironmentBlessings && clone.DomainEnvironment==s.DomainEnvironment,"Environment ledger, selection and domain clone together");
        var saved=ItemIO.Save(s.Item);var loaded=(SoulItem)ItemIO.Load(saved).ModItem;
        check(loaded.EnvironmentCollection==s.EnvironmentCollection && loaded.EnvironmentBlessings==s.EnvironmentBlessings && loaded.DomainEnvironment==s.DomainEnvironment,"Actual ItemIO save/load preserves environment choices");
        using(var bytes=new MemoryStream())
        {s.NetSend(new BinaryWriter(bytes));bytes.Position=0;loaded.NetReceive(new BinaryReader(bytes));
            check(loaded.EnvironmentBlessings==s.EnvironmentBlessings && loaded.DomainEnvironment==s.DomainEnvironment && bytes.Position==bytes.Length,"Actual network round-trip consumes the complete v8 environment payload");}
        s.PreReforge();s.EnvironmentCollection=s.EnvironmentBlessings=0;s.DomainEnvironment=E.None;s.PostReforge();
        check(s.EnvironmentCollection==EnvironmentRules.All && s.DomainEnvironment==E.Forest && EnvironmentRules.Count(s.EnvironmentBlessings)==5,"Reforge preserves all environment progress and selections");
        var remember=typeof(SoulItem).Assembly.GetType("ZenithReborn.HeroPrefixCompat");
        remember.GetMethod("Remember",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,[s]);
        var fresh=(SoulItem)new Item(s.Type).ModItem;fresh.ApplyPrefix(PrefixID.Legendary);
        check(fresh.EnvironmentCollection==s.EnvironmentCollection && fresh.EnvironmentBlessings==s.EnvironmentBlessings && fresh.DomainEnvironment==s.DomainEnvironment,"HERO rebuild preserves environment ledger and choices");

        (p,s,ep,n)=Setup();s.EnvironmentCollection=EnvironmentRules.All;s.SetEnvironmentDomain(E.Forest);ep.RefreshEnvironmentStats();
        ep.PostUpdate();for(int i=0;i<210;i++)ep.PostUpdate();
        check(ep.LeafReady && ep.ConsumableDodge(new Player.HurtInfo{Damage=50}) && !ep.LeafReady,"Forest actually matures a leaf and consumes it for one guaranteed dodge");
        check(!ep.ConsumableDodge(new Player.HurtInfo{Damage=50}),"An immature leaf cannot repeatedly ignore incoming damage");
        s.SetEnvironmentDomain(E.Ice);ep.RefreshEnvironmentStats();for(int i=0;i<300;i++)ep.PostUpdate();
        check(ep.IceArmor>0,"Ice mechanism produces a real absorbable armor resource");
        s.SetEnvironmentDomain(E.Underworld);ep.RefreshEnvironmentStats();ep.PostUpdate();int before=p.statLife;
        q=Blade(p,s,0);ep.Hit(q,n,default,5000,true);
        check(p.statLife>before && p.lifeSteal<80,"Underworld damage produces real healing and consumes life-steal budget");
        p.AddBuff(BuffID.MoonLeech,600);before=p.statLife;ep.Hit(q,n,default,5000,false);
        check(p.statLife==before,"Moon Leech blocks environmental life-steal");p.ClearBuff(BuffID.MoonLeech);
        s.SetEnvironmentDomain(E.Jungle);ep.RefreshEnvironmentStats();
        for(int i=0;i<720;i++){if(i%60==0)ep.Hit(q,n,default,1,false);ep.PostUpdate();}
        check(ep.NectarReady,"Twelve seconds of combat produces a ready honey resource");
        ep.OnHurt(new Player.HurtInfo{Damage=40});before=p.statLife;for(int i=0;i<240;i++)ep.PostUpdate();
        check(!ep.NectarReady && p.statLife>before,"Honey actually supplies gradual healing after a hit");

        foreach(var e in new[]{E.Underground,E.Caverns,E.Space,E.Snow,E.Desert,E.UndergroundDesert,E.Ocean,E.UndergroundJungle,E.JungleTemple,E.GlowingMushroom,E.Corruption,E.UndergroundCorruption,E.Crimson,E.UndergroundHallow,E.Dungeon})
        {
            (p,s,ep,n)=Setup();s.EnvironmentCollection=EnvironmentRules.All;s.SetEnvironmentDomain(e);ep.RefreshEnvironmentStats();ep.PostUpdate();
            for(int i=0;i<12;i++){q=Blade(p,s,i%4);ep.Hit(q,n,default,100,true);}
            var mark=n.GetGlobalNPC<EnvironmentTarget>().Find(0,s.SoulId);
            bool passive=e is E.Corruption or E.Crimson;
            check(passive?mark!=null && (mark.Penetration>0 || mark.Bleed>0):Main.projectile.Any(a=>a.active && a.ModProjectile is EnvironmentEffect effect && effect.Environment==e),"Independent environment mechanism executes: "+e);
        }
        (p,s,ep,n)=Setup();s.EnvironmentCollection=EnvironmentRules.All;s.SetEnvironmentDomain(E.UndergroundJungle);ep.RefreshEnvironmentStats();ep.PostUpdate();
        q=Blade(p,s,0);ep.Hit(q,n,default,100,true);int textDamage=0;n.GetGlobalNPC<EnvironmentTarget>().UpdateLifeRegen(n,ref textDamage);
        check(n.lifeRegen<0 && textDamage>0,"Owned poison buds use the engine NPC life-regeneration damage path");
        int children=Main.projectile.Count(a=>a.active && a.ModProjectile is EnvironmentEffect);
        ep.Hit(q,n,default,100,false);ep.Hit(q,n,default,100,false);
        check(Main.projectile.Count(a=>a.active && a.ModProjectile is EnvironmentEffect)==children,"Later frame hits cannot add extra primary proc counts");
        n.life=0;ep.DiscoverNaturalKill(n);check((s.EnvironmentCollection&~EnvironmentRules.All)==0,"Natural discovery cannot insert invalid environment bits");
        p.inventory[0]=new Item();ep.PostUpdateEquips();
        check(ep.Stat("Defense")==0 && !ep.Mechanism(E.Forest),"Changing weapons removes the soul's passives and mechanisms");
        (p,s,ep,n)=Setup();
        int initial=s.EnvironmentCollection;s.Hosts[0].TurnToAir();s.Hosts[10]=new Item(ItemID.AshWoodSword);s.SupportHosts[5]=new Item(ItemID.PearlwoodSword);
        s.RegisterEnvironments();check(s.EnvironmentCollection==initial,"Legacy recovery items cannot manufacture environment collection progress");
        check(!s.SetEnvironmentDomain(E.JungleTemple),"Uncollected environments cannot be selected as an active domain");
        p.ZoneLihzhardTemple=true;p.ZoneRockLayerHeight=true;n.life=0;ep.DiscoverNaturalKill(n);
        check(EnvironmentRules.Has(s.EnvironmentCollection,E.JungleTemple) && !EnvironmentRules.Has(ep.NaturalMask,E.Caverns),"Natural combat collects the temple; a named biome takes precedence over generic cavern depth");
        p.ZoneLihzhardTemple=false;ep.DiscoverNaturalKill(n);
        check(EnvironmentRules.Has(s.EnvironmentCollection,E.Caverns),"Generic caverns can be collected outside named biomes");
        s.SetEnvironmentDomain(E.JungleTemple);p.ZoneLihzhardTemple=false;p.ZoneRockLayerHeight=false;p.ZoneOverworldHeight=true;n.life=0;
        int beforeDiscovery=s.EnvironmentCollection;ep.DiscoverNaturalKill(n);
        check((s.EnvironmentCollection&~beforeDiscovery&EnvironmentRules.Bit(E.JungleTemple))==0,"An artificial domain cannot generate its own natural exploration discoveries");
        s.EnvironmentBlessings=EnvironmentRules.All;s.EnvironmentCollection=EnvironmentRules.Bit(E.Forest);s.DomainEnvironment=(E)255;s.NormalizeEnvironments();
        check(s.EnvironmentBlessings==0 && s.DomainEnvironment==E.None,"Invalid domain and over-cap selections are normalized safely");
        (p,s,ep,n)=Setup();s.EnvironmentCollection=EnvironmentRules.All;s.SetEnvironmentDomain(E.Underground);ep.RefreshEnvironmentStats();ep.PostUpdate();
        for(int i=0;i<6;i++)
        {
            q=Blade(p,s,i%4);q.Center=n.Center;Array.Clear(n.immune);Array.Clear(q.localNPCImmunity);q.Damage();q.Kill();
        }
        var echo=Main.projectile.FirstOrDefault(a=>a.active && a.ModProjectile is EnvironmentEffect effect && effect.Environment==E.Underground);
        check(echo!=null,"Actual engine projectile damage invokes the environment first-hit pipeline");
        if(echo!=null)
        {
            int beforeChild=n.life,beforeCount=Main.projectile.Count(a=>a.active && a.ModProjectile is EnvironmentEffect);
            echo.ModProjectile.AI();echo.Center=n.Center;Array.Clear(n.immune);echo.Damage();
            check(n.life<beforeChild,"Environment child projectile deals actual engine damage");
            check(Main.projectile.Count(a=>a.active && a.ModProjectile is EnvironmentEffect)==beforeCount,"Actual child damage cannot recursively spawn environment attacks");
            int once=n.life;Array.Clear(n.immune);Array.Clear(echo.localNPCImmunity);echo.Damage();
            check(n.life==once,"Burst root immunity remains effective even when ordinary NPC immunity is cleared");
        }
        foreach(var shot in Main.projectile)shot.active=false;foreach(var enemy in Main.npc)enemy.active=false;
    }
}
