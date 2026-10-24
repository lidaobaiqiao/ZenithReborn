using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 隔离菜单客户端的实际GPU绘制：弯折带面、定姿态材质动画和多剑配色。
// 这是生产渲染函数的受控回放，不能作为世界攻击或鼠标放置的证据。
public class SoulTrailRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_SOUL_TRAIL_RENDER");
        if(ran || string.IsNullOrEmpty(output) || !Main.gameMenu || Main.menuMode!=0 ||
            !Main.SavePath.Contains("soul-trail-render-profile",StringComparison.OrdinalIgnoreCase))return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;
        var targets=device.GetRenderTargets();var viewport=device.Viewport;var camera=Main.screenPosition;
        var vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxVertices];
        var centers=new Vector2[SoulMaterialTrail.HistoryLength];var rotations=new float[centers.Length];
        var palette=new[]{ItemID.TerraBlade,ItemID.EnchantedSword,ItemID.Starfury,ItemID.GoldBroadsword};
        var hashes=new List<string>();bool restored=true;int lastPixels=0;
        try
        {
            Main.screenPosition=Vector2.Zero;
            foreach(int item in palette)Main.instance.LoadItem(item);
            for(int frame=0;frame<96;frame++)Frame($"frames/trail-{frame:000}.png",frame/60f,frame,1,true);
            string fixedA=Frame("fixed-a.png",0,36,1,false);
            string fixedB=Frame("fixed-b.png",.32f,36,1,false);
            for(int i=0;i<palette.Length;i++)hashes.Add(Frame($"palette-{i}.png",.2f,36,1,false,i));
            int endingStart=0,endingMiddle=0,endingFinal=0,formationStart=0,formationFinal=0;
            for(int frame=0;frame<=SoulTrailEnding.NormalFrames;frame++)
            {
                Frame($"ending/end-{frame:000}.png",frame/60f,36,SoulTrailEnding.Opacity(frame,SoulTrailEnding.NormalFrames),false,
                    dissolve:SoulTrailEnding.Dissolve(frame,SoulTrailEnding.NormalFrames));
                if(frame==0)endingStart=lastPixels;
                if(frame==SoulTrailEnding.NormalFrames/2)endingMiddle=lastPixels;
                if(frame==SoulTrailEnding.NormalFrames)endingFinal=lastPixels;
            }
            for(int frame=0;frame<18;frame++)
            {
                Frame($"formation/start-{frame:000}.png",frame/60f,frame,
                    SoulMaterialTrail.Visibility(frame,false,0),true,history:Math.Min(frame*2,centers.Length));
                if(frame==0)formationStart=lastPixels;
                if(frame==17)formationFinal=lastPixels;
            }
            Frame("expired.png",.2f,36,0,false);int expired=lastPixels;
            Frame("mask-fully-dissolved.png",.2f,36,1,false,dissolve:1);int maskExpired=lastPixels;
            var glass=GlassSurface();
            var refraction=RefractionRenderCheck.Run(output);
            byte[] package=File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod"));
            File.WriteAllText(Path.Combine(output,"gpu.json"),JsonSerializer.Serialize(new{
                passed=fixedA!=fixedB && hashes.Distinct().Count()==4 && restored && expired==0 && maskExpired==0 &&
                    endingStart>endingMiddle && endingMiddle>0 && endingFinal==0 && formationStart==0 && formationFinal>1000 && glass.passed && refraction.Passed,
                testedPackageSha256=Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(),
                actualGpu=true,productionRenderer=true,noncircularPath=true,
                fixedPoseMaterialAnimated=fixedA!=fixedB,paletteImagesDistinct=hashes.Distinct().Count(),
                materialEffect=SwordFlowMaterial.EffectPath,artworkInputs=SwordFlowMaterial.LoadedArtworkPaths,
                thirdPartyAssetsPresent=ModContent.GetInstance<global::ZenithReborn.ZenithReborn>().FileExists("Assets/ThirdParty/CalamityOverhaul/OniCrimsonSlash.fxc"),
                graphicsStateRestored=restored,expiredPixels=expired,previewFrames=96,
                fullyDissolvedPixelsAtFullOpacity=maskExpired,
                endingStartPixels=endingStart,endingMiddlePixels=endingMiddle,endingFinalPixels=endingFinal,
                formationStartPixels=formationStart,formationFinalPixels=formationFinal,
                endingFrames=SoulTrailEnding.NormalFrames+1,formationFrames=18,
                independentEndingLifetimeFrames=SoulTrailEnding.NormalFrames,
                glassSurface=new{glass.passed,glass.bodyMeanAlpha,glass.edgeMeanAlpha},
                refraction,
                actualMouseGameplayTested=false,liveMultiplayerTested=false},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());}
        finally
        {
            Main.screenPosition=camera;device.SetRenderTargets(targets);device.Viewport=viewport;
            Main.instance.Exit();
        }

        (bool passed,double bodyMeanAlpha,double edgeMeanAlpha) GlassSurface()
        {
            const int width=960,height=540;
            using var target=new RenderTarget2D(device,width,height);
            var a=new VertexPositionColorTexture(new Vector3(120,140,0),Color.White,new Vector2(0,0));
            var b=new VertexPositionColorTexture(new Vector3(840,140,0),Color.White,new Vector2(1,0));
            var c=new VertexPositionColorTexture(new Vector3(120,400,0),Color.White,new Vector2(0,1));
            var d=new VertexPositionColorTexture(new Vector3(840,400,0),Color.White,new Vector2(1,1));
            VertexPositionColorTexture[] surface=[a,b,c,b,d,c];
            var pixels=new Color[width*height];
            double body=0,edge=0;
            for(int scene=0;scene<2;scene++)
            {
                device.SetRenderTarget(target);device.Clear(Color.Transparent);
                if(scene==1)
                {
                    Main.spriteBatch.Begin();
                    for(int y=0;y<height;y+=32)for(int x=0;x<width;x+=32)
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle(x,y,32,32),new Rectangle(0,0,1,1),
                            ((x+y)/32)%2==0?new Color(52,65,80):new Color(172,185,196));
                    Main.spriteBatch.End();
                }
                MartialSlashRenderer.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(surface,6,new Color(120,195,255),
                    1,.3f,width,height,true));
                device.SetRenderTarget(null);
                if(scene==0)
                {
                    target.GetData(pixels);
                    body=Enumerable.Range(0,180).Average(i=>(double)pixels[(220+i%40)*width+300+i%120].A);
                    edge=Enumerable.Range(0,180).Average(i=>(double)pixels[145*width+300+i].A);
                }
                using var png=File.Create(Path.Combine(output,scene==0?"glass-alpha.png":"glass-checker.png"));
                target.SaveAsPng(png,width,height);
            }
            // 实际读取生产材质输出透明度，确认主体透光而刃缘反光仍清楚。
            return (body>140&&body<230&&edge>body+10,body,edge);
        }

        string Frame(string filename,float clock,int age,float opacity,bool sprite,int selected=-1,
            float dissolve=0,int? history=null)
        {
            const int width=960,height=540;
            using var target=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None);
            device.SetRenderTarget(target);device.Clear(new Color(12,17,29));
            for(int sword=0;sword<(selected<0?3:1);sword++)
            {
                int item=palette[selected<0?sword:selected];
                for(int i=0;i<centers.Length;i++)
                {
                    float t=(age-i)*.046f+sword*.7f;
                    centers[i]=new Vector2(450+MathF.Sin(t)*280,280+MathF.Sin(t*1.8f)*100+sword*12);
                    rotations[i]=t*1.1f+MathHelper.PiOver4;
                }
                var texture=TextureAssets.Item[item].Value;
                float halfLength=texture.Size().Length()*.5f*1.6f;
                int count=SoulMaterialTrail.Build(centers,rotations,history??centers.Length,halfLength,Vector2.Zero,vertices,clock);
                var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
                var textures=Enumerable.Range(0,3).Select(i=>device.Textures[i]).ToArray();
                var samplers=Enumerable.Range(0,3).Select(i=>device.SamplerStates[i]).ToArray();
                MartialSlashRenderer.DrawWithRestoredState(()=>
                    SoulMaterialTrail.Draw(vertices,count,SoulVisuals.Tint(item),opacity*.86f,clock,width,height,true,dissolve,item));
                restored&=device.BlendState==blend && device.DepthStencilState==depth && device.RasterizerState==raster &&
                    Enumerable.Range(0,3).All(i=>device.Textures[i]==textures[i] && device.SamplerStates[i]==samplers[i]);
                if(sprite)
                {
                    Main.spriteBatch.Begin();
                    Main.spriteBatch.Draw(texture,centers[0],null,Color.White*opacity,rotations[0],texture.Size()*.5f,1.6f,SpriteEffects.None,0);
                    Main.spriteBatch.End();
                }
            }
            device.SetRenderTarget(null);
            var colors=new Color[width*height];target.GetData(colors);
            lastPixels=colors.Count(c=>c.R>25 || c.G>30 || c.B>42);
            string path=Path.Combine(output,filename);Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var stream=File.Create(path))target.SaveAsPng(stream,width,height);
            return Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(colors.AsSpan())));
        }
    }
}
