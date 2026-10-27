using System.Reflection;
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

// 完整贴图材质的独立GPU检查：记录真实绑定、逐图消融、光向与动画，不套用旧79的像素合同。
public sealed class TexturedSwordMaterialRenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_TEXTURED_SWORD_RENDER");
        if(ran||string.IsNullOrEmpty(output)||!Main.gameMenu||Main.menuMode!=0||!Main.SavePath.Contains("textured-sword-profile"))return;
        ran=true;Directory.CreateDirectory(output);
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        bool oldDay=Main.dayTime;double oldTime=Main.time;
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var maps=(Dictionary<string,Texture2D>)typeof(SwordFlowMaterial).GetField("maps",flags).GetValue(null);
        var mask=typeof(SoulSceneRefraction).GetField("<BuildingMask>k__BackingField",flags);
        var surface=typeof(SoulSceneRefraction).GetField("<BuildingSurface>k__BackingField",flags);
        var vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxLayeredVertices];
        var centers=new Vector2[13];var rotations=new float[13];
        for(int i=0;i<13;i++)
        {
            float t=1-i/12f;var tip=new Vector2(90+700*t,55+295*t*t);
            var axis=Vector2.Normalize(new Vector2(590*t,-700));
            centers[i]=tip-axis*54;rotations[i]=axis.ToRotation()+MathHelper.PiOver4;
        }
        int count=SoulMaterialTrail.BuildLayered(centers,rotations,13,54,Vector2.Zero,vertices,
            new[]{new SoulMaterialTrail.TrailLayer(0,13,1,true)},.3f);
        using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        using var scene=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        using var backgroundBatch=new SpriteBatch(device);
        var changes=new Dictionary<string,object>();
        try
        {
            VfxIsolation.ApplyDefaultPreset();Main.dayTime=true;Main.time=16200;
            var baseline=Render("all-textures-day");
            var effect=(Effect)typeof(SwordFlowMaterial).GetField("effect",flags).GetValue(null);
            var actualParameters=new{opacity=effect.Parameters["uBodyOpacity"].GetValueSingle(),coverageVisibility=effect.Parameters["uOpacity"].GetValueSingle(),roughness=effect.Parameters["uRoughness"].GetValueSingle(),
                metallic=effect.Parameters["uMetallic"]?.GetValueSingle()??0};
            string[] useNames={"uUseBrush","uUseNoise","uUseNormal","uUseSurface","uUseHeight","uUseEmission","uUseEnvironment","uUseZoneMask","uUseColorField","uUseMotionMask"};
            var uses=useNames.ToDictionary(n=>n,n=>effect.Parameters[n]?.GetValueSingle()??-1);
            var loaded=maps.ToDictionary(p=>p.Key,p=>new[]{p.Value.Width,p.Value.Height});
            var normal=Render("all-textures-normal",mode:1);
            var clearOptics=SwordFlowMaterial.DefaultOptics with{BodyOpacity=0};
            var solidOptics=SwordFlowMaterial.DefaultOptics with{BodyOpacity=1};
            var clearCoverage=Render("coverage-body-0",mode:1,optics:clearOptics);
            var solidCoverage=Render("coverage-body-1",mode:1,optics:solidOptics);
            bool coverageIndependent=normal.SequenceEqual(clearCoverage)&&normal.SequenceEqual(solidCoverage);
            var clearReflection=Render("body-0-black",sceneKind:0,optics:clearOptics);
            Render("body-0-surface-data",mode:2,optics:clearOptics);
            int reflectionVisible=clearReflection.Count(p=>p.R>40||p.G>40||p.B>40);
            var lowPattern=Render("body-current-bright-background",sceneKind:2);
            var solidPlain=Render("body-1-plain",optics:solidOptics);
            var solidPattern=Render("body-1-bright-background",sceneKind:2,optics:solidOptics);
            long lowBackgroundResponse=Response(baseline,lowPattern,normal);
            long solidBackgroundResponse=Response(solidPlain,solidPattern,normal);
            bool glassContract=coverageIndependent&&reflectionVisible>100&&lowBackgroundResponse>solidBackgroundResponse;
            int nonzeroRoughnessPixels=normal.Count(p=>p.A>8&&p.B!=0);
            int metallicChanged=Difference(baseline,Render("metallic-zero",optics:SwordFlowMaterial.DefaultOptics with{Metallic=0}));
            int roughnessChanged=Difference(baseline,Render("roughness-reference",optics:SwordFlowMaterial.DefaultOptics with{
                Roughness=GlassMaterialPreset.Roughness==0?.08f:0}));
            foreach(string key in new[]{"NormalMap","SurfaceMap","HeightMap","EmissionMap","EnvironmentMap","ZoneMask"})
            {
                if(!maps.Remove(key,out var texture)){changes[key]=new{missing=true};continue;}
                try
                {
                    var pixels=Render("without-"+key);
                    var normals=Render("normal-without-"+key,mode:1);
                    changes[key]=new{changedPixels=Difference(baseline,pixels),normalChangedPixels=Difference(normal,normals)};
                }
                finally{maps[key]=texture;}
            }
            foreach(string name in new[]{"defaultBrush","defaultNoise"})
            {
                var field=typeof(SwordFlowMaterial).GetField(name,flags);var texture=field.GetValue(null);
                field.SetValue(null,null);
                try{changes[name]=new{changedPixels=Difference(baseline,Render("without-"+name))};}
                finally{field.SetValue(null,texture);}
            }
            var palette=new List<string>();
            foreach(int item in new[]{ItemID.TerraBlade,ItemID.EnchantedSword,ItemID.Starfury})
                palette.Add(Hash(Render("palette-"+item,item:item)));
            Main.dayTime=false;Main.time=12000;var night=Render("all-textures-night");
            Main.dayTime=true;Main.time=16200;
            Render("all-textures-black",sceneKind:0);Render("all-textures-bright-background",sceneKind:2);
            // 主动沿迹条纹与贴图杂纹分别消融，避免把设计条带误报成网格棱纹。
            VfxIsolation.PartValues[23]=0;
            int stripeChanged=Difference(baseline,Render("without-P24"));
            Render("black-without-P24",sceneKind:0);
            VfxIsolation.PartValues[23]=1;
            var animation=new List<string>();
            for(int i=0;i<12;i++)animation.Add(Hash(Render("motion-"+i.ToString("00"),clock:.3f+i/12f)));
            var expired=Render("expired",mode:1,opacity:0);var dissolved=Render("dissolved",mode:1,dissolve:1);
            var refraction=RefractionRenderCheck.Run(output);
            var coloredHighlight=SwordHighlightRenderCheck.Run((name,item,mode)=>Render(name,mode,item,sceneKind:0));
            var bloom=GlassBloomRenderCheck.Run(output);
            var spaceWarp=SwordSpaceDistortionRenderCheck.Run(output,normal,960,540);
            bool all= !VfxIsolation.PureCodeOnly&&VfxIsolation.Branches.All(b=>b.Get())&&VfxIsolation.Details.All(d=>d.Get())&&
                VfxIsolation.PartValues.All(v=>v==1)&&VfxIsolation.GlassPartValues.All(v=>v==1);
            bool bound=useNames.Take(8).All(n=>uses[n]==1);
            bool tuning=actualParameters.opacity==GlassMaterialPreset.Opacity&&actualParameters.roughness==GlassMaterialPreset.Roughness&&
                actualParameters.metallic==GlassMaterialPreset.Metallic&&
                (GlassMaterialPreset.Roughness!=0||nonzeroRoughnessPixels==0)&&(GlassMaterialPreset.Metallic==0||metallicChanged>0);
            var report=new{passed=all&&bound&&palette.Distinct().Count()==3&&animation.Distinct().Count()>1&&
                    expired.All(p=>p.A==0)&&dissolved.All(p=>p.A==0)&&refraction.Passed&&tuning&&glassContract&&coloredHighlight.Passed&&bloom.Passed&&spaceWarp.Passed,
                actualGpu=true,productionRenderer=true,syntheticPoses=true,allEffectsEnabled=all,
                enabledOptionCount=VfxIsolation.Branches.Length+VfxIsolation.Details.Length+VfxIsolation.Parts.Length+VfxIsolation.GlassParts.Length,uses,loaded,changes,
                actualParameters,coloredHighlight,bloom,spaceWarp,nonzeroRoughnessPixels,metallicChangedPixels=metallicChanged,roughnessChangedPixels=roughnessChanged,
                glassContract=new{passed=glassContract,coverageIndependentOfBodyOpacity=coverageIndependent,
                    reflectionVisiblePixelsAtBodyZero=reflectionVisible,lowBodyBackgroundResponse=lowBackgroundResponse,
                    solidBodyBackgroundResponse=solidBackgroundResponse},
                lightingChangedPixels=Difference(baseline,night),p24ChangedPixels=stripeChanged,distinctPaletteImages=palette.Distinct().Count(),
                distinctAnimationFrames=animation.Distinct().Count(),refraction,
                testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant(),
                worldTested=false,artApproved=false};
            File.WriteAllText(Path.Combine(output,"textured-material.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally
        {
            mask.SetValue(null,false);surface.SetValue(null,false);Main.dayTime=oldDay;Main.time=oldTime;
            SoulSceneRefraction.EndCapture();VfxIsolation.ApplyDefaultPreset();device.SetRenderTargets(saved);device.Viewport=viewport;Main.instance.Exit();
        }
        Color[] Render(string name,int mode=0,int item=ItemID.TerraBlade,float clock=.3f,int sceneKind=1,float opacity=1,float dissolve=0,
            SwordFlowMaterial.GlassOptics? optics=null)
        {
            SoulSceneRefraction.EndCapture();
            if(sceneKind>0)
            {
                // 背景响应合同使用精确清屏值；棋盘IOR对照由独立RefractionRenderCheck负责。
                device.SetRenderTarget(scene);device.Clear(sceneKind==2?new Color(200,205,210):new Color(100,115,135));
                if(!SoulSceneRefraction.Capture())throw new Exception("材质夹具未能保留背景");
            }
            device.SetRenderTarget(target);device.Clear(mode==0?new Color(10,13,17):Color.Transparent);
            if(mode==0&&sceneKind>0)
            {backgroundBatch.Begin(SpriteSortMode.Deferred,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                backgroundBatch.Draw(scene,Vector2.Zero,Color.White);backgroundBatch.End();}
            mask.SetValue(null,mode==1);surface.SetValue(null,mode==2);
            try{MartialSlashRenderer.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(vertices,count,SoulVisuals.Tint(item),opacity,clock,960,540,true,dissolve,item,optics));}
            finally{mask.SetValue(null,false);surface.SetValue(null,false);}
            var pixels=new Color[960*540];target.GetData(pixels);
            using(var png=File.Create(Path.Combine(output,name+".png")))target.SaveAsPng(png,960,540);
            device.SetRenderTarget(null);return pixels;
        }
        static int Difference(Color[] a,Color[] b)=>a.Zip(b).Count(p=>Math.Abs(p.First.R-p.Second.R)+Math.Abs(p.First.G-p.Second.G)+Math.Abs(p.First.B-p.Second.B)+Math.Abs(p.First.A-p.Second.A)>2);
        static long Response(Color[] a,Color[] b,Color[] coverage)
        {
            long sum=0;
            for(int i=0;i<a.Length;i++)if(coverage[i].A>128)
                sum+=Math.Abs(a[i].R-b[i].R)+Math.Abs(a[i].G-b[i].G)+Math.Abs(a[i].B-b[i].B);
            return sum;
        }
        static string Hash(Color[] pixels)=>Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan())));
    }
}
