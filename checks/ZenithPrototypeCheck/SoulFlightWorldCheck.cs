using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 在专用隔离世界中走生产主飞剑AI与绘制，检查三种颜色、实际命中与返回收尾。
// 由夹具安排发射，不能称鼠标攻击或自然游玩；不放置任何假人。
public class SoulFlightWorldCheck : ModSystem
{
    private bool entering,ready,finished;
    private int tick,npcId=-1,captured=-1;
    private Player player;
    private SoulItem soul;
    private Vector2 anchor;
    private string output;
    private int refractionMaskPixels;
    private int materialLightCalls;
    private string LightPreset=>Environment.GetEnvironmentVariable("ZENITH_WORLD_LIGHT_PRESET")??"day";
    private readonly List<object> ambientSamples=new();
    private int forcedEndingId=-1;
    private bool endingAfterParentKill;
    private Texture2D regionMask;
    private bool Advanced=>Environment.GetEnvironmentVariable("ZENITH_SURFACE_WORLD")=="1";
    internal bool RealInput=>Enabled&&Environment.GetEnvironmentVariable("ZENITH_SOUL_INPUT_CHECK")=="1";
    // 真实输入核对先等待观察窗口完成，再由外部放置准备标记；避免启动时遗留按键被误记为本轮攻击。
    internal bool InputArmed=>Environment.GetEnvironmentVariable("ZENITH_REQUIRE_INPUT_ARM")!="1"||
        output!=null&&File.Exists(Path.Combine(output,"input-arm"));
    private int inputTick=-1;
    private readonly List<object> inputTrace=new();
    private readonly HashSet<int> damaged=new();
    private readonly HashSet<int> returned=new();
    private readonly List<object> poses=new();
    private readonly Dictionary<int,int> weapons=new();
    private readonly Dictionary<int,int> echoTargets=new();
    private readonly HashSet<int> echoHits=new();
    private readonly List<object> echoHitRecords=new();
    private bool EchoDamage=>Enabled&&Environment.GetEnvironmentVariable("ZENITH_ECHO_DAMAGE_CHECK")=="1";
    private bool echoProbesCreated;
    private int echoProbeBlade=-1;
    private bool Enabled=>!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZENITH_SOUL_FLIGHT_WORLD")) &&
        Main.SavePath.Contains("soul-flight-world-profile",StringComparison.OrdinalIgnoreCase);
    internal bool Isolated=>Enabled;
    internal int VisualTick=>tick;
    public override void Load(){if(!Main.dedServ){Main.OnPreDraw+=Enter;Main.OnPostDraw+=Capture;On_Lighting.AddLight_int_int_float_float_float+=ObserveLight;}}
    public override void Unload(){Main.OnPreDraw-=Enter;Main.OnPostDraw-=Capture;On_Lighting.AddLight_int_int_float_float_float-=ObserveLight;var old=regionMask;regionMask=null;Main.QueueMainThreadAction(()=>old?.Dispose());}
    private void ObserveLight(On_Lighting.orig_AddLight_int_int_float_float_float orig,int x,int y,float red,float green,float blue)
    {
        if(Enabled&&Advanced&&ready&&red*red+green*green+blue*blue>.01f)
            foreach(var p in Main.ActiveProjectiles)
                if(p.ModProjectile is SoulBlade&&x==(int)(p.Center.X/16)&&y==(int)(p.Center.Y/16)){materialLightCalls++;break;}
        orig(x,y,red,green,blue);
    }
    private void Enter(GameTime time)
    {
        if(!Enabled || entering || !Main.gameMenu || Main.menuMode!=0)return;
        entering=true;output=Environment.GetEnvironmentVariable("ZENITH_SOUL_FLIGHT_WORLD");Directory.CreateDirectory(output);
        Main.instance.Window.Title=RealInput?"飞剑真实输入核对 · 隔离":Advanced?"天顶材质世界核对 · 隔离":"主飞剑回归 · 隔离";
        try
        {
            Main.LoadPlayers();Main.LoadWorlds();
            var pf=Main.PlayerList.Find(p=>p.Name=="测试" && !p.IsCloudSave);
            var wf=Main.WorldList.Find(w=>Path.GetFileNameWithoutExtension(w.Path)=="测试" && !w.IsCloudSave);
            if(pf==null || wf==null)throw new Exception("隔离目录缺少本地测试副本");
            if(!Path.GetFullPath(pf.Path).StartsWith(Main.SavePath,StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFullPath(wf.Path).StartsWith(Main.SavePath,StringComparison.OrdinalIgnoreCase))
                throw new Exception("禁止加载隔离目录外的存档");
            pf.SetAsActive();wf.SetAsActive();Main.autoSave=false;Main.netMode=NetmodeID.SinglePlayer;
            Main.menuMode=10;WorldGen.playWorld();
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());Main.instance.Exit();}
    }
    public override void PostUpdateEverything()
    {
        if(RealInput&&output!=null&&File.Exists(Path.Combine(output,"exit"))){Main.instance.Exit();return;}
        if(!Enabled || Main.gameMenu || finished)return;
        if(!ready)
        {
            ready=true;player=Main.LocalPlayer;anchor=player.Center-new Vector2(0,200);
            Main.dayTime=true;Main.time=27000;Main.hideUI=true;
            soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;
            if(RealInput){soul.Hosts[0]=new Item(ItemID.TerraBlade);soul.Hosts[1]=new Item(ItemID.EnchantedSword);soul.Hosts[2]=new Item(ItemID.Starfury);soul.RefreshStats();}
            player.inventory[0]=soul.Item;player.selectedItem=0;player.itemAnimation=player.itemTime=0;
            npcId=NPC.NewNPC(player.GetSource_Misc("SoulFlightWorldCheck"),(int)anchor.X+360,(int)anchor.Y,NPCID.Zombie);
            if(Advanced)
            {
                var material=new SurfaceMaterialDefinition{UseWeaponColor=true,EmissionStrength=.35f,WorldLightStrength=.7f,ClearCoat=.25f};
                // 只在隔离夹具启用分区，确认真实飞剑的各材质数据通道能共同合成。
                material.DetailLayers=[new(){Weight=.3f,Blend=1,RoughnessDelta=.5f},new(){Weight=.15f,Tint=new(.1f,.3f,.5f),EmissionStrength=.3f}];
                regionMask=new Texture2D(Main.graphics.GraphicsDevice,64,64);var pixels=new Color[64*64];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=new Color(y<16?1f:0,x>32?1f:0,0,0);
                regionMask.SetData(pixels);
                foreach(int type in new[]{ItemID.TerraBlade,ItemID.EnchantedSword,ItemID.Starfury})
                    SwordFlowMaterial.SetMaterialBinding(type,material,new(){LayerMask=regionMask});
            }
        }
        player.Center=anchor;player.velocity=Vector2.Zero;player.immune=true;player.immuneTime=60;
        // 同一隔离档与攻击姿态只切换世界照明；局部光源用原版照明接口注入，不给材质伪造亮度。
        Main.dayTime=LightPreset is "day" or "dusk";
        Main.time=LightPreset=="dusk"?51840:Main.dayTime?27000:16200;
        if(LightPreset=="torch")Lighting.AddLight(anchor+new Vector2(180,0),1.8f,1.4f,.9f);
        // 天顶椭圆在前端折返，目标放在真实端点；椭圆内部不等于剑体经过的位置。
        var target=Main.npc[npcId];target.Center=anchor+new Vector2(360,0);target.velocity=Vector2.Zero;
        target.noGravity=true;target.noTileCollide=true;target.knockBackResist=0;target.defense=0;
        if(RealInput)
        {
            target.life=target.lifeMax=100000;
            var blades=Main.projectile.Where(p=>p.active&&p.ModProjectile is SoulBlade).ToArray();
            if(inputTick<0&&blades.Length>0)inputTick=0;
            if(inputTick>=0)
            {
                inputTrace.Add(new{tick=inputTick,mouseLeft=Main.mouseLeft,player.controlUseItem,player.itemAnimation,
                    created=SoulTrailEnding.Created,endings=SoulTrailEnding.ActiveCount,composite=SoulSceneRefraction.CompositingReady,
                    blades=blades.Select(p=>new{id=p.whoAmI,type=(int)p.ai[0],phase=((SoulBlade)p.ModProjectile).MotionPhase,retired=((SoulBlade)p.ModProjectile).Retired}).ToArray()});
                tick=inputTick++;
                if(inputTick==180)
                {
                    finished=true;File.WriteAllText(Path.Combine(output,"input.json"),JsonSerializer.Serialize(new{actualMouseAttack=true,
                        testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant(),
                        trace=inputTrace},new JsonSerializerOptions{WriteIndented=true}));
                }
            }
            if(File.Exists(Path.Combine(output,"exit")))Main.instance.Exit();
            return;
        }
        if(tick is 20 or 130 or 240 or 340)
        {
            int type=tick is 20 or 340?ItemID.TerraBlade:tick==130?ItemID.EnchantedSword:ItemID.Starfury;
            soul.Hosts[0]=new Item(type);soul.RefreshStats();target.life=target.lifeMax=100000;
            int id=soul.EmitBlade(player,soul.Hosts[0],0,1);
            if(id<Main.maxProjectiles)
            {
                var blade=(SoulBlade)Main.projectile[id].ModProjectile;
                blade.TargetOffset=new Vector2(360,0);blade.Vertical=60;
                weapons[id]=type;
                if(tick==340)forcedEndingId=id;
            }
        }
        // 隔离夹具额外检查提前收回后父弹幕立即消失，材质仍可正常收尽。
        if(EchoDamage&&!echoProbesCreated)
        {
            foreach(var projectile in Main.ActiveProjectiles)
            {
                if(projectile.ModProjectile is not SoulBlade blade||blade.MotionPhase<73||blade.MotionPhase>79)continue;
                echoProbesCreated=true;
                echoProbeBlade=projectile.whoAmI;
                for(int echo=1;echo<SoulTrailUnits.Count;echo++)
                {
                    if(!blade.TryGetEchoPose(echo,out var center,out _,out _))continue;
                    int id=NPC.NewNPC(player.GetSource_Misc("EchoDamageCheck"),(int)center.X,(int)center.Y,NPCID.Zombie);
                    if(id>=Main.maxNPCs)continue;
                    var npc=Main.npc[id];npc.width=npc.height=40;npc.Center=center;npc.life=npc.lifeMax=100000;
                    npc.noGravity=npc.noTileCollide=true;npc.knockBackResist=0;npc.defense=0;npc.velocity=Vector2.Zero;
                    echoTargets[id]=echo;
                }
                break;
            }
        }
        // 使用常见敌人体量并跟随未命中的历史姿态，避免高速相邻更新跨过微小夹具目标。
        // 这里只安排NPC的位置，扣血仍必须由下一次生产Projectile.Update触发。
        if(EchoDamage&&echoProbeBlade>=0&&Main.projectile[echoProbeBlade].active&&Main.projectile[echoProbeBlade].ModProjectile is SoulBlade probeBlade)
            foreach(var pair in echoTargets)
                if(!echoHits.Contains(pair.Value)&&Main.npc[pair.Key].active&&probeBlade.TryGetEchoPose(pair.Value,out var at,out _,out _))
                {Main.npc[pair.Key].Center=at;Main.npc[pair.Key].velocity=Vector2.Zero;}
        if(tick==100)foreach(int id in echoTargets.Keys)Main.npc[id].active=false;
        if(forcedEndingId>=0&&tick==350&&Main.projectile[forcedEndingId].active)
            ((SoulBlade)Main.projectile[forcedEndingId].ModProjectile).Retire();
        if(forcedEndingId>=0&&tick==353&&Main.projectile[forcedEndingId].active)Main.projectile[forcedEndingId].Kill();
        foreach(var projectile in Main.ActiveProjectiles)
        {
            if(projectile.ModProjectile is not SoulBlade blade || !weapons.TryGetValue(projectile.whoAmI,out int type))continue;
            if(blade.MotionPhase>=84 && !blade.Retired)returned.Add(type);
            poses.Add(new{tick,type,phase=blade.MotionPhase,blade.Retired,
                x=projectile.Center.X,y=projectile.Center.Y,rotation=projectile.rotation,
                trailVisibility=blade.Retired?(float?)null:SoulMaterialTrail.Visibility(blade.MotionPhase,false,0)});
        }
        tick++;
        if(tick>=390)
        {
            finished=true;
            File.WriteAllText(Path.Combine(output,"world.json"),JsonSerializer.Serialize(new{
                passed=damaged.Count==3 && returned.Count==3 && SoulSceneRefraction.CapturedFrames>0 && refractionMaskPixels>0&&(!Advanced||materialLightCalls>0)&&endingAfterParentKill&&SoulTrailEnding.Created>=4&&!SoulTrailEnding.HasActive&&(!EchoDamage||echoHits.Count==3),
                testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod"))))
                    .ToLowerInvariant(),
                actualWorld=true,productionAI=true,productionRenderer=true,isolatedSaves=true,
                lightPreset=LightPreset,ambientSamples,
                refractionFrames=SoulSceneRefraction.CapturedFrames,refractionDeferredFrames=SoulSceneRefraction.DeferredFrames,
                refractionSource=SoulSceneRefraction.LastSource,
                refractionMaskPixels,
                advancedSurfaceMaterial=Advanced,materialRegionsActive=Advanced&&regionMask!=null,materialLightCalls,
                endingAfterParentKill,endingsCreated=SoulTrailEnding.Created,remainingEndings=SoulTrailEnding.ActiveCount,
                echoDamageTested=EchoDamage,echoDamageUnits=echoHits,echoHitRecords,echoTargetsFollowPoses=EchoDamage,
                generatedFixtureAttack=true,actualMouseAttack=false,superDummyPlacementTested=false,
                damageEvidence="原版命中回调中确认目标受到SoulBlade直接命中，不计衍生攻击",
                damagedItems=damaged,completedReturnItems=returned,frames=tick,poses,
                liveMultiplayerTested=false,artApproved=false},new JsonSerializerOptions{WriteIndented=true}));
            Main.instance.Exit();
        }
    }
    internal void ObserveHit(NPC npc,Projectile projectile,int damage)
    {
        if(EchoDamage&&ready&&!finished&&damage>0&&projectile.ModProjectile is SoulBlade blade&&echoTargets.TryGetValue(npc.whoAmI,out int echo)
            &&!blade.BodyCollides(npc.Hitbox)&&blade.TryGetEchoPose(echo,out var center,out _,out _))
        {
            echoHits.Add(echo);
            echoHitRecords.Add(new{tick,echo,damage,phase=blade.MotionPhase,bodyCanDamage=blade.BodyCanDamage,
                bodyDistance=Vector2.Distance(projectile.Center,npc.Center),echoDistance=Vector2.Distance(center,npc.Center)});
        }
        if(Enabled && ready && !finished && npc.whoAmI==npcId && damage>0 &&
            projectile.ModProjectile is SoulBlade && weapons.TryGetValue(projectile.whoAmI,out int type))
            damaged.Add(type);
    }
    private void Capture(GameTime time)
    {
        if(Enabled&&ready&&!finished&&!Main.gameMenu&&forcedEndingId>=0&&!Main.projectile[forcedEndingId].active&&SoulTrailEnding.HasActive&&SoulSceneRefraction.CompositingReady)
            endingAfterParentKill=true;
        if(!Enabled || !ready || finished || Main.gameMenu || (RealInput?inputTick<0:tick%3!=0) || captured==tick)return;
        captured=tick;var device=Main.graphics.GraphicsDevice;var pp=device.PresentationParameters;
        // 79基底没有89的照明采样诊断接口；世界昼夜预设继续保留。
        var pixels=new Color[pp.BackBufferWidth*pp.BackBufferHeight];device.GetBackBufferData(pixels);
        using var image=new Texture2D(device,pp.BackBufferWidth,pp.BackBufferHeight);image.SetData(pixels);
        string path=Path.Combine(output,$"frames/frame-{tick:000}.png");Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var stream=File.Create(path);image.SaveAsPng(stream,image.Width,image.Height);
        if(tick is 24 or 27 or 30 or 42 or 48 or 54 or 60 && SoulSceneRefraction.NormalMask is Texture2D normal)
        {
            var maskPixels=new Color[normal.Width*normal.Height];normal.GetData(maskPixels);
            if(tick==42)refractionMaskPixels=maskPixels.Count(color=>color.A>8);
            using var png=File.Create(Path.Combine(output,$"refraction-mask-{tick:000}.png"));normal.SaveAsPng(png,normal.Width,normal.Height);
        }
    }
    public override void ModifyScreenPosition()
    {
        if(Enabled && ready && !Main.gameMenu)
            Main.screenPosition=anchor+new Vector2(180,0)-new Vector2(Main.screenWidth,Main.screenHeight)*.5f;
    }
}

// 仅记录夹具中的真实主剑命中；不会改写伤害或生产战斗状态。
public class SoulFlightHitCheck : GlobalNPC
{
    public override void OnHitByProjectile(NPC npc,Projectile projectile,NPC.HitInfo hit,int damageDone)
        =>ModContent.GetInstance<SoulFlightWorldCheck>().ObserveHit(npc,projectile,damageDone);
}

// 受控夹具不接受窗口遗留的物品使用输入，避免额外挥剑触发既有提前归鞘规则。
// 只在隔离路径与显式环境变量同时满足时启用，不影响正式游玩。
public class SoulFlightFixtureInput : ModPlayer
{
    public override void SetControls()
    {
        var check=ModContent.GetInstance<SoulFlightWorldCheck>();
        if(!check.Isolated || check.RealInput&&check.InputArmed || Main.gameMenu)return;
        Player.controlUseItem=false;Player.controlUseTile=false;Player.releaseUseItem=true;
    }
}
