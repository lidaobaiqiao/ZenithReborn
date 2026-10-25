using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 只在隔离菜单中临时替换输入，使用原始80着色器逐项定位纹理伪影，退出前恢复全部资源。
internal static class TextureAblationRenderCheck
{
    internal static void Run(string output)
    {
        var device=Main.graphics.GraphicsDevice;
        var targets=device.GetRenderTargets();var viewport=device.Viewport;
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var effectField=typeof(SwordFlowMaterial).GetField("effect",flags);
        var maps=(Dictionary<string,Texture2D>)typeof(SwordFlowMaterial).GetField("maps",flags).GetValue(null);
        var savedMaps=new Dictionary<string,Texture2D>(maps);
        var surfaceField=typeof(SoulSceneRefraction).GetField("<BuildingSurface>k__BackingField",flags);
        var maskField=typeof(SoulSceneRefraction).GetField("<BuildingMask>k__BackingField",flags);
        // 先完成生产Effect初始化，后续才切换到收藏的80，避免首帧加载覆盖夹具状态。
        var mesh=new VertexPositionColorTexture[SoulMaterialTrail.MaxVertices];
        var centers=new Vector2[SoulMaterialTrail.HistoryLength];var rotations=new float[centers.Length];
        for(int i=0;i<centers.Length;i++)
        {
            float t=(36-i)*.046f;
            centers[i]=new Vector2(450+MathF.Sin(t)*280,280+MathF.Sin(t*1.8f)*100);
            rotations[i]=t*1.1f+MathHelper.PiOver4;
        }
        int count=SoulMaterialTrail.Build(centers,rotations,centers.Length,52,Vector2.Zero,mesh,.3f);
        using var target=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        using var baseline=ModContent.Request<Effect>("ZenithPrototypeCheck/Assets/Baselines/SwordFlow0380",AssetRequestMode.ImmediateLoad).Value.Clone();
        using var flatHeight=Flat(new Color(128,128,128));
        using var flatNormal=Flat(new Color(128,128,255));
        using var flatSurface=Flat(Mean(savedMaps["SurfaceMap"]));
        using var flatEnvironment=Flat(Mean(savedMaps["EnvironmentMap"]));
        using var scene=new RenderTarget2D(device,960,540,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        var cases=new List<object>();Effect original=null;
        try
        {
            SoulSceneRefraction.EndCapture();
            device.SetRenderTarget(target);
            SwordFlowMaterial.Draw(mesh,count,new Color(120,195,255),1,.3f,960,540,true);
            original=(Effect)effectField.GetValue(null);effectField.SetValue(null,baseline);
            foreach(string variant in new[]{"original80","flat-height","flat-normal","flat-height-normal","flat-surface","flat-environment","soft-environment","flat-all"})
            {
                foreach(var pair in savedMaps)maps[pair.Key]=pair.Value;
                if(variant is "flat-height" or "flat-height-normal" or "flat-all")maps["HeightMap"]=flatHeight;
                if(variant is "flat-normal" or "flat-height-normal" or "flat-all")maps["NormalMap"]=flatNormal;
                if(variant is "flat-surface" or "flat-all")maps["SurfaceMap"]=flatSurface;
                if(variant is "flat-environment" or "flat-all")maps["EnvironmentMap"]=flatEnvironment;
                if(variant=="soft-environment")maps["EnvironmentMap"]=savedMaps["EnvironmentSoftMap"];
                foreach(string channel in new[]{"normal","surface","night","day"})
                {
                    maskField.SetValue(null,channel=="normal");surfaceField.SetValue(null,channel=="surface");
                    if(channel is "night" or "day")
                    {
                        device.SetRenderTarget(scene);device.Clear(channel=="night"?new Color(8,12,20):new Color(130,165,205));
                        if(!SoulSceneRefraction.Capture())throw new Exception("隔离背景捕获失败");
                    }
                    else SoulSceneRefraction.EndCapture();
                    device.SetRenderTarget(target);device.Clear(Color.Transparent);
                    MartialSlashRenderer.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(mesh,count,new Color(120,195,255),1,.3f,960,540,true,
                        lighting:Vector3.Normalize(new(.85f,-.25f,.45f)),ambientLight:1));
                    var pixels=new Color[960*540];target.GetData(pixels);
                    using(var png=File.Create(Path.Combine(output,$"texture-{variant}-{channel}.png")))target.SaveAsPng(png,960,540);
                    cases.Add(new{variant,channel,coveredPixels=pixels.Count(p=>p.A>0),mean=pixels.Where(p=>p.A>0).Select(p=>p.R*.2126+p.G*.7152+p.B*.0722).DefaultIfEmpty(0).Average()});
                }
            }
            File.WriteAllText(Path.Combine(output,"texture-ablation.json"),JsonSerializer.Serialize(new{actualGpu=true,baseline="0.3.80",productionMaps=savedMaps.Keys,cases,
                worldGameplayTested=false,liveMultiplayerTested=false},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally
        {
            foreach(var pair in savedMaps)maps[pair.Key]=pair.Value;
            if(original!=null)effectField.SetValue(null,original);
            maskField.SetValue(null,false);surfaceField.SetValue(null,false);
            SoulSceneRefraction.EndCapture();device.SetRenderTargets(targets);device.Viewport=viewport;
        }
        Texture2D Flat(Color value){var texture=new Texture2D(device,1,1);texture.SetData(new[]{value});return texture;}
        Color Mean(Texture2D texture)
        {
            var pixels=new Color[texture.Width*texture.Height];texture.GetData(pixels);
            return new Color((int)pixels.Average(p=>(double)p.R),(int)pixels.Average(p=>(double)p.G),(int)pixels.Average(p=>(double)p.B));
        }
    }
}
