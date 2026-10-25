using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 固定几何、色场时间与覆盖度，只换光照/环境方向，检查高光确实响应材质而不是坐标白线。
internal static class GlassResponseRenderCheck
{
    internal sealed record Result(bool Passed,int LightingChangedPixels,int EnvironmentChangedPixels,
        int RoughnessChangedPixels,int MaximumAlphaDifference,int WhitePixels);
    internal static Result Run(string output)
    {
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        var a=new VertexPositionColorTexture(new Vector3(120,140,0),Color.White,new(0,0));
        var b=new VertexPositionColorTexture(new Vector3(840,140,0),Color.White,new(1,0));
        var c=new VertexPositionColorTexture(new Vector3(120,400,0),Color.White,new(0,1));
        var d=new VertexPositionColorTexture(new Vector3(840,400,0),Color.White,new(1,1));
        VertexPositionColorTexture[] mesh=[a,b,c,b,d,c];
        SoulSceneRefraction.EndCapture();
        try
        {
            var left=Render("response-light-left.png",new(.85f,-.25f,.45f),0,.08f);
            var right=Render("response-light-right.png",new(-.85f,.25f,.45f),0,.08f);
            var rotated=Render("response-environment-rotated.png",new(.85f,-.25f,.45f),.35f,.08f);
            var rough=Render("response-rough.png",new(.85f,-.25f,.45f),0,.55f);
            int lighting=Changed(left,right),environment=Changed(left,rotated),roughness=Changed(left,rough);
            int alpha=Enumerable.Range(0,left.Length).Max(i=>Math.Abs(left[i].A-right[i].A)+Math.Abs(left[i].A-rotated[i].A)+Math.Abs(left[i].A-rough[i].A));
            var result=new Result(lighting>400&&environment>400&&roughness>400&&alpha==0,lighting,environment,roughness,alpha,
                left.Count(p=>p.R>245&&p.G>245&&p.B>245));
            File.WriteAllText(Path.Combine(output,"glass-response.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            return result;
        }
        finally{device.SetRenderTargets(saved);device.Viewport=viewport;}
        Color[] Render(string file,Vector3 light,float rotation,float roughness)
        {
            device.SetRenderTarget(target);device.Clear(Color.Transparent);
            MartialSlashRenderer.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(mesh,6,new Color(120,195,255),1,.3f,960,540,true,
                optics:new(1.5f,roughness,.55f),lighting:Vector3.Normalize(light),environmentRotation:rotation));
            var pixels=new Color[960*540];target.GetData(pixels);
            using var stream=File.Create(Path.Combine(output,file));target.SaveAsPng(stream,960,540);return pixels;
        }
        int Changed(Color[] x,Color[] y)=>Enumerable.Range(0,x.Length).Count(i=>Math.Abs(x[i].R-y[i].R)+Math.Abs(x[i].G-y[i].G)+Math.Abs(x[i].B-y[i].B)>5);
    }
}
