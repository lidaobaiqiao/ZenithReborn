using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 独立高度场实验：画布为固定二维输入，只提供GPU读回证据，不代表游戏世界效果。
public sealed class LiquidGlassRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime gameTime)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_LIQUID_GLASS_RENDER");
        if(ran||string.IsNullOrEmpty(output)||!Main.gameMenu||Main.menuMode!=0||!Main.SavePath.Contains("liquid-glass-profile"))return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;var targets=device.GetRenderTargets();var viewport=device.Viewport;
        const int width=960,height=480;
        try
        {
            using var image=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            using var background=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            using var pixel=new Texture2D(device,1,1);pixel.SetData(new[]{Color.White});
            using var batch=new SpriteBatch(device);
            using var effect=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlass",AssetRequestMode.ImmediateLoad).Value.Clone();
            device.SetRenderTarget(background);device.Clear(new Color(33,40,46));
            batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
            for(int y=0;y<height;y+=32)for(int x=0;x<width;x+=32)
                batch.Draw(pixel,new Rectangle(x,y,32,32),((x/32+y/32)%2==0)?new Color(72,91,102):new Color(168,185,186));
            batch.End();device.SetRenderTarget(null);Save(background,"background");
            // 固定色像素着色器先检验应用和读回，再判定高度场输出是否可信。
            using var sentinel=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/LiquidGlass/Sentinel",AssetRequestMode.ImmediateLoad).Value.Clone();
            device.SetRenderTarget(image);device.Clear(Color.Black);
            MartialSlashRenderer.DrawWithRestoredState(()=>{
                batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,sentinel);
                batch.Draw(background,Vector2.Zero,Color.White);batch.End();
            });
            device.SetRenderTarget(null);
            var sentinelData=new Color[width*height];image.GetData(sentinelData);Save(image,"sentinel");
            int sentinelPixels=sentinelData.Count(p=>p.R>=250&&p.G<=5&&p.B>=250);
            using var parameterProbe=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/LiquidGlass/ParameterProbe",AssetRequestMode.ImmediateLoad).Value.Clone();
            var modeReadbacks=new Dictionary<string,float>();
            var probeZero=Probe(0,"parameter-mode-zero");
            var probeSix=Probe(6,"parameter-mode-six");
            int probeRed=probeZero.Count(p=>p.R>=250&&p.G<=5&&p.B<=5);
            int probeGreen=probeSix.Count(p=>p.R<=5&&p.G>=250&&p.B<=5);
            string probeTechnique=parameterProbe.CurrentTechnique.Name;
            var probePasses=parameterProbe.CurrentTechnique.Passes.Select(p=>p.Name).ToArray();
            string liquidTechnique=effect.CurrentTechnique.Name;
            var liquidPasses=effect.CurrentTechnique.Passes.Select(p=>p.Name).ToArray();
            var singleHeight=Render("single-height",0,1,0,.3f,0);
            var singleNormal=Render("single-normal",1,1,0,.3f,0);
            Render("single-reflection",2,1,0,.3f,0);
            Render("single-composite",3,1,0,.3f,0);
            var strictHeight=Render("cross-strict-height",0,2,0,.3f,0);
            var strictNormal=Render("cross-strict-normal",1,2,0,.3f,0);
            var strictReflection=Render("cross-strict-reflection",2,2,0,.3f,0);
            var twoXReflection=RenderReflectionTwoX();
            var composite=Render("cross-strict-composite",3,2,0,.3f,0);
            Render("cross-owner",4,2,0,.3f,0);
            var boundary=Render("cross-boundary",5,2,0,.3f,0);
            var smoothHeight=Render("cross-smooth-height",0,2,1,.3f,0);
            Render("cross-smooth-normal",1,2,1,.3f,0);
            var smoothReflection=Render("cross-smooth-reflection",2,2,1,.3f,0);
            Render("cross-smooth-composite",3,2,1,.3f,0);
            var motionHeight=Render("motion-height",0,2,0,1.1f,0);
            var motionNormal=Render("motion-normal",1,2,0,1.1f,0);
            var reflection=Render("motion-start-reflection",2,2,0,.3f,0);
            var motionReflection=Render("motion-end-reflection",2,2,0,1.1f,0);
            var motionSmoothReflection=Render("motion-smooth-reflection",2,2,1,1.1f,0);
            var motionBoundary=Render("motion-cross-boundary",5,2,0,1.1f,0);
            Render("motion-end-composite",3,2,0,1.1f,0);
            var lightHeight=Render("light-moved-height",0,2,0,.3f,.25f);
            var lightNormal=Render("light-moved-normal",1,2,0,.3f,.25f);
            var lightReflection=Render("light-moved-reflection",2,2,0,.3f,.25f);
            Render("light-moved-composite",3,2,0,.3f,.25f);
            var baseImage=Render("base-composite",6,2,0,.3f,0);
            // 相同法线和菲涅耳，只改变背景取样位置，以隔离折射而不是表面反射或透射能量。
            var noRefraction=Render("transmission-unrefracted",7,2,0,.3f,0);
            var withRefraction=Render("transmission-refracted",8,2,0,.3f,0);
            var motionRefraction=Render("motion-refraction-t11",8,2,0,1.1f,0);
            // 在固定光源下补充三个中间时刻；这是离散采样，不宣称已完成自然动画验证。
            var intermediateMotion=new List<object>();
            bool intermediateChanges=true;
            foreach(float sampleTime in new[]{.5f,.7f,.9f})
            {
                string label=((int)Math.Round(sampleTime*10)).ToString();
                var sampledHeight=Render("motion-height-t"+label,0,2,0,sampleTime,0);
                var sampledReflection=Render("motion-reflection-t"+label,2,2,0,sampleTime,0);
                var sampledRefraction=Render("motion-refraction-t"+label,8,2,0,sampleTime,0);
                int heightDelta=Difference(strictHeight,sampledHeight),reflectionDelta=Difference(reflection,sampledReflection);
                int refractionDelta=Difference(withRefraction,sampledRefraction);
                intermediateChanges&=heightDelta>0&&reflectionDelta>0&&refractionDelta>0;
                intermediateMotion.Add(new{time=sampleTime,heightDiffFromStart=heightDelta,
                    reflectionDiffFromStart=reflectionDelta,refractionDiffFromStart=refractionDelta});
            }
            int reflectedPixels=reflection.Count(p=>p.R+p.G+p.B>0);
            int compositePixels=Difference(composite,baseImage);
            // 场景是已有世界画面的静态重放，不是本轮原生世界采样。
            using(var stream=File.OpenRead(Path.Combine(Main.SavePath,"day.png")))
            using(var scene=Texture2D.FromStream(device,stream))
            {
                device.SetRenderTarget(background);device.Clear(Color.Black);
                batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone);
                batch.Draw(scene,new Rectangle(0,0,width,height),new Rectangle(400,500,1600,900),Color.White);
                batch.End();device.SetRenderTarget(null);Save(background,"world-replay-background");
                Render("world-replay-composite",3,2,0,.3f,0);
                Render("world-replay-motion",3,2,0,1.1f,0);
            }
            int singleCross=Difference(singleHeight,strictHeight),smoothChange=Difference(strictHeight,smoothHeight);
            int animationHeight=Difference(strictHeight,motionHeight),animationNormal=Difference(strictNormal,motionNormal);
            int animationReflection=Difference(reflection,motionReflection),lightChange=Difference(reflection,lightReflection);
            int fixedHeightLight=Difference(strictHeight,lightHeight),fixedNormalLight=Difference(strictNormal,lightNormal);
            int refractionPixels=Difference(noRefraction,withRefraction);
            int crossingRegion=boundary.Count(p=>p.R>=192);
            int crossingStrictBright=Enumerable.Range(0,boundary.Length).Count(i=>boundary[i].R>=192&&strictReflection[i].R>=20);
            int crossingSmoothBright=Enumerable.Range(0,boundary.Length).Count(i=>boundary[i].R>=192&&smoothReflection[i].R>=20);
            int motionCrossingRegion=motionBoundary.Count(p=>p.R>=192);
            int motionCrossingStrictBright=Enumerable.Range(0,boundary.Length).Count(i=>motionBoundary[i].R>=192&&motionReflection[i].R>=20);
            int motionCrossingSmoothBright=Enumerable.Range(0,boundary.Length).Count(i=>motionBoundary[i].R>=192&&motionSmoothReflection[i].R>=20);
            bool sentinelPassed=sentinelPixels>width*height*0.99;
            bool probePassed=probeRed>width*height*.99&&probeGreen>width*height*.99;
            int animatedRefraction=Difference(withRefraction,motionRefraction);
            int resolutionChanged=Difference(strictReflection,twoXReflection);
            double resolutionMeanAbsolute=Enumerable.Range(0,strictReflection.Length).Average(i=>
                (Math.Abs(strictReflection[i].R-twoXReflection[i].R)+Math.Abs(strictReflection[i].G-twoXReflection[i].G)+Math.Abs(strictReflection[i].B-twoXReflection[i].B))/3.0);
            bool shaderOutputObserved=sentinelPassed&&singleCross>0&&animationHeight>0&&animationNormal>0&&compositePixels>0;
            bool passed=shaderOutputObserved&&probePassed&&smoothChange>0&&animationReflection>0&&lightChange>0&&reflectedPixels>0&&refractionPixels>0&&animatedRefraction>0&&intermediateChanges&&fixedHeightLight==0&&fixedNormalLight==0;
            var sourcePackage=Path.Combine(Main.SavePath,"Mods/ZenithPrototypeCheck.tmod");
            File.WriteAllText(Path.Combine(output,"liquid-glass.json"),JsonSerializer.Serialize(new {
                passed,actualGpu=true,independentFlatCanvas=true,productionPipeline=false,naturalWorld=false,artApproved=false,
                width,height,singleVsCrossHeightPixels=singleCross,strictVsSmoothHeightPixels=smoothChange,
                animationHeightPixels=animationHeight,animationNormalPixels=animationNormal,animationReflectionPixels=animationReflection,
                lightMovedReflectionPixels=lightChange,lightMovedHeightPixels=fixedHeightLight,lightMovedNormalPixels=fixedNormalLight,
                sentinelPassed,sentinelPixels,probeTechnique,probePasses,probeRedPixels=probeRed,probeGreenPixels=probeGreen,
                probePassed,
                liquidTechnique,liquidPasses,modeReadbacks,shaderOutputObserved,rawNonBlackReflectionOutputPixels=reflectedPixels,
                validReflectionPixels=shaderOutputObserved?(int?)reflectedPixels:null,
                isolatedRefractionChangedPixels=refractionPixels,crossingRegionPixels=crossingRegion,
                animatedRefractionPixels=animatedRefraction,
                crossingStrictBrightPixels=crossingStrictBright,crossingSmoothBrightPixels=crossingSmoothBright,
                motionCrossingRegionPixels=motionCrossingRegion,motionCrossingStrictBrightPixels=motionCrossingStrictBright,
                motionCrossingSmoothBrightPixels=motionCrossingSmoothBright,
                intermediateMotion,intermediateChanges,
                twoXReflectionRawWidth=width*2,twoXReflectionRawHeight=height*2,
                twoXDownsampleMethod="八位输出逐通道2x2算术平均",twoXChangedPixels=resolutionChanged,twoXMeanAbsoluteRgb=resolutionMeanAbsolute,
                compositeVsBackgroundPixels=compositePixels,worldBackground="historical replay",
                testedCheckPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePackage))).ToLowerInvariant()
            },new JsonSerializerOptions{WriteIndented=true}));
            Color[] Render(string name,float mode,float count,float smooth,float clock,float light)
            {
                effect.Parameters["uMode"].SetValue(mode);effect.Parameters["uCount"].SetValue(count);
                effect.Parameters["uSmooth"].SetValue(smooth);effect.Parameters["uTime"].SetValue(clock);
                effect.Parameters["uLight"].SetValue(light);effect.Parameters["uResolution"].SetValue(new Vector2(width,height));
                modeReadbacks[name]=effect.Parameters["uMode"].GetValueSingle();
                device.SetRenderTarget(image);device.Clear(Color.Magenta);
                MartialSlashRenderer.DrawWithRestoredState(()=>{
                    batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,effect);
                    batch.Draw(background,Vector2.Zero,Color.White);batch.End();
                });
                device.SetRenderTarget(null);
                var result=new Color[width*height];image.GetData(result);Save(image,name);return result;
            }
            Color[] Probe(float mode,string name)
            {
                parameterProbe.Parameters["uProbeMode"].SetValue(mode);
                modeReadbacks[name]=parameterProbe.Parameters["uProbeMode"].GetValueSingle();
                device.SetRenderTarget(image);device.Clear(Color.Black);
                MartialSlashRenderer.DrawWithRestoredState(()=>{
                    batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,parameterProbe);
                    batch.Draw(background,Vector2.Zero,Color.White);batch.End();
                });
                device.SetRenderTarget(null);
                var pixels=new Color[width*height];image.GetData(pixels);Save(image,name);return pixels;
            }
            Color[] RenderReflectionTwoX()
            {
                // 同一物理画布与输入，提升像素分辨率后更新每像素梯度换算；不改变曲面参数。
                using var high=new RenderTarget2D(device,width*2,height*2,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
                effect.Parameters["uMode"].SetValue(2f);effect.Parameters["uCount"].SetValue(2f);
                effect.Parameters["uSmooth"].SetValue(0f);effect.Parameters["uTime"].SetValue(.3f);
                effect.Parameters["uLight"].SetValue(0f);effect.Parameters["uResolution"].SetValue(new Vector2(width*2,height*2));
                device.SetRenderTarget(high);device.Clear(Color.Black);
                MartialSlashRenderer.DrawWithRestoredState(()=>{
                    batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,effect);
                    batch.Draw(background,new Rectangle(0,0,width*2,height*2),Color.White);batch.End();
                });
                device.SetRenderTarget(null);
                using(var file=File.Create(Path.Combine(output,"cross-strict-reflection-2x.png")))high.SaveAsPng(file,width*2,height*2);
                var source=new Color[width*height*4];high.GetData(source);
                var reduced=new Color[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    int i=2*y*width*2+2*x;
                    var a=source[i];var b=source[i+1];var c=source[i+width*2];var d=source[i+width*2+1];
                    reduced[y*width+x]=new Color((a.R+b.R+c.R+d.R+2)/4,(a.G+b.G+c.G+d.G+2)/4,
                        (a.B+b.B+c.B+d.B+2)/4,(a.A+b.A+c.A+d.A+2)/4);
                }
                using var preview=new Texture2D(device,width,height);preview.SetData(reduced);
                using(var file=File.Create(Path.Combine(output,"cross-strict-reflection-2x-downsampled.png")))preview.SaveAsPng(file,width,height);
                return reduced;
            }
            void Save(RenderTarget2D target,string name){using var file=File.Create(Path.Combine(output,name+".png"));target.SaveAsPng(file,width,height);}
            static int Difference(Color[] a,Color[] b)=>a.Zip(b).Count(pair=>pair.First!=pair.Second);
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally{device.SetRenderTargets(targets);device.Viewport=viewport;Main.instance.Exit();}
    }
}


