using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class OreRewardCheck
{
    private static void AdvanceTime(uint frames)=>typeof(Main).GetField("_gameUpdateCount",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
        .SetValue(null,unchecked(Main.GameUpdateCount+frames));
    private static (Player,SoulItem,NPC) Setup(int count)
    {
        foreach(var p in Main.projectile)p.active=false;foreach(var n in Main.npc)n.active=false;
        ModContent.GetInstance<OreSystem>().OnWorldUnload();Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;
        var player=new Player{active=true,whoAmI=0,position=new(1000,1000)};Main.player[0]=player;player.ResetEffects();
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.Seals=SoulCurses.All;
        int[] types=HardOreTypes.Types.Concat(OreFamily.DefaultType.Take(8)).ToArray();
        for(int i=0;i<Math.Min(count,9);i++)soul.Hosts[i]=new Item(types[i]);
        if(count>=10){soul.Hosts[9]=new Item(ItemID.WoodenSword);soul.SupportHosts[0]=new Item(types[9]);}
        player.inventory[0]=soul.Item;soul.RefreshStats();
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.active=true;target.whoAmI=20;target.life=target.lifeMax=10000000;
        target.defense=0;target.width=target.height=100;target.knockBackResist=0;target.HideStrikeDamage=true;target.Center=player.Center+new Vector2(140,0);Main.npc[20]=target;
        return(player,soul,target);
    }
    private static Projectile Primary(Player p,SoulItem s,int host,NPC target,bool advance=true)
    {
        if(advance)AdvanceTime(10);
        int i=s.EmitBlade(p,s.Hosts[host],host,s.Active.Count());var blade=Main.projectile[i];blade.damage=100;blade.CritChance=0;
        ((SoulBlade)blade.ModProjectile).SpecialDamage=100;blade.ModProjectile.AI();blade.Center=target.Center;
        Array.Clear(target.immune);blade.Damage();blade.Kill();return blade;
    }
    private static OreFlyingSword[] Flights()=>Main.projectile.Where(p=>p.active && p.ModProjectile is OreFlyingSword).Select(p=>(OreFlyingSword)p.ModProjectile).ToArray();
    private static void Gather(Player p,SoulItem s,NPC n,int count,int host)
    {for(int i=0;i<count;i++)Primary(p,s,host,n);foreach(var f in Flights())for(int i=0;i<27;i++)f.AI();}
    private static void Hit(OreFlyingSword f,NPC n)
    {Array.Clear(f.Projectile.localNPCImmunity);Array.Clear(n.immune);f.Projectile.Center=n.Center;f.Projectile.Damage();}
    private static Projectile[] Children(HardOreKind kind)=>Main.projectile.Where(p=>p.active && p.ModProjectile is HardOreShot && (int)p.ai[1]==(int)kind).ToArray();
    internal static void Run(Action<bool,string> check)
    {
        using var healText=new MonoMod.RuntimeDetour.Hook(typeof(CombatText).GetMethod(nameof(CombatText.NewText),
            [typeof(Rectangle),typeof(Color),typeof(string),typeof(bool),typeof(bool)]),
            typeof(HardOreCheck).GetMethod("HeadlessText",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic));
        var(p,s,n)=Setup(1);
        int raw=Math.Max(1,(int)(p.GetWeaponDamage(s.Hosts[0])*s.Potency));
        check(s.BladeDamage(p,s.Hosts[0])==raw,"One material unlocks flight without a hidden primary damage bonus");
        Primary(p,s,0,n);check(Flights().Length==1,"One valid material actually creates its first flying sword");
        check(Flights()[0].Projectile.damage==150,"Real one-material flying projectile receives 150 percent of its actual 100-damage parent");
        var defenseMods=n.GetIncomingStrikeModifiers(DamageClass.Melee,1);var defenseBefore=defenseMods.DefenseEffectiveness;
        Flights()[0].ModifyHitNPC(n,ref defenseMods);
        check(defenseMods.DefenseEffectiveness.Equals(defenseBefore),"150 percent flying damage does not multiply enemy armor by 1.5");
        (p,s,n)=Setup(1);s.Hosts[0]=new Item(ItemID.WoodenSword);s.SupportHosts[0]=new Item(ItemID.CopperBroadsword);s.RefreshStats();
        Primary(p,s,0,n);check(Flights().Length==1 && Flights()[0].SetCount==1 && Flights()[0].Projectile.damage==150,
            "Real non-ore primary charges a 150 percent flying sword through one active shared ore support");
        (p,s,n)=Setup(1);s.Hosts[1]=new Item(ItemID.EnchantedSword);s.RefreshStats();
        Primary(p,s,1,n);
        check(Flights().Length==1 && Flights()[0].Projectile.GetGlobalProjectile<SoulSource>().SourceWeapon==ItemID.EnchantedSword,
            "A single active ore primary unlocks first-hit charge for a non-ore primary with its actual source preserved");
        (p,s,n)=Setup(1);s.Hosts[0]=new Item(ItemID.EnchantedSword);s.RefreshStats();Primary(p,s,0,n);
        check(Flights().Length==0,"Non-ore primary alone cannot create ore flying swords without an active unlock");
        (p,s,n)=Setup(1);s.Hosts[1]=new Item(ItemID.EnchantedSword);s.LegacyReleased=0;s.Seals&=~(SoulCurses.Brain|SoulCurses.Cultist);s.RefreshStats();
        Primary(p,s,1,n);check(Flights().Length==1,"Switching permits a second primary within the four-primary flight allowance");
        SwitchingLimit(check);
        (p,s,n)=Setup(6);
        for(int ordinal=0;ordinal<6;ordinal++)
        {
            Primary(p,s,0,n);
            foreach(var f in Flights())for(int frame=0;frame<15;frame++)f.AI();
        }
        var ring=Flights();var angles=ring.Select(f=>(f.Projectile.Center-n.Center).ToRotation()).Order().ToArray();
        check(ring.Length==6 && ring.All(f=>!f.Launched) && ring.Select(f=>f.Age).Distinct().Count()==6,
            "Staggered real first hits produce six differently aged blades in the shared prelaunch formation");
        check(Enumerable.Range(0,angles.Length).All(i=>Math.Abs((i==angles.Length-1?angles[0]+MathHelper.TwoPi:angles[i+1])-angles[i]-MathHelper.TwoPi/6)<.001f),
            "All six actual ring positions have equal angular gaps including the closing seam despite collection-age differences");
        foreach(int count in new[]{2,3,4,6,8,10})
        {
            (p,s,n)=Setup(count);float multiplier=count<4?1.05f:1.10f;
            check(OreFamily.Count(s)==count && s.BladeDamage(p,s.Hosts[0])==Math.Max(1,(int)(p.GetWeaponDamage(s.Hosts[0])*s.Potency*multiplier)),"Tier two/four applies five/ten percent to each ore primary without multiplying by count");
            check(s.BladeDamage(p,new Item(ItemID.Meowmere))==Math.Max(1,(int)(p.GetWeaponDamage(new Item(ItemID.Meowmere))*s.Potency)),"Ore primary bonus never changes unrelated sword damage");
        }
        (p,s,n)=Setup(6);Gather(p,s,n,6,0);var first=Flights()[0];var q=first.Projectile;
        Vector2 start=q.Center,dir=q.velocity.SafeNormalize(Vector2.UnitX);q.Center=start+dir*561;first.AI();
        check(first.AttackPass==1 && q.active && Vector2.Dot(q.velocity,dir)<0,"Six-tier flight turns around and stays damaging for a second pass");
        q.Center+=q.velocity.SafeNormalize(Vector2.UnitX)*561;first.AI();check(!q.active,"Second pass has a finite distance and does not loop forever");
        (p,s,n)=Setup(5);Gather(p,s,n,5,0);first=Flights()[0];q=first.Projectile;start=q.Center;dir=q.velocity.SafeNormalize(Vector2.UnitX);
        q.Center=start+dir*561;first.AI();check(!q.active,"Five materials cannot receive the six-tier return attack");

        (p,s,n)=Setup(8);Gather(p,s,n,8,6);
        var cobalt=Flights().Single(f=>f.Material==(int)OreMaterial.Cobalt);
        int before=Children(HardOreKind.Cobalt).Length;Hit(cobalt,n);
        check(Children(HardOreKind.Cobalt).Length==before+1 && !cobalt.TraitWindow,"Eight-tier flying Cobalt first hit immediately triggers its own existing proc");
        var child=Children(HardOreKind.Cobalt).Last();child.ModProjectile.AI();
        check(child.active && ((HardOreShot)child.ModProjectile).FromOreFlight && child.GetGlobalProjectile<SoulSource>().SourceWeapon==ItemID.CobaltSword,"Flying-sword child retains its own material identity and passes real source validation");
        n.defense=30;child.Center=n.Center;int armored=n.life;Array.Clear(n.immune);child.Damage();n.defense=0;
        check(armored-n.life>1,"Supplemental material effect scales enemy defense with its own small damage share");
        Hit(cobalt,n);check(Children(HardOreKind.Cobalt).Length==before+1,"Flying frame damage cannot repeatedly trigger its material effect");
        var flower=Flights().Single(f=>f.Material==(int)OreMaterial.Orichalcum);Hit(flower,n);
        check(Children(HardOreKind.Orichalcum).Length==3,"Eight-tier flying Orichalcum emits the existing three petals");
        var titanium=Flights().Single(f=>f.Material==(int)OreMaterial.Titanium);Hit(titanium,n);
        var fragment=Children(HardOreKind.Titanium).Single();fragment.ModProjectile.AI();
        check(fragment.active && Vector2.Distance(fragment.Center,titanium.Projectile.Center)<20,"Titanium fragment can orbit its actual flying-sword parent");
        p.statLifeMax=p.statLifeMax2=500;p.statLife=100;p.lifeSteal=100;
        var palladium=Flights().Single(f=>f.Material==(int)OreMaterial.Palladium);Hit(palladium,n);
        check(p.statLife==103 && p.lifeSteal==97,"Flying Palladium uses actual healing and spends the existing life-steal budget");
        Hit(palladium,n);check(p.statLife==103,"Palladium frame damage cannot repeatedly heal from the same flying sword");
        var mythril=Flights().Single(f=>f.Material==(int)OreMaterial.Mythril);Hit(mythril,n);
        var follow=Main.projectile[s.EmitBlade(p,s.Hosts[6],6,8)];
        var mods=n.GetIncomingStrikeModifiers(DamageClass.Melee,1);p.GetModPlayer<HardOrePlayer>().Modify(follow,n,ref mods);
        check(Math.Abs(mods.SourceDamage.ApplyTo(100)-112)<.01,"Flying Mythril's mark is consumed by another real source sword");
        var adamantite=Flights().Single(f=>f.Material==(int)OreMaterial.Adamantite);n.defense=80;
        mods=n.GetIncomingStrikeModifiers(DamageClass.Melee,1);adamantite.ModifyHitNPC(n,ref mods);
        check(Math.Abs(mods.SourceDamage.ApplyTo(100)-112)<.01,"Flying Adamantite applies its own defense-threshold damage effect");n.defense=0;
        int flights=Flights().Length;child.Center=n.Center;Array.Clear(n.immune);child.Damage();
        check(Flights().Length==flights,"Material children cannot recursively charge another flying-sword formation");
        (p,s,n)=Setup(7);Gather(p,s,n,7,6);cobalt=Flights().Single(f=>f.Material==(int)OreMaterial.Cobalt);Hit(cobalt,n);
        check(Children(HardOreKind.Cobalt).Length==0,"Seven materials do not leak the eight-tier flying material effect");

        (p,s,n)=Setup(10);Primary(p,s,9,n);
        check(Flights().Length==1 && Flights()[0].SetCount==10,"Ten-tier ore support makes a non-ore primary charge exactly one sword");
        Gather(p,s,n,9,9);check(Flights().Length==10,"Ten supported first hits complete one ten-material formation");
        check(Flights().All(f=>Math.Abs(f.Projectile.velocity.Length()-OreFlyingSword.LungeSpeed*1.5f*OreFlightMotion.InitialSpeed)<.01f) &&
            Flights().All(f=>f.Projectile.damage==OreFamily.PerSwordDamage(100,10)),"Ten-tier damage and native movement speed both gain fifty percent");
        first=Flights()[0];using(var bytes=new MemoryStream())
        {
            first.SendExtraAI(new BinaryWriter(bytes));bytes.Position=0;var clone=new Projectile();clone.SetDefaults(first.Projectile.type);
            var received=(OreFlyingSword)clone.ModProjectile;received.ReceiveExtraAI(new BinaryReader(bytes));
            check(received.SetCount==10 && received.AttackPass==first.AttackPass && received.PassAge==first.PassAge && received.FlightSpeed==first.FlightSpeed && bytes.Position==bytes.Length,"Tier, motion age and additional-pass state consume exactly the network payload");
        }
        s.SupportHosts[0].TurnToAir();Primary(p,s,9,n);
        check(Flights().Length==11 && Flights().Last().SetCount==9 && Math.Abs(Flights().Last().FlightSpeed-OreFlyingSword.LungeSpeed)<.01f,
            "Dropping below ten keeps unlocked non-ore primary charge but immediately removes the ten-tier speed bonus");
        Skyfall(check);NativeReturn(check);
        foreach(var shot in Main.projectile)shot.active=false;foreach(var npc in Main.npc)npc.active=false;
    }
    private static void SwitchingLimit(Action<bool,string> check)
    {
        var(p,s,n)=Setup(10);s.LegacyReleased=0;s.Seals&=~(SoulCurses.Brain|SoulCurses.Cultist);
        int capacity=s.SupportCapacity;
        check(s.OreFlightMains.Count()==4 && OreFamily.Count(s)==4 && s.TraitMains.Count()==1,
            "Switching caps ore primaries at four while ordinary traits remain first-primary only");
        for(int i=0;i<10;i++)Primary(p,s,i,n,false);
        check(Flights().Length==4 && Flights().All(f=>f.SetCount==4),
            "Ten distinct primaries hitting in one game frame produce only four cursed flying swords without shared cooldown");
        check(s.SupportCapacity==capacity && s.Active.Count()==10,
            "Flight restriction leaves structural supports and all direct-damage primaries active");
        s.Seals|=SoulCurses.Brain;
        check(s.OreFlightMains.Count()==10 && OreFamily.Count(s)==10,
            "First Switching boss fully restores ore counting and trigger eligibility without waiting for blessing");
        Primary(p,s,9,n,false);check(Flights().Length==5,"Released Switching allows a primary beyond the first four to charge");
        (p,s,n)=Setup(10);s.LegacyReleased=0;s.Seals&=~(SoulCurses.Brain|SoulCurses.Cultist);
        s.Hosts[4]=s.Hosts[0].Clone();
        Primary(p,s,4,n,false);check(Flights().Length==0,
            "A fifth primary identical to the first cannot bypass the limit through item type matching");
        var emitted=Primary(p,s,0,n,false);
        check(Flights().Length==1 && Flights()[0].Projectile.GetGlobalProjectile<SoulSource>().PrimaryOrdinal==0,
            "Allowed primary passes its actual source position to the flying sword");
        var source=emitted.GetGlobalProjectile<SoulSource>();
        using(var stream=new MemoryStream())
        {
            source.SendExtraAI(emitted,null,new BinaryWriter(stream));stream.Position=0;
            var remote=new SoulSource();remote.ReceiveExtraAI(emitted,null,new BinaryReader(stream));
            check(remote.PrimaryOrdinal==0 && remote.SourceWeapon==source.SourceWeapon && stream.Position==stream.Length,
                "Projectile network payload preserves source ordinal and consumes the entire message");
        }
        s.Hosts[0].TurnToAir();Primary(p,s,4,n,false);
        check(Flights().Length==2,"Empty normal slots do not consume one of the four effective primary positions");
        check(SoulCurses.Description(s,SoulCurse.Switching).Contains("前4把") &&
            SoulTraits.All(s).Single(t=>t.Key=="ore-wave").Requirement.Contains("前4把"),
            "Curse and codex both explain the four-primary exception");
    }
    private static void Skyfall(Action<bool,string> check)
    {
        var(p,s,n)=Setup(1);s.OreCollectionMask=OreFamily.AllMaterials;
        Primary(p,s,0,n);var giants=Main.projectile.Where(q=>q.active && q.ModProjectile is OreSkyfall).ToArray();
        check(giants.Length==1,"Cumulative full collection adds a real giant projectile to a complete volley");
        var giant=giants[0];var mod=(OreSkyfall)giant.ModProjectile;
        check(giant.damage==(int)(100*OreSkyfall.DamageMultiplier(s)) && mod.CanDamage()==false,"Giant has explicit large source-scaled damage and a harmless warning phase");
        for(int i=0;i<OreSkyfall.WarningFrames;i++)mod.AI();check(mod.CanDamage()==false,"Warning lasts thirty game frames");
        mod.AI();giant.Center=n.Center;int before=n.life;Array.Clear(n.immune);giant.Damage();
        check(n.life<before && giant.active,"Falling giant deals real engine damage without disappearing on first contact");
        before=n.life;Array.Clear(n.immune);Array.Clear(giant.localNPCImmunity);giant.Damage();
        check(n.life==before,"Giant cannot repeatedly multiply a single target's damage each frame");
        Primary(p,s,0,n);check(Main.projectile.Count(q=>q.active && q.ModProjectile is OreSkyfall)==1,"Full collection giant honors the shared five-second cooldown");
        AdvanceTime(OreSystem.SkyfallCooldown);Primary(p,s,0,n);
        check(Main.projectile.Count(q=>q.active && q.ModProjectile is OreSkyfall)==2,"A later complete volley summons a giant after cooldown");
        foreach(var q in Main.projectile)q.active=false;ModContent.GetInstance<OreSystem>().OnWorldUnload();
        (p,s,n)=Setup(2);s.OreCollectionMask=OreFamily.AllMaterials;Primary(p,s,0,n);
        AdvanceTime(OreSystem.FormationTimeout);ModContent.GetInstance<OreSystem>().PostUpdateEverything();
        check(!Main.projectile.Any(q=>q.active && q.ModProjectile is OreSkyfall),"Interrupted partial volley cannot trigger the completion-only giant");
    }
    private static void NativeReturn(Action<bool,string> check)
    {
        var(p,s,n)=Setup(6);Gather(p,s,n,6,0);OreTierProbe.Passes.Clear();
        Vector2 center=n.Center;n.width=n.height=8;n.Center=center;
        var moving=Flights()[0];var speeds=new List<float>();int turnaround=-1;
        for(int tick=0;tick<150;tick++)
        {
            AdvanceTime(1);p.GetModPlayer<HardOrePlayer>().PostUpdate();Array.Clear(n.immune);
            foreach(var q in Main.projectile.Where(q=>q.active).ToArray())q.Update(q.whoAmI);
            if(moving.Projectile.active && moving.AttackPass==0 && speeds.Count<8)speeds.Add(moving.Projectile.velocity.Length());
            if(moving.AttackPass==1 && turnaround<0)turnaround=tick+1;
        }
        check(OreTierProbe.Passes.Contains(0) && OreTierProbe.Passes.Contains(1),"Accelerating native movement and swept collision hit an eight-pixel target on outgoing and returning passes");
        check(speeds.Count==8 && speeds[0]<speeds[2] && speeds[2]<speeds[5] && speeds[^1]>speeds[0]*4,
            "Actual native projectile updates accelerate through the opening frames instead of gliding at constant speed");
        check(turnaround>0 && turnaround<32,"Actual accelerated outgoing pass completes substantially faster than the old 49-tick glide");
        string report=Environment.GetEnvironmentVariable("ZENITH_CHECK_REPORT");
        if(!string.IsNullOrEmpty(report))File.WriteAllText(Path.Combine(Path.GetDirectoryName(report),"ore-motion-0.3.49.json"),
            System.Text.Json.JsonSerializer.Serialize(new{actualNativeProjectileUpdates=true,firstEightSpeeds=speeds,
                outgoingFrames=turnaround,thinTargetPixels=8,outgoingAndReturningHit=OreTierProbe.Passes.Contains(0)&&OreTierProbe.Passes.Contains(1),
                realWorldTested=false,multiplayerTested=false},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        (p,s,n)=Setup(1);s.OreCollectionMask=OreFamily.AllMaterials;Primary(p,s,0,n);OreTierProbe.GiantDamages.Clear();
        var giant=Main.projectile.Single(q=>q.active && q.ModProjectile is OreSkyfall);
        for(int tick=0;tick<100 && giant.active;tick++){Array.Clear(n.immune);giant.Update(giant.whoAmI);}
        check(OreTierProbe.GiantDamages.Count==1 && OreTierProbe.GiantDamages[0]>1000,"Actual giant warning, falling movement and native collision produce one large hit");
    }
}

public class OreTierProbe : GlobalNPC
{
    internal static readonly List<int> Passes=[];
    internal static readonly List<int> GiantDamages=[];
    public override void OnHitByProjectile(NPC npc,Projectile projectile,NPC.HitInfo hit,int damageDone)
    {if(projectile.ModProjectile is OreFlyingSword f)Passes.Add(f.AttackPass);if(projectile.ModProjectile is OreSkyfall)GiantDamages.Add(damageDone);}
}
