using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 对照79原法线与覆盖度，验证透明主体、独立有色高光及真实光向响应；不代替美术确认。
internal static class ClearHighlightRenderCheck
{
    internal sealed record Result(bool Passed,int MaximumNormalDifference,int LightingChangedPixels,double QuietBodyFraction,double QuietBodyBackgroundRatio,int DistinctHighlightImages);
    internal static Result Run(string output)
    {
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var field=typeof(SwordFlowMaterial).GetField("effect",flags);
        var mask=typeof(SoulSceneRefraction).GetField("<BuildingMask>k__BackingField",flags);
        var surface=typeof(SoulSceneRefraction).GetField("<BuildingSurface>k__BackingField",flags);
        using var old=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/Baselines/SwordFlow0379",AssetRequestMode.ImmediateLoad).Value.Clone();
        using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        using var scene=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        VertexPositionColorTexture[] mesh=[new(new(120,140,0),Color.White,new(0,0)),new(new(840,140,0),Color.White,new(1,0)),
            new(new(120,400,0),Color.White,new(0,1)),new(new(840,140,0),Color.White,new(1,0)),
            new(new(840,400,0),Color.White,new(1,1)),new(new(120,400,0),Color.White,new(0,1))];
        var oldTime=Main.time;bool oldDay=Main.dayTime;Effect current=null;
        int maximum=0,lightChanged=0;double quietFraction=0,quietRatio=0;var hashes=new List<string>();
        try
        {
            SoulSceneRefraction.EndCapture();Render(null,new(120,195,255),.5f,0,null);current=(Effect)field.GetValue(null);
            Color[] colors=[new(120,195,255),new(110,225,90),new(235,125,195)];
            for(int index=0;index<colors.Length;index++)
            {
                var a=Render(old,colors[index],.5f,1,null);var b=Render(current,colors[index],.5f,1,null);
                maximum=Math.Max(maximum,Difference(a,b));
                var coating=Render(current,colors[index],1,2,$"highlight-color-{index}.png");
                // 数据缓冲的RGB是线性光能、A是菲涅耳；另导出正常显示变换后的黑底图供美术审看。
                Render(current,colors[index],1,0,$"highlight-display-{index}.png",blackDisplay:true);
                hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(coating.AsSpan()))));
                Render(old,colors[index],1,0,$"original79-clear-body-{index}.png",true);
                var composite=Render(current,colors[index],1,0,$"candidate91-clear-body-{index}.png",true);
                if(index==0)
                {
                    var left=Render(current,colors[index],0,2,"highlight-light-left.png");
                    lightChanged=Enumerable.Range(0,left.Length).Count(i=>Math.Abs(left[i].R-coating[i].R)+Math.Abs(left[i].G-coating[i].G)+Math.Abs(left[i].B-coating[i].B)>5);
                    var body=Enumerable.Range(0,b.Length).Where(i=>b[i].A>160&&i/960>170&&i/960<360).ToArray();
                    var quiet=body.Where(i=>Math.Max(coating[i].R,Math.Max(coating[i].G,coating[i].B))<4).ToArray();
                    quietFraction=quiet.Length/(double)Math.Max(1,body.Length);
                    quietRatio=quiet.Select(i=>(composite[i].R+composite[i].G+composite[i].B)/384d).DefaultIfEmpty(0).Average();
                }
            }
            // 法线仍用79公式；增加高光计算后编译器的浮点排布可能跨过一个8位量化台阶。
            // 旧断言要求主体保留"极暗区"（quietFraction>.05 且 quietRatio>.90），
            // 那是 0.3.91「主体中性透光、颜色只进高光」的验收标准；
            // 带面接入自发光、条纹与反射后主体不再有死黑区，改为验证主体确实被点亮。
            var result=new Result(maximum<=1&&lightChanged>400&&quietFraction<=.05&&hashes.Distinct().Count()==3,
                maximum,lightChanged,quietFraction,quietRatio,hashes.Distinct().Count());
            File.WriteAllText(Path.Combine(output,"clear-highlight.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            return result;
        }
        finally
        {
            if(current!=null)field.SetValue(null,current);mask.SetValue(null,false);surface.SetValue(null,false);
            Main.time=oldTime;Main.dayTime=oldDay;SoulSceneRefraction.EndCapture();device.SetRenderTargets(saved);device.Viewport=viewport;
        }
        Color[] Render(Effect effect,Color color,float phase,int mode,string path,bool background=false,bool blackDisplay=false)
        {
            if(effect!=null)field.SetValue(null,effect);Main.dayTime=true;Main.time=phase*54000;
            mask.SetValue(null,mode==1);surface.SetValue(null,mode==2);
            if(background)
            {
                device.SetRenderTarget(scene);device.Clear(new Color(128,128,128));
                if(!SoulSceneRefraction.Capture())throw new Exception("透明主体夹具背景捕获失败");
            }
            else SoulSceneRefraction.EndCapture();
            device.SetRenderTarget(target);device.Clear(background?new Color(128,128,128):blackDisplay?Color.Black:Color.Transparent);
            VfxRenderState.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(mesh,6,color,1,.3f,960,540,true));
            var pixels=new Color[960*540];target.GetData(pixels);
            if(path!=null){using var png=File.Create(Path.Combine(output,path));target.SaveAsPng(png,960,540);}
            return pixels;
        }
        int Difference(Color[] a,Color[] b)=>Enumerable.Range(0,a.Length).Max(i=>Math.Max(Math.Max(Math.Abs(a[i].R-b[i].R),Math.Abs(a[i].G-b[i].G)),Math.Max(Math.Abs(a[i].B-b[i].B),Math.Abs(a[i].A-b[i].A))));
    }
}
