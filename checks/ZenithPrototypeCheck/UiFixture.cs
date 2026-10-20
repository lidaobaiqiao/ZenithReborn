using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// Opt-in GUI fixture. Never touches a production character/profile.
public class UiFixture : ModSystem
{
    private bool ready;
    private int ticks;
    public override void OnWorldLoad()=>ready=false;
    public override void PostUpdateProjectiles()
    {
        if(Environment.GetEnvironmentVariable("ZENITH_ORE_GUI")!="1" ||
            !Main.SavePath.Contains("stage-one-playtest",StringComparison.OrdinalIgnoreCase)) return;
        var swords=Main.projectile.Where(p=>p.active && p.ModProjectile is OreFlyingSword).ToArray();
        if(swords.Length>0 && swords[0].timeLeft<=60)
        { Main.autoPause=true; Main.playerInventory=true; }
    }
    public override void PostUpdateWorld()
    {
        if(Main.dedServ || Environment.GetEnvironmentVariable("ZENITH_UI_GUI")!="1" ||
            !Main.SavePath.Contains("stage-one-playtest",StringComparison.OrdinalIgnoreCase)) return;
        Player p=Main.LocalPlayer;if(!p.active) return;
        if(ready)
        {
            if(++ticks%30==0)
                System.IO.File.WriteAllText(System.IO.Path.Combine(Main.SavePath,"ui-input-probe.json"),
                    System.Text.Json.JsonSerializer.Serialize(new {
                        ticks,editing=SoulUI.EditingSoul!=null,blocking=SoulInput.Blocking,
                        cursor=new{Main.mouseItem.type,Main.mouseItem.stack},
                        inventory=p.inventory.Take(50).Select(i=>new{i.type,i.stack}).ToArray(),
                        armor=p.armor.Select(i=>new{i.type,i.stack}).ToArray(),
                        hosts=p.inventory.Select(i=>i.ModItem).OfType<SoulItem>().SelectMany(s=>s.Stored).Select(i=>new{i.type,i.stack}).ToArray(),
                        useItem=p.controlUseItem,useTile=p.controlUseTile,
                    }));
            return;
        }
        ready=true;
        for(int i=1;i<50;i++) { p.inventory[i]=new Item(ItemID.StoneBlock);p.inventory[i].stack=100+i; }
        p.inventory[1]=new Item(ItemID.GoldBroadsword);
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.KingMemory=true;soul.Kills=SoulProgress.All;soul.Cores=63;
        for(int i=0;i<7;i++) soul.Hosts[i]=new Item(WoodTypes.Types[i]);
        for(int i=0;i<5;i++) soul.SupportHosts[i]=new Item(WoodTypes.Types[i+7]);
        for(int i=0;i<3;i++) soul.Hosts[i+7]=new Item(HardOreTypes.Types[i]);
        soul.ForestHosts[0]=new Item(ItemID.TerraBlade);soul.ForestHosts[1]=new Item(ItemID.StarWrath);
        soul.RefreshStats();p.inventory[0]=soul.Item;p.selectedItem=0;
        Main.dayTime=true;Main.time=12000;
        if(Environment.GetEnvironmentVariable("ZENITH_ORE_GUI")=="1")
        {
            for(int i=0;i<soul.Hosts.Length;i++)soul.Hosts[i]=new Item();
            for(int i=0;i<soul.SupportHosts.Length;i++)soul.SupportHosts[i]=new Item();
            for(int i=0;i<soul.ForestHosts.Length;i++)soul.ForestHosts[i]=new Item();
            for(int i=0;i<4;i++)soul.Hosts[i]=new Item(OreFamily.DefaultType[i]);
            soul.RefreshStats();
            int n=NPC.NewNPC(p.GetSource_Misc("OreRenderFixture"),(int)p.Center.X+160,(int)p.Center.Y,NPCID.Zombie);
            var target=Main.npc[n];target.aiStyle=-1;target.noGravity=true;target.noTileCollide=true;
            target.damage=0;target.life=target.lifeMax=100000;target.HideStrikeDamage=true;
            target.width=80;target.height=100;target.Center=p.Center+new Vector2(160,0);
            int b=soul.EmitBlade(p,soul.Hosts[0],0,4);var blade=Main.projectile[b];
            blade.ModProjectile.AI();blade.Center=target.Center;blade.Damage();
            Main.autoPause=false; Main.playerInventory=false;
            return;
        }
        SoulUI.Open(soul);
    }
}

public class OreRenderProbe : GlobalProjectile
{
    private static int draws;
    public override void PostDraw(Projectile projectile,Microsoft.Xna.Framework.Color lightColor)
    {
        if(projectile.ModProjectile is not OreFlyingSword || Environment.GetEnvironmentVariable("ZENITH_ORE_GUI")!="1") return;
        draws++;
        if(draws==20) System.IO.File.WriteAllText(System.IO.Path.Combine(Main.SavePath,"ore-render-probe.json"),
            System.Text.Json.JsonSerializer.Serialize(new{drawCalls=draws,activeSwords=Main.projectile.Count(p=>p.active && p.ModProjectile is OreFlyingSword),
            hidden=projectile.hide,texture=projectile.ModProjectile.Texture,profile="isolated-stage-one-playtest"}));
    }
}
