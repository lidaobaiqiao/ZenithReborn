using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class DamageBalanceCheck
{
    internal static bool Recording;
    internal static long Damage, PrimaryDamage;
    internal static int Hits, PrimaryHits;
    internal static void Run(Action<bool,string> check)
    {
        var reports=new List<object>();
        Lighting.Initialize();
        foreach(int defense in new[]{0,50,150})
        foreach(int prefix in new[]{0,(int)PrefixID.Legendary})
        {
            object Trial(bool soulMode)
            {
                foreach(var p in Main.projectile) p.active=false;
                foreach(var n in Main.npc) n.active=false;
                Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.rand=new UnifiedRandom(7241);
                Main.GameViewMatrix??=new Terraria.Graphics.SpriteViewMatrix(null);
                var player=new Player{active=true,whoAmI=0,position=new Vector2(1000,1000)};
                Main.player[0]=player;player.ResetEffects();
                Item weapon=new(soulMode?ModContent.ItemType<SoulItem>():ItemID.Zenith);
                if(weapon.ModItem is SoulItem soul)
                {
                    soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;
                    for(int i=0;i<10;i++) soul.Hosts[i]=new Item(SoulProgress.ZenithWeapons[i]);
                    soul.RefreshStats();
                }
                if(prefix!=0) weapon.Prefix(prefix);
                if(weapon.ModItem is SoulItem refreshed) refreshed.RefreshStats();
                player.inventory[0]=weapon;player.selectedItem=0;player.controlUseItem=true;player.releaseUseItem=true;
                var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;
                target.width=80;target.height=120;target.Center=player.Center+new Vector2(240,0);
                target.life=target.lifeMax=10000000;target.defense=defense;target.knockBackResist=0;target.HideStrikeDamage=true;
                Main.npc[20]=target;Main.screenPosition=Vector2.Zero;
                Main.mouseX=(int)target.Center.X;Main.mouseY=(int)target.Center.Y;
                Main.screenWidth=1920;Main.screenHeight=1080;
                Damage=PrimaryDamage=0;Hits=PrimaryHits=0;Recording=true;
                for(int frame=0;frame<1200;frame++)
                {
                    for(int owner=0;owner<target.immune.Length;owner++) if(target.immune[owner]>0) target.immune[owner]--;
                    player.ItemCheck();
                    for(int i=0;i<Main.maxProjectiles;i++) if(Main.projectile[i].active) Main.projectile[i].Update(i);
                    player.GetModPlayer<SoulPlayer>().PostUpdate();
                }
                Recording=false;
                check(Damage>0 && PrimaryDamage>0,"Actual engine combat records damage: "+(soulMode?"Soul":"Vanilla"));
                return new{weapon=soulMode?"soul":"vanilla",defense,prefix,seconds=20,damage=Damage,dps=Damage/20d,
                    primaryDamage=PrimaryDamage,primaryHits=PrimaryHits,hits=Hits,panel=weapon.damage,animation=weapon.useAnimation,useTime=weapon.useTime};
            }
            reports.Add(Trial(false));reports.Add(Trial(true));
        }
        string report=Environment.GetEnvironmentVariable("ZENITH_CHECK_REPORT");
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(report),"damage-comparison.json"),
            JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
        foreach(var p in Main.projectile) p.active=false;
        foreach(var n in Main.npc) n.active=false;
        VerifyCombatState(check);
    }

    private static void VerifyCombatState(Action<bool,string> check)
    {
        var player=new Player{active=true,whoAmI=0};Main.player[0]=player;player.ResetEffects();
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.Hosts[0]=new Item(ItemID.CopperShortsword);soul.RefreshStats();
        player.inventory[0]=soul.Item;
        int early=soul.BladeDamage(player,soul.Hosts[0]);
        check(early==Math.Max(1,(int)(player.GetWeaponDamage(soul.Hosts[0])*.65f)),"Initial stage retains 65-percent host damage");
        soul.Kills=SoulProgress.All;soul.Cores=63;soul.RefreshStats();
        check(!soul.Awakened,"Progress alone does not bypass the ten-material awakening requirement");
        soul.AwakeningMemory=true;soul.RefreshStats();
        int weakBefore=soul.BladeDamage(player,soul.Hosts[0]);
        check(weakBefore<new Item(ItemID.Zenith).damage,"Awakened copper sword has no shared Zenith damage floor");
        soul.Hosts[1]=new Item(ItemID.Meowmere);soul.Hosts[1].Prefix(PrefixID.Legendary);soul.RefreshStats();
        check(soul.BladeDamage(player,soul.Hosts[0])==weakBefore,"Adding a strong host neither boosts nor dilutes the weak host");
        int highBefore=soul.BladeDamage(player,soul.Hosts[1]);
        soul.Hosts[1].damage*=2;soul.Hosts[1].useTime=soul.Hosts[1].useAnimation=35;soul.RefreshStats();
        check(soul.BladeDamage(player,soul.Hosts[1])>highBefore && soul.BladeDamage(player,soul.Hosts[0])==weakBefore,
            "Increasing one host damage only increases its own blade damage");
        soul.Item.Prefix(PrefixID.Legendary);soul.RefreshStats();
        check(soul.BladeDamage(player,soul.Hosts[0])==weakBefore,"Soul panel/prefix cannot replace the individual host damage");
        int index=soul.EmitBlade(player,soul.Hosts[0],0,1);
        var blade=(SoulBlade)Main.projectile[index].ModProjectile;
        int other=soul.EmitBlade(player,soul.Hosts[1],1,10);
        var strong=(SoulBlade)Main.projectile[other].ModProjectile;
        check(blade.SpecialDamage==weakBefore && blade.Projectile.damage==weakBefore && strong.SpecialDamage>blade.SpecialDamage,
            "Actual emitted blades use their own damage with no slot-count division");
        check(blade.Projectile.CritChance==player.GetWeaponCrit(soul.Hosts[0]) && strong.Projectile.CritChance==player.GetWeaponCrit(soul.Hosts[1]),
            "Actual emitted crit chances follow each host including its prefix");
        check(Math.Abs(strong.Projectile.knockBack-player.GetWeaponKnockback(soul.Hosts[1]))<.001,
            "High host keeps its own knockback rather than the shared template");
        check(strong.Projectile.ai[1]!=blade.Projectile.ai[1],"Each blade flight duration follows its host attack speed");
        using(var data=new System.IO.MemoryStream())
        {
            blade.SendExtraAI(new System.IO.BinaryWriter(data));data.Position=0;
            var remote=new Projectile();remote.SetDefaults(ModContent.ProjectileType<SoulBlade>());
            ((SoulBlade)remote.ModProjectile).ReceiveExtraAI(new System.IO.BinaryReader(data));
            check(data.Position==data.Length && ((SoulBlade)remote.ModProjectile).Reborn && remote.localNPCHitCooldown==8,
                "Reborn state and hit interval survive projectile network serialization");
        }
        blade.ConfigureReborn(false);
        check(!blade.Reborn && blade.Projectile.localNPCHitCooldown==8,"Non-reborn blades also allow repeated hits");
        VerifyRepeatedHits(player,blade,strong,check);
        VerifySpecials(player,soul,check);
        foreach(var p in Main.projectile) p.active=false;
    }

    private static void VerifyRepeatedHits(Player player,SoulBlade weak,SoulBlade strong,Action<bool,string> check)
    {
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;
        target.life=target.lifeMax=10000000;target.defense=0;target.knockBackResist=0;target.HideStrikeDamage=true;
        target.Center=player.Center+new Vector2(120,0);Main.npc[20]=target;
        foreach(var blade in new[]{weak,strong})
        {
            blade.AI();blade.Projectile.Center=target.Center;blade.Projectile.CritChance=0;
        }
        Recording=true;Damage=0;Hits=0;
        weak.Projectile.Damage();int once=Hits;long weakDamage=Damage;
        weak.Projectile.Damage();
        check(once==1 && Hits==once,"Engine enforces an interval between hits of the same blade");
        strong.Projectile.Damage();
        check(Hits==2 && Damage-weakDamage>weakDamage,"Another blade hits independently with its own higher damage");
        for(int frame=0;frame<8;frame++) if(weak.Projectile.localNPCImmunity[20]>0) weak.Projectile.localNPCImmunity[20]--;
        weak.Projectile.Damage();
        check(Hits==3,"Engine permits the original weak blade to hit the same target again after cooldown");
        Recording=false;target.active=false;
    }

    private static void VerifySpecials(Player player,SoulItem soul,Action<bool,string> check)
    {
        foreach(var p in Main.projectile) p.active=false;
        var method=typeof(SoulBlade).GetMethod("ReleaseWeaponSpecial",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        SoulBlade Make(int type)
        {
            var host=new Item(type);
            return (SoulBlade)Main.projectile[soul.EmitBlade(player,host,0,10)].ModProjectile;
        }
        var enchanted=Make(ItemID.EnchantedSword);var star=Make(ItemID.Starfury);
        int enchantedType=new Item(ItemID.EnchantedSword).shoot,starType=new Item(ItemID.Starfury).shoot;
        int Count(int type)=>Main.projectile.Count(p=>p.active && p.type==type);
        method.Invoke(enchanted,[player,ItemID.EnchantedSword]);method.Invoke(star,[player,ItemID.Starfury]);
        check(Count(enchantedType)==1 && Count(starType)==1,"Enchanted sword and Starfury both trigger on the same frame");
        var beam=Main.projectile.First(p=>p.active && p.type==enchantedType);
        var fallingStar=Main.projectile.First(p=>p.active && p.type==starType);
        check(beam.damage==Math.Max(1,(int)(enchanted.SpecialDamage*.3f)) && fallingStar.damage==Math.Max(1,(int)(star.SpecialDamage*.3f)),
            "Each special projectile derives its damage from its own sword");
        method.Invoke(enchanted,[player,ItemID.EnchantedSword]);method.Invoke(star,[player,ItemID.Starfury]);
        check(Count(enchantedType)==1 && Count(starType)==1,"Independent cooldowns still prevent repeated same-sword procs");
        for(int frame=0;frame<45;frame++) player.GetModPlayer<SoulPlayer>().PostUpdate();
        method.Invoke(enchanted,[player,ItemID.EnchantedSword]);method.Invoke(star,[player,ItemID.Starfury]);
        check(Count(enchantedType)==2 && Count(starType)==2,"Both sword-specific cooldowns expire after 45 frames");
    }
}

public class DamageBalanceProbe : GlobalProjectile
{
    public override void OnHitNPC(Projectile projectile,NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(!DamageBalanceCheck.Recording || target.whoAmI!=20) return;
        DamageBalanceCheck.Damage+=damageDone;DamageBalanceCheck.Hits++;
        if(projectile.type==ProjectileID.FinalFractal || projectile.ModProjectile is SoulBlade)
        { DamageBalanceCheck.PrimaryDamage+=damageDone;DamageBalanceCheck.PrimaryHits++; }
    }
}
