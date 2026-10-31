using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 真实刀光几何的隔离夹具：背景是历史画面重放，不调用生产材质或原生世界合成。
public sealed class LiquidGlassTrailRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime gameTime)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_LIQUID_TRAIL_RENDER");
        if(ran||string.IsNullOrEmpty(output)||!Main.gameMenu||Main.menuMode!=0||!Main.SavePath.Contains("liquid-trail-profile"))return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;var oldTargets=device.GetRenderTargets();var oldViewport=device.Viewport;
        const int width=960,height=540;const float halfLength=54;
        try
        {
            var centers=new Vector2[13];var rotations=new float[13];
            for(int i=0;i<13;i++)
            {
                float t=1-i/12f;var tip=new Vector2(90+700*t,100+295*t*t);
                var axis=Vector2.Normalize(new Vector2(590*t,-700));
                centers[i]=tip-axis*halfLength;rotations[i]=axis.ToRotation()+MathHelper.PiOver4;
            }
            float distance=0;
            Vector2 tipAt(int i)
            {
                float angle=rotations[i]-MathHelper.PiOver4;
                return centers[i]+new Vector2(MathF.Cos(angle),MathF.Sin(angle))*halfLength;
            }
            for(int i=1;i<centers.Length;i++)distance+=Vector2.Distance(tipAt(i-1),tipAt(i));
            float materialLength=Math.Max(distance,halfLength*6);
            var original=new VertexPositionColorTexture[SoulMaterialTrail.MaxLayeredVertices];
            int count=SoulMaterialTrail.BuildLayered(centers,rotations,13,halfLength,Vector2.Zero,original,
                new[]{new SoulMaterialTrail.TrailLayer(0,13,1,true)},.3f);
            var prepared=LiquidGlassTrailMesh.Prepare(original,count);var vertices=prepared.Vertices;
            var geometrySimilarity=LiquidGlassTrailGeometryCheck.CheckSimilarity(original,count,vertices);
            // 导出实际网格与带尺度的梯度供离线光学分析；不作为游戏运行资产。
            File.WriteAllText(Path.Combine(output,"trail-mesh.json"),JsonSerializer.Serialize(vertices.Select(v=>new{
                position=new[]{v.Position.X,v.Position.Y},uv=new[]{v.UV.X,v.UV.Y},
                gradU=new[]{v.GradU.X,v.GradU.Y},gradV=new[]{v.GradV.X,v.GradV.Y},coverage=v.Color.A
            })));
            using var target=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            using var background=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            using var pixel=new Texture2D(device,1,1);pixel.SetData(new[]{Color.White});
            using var batch=new SpriteBatch(device);
            using var effect=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassTrail",AssetRequestMode.ImmediateLoad).Value.Clone();
            bool environmentSupported=effect.Parameters["uEnvironmentStrength"]!=null&&effect.Parameters["uSceneAvailable"]!=null&&
                effect.Parameters["uReflectionReachWorld"]!=null;
            effect.Parameters["uScene"].SetValue(background);effect.Parameters["uSceneSize"].SetValue(new Vector2(width,height));
            effect.Parameters["uMaterialLength"].SetValue(materialLength);effect.Parameters["uTrailDistance"].SetValue(distance);
            effect.Parameters["uSurfaceWidth"].SetValue(halfLength*1.52f);effect.Parameters["uHeightUnit"].SetValue(100f);
            // 棋盘提供高频采样对照；此背景并非游戏世界当前帧。
            device.SetRenderTarget(background);device.Clear(Color.Black);
            batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
            for(int y=0;y<height;y+=24)for(int x=0;x<width;x+=24)
                batch.Draw(pixel,new Rectangle(x,y,24,24),((x/24+y/24)%2==0)?new Color(72,91,102):new Color(168,185,186));
            batch.End();device.SetRenderTarget(null);Save(background,"checker-background");
            var height0=Render("height",0,.3f,0,0,Vector2.Zero);
            var normal0=Render("normal",1,.3f,0,0,Vector2.Zero);
            var light=Render("reflection",2,.3f,0,0,Vector2.Zero);
            var combined=Render("checker-composite",3,.3f,0,0,Vector2.Zero);
            var owner=Render("cross-owner",4,.3f,0,0,Vector2.Zero);
            var boundary=Render("cross-boundary",5,.3f,0,0,Vector2.Zero);
            var smooth=Render("smooth-reflection",2,.3f,0,1,Vector2.Zero);
            var unrefracted=Render("transmission-unrefracted",7,.3f,0,0,Vector2.Zero);
            var refracted=Render("transmission-refracted",8,.3f,0,0,Vector2.Zero);
            var lightHeight=Render("light-moved-height",0,.3f,.25f,0,Vector2.Zero);
            var lightNormal=Render("light-moved-normal",1,.3f,.25f,0,Vector2.Zero);
            var lightMoved=Render("light-moved-reflection",2,.3f,.25f,0,Vector2.Zero);
            var motionHeight=Render("motion-height",0,1.1f,0,0,Vector2.Zero);
            var motionNormal=Render("motion-normal",1,1.1f,0,0,Vector2.Zero);
            var motionLight=Render("motion-reflection",2,1.1f,0,0,Vector2.Zero);
            var motionRefraction=Render("motion-refraction",8,1.1f,0,0,Vector2.Zero);
            var clampMap=Render("slope-clamp",9,.3f,0,0,Vector2.Zero);
            // 单独改变面光角度，再改变曲面长度单位；分别输出中间量，避免用整体提亮替代曲面观察。
            var trialLightDirection=new Vector2(.6f,-1f);
            var angleHeight=Render("angle-height",0,.3f,0,0,Vector2.Zero,100,trialLightDirection);
            var angleNormal=Render("angle-normal",1,.3f,0,0,Vector2.Zero,100,trialLightDirection);
            var angleReflection=Render("angle-reflection",2,.3f,0,0,Vector2.Zero,100,trialLightDirection);
            var angleClamp=Render("angle-slope-clamp",9,.3f,0,0,Vector2.Zero,100,trialLightDirection);
            Render("angle-checker-composite",3,.3f,0,0,Vector2.Zero,100,trialLightDirection);
            var scaleHeight=Render("scale200-height",0,.3f,0,0,Vector2.Zero,200,trialLightDirection);
            var scaleNormal=Render("scale200-normal",1,.3f,0,0,Vector2.Zero,200,trialLightDirection);
            var scaleReflection=Render("scale200-reflection",2,.3f,0,0,Vector2.Zero,200,trialLightDirection);
            Render("scale200-checker-composite",3,.3f,0,0,Vector2.Zero,200,trialLightDirection);
            var scaleClamp=Render("scale200-slope-clamp",9,.3f,0,0,Vector2.Zero,200,trialLightDirection);
            // 对解析梯度与中心差分的差异定位：固定尺度记录同一交界及平滑交界输出。
            Render("scale200-cross-owner",4,.3f,0,0,Vector2.Zero,200,trialLightDirection);
            Render("scale200-cross-boundary",5,.3f,0,0,Vector2.Zero,200,trialLightDirection);
            Render("scale200-smooth-normal",1,.3f,0,1,Vector2.Zero,200,trialLightDirection);
            Render("scale200-smooth-reflection",2,.3f,0,1,Vector2.Zero,200,trialLightDirection);
            // 同一曲面与面光下仅改变光路厚度，不将加强折射误认为曲面变亮。
            var scaleTransmission=Render("scale200-transmission",8,.3f,0,0,Vector2.Zero,200,trialLightDirection);
            var thickTransmission=Render("thickness18-transmission",8,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
            var thickNormal=Render("thickness18-normal",1,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
            var thickReflection=Render("thickness18-reflection",2,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
            // 同曲面有无场景反射的棋盘对照，另测无场景时不会采白占位。
            Color[] sceneReflection=null,sceneOnly=null,unavailable=null,environmentNormal=null;
            if(environmentSupported)
            {
                sceneReflection=Render("environment-checker-reflection",2,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18,environmentStrength:1);
                sceneOnly=Render("environment-checker-only",11,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18,environmentStrength:1);
                unavailable=Render("environment-unavailable-reflection",2,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18,environmentStrength:1,sceneAvailable:0);
                environmentNormal=Render("environment-normal",1,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18,environmentStrength:1);
            }
            var translated=Render("height-translated",0,.3f,0,0,new Vector2(40,20));
            // 画面绕中心旋转并缩放；曲面仍绑定世界网格，折射向量需经过同一视图变换。
            const float cameraScale=1.15f,cameraAngle=.18f;
            var cameraHeight=Render("camera-height",0,.3f,0,0,Vector2.Zero,200,trialLightDirection,cameraScale,cameraAngle);
            Render("camera-normal",1,.3f,0,0,Vector2.Zero,200,trialLightDirection,cameraScale,cameraAngle);
            var cameraBase=Render("camera-transmission-unrefracted",7,.3f,0,0,Vector2.Zero,200,trialLightDirection,cameraScale,cameraAngle);
            var cameraRefracted=Render("camera-transmission-refracted",8,.3f,0,0,Vector2.Zero,200,trialLightDirection,cameraScale,cameraAngle);
            Render("camera-reflection",2,.3f,0,0,Vector2.Zero,200,trialLightDirection,cameraScale,cameraAngle);
            var worldRay=Render("world-refracted-direction",10,.3f,0,0,Vector2.Zero,200,trialLightDirection);
            var cameraRay=Render("camera-refracted-direction",10,.3f,0,0,Vector2.Zero,200,trialLightDirection,cameraScale,cameraAngle);
            var cameraMatrix=Matrix.CreateRotationZ(cameraAngle)*Matrix.CreateScale(cameraScale);
            Vector2 pivot=new(width*.5f,height*.5f);long heightErrorSum=0;double directionErrorSum=0;int heightSamples=0;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                Color source=scaleHeight[y*width+x];
                if(source.A<64||source.R<4)continue;
                Vector2 point=Vector2.Transform(new Vector2(x+.5f,y+.5f)-pivot,cameraMatrix)+pivot;
                int dx=(int)MathF.Floor(point.X),dy=(int)MathF.Floor(point.Y);
                if(dx<0||dy<0||dx>=width||dy>=height)continue;
                Color transformed=cameraHeight[dy*width+dx];
                if(transformed.A<64)continue;
                heightErrorSum+=Math.Abs(source.R-transformed.R);
                Color originalRay=worldRay[y*width+x],rotatedRay=cameraRay[dy*width+dx];
                Vector2 ray=new(originalRay.R/255f*2-1,originalRay.G/255f*2-1);
                Vector2 expected=Vector2.TransformNormal(ray,cameraMatrix);
                directionErrorSum+=Math.Abs((expected.X*.5+.5)*255-rotatedRay.R)+Math.Abs((expected.Y*.5+.5)*255-rotatedRay.G);
                heightSamples++;
            }
            float cameraHeightMeanError=heightSamples>0?(float)heightErrorSum/heightSamples:float.PositiveInfinity;
            float cameraDirectionMeanError=heightSamples>0?(float)(directionErrorSum/(2*heightSamples)):float.PositiveInfinity;
            int cameraRefractedPixels=Difference(cameraBase,cameraRefracted);
            bool cameraCheck=heightSamples>100&&cameraHeightMeanError<8&&cameraDirectionMeanError<4&&cameraRefractedPixels>0;
            int compare=0,matching=0;
            for(int y=0;y<height-20;y++)for(int x=0;x<width-40;x++)
            {
                Color a=height0[y*width+x],b=translated[(y+20)*width+x+40];
                if(a.A==0&&b.A==0)continue;
                compare++;
                if(Math.Abs(a.R-b.R)<=1&&Math.Abs(a.G-b.G)<=1&&Math.Abs(a.B-b.B)<=1&&Math.Abs(a.A-b.A)<=1)matching++;
            }
            var backgroundPixels=new Color[width*height];background.GetData(backgroundPixels);
            int outsideChanged=Enumerable.Range(0,height0.Length).Count(i=>height0[i].A==0&&combined[i]!=backgroundPixels[i]);
            int coverage=height0.Count(c=>c.A>0),reflection=light.Count(c=>c.R+c.G+c.B>0);
            int slopeClipped=clampMap.Count(c=>c.A>0&&c.R>=128);
            int scaleSlopeClipped=scaleClamp.Count(c=>c.A>0&&c.R>=128);
            int refractDiff=Difference(unrefracted,refracted),timeHeight=Difference(height0,motionHeight);
            int timeNormal=Difference(normal0,motionNormal),timeReflection=Difference(light,motionLight);
            int timeRefraction=Difference(refracted,motionRefraction);
            int lightHeightDiff=Difference(height0,lightHeight),lightNormalDiff=Difference(normal0,lightNormal);
            int lightReflectionDiff=Difference(light,lightMoved);
            int angleHeightDiff=Difference(height0,angleHeight),angleNormalDiff=Difference(normal0,angleNormal);
            int angleReflectionDiff=Difference(light,angleReflection),angleClampDiff=Difference(clampMap,angleClamp);
            int scaleHeightDiff=Difference(angleHeight,scaleHeight),scaleNormalDiff=Difference(angleNormal,scaleNormal);
            int thicknessTransmissionDiff=Difference(scaleTransmission,thickTransmission);
            int thicknessNormalDiff=Difference(scaleNormal,thickNormal),thicknessReflectionDiff=Difference(scaleReflection,thickReflection);
            int? environmentReflectionDiff=environmentSupported?Difference(thickReflection,sceneReflection):null;
            int? environmentMissingDiff=environmentSupported?Difference(thickReflection,unavailable):null;
            int? environmentNormalDiff=environmentSupported?Difference(scaleNormal,environmentNormal):null;
            int? environmentPixels=environmentSupported?sceneOnly.Count(p=>p.R+p.G+p.B>0):null;
            bool controlledChecksPassed=angleHeightDiff==0&&angleNormalDiff==0&&angleClampDiff==0&&angleReflectionDiff>0&&
                scaleHeightDiff>0&&scaleNormalDiff>0&&thicknessTransmissionDiff>0&&thicknessNormalDiff==0&&thicknessReflectionDiff==0&&
                (!environmentSupported||(environmentReflectionDiff>0&&environmentPixels>0&&environmentMissingDiff==0&&environmentNormalDiff==0));
            bool translatedMatch=compare>0&&(double)matching/compare>.99;
            bool passed=count>0&&coverage>0&&reflection>0&&refractDiff>0&&timeHeight>0&&timeNormal>0&&timeReflection>0&&
                timeRefraction>0&&lightHeightDiff==0&&lightNormalDiff==0&&lightReflectionDiff>0&&outsideChanged==0&&translatedMatch&&controlledChecksPassed&&
                geometrySimilarity.All(item=>item.Passed)&&cameraCheck;
            // 历史背景只重放既有截图，不能作为原生同帧场景的证据。
            using(var stream=File.OpenRead(Path.Combine(Main.SavePath,"day.png")))
            using(var scene=Texture2D.FromStream(device,stream))
            {
                device.SetRenderTarget(background);device.Clear(Color.Black);
                batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone);
                batch.Draw(scene,new Rectangle(0,0,width,height),new Rectangle(400,500,1600,900),Color.White);
                batch.End();device.SetRenderTarget(null);Save(background,"world-replay-background");
                Render("world-replay-composite",3,.3f,0,0,Vector2.Zero);
                Render("world-replay-motion",3,1.1f,0,0,Vector2.Zero);
                Render("world-replay-angle",3,.3f,0,0,Vector2.Zero,100,trialLightDirection);
                Render("world-replay-scale200",3,.3f,0,0,Vector2.Zero,200,trialLightDirection);
                Render("world-replay-thickness18",3,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
                if(environmentSupported)
                {
                    Render("world-replay-day-environment",3,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18,environmentStrength:1);
                    Render("world-replay-day-environment-only",11,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18,environmentStrength:1);
                }
                Render("world-replay-camera",3,.3f,0,0,Vector2.Zero,200,trialLightDirection,cameraScale,cameraAngle);
            }
            using(var stream=File.OpenRead(Path.Combine(Main.SavePath,"night.png")))
            using(var scene=Texture2D.FromStream(device,stream))
            {
                device.SetRenderTarget(background);device.Clear(Color.Black);
                batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone);
                batch.Draw(scene,new Rectangle(0,0,width,height),new Rectangle(400,500,1600,900),Color.White);
                batch.End();device.SetRenderTarget(null);Save(background,"world-replay-night-background");
                Render("world-replay-night-no-environment",3,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
                if(environmentSupported)
                {
                    Render("world-replay-night-environment",3,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18,environmentStrength:1);
                    Render("world-replay-night-environment-only",11,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18,environmentStrength:1);
                }
            }
            // 后续姿态统一沿用白天历史背景，避免把昼夜切换混入姿态对照。
            using(var stream=File.OpenRead(Path.Combine(Main.SavePath,"day.png")))
            using(var scene=Texture2D.FromStream(device,stream))
            {
                device.SetRenderTarget(background);device.Clear(Color.Black);
                batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone);
                batch.Draw(scene,new Rectangle(0,0,width,height),new Rectangle(400,500,1600,900),Color.White);
                batch.End();device.SetRenderTarget(null);
            }
            // 不同曲率与长度的固定轨迹均由生产网格生成器构造；这些仍是合成姿态。
            var alternatePoses=new List<object>();
            var baselineVertices=vertices;int baselineCount=count;
            for(int pose=0;pose<2;pose++)
            {
                var poseCenters=new Vector2[13];var poseRotations=new float[13];var tips=new Vector2[13];
                for(int i=0;i<13;i++)
                {
                    float t=1-i/12f;
                    tips[i]=pose==0?new Vector2(150+540*t,130+170*t*t):new Vector2(180+480*t,120+300*t*t*t);
                    var direction=Vector2.Normalize(pose==0?new Vector2(300*t,-700):new Vector2(540*t,-650));
                    poseCenters[i]=tips[i]-direction*halfLength;
                    poseRotations[i]=direction.ToRotation()+MathHelper.PiOver4;
                }
                float poseDistance=0;
                for(int i=1;i<tips.Length;i++)poseDistance+=Vector2.Distance(tips[i-1],tips[i]);
                var poseOriginal=new VertexPositionColorTexture[SoulMaterialTrail.MaxLayeredVertices];
                count=SoulMaterialTrail.BuildLayered(poseCenters,poseRotations,13,halfLength,Vector2.Zero,poseOriginal,
                    new[]{new SoulMaterialTrail.TrailLayer(0,13,1,true)},.3f);
                var posePrepared=LiquidGlassTrailMesh.Prepare(poseOriginal,count);vertices=posePrepared.Vertices;
                effect.Parameters["uTrailDistance"].SetValue(poseDistance);
                effect.Parameters["uMaterialLength"].SetValue(Math.Max(poseDistance,halfLength*6));
                string prefix=$"pose-{pose+1}";
                var poseHeight=Render(prefix+"-height",0,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
                var poseReflection=Render(prefix+"-reflection",2,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
                var poseMotion=Render(prefix+"-motion-height",0,1.1f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
                var poseBefore=Render(prefix+"-unrefracted",7,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
                var poseAfter=Render(prefix+"-refracted",8,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
                Render(prefix+"-world-replay",3,.3f,0,0,Vector2.Zero,200,trialLightDirection,thicknessWorld:18);
                alternatePoses.Add(new {pose=pose+1,poseDistance,mesh=posePrepared.Info,coveragePixels=poseHeight.Count(p=>p.A>0),
                    reflectionPixels=poseReflection.Count(p=>p.R+p.G+p.B>0),motionHeightPixels=Difference(poseHeight,poseMotion),
                    refractionPixels=Difference(poseBefore,poseAfter)});
            }
            vertices=baselineVertices;count=baselineCount;
            effect.Parameters["uTrailDistance"].SetValue(distance);
            effect.Parameters["uMaterialLength"].SetValue(materialLength);
            var package=Path.Combine(Main.SavePath,"Mods/ZenithPrototypeCheck.tmod");
            var corePackage=Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod");
            File.WriteAllText(Path.Combine(output,"liquid-trail.json"),JsonSerializer.Serialize(new{
                passed,actualGpu=true,actualTrailGeometry=true,syntheticPose=true,historicalBackgroundReplay=true,
                productionMaterial=false,productionWorldPipeline=false,naturalGameplay=false,artApproved=false,
                width,height,vertexCount=count,mesh=prepared.Info,geometrySimilarity,alternatePoses,trailDistance=distance,materialLength,
                worldHeightUnit=100,referenceWidth=halfLength*1.52f,coveragePixels=coverage,reflectionPixels=reflection,slopeClippedPixels=slopeClipped,
                controlledVariants=new{
                    passed=controlledChecksPassed,angleDirection=new[]{.6f,-1f},heightUnit=100,angleHeightDiff,
                    angleNormalDiff,angleReflectionDiff,angleClampDiff,
                    angleReflectionPixels=angleReflection.Count(c=>c.R+c.G+c.B>0),angleSlopeClippedPixels=angleClamp.Count(c=>c.A>0&&c.R>=128),
                    scale200HeightDiff=scaleHeightDiff,scale200NormalDiff=scaleNormalDiff,
                    scale200ReflectionPixels=scaleReflection.Count(c=>c.R+c.G+c.B>0),scale200SlopeClippedPixels=scaleSlopeClipped,
                    thicknessBaseline=7,thicknessTrial=18,thicknessTransmissionDiff,thicknessNormalDiff,thicknessReflectionDiff,
                    environmentTested=environmentSupported,environmentReflectionDiff,environmentMissingDiff,environmentNormalDiff,environmentPixels},
                refractionChangedPixels=refractDiff,timeHeightPixels=timeHeight,timeNormalPixels=timeNormal,
                timeReflectionPixels=timeReflection,timeRefractionPixels=timeRefraction,lightHeightPixels=lightHeightDiff,
                lightNormalPixels=lightNormalDiff,lightReflectionPixels=lightReflectionDiff,outsideChangedPixels=outsideChanged,
                camera=new{scale=cameraScale,rotation=cameraAngle,compared=heightSamples,meanHeightError=cameraHeightMeanError,
                    meanDirectionError=cameraDirectionMeanError,refractedPixels=cameraRefractedPixels,passed=cameraCheck},
                translation=new{pixels=new[]{40,20},compared=compare,matching,passed=translatedMatch},
                testedCheckPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))).ToLowerInvariant(),
                testedGeometrySourcePackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(corePackage))).ToLowerInvariant()
            },new JsonSerializerOptions{WriteIndented=true}));

            Color[] Render(string name,float mode,float time,float lightValue,float smoothing,Vector2 translation,float heightUnit=100,Vector2? lightDirection=null,
                float cameraScale=1,float cameraAngle=0,float? thicknessWorld=null,float environmentStrength=0,float sceneAvailable=1)
            {
                bool diagnostics=mode==0||mode==1||mode==4||mode==5||mode==9||mode==10||mode==11;
                var worldToScreen=Matrix.CreateRotationZ(cameraAngle)*Matrix.CreateScale(cameraScale);
                effect.Parameters["uWorldToScreen"].SetValue(worldToScreen);
                effect.Parameters["uTransform"].SetValue(Matrix.CreateTranslation(-width*.5f,-height*.5f,0)*worldToScreen*
                    Matrix.CreateTranslation(width*.5f+translation.X,height*.5f+translation.Y,0)*
                    Matrix.CreateOrthographicOffCenter(0,width,height,0,-1,1));
                effect.Parameters["uMode"].SetValue(mode);effect.Parameters["uTime"].SetValue(time);
                effect.Parameters["uLight"].SetValue(lightValue);effect.Parameters["uSmooth"].SetValue(smoothing);
                effect.Parameters["uLightDirection"].SetValue(lightDirection??new Vector2(-.5f,-.7f));
                effect.Parameters["uHeightUnit"].SetValue(heightUnit);
                effect.Parameters["uThicknessWorld"].SetValue(thicknessWorld??heightUnit*.035f);
                effect.Parameters["uEnvironmentStrength"]?.SetValue(environmentStrength);
                effect.Parameters["uSceneAvailable"]?.SetValue(sceneAvailable);
                effect.Parameters["uReflectionReachWorld"]?.SetValue(34f);
                device.SetRenderTarget(target);device.Clear(Color.Transparent);
                if(!diagnostics&&mode!=2)
                {
                    batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                    batch.Draw(background,Vector2.Zero,Color.White);batch.End();
                }
                VfxRenderState.DrawWithRestoredState(()=>{
                    device.BlendState=diagnostics?BlendState.Opaque:BlendState.NonPremultiplied;
                    device.DepthStencilState=DepthStencilState.None;device.RasterizerState=RasterizerState.CullNone;
                    foreach(var pass in effect.CurrentTechnique.Passes){pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,0,count/3);}
                });
                device.SetRenderTarget(null);var result=new Color[width*height];target.GetData(result);Save(target,name);return result;
            }
            void Save(RenderTarget2D texture,string name)
            {using var file=File.Create(Path.Combine(output,name+".png"));texture.SaveAsPng(file,width,height);}
            static int Difference(Color[] a,Color[] b)=>a.Zip(b).Count(p=>p.First!=p.Second);
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally{device.SetRenderTargets(oldTargets);device.Viewport=oldViewport;Main.instance.Exit();}
    }
}
