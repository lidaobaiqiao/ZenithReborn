using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using ReLogic.Content;

namespace ZenithReborn;

// 游戏复用原版滤镜的当前帧背景，提前准备材质数据，不复制或累积游戏场景。
// 下方保留的安全快照入口仅供隔离材质夹具验证；不能切离丢弃型目标时直接跳过。
[Autoload(Side=ModSide.Client)]
public class SoulSceneRefraction : ModSystem
{
    private static RenderTarget2D snapshot;
    private static SpriteBatch batch;
    private static RenderTarget2D normalMask;
    private static RenderTarget2D surfaceMask;
    private static RenderTarget2D materialProperties;
    private static RenderTarget2D materialTransmission;
    private static Effect screenEffect;
    private static GlassScreenData screenData;
    private static readonly VertexPositionColorTexture[] vertices=new VertexPositionColorTexture[SoulMaterialTrail.MaxLayeredVertices];
    private const string FilterKey="ZenithReborn:SwordGlass";
    private static bool maskReady;
    private static ulong preparedTick;
    private static int additionalOutput;
    public static bool BuildingMask {get;private set;}
    public static bool BuildingSurface {get;private set;}
    public static int MaterialOutputMode=>BuildingMask?1:BuildingSurface?2:additionalOutput;
    public static bool CompositingReady=>maskReady&&preparedTick==Main.GameUpdateCount&&screenData!=null&&Filters.Scene[FilterKey].IsActive();
    public static Texture2D NormalMask=>normalMask;
    public static Texture2D Scene=>Available?snapshot:null;
    public static bool Available {get;private set;}
    public static Vector4 UvTransform {get;private set;}=new(0,0,1,1);
    public static int CapturedFrames {get;private set;}
    public static int DeferredFrames {get;private set;}
    public static string LastSource {get;private set;}="none";

