using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 仅隔离夹具：重放同一帧只改变IOR；主体密度合同由生产带面GPU夹具单独验证。
public class WorldRefractionProbe : ModSystem
{
    private bool captured;
    private object parameters;
    private float? probeIor;
    private float? probeWarp;
    private float? probeReflection;
    private float probeSpacePhase;
    private int applyCount;
    private readonly List<Texture2D> testProperties=new();
    public override void Load()
    {
        On_FilterManager.EndCapture+=Capture;
        On_ScreenShaderData.Apply+=Observe;
    }
    public override void Unload()
    {
        On_FilterManager.EndCapture-=Capture;
        On_ScreenShaderData.Apply-=Observe;
    }
    private bool Enabled=>ModContent.GetInstance<SoulFlightWorldCheck>().Isolated;
    private void Observe(On_ScreenShaderData.orig_Apply orig,ScreenShaderData self)
    {
        orig(self);
        if(!Enabled||self.GetType().DeclaringType!=typeof(SoulSceneRefraction))return;
        var device=Main.graphics.GraphicsDevice;
        if(probeIor.HasValue)
        {
            self.Shader.Parameters["uGlassIor"].SetValue(probeIor.Value);
            if(probeReflection.HasValue)self.Shader.Parameters["uGlassSsrStrength"]?.SetValue(probeReflection.Value);
            self.Shader.Parameters["uSpaceWarpPhase"]?.SetValue(probeSpacePhase);
            if(probeWarp.HasValue)self.Shader.Parameters["uGlassPartSpaceWarp"]?.SetValue(probeWarp.Value);
            if(self.Shader.Parameters["uHasMaterialProperties"].GetValueSingle()>.5f)
            {
                // 新材质的IOR属于逐像素属性；隔离对照只替换这个通道，其他输入完全相同。
                var source=self.Shader.Parameters["uMaterialProperties"].GetValueTexture2D();
                var pixels=new Color[source.Width*source.Height];source.GetData(pixels);
                for(int i=0;i<pixels.Length;i++)pixels[i].R=(byte)Math.Clamp((probeIor.Value-1)/1.5f*pixels[i].A,0,255);
                var texture=new Texture2D(device,source.Width,source.Height);texture.SetData(pixels);testProperties.Add(texture);
                self.Shader.Parameters["uMaterialProperties"].SetValue(texture);
            }
            self.Shader.CurrentTechnique.Passes["GlassScreen"].Apply();
            applyCount++;
        }
        parameters=new{
            ior=self.Shader.Parameters["uGlassIor"].GetValueSingle(),
            transmission=self.Shader.Parameters["uGlassTransmission"].GetValueSingle(),
            thickness=self.Shader.Parameters["uGlassThickness"].GetValueSingle(),
            bodyOpacity=self.Shader.Parameters["uGlassBodyOpacity"]?.GetValueSingle()??1f,
            screenReflectionStrength=self.Shader.Parameters["uGlassSsrStrength"]?.GetValueSingle()??0f,
            spaceWarpStrength=self.Shader.Parameters["uSpaceWarpStrength"]?.GetValueSingle()??0f,
            spaceWarpRadius=self.Shader.Parameters["uSpaceWarpRadius"]?.GetValueSingle()??0f,
            spaceWarpEnabled=self.Shader.Parameters["uGlassPartSpaceWarp"]?.GetValueSingle()??0f,
            ready=self.Shader.Parameters["uMaskReady"].GetValueSingle(),
            materialPropertiesReady=self.Shader.Parameters["uHasMaterialProperties"].GetValueSingle(),
            normalBoundToSlot1=ReferenceEquals(device.Textures[1],SoulSceneRefraction.NormalMask),
            optics=SwordFlowMaterial.DefaultOptics
        };
    }
    private void Capture(On_FilterManager.orig_EndCapture orig,FilterManager self,RenderTarget2D final,
        RenderTarget2D screen1,RenderTarget2D screen2,Color clear)
    {
        bool probe=Enabled&&!captured&&ModContent.GetInstance<SoulFlightWorldCheck>().VisualTick>=42&&
            ModContent.GetInstance<SoulFlightWorldCheck>().VisualTick<60;
        if(!probe){orig(self,final,screen1,screen2,clear);return;}
        captured=true;
        var device=Main.graphics.GraphicsDevice;var pp=device.PresentationParameters;
        var before=new Color[screen1.Width*screen1.Height];screen1.GetData(before);
        using var input=new Texture2D(device,screen1.Width,screen1.Height);input.SetData(before);
        using var batch=new SpriteBatch(device);
        Color[][] images=new Color[6][];
        probeSpacePhase=SwordSpaceDistortion.At(Main.GlobalTimeWrappedHourly,Main.screenPosition).Phase;
        try
        {
            for(int mode=0;mode<6;mode++)
            {
                // 第二遍只恢复夹具自身这帧的输入；正式游戏不走任何快照或重放路径。
                if(mode>0)
                {
                    device.SetRenderTarget(screen1);
                    batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.PointClamp,
                        DepthStencilState.None,RasterizerState.CullNone);
                    batch.Draw(input,Vector2.Zero,Color.White);batch.End();
                }
                probeIor=mode==0?1f:1.5f;
                probeWarp=mode<2?null:mode==2?0f:1f;
                probeReflection=mode==4?0f:mode==5?1f:null;
                orig(self,final,screen1,screen2,clear);
                images[mode]=new Color[pp.BackBufferWidth*pp.BackBufferHeight];device.GetBackBufferData(images[mode]);
            }
        }
        finally{probeIor=null;probeWarp=null;probeReflection=null;foreach(var texture in testProperties)texture.Dispose();testProperties.Clear();}
        var masks=new Color[before.Length];SoulSceneRefraction.NormalMask.GetData(masks);
        var materialBuffers=new Dictionary<string,int>();
        foreach(string field in new[]{"surfaceMask","materialProperties","materialTransmission"})
        {
            var texture=typeof(SoulSceneRefraction).GetField(field,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as Texture2D;
            if(texture==null)continue;
            var data=GlassBufferReadback.Read(texture);
            materialBuffers[field]=masks.Zip(data).Count(p=>Math.Abs(p.First.A-p.Second.A)>1);
        }
        long iorDifference=0;int changedPixels=0,outsideChangedPixels=0;
        for(int i=0;i<images[0].Length;i++)
        {
            int delta=Math.Abs(images[0][i].R-images[1][i].R)+Math.Abs(images[0][i].G-images[1][i].G)+Math.Abs(images[0][i].B-images[1][i].B);
            iorDifference+=delta;if(delta>3)changedPixels++;
            if(masks[i].A==0&&delta!=0)outsideChangedPixels++;
        }
        long reflectionDifference=0;int reflectionChanged=0,reflectionOutside=0;
        for(int i=0;i<images[4].Length;i++)
        {
            int delta=Math.Abs(images[4][i].R-images[5][i].R)+Math.Abs(images[4][i].G-images[5][i].G)+Math.Abs(images[4][i].B-images[5][i].B);
            reflectionDifference+=delta;if(delta>3)reflectionChanged++;
            if(masks[i].A==0&&delta!=0)reflectionOutside++;
        }
        string output=Environment.GetEnvironmentVariable("ZENITH_SOUL_FLIGHT_WORLD");
        Save(before,screen1.Width,screen1.Height,"before-refraction.png");
        Save(images[0],pp.BackBufferWidth,pp.BackBufferHeight,"native-ior-1.0.png");
        Save(images[1],pp.BackBufferWidth,pp.BackBufferHeight,"native-ior-1.5.png");
        Save(images[1],pp.BackBufferWidth,pp.BackBufferHeight,"after-refraction.png");
        Save(images[2],pp.BackBufferWidth,pp.BackBufferHeight,"native-space-warp-off.png");
        Save(images[3],pp.BackBufferWidth,pp.BackBufferHeight,"native-space-warp-on.png");
        Save(images[4],pp.BackBufferWidth,pp.BackBufferHeight,"reflection-off.png");
        Save(images[5],pp.BackBufferWidth,pp.BackBufferHeight,"reflection-on.png");
        var spaceWarp=SpaceWarpRegionCheck.Compare(images[2],images[3],masks,pp.BackBufferWidth,pp.BackBufferHeight,GlassMaterialPreset.SpaceWarpRadius);
        File.WriteAllText(Path.Combine(output,"native-probe.json"),JsonSerializer.Serialize(new{
            parameters,actualNativeFilter=true,sameFrameFullNativePipeline=true,applyCount,
            testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant(),
            probeTick=ModContent.GetInstance<SoulFlightWorldCheck>().VisualTick,
            iorDifference,changedPixels,outsideChangedPixels,reflectionDifference,reflectionChanged,reflectionOutside,
            reflectionPassed=reflectionDifference>1000&&reflectionChanged>100&&reflectionOutside==0,spaceWarp,materialBuffers,
            passed=applyCount==6&&iorDifference>100000&&changedPixels>1000&&outsideChangedPixels==0&&
                spaceWarp.OutsideBladeChangedPixels>100&&spaceWarp.BeyondReachChangedPixels==0
        },new JsonSerializerOptions{WriteIndented=true}));
        void Save(Color[] pixels,int width,int height,string name)
        {
            using var image=new Texture2D(device,width,height);image.SetData(pixels);
            using var png=File.Create(Path.Combine(output,name));image.SaveAsPng(png,width,height);
        }
    }
}
