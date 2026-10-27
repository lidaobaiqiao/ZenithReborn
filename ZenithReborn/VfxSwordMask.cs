using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using ReLogic.Content;

namespace ZenithReborn;

// 运行时只画一次合成结果：缓慢流动底色 × 连续插值的动态蒙版。
// 所有离屏合成都在 Main.OnPreDraw 执行，绝不在弹幕 PreDraw 中打断原版批次。
[Autoload(Side=ModSide.Client)]
public class VfxSwordMask : ModSystem
{
    private const int Frames=16,Columns=4,FrameW=640,FrameH=256;
    private static Texture2D noise;
    private static SpriteBatch batch;
    private static RenderTarget2D mask;
    private static BlendState multiply,add;
    private static readonly Dictionary<int,Texture2D> atlases=new();
    private static readonly Dictionary<int,string> atlasPaths=new();
    private static readonly Dictionary<int,RenderTarget2D> targets=new();
    private static readonly Dictionary<int,VfxMaskData.Row> rows=new();
    private static readonly HashSet<int> disabled=new();

    public override void Load()
    {
        // 纯代码排查不登记或加载矿石剑身图集，避免其他飞剑路径偷偷带入素材。
        if(VfxIsolation.PureCodeOnly)return;
        foreach(var row in VfxMaskData.Rows)
        {
            string path="ZenithReborn/Assets/Vfx/SwordBody/"+Terraria.ID.ItemID.Search.GetName(row.ItemId);
            if(ModContent.HasAsset(path)){rows[row.ItemId]=row;atlasPaths[row.ItemId]=path;}
        }
        Main.OnPreDraw+=PrepareFrame;
    }
    // 切世界时缓冲里的旧剑迹不清会留成"残余剑影"，必须一起释放。
    public override void OnWorldUnload()
    {
        var old=targets.Values.ToList();
        targets.Clear();
        Main.QueueMainThreadAction(()=>{foreach(var resource in old)resource.Dispose();});
    }

