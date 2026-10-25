using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 固定输入对照原始80：白天保持逐像素一致，暗处只降低表面反光，不改变折射数据与形状。
internal static class DayNightMaterialRenderCheck
{
    internal sealed record Result(bool Passed,int DayMaximumDifference,int MaximumOpticalDifference,double NightLuminanceRatio);
    internal static Result Run(string output)
    {
        var device=Main.graphics.GraphicsDevice;var targets=device.GetRenderTargets();var viewport=device.Viewport;
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var field=typeof(SwordFlowMaterial).GetField("effect",flags);
        var mode=typeof(SoulSceneRefraction).GetField("additionalOutput",flags);
        var mask=typeof(SoulSceneRefraction).GetField("<BuildingMask>k__BackingField",flags);
        using var old=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/Baselines/SwordFlow0380",AssetRequestMode.ImmediateLoad).Value.Clone();
        using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        VertexPositionColorTexture[] mesh=[new(new(120,140,0),Color.White,new(0,0)),new(new(840,140,0),Color.White,new(1,0)),
            new(new(120,400,0),Color.White,new(0,1)),new(new(840,140,0),Color.White,new(1,0)),
            new(new(840,400,0),Color.White,new(1,1)),new(new(120,400,0),Color.White,new(0,1))];
        Effect current=null;int maximumDay=0,maximumOptical=0;double maximumRatio=0;var cases=new List<object>();
        try
        {
            SoulSceneRefraction.EndCapture();
            Render(null,1,0,new(120,195,255),null);current=(Effect)field.GetValue(null);
            Color[] colors=[new(120,195,255),new(110,225,90),new(235,125,195)];
            for(int index=0;index<colors.Length;index++)
            {
                var daylight=Render(old,1,0,colors[index],$"old80-day-{index}.png");
                var day=Render(current,1,0,colors[index],$"new89-day-{index}.png");
                maximumDay=Math.Max(maximumDay,Difference(daylight,day));
                var nightOld=Render(old,.04f,0,colors[index],$"old80-night-{index}.png");
                var night=Render(current,.04f,0,colors[index],$"new89-night-{index}.png");
                var dusk=Render(current,.35f,0,colors[index],$"new89-dusk-{index}.png");
                var torch=Render(current,.85f,0,colors[index],$"new89-lit-{index}.png");
                foreach(int outputMode in new[]{1,3,4})
                {
                    var a=Render(old,1,outputMode,colors[index],null);
                    var b=Render(current,.04f,outputMode,colors[index],null);
                    maximumOptical=Math.Max(maximumOptical,Difference(a,b));
                }
                double oldMean=Mean(nightOld),nightMean=Mean(night),duskMean=Mean(dusk),litMean=Mean(torch);
                double ratio=nightMean/Math.Max(.001,oldMean);maximumRatio=Math.Max(maximumRatio,ratio);
                bool passed=ratio<.60&&nightMean<duskMean&&duskMean<litMean&&Difference(day,torch)==0&&
                    Enumerable.Range(0,day.Length).All(i=>day[i].A==night[i].A);
                cases.Add(new{index,oldNightMean=oldMean,nightMean,duskMean,litMean,ratio,passed});
                if(!passed)throw new Exception($"昼夜材质响应不符：{index} 暗处比值={ratio}");
            }
            var result=new Result(maximumDay==0&&maximumOptical==0,maximumDay,maximumOptical,maximumRatio);
            File.WriteAllText(Path.Combine(output,"day-night.json"),JsonSerializer.Serialize(new{result.Passed,
                actualGpu=true,baseline="0.3.80",result.DayMaximumDifference,result.MaximumOpticalDifference,
                result.NightLuminanceRatio,cases,actualMouseGameplayTested=false,liveMultiplayerTested=false},new JsonSerializerOptions{WriteIndented=true}));
            return result;
        }
        finally
        {
            if(current!=null)field.SetValue(null,current);
            mode.SetValue(null,0);mask.SetValue(null,false);device.SetRenderTargets(targets);device.Viewport=viewport;
        }
        Color[] Render(Effect effect,float ambient,int outputMode,Color tint,string path)
        {
            if(effect!=null)field.SetValue(null,effect);
            mode.SetValue(null,outputMode>=3?outputMode:0);mask.SetValue(null,outputMode==1);
            device.SetRenderTarget(target);device.Clear(Color.Transparent);
            MartialSlashRenderer.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(mesh,6,tint,1,.3f,960,540,true,
                lighting:Vector3.Normalize(new(.85f,-.25f,.45f)),ambientLight:ambient));
            var pixels=new Color[960*540];target.GetData(pixels);
            if(path!=null){using var stream=File.Create(Path.Combine(output,path));target.SaveAsPng(stream,960,540);}
            return pixels;
        }
        int Difference(Color[] a,Color[] b)=>Enumerable.Range(0,a.Length).Max(i=>Math.Max(Math.Max(Math.Abs(a[i].R-b[i].R),Math.Abs(a[i].G-b[i].G)),Math.Max(Math.Abs(a[i].B-b[i].B),Math.Abs(a[i].A-b[i].A))));
        double Mean(Color[] pixels)=>pixels.Where(p=>p.A>160).Average(p=>p.R*.2126+p.G*.7152+p.B*.0722);
    }
}
