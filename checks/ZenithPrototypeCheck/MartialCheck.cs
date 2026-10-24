using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class MartialCheck
{
    private static (Player,SoulItem,MartialPlayer,NPC) Setup()
    {
        foreach(var q in Main.projectile)q.active=false;foreach(var enemy in Main.npc)enemy.active=false;
        Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.screenPosition=Vector2.Zero;
        ModContent.GetInstance<MartialConfig>().EnableAttackPrototype=true;
        var p=new Player{active=true,whoAmI=0,position=new(1000,1000)};Main.player[0]=p;p.ResetEffects();
        var s=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        s.Attached=true;s.Kills=SoulProgress.All;s.Cores=63;s.Seals=SoulCurses.All;s.Hosts[0]=new Item(ItemID.Katana);s.Hosts[1]=new Item(ItemID.Muramasa);
        p.inventory[0]=s.Item;s.RefreshStats();p.statLife=p.statLifeMax2=400;
        var n=new NPC();n.SetDefaults(NPCID.Zombie);n.active=true;n.whoAmI=20;n.life=n.lifeMax=10000000;n.defense=0;
        n.width=n.height=180;n.knockBackResist=0;n.HideStrikeDamage=true;n.Center=p.Center+new Vector2(220,0);Main.npc[20]=n;
        Main.mouseX=(int)n.Center.X;Main.mouseY=(int)n.Center.Y;
        return(p,s,p.GetModPlayer<MartialPlayer>(),n);
    }
    private static MartialBlade[] Blades()=>Main.projectile.Where(q=>q.active && q.ModProjectile is MartialBlade).Select(q=>(MartialBlade)q.ModProjectile).ToArray();
    private static MartialGiant Giant()=>Main.projectile.First(q=>q.active && q.ModProjectile is MartialGiant).ModProjectile as MartialGiant;
    private static void Hit(Projectile q,NPC n)
    {Array.Clear(q.localNPCImmunity);Array.Clear(n.immune);q.Damage();}
    internal static void Run(Action<bool,string> check)
    {
        using var textHook=new MonoMod.RuntimeDetour.Hook(typeof(CombatText).GetMethod(nameof(CombatText.NewText),
            [typeof(Rectangle),typeof(Color),typeof(string),typeof(bool),typeof(bool)]),
            typeof(HardOreCheck).GetMethod("HeadlessText",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic));
        var(p,s,state,n)=Setup();
        var keys=Main.keyState;var oldKeys=Main.oldKeyState;
        try
        {
            Main.oldKeyState=new KeyboardState();Main.keyState=new KeyboardState(Keys.G);
            check(MartialInput.Pressed(null,Keys.G) && !MartialInput.Pressed(null,Keys.V),"Empty legacy key profiles can activate the G fallback without triggering V");
            Main.oldKeyState=Main.keyState;check(!MartialInput.Pressed(null,Keys.G),"Holding the fallback key does not repeatedly toggle the form");
            check(MartialInput.Name(null,"V")=="V","HUD shows a usable default key for an unassigned profile");
        }
        finally{Main.keyState=keys;Main.oldKeyState=oldKeys;}
        check(!state.TryActivate() && s.MartialCharge==0,"武技 empty charge rejects manual activation");
        int index=s.EmitBlade(p,s.Hosts[0],0,2);var basic=Main.projectile[index];basic.ModProjectile.AI();basic.Center=n.Center;
        Hit(basic,n);check(s.MartialCharge==30,"Actual native soul-blade damage charges 武技");
        Hit(basic,n);check(s.MartialCharge==30,"Same emitted blade frame damage cannot charge again");
        var fake=new Projectile();fake.SetDefaults(ModContent.ProjectileType<MartialWave>());fake.owner=0;
        fake.GetGlobalProjectile<SoulSource>().SoulId=s.SoulId;state.PrimaryHit(fake,n,100);
        check(s.MartialCharge==30,"Derived wave cannot recursively generate 武技 charge");
        s.MartialCharge=300;s.MartialTier=3;
        for(int i=0;i<180;i++)state.PostUpdate();check(s.MartialCharge==300 && state.Mode==MartialMode.Normal,"Full charge is retained without automatic activation");
        s.YujianCharge=789;s.EnvironmentCollection=EnvironmentRules.All;
        var tag=new TagCompound();s.SaveData(tag);var restored=(SoulItem)new Item(s.Type).ModItem;restored.LoadData(tag);
        check(restored.MartialCharge==300 && restored.MartialTier==3 && restored.YujianCharge==789 && restored.EnvironmentCollection==EnvironmentRules.All,"New save retains martial meter, selection, 御剑 and environments");
        check(((SoulItem)s.Item.Clone().ModItem).MartialCharge==300,"Clone retains unspent 武技 meter");
        using(var stream=new MemoryStream())
        {s.NetSend(new BinaryWriter(stream));stream.Position=0;restored.NetReceive(new BinaryReader(stream));
            check(restored.MartialCharge==300 && restored.MartialTier==3 && stream.Position==stream.Length,"Network retains martial fields with exact payload consumption");}
        s.PreReforge();s.MartialCharge=0;s.MartialTier=1;s.PostReforge();check(s.MartialCharge==300 && s.MartialTier==3,"Reforge retains charge and tier");
        var old=new TagCompound();s.SaveData(old);old.Remove("martialCharge");old.Remove("martialTier");restored.LoadData(old);
        check(restored.MartialCharge==0 && restored.MartialTier==1 && restored.YujianCharge==789,"Older item records safely start martial charge at zero without losing other fields");

        (p,s,state,n)=Setup();s.MartialCharge=300;s.MartialTier=1;
        check(state.TryActivate() && state.SuppressNative(s) && s.MartialCharge==200,"Manual tier one spends only one tier and suppresses native shooting");
        s.UseAnimation(p);var blades=Blades();check(blades.Length==3 && blades.All(b=>b.State==0 && b.CanDamage()==false),"Attack summons three harmless rotating formation swords");
        for(int i=0;i<40;i++)foreach(var b in blades)b.AI();
        check(blades.All(b=>b.State==1),"Formation swords rotate, then thrust");
        foreach(var b in blades){b.Projectile.Center=n.Center;Hit(b.Projectile,n);}
        check(blades.All(b=>b.State==2 && b.Projectile.active && b.CanDamage()==false),"Real hits embed the visible swords instead of destroying them");
        int regenDamage=0;n.lifeRegen=0;n.GetGlobalNPC<MartialTarget>().UpdateLifeRegen(n,ref regenDamage);
        check(n.lifeRegen<0 && regenDamage>0,"Embedded swords apply a real continuous debuff through NPC life regeneration");
        Vector2 previous=blades[0].Projectile.Center;n.position+=new Vector2(17,-8);blades[0].AI();
        check(Vector2.Distance(blades[0].Projectile.Center-previous,new Vector2(17,-8))<.1f,"Embedded sword follows the NPC rather than remaining at the old hit point");
        int charge=s.MartialCharge;state.Cancel();check(!state.SuppressNative(s) && s.MartialCharge==charge && !Blades().Any(),"Manual cancellation clears formation without overwriting the remaining meter");

        (p,s,state,n)=Setup();s.MartialCharge=300;s.MartialTier=2;state.TryActivate();s.UseAnimation(p);
        blades=Blades();var giant=Giant();
        check(blades.Length==4 && state.Mode==MartialMode.Giant && giant.Tier==2,"Tier two emits four swords using real weapon snapshots then summons the giant");
        for(int i=0;i<42;i++)foreach(var b in blades)b.AI();
        foreach(var b in blades){b.Projectile.Center=n.Center;Hit(b.Projectile,n);}
        for(int i=0;i<MartialMotion.ThunderImpact-1;i++)giant.AI();
        check(!giant.SwordVisible && giant.CanDamage()==false,"雷光触及角色前巨剑不出现且没有伤害");
        giant.AI();
        check(giant.SwordVisible && giant.Projectile.scale==1 && giant.Phase==0,"落雷当帧巨剑立刻完整显现");
        float chargeStart=giant.Projectile.rotation;
        for(int i=0;i<MartialMotion.ChargeFrames-1;i++)giant.AI();
        check(giant.Phase==0 && giant.Projectile.rotation<chargeStart-.6f,"巨剑缓慢后压蓄力，不会提前重斩");
        for(int i=0;i<8;i++)giant.AI();
        check(giant.Phase==4 && blades.All(b=>b.State==3 && b.Projectile.active),"Giant heavy slash actually flings all embedded swords");
        n.Center=p.Center+giant.Projectile.rotation.ToRotationVector2()*150;int life=n.life;
        Hit(giant.Projectile,n);check(n.life<life-giant.Projectile.damage*4,"Giant heavy slash deals its high multiplier in the actual damage pipeline");
        life=n.life;Hit(giant.Projectile,n);check(n.life==life,"Same heavy slash cannot repeat on the same root each frame");
        var returning=blades[0];
        float initialSpin=returning.Projectile.rotation;
        for(int i=0;i<MartialMotion.FlingSpinFrames;i++)returning.AI();
        check(returning.State==4 && returning.CanDamage()!=false &&
            MathF.Abs(returning.Projectile.rotation-initialSpin)>MathHelper.TwoPi*2.9f,
            "击飞剑确实旋转三圈后进入有伤害的返航阶段");
        n.Center=returning.Projectile.Center;life=n.life;Hit(returning.Projectile,n);
        check(n.life<life,"返航阶段经过敌人时进入真实伤害管线");
        for(int i=0;i<100 && returning.Projectile.active;i++)returning.AI();
        check(!returning.Projectile.active && Vector2.Distance(returning.Projectile.Center,p.Center)<60,
            "返航剑移动回角色附近收回，不在远处定时消失");
        for(int i=0;i<40 && giant.Projectile.active;i++)giant.AI();
        check(state.Mode==MartialMode.Normal,"Heavy-slash completion restores native attack mode");

        (p,s,state,n)=Setup();s.MartialCharge=300;s.MartialTier=3;state.TryActivate();s.UseAnimation(p);giant=Giant();
        for(int i=0;i<46;i++)foreach(var b in Blades())b.AI();
        foreach(var b in Blades()){b.Projectile.Center=n.Center;Hit(b.Projectile,n);}
        for(int i=0;i<MartialTuning.GiantReadyFrames;i++)giant.AI();
        check(giant.ReadyForCut && !giant.TimeStopped,"Tier-three giant waits for the player's attack before time stop");
        check(giant.StartTimeCut(n.Center) && giant.TimeStopped,"Attack starts time stop against a real nearby enemy");
        check(!p.GetModPlayer<MartialDashPlayer>().IsLightDash,"化光只发生在实际突进期间，蓄势人物仍可见");
        n.velocity=new Vector2(3,-2);var freeze=n.GetGlobalNPC<MartialFreezeNPC>();
        check(!freeze.PreAI(n) && n.velocity==Vector2.Zero,"Time stop freezes hostile NPC AI and movement");
        var hostile=new Projectile();hostile.SetDefaults(ProjectileID.DeathLaser);hostile.Center=n.Center;hostile.hostile=true;hostile.velocity=new(5,1);
        var projFreeze=hostile.GetGlobalProjectile<MartialFreezeProjectile>();
        check(!projFreeze.PreAI(hostile) && !projFreeze.ShouldUpdatePosition(hostile) && !projFreeze.CanHitPlayer(hostile,p),"Time stop freezes hostile projectile AI, movement and contact damage");
        var friendly=new Projectile();friendly.SetDefaults(ProjectileID.WoodenArrowFriendly);friendly.Center=n.Center;friendly.hostile=false;
        check(friendly.GetGlobalProjectile<MartialFreezeProjectile>().PreAI(friendly),"Friendly attacks keep moving during time stop");
        Vector2 playerStart=p.Center;for(int i=0;i<11;i++)giant.AI();
        check(p.GetModPlayer<MartialDashPlayer>().IsLightDash && p.Center!=playerStart,"实际突进时人物进入流光绘制状态");
        for(int i=0;i<11;i++)giant.AI();
        check(giant.Phase==3 && p.Center!=playerStart && Vector2.Distance(p.Center,n.Center)<Vector2.Distance(playerStart,n.Center),"Player body really dashes toward the enemy before the embedded giant hold");
        check(!p.GetModPlayer<MartialDashPlayer>().IsLightDash && giant.DashPath.Count>=2,"突进结束恢复人物，留下实际移动路径供流光绘制");
        Vector2 holdPoint=p.Center;p.position+=new Vector2(5,40);giant.AI();
        check(Vector2.Distance(p.Center,holdPoint)<.01f,"Airborne hold anchors the player instead of letting gravity pull the giant out of the target");
        int firstLife=n.life;giant.AI();Hit(giant.Projectile,n);
        for(int i=0;i<12;i++)giant.AI();Hit(giant.Projectile,n);
        check(n.life<firstLife-giant.Projectile.damage && giant.TimeStopped,"Giant sword deals repeated actual hold damage inside the stopped target");
        while(giant.Phase==3)giant.AI();
        check(!giant.TimeStopped && freeze.PreAI(n) && n.velocity==new Vector2(3,-2),"Two-second hold ends and restores the exact pre-stop NPC velocity");
        check(projFreeze.PreAI(hostile) && hostile.velocity==new Vector2(5,1) && projFreeze.ShouldUpdatePosition(hostile),"Enemy projectile resumes its saved trajectory after time stop");
        for(int i=0;i<7;i++)giant.AI();
        var beam=Main.projectile.FirstOrDefault(q=>q.active && q.ModProjectile is MartialWave);
        check(beam!=null && beam.damage==giant.Projectile.damage*14,"Final slash launches an independently damaging giant sword wave");
        beam.Center=n.Center;life=n.life;Hit(beam,n);
        check(n.life<life && n.HasBuff(ModContent.BuffType<MartialScar>()),"Sword wave causes actual damage and leaves the finishing sword scar");
        regenDamage=0;n.lifeRegen=0;n.GetGlobalNPC<MartialTarget>().UpdateLifeRegen(n,ref regenDamage);
        check(n.lifeRegen<0 && regenDamage>0,"Finishing sword scar has a real damage effect");
        (p,s,state,n)=Setup();s.MartialCharge=300;s.MartialTier=3;state.TryActivate();s.UseAnimation(p);giant=Giant();
        for(int i=0;i<46;i++)foreach(var b in Blades())b.AI();
        foreach(var b in Blades()){b.Projectile.Center=n.Center;Hit(b.Projectile,n);}
        for(int i=0;i<MartialTuning.GiantReadyFrames;i++)giant.AI();giant.StartTimeCut(n.Center);state.Cancel();
        check(!MartialTime.Frozen(n.Center) && state.Mode==MartialMode.Normal,"Cancellation removes time stop immediately, including an unfinished dash");
        ModContent.GetInstance<MartialConfig>().EnableAttackPrototype=false;
        check(!state.TryActivate(),"Prototype can be disabled without changing the native sword system");
        ModContent.GetInstance<MartialConfig>().EnableAttackPrototype=true;
    }
}
