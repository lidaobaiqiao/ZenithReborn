using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class OreStackCheck
{
    private static (Player,SoulItem,NPC) Setup(params int[] types)
    {
        foreach(var q in Main.projectile)q.active=false;foreach(var n in Main.npc)n.active=false;
        ModContent.GetInstance<OreSystem>().OnWorldUnload();
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        var player=new Player{whoAmI=0,active=true,position=new(1000,1000)};Main.player[0]=player;player.ResetEffects();
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.LegacyReleased=63;soul.Kills=SoulProgress.All;soul.Cores=63;
        for(int i=0;i<types.Length;i++)soul.Hosts[i]=new Item(types[i]);
        soul.RefreshStats();player.inventory[0]=soul.Item;
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.active=true;target.whoAmI=20;
        target.width=target.height=12;target.life=target.lifeMax=100000;target.defense=0;
        target.HideStrikeDamage=true;target.knockBackResist=0;target.Center=player.Center+new Vector2(200,0);Main.npc[20]=target;
        return(player,soul,target);
    }
    private static OreFlyingSword[] Shots()=>Main.projectile.Where(q=>q.active&&q.ModProjectile is OreFlyingSword)
        .Select(q=>(OreFlyingSword)q.ModProjectile).ToArray();
    private static SoulBlade Blade(Player p,SoulItem s,int damage=100)
    {
        var b=(SoulBlade)Main.projectile[s.EmitBlade(p,s.Hosts[0],0,s.Active.Count())].ModProjectile;
        b.Projectile.damage=b.Projectile.originalDamage=b.SpecialDamage=damage;b.Projectile.CritChance=0;
        b.TargetOffset=new(360,0);while(b.MotionPhase<77)b.AI();return b;
    }
    private static void Hit(SoulBlade b,NPC n,int unit)
    {
        b.Projectile.localAI[0]=unit==0?40:77;
        if(unit==0)n.Center=b.Projectile.Center;
        else{b.TryGetEchoPose(unit,out var center,out _,out _);n.Center=center;}
        Array.Clear(n.immune);Array.Clear(b.Projectile.localNPCImmunity);b.Projectile.Damage();
    }
    internal static void Run(Action<bool,string> check)
    {
        bool server=Main.dedServ,menu=Main.gameMenu;Main.dedServ=true;Main.gameMenu=false;
        try
        {
            var(p,s,n)=Setup(ItemID.CopperBroadsword,ItemID.TinBroadsword,ItemID.IronBroadsword,ItemID.LeadBroadsword);
            check(OreSwordTraits.EchoStacking(s)&&OreSwordTraits.StackDamage(s),"有效铜锡材质激活两项成阵联动");
            var b=Blade(p,s);
            for(int unit=0;unit<4;unit++)
            {
                Hit(b,n,unit);var shots=Shots();
                check(shots.Length==unit+1,"本体及各实体剑影的原生命中分别新增一把飞剑");
                check(shots.All(q=>q.StackGrowth.Layers==unit+1),"叠层更新已存在和新生成的全部成员");
                check(shots.All(q=>q.Projectile.damage==(int)(172*(1+.05f*unit))),"同阵所有剑均获得当前层数增伤");
                Hit(b,n,unit);check(Shots().Length==unit+1,"同一单元重复帧伤不重复积攒");
            }
            var wave=Shots();
            check(wave.Select(q=>q.Projectile.whoAmI).Distinct().Count()==4 && wave.Select(q=>q.Material).Distinct().Count()==4,
                "四把不同矿材继续保留独立弹幕，没有合并重叠飞剑");
            check(wave.All(q=>!q.WaitingForVolley && q.Projectile.damage==197),"四层齐射快照为基础172加15%，不是逐层复乘");
            foreach(var q in wave)
            {
                float previous=q.StackGlow.Brightness;
                for(int i=0;i<12;i++)
                {
                    q.AI();check(q.StackGlow.Brightness>=previous&&q.StackGlow.Brightness<=q.StackGrowth.Progress,
                        "叠层亮度动画连续靠近目标，不越界闪白");previous=q.StackGlow.Brightness;
                }
                check(q.StackGlow.Brightness>q.StackGrowth.Progress*.94f,"十二帧内达到该层目标亮度的95%左右");
            }
            using(var bytes=new MemoryStream())
            {
                wave[0].SendExtraAI(new BinaryWriter(bytes));bytes.Position=0;
                var copy=new Projectile();copy.SetDefaults(ModContent.ProjectileType<OreFlyingSword>());
                var remote=(OreFlyingSword)copy.ModProjectile;remote.ReceiveExtraAI(new BinaryReader(bytes));
                check(bytes.Position==bytes.Length&&remote.StackGrowth.Layers==4 && remote.StackGrowth.Damage==197 &&
                    Math.Abs(remote.StackGrowth.DamageShare-wave[0].DamageShare)<.0001f,"飞剑同步保留原始伤害快照、层数和防御结算份额");
                check(remote.StackGlow.Brightness==0,"接收层数从当前显示进度缓动，不在网络更新时跳亮");
            }
            var next=Blade(p,s,200);Hit(next,n,0);var newest=Shots().Except(wave).Single();
            check(newest.StackGrowth.Layers==1&&newest.Projectile.damage==344 && wave.All(q=>q.Projectile.damage==197),
                $"新阵从一层与自身伤害重新开始，已释放阵列不继续叠加：layers={newest.StackGrowth.Layers}, damage={newest.Projectile.damage}, base={newest.StackGrowth.BaseDamage}, old={string.Join(',',wave.Select(q=>q.Projectile.damage))}");
            Hit(next,n,1);var current=Shots().Except(wave).ToArray();
            check(current.All(q=>q.Projectile.damage==361)&&wave.All(q=>q.Projectile.damage==197),"新旧波次伤害隔离且各自保留倍率");

            (p,s,n)=Setup(ItemID.IronBroadsword,ItemID.LeadBroadsword);b=Blade(p,s);Hit(b,n,1);
            check(Shots().Length==0,"没有铜质特性时剑影仍伤害目标，但不积攒百炼飞剑");
            Hit(b,n,0);check(Shots().Length==1,"剑影命中不能提前吃掉本体资格");
            b=Blade(p,s);Hit(b,n,0);check(Shots().All(q=>q.Projectile.damage==157 && !q.StackGrowth.Empowered),
                "没有锡质特性时多把飞剑保留原伤害，不获得叠层提亮");

            (p,s,n)=Setup(ItemID.IronBroadsword,ItemID.CopperBroadsword,ItemID.TinBroadsword);s.LegacyReleased=0;s.Seals=0;
            check(!OreSwordTraits.EchoStacking(s)&&!OreSwordTraits.StackDamage(s)&&OreFamily.Count(s)==3,
                "切换诅咒的前四矿材例外不会放开铜锡普通特性");
            s.LegacyReleased=63;s.Hosts[1].TurnToAir();s.Hosts[2].TurnToAir();
            s.SupportHosts[0]=new Item(ItemID.CopperShortsword);s.SupportHosts[1]=new Item(ItemID.TinShortsword);s.RefreshStats();
            check(OreSwordTraits.EchoStacking(s)&&OreSwordTraits.StackDamage(s),"有效支援中的铜锡短剑也可提供同材质联动");
            s.Attached=false;check(!OreSwordTraits.EchoStacking(s)&&!OreSwordTraits.StackDamage(s),"未附着预览不授予矿石特性");
            var maximum=new OreStackGrowth();maximum.Capture(100,1.5f);maximum.SetLayers(100,true);
            check(maximum.Layers==14&&maximum.Damage==165,"十四层上限为加65%，异常层数不会无限增伤");
        }
        finally
        {
            foreach(var q in Main.projectile)q.active=false;foreach(var n in Main.npc)n.active=false;
            ModContent.GetInstance<OreSystem>().OnWorldUnload();Main.dedServ=server;Main.gameMenu=menu;
        }
    }
}