    public override void Load()
    {
        On_FilterManager.BeginCapture+=BeginWorldCapture;
        Main.QueueMainThreadAction(()=>{
            screenEffect=ModContent.Request<Effect>("ZenithReborn/Assets/Effects/ZRGlassScreen",AssetRequestMode.ImmediateLoad).Value.Clone();
            screenData=new GlassScreenData(screenEffect);
            Filters.Scene[FilterKey]=new Filter(screenData,EffectPriority.VeryHigh);
            Filters.Scene[FilterKey].Load();
        });
    }
    public override void PostUpdateEverything()
    {
        if(screenData==null)return;
        bool active=!Main.gameMenu&&SoulTrailEnding.HasActive;
        if(!Main.gameMenu)foreach(var p in Main.ActiveProjectiles)if(p.ModProjectile is SoulBlade){active=true;break;}
        if(active&&!Filters.Scene[FilterKey].IsActive())Filters.Scene.Activate(FilterKey);
        else if(!active&&Filters.Scene[FilterKey].IsActive()){Filters.Scene.Deactivate(FilterKey);maskReady=false;}
    }
    private static void BeginWorldCapture(On_FilterManager.orig_BeginCapture orig,FilterManager self,RenderTarget2D target,Color clear)
    {
        if(ReferenceEquals(self,Filters.Scene)&&screenData!=null&&Filters.Scene[FilterKey].IsActive())PrepareNormals();
        orig(self,target,clear);
    }
    private static void PrepareNormals()
    {
        maskReady=false;
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        if(saved.Any(b=>b.RenderTarget is not RenderTarget2D t||t.RenderTargetUsage!=RenderTargetUsage.PreserveContents))
        {DeferredFrames++;return;}
        var usage=device.PresentationParameters.RenderTargetUsage;
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        var scissor=device.ScissorRectangle;var bindings=device.GetVertexBuffers();var indices=device.Indices;
        var textures=Enumerable.Range(0,16).Select(i=>device.Textures[i]).ToArray();
        var samplers=Enumerable.Range(0,16).Select(i=>device.SamplerStates[i]).ToArray();
        try
        {
            device.PresentationParameters.RenderTargetUsage=RenderTargetUsage.PreserveContents;
            if(normalMask==null||normalMask.IsDisposed||normalMask.Width!=Main.screenWidth||normalMask.Height!=Main.screenHeight)
            {
                normalMask?.Dispose();surfaceMask?.Dispose();normalMask=new RenderTarget2D(device,Main.screenWidth,Main.screenHeight,false,
                    SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
                surfaceMask=new RenderTarget2D(device,Main.screenWidth,Main.screenHeight,false,
                    SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            }
            for(int i=0;i<16;i++)device.Textures[i]=null;
            device.SetRenderTarget(normalMask);device.Clear(Color.Transparent);BuildingMask=true;
            MartialSlashRenderer.DrawWithRestoredState(()=>{
                foreach(var p in Main.ActiveProjectiles)if(p.ModProjectile is SoulBlade blade)blade.DrawRibbonShader(vertices);
                SoulTrailEnding.DrawAll(vertices);
            });
            // 法线和表面共享同一轨迹与动态形状；不把彩色主体预先涂入待折射的世界。
            BuildingMask=false;BuildingSurface=true;device.SetRenderTarget(surfaceMask);device.Clear(Color.Transparent);
            MartialSlashRenderer.DrawWithRestoredState(()=>{
                foreach(var p in Main.ActiveProjectiles)if(p.ModProjectile is SoulBlade blade)blade.DrawRibbonShader(vertices);
                SoulTrailEnding.DrawAll(vertices);
            });
            BuildingSurface=false;
            // 默认剑流与通用材质共用已有的光学属性/透射缓冲，不再把默认路径当纯彩色覆盖层。
            {
                if(materialProperties==null||materialProperties.IsDisposed||materialProperties.Width!=Main.screenWidth||materialProperties.Height!=Main.screenHeight)
                {
                    materialProperties?.Dispose();materialTransmission?.Dispose();
                    materialProperties=new RenderTarget2D(device,Main.screenWidth,Main.screenHeight,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
                    materialTransmission=new RenderTarget2D(device,Main.screenWidth,Main.screenHeight,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
                }
                for(int mode=3;mode<=4;mode++)
                {
                    additionalOutput=mode;for(int i=0;i<16;i++)device.Textures[i]=null;
                    device.SetRenderTarget(mode==3?materialProperties:materialTransmission);device.Clear(Color.Transparent);
                    MartialSlashRenderer.DrawWithRestoredState(()=>{
                        foreach(var p in Main.ActiveProjectiles)if(p.ModProjectile is SoulBlade blade)blade.DrawRibbonShader(vertices);
                        SoulTrailEnding.DrawAll(vertices);
                    });
                }
            }
            additionalOutput=0;preparedTick=Main.GameUpdateCount;
            maskReady=true;
        }
        finally
        {
            BuildingMask=false;BuildingSurface=false;additionalOutput=0;device.SetRenderTargets(saved);device.PresentationParameters.RenderTargetUsage=usage;
            device.Viewport=viewport;device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=raster;
            device.ScissorRectangle=scissor;device.SetVertexBuffers(bindings);device.Indices=indices;
            for(int i=0;i<16;i++){device.Textures[i]=textures[i];device.SamplerStates[i]=samplers[i];}
        }
    }
    private sealed class GlassScreenData : ScreenShaderData
    {
#pragma warning disable CS0618
        internal GlassScreenData(Effect effect):base(new Ref<Effect>(effect),"GlassScreen"){}
#pragma warning restore CS0618
        public override void Apply()
        {
            if(normalMask!=null)UseImage(normalMask,0,SamplerState.LinearClamp);
            if(surfaceMask!=null)UseImage(surfaceMask,1,SamplerState.LinearClamp);
            Shader.Parameters["uGlassTexel"].SetValue(new Vector2(1f/Main.screenWidth,1f/Main.screenHeight));
            Shader.Parameters["uGlassTransmission"].SetValue(SwordFlowMaterial.DefaultOptics.Transmission);
            Shader.Parameters["uGlassThickness"].SetValue(GlassMaterialPreset.Thickness);
            Shader.Parameters["uGlassIor"].SetValue(SwordFlowMaterial.DefaultOptics.Ior);
            Shader.Parameters["uMaskReady"].SetValue(maskReady?1f:0f);
            Shader.Parameters["uHasMaterialProperties"].SetValue(materialProperties!=null&&maskReady?1f:0f);
            Shader.Parameters["uMaterialProperties"].SetValue(materialProperties??normalMask);
            Shader.Parameters["uMaterialTransmission"].SetValue(materialTransmission??normalMask);
            if(maskReady){CapturedFrames++;LastSource="native-screen-filter-current-frame";}
            base.Apply();
        }
    }

    public static bool Capture()
    {
        Available=false;
        var device=Main.graphics.GraphicsDevice;
        var saved=device.GetRenderTargets();var viewport=device.Viewport;
        if(saved.Any(binding=>binding.RenderTarget is not RenderTarget2D target||target.RenderTargetUsage!=RenderTargetUsage.PreserveContents))
        {DeferredFrames++;LastSource="discard-target-deferred";return false;}
        var source=saved.Length==1?saved[0].RenderTarget as RenderTarget2D:Main.screenTarget;
        if(source==null||source.IsDisposed||source==snapshot||source.Width<2||source.Height<2)
        {LastSource="no-readable-scene";return false;}
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        var scissor=device.ScissorRectangle;var vertices=device.GetVertexBuffers();var indices=device.Indices;
        var samplers=Enumerable.Range(0,3).Select(i=>device.SamplerStates[i]).ToArray();
        var textures=Enumerable.Range(0,3).Select(i=>device.Textures[i]).ToArray();
        var usage=device.PresentationParameters.RenderTargetUsage;
        try
        {
            if(snapshot==null||snapshot.IsDisposed||snapshot.Width!=source.Width||snapshot.Height!=source.Height)
            {
                snapshot?.Dispose();
                snapshot=new RenderTarget2D(device,source.Width,source.Height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            }
            batch??=new SpriteBatch(device);
            device.PresentationParameters.RenderTargetUsage=RenderTargetUsage.PreserveContents;
            for(int i=0;i<3;i++)device.Textures[i]=null;
            device.SetRenderTarget(snapshot);
            batch.Begin(SpriteSortMode.Deferred,BlendState.Opaque,SamplerState.LinearClamp,
                DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
            batch.Draw(source,new Rectangle(0,0,source.Width,source.Height),Color.White);batch.End();
            UvTransform=new Vector4(viewport.X/(float)source.Width,viewport.Y/(float)source.Height,
                viewport.Width/(float)source.Width,viewport.Height/(float)source.Height);
            Available=true;CapturedFrames++;
            LastSource=saved.Length==1?"current-preserved-scene":"native-screen-target";
            return true;
        }
        finally
        {
            device.SetRenderTargets(saved);device.PresentationParameters.RenderTargetUsage=usage;
            device.Viewport=viewport;Main.pixelShader.CurrentTechnique.Passes[0].Apply();
            device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=raster;device.ScissorRectangle=scissor;
            for(int i=0;i<3;i++){device.SamplerStates[i]=samplers[i];device.Textures[i]=textures[i];}
            device.SetVertexBuffers(vertices);device.Indices=indices;
        }
    }
    public static void EndCapture()=>Available=false;
    public override void Unload()
    {
        On_FilterManager.BeginCapture-=BeginWorldCapture;
        if(screenData!=null)Filters.Scene.Deactivate(FilterKey);
        var oldNormal=normalMask;var oldSurface=surfaceMask;var oldScreen=screenEffect;normalMask=null;surfaceMask=null;screenEffect=null;screenData=null;maskReady=false;
        var oldProperties=materialProperties;var oldTransmission=materialTransmission;materialProperties=null;materialTransmission=null;additionalOutput=0;
        var old=snapshot;var oldBatch=batch;snapshot=null;batch=null;Available=false;
        CapturedFrames=DeferredFrames=0;LastSource="none";
        Main.QueueMainThreadAction(()=>{old?.Dispose();oldBatch?.Dispose();oldNormal?.Dispose();oldSurface?.Dispose();oldScreen?.Dispose();oldProperties?.Dispose();oldTransmission?.Dispose();});
    }
}
