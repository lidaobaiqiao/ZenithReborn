using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 直接运行正式屏幕着色器，隔离背景亮度与材质自身亮部，验证泛光不会刷白透明主体。
internal static class GlassBloomRenderCheck
{
    internal sealed record Result(bool Passed,int EmptySurfaceChangedPixels,long RedGain,long GreenGain,long BlueGain);
    internal static Result Run(string output)
    {
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        using var target=new RenderTarget2D(device,64,64,false,SurfaceFormat.Color,DepthFormat.None);
        using var background=new Texture2D(device,1,1);using var normal=new Texture2D(device,1,1);
        using var coating=new Texture2D(device,1,1);
        using var shader=ModContent.Request<Effect>("ZenithReborn/Assets/Effects/ZRGlassScreen",AssetRequestMode.ImmediateLoad).Value.Clone();
        using var batch=new SpriteBatch(device);
        normal.SetData(new[]{new Color(128,128,0,255)});
        var p=shader.Parameters;
        p["uGlassTexel"].SetValue(new Vector2(1/64f));p["uGlassTransmission"].SetValue(.55f);
        p["uGlassBodyOpacity"].SetValue(.1f);p["uGlassSsrStrength"].SetValue(0f);
        p["uGlassThickness"].SetValue(0f);p["uGlassIor"].SetValue(1f);p["uMaskReady"].SetValue(1f);
        p["uHasMaterialProperties"].SetValue(0f);p["uMaterialProperties"].SetValue(normal);p["uMaterialTransmission"].SetValue(normal);
        foreach(var part in VfxIsolation.GlassParts)p[part.Uniform].SetValue(1f);
        try
        {
            background.SetData(new[]{new Color(220,220,220)});coating.SetData(new[]{Color.Transparent});
            var without=Render("bloom-bright-background-off",false);var with=Render("bloom-bright-background-on",true);
            int emptyChanges=without.Zip(with).Count(pair=>pair.First!=pair.Second);
            background.SetData(new[]{new Color(10,10,10)});coating.SetData(new[]{new Color(15,204,35,0)});
            without=Render("bloom-green-surface-off",false);with=Render("bloom-green-surface-on",true);
            long r=0,g=0,b=0;
            for(int i=0;i<with.Length;i++){r+=with[i].R-without[i].R;g+=with[i].G-without[i].G;b+=with[i].B-without[i].B;}
            return new(emptyChanges==0&&g>0&&g>r+b,emptyChanges,r,g,b);
        }
        finally{device.SetRenderTargets(saved);device.Viewport=viewport;}
        Color[] Render(string name,bool bloom)
        {
            device.SetRenderTarget(target);device.Clear(Color.Magenta);
            VfxRenderState.DrawWithRestoredState(()=>
            {
                p["uGlassPartBloom"].SetValue(bloom?1f:0f);
                batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone,shader);
                device.Textures[1]=normal;device.Textures[2]=coating;
                for(int i=0;i<3;i++)device.SamplerStates[i]=SamplerState.PointClamp;
                batch.Draw(background,new Rectangle(0,0,64,64),Color.White);batch.End();
            });
            var pixels=new Color[64*64];target.GetData(pixels);
            using var png=File.Create(Path.Combine(output,name+".png"));target.SaveAsPng(png,64,64);
            device.SetRenderTarget(null);return pixels;
        }
    }
}
