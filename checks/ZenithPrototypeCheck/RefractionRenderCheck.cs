using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 在相同背景、相同网格和相同透明度下比较折射率，不能用颜色／透明度变化冒充折射。
internal static class RefractionRenderCheck
{
    internal sealed record Result(bool Passed,bool ScenePreserved,bool DiscardTargetPreserved,
        long PatternIorDifference,long SolidIorDifference,int SharpHighlightPixels,int RoughHighlightPixels,
        float Ior,float Roughness,float Transmission);

    internal static Result Run(string output)
    {
        var device=Main.graphics.GraphicsDevice;
        var saved=device.GetRenderTargets();var viewport=device.Viewport;
        using var scene=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        using var result=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        using var batch=new SpriteBatch(device);
        var a=new VertexPositionColorTexture(new Vector3(120,140,0),Color.White,new Vector2(0,0));
        var b=new VertexPositionColorTexture(new Vector3(840,140,0),Color.White,new Vector2(1,0));
        var c=new VertexPositionColorTexture(new Vector3(120,400,0),Color.White,new Vector2(0,1));
        var d=new VertexPositionColorTexture(new Vector3(840,400,0),Color.White,new Vector2(1,1));
        VertexPositionColorTexture[] surface=[a,b,c,b,d,c];
        bool preserved=true,discardPreserved;
        try
        {
            long patterned=Difference(true),solid=Difference(false);
            SoulSceneRefraction.EndCapture();
            int sharp=Highlight(.08f),rough=Highlight(.6f);
            using var discard=new RenderTarget2D(device,16,16,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.DiscardContents);
            device.SetRenderTarget(discard);var marker=new Color(39,94,151);device.Clear(marker);
            bool skipped=!SoulSceneRefraction.Capture();
            var pixels=new Color[256];discard.GetData(pixels);
            discardPreserved=skipped&&pixels.All(p=>p==marker);
            return new(preserved&&discardPreserved&&patterned>solid*2+10000&&sharp>0&&rough>sharp,
                preserved,discardPreserved,patterned,solid,sharp,rough,1.5f,.08f,.55f);
        }
        finally
        {
            SoulSceneRefraction.EndCapture();device.SetRenderTargets(saved);device.Viewport=viewport;
        }

        long Difference(bool patterned)
        {
            device.SetRenderTarget(scene);device.Clear(new Color(82,103,127));
            if(patterned)
            {
                batch.Begin(SpriteSortMode.Deferred,BlendState.Opaque,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                for(int y=0;y<540;y+=12)for(int x=0;x<960;x+=24)
                    batch.Draw(TextureAssets.MagicPixel.Value,new Rectangle(x,y,24,12),new Rectangle(0,0,1,1),
                        ((x/24+y/12)%2==0)?new Color(32,59,85):new Color(205,182,147));
                batch.End();
            }
            var before=new Color[960*540];scene.GetData(before);
            preserved&=SoulSceneRefraction.Capture();
            var after=new Color[before.Length];scene.GetData(after);
            preserved&=before.SequenceEqual(after)&&ReferenceEquals(device.GetRenderTargets()[0].RenderTarget,scene);
            Color[][] images=new Color[2][];
            for(int mode=0;mode<2;mode++)
            {
                device.SetRenderTarget(result);
                batch.Begin(SpriteSortMode.Deferred,BlendState.Opaque);batch.Draw(scene,Vector2.Zero,Color.White);batch.End();
                MartialSlashRenderer.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(surface,6,new Color(120,195,255),1,.3f,
                    960,540,true,optics:new SwordFlowMaterial.GlassOptics(mode==0?1:1.5f,.08f,1)));
                images[mode]=new Color[before.Length];result.GetData(images[mode]);
                if(patterned)
                {using var png=File.Create(Path.Combine(output,mode==0?"ior-1.0.png":"ior-1.5.png"));result.SaveAsPng(png,960,540);}
            }
            long delta=0;
            for(int y=170;y<330;y++)for(int x=150;x<800;x++)
            {
                int i=y*960+x;
                delta+=Math.Abs(images[0][i].R-images[1][i].R)+Math.Abs(images[0][i].G-images[1][i].G)+Math.Abs(images[0][i].B-images[1][i].B);
            }
            return delta;
        }
        int Highlight(float roughness)
        {
            device.SetRenderTarget(result);device.Clear(Color.Transparent);
            MartialSlashRenderer.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(surface,6,new Color(120,195,255),1,.3f,
                960,540,true,optics:new SwordFlowMaterial.GlassOptics(1.5f,roughness,.55f)));
            var pixels=new Color[960*540];result.GetData(pixels);
            return pixels.Count(pixel=>pixel.R>180&&pixel.G>180&&pixel.B>180);
        }
    }
}
