using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 仅运行隔离副本世界，检查点可暂停查看；自动模式正常退出，不碰正式存档。
public class MartialGuiFixture : ModSystem
{
    private int ticks,step;
    private int pauseDraws;
    private bool waiting,entering,seenReturning;
    private SoulItem soul;
    private NPC target;
    private Projectile hostile;
    private Vector2 stopNpc,stopShot,initialPlayer;
    private readonly Dictionary<string,object> samples=[];
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Enter;}
    public override void Unload()=>Main.OnPreDraw-=Enter;
    private void Enter(GameTime time)
    {
        if(entering || !Main.gameMenu || Main.menuMode!=0 || Environment.GetEnvironmentVariable("ZENITH_MARTIAL_AUTORUN")!="1" ||
            Environment.GetEnvironmentVariable("ZENITH_MARTIAL_GUI")!="1" || !Main.SavePath.Contains("martial-playtest",StringComparison.OrdinalIgnoreCase))return;
        entering=true;
        try
        {
            Main.LoadPlayers();Main.LoadWorlds();
            var pf=Main.PlayerList.Find(p=>p.Name=="测试" && !p.IsCloudSave);
            var wf=Main.WorldList.Find(w=>Path.GetFileNameWithoutExtension(w.Path)=="测试" && !w.IsCloudSave);
            if(pf==null || wf==null || !Path.GetFullPath(pf.Path).StartsWith(Main.SavePath,StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFullPath(wf.Path).StartsWith(Main.SavePath,StringComparison.OrdinalIgnoreCase))throw new Exception("只能运行专用隔离测试副本");
            pf.SetAsActive();wf.SetAsActive();Main.autoSave=false;Main.netMode=NetmodeID.SinglePlayer;Main.menuMode=10;WorldGen.playWorld();
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(Main.SavePath,"martial-error.txt"),ex.ToString());Main.instance.Exit();}
    }
    private bool Enabled=>!Main.dedServ && !Main.gameMenu && Environment.GetEnvironmentVariable("ZENITH_MARTIAL_GUI")=="1" &&
        Main.SavePath.Contains("martial-playtest",StringComparison.OrdinalIgnoreCase);
    public override void OnWorldLoad(){ticks=step=0;waiting=seenReturning=false;soul=null;samples.Clear();}
    private void Start(int tier)
    {
        var p=Main.LocalPlayer;var mp=p.GetModPlayer<MartialPlayer>();mp.Cancel();
        soul.MartialCharge=300;soul.MartialTier=tier;target.velocity=Vector2.Zero;
        target.Center=p.Center+new Vector2(300,-20);Main.mouseX=(int)(target.Center.X-Main.screenPosition.X);Main.mouseY=(int)(target.Center.Y-Main.screenPosition.Y);
        mp.TryActivate();mp.BeginAttack(soul);
    }
    private void Checkpoint(string name,object value)
    {
        samples[name]=value;
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(Main.SavePath,"martial-gui.json"),JsonSerializer.Serialize(new{version=ModLoader.GetMod("ZenithReborn").Version.ToString(),
            tested_package_sha256=hash,step,samples,seenReturning,actualWorld=true,liveMultiplayerTested=false},new JsonSerializerOptions{WriteIndented=true}));
        waiting=true;pauseDraws=0;Main.autoPause=true;Main.playerInventory=true;
    }
    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if(!Enabled || !waiting || Environment.GetEnvironmentVariable("ZENITH_MARTIAL_AUTORUN")!="1")return;
        if(++pauseDraws<75)return;
        if(step==5){Main.instance.Exit();return;}
        Main.autoPause=false;Main.playerInventory=false;
    }
    public override void PostUpdateWorld()
    {
        if(!Enabled)return;
        var p=Main.LocalPlayer;if(!p.active)return;
        p.immune=true;p.immuneTime=30;p.statLife=p.statLifeMax2;
        if(++ticks==60)
        {
            soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;soul.Attached=true;soul.Seals=SoulCurses.All;
            soul.Kills=SoulProgress.All;soul.Cores=63;
            int[] types=[ItemID.Katana,ItemID.Muramasa,ItemID.Excalibur,ItemID.TerraBlade];
            for(int i=0;i<4;i++)soul.Hosts[i]=new Item(types[i]);soul.RefreshStats();p.inventory[0]=soul.Item;p.selectedItem=0;
            int index=NPC.NewNPC(p.GetSource_Misc("MartialGuiFixture"),(int)p.Center.X+300,(int)p.Center.Y,NPCID.KingSlime);
            target=Main.npc[index];target.aiStyle=-1;target.noGravity=target.noTileCollide=true;target.damage=0;
            target.life=target.lifeMax=10000000;target.defense=0;target.knockBackResist=0;target.HideStrikeDamage=true;
            target.Center=p.Center+new Vector2(300,-20);Main.autoPause=false;Main.playerInventory=false;Main.mapStyle=0;Main.dayTime=true;Main.time=18000;
            initialPlayer=p.Center;Start(1);step=1;
        }
        if(soul==null || target==null)return;
        target.timeLeft=750;
        if(waiting)
        {
            waiting=false;step++;
            if(step==2)Start(2);
            if(step==3)Start(3);
            return;
        }
        var blades=Main.projectile.Where(q=>q.active && q.ModProjectile is MartialBlade).ToArray();
        seenReturning|=blades.Any(q=>((MartialBlade)q.ModProjectile).State==4);
        var g=Main.projectile.FirstOrDefault(q=>q.active && q.ModProjectile is MartialGiant)?.ModProjectile as MartialGiant;
        if(step==1 && blades.Count(q=>((MartialBlade)q.ModProjectile).State==2)>=3)
        {
            int regen=0;target.lifeRegen=0;target.GetGlobalNPC<MartialTarget>().UpdateLifeRegen(target,ref regen);
            Checkpoint("embedded",new{tick=ticks,pins=blades.Count(q=>((MartialBlade)q.ModProjectile).State==2),target.life,target.lifeRegen,regen});
        }
        else if(step==2 && g?.Phase==4 && g.Age>=8)
            Checkpoint("flung",new{tick=ticks,phase=g.Phase,flung=blades.Count(q=>((MartialBlade)q.ModProjectile).State==3),target.life});
        else if(step==3 && g?.ReadyForCut==true)
        {
            target.velocity=new Vector2(1.5f,0);g.StartTimeCut(target.Center);
            int id=Projectile.NewProjectile(p.GetSource_Misc("MartialGuiFixture"),p.Center+new Vector2(130,-40),new Vector2(4,1),ProjectileID.DeathLaser,0,0,Main.myPlayer);
            hostile=Main.projectile[id];hostile.hostile=true;hostile.friendly=false;hostile.timeLeft=600;
            stopNpc=target.Center;stopShot=hostile.Center;
        }
        else if(step==3 && g?.Phase==3 && g.Age>=35)
            Checkpoint("time-stop-hold",new{tick=ticks,phase=g.Phase,holdAge=g.Age,playerMoved=Vector2.Distance(initialPlayer,p.Center),
                npcStopped=Vector2.Distance(stopNpc,target.Center)<.1f,bulletStopped=hostile!=null && Vector2.Distance(stopShot,hostile.Center)<.1f,
                target.life,pins=blades.Count(q=>((MartialBlade)q.ModProjectile).State==2)});
        else if(step==4 && g?.Phase==4 && g.Age>=8)
            Checkpoint("finishing-wave",new{tick=ticks,phase=g.Phase,waves=Main.projectile.Count(q=>q.active && q.ModProjectile is MartialWave),
                npcResumed=target.velocity.X!=0,bulletResumed=hostile!=null && hostile.velocity.Length()>0,target.life});
        else if(step==5 && p.GetModPlayer<MartialPlayer>().Mode==MartialMode.Normal && blades.Length==0 && !Main.projectile.Any(q=>q.active && q.ModProjectile is MartialGiant or MartialWave))
            Checkpoint("restored",new{tick=ticks,nativeRestored=true,returned=seenReturning,frozen=MartialTime.Frozen(target.Center),target.life});
    }
}
