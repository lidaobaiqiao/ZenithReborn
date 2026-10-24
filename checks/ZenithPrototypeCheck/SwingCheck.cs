using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class SwingCheck
{
    internal static bool Recording, NormalizeTraits;
    internal static int Tick;
    internal static List<(int tick,int remaining,int duration,int host)> Trace=[];
    private static (Player,SoulItem) Setup(int count)
    {
        foreach(var p in Main.projectile)p.active=false;
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        var player=new Player{whoAmI=0,active=true,direction=1,position=new(1000,1000)};
        Main.player[0]=player;player.ResetEffects();
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;
        // 独行解除态为4把、走PostItemCheck均匀分布，不再与原版每挥3把重合。
        // 本套检查验证的是原生时钟契约，统一固定到削弱档（3把），让发射经过原版ItemCheck发射点。
        soul.LegacyReleased&=~(1<<(int)SoulCurse.Solitude);
        soul.Seals|=SoulCurses.Destroyer;
        for(int i=0;i<count;i++)soul.Hosts[i]=new Item(SoulProgress.ZenithWeapons[i]);
        soul.RefreshStats();player.inventory[0]=soul.Item;
        return(player,soul);
    }

    private static List<(int tick,int remaining,int duration,int host)> Trial(bool soulMode,float speed,int prefix,int count=10)
    {
        var (player,soul)=Setup(count);
        if(!soulMode)player.inventory[0]=new Item(ItemID.Zenith);
        if(prefix!=0)player.inventory[0].Prefix(prefix);
        player.GetAttackSpeed(DamageClass.Melee)=speed;
        player.controlUseItem=true;player.releaseUseItem=true;
        Main.GameViewMatrix??=new Terraria.Graphics.SpriteViewMatrix(null);
        Main.mouseX=1240;Main.mouseY=1040;Main.screenPosition=Vector2.Zero;
        Main.screenWidth=1920;Main.screenHeight=1080;
        Trace=[];Recording=NormalizeTraits=true;
        try{for(Tick=0;Tick<150;Tick++)player.ItemCheck();}
        finally{Recording=NormalizeTraits=false;}
        return Trace.ToList();
    }

    internal static void Run(Action<bool,string> check)
    {
        var reports=new List<object>();
        foreach(float speed in new[]{1f,1.5f,100f})foreach(int prefix in new[]{0,(int)PrefixID.Legendary})
        {
            var native=Trial(false,speed,prefix);var soul=Trial(true,speed,prefix);
            check(native.Count>0 && native.Select(x=>(x.tick,x.remaining,x.duration)).SequenceEqual(soul.Select(x=>(x.tick,x.remaining,x.duration))),
                "Actual native ItemCheck shot ticks and animation timers match Zenith under equal prefix and speed modifiers");
            check(soul.Select(x=>x.host).SequenceEqual(Enumerable.Range(0,soul.Count).Select(i=>SoulProgress.ZenithWeapons[i%10])),
                "Native firing clock preserves independent host rotation and crosses the ten-host boundary");
            check(soul.GroupBy(x=>x.tick).All(g=>g.Count()==1),"A vanilla firing opportunity emits one actual blade, never a batch");
            if(speed==1 && prefix==0)
                check(soul.Take(6).Select(x=>x.tick).SequenceEqual(new[]{0,10,20,30,40,50}),
                    "Unmodified Zenith releases at 0/10/20 and reuses at frame 30");
            reports.Add(new{speed,prefix,native=native.Select(x=>new{x.tick,x.remaining,x.duration}),soul=soul.Select(x=>new{x.tick,x.remaining,x.duration,x.host})});
        }
        foreach(int count in new[]{1,2,3,5})
        {
            var trace=Trial(true,1,0,count);
            check(trace.Take(Math.Min(3,count)).Select(x=>x.host).Distinct().Count()==Math.Min(3,count),"Sparse loadout does not repeat its hosts within a gesture");
            check(trace.Take(6).Select(x=>x.host).SequenceEqual(Enumerable.Range(0,Math.Min(6,trace.Count)).Select(i=>SoulProgress.ZenithWeapons[i%count])),
                "Spare nodes remain empty and next animation resumes host rotation");
            if(count<3)check(!trace.Any(x=>x.tick%30>=count*10),"Missing hosts leave native shot opportunities empty");
        }
        foreach(string reason in new[]{"switch","death","loadout","unattached","edit"})
        {
            var (player,soul)=Setup(10);player.itemAnimationMax=player.itemAnimation=30;soul.UseAnimation(player);
            if(reason=="switch")player.inventory[0]=new Item(ItemID.IronBroadsword);
            if(reason=="death")player.dead=true;
            if(reason=="loadout")soul.Hosts[0].TurnToAir();
            if(reason=="unattached")soul.Attached=false;
            if(reason=="edit")SoulUI.Toggle(soul);
            player.GetModPlayer<SoulSwingPlayer>().FireHost();
            check(!Main.projectile.Any(p=>p.active && p.ModProjectile is SoulBlade),"Firing is cancelled on "+reason);
            if(reason=="edit")SoulUI.Toggle(soul);
        }
        foreach(bool server in new[]{false,true})
        {
            var (player,soul)=Setup(10);Main.myPlayer=1;Main.netMode=server?NetmodeID.Server:NetmodeID.SinglePlayer;
            player.itemAnimation=player.itemAnimationMax=30;soul.UseAnimation(player);player.GetModPlayer<SoulSwingPlayer>().FireHost();
            check(!Main.projectile.Any(p=>p.active && p.ModProjectile is SoulBlade),"Only the owning client creates blades");
        }
        Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;
        var (p2,s2)=Setup(10);s2.Item.Prefix(PrefixID.Legendary);int useTime=s2.Item.useTime,animation=s2.Item.useAnimation;
        s2.RefreshStats();check(s2.Item.useTime==useTime && s2.Item.useAnimation==animation,"Inventory refresh preserves prefix timing");
        int[] themed=WoodTypes.Types.Concat(SoulProgress.ZenithWeapons).Concat(HardOreTypes.Types).Distinct().ToArray();
        check(themed.Select(SoulVisuals.Tint).Distinct().Count()==themed.Length,"Existing per-sword palette is retained");
        string report=Environment.GetEnvironmentVariable("ZENITH_CHECK_REPORT");
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(report),"native-cadence.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
        foreach(var p in Main.projectile)p.active=false;
    }
}

public class NativeCadenceProbe : GlobalProjectile
{
    public override void OnSpawn(Projectile projectile,IEntitySource source)
    {
        if(!SwingCheck.Recording || projectile.owner!=0 || projectile.type!=ProjectileID.FinalFractal && projectile.ModProjectile is not SoulBlade)return;
        var p=Main.player[0];SwingCheck.Trace.Add((SwingCheck.Tick,p.itemAnimation,p.itemAnimationMax,(int)projectile.ai[0]));
    }
}

public class NativeCadenceTraits : GlobalItem
{
    // Controlled comparison isolates the base native clock from the soul's
    // deliberately retained mechanical/wood/support trait speed modifiers.
    public override float UseSpeedMultiplier(Item item,Player player)=>SwingCheck.NormalizeTraits && item.ModItem is SoulItem soul?1/soul.UseSpeedMultiplier(player):1;
}
