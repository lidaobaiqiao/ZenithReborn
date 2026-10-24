using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ZenithReborn;

namespace ZenithMaterialLab;

// 用真正打进测试模组的数据载荷检查RGBA四区，特别是Alpha为0处的RGB。
internal static class LabRegionCheck
{
    internal sealed record Result(bool Passed,bool DataChannelsPreserved,bool CoverageUnchanged,
        bool OutsideRegionsUnchanged,bool InheritedLayers,bool InvalidLayersRejected,object[] Channels);
    internal static Result Run(string output,LabResources resources,LabPreview preview)
    {
        var material=LabChecks.Preset("glass");
        material.Transmission=0;material.Metallic=0;material.LightStrength=0;material.EnvironmentStrength=0;
        material.AmbientColor=Vector3.Zero;material.BaseColor=new(.2f);
        material.Assets.EnvironmentMap="";
        material.Assets.LayerMask="ZenithMaterialLab/Assets/Diagnostics/RegionMask.zrdata";
        var loaded=SurfaceMaterialRenderer.LoadInputs(material);
        var data=new Color[loaded.LayerMask.Width*loaded.LayerMask.Height];loaded.LayerMask.GetData(data);
        int w=loaded.LayerMask.Width,h=loaded.LayerMask.Height;
        bool preserved=data[w/4+h/4*w]==new Color(255,0,0,0)&&data[w*3/4+h/4*w]==new Color(0,255,0,0)
            &&data[w/4+h*3/4*w]==new Color(0,0,255,0)&&data[w*3/4+h*3/4*w]==new Color(0,0,0,255);
        var baseline=preview.Render(material,loaded,resources.Background,0,.25f,true);
        preview.Save(Path.Combine(output,"region-baseline.png"));
        var baselineNormal=preview.Render(material,loaded,resources.Background,0,.25f,true,1);
        var results=new List<object>();bool all=preserved,coverage=true,outside=true;
        for(int channel=0;channel<4;channel++)
        {
            material.DetailLayers=[new(),new(),new(),new()];
            material.DetailLayers[channel]=new(){Weight=.9f,Tint=new(.2f,.55f,.9f),EmissionStrength=3};
            var actual=preview.Render(material,loaded,resources.Background,0,.25f,true);
            string label="RGBA"[channel].ToString();preview.Save(Path.Combine(output,$"region-{label}.png"));
            var normal=preview.Render(material,loaded,resources.Background,0,.25f,true,1);
            int changed=0,leaked=0;
            for(int y=110;y<430;y++)for(int x=140;x<820;x++)
            {
                int index=y*960+x;float u=(x-140f)/680,v=(y-110f)/320;
                coverage&=normal[index].A==baselineNormal[index].A;
                // 跳过双线性过滤的边界带，核对区域内部是否串到其他通道。
                if(Math.Abs(u-.5f)<.04f||Math.Abs(v-.5f)<.04f||u<.04f||u>.96f||v<.04f||v>.96f)continue;
                int selected=(u>=.5f?1:0)+(v>=.5f?2:0);
                int delta=Math.Abs(actual[index].R-baseline[index].R)+Math.Abs(actual[index].G-baseline[index].G)+Math.Abs(actual[index].B-baseline[index].B);
                if(delta>3){if(selected==channel)changed++;else leaked++;}
            }
            outside&=leaked==0;all&=changed>1000&&leaked==0;
            results.Add(new{channel="RGBA"[channel].ToString(),changed,outsideChanged=leaked});
        }
        string baseJson=material.ToJson();
        var inherited=MaterialInheritance.Resolve("{\"base\":\"parent.json\",\"Name\":\"继承分区\"}",_=>baseJson);
        bool inheritedLayers=inherited.DetailLayers.Count==4&&inherited.DetailLayers[3].EmissionStrength==3;
        bool invalid=false;try{material.DetailLayers.Add(new());material.Validate();}catch(ArgumentException){invalid=true;}
        return new(all&&coverage&&outside&&inheritedLayers&&invalid,preserved,coverage,outside,inheritedLayers,invalid,results.ToArray());
    }
}
