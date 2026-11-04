using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// Opt-in GUI fixture. Never touches a production character/profile.
public class UiFixture : ModSystem
{
    private Point replayStart;
    private string replayBefore;
    private void ReplayDrag()
    {
        if(Environment.GetEnvironmentVariable("ZENITH_DRAG_REPLAY")!="1" || ticks<120 || ticks>140 || SoulUI.EditingSoul==null) return;
        float scale=Main.UIScale;int w=Main.screenWidth,h=Main.screenHeight;
        Main.screenWidth=(int)(w/scale);Main.screenHeight=(int)(h/scale);
        var current=SoulUI.PanelBounds();Main.screenWidth=w;Main.screenHeight=h;
        if(ticks==120)
        {
            replayBefore=current.ToString();
        }
        replayStart=new Point(current.X+40,current.Y+20);
        // Short synthetic movements across twenty rendered frames. Physical
        // input remains untouched; this is not OS drag completion evidence.
        if(ticks<140)
        {
            SoulUI.HandleWindowDrag((int)(replayStart.X*scale),(int)(replayStart.Y*scale),w,h,scale,true,true);
            SoulUI.HandleWindowDrag((int)((replayStart.X+4)*scale),(int)(replayStart.Y*scale),w,h,scale,true,false);
        }
        if(ticks==140)
        {
            SoulUI.HandleWindowDrag(0,0,w,h,scale,false,false);
            Main.screenWidth=(int)(w/scale);Main.screenHeight=(int)(h/scale);
            string after=SoulUI.PanelBounds().ToString();Main.screenWidth=w;Main.screenHeight=h;
            System.IO.File.WriteAllText(System.IO.Path.Combine(Main.SavePath,"window-drag-replay.json"),
                System.Text.Json.JsonSerializer.Serialize(new{before=replayBefore,after,frames=20,physical_drag=false}));
        }
    }
    private bool ready;
    private int ticks;
    private int visualTarget=-1;
    public override void OnWorldLoad()=>ready=false;
    public override void PostUpdateProjectiles()
    {
        if(Environment.GetEnvironmentVariable("ZENITH_VISUAL_GUI")=="1" &&
            Main.SavePath.Contains("stage-one-playtest",StringComparison.OrdinalIgnoreCase) && SoulUI.EditingSoul==null &&
            Main.projectile.Any(p=>p.active && p.ModProjectile is OreFlyingSword && p.timeLeft<=48))
        { Main.autoPause=true;Main.playerInventory=true;return; }
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
            // Keep UI inspection independent of incidental NPC combat.
            p.statLife=p.statLifeMax2;p.immune=true;p.immuneTime=60;
            ReplayDrag();
            if(Environment.GetEnvironmentVariable("ZENITH_VISUAL_GUI")=="1" && visualTarget>=0)
            {
                var enemy=Main.npc[visualTarget];
                if(!enemy.active)
                {
                    visualTarget=NPC.NewNPC(p.GetSource_Misc("VisualFixture"),(int)p.Center.X+160,(int)p.Center.Y,NPCID.Zombie);
                    enemy=Main.npc[visualTarget];enemy.aiStyle=-1;enemy.noGravity=true;enemy.noTileCollide=true;
                    enemy.damage=0;enemy.life=enemy.lifeMax=100000;enemy.HideStrikeDamage=true;enemy.width=enemy.height=30;
                }
                enemy.timeLeft=750;
            }
            if(Environment.GetEnvironmentVariable("ZENITH_VISUAL_GUI")=="1" && SoulUI.EditingSoul==null &&
                visualTarget>=0 && Main.npc[visualTarget].active && ticks%90==0)
            {
                var source=p.inventory[0].ModItem as SoulItem;
                if(source!=null)
                {
                    int b=source.EmitBlade(p,source.Hosts[0],0,4);var blade=Main.projectile[b];
                    blade.ModProjectile.AI();blade.Center=Main.npc[visualTarget].Center;blade.Damage();
                }
            }
            if(++ticks%30==0)
                System.IO.File.WriteAllText(System.IO.Path.Combine(Main.SavePath,"ui-input-probe.json"),
                    System.Text.Json.JsonSerializer.Serialize(new {
                        ticks,editing=SoulUI.EditingSoul!=null,blocking=SoulInput.Blocking,
                        domain=SoulUI.DomainMode,selected=SoulUI.SelectedSlot,legendary=SoulDifficulty.Legendary,
                        supportCapacity=SoulUI.EditingSoul?.SupportCapacity,
                        supports=SoulUI.EditingSoul?.Supports.Select(i=>i.type).ToArray(),
                        legendaryHost=SoulUI.EditingSoul?.LegendaryHost.type,
                        cursor=new{Main.mouseItem.type,Main.mouseItem.stack},
                        inventory=p.inventory.Take(50).Select(i=>new{i.type,i.stack}).ToArray(),
                        armor=p.armor.Select(i=>new{i.type,i.stack}).ToArray(),
                        hosts=p.inventory.Select(i=>i.ModItem).OfType<SoulItem>().SelectMany(s=>s.Stored).Select(i=>new{i.type,i.stack}).ToArray(),
                        useItem=p.controlUseItem,useTile=p.controlUseTile,
                    }));
            return;
        }
        ready=true; Main.autoPause=false; Main.mapStyle=0;
        for(int i=1;i<50;i++) { p.inventory[i]=new Item(ItemID.StoneBlock);p.inventory[i].stack=100+i; }
        p.inventory[1]=new Item(ItemID.GoldBroadsword);
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.KingMemory=true;soul.Kills=SoulProgress.All;soul.Cores=63;
        // 旧口径：7 主槽 + 5 支援槽共 12 木；现名册 8 把 → 6 主槽 + 2 支援槽。
        for(int i=0;i<6;i++) soul.Hosts[i]=new Item(WoodTypes.Types[i]);
        for(int i=0;i<2;i++) soul.SupportHosts[i]=new Item(WoodTypes.Types[i+6]);
        for(int i=0;i<3;i++) soul.Hosts[i+7]=new Item(HardOreTypes.Types[i]);
        soul.ForestHosts[0]=new Item(ItemID.TerraBlade);soul.ForestHosts[1]=new Item(ItemID.StarWrath);
        soul.RefreshStats();p.inventory[0]=soul.Item;p.selectedItem=0;
        if(Environment.GetEnvironmentVariable("ZENITH_DOMAIN_GUI")=="1")
        {
            SoulUI.DomainMode=true;soul.Seals=SoulCurses.All;
            int[] available=OreFamily.DefaultType.Concat(SoulProgress.ZenithWeapons).Append(ItemID.IceBlade).Append(ItemID.BladeofGrass).Distinct().ToArray();
            for(int i=1;i<50;i++)p.inventory[i]=i<=available.Length?new Item(available[i-1]):new Item();
            p.inventory[1].Prefix(PrefixID.Legendary);
            if(Environment.GetEnvironmentVariable("ZENITH_FOLLOWUP_GUI")=="1")
            {
                soul.OreCollectionMask=OreFamily.AllMaterials;
                for(int j=0;j<3;j++)soul.Hosts[j+7]=new Item(OreFamily.DefaultType[j+8]);
                soul.ForestHosts[0]=new Item(OreFamily.DefaultType[0]);soul.ForestHosts[1]=new Item(OreFamily.DefaultType[1]);
                soul.AdditionalSupports=OreFamily.DefaultType.Skip(2).Take(6).Select(t=>new Item(t)).ToList();
                if(!SoulDifficulty.Legendary)SoulDifficulty.Toggle(out _);
                soul.LegendaryHost=new Item(ItemID.Meowmere);soul.RefreshStats();
            }
        }
        Main.dayTime=true;Main.time=12000;
        if(Environment.GetEnvironmentVariable("ZENITH_BLESS_GUI")=="1")
        {
            soul.Seals=SoulCurses.All;soul.LegacyReleased=0;soul.YujianCharge=BlessingTuning.ChargeFrames;
            soul.RefreshStats();Main.autoPause=false;Main.playerInventory=false;return;
        }
        if(Environment.GetEnvironmentVariable("ZENITH_ORE_GUI")=="1" || Environment.GetEnvironmentVariable("ZENITH_VISUAL_GUI")=="1")
        {
            for(int i=0;i<soul.Hosts.Length;i++)soul.Hosts[i]=new Item();
            for(int i=0;i<soul.SupportHosts.Length;i++)soul.SupportHosts[i]=new Item();
            for(int i=0;i<soul.ForestHosts.Length;i++)soul.ForestHosts[i]=new Item();
            for(int i=0;i<4;i++)soul.Hosts[i]=new Item(OreFamily.DefaultType[i]);
            soul.RefreshStats();
            int n=NPC.NewNPC(p.GetSource_Misc("OreRenderFixture"),(int)p.Center.X+160,(int)p.Center.Y,NPCID.Zombie);
            visualTarget=n;
            var target=Main.npc[n];target.aiStyle=-1;target.noGravity=true;target.noTileCollide=true;
            target.damage=0;target.life=target.lifeMax=100000;target.HideStrikeDamage=true;
            target.width=80;target.height=100;target.Center=p.Center+new Vector2(160,0);
            if(Environment.GetEnvironmentVariable("ZENITH_VISUAL_GUI")=="1") { target.width=target.height=30; }
            int b=soul.EmitBlade(p,soul.Hosts[0],0,4);var blade=Main.projectile[b];
            blade.ModProjectile.AI();blade.Center=target.Center;blade.Damage();
            Main.autoPause=false; Main.playerInventory=false;
            if(Environment.GetEnvironmentVariable("ZENITH_VISUAL_GUI")=="1")SoulUI.Open(soul);
            return;
        }
        if(Environment.GetEnvironmentVariable("ZENITH_TRAIT_GUI")=="1")
        {
            for(int i=0;i<soul.Hosts.Length;i++)soul.Hosts[i]=new Item();
            for(int i=0;i<soul.SupportHosts.Length;i++)soul.SupportHosts[i]=new Item();
            for(int i=0;i<soul.ForestHosts.Length;i++)soul.ForestHosts[i]=new Item();
            soul.Hosts[0]=new Item(ItemID.WoodenSword);soul.Hosts[1]=new Item(ItemID.CobaltSword);
            soul.Hosts[2]=new Item(ItemID.EnchantedSword);soul.SupportHosts[0]=new Item(ItemID.IceBlade);
            soul.RefreshStats();Main.autoPause=false;
        }
        SoulUI.Open(soul);
    }
}

public class OreRenderProbe : GlobalProjectile
{
    private static int draws;
    public override void PostDraw(Projectile projectile,Microsoft.Xna.Framework.Color lightColor)
    {
        if(projectile.ModProjectile is not OreFlyingSword || (Environment.GetEnvironmentVariable("ZENITH_ORE_GUI")!="1" &&
            Environment.GetEnvironmentVariable("ZENITH_VISUAL_GUI")!="1")) return;
        draws++;
        if(draws==20) System.IO.File.WriteAllText(System.IO.Path.Combine(Main.SavePath,"ore-render-probe.json"),
            System.Text.Json.JsonSerializer.Serialize(new{drawCalls=draws,activeSwords=Main.projectile.Count(p=>p.active && p.ModProjectile is OreFlyingSword),
            hidden=projectile.hide,texture=projectile.ModProjectile.Texture,profile="isolated-stage-one-playtest"}));
    }
}