    public override void Unload()
    {
        Main.OnPreDraw-=PrepareFrame;
        var owned=targets.Values.Cast<IDisposable>().Concat(new IDisposable[]{mask,batch,multiply,add,noise}).Where(value=>value!=null).ToArray();
        Main.QueueMainThreadAction(()=>{foreach(var resource in owned)resource.Dispose();});
        targets.Clear();atlases.Clear();atlasPaths.Clear();rows.Clear();disabled.Clear();
        mask=null;batch=null;multiply=null;add=null;noise=null;
    }
    public static bool Has(int itemType)=>!VfxIsolation.PureCodeOnly&&atlasPaths.ContainsKey(itemType)&&!disabled.Contains(itemType);
    private static void PrepareFrame(GameTime gameTime)
    {
        if(Main.gameMenu||Main.dedServ)return;
        var needed=new HashSet<int>();
        foreach(var projectile in Main.ActiveProjectiles)
            if(projectile.ModProjectile is OreFlyingSword sword&&sword.Launched)
                needed.Add(OreFamily.MaterialOf(sword.VisualType)==sword.Material?sword.VisualType:
                    OreFamily.DefaultType[Math.Clamp(sword.Material,0,13)]);
        foreach(var end in OrePierceFeedback.Endings.Ends)if(!end.Giant)needed.Add(end.VisualType);
        foreach(int item in needed)Prepare(item,Main.GlobalTimeWrappedHourly);
    }
    // 只能从没有开启绘制批次的准备阶段调用；隔离 GPU 夹具也复用这个真实入口。
    public static Texture2D Prepare(int itemType,float time)
    {
        if(!Has(itemType))return null;
        var device=Main.graphics.GraphicsDevice;
        var saved=device.GetRenderTargets();var viewport=device.Viewport;
        // 不改其他模组的目标属性，也不切离丢弃型目标；遇到这种准备时机，
        // 延后合成并沿用已有结果，避免重新绑定时把别人的场景清空。
        if(saved.Any(binding=>binding.RenderTarget is not RenderTarget2D target||target.RenderTargetUsage!=RenderTargetUsage.PreserveContents))
            return targets.GetValueOrDefault(itemType);
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        var scissor=device.ScissorRectangle;
        var samplers=Enumerable.Range(0,3).Select(i=>device.SamplerStates[i]).ToArray();
        var textures=Enumerable.Range(0,3).Select(i=>device.Textures[i]).ToArray();
        var vertices=device.GetVertexBuffers();var indices=device.Indices;
        var backBufferUsage=device.PresentationParameters.RenderTargetUsage;
        try
        {
            // 返回窗口缓冲时也保留已有像素，避免清掉此前绘制者留下的内容。
            device.PresentationParameters.RenderTargetUsage=RenderTargetUsage.PreserveContents;
            batch??=new SpriteBatch(device);
            // 只替换色场噪声来源，原来的蒙版、配色、滚动速度和合成方式保持。
            noise??=SwordBodyColorNoise.Create(device);
            multiply??=new BlendState{ColorSourceBlend=Blend.Zero,ColorDestinationBlend=Blend.SourceAlpha,
                AlphaSourceBlend=Blend.Zero,AlphaDestinationBlend=Blend.SourceAlpha};
            add??=new BlendState{ColorSourceBlend=Blend.One,ColorDestinationBlend=Blend.One,
                AlphaSourceBlend=Blend.One,AlphaDestinationBlend=Blend.One};
            mask??=new RenderTarget2D(device,FrameW,FrameH);
            if(!atlases.TryGetValue(itemType,out var atlas))
                atlases[itemType]=atlas=ModContent.Request<Texture2D>(atlasPaths[itemType],AssetRequestMode.ImmediateLoad).Value;
            if(!targets.TryGetValue(itemType,out var result)||result.IsDisposed)
                targets[itemType]=result=new RenderTarget2D(device,FrameW,FrameH);
            var row=rows[itemType];
            // 相邻两帧先相加得到一个插值蒙版；不是两套错相的彩色特效。
            float phase=time/row.LoopSeconds*Frames;int a=(int)phase%Frames,b=(a+1)%Frames;float f=phase-MathF.Floor(phase);
            Rectangle Frame(int i)=>new(i%Columns*FrameW,i/Columns*FrameH,FrameW,FrameH);
            device.SetRenderTarget(mask);device.Clear(Color.Transparent);
            Begin(add,SamplerState.LinearClamp);
            batch.Draw(atlas,Vector2.Zero,Frame(a),Color.White*(1-f));
            batch.Draw(atlas,Vector2.Zero,Frame(b),Color.White*f);batch.End();
            device.SetRenderTarget(result);device.Clear(row.Clear);
            Begin(BlendState.AlphaBlend,SamplerState.LinearWrap);
            batch.Draw(noise,new Rectangle(0,0,FrameW,FrameH),
                new Rectangle((int)(time*row.PrimaryScroll),0,noise.Width,noise.Height),row.Primary*row.PrimaryOpacity);
            batch.Draw(noise,new Rectangle(0,0,FrameW,FrameH),
                new Rectangle((int)(time*row.SecondaryScroll),noise.Height/3,noise.Width,noise.Height),row.Secondary*row.SecondaryOpacity);
            batch.End();Begin(multiply,SamplerState.LinearClamp);
            batch.Draw(mask,Vector2.Zero,Color.White);batch.End();
            return result;
        }
        catch(Exception error)
        {
            disabled.Add(itemType);ModContent.GetInstance<VfxSwordMask>().Mod.Logger.Error($"剑身蒙版合成失败，物品 {itemType}",error);
            return null;
        }
        finally
        {
            try{device.SetRenderTargets(saved);}
            finally
            {
                device.PresentationParameters.RenderTargetUsage=backBufferUsage;
            }
            device.Viewport=viewport;device.BlendState=blend;
            device.DepthStencilState=depth;device.RasterizerState=raster;device.ScissorRectangle=scissor;
            for(int i=0;i<3;i++){device.SamplerStates[i]=samplers[i];device.Textures[i]=textures[i];}
            device.SetVertexBuffers(vertices);device.Indices=indices;
        }
    }
    private static void Begin(BlendState state,SamplerState sampler)=>batch.Begin(SpriteSortMode.Deferred,
        state,sampler,DepthStencilState.None,RasterizerState.CullNone,null,Matrix.Identity);
    public static void Draw(int itemType,Vector2 center,float rotation,float scale,float time,float alpha=1f)
    {
        if(alpha<=0||!Has(itemType)||!targets.TryGetValue(itemType,out var target))return;
        var row=rows[itemType];
        // H3 直接按固定画布密度采样原版贴图，不再用近似剑尖长度反推倍率。
        float canvasScale=scale/(1.85f*1.3122883f);
        // 单次柔和加色让气流在浅色背景上不变成黑烟；原版剑体随后正常覆盖。
        Main.spriteBatch.Draw(target,center-Main.screenPosition,null,new Color(255,255,255,0)*alpha,rotation,
            new Vector2(row.PivotX,128),canvasScale,SpriteEffects.None,0);
    }
}
