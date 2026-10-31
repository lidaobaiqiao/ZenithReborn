using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 隔离菜单客户端回放当前原版流光绘制，不操作桌面、世界或正式存档。
public class MartialSlashRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_SLASH_RENDER");
        if(ran || string.IsNullOrEmpty(output) || !Main.gameMenu || Main.menuMode!=0 ||
            !Main.SavePath.Contains("slash-render-profile",StringComparison.OrdinalIgnoreCase))return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;
        var targets=device.GetRenderTargets();var viewport=device.Viewport;var screen=Main.screenPosition;
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        var scissor=device.ScissorRectangle;var sampler=device.SamplerStates[0];var texture=device.Textures[0];
        var vertices=device.GetVertexBuffers();var indices=device.Indices;
        try
        {
            Main.screenPosition=Vector2.Zero;Main.instance.LoadItem(ItemID.Zenith);
            var samples=new List<object>();bool restored=true;
            var frameHashes=new Dictionary<string,string>();
            int before=Frame(2,12,0,true,"slash-before.png");
            int after=Frame(2,12,0,false,"slash-tier2.png");
            Frame(3,12,0,false,"slash-flow-only.png",null,false);
            Frame(3,12,0,false,"slash-tier3.png");
            int material=Frame(3,12,0,false,"slash-material.png",null,false,false);
            Frame(3,12,0,false,"slash-flow-shift.png",.7f);
            Frame(3,6,0,false,"slash-early.png");
            Frame(3,17,0,false,"slash-late.png");
            int residue=Frame(3,22,20,false,"slash-particle-residue.png");
            int faded=Frame(3,22,50,false,"slash-expired.png");
            for(int tick=1;tick<=22;tick++)Frame(3,tick,0,false,$"frames/slash-{tick:000}.png",tick/60f);
            for(int tick=1;tick<=50;tick++)Frame(3,22,tick,false,$"frames/slash-{22+tick:000}.png",(22+tick)/60f);
            bool nativeDust=false,wasMenu=Main.gameMenu,wasPaused=Main.gamePaused;
            try
            {
                // 菜单中只临时允许此次同步粒子分配，不更新游戏或加载人物世界。
                Main.gameMenu=false;Main.gamePaused=false;
                var pooled=SoulFlowSpark.Spawn(new(new(440,350),new(6,1),Color.Cyan,1,24,false));
                nativeDust=pooled is {active:true} && pooled.type==ModContent.DustType<SoulFlowSpark>() && pooled.customData is FlowSparkSpawn;
                if(pooled!=null)for(int i=0;i<30;i++)ModContent.GetInstance<SoulFlowSpark>().Update(pooled);
                nativeDust&=pooled is {active:false};
            }
            finally{Main.gameMenu=wasMenu;Main.gamePaused=wasPaused;}
            byte[] package=File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod"));
            string packageHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(package)).ToLowerInvariant();
            File.WriteAllText(Path.Combine(output,"slash-gpu.json"),JsonSerializer.Serialize(new{
                passed=after>0 && material>0 && faded==0 && residue>0 && nativeDust && restored &&
                    frameHashes["slash-tier3.png"]!=frameHashes["slash-flow-shift.png"] &&
                    frameHashes["slash-tier3.png"]!=frameHashes["slash-flow-only.png"],
                version=ModLoader.GetMod("ZenithReborn").Version.ToString(),
                tested_package_sha256=packageHash,
                renderer="原版流光、细刃与原版资源粒子；第三方厚月牙已删除",
                legacyCrescentPresent=ModLoader.GetMod("ZenithReborn").FileExists("Assets/ThirdParty/CalamityOverhaul/OniCrimsonSlash.fxc"),
                nativeFlowColoredPixels=material,
                nativeDustSpawnAndExpiry=nativeDust,detachedParticlePixels=residue,previewFrameCount=72,
                particleVisualDifference=frameHashes["slash-tier3.png"]!=frameHashes["slash-flow-only.png"],
                flowingTextureAnimated=frameHashes["slash-tier3.png"]!=frameHashes["slash-flow-shift.png"],
                beforeColoredPixels=before,afterColoredPixels=after,expiredColoredPixels=faded,
                graphicsStateRestored=restored,samples,actualGpu=true,actualSharedRenderer=true,
                gameplayTested=false,liveMultiplayerTested=false},new JsonSerializerOptions{WriteIndented=true}));

            int Frame(int tier,int age,int fade,bool legacy,string filename,float? visualTime=null,bool includeParticles=true,bool includeBlade=true)
            {
                const int width=960,height=720;var pivot=new Vector2(440,350);
                using var target=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None);
                device.SetRenderTarget(target);device.Clear(new Color(12,17,29));
                var trail=new MartialSlashTrail();float rotation=0;
                var emitter=new SlashSparkEmitter(42);var particles=new List<Dust>();
                var evaps=new List<Dust>();
                var rng=new Random(42);
                float Rand(float a,float b)=>a+(b-a)*(float)rng.NextDouble();
                // 月牙扫过轮廓上的点：角度覆盖整段挥砍，半径沿剑身。
                // 圆圈从整个轮廓脱离，而不是只在最终剑线上。
                Vector2 RimPoint(float rMin,float rMax){
                    float ang=Rand(-1.35f,1.45f);
                    float radius=Rand(rMin,rMax);
                    return pivot+ang.ToRotationVector2()*radius;
                }
                void Emit(FlowSparkSpawn spawn){var d=new Dust{active=true};SoulFlowSpark.Configure(d,spawn);particles.Add(d);}
                void StepParticles(){foreach(var d in particles)if(d.active)SoulFlowSpark.Step(d);}
                // 菜单里 Dust.NewDust 会被拒绝，生成时临时放开。
                Dust SpawnEvap(Vector2 pos,Vector2 vel,Color tint,float size,int life){
                    bool wasMenu=Main.gameMenu;Main.gameMenu=false;
                    try{return EvapCircle.Spawn(new EvapCircleSpawn(pos,vel,tint,size,life));}
                    finally{Main.gameMenu=wasMenu;}
                }
                // 挥砍结束的爆发，随后是持续小圆圈从月牙轮廓脱离。
                void EvapBurst(){
                    for(int i=0;i<16;i++)
                        evaps.Add(SpawnEvap(RimPoint(120,300),
                            new Vector2(Rand(-.25f,.25f),Rand(-1.6f,-.7f)),
                            new Color(235,242,255,180),Rand(.45f,.85f),26));
                }
                void EvapTrickle(){
                    for(int i=0;i<3;i++)
                        evaps.Add(SpawnEvap(RimPoint(120,300),
                            new Vector2(Rand(-.2f,.2f),Rand(-1.4f,-.6f)),
                            new Color(235,242,255,160),Rand(.35f,.7f),22));
                }
                void StepEvaps(){for(int i=evaps.Count-1;i>=0;i--){var d=evaps[i];if(d.active)EvapCircle.Step(d);else evaps.RemoveAt(i);}}
                for(int tick=1;tick<=age;tick++)
                {
                    rotation=MathHelper.Lerp(-1.35f,1.45f,MathHelper.SmoothStep(0,1,Math.Min(1,tick/18f)));
                    trail.Update(pivot,rotation,true,false);
                    StepParticles();
                    if(!legacy && includeParticles)emitter.Update(pivot,rotation,tier,tick,true,Emit);
                }
                if(fade>0 && !legacy)EvapBurst();
                for(int tick=0;tick<fade;tick++){
                    trail.Update(pivot,rotation,false,true);StepParticles();
                    if(!legacy && includeParticles){if(tick<16)EvapTrickle();StepEvaps();}
                }
                var initialBlend=device.BlendState;var initialDepth=device.DepthStencilState;
                var initialRaster=device.RasterizerState;var initialScissor=device.ScissorRectangle;
                var initialTextures=Enumerable.Range(0,3).Select(i=>device.Textures[i]).ToArray();
                var initialSamplers=Enumerable.Range(0,3).Select(i=>device.SamplerStates[i]).ToArray();
                if(!legacy)
                    VfxRenderState.DrawWithRestoredState(()=>trail.DrawPrimitives(tier,width,height,true,visualTime??age/60f));
                restored&=device.BlendState==initialBlend && device.DepthStencilState==initialDepth &&
                    device.RasterizerState==initialRaster && device.ScissorRectangle==initialScissor;
                for(int slot=0;slot<3;slot++)restored&=device.Textures[slot]==initialTextures[slot] &&
                    device.SamplerStates[slot]==initialSamplers[slot];
                if(includeBlade && fade<MartialSlashTrail.Lifetime)
                {
                    Main.spriteBatch.Begin();
                    if(legacy)
                    {
                        for(int tick=age;tick>=Math.Max(1,age-11);tick--)
                        {
                            float angle=MathHelper.Lerp(-1.35f,1.45f,MathHelper.SmoothStep(0,1,tick/18f));
                            Vector2 axis=angle.ToRotationVector2();
                            MartialVisual.Line(pivot+axis*45,pivot+axis*330,MartialVisual.Cyan*((1-(age-tick)/12f)*.15f),8);
                        }
                    }
                    else trail.DrawSwordGhosts(tier);
                    // 蒸发曲线：剑身随月牙 shader 侵蚀同步淡出，并向暖白过渡。
                    Color bladeTint;float bladeOpacity;
                    if(fade>0 && !legacy){
                        float t=Math.Clamp(fade/16f,0,1);
                        bladeOpacity=MathF.Pow(1-t,.7f);
                        bladeTint=Color.Lerp(tier==3?MartialVisual.Violet:MartialVisual.Cyan,new Color(235,242,255),t);
                    }else{
                        bladeTint=tier==3?MartialVisual.Violet:MartialVisual.Cyan;
                        bladeOpacity=1f;
                    }
                    MartialVisual.Sword(ItemID.Zenith,pivot+rotation.ToRotationVector2()*145,
                        rotation+MathHelper.PiOver4,300,bladeTint,bladeOpacity);
                    Main.spriteBatch.End();
                }
                // 绘制向上飘、边飘边缩小的蒸发小圆圈，走真实 EvapCircle 粒子。
                if(evaps.Count>0){
                    Main.spriteBatch.Begin();
                    foreach(var d in evaps)EvapCircle.Draw(Main.spriteBatch,d);
                    Main.spriteBatch.End();
                }
                Main.spriteBatch.Begin();
                foreach(var d in particles)SoulFlowSpark.DrawSpark(Main.spriteBatch,d);
                Main.spriteBatch.End();
                device.SetRenderTarget(null);
                string framePath=Path.Combine(output,filename);Directory.CreateDirectory(Path.GetDirectoryName(framePath));
                using(var stream=File.Create(framePath))target.SaveAsPng(stream,width,height);
                var colors=new Color[width*height];target.GetData(colors);int colored=0;
                foreach(var c in colors)if(c.R>25 || c.G>30 || c.B>42)colored++;
                frameHashes[filename]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(colors.AsSpan())));
                samples.Add(new{filename,tier,age,fade,coloredPixels=colored,history=trail.Count,particles=particles.Count(d=>d.active)});
                return colored;
            }
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"slash-gpu-error.txt"),ex.ToString());}
        finally
        {
            Main.screenPosition=screen;device.SetRenderTargets(targets);device.Viewport=viewport;
            device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=raster;
            device.ScissorRectangle=scissor;device.SamplerStates[0]=sampler;device.Textures[0]=texture;
            device.SetVertexBuffers(vertices);device.Indices=indices;
            Main.instance.Exit();
        }
    }
}
