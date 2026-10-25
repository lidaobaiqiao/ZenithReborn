using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 恢复操作直接对照收藏79的编译着色器，不用新版本的美术阈值重新定义79。
internal static class Baseline79RenderCheck
{
    internal sealed record Result(bool Passed,int MaximumPixelDifference,int Cases);
    internal static Result Run(string output)
    {
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        var field=typeof(SwordFlowMaterial).GetField("effect",BindingFlags.Static|BindingFlags.NonPublic);
        using var baseline=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/Baselines/SwordFlow0379",AssetRequestMode.ImmediateLoad).Value.Clone();
        using var target=new RenderTarget2D(device,640,300,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        VertexPositionColorTexture[] mesh=[new(new(50,45,0),Color.White,new(0,0)),new(new(590,45,0),Color.White,new(1,0)),
            new(new(50,255,0),Color.White,new(0,1)),new(new(590,45,0),Color.White,new(1,0)),
            new(new(590,255,0),Color.White,new(1,1)),new(new(50,255,0),Color.White,new(0,1))];
        Effect current=null;int maximum=0,cases=0;
        try
        {
            SoulSceneRefraction.EndCapture();Render(null,new(120,195,255),0,0);current=(Effect)field.GetValue(null);
            foreach(var color in new Color[]{new(120,195,255),new(110,225,90),new(235,125,195)})
            foreach(float clock in new[]{.0f,.3f})
            foreach(float dissolve in new[]{0f,.5f})
            {
                var a=Render(baseline,color,clock,dissolve);var b=Render(current,color,clock,dissolve);
                for(int i=0;i<a.Length;i++)maximum=Math.Max(maximum,Math.Max(Math.Max(Math.Abs(a[i].R-b[i].R),Math.Abs(a[i].G-b[i].G)),Math.Max(Math.Abs(a[i].B-b[i].B),Math.Abs(a[i].A-b[i].A))));
                cases++;
            }
            var result=new Result(maximum==0,maximum,cases);
            File.WriteAllText(Path.Combine(output,"baseline79.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            return result;
        }
        finally{if(current!=null)field.SetValue(null,current);device.SetRenderTargets(saved);device.Viewport=viewport;}
        Color[] Render(Effect effect,Color color,float clock,float dissolve)
        {
            if(effect!=null)field.SetValue(null,effect);
            device.SetRenderTarget(target);device.Clear(Color.Transparent);
            MartialSlashRenderer.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(mesh,6,color,1,clock,640,300,true,dissolve));
            var pixels=new Color[640*300];target.GetData(pixels);return pixels;
        }
    }
}
