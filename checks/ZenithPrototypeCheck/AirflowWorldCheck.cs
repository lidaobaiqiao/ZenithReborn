using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 只在专用隔离目录启用：加载已复制的测试人物／世界，直接观察生产飞剑的 AI 与绘制。
// 不叠加第二套预览特效，不保存测试中的物品或 NPC，不读取正式存档。
public class AirflowWorldCheck : ModSystem
{
    private bool entering,ready,finished;
    private int tick,npcId=-1,projectileId=-1,hitObservedFrames;
    private readonly HashSet<int> launchedItems=new();
    private bool damageObserved;
    private readonly HashSet<int> damagedItems=new();
    private Player player;
    private Vector2 anchor;
    private SoulItem soul;
    private string root;
    private readonly List<object> records=new();
    private bool DummyCheck=>Environment.GetEnvironmentVariable("ZENITH_DUMMY_CHECK")=="1";
    private bool PlacementOnly=>Environment.GetEnvironmentVariable("ZENITH_DUMMY_PLACEMENT_ONLY")=="1";
    private string BeforePlacement=>Environment.GetEnvironmentVariable("ZENITH_DUMMY_BEFORE_PLACEMENT")??"none";
    private bool dummyPlaced;
    private int dummyFrames;
    private int modDummyId=-1;
    private int capturedTick=-1;
    private bool Enabled=>!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZENITH_AIRFLOW_WORLD"))&&
        Main.SavePath.Contains("airflow-world-profile",StringComparison.OrdinalIgnoreCase);
    public override void Load(){if(!Main.dedServ){Main.OnPreDraw+=Enter;Main.OnPostDraw+=Capture;}}
    public override void Unload(){Main.OnPreDraw-=Enter;Main.OnPostDraw-=Capture;}
    private void Enter(GameTime time)
    {
        if(!Enabled||entering||!Main.gameMenu||Main.menuMode!=0)return;
        entering=true;root=Environment.GetEnvironmentVariable("ZENITH_AIRFLOW_WORLD");Directory.CreateDirectory(root);
        try
        {
            Main.LoadPlayers();Main.LoadWorlds();
            var pf=Main.PlayerList.Find(p=>p.Name=="测试");
            var wf=Main.WorldList.Find(w=>Path.GetFileNameWithoutExtension(w.Path)=="测试");
            if(pf==null||wf==null)throw new Exception("隔离目录缺少测试人物或世界。");
            pf.SetAsActive();wf.SetAsActive();Main.autoSave=false;Main.netMode=NetmodeID.SinglePlayer;
            Main.menuMode=10;WorldGen.playWorld();
            File.WriteAllText(Path.Combine(root,"entry.txt"),"只加载隔离测试存档，已请求原版世界加载。\n");
        }
        catch(Exception error){File.WriteAllText(Path.Combine(root,"error.txt"),error.ToString());Main.instance.Exit();}
    }
    public override void PostUpdateEverything()
    {
        if(!Enabled||Main.gameMenu||finished)return;
        if(!ready)
        {
            ready=true;player=Main.LocalPlayer;anchor=player.Center-new Vector2(0,180);
            if(PlacementOnly){Main.dayTime=false;Main.time=60;Main.moonPhase=1;}
            soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;
            player.inventory[0]=soul.Item;player.selectedItem=0;Main.hideUI=!DummyCheck;
            if(DummyCheck&&BeforePlacement=="none"&&ModContent.TryFind<ModItem>("ImproveGame/Dummy",out var dummyItem))
                player.inventory[0]=new Item(dummyItem.Type);
            if(!PlacementOnly||BeforePlacement!="none")npcId=NPC.NewNPC(player.GetSource_Misc("AirflowWorldCheck"),(int)anchor.X+240,(int)anchor.Y,NPCID.Zombie);
            if(npcId>=0&&npcId<Main.maxNPCs)Main.npc[npcId].life=Main.npc[npcId].lifeMax=100000;
        }
        if(PlacementOnly&&BeforePlacement!="none")
        {
            if(tick==20)Spawn(ItemID.MythrilSword);
            if(tick==30)
            {
                int id=soul.EmitBlade(player,soul.Hosts[0],0,1);
                ((SoulBlade)Main.projectile[id].ModProjectile).TargetOffset=new Vector2(240,0);
                if(BeforePlacement=="burst"){soul.YujianCharge=BlessingTuning.ChargeFrames;player.GetModPlayer<BlessingPlayer>().TryActivate();}
            }
        }
        if(DummyCheck&&tick==(PlacementOnly?60:5))
        {
            // 实际放置原版稻草人及对应实体；支撑和存档只在隔离世界中改变。
            int x=(int)(anchor.X+100)/16,y=(int)anchor.Y/16+3;
            for(int dx=-2;dx<4;dx++)for(int dy=-4;dy<1;dy++)Main.tile[x+dx,y+dy].ClearTile();
            for(int dx=-2;dx<4;dx++){var tile=Main.tile[x+dx,y+1];tile.HasTile=true;tile.TileType=TileID.Stone;}
            if(!PlacementOnly)WorldGen.PlaceObject(x,y,TileID.TargetDummy);
            dummyPlaced=Main.tile[x,y].HasTile&&Main.tile[x,y].TileType==TileID.TargetDummy;
            if(dummyPlaced)Terraria.GameContent.Tile_Entities.TETrainingDummy.Place(x,y-2);
            if(ModContent.TryFind<ModNPC>("ImproveGame/DummyNPC",out var dummy))
            {
                // 调用已安装模组物品的真实放置路径，包含它自己的生成／配置／粒子。
                if(BeforePlacement!="none"&&ModContent.TryFind<ModItem>("ImproveGame/Dummy",out var item))
                {player.inventory[0]=new Item(item.Type);player.itemTime=player.itemAnimation=0;}
                Main.mouseX=(int)(anchor.X+100-Main.screenPosition.X);Main.mouseY=(int)(anchor.Y-Main.screenPosition.Y);
                if(PlacementOnly)
                {
                    // 走原版完整物品使用流程，覆盖全局物品钩子及挥手动画，不能用单个回调替代。
                    player.mouseInterface=false;player.controlUseItem=true;player.releaseUseItem=true;
                    player.ItemCheck();player.controlUseItem=false;
                }
                else player.HeldItem.ModItem.CanUseItem(player);
                foreach(var npc in Main.ActiveNPCs)if(npc.type==dummy.Type){modDummyId=npc.whoAmI;break;}
            }
        }
        if(DummyCheck){foreach(var npc in Main.ActiveNPCs)if(npc.type==NPCID.TargetDummy){dummyFrames++;break;}}
        player.Center=anchor;player.velocity=Vector2.Zero;player.immune=true;player.immuneTime=60;
        if(npcId>=0&&Main.npc[npcId].active)
        {
            var target=Main.npc[npcId];target.Center=anchor+new Vector2(240,0);target.velocity=Vector2.Zero;
            target.noGravity=true;target.noTileCollide=true;target.knockBackResist=0;
        }
        if(!PlacementOnly&&tick==20)player.inventory[0]=soul.Item;
        if(!PlacementOnly&&tick is 20 or 130 or 240)Spawn(tick==20?ItemID.CopperShortsword:tick==130?ItemID.CobaltSword:ItemID.MythrilSword);
        if(!PlacementOnly&&DummyCheck&&tick is 310 or 350 or 390)
        {
            soul.Hosts[0]=new Item(ItemID.MythrilSword);soul.RefreshStats();
            int id=soul.EmitBlade(player,soul.Hosts[0],0,1);
            ((SoulBlade)Main.projectile[id].ModProjectile).TargetOffset=new Vector2(200,0);
        }
        var projectile=projectileId>=0?Main.projectile[projectileId]:null;
        if(projectile?.active==true&&projectile.ModProjectile is OreFlyingSword sword)
        {
            if(sword.Launched)
            {
                launchedItems.Add(sword.VisualType);
                if(Main.npc[npcId].life<100000)damagedItems.Add(sword.VisualType);
            }
            records.Add(new {tick,item=sword.VisualType,age=sword.Age,launched=sword.Launched,
                speed=projectile.velocity.Length(),x=projectile.Center.X,y=projectile.Center.Y});
        }
        damageObserved|=npcId>=0&&Main.npc[npcId].life<100000;
        tick++;
        if(tick>=(PlacementOnly?220:DummyCheck?500:345))
        {
            finished=true;
            File.WriteAllText(Path.Combine(root,"world.json"),JsonSerializer.Serialize(new{
                passed=PlacementOnly?modDummyId>=0:launchedItems.Count==3&&damagedItems.Count==3&&damageObserved,
                placementOnly=PlacementOnly,beforePlacement=BeforePlacement,actualWorld=true,productionAI=true,productionRenderer=true,overlayRenderer=false,
                testedPackageSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant(),
                isolatedSaves=true,frames=tick,hitObservedFrames,damageObserved,launchedItems,damagedItems,records,
                dummyCheck=DummyCheck,dummyPlaced,dummyFrames,modDummyId,
                lighting=Terraria.Lighting.Mode.ToString(),Main.drawToScreen,
                loadedMods=ModLoader.Mods.Select(m=>new {m.Name,version=m.Version.ToString()}).ToArray(),
                multiplayerTested=false,artApproved=false
            },new JsonSerializerOptions{WriteIndented=true}));
            Main.instance.Exit();
        }
    }
    private void Spawn(int type)
    {
        soul.Hosts[0]=new Item(type);soul.RefreshStats();player.selectedItem=0;
        var target=Main.npc[npcId];target.life=target.lifeMax=100000;target.defense=0;
        projectileId=Projectile.NewProjectile(player.GetSource_Misc("AirflowWorldCheck"),anchor,Vector2.Zero,
            ModContent.ProjectileType<OreFlyingSword>(),30,0,Main.myPlayer);
        var projectile=Main.projectile[projectileId];var sword=(OreFlyingSword)projectile.ModProjectile;
        sword.Material=OreFamily.MaterialOf(type);sword.VisualType=type;sword.Total=4;sword.RingIndex=3;
        sword.SetCount=1;sword.SourceSoulId=soul.SoulId;sword.BindTarget(target);
        var origin=projectile.GetGlobalProjectile<SoulSource>();origin.SourceWeapon=type;
        origin.PrimaryOrdinal=0;origin.SoulId=soul.SoulId;
    }
    private void Capture(GameTime time)
    {
        if(!Enabled||!ready||finished||Main.gameMenu||tick%3!=0||capturedTick==tick)return;
        capturedTick=tick;
        if(npcId>=0&&Main.npc[npcId].life<100000)hitObservedFrames++;
        string path=Path.Combine(root,$"scene/frame-{tick:000}.png");Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var file=File.Create(path);
        Main.screenTarget.SaveAsPng(file,Main.screenTarget.Width,Main.screenTarget.Height);
        if(DummyCheck&&tick%12==0)
        {
            // 另取最终窗口缓冲；screenTarget 本身正常不能证明后处理输出正常。
            var device=Main.graphics.GraphicsDevice;var pp=device.PresentationParameters;
            var pixels=new Color[pp.BackBufferWidth*pp.BackBufferHeight];device.GetBackBufferData(pixels);
            using var image=new Microsoft.Xna.Framework.Graphics.Texture2D(device,pp.BackBufferWidth,pp.BackBufferHeight);
            image.SetData(pixels);string final=Path.Combine(root,$"final/frame-{tick:000}.png");Directory.CreateDirectory(Path.GetDirectoryName(final));
            using var output=File.Create(final);image.SaveAsPng(output,image.Width,image.Height);
            if(PlacementOnly)
            {
                var light=Lighting.GetColor((int)player.Center.X/16,(int)player.Center.Y/16);
                File.AppendAllText(Path.Combine(root,"lighting.jsonl"),JsonSerializer.Serialize(new{tick,Main.dayTime,Main.time,
                    sky=Main.ColorOfTheSkies.PackedValue,light=light.PackedValue,Main.drawToScreen,Main.screenWidth,Main.screenHeight,
                    targetWidth=Main.screenTarget.Width,targetHeight=Main.screenTarget.Height,
                    zoom=Main.GameViewMatrix.Zoom.X,padding=BlessingCamera.RequestedPadding,
                    blackPixels=pixels.Count(c=>c.R<4&&c.G<4&&c.B<4),totalPixels=pixels.Length})+"\n");
            }
        }
    }
    public override void ModifyScreenPosition()
    {
        // 只调整隔离夹具的取景，让整趟穿刺与结束余流都留在画面内，保持正常倍率。
        if(Enabled&&ready&&!Main.gameMenu)
            Main.screenPosition=anchor+new Vector2(180,0)-new Vector2(Main.screenWidth,Main.screenHeight)*.5f;
    }
}
