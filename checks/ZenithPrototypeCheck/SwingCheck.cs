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
    private static Projectile[] Shots => Main.projectile.Where(p=>p.active && p.ModProjectile is SoulBlade).ToArray();
    private static void Clear() { foreach(var shot in Shots) shot.Kill(); }
    private static (Player, SoulItem) Setup(int count)
    {
        Clear(); Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
        var player=new Player{whoAmI=0,active=true,direction=1,gravDir=1};Main.player[0]=player;player.ResetEffects();
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;
        for(int i=0;i<count;i++) soul.Hosts[i]=new Item(SoulProgress.ZenithWeapons[i]);
        soul.RefreshStats();player.inventory[0]=soul.Item;player.selectedItem=0;
        return (player,soul);
    }
    private static List<int> Gesture(Player player,SoulItem soul,int duration,Action<bool,string> check)
    {
        var types=new List<int>();int baseline=Shots.Length;
        soul.UseAnimation(player);player.itemAnimationMax=duration;
        for(int remaining=duration;remaining>=1;remaining--)
        {
            player.itemAnimation=remaining;
            int before=Shots.Length;
            player.GetModPlayer<SoulSwingPlayer>().PostItemCheck();
            soul.UseItemFrame(player);
            var fresh=Shots.Skip(before).ToArray();
            check(fresh.Length<=1,"At most one actual projectile per animation frame");
            types.AddRange(fresh.Select(p=>(int)p.ai[0]));
            int phase=SoulSwingPlayer.Phase(player);
            float expected=MathHelper.Lerp(-2.6f,.65f,phase/5f);
            check(Math.Abs(player.compositeFrontArm.rotation-expected)<.001f,"Arm descent matches the current release node");
            if(fresh.Length>0)
            {
                int elapsed=duration-remaining+1;
                check(elapsed==(int)Math.Ceiling(phase*duration/5d),"Projectile first exists at its 20-percent threshold");
                check(fresh[0].ai[2]==0 && !fresh[0].hide,"No delayed hidden batch");
            }
            // A second hook call on the same animation frame cannot duplicate a release.
            int after=Shots.Length;player.GetModPlayer<SoulSwingPlayer>().PostItemCheck();
            check(Shots.Length==after,"Repeated frame does not duplicate projectile");
        }
        check(Shots.Length-baseline==types.Count,"Spawn accounting agrees with real projectile array");
        return types;
    }
    internal static void Run(Action<bool,string> check)
    {
        var (player,soul)=Setup(10);
        int before=Shots.Length;
        check(!soul.Shoot(player,new EntitySource_ItemUse_WithAmmo(player,soul.Item,0),player.Center,Vector2.UnitX,
            ModContent.ProjectileType<SoulBlade>(),1,1) && Shots.Length==before,"Vanilla Shoot creates no batch");
        var first=Gesture(player,soul,25,check);var second=Gesture(player,soul,25,check);
        check(first.SequenceEqual(SoulProgress.ZenithWeapons.Take(5)) && second.SequenceEqual(SoulProgress.ZenithWeapons.Skip(5)),
            "Ten swords split into first five and next five on two swings");
        check(Math.Abs(soul.UseSpeedMultiplier(player)-1.112f)<.001,"Mechanical flow advances once per gesture");
        var third=Gesture(player,soul,26,check);
        check(third.SequenceEqual(first),"Cycle wraps and non-divisible animation duration preserves all nodes");
        foreach(int count in new[]{1,2,3,4,5,6,7})
        {
            (player,soul)=Setup(count);
            var a=Gesture(player,soul,10,check);var b=Gesture(player,soul,10,check);
            check(a.Count==Math.Min(5,count) && a.Distinct().Count()==a.Count,"Sparse loadout leaves remaining nodes empty: "+count);
            check(b.SequenceEqual(Enumerable.Range(Math.Min(5,count),Math.Min(5,count)).Select(i=>soul.Hosts[i%count].type)),
                "Next swing continues rotation: "+count);
        }
        (player,soul)=Setup(10);
        soul.UseAnimation(player);player.itemAnimation=player.itemAnimationMax=1;
        player.GetModPlayer<SoulSwingPlayer>().PostItemCheck();
        check(player.itemAnimationMax==5 && player.itemAnimation==5 && Shots.Length==1,"Extreme speed clamps gesture to five distinct release frames");
        for(int t=4;t>=1;t--) { player.itemAnimation=t;player.GetModPlayer<SoulSwingPlayer>().PostItemCheck(); }
        check(Shots.Length==5,"High-speed gesture emits five real blades");
        foreach(string reason in new[]{"switch","death","loadout","unattached","edit"})
        {
            (player,soul)=Setup(10);soul.UseAnimation(player);player.itemAnimationMax=25;player.itemAnimation=25;
            player.GetModPlayer<SoulSwingPlayer>().PostItemCheck();
            if(reason=="switch") player.inventory[0]=new Item(ItemID.IronBroadsword);
            if(reason=="death") player.dead=true;
            if(reason=="loadout") soul.Hosts[0].TurnToAir();
            if(reason=="unattached") soul.Attached=false;
            if(reason=="edit") SoulUI.Toggle(soul);
            for(int t=24;t>=1;t--) { player.itemAnimation=t;player.GetModPlayer<SoulSwingPlayer>().PostItemCheck(); }
            check(Shots.Length==0,"Pending release cancelled on "+reason);
            if(reason=="edit") SoulUI.Toggle(soul);
        }
        (player,soul)=Setup(10);Main.myPlayer=1;
        check(Gesture(player,soul,10,check).Count==0,"Remote player does not create duplicate projectiles");
        Main.myPlayer=0;Main.netMode=NetmodeID.Server;
        check(Gesture(player,soul,10,check).Count==0,"Dedicated server does not duplicate owner projectiles");
        Main.netMode=NetmodeID.SinglePlayer;
        // Exercise vanilla's actual auto-reuse and countdown pipeline, rather
        // than relying exclusively on manually dispatched mod hooks.
        (player,soul)=Setup(10);
        Main.GameViewMatrix ??= new Terraria.Graphics.SpriteViewMatrix(null);
        player.position=new Vector2(1000,1000);player.controlUseItem=true;player.releaseUseItem=true;
        var engineTrace=new List<object>();var engineTypes=new List<int>();
        for(int tick=0;tick<150 && engineTypes.Count<10;tick++)
        {
            int beforeCount=Shots.Length;
            player.ItemCheck();player.PlayerFrame();
            var fresh=Shots.Skip(beforeCount).ToArray();
            check(fresh.Length<=1,"Actual vanilla ItemCheck never emits a batch");
            foreach(var p in fresh)
            {
                engineTypes.Add((int)p.ai[0]);
                engineTrace.Add(new{tick,remaining=player.itemAnimation,duration=player.itemAnimationMax,type=(int)p.ai[0],arm=player.compositeFrontArm.rotation});
                check(SoulSwingPlayer.Phase(player)>=1 && player.compositeFrontArm.enabled,"Actual PlayerFrame applies the swing arm pose");
            }
        }
        check(engineTypes.SequenceEqual(SoulProgress.ZenithWeapons),"Actual vanilla auto-reuse releases all ten hosts across two gestures");
        float originalArm=player.compositeFrontArm.rotation;
        player.gravDir=-1;soul.UseItemFrame(player);
        check(Math.Abs(player.compositeFrontArm.rotation+originalArm)<.001,"Inverted gravity mirrors arm exactly once");
        int[] themed=WoodTypes.Types.Concat(SoulProgress.ZenithWeapons).Concat(new int[]{ItemID.IceBlade,ItemID.BladeofGrass,
            ItemID.CopperBroadsword,ItemID.TinShortsword,ItemID.TinBroadsword,ItemID.IronShortsword,ItemID.IronBroadsword,
            ItemID.LeadShortsword,ItemID.LeadBroadsword,ItemID.SilverShortsword,ItemID.SilverBroadsword,
            ItemID.TungstenShortsword,ItemID.TungstenBroadsword,ItemID.GoldShortsword,ItemID.GoldBroadsword,
            ItemID.PlatinumShortsword,ItemID.PlatinumBroadsword,ItemID.BoneSword,ItemID.Terragrim,ItemID.Arkhalis}).Distinct().ToArray();
        check(themed.Select(SoulVisuals.Tint).Distinct().Count()==themed.Length,"Themed sword palette has individual distinct colors");
        int[] fallback=[ItemID.NightsEdge,ItemID.Excalibur,ItemID.TrueNightsEdge,ItemID.TrueExcalibur];
        check(fallback.Select(SoulVisuals.Tint).Distinct().Count()==4 && fallback.All(t=>SoulVisuals.Tint(t)==SoulVisuals.Tint(t)),
            "Unregistered sword identities get stable individual fallback colors");
        string report=Environment.GetEnvironmentVariable("ZENITH_CHECK_REPORT");
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(report),"swing-trace.json"),
            JsonSerializer.Serialize(new{first,second,third,engineTrace,palette=themed.Select(t=>new{type=t,color=SoulVisuals.Tint(t).ToString()})},new JsonSerializerOptions{WriteIndented=true}));
        Clear();
    }
}
