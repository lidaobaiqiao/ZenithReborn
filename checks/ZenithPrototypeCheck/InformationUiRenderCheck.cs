using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 菜单中离屏调用真实魂域绘制，不读写正式角色或世界；固定输入便于检查中文排版。
public class InformationUiRenderCheck : ModSystem
{
    private bool ran;
    private const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private static void Field(string name,object value)=>typeof(SoulUI).GetField(name,Hidden)?.SetValue(null,value);
    private void Capture(GameTime time)
    {
        string root=Environment.GetEnvironmentVariable("ZENITH_INFORMATION_GPU");
        if(ran||string.IsNullOrEmpty(root)||!Main.gameMenu||Main.menuMode!=0)return;
        ran=true;Directory.CreateDirectory(root);
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        int oldWidth=Main.screenWidth,oldHeight=Main.screenHeight;
        try
        {
            Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.UIScale=1;
            var player=new Player{active=true,whoAmI=0};Main.player[0]=player;player.ResetEffects();
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.KingMemory=true;soul.Kills=SoulProgress.Eye|SoulProgress.Evil|SoulProgress.Skeleton|SoulProgress.Flesh;
            soul.Cores=3;soul.LegacyReleased=0;soul.Seals=SoulCurses.Eye|SoulCurses.Brain|SoulCurses.Flesh;
            int[] mains=[ItemID.WoodenSword,ItemID.CopperShortsword,ItemID.TinBroadsword,ItemID.EnchantedSword,ItemID.Starfury];
            for(int i=0;i<mains.Length;i++)soul.Hosts[i]=new Item(mains[i]);
            soul.SupportHosts[0]=new Item(ItemID.BorealWoodSword);soul.SupportHosts[1]=new Item(ItemID.RichMahoganySword);
            player.inventory[0]=soul.Item;player.inventory[1]=new Item(ItemID.IronBroadsword);player.inventory[2]=new Item(ItemID.IceBlade);
            soul.EnvironmentCollection=EnvironmentRules.Bit(SwordEnvironment.Forest)|EnvironmentRules.Bit(SwordEnvironment.Snow)|
                EnvironmentRules.Bit(SwordEnvironment.Space)|EnvironmentRules.Bit(SwordEnvironment.Underground)|EnvironmentRules.Bit(SwordEnvironment.Caverns);
            soul.RefreshStats();soul.RegisterEnvironments();
            SoulUI.DomainMode=true;SoulUI.Open(soul);Main.mouseItem=new Item();Main.mouseLeft=Main.mouseLeftRelease=false;
            byte[] Snapshot(){using var m=new MemoryStream();soul.NetSend(new BinaryWriter(m));return m.ToArray();}
            byte[] before=Snapshot();var images=new List<string>();var draw=typeof(SoulUI).GetMethod("Draw",Hidden);
            void Render(string name,int page,int width=1440,int height=900,bool activeOnly=true,int group=0,string selection="",int filter=0,Point? click=null,bool continuation=false,float? age=null,float? opening=null,float? bossTime=null,int? detailOffset=null)
            {
                // 正式 UI 层已经换成界面单位；离屏夹具也必须先除缩放，目标纹理仍用物理像素。
                Main.screenWidth=(int)(width/Main.UIScale);Main.screenHeight=(int)(height/Main.UIScale);Main.mouseX=Main.mouseY=-100;
                Field("<HoveredSlotRequirement>k__BackingField","");
                if(!continuation){SoulUI.SelectPage(page);SoulUI.SelectTraitScope(activeOnly);SoulUI.SelectTraitCategory(filter);Field("traitSelection",selection);Field("memoryTab",group);Field("scroll",0);}
                Main.mouseLeft=Main.mouseLeftRelease=click.HasValue;
                if(click is Point pointer){Main.mouseX=pointer.X;Main.mouseY=pointer.Y;}
                double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
                Field("pageStarted",now-1);Field("openedAt",now-(opening??3));Field("detailStarted",now-1);Field("armoryFocusAt",now-1);
                if(age.HasValue)Field("attunedAt",now-age.Value);
                if(bossTime.HasValue)Field("bossAnimationStarted",now-bossTime.Value);
                if(detailOffset.HasValue)Field("traitDetailScroll",detailOffset.Value);
                using var target=new RenderTarget2D(device,width,height);device.SetRenderTarget(target);device.Clear(Color.Black);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,Main.DefaultSamplerState,
                    DepthStencilState.None,RasterizerState.CullNone,null,Main.UIScaleMatrix);
                draw.Invoke(null,null);Main.spriteBatch.End();device.SetRenderTarget(null);
                string relative=name+".png",path=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var png=File.Create(path))target.SaveAsPng(png,width,height);images.Add(relative);
            }
            if(Environment.GetEnvironmentVariable("ZENITH_ARMORY_INTERACTION_GPU")=="1")
            {
                var interaction=ArmoryInteractionRenderCheck.Run(soul,player,(name,width,height,click)=>
                    Render(name,0,width,height,click:click,continuation:true),out bool interactionPassed);
                Render("24-trait-row",SoulUI.TraitsPage,activeOnly:false,filter:2,click:new Point(110,285));
                bool traitRow=SoulUI.SelectedTraitKey=="ore-tier-2";
                Field("growthFocus",-1);Render("25-growth-node",2,click:new Point(100,320));
                bool growthNode=(int)typeof(SoulUI).GetField("growthFocus",Hidden).GetValue(null)==0;
                Field("sealFocus",-1);Render("26-seal-card",2,group:2,click:new Point(700,320));
                bool sealCard=(int)typeof(SoulUI).GetField("sealFocus",Hidden).GetValue(null)==1;
                var environmentBefore=soul.DomainEnvironment;
                Render("27-environment",SoulUI.EnvironmentPage,click:new Point(210,176));
                bool environment=(int)typeof(SoulUI).GetField("environmentFocus",Hidden).GetValue(null)==2&&soul.DomainEnvironment==environmentBefore;
                bool snapshotUnchanged=before.SequenceEqual(Snapshot());
                bool interactionAndPagesPassed=interactionPassed&&traitRow&&growthNode&&sealCard&&environment&&snapshotUnchanged;
                File.WriteAllText(Path.Combine(root,"gpu.json"),JsonSerializer.Serialize(new{
                    passed=interactionAndPagesPassed,actualGpu=true,actualProductionUi=true,images,interaction,sharedClipPages=new{traitRow,growthNode,sealCard,environment},
                    readOnlySoulSnapshotUnchanged=snapshotUnchanged,physicalMouseTested=false,worldCombatTested=false,multiplayerTested=false,
                    testedPackageSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant()
                },new JsonSerializerOptions{WriteIndented=true}));
                Environment.Exit(interactionAndPagesPassed?0:1);return;
            }
            if(Environment.GetEnvironmentVariable("ZENITH_BOSS_UNLOCKS_GPU")=="1")
            {
                var bosses=BossUnlockUiRenderCheck.Run(soul,player,(name,curse,width,height,age,offset)=>
                    Render(name,SoulUI.TraitsPage,width,height,activeOnly:false,selection:"curse-"+curse,filter:4,bossTime:age,detailOffset:offset),out bool bossPassed);
                Field("sealFocus",1);Render("boss-growth-seals",2,group:2);
                Render("boss-growth-seals-small",2,640,720,group:2);
                bool snapshotUnchanged=before.SequenceEqual(Snapshot());
                File.WriteAllText(Path.Combine(root,"gpu.json"),JsonSerializer.Serialize(new{
                    passed=bossPassed&&snapshotUnchanged,actualGpu=true,actualProductionUi=true,images,bosses,
                    readOnlySoulSnapshotUnchanged=snapshotUnchanged,physicalMouseTested=false,worldCombatTested=false,multiplayerTested=false,
                    testedPackageSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant()
                },new JsonSerializerOptions{WriteIndented=true}));
                Environment.Exit(bossPassed&&snapshotUnchanged?0:1);return;
            }
            Render("armory",0);
            Render("armory-compact",0,960,720);
            var armory=ArmoryUiRenderCheck.Run(soul,player,(name,width,height,click,age,opening)=>
                Render(name,0,width,height,click:click,continuation:true,age:age,opening:opening),out bool armoryPassed);
            Render("traits-active",SoulUI.TraitsPage);
            Render("traits-all",SoulUI.TraitsPage,activeOnly:false);
            Render("traits-selected",SoulUI.TraitsPage,activeOnly:false,selection:"ore-wave",filter:2);
            Render("growth-route",2);Render("growth-cores",2,group:1);Render("growth-seals",2,group:2);
            Render("environment",SoulUI.EnvironmentPage);Render("compact-traits",SoulUI.TraitsPage,640,720,activeOnly:false);
            Render("compact-environment",SoulUI.EnvironmentPage,640,720);
            Render("scope-click",SoulUI.TraitsPage,click:new Point(1320,105));
            bool scopeSwitched=!SoulUI.ActiveTraitsOnly;
            Render("category-click",SoulUI.TraitsPage,activeOnly:false,click:new Point(450,158));
            bool filterClicked=SoulUI.TraitCategoryFilter==2;
            Render("row-click",SoulUI.TraitsPage,activeOnly:false,filter:2,click:new Point(110,285));
            bool rowSelected=SoulUI.SelectedTraitKey=="ore-tier-2";
            Render("growth-tab-click",2,click:new Point(330,197));
            bool growthSwitched=(int)typeof(SoulUI).GetField("memoryTab",Hidden).GetValue(null)==1;
            var domainBefore=soul.DomainEnvironment;
            Render("environment-select-click",SoulUI.EnvironmentPage,click:new Point(210,176));
            bool environmentSelected=(int)typeof(SoulUI).GetField("environmentFocus",Hidden).GetValue(null)==2&&soul.DomainEnvironment==domainBefore;
            bool unchanged=before.SequenceEqual(Snapshot());
            bool passed=unchanged&&scopeSwitched&&filterClicked&&rowSelected&&growthSwitched&&environmentSelected&&armoryPassed;
            File.WriteAllText(Path.Combine(root,"gpu.json"),JsonSerializer.Serialize(new {
                passed,actualGpu=true,actualProductionUi=true,images,readOnlySoulSnapshotUnchanged=unchanged,
                syntheticInputReplay=new{scopeSwitched,filterClicked,rowSelected,growthSwitched,environmentSelected},armory,
                controlledRevealFrames=28,physicalMouseTested=false,worldCombatTested=false,multiplayerTested=false,
                testedPackageSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant()
            },new JsonSerializerOptions{WriteIndented=true}));
            Environment.Exit(passed?0:1);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(root,"failure.txt"),ex.ToString());Environment.Exit(1);}
        finally{device.SetRenderTargets(saved);device.Viewport=viewport;Main.screenWidth=oldWidth;Main.screenHeight=oldHeight;}
    }
}
