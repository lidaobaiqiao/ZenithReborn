using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 将实际生产带面覆盖交给正式屏幕着色器，对照空间开关、静态纯色背景和生命周期清空。
internal static class SwordSpaceDistortionRenderCheck
{
    internal sealed record Result(bool Passed,SpaceWarpRegionCheck.Result Region,int AnimationChangedPixels,
        int SolidBackgroundChangedPixels,int ClearedMaskChangedPixels,int MaxRedCoordinateChange,int MaxGreenCoordinateChange);
    internal static Result Run(string output,Color[] coverage,int width,int height)
    {
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        using var target=new RenderTarget2D(device,width,height,false,SurfaceFormat.Color,DepthFormat.None);
        using var background=new Texture2D(device,width,height);using var normals=new Texture2D(device,width,height);
        using var empty=new Texture2D(device,1,1);empty.SetData(new[]{Color.Transparent});normals.SetData(coverage);
        using var shader=ModContent.Request<Effect>("ZenithReborn/Assets/Effects/ZRGlassScreen",AssetRequestMode.ImmediateLoad).Value.Clone();
        using var batch=new SpriteBatch(device);var p=shader.Parameters;
        p["uGlassTexel"].SetValue(new Vector2(1f/width,1f/height));p["uGlassTransmission"].SetValue(.55f);
        p["uGlassBodyOpacity"].SetValue(.1f);p["uGlassSsrStrength"].SetValue(0f);p["uGlassThickness"].SetValue(0f);
        p["uGlassIor"].SetValue(1f);p["uMaskReady"].SetValue(1f);p["uHasMaterialProperties"].SetValue(0f);
        p["uMaterialProperties"].SetValue(empty);p["uMaterialTransmission"].SetValue(empty);
        foreach(var part in VfxIsolation.GlassParts)p[part.Uniform].SetValue(0f);
        var image=new Color[width*height];
        try
        {
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)image[y*width+x]=
                x%48<3||y%48<3?new Color(220,205,172):((x/48+y/48)%2==0?new Color(48,89,123):new Color(93,143,159));
            background.SetData(image);
            var off=Render("space-warp-off",false,0);var on=Render("space-warp-on",true,0);
            var region=SpaceWarpRegionCheck.Compare(off,on,coverage,width,height,GlassMaterialPreset.SpaceWarpRadius);
            var animated=Render("space-warp-motion",true,1.2f);
            int animation=Difference(on,animated);
            Array.Fill(image,new Color(95,115,135));background.SetData(image);
            int solid=Difference(Render("space-warp-solid-off",false,0),Render("space-warp-solid-on",true,0));
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)image[y*width+x]=new Color(x/(float)(width-1),y/(float)(height-1),.25f);
            background.SetData(image);off=Render("space-warp-coordinate-off",false,0);on=Render("space-warp-coordinate-on",true,0);
            int dx=off.Zip(on).Max(c=>Math.Abs(c.First.R-c.Second.R)),dy=off.Zip(on).Max(c=>Math.Abs(c.First.G-c.Second.G));
            Array.Fill(image,Color.Transparent);normals.SetData(image);
            int cleared=Difference(Render("space-warp-cleared-off",false,0),Render("space-warp-cleared-on",true,0));
            bool bounded=dx<=Math.Ceiling(GlassMaterialPreset.SpaceWarpStrength*255/(width-1))+1&&
                dy<=Math.Ceiling(GlassMaterialPreset.SpaceWarpStrength*255/(height-1))+1;
            return new(region.OutsideBladeChangedPixels>200&&region.BeyondReachChangedPixels==0&&animation>100&&solid==0&&cleared==0&&bounded,
                region,animation,solid,cleared,dx,dy);
        }
        finally{device.SetRenderTargets(saved);device.Viewport=viewport;}
        Color[] Render(string name,bool enabled,float clock)
        {
            device.SetRenderTarget(target);device.Clear(Color.Magenta);
            MartialSlashRenderer.DrawWithRestoredState(()=>
            {
                SwordSpaceDistortion.Apply(p,clock,Vector2.Zero);p["uGlassPartSpaceWarp"].SetValue(enabled?1f:0f);
                batch.Begin(SpriteSortMode.Immediate,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,shader);
                device.Textures[1]=normals;device.Textures[2]=empty;
                batch.Draw(background,new Rectangle(0,0,width,height),Color.White);batch.End();
            });
            var pixels=new Color[width*height];target.GetData(pixels);
            using var png=File.Create(Path.Combine(output,name+".png"));target.SaveAsPng(png,width,height);
            device.SetRenderTarget(null);return pixels;
        }
        static int Difference(Color[] a,Color[] b)=>a.Zip(b).Count(c=>Math.Abs(c.First.R-c.Second.R)+Math.Abs(c.First.G-c.Second.G)+Math.Abs(c.First.B-c.Second.B)>3);
    }
}
