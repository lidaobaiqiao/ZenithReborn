using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 独立性能夹具：固定真实刀光网格，比较相同绘制下的液态材质与背景采样基线。
public sealed class LiquidGlassPerfCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime gameTime)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_LIQUID_PERF_RENDER");
        if(ran||string.IsNullOrEmpty(output)||!Main.gameMenu||Main.menuMode!=0||!Main.SavePath.Contains("liquid-perf-profile"))return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;var targets=device.GetRenderTargets();var viewport=device.Viewport;
        const int width=960,height=540;const float halfLength=54;
        const int batchSize=32,samples=8,warmups=4;
        try
        {
            // 使用与隔离画面检查相同的固定姿态；这不是实际世界中的逐帧负载。
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
            var vertices=LiquidGlassTrailMesh.Prepare(original,count).Vertices;
            if(count<=0)throw new InvalidOperationException("性能网格为空");
            using var target=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            using var background=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            using var pixel=new Texture2D(device,1,1);pixel.SetData(new[]{Color.White});
            using var batch=new SpriteBatch(device);
            using var baseline=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassPerfBase",AssetRequestMode.ImmediateLoad).Value.Clone();
            using var candidate=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/LiquidGlass/LiquidGlassTrail",AssetRequestMode.ImmediateLoad).Value.Clone();
            using var timer=new LiquidGlassGpuTimer(device);
            device.SetRenderTarget(background);device.Clear(Color.Black);
            batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
            for(int y=0;y<height;y+=24)for(int x=0;x<width;x+=24)
                batch.Draw(pixel,new Rectangle(x,y,24,24),((x/24+y/24)%2==0)?new Color(72,91,102):new Color(168,185,186));
            batch.End();
            var projection=Matrix.CreateOrthographicOffCenter(0,width,height,0,-1,1);
            baseline.Parameters["uTransform"].SetValue(projection);baseline.Parameters["uScene"].SetValue(background);
            candidate.Parameters["uTransform"].SetValue(projection);candidate.Parameters["uScene"].SetValue(background);
            candidate.Parameters["uSceneSize"].SetValue(new Vector2(width,height));
            candidate.Parameters["uMaterialLength"].SetValue(materialLength);candidate.Parameters["uTrailDistance"].SetValue(distance);
            candidate.Parameters["uSurfaceWidth"].SetValue(halfLength*1.52f);candidate.Parameters["uHeightUnit"].SetValue(200f);
            candidate.Parameters["uWorldToScreen"].SetValue(Matrix.Identity);candidate.Parameters["uThicknessWorld"].SetValue(7f);
            candidate.Parameters["uMode"].SetValue(3f);candidate.Parameters["uTime"].SetValue(.3f);
            candidate.Parameters["uLight"].SetValue(0f);candidate.Parameters["uSmooth"].SetValue(0f);
            candidate.Parameters["uLightDirection"].SetValue(new Vector2(.6f,-1f));
            var baseTimes=new List<double>();var candidateTimes=new List<double>();
            device.SetRenderTarget(target);device.Clear(Color.Transparent);
            MartialSlashRenderer.DrawWithRestoredState(()=>{
                device.BlendState=BlendState.NonPremultiplied;device.DepthStencilState=DepthStencilState.None;
                device.RasterizerState=RasterizerState.CullNone;
                // 预热后交错两种顺序，时间戳只包围相同数量的绘制，不包含贴图准备或读回。
                for(int i=0;i<warmups;i++){Measure(baseline);Measure(candidate);}
                for(int i=0;i<samples;i++)
                {
                    if(i%2==0){baseTimes.Add(Measure(baseline));candidateTimes.Add(Measure(candidate));}
                    else{candidateTimes.Add(Measure(candidate));baseTimes.Add(Measure(baseline));}
                }
            });
            double Median(List<double> values)
            {
                var sorted=values.OrderBy(x=>x).ToArray();return (sorted[(sorted.Length-1)/2]+sorted[sorted.Length/2])*.5;
            }
            double medianBase=Median(baseTimes),medianCandidate=Median(candidateTimes);
            var check=Path.Combine(Main.SavePath,"Mods/ZenithPrototypeCheck.tmod");
            var core=Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod");
            File.WriteAllText(Path.Combine(output,"liquid-performance.json"),JsonSerializer.Serialize(new{
                passed=true,method="D3D11 timestamp and disjoint frequency queries",actualGpu=true,
                everySampleFrequencyValidAndNotDisjoint=true,queryTimeoutSeconds=5,
                independentSyntheticPose=true,productionPipeline=false,wholeFrameBenchmark=false,
                width,height,vertexCount=count,drawsPerSample=batchSize,samplesPerVariant=samples,warmupBatchesPerVariant=warmups,
                baseline="同网格、投影、场景采样与混合，不计算液态高度及光学",candidate="独立液态高度、法线、反光和折射一次绘制",
                measuredRegion="仅重复网格绘制；不包含清屏、目标切换、读回、生产多缓冲或游戏其余绘制",
                baselineGpuMillisecondsPerDraw=baseTimes,candidateGpuMillisecondsPerDraw=candidateTimes,
                baselineMedianMilliseconds=medianBase,candidateMedianMilliseconds=medianCandidate,
                medianDifferenceMilliseconds=medianCandidate-medianBase,
                testedCheckPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(check))).ToLowerInvariant(),
                testedGeometrySourcePackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core))).ToLowerInvariant()
            },new JsonSerializerOptions{WriteIndented=true}));
            double Measure(Effect effect)
            {
                effect.CurrentTechnique.Passes[0].Apply();
                return timer.MeasureMilliseconds(()=>device.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,0,count/3),batchSize);
            }
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally{device.SetRenderTargets(targets);device.Viewport=viewport;Main.instance.Exit();}
    }
}
