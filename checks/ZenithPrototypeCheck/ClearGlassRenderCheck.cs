using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 透明材质专项：真实GPU运行生产带面及屏幕合成，固定姿态重放背景；不声称自然世界游玩。
public sealed class ClearGlassRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_CLEAR_GLASS_RENDER");
        if(ran||string.IsNullOrEmpty(output)||!Main.gameMenu||Main.menuMode!=0||!Main.SavePath.Contains("clear-glass-profile"))return;
        ran=true;Directory.CreateDirectory(output);
        bool baseline=Environment.GetEnvironmentVariable("ZENITH_CLEAR_GLASS_BASELINE")=="1";
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        bool oldDay=Main.dayTime;double oldTime=Main.time;
        const int width=960,height=540;
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var mask=typeof(SoulSceneRefraction).GetField("<BuildingMask>k__BackingField",flags);
        var surface=typeof(SoulSceneRefraction).GetField("<BuildingSurface>k__BackingField",flags);
        var extra=typeof(SoulSceneRefraction).GetField("additionalOutput",flags);
        var vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxLayeredVertices];
        var centers=new Vector2[13];var rotations=new float[13];
        for(int i=0;i<13;i++)
        {
            float t=1-i/12f;var tip=new Vector2(90+700*t,100+295*t*t);
            var axis=Vector2.Normalize(new Vector2(590*t,-700));
            centers[i]=tip-axis*54;rotations[i]=axis.ToRotation()+MathHelper.PiOver4;
        }
        int count=SoulMaterialTrail.BuildLayered(centers,rotations,13,54,Vector2.Zero,vertices,
            new[]{new SoulMaterialTrail.TrailLayer(0,13,1,true)},.3f);
        // 旧包仍用它自己的八位精度；新包从生产定义读取，不能让基线偷偷受益于新精度。
        var surfaceFormat=(SurfaceFormat)(typeof(SoulSceneRefraction).GetField("SurfaceBufferFormat")?.GetRawConstantValue()??SurfaceFormat.Color);
        var buffers=Enumerable.Range(0,4).Select(i=>new RenderTarget2D(device,width,height,false,
            i==1?surfaceFormat:SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents)).ToArray();
        using var target=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        using var background=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        using var pixel=new Texture2D(device,1,1);pixel.SetData(new[]{Color.White});
        using var batch=new SpriteBatch(device);
        using var shader=ModContent.Request<Effect>("ZenithReborn/Assets/Effects/ZRGlassScreen",AssetRequestMode.ImmediateLoad).Value.Clone();
        var optics=SwordFlowMaterial.DefaultOptics;
        // 跨版本对照从被测程序集读取常量，避免旧包误用编译检查模组时内联的新厚度。
        float thickness=(float)typeof(GlassMaterialPreset).GetField(nameof(GlassMaterialPreset.Thickness)).GetRawConstantValue();
        try
        {
            VfxIsolation.ApplyDefaultPreset();Main.dayTime=true;Main.time=16200;SoulSceneRefraction.EndCapture();
            if(Environment.GetEnvironmentVariable("ZENITH_GLASS_HIGHLIGHT_PROBE")=="1")
            {
                GlassHighlightProbe.Run(output,(name,item,clock,backgroundKind)=>{
                    var data=Layers("",item,1,optics,clock:clock);SetBackground(backgroundKind);
                    return new GlassHighlightProbe.Sample(Composite(name,1),data[0]);
                });
                return;
            }
            var single=Layers("single",ItemID.TerraBlade,1,optics);
            var repeated=Layers("overlap-four",ItemID.TerraBlade,4,optics);
            double singleEnergy=Energy(single,single[0]),overlapEnergy=Energy(repeated,single[0]);
            double overlapGain=overlapEnergy/Math.Max(.0001,singleEnergy);
            int alphaMismatch=single[0].Zip(single[1]).Count(p=>Math.Abs(p.First.A-p.Second.A)>1);
            var clear=Layers("body-zero",ItemID.TerraBlade,1,optics with{BodyOpacity=0});
            var solid=Layers("body-one",ItemID.TerraBlade,1,optics with{BodyOpacity=1});
            bool coverageIndependent=single[0].SequenceEqual(clear[0])&&single[0].SequenceEqual(solid[0]);
            int clearSurface=clear[1].Count(p=>p.R+p.G+p.B>10);
            var images=new Dictionary<string,string>();
            foreach(int item in new[]{ItemID.TerraBlade,ItemID.EnchantedSword,ItemID.Starfury})
            {
                foreach(bool day in new[]{true,false})
                {
                    Main.dayTime=day;Main.time=day?16200:12000;
                    Layers("",item,1,optics);
                    SetBackground(day?"day":"night");
                    var pixels=Composite($"{item}-{(day?"day":"night")}",1);
                    images[$"{item}-{day}"]=Convert.ToHexString(SHA256.HashData(pixels.SelectMany(p=>new[]{p.R,p.G,p.B,p.A}).ToArray()));
                }
            }
            Main.dayTime=true;Main.time=16200;
            Layers("",ItemID.TerraBlade,1,optics);SetBackground("pattern");
            Composite("pattern-single",1);
            var iorOne=Composite("pattern-ior-one",1,1);
            var iorGlass=Composite("pattern-ior-glass",1,1.5f);
            int refracted=iorOne.Zip(iorGlass).Count(p=>p.First!=p.Second);
            var off=Composite("empty-mask",0);
            var original=new Color[width*height];background.GetData(original);
            int emptyChanged=off.Zip(original).Count(p=>p.First!=p.Second);
            Layers("",ItemID.TerraBlade,4,optics);Composite("pattern-overlap-four",1);
            Layers("",ItemID.TerraBlade,1,optics,visibility:0);
            int expiredChanged=Composite("expired",1).Zip(original).Count(p=>p.First!=p.Second);
            Layers("",ItemID.TerraBlade,1,optics,dissolve:1);
            int dissolvedChanged=Composite("dissolved",1).Zip(original).Count(p=>p.First!=p.Second);
            SetBackground("day");
            for(int frame=0;frame<3;frame++)
            {Layers("",ItemID.TerraBlade,1,optics,clock:.3f+frame*.5f);Composite("motion-"+frame,1);}
            bool customMaterial=false;
            if(!baseline)
            {
                var custom=new SurfaceMaterialDefinition{Transmission=.95f,Thickness=18,SsrStrength=0};
                try
                {
                    SwordFlowMaterial.SetMaterialBinding(ItemID.TerraBlade,custom);
                    var customLayers=Layers("custom-material",ItemID.TerraBlade,1,optics);
                    Composite("custom-material-day",1);
                    customMaterial=customLayers[0].Any(p=>p.A>0)&&customLayers[0].Zip(customLayers[1]).All(p=>Math.Abs(p.First.A-p.Second.A)<=1)&&
                        customLayers[2].All(p=>p.B==0)&&customLayers[3].Any(p=>p.R>0);
                }
                finally{SwordFlowMaterial.SetMaterialBinding(ItemID.TerraBlade,null);}
            }
            // 消融只用于定位原版整面自发光的贡献，不改变默认开关。
            int emissionIndex=Array.FindIndex(VfxIsolation.Parts,p=>p.Uniform=="uPartEmission");
            VfxIsolation.PartValues[emissionIndex]=0;Layers("",ItemID.TerraBlade,1,optics);SetBackground("day");
            Composite("day-without-emission",1);VfxIsolation.ApplyDefaultPreset();
            bool passed=coverageIndependent&&clearSurface>100&&refracted>100&&emptyChanged==0&&expiredChanged==0&&dissolvedChanged==0&&
                (baseline||(alphaMismatch==0&&overlapGain>.96&&overlapGain<1.04&&customMaterial));
            File.WriteAllText(Path.Combine(output,"clear-glass.json"),JsonSerializer.Serialize(new{
                passed,baseline,actualGpu=true,productionShaders=true,syntheticPose=true,replayedWorldBackground=true,
                naturalGameplayTested=false,artApproved=false,coverageIndependent,clearSurfacePixels=clearSurface,
                alphaMismatch,singleEnergy,overlapEnergy,overlapGain,refractedPixels=refracted,emptyChanged,expiredChanged,dissolvedChanged,customMaterial,images,
                optics,thickness,surfaceBufferFormat=surfaceFormat.ToString(),all57OptionsEnabled=VfxIsolation.Branches.All(b=>b.Get())&&
                    VfxIsolation.Details.All(d=>d.Get())&&VfxIsolation.PartValues.All(v=>v==1)&&VfxIsolation.GlassPartValues.All(v=>v==1),
                testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant()
            },new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally
        {
            mask.SetValue(null,false);surface.SetValue(null,false);extra.SetValue(null,0);
            Main.dayTime=oldDay;Main.time=oldTime;VfxIsolation.ApplyDefaultPreset();SoulSceneRefraction.EndCapture();
            device.SetRenderTargets(saved);device.Viewport=viewport;foreach(var buffer in buffers)buffer.Dispose();Main.instance.Exit();
        }
        Color[][] Layers(string name,int item,int repeats,SwordFlowMaterial.GlassOptics glass,float visibility=1,float dissolve=0,float clock=.3f)
        {
            var pixels=new Color[4][];
            for(int mode=1;mode<=4;mode++)
            {
                mask.SetValue(null,mode==1);surface.SetValue(null,mode==2);extra.SetValue(null,mode>=3?mode:0);
                for(int slot=0;slot<16;slot++)device.Textures[slot]=null;
                device.SetRenderTarget(buffers[mode-1]);device.Clear(Color.Transparent);
                VfxRenderState.DrawWithRestoredState(()=>{
                    for(int n=0;n<repeats;n++)SwordFlowMaterial.Draw(vertices,count,SoulVisuals.Tint(item),visibility,clock,width,height,true,dissolve,item,glass);
                });
                pixels[mode-1]=GlassBufferReadback.Read(buffers[mode-1]);
                if(name.Length>0)Save(buffers[mode-1],name+"-"+mode);
            }
            mask.SetValue(null,false);surface.SetValue(null,false);extra.SetValue(null,0);device.SetRenderTarget(null);
            return pixels;
        }
        void SetBackground(string kind)
        {
            device.SetRenderTarget(background);device.Clear(kind=="black"?Color.Black:new Color(30,35,42));
            batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
            if(kind=="pattern")
            {
                for(int y=0;y<height;y+=24)for(int x=0;x<width;x+=24)
                    batch.Draw(pixel,new Rectangle(x,y,24,24),((x/24+y/24)%2==0)?new Color(68,76,86):new Color(165,177,187));
            }
            else if(kind!="black")
            {
                using var input=File.OpenRead(Path.Combine(Main.SavePath,kind+".png"));using var texture=Texture2D.FromStream(device,input);
                batch.Draw(texture,new Rectangle(0,0,width,height),new Rectangle(400,500,1600,900),Color.White);
            }
            batch.End();Save(background,"background-"+kind);device.SetRenderTarget(null);
        }
        Color[] Composite(string name,float ready,float? overrideIor=null)
        {
            var p=shader.Parameters;bool advanced=!baseline;
            p["uGlassTexel"].SetValue(new Vector2(1f/width,1f/height));p["uGlassTransmission"].SetValue(optics.Transmission);
            p["uGlassBodyOpacity"].SetValue(optics.BodyOpacity);p["uGlassSsrStrength"].SetValue(GlassMaterialPreset.SsrStrength);
            p["uGlassThickness"].SetValue(thickness);p["uGlassIor"].SetValue(overrideIor??optics.Ior);
            p["uMaskReady"].SetValue(ready);p["uHasMaterialProperties"].SetValue(advanced?1f:0f);
            Texture2D properties=buffers[2];Texture2D replacement=null;
            if(advanced&&overrideIor.HasValue)
            {
                var data=new Color[width*height];buffers[2].GetData(data);
                for(int i=0;i<data.Length;i++)data[i].R=(byte)Math.Clamp((overrideIor.Value-1)/1.5f*data[i].A,0,255);
                replacement=new Texture2D(device,width,height);replacement.SetData(data);properties=replacement;
            }
            p["uMaterialProperties"].SetValue(properties);p["uMaterialTransmission"].SetValue(buffers[3]);
            foreach(var part in VfxIsolation.GlassParts)p[part.Uniform]?.SetValue(1f);
            SwordSpaceDistortion.Apply(p,.3f,Vector2.Zero,Matrix.Identity);
            // 该对照隔离主体光学，周围空间波纹已有专项，避免掩盖背景透射的变化。
            p["uGlassPartSpaceWarp"].SetValue(0f);
            if(ready==0)p["uGlassPartBloom"].SetValue(0f);
            device.SetRenderTarget(target);device.Clear(Color.Magenta);
            VfxRenderState.DrawWithRestoredState(()=>{
                batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone,shader);
                device.Textures[1]=buffers[0];device.Textures[2]=buffers[1];
                device.SamplerStates[1]=SamplerState.LinearClamp;device.SamplerStates[2]=SamplerState.LinearClamp;
                batch.Draw(background,Vector2.Zero,Color.White);batch.End();
            });
            var pixels=new Color[width*height];target.GetData(pixels);Save(target,name);device.SetRenderTarget(null);
            replacement?.Dispose();return pixels;
        }
        void Save(RenderTarget2D texture,string name)
        {GlassBufferReadback.Save(texture,Path.Combine(output,name+".png"));}
        static double Energy(Color[][] data,Color[] reference)
        {
            double sum=0;int pixels=0;
            for(int i=0;i<data[0].Length;i++)if(reference[i].A>160)
            {sum+=(data[1][i].R+data[1][i].G+data[1][i].B)/(double)data[0][i].A;pixels++;}
            return sum/Math.Max(1,pixels);
        }
    }
}
