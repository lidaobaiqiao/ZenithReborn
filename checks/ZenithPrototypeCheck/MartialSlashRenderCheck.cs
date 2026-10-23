using System.Text.Json;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// Offscreen captures of the real renderer in a disposable menu-only client.
// No desktop controls, world loads, player saves or gameplay automation.
public class MartialSlashRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_SLASH_RENDER");
        if(ran || string.IsNullOrEmpty(output) || !Main.gameMenu || Main.menuMode!=0 ||
            !Main.SavePath.Contains("slash-render-profile",StringComparison.OrdinalIgnoreCase) || !ShaderManager.HasFinishedLoading)return;
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
                // Dust.NewDust rejects menu emission. Temporarily allow only this
                // synchronous allocation; no game tick, player or world is loaded.
                Main.gameMenu=false;Main.gamePaused=false;
                var pooled=SoulFlowSpark.Spawn(new(new(440,350),new(6,1),Color.Cyan,1,24,false));
                nativeDust=pooled is {active:true} && pooled.type==ModContent.DustType<SoulFlowSpark>() && pooled.customData is FlowSparkSpawn;
                if(pooled!=null)for(int i=0;i<30;i++)ModContent.GetInstance<SoulFlowSpark>().Update(pooled);
                nativeDust&=pooled is {active:false};
            }
            finally{Main.gameMenu=wasMenu;Main.gamePaused=wasPaused;}
            OreRenderCheck.Run(output);
            byte[] package=File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod"));
            string packageHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(package)).ToLowerInvariant();
            File.WriteAllText(Path.Combine(output,"slash-gpu.json"),JsonSerializer.Serialize(new{
                passed=after>10000 && material>6000 && faded==0 && residue>0 && nativeDust && restored &&
                    frameHashes["slash-tier3.png"]!=frameHashes["slash-flow-shift.png"] &&
                    frameHashes["slash-tier3.png"]!=frameHashes["slash-flow-only.png"],
                version=ModLoader.GetMod("ZenithReborn").Version.ToString(),
                tested_package_sha256=packageHash,
                renderer="Reused MIT Onikiri crescent material + native flow + Luminance edge + vanilla Dust",
                bladeFaceColoredPixels=material,
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
                void Emit(FlowSparkSpawn spawn){var d=new Dust{active=true};SoulFlowSpark.Configure(d,spawn);particles.Add(d);}
                void StepParticles(){foreach(var d in particles)if(d.active)SoulFlowSpark.Step(d);}
                for(int tick=1;tick<=age;tick++)
                {
                    rotation=MathHelper.Lerp(-1.35f,1.45f,MathHelper.SmoothStep(0,1,Math.Min(1,tick/18f)));
                    trail.Update(pivot,rotation,true,false);
                    StepParticles();
                    if(!legacy && includeParticles)emitter.Update(pivot,rotation,tier,tick,true,Emit);
                }
                for(int tick=0;tick<fade;tick++){trail.Update(pivot,rotation,false,true);StepParticles();}
                var initialBlend=device.BlendState;var initialDepth=device.DepthStencilState;
                var initialRaster=device.RasterizerState;var initialScissor=device.ScissorRectangle;
                var initialTextures=Enumerable.Range(0,3).Select(i=>device.Textures[i]).ToArray();
                var initialSamplers=Enumerable.Range(0,3).Select(i=>device.SamplerStates[i]).ToArray();
                if(!legacy)
                    MartialSlashRenderer.DrawWithRestoredState(()=>trail.DrawPrimitives(tier,width,height,true,visualTime??age/60f));
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
                    MartialVisual.Sword(ItemID.Zenith,pivot+rotation.ToRotationVector2()*145,
                        rotation+MathHelper.PiOver4,300,tier==3?MartialVisual.Violet:MartialVisual.Cyan,1-fade/(float)MartialSlashTrail.Lifetime);
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
