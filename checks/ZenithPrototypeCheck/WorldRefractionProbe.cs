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

// 仅隔离夹具：将完全相同的一帧送过真实原版滤镜链两次，只改变自有滤镜的IOR。
public class WorldRefractionProbe : ModSystem
{
    private bool captured;
    private object parameters;
    private float? probeIor;
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
            ready=self.Shader.Parameters["uMaskReady"].GetValueSingle(),
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
        Color[][] images=new Color[2][];
        try
        {
            for(int mode=0;mode<2;mode++)
            {
                // 第二遍只恢复夹具自身这帧的输入；正式游戏不走任何快照或重放路径。
                if(mode==1)
                {
                    device.SetRenderTarget(screen1);
                    batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.PointClamp,
                        DepthStencilState.None,RasterizerState.CullNone);
                    batch.Draw(input,Vector2.Zero,Color.White);batch.End();
                }
                probeIor=mode==0?1f:1.5f;
                orig(self,final,screen1,screen2,clear);
                images[mode]=new Color[pp.BackBufferWidth*pp.BackBufferHeight];device.GetBackBufferData(images[mode]);
            }
        }
        finally{probeIor=null;foreach(var texture in testProperties)texture.Dispose();testProperties.Clear();}
        var masks=new Color[before.Length];SoulSceneRefraction.NormalMask.GetData(masks);
        long iorDifference=0;int changedPixels=0,outsideChangedPixels=0;
        for(int i=0;i<images[0].Length;i++)
        {
            int delta=Math.Abs(images[0][i].R-images[1][i].R)+Math.Abs(images[0][i].G-images[1][i].G)+Math.Abs(images[0][i].B-images[1][i].B);
            iorDifference+=delta;if(delta>3)changedPixels++;
            if(masks[i].A==0&&delta!=0)outsideChangedPixels++;
        }
        string output=Environment.GetEnvironmentVariable("ZENITH_SOUL_FLIGHT_WORLD");
        Save(before,screen1.Width,screen1.Height,"before-refraction.png");
        Save(images[0],pp.BackBufferWidth,pp.BackBufferHeight,"native-ior-1.0.png");
        Save(images[1],pp.BackBufferWidth,pp.BackBufferHeight,"native-ior-1.5.png");
        Save(images[1],pp.BackBufferWidth,pp.BackBufferHeight,"after-refraction.png");
        File.WriteAllText(Path.Combine(output,"native-probe.json"),JsonSerializer.Serialize(new{
            parameters,actualNativeFilter=true,sameFrameFullNativePipeline=true,applyCount,
            testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant(),
            probeTick=ModContent.GetInstance<SoulFlightWorldCheck>().VisualTick,
            iorDifference,changedPixels,outsideChangedPixels,
            passed=applyCount==2&&iorDifference>100000&&changedPixels>1000&&outsideChangedPixels==0
        },new JsonSerializerOptions{WriteIndented=true}));
        void Save(Color[] pixels,int width,int height,string name)
        {
            using var image=new Texture2D(device,width,height);image.SetData(pixels);
            using var png=File.Create(Path.Combine(output,name));image.SaveAsPng(png,width,height);
        }
    }
}
