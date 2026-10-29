using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 仅隔离夹具：分辨笔刷、内部条纹、刃线和曲面高光，不把某张纹理直接认定为唯一原因。
internal static class GlassHighlightProbe
{
    internal sealed record Sample(Color[] Pixels,Color[] Coverage);
    internal static void Run(string output,Func<string,int,float,string,Sample> render)
    {
        var field=typeof(SwordFlowMaterial).GetField("defaultBrush",BindingFlags.Static|BindingFlags.NonPublic);
        var brush=field.GetValue(null);
        var cases=new (string Name,bool Brush,string[] Off)[]{
            ("all",true,[]),("without-brush",false,[]),
            ("without-stripe",true,["uPartStripe"]),
            ("without-edge",true,["uPartRazorCore","uPartRazorHalo","uPartRazorMist"]),
            ("without-band",true,["uPartBandReflection"]),
            ("without-soft-highlight",true,["uPartSoftLobe"]),
            ("without-brush-and-stripe",false,["uPartStripe"]),
            ("surface-reflection",true,["uPartStripe","uPartRazorCore","uPartRazorHalo","uPartRazorMist","uPartEmission"])
        };
        var records=new Dictionary<string,object>();Sample reference=null;
        try
        {
            foreach(var sample in cases)
            {
                VfxIsolation.ApplyDefaultPreset();field.SetValue(null,sample.Brush?brush:null);
                foreach(string name in sample.Off)
                    VfxIsolation.PartValues[Array.FindIndex(VfxIsolation.Parts,p=>p.Uniform==name)]=0;
                render(sample.Name+"-world",ItemID.TerraBlade,.3f,"day");
                var result=render(sample.Name+"-surface",ItemID.TerraBlade,.3f,"black");
                var pixels=result.Pixels;reference??=result;
                records[sample.Name]=new{
                    changedPixels=reference.Pixels.Zip(pixels).Count(p=>p.First!=p.Second),
                    coverageChangedPixels=reference.Coverage.Zip(result.Coverage).Count(p=>p.First!=p.Second),
                    visiblePixels=pixels.Count(p=>Math.Max(p.R,Math.Max(p.G,p.B))>20),
                    brightPixels=pixels.Count(p=>Math.Max(p.R,Math.Max(p.G,p.B))>100),
                    rgbEnergy=pixels.Sum(p=>(long)p.R+p.G+p.B)
                };
            }
            VfxIsolation.ApplyDefaultPreset();field.SetValue(null,brush);
            foreach(int item in new[]{ItemID.EnchantedSword,ItemID.Starfury})render("palette-"+item,item,.3f,"day");
            for(int frame=0;frame<3;frame++)
            {
                Main.time=9000+frame*12000;
                render("light-"+frame,ItemID.TerraBlade,.3f,"black");
            }
            File.WriteAllText(Path.Combine(output,"highlight-probe.json"),JsonSerializer.Serialize(new{
                passed=true,actualGpu=true,productionShaders=true,syntheticPose=true,artApproved=false,records,
                testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant()
            },new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{field.SetValue(null,brush);VfxIsolation.ApplyDefaultPreset();}
    }
}
