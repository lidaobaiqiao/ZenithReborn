using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using ZenithReborn;

namespace ZenithMaterialLab;

// 功能完成后才逐项画A/B对照；这些是实际GPU结果，不把编译或参数读数当画面证据。
internal static class LabChecks
{
    internal static SurfaceMaterialDefinition Preset(string name)
    {
        var m=new SurfaceMaterialDefinition{UseWeaponColor=false};m.Assets.EnvironmentMap="environment.png";
        if(name=="metal"){m.Metallic=1;m.Roughness=.22f;m.Transmission=0;m.BaseColor=new(.85f,.62f,.28f);}
        if(name=="coat"){m.Transmission=0;m.Roughness=.45f;m.ClearCoat=1;m.BaseColor=new(.12f,.36f,.65f);}
        if(name=="film"){m.Metallic=.35f;m.Transmission=.15f;m.FilmWeight=1;m.FilmThickness=420;}
        if(name=="emission"){m.Transmission=.15f;m.EmissionStrength=1.7f;m.WorldLightStrength=.8f;m.Assets.EmissionMap="emission.png";}
        if(name=="scatter"){m.Subsurface=.8f;m.Transmission=.2f;m.VolumeDensity=.3f;m.VolumeColor=new(.8f,.4f,.2f);}
        if(name=="regions")
        {
            m.Metallic=.6f;m.Roughness=.15f;m.Transmission=.2f;m.Assets.LayerMask="regions.zrdata";
            m.DetailLayers=[new(){Weight=.5f,Blend=1,RoughnessDelta=.7f,Tint=new(.9f,.95f,1)},
                new(){Weight=.45f,Tint=new(.35f,.1f,.7f),EmissionStrength=.3f,ScrollU=.08f},
                new(){Weight=.5f,Blend=1,Tint=new(.3f,.5f,.65f),MetallicDelta=-.6f},
                new(){Weight=.3f,Tint=new(.1f,.5f,.65f),EmissionStrength=.7f}];
        }
        return m;
    }
    private sealed record Case(string Name,Action<SurfaceMaterialDefinition> A,Action<SurfaceMaterialDefinition> B,
        string Map=null,int Shape=1,Action<SurfaceMaterialDefinition> Prepare=null);
    internal static void Run(string output,string root,LabResources resources,LabPreview preview)
    {
        Directory.CreateDirectory(output);var results=new List<object>();bool all=true;
        var cases=new Case[]{
            new("base-color",m=>m.BaseColor=new(.1f,.2f,.8f),m=>m.BaseColor=new(.8f,.2f,.1f)),
            new("metallic",m=>m.Metallic=0,m=>m.Metallic=1),
            new("roughness",m=>m.Roughness=.04f,m=>m.Roughness=.85f),
            new("ior",m=>m.Ior=1,m=>m.Ior=2,Prepare:m=>{m.Transmission=1;m.EnvironmentStrength=0;m.LightStrength=0;m.AmbientColor=Vector3.Zero;}),
            new("opacity",m=>m.Opacity=0,m=>m.Opacity=1),
            new("transmission",m=>m.Transmission=0,m=>m.Transmission=1),
            new("thickness",m=>m.Thickness=0,m=>m.Thickness=100,Prepare:m=>m.Transmission=.9f),
            new("absorption",m=>m.AbsorptionDensity=0,m=>m.AbsorptionDensity=3,Prepare:m=>{m.Transmission=1;m.AbsorptionColor=new(.2f,.7f,.9f);}),
            new("normal-map",m=>m.NormalStrength=0,m=>m.NormalStrength=2,"NormalMap"),
            new("height-bump",m=>m.BumpStrength=0,m=>m.BumpStrength=2,"HeightMap"),
            new("geometry-displacement",m=>m.Displacement=0,m=>m.Displacement=28,"HeightMap",0),
            new("specular",m=>m.Specular=0,m=>m.Specular=2),
            new("anisotropy",m=>m.Anisotropy=-.85f,m=>m.Anisotropy=.85f,Prepare:m=>{m.Metallic=1;m.Roughness=.45f;m.LightStrength=4;m.EnvironmentStrength=.2f;}),
            new("anisotropy-rotation",m=>m.AnisotropyRotation=0,m=>m.AnisotropyRotation=90,Prepare:m=>{m.Anisotropy=.8f;m.Metallic=1;m.Roughness=.45f;m.LightStrength=4;m.EnvironmentStrength=.2f;}),
            new("environment-reflection",m=>m.EnvironmentStrength=0,m=>m.EnvironmentStrength=2),
            new("environment-texture",m=>m.Assets.EnvironmentMap="",m=>m.Assets.EnvironmentMap="environment.png"),
            new("light-strength",m=>m.LightStrength=0,m=>m.LightStrength=4),
            new("light-direction",m=>m.LightAzimuth=-100,m=>m.LightAzimuth=100),
            new("light-elevation",m=>m.LightElevation=10,m=>m.LightElevation=80),
            new("light-color",m=>m.LightColor=new(1,.1f,.1f),m=>m.LightColor=new(.1f,.1f,1)),
            new("clear-coat",m=>m.ClearCoat=0,m=>m.ClearCoat=1),
            new("coat-roughness",m=>m.CoatRoughness=.03f,m=>m.CoatRoughness=.8f,Prepare:m=>m.ClearCoat=1),
            new("coat-ior",m=>m.CoatIor=1,m=>m.CoatIor=2.4f,Prepare:m=>m.ClearCoat=1),
            new("sheen",m=>m.Sheen=0,m=>m.Sheen=1),
            new("sheen-roughness",m=>m.SheenRoughness=.03f,m=>m.SheenRoughness=.9f,Prepare:m=>m.Sheen=1),
            new("thin-film",m=>m.FilmWeight=0,m=>m.FilmWeight=1),
            new("film-thickness",m=>m.FilmThickness=150,m=>m.FilmThickness=850,Prepare:m=>m.FilmWeight=1),
            new("film-ior",m=>m.FilmIor=1,m=>m.FilmIor=2.4f,Prepare:m=>m.FilmWeight=1),
            new("emission",m=>m.EmissionStrength=0,m=>m.EmissionStrength=2),
            new("emission-texture",m=>m.Assets.EmissionMap="",m=>m.Assets.EmissionMap="emission.png",Prepare:m=>m.EmissionStrength=2),
            new("subsurface-approximation",m=>m.Subsurface=0,m=>m.Subsurface=1),
            new("subsurface-radius",m=>m.SubsurfaceRadius=.05f,m=>m.SubsurfaceRadius=2,Prepare:m=>m.Subsurface=1),
            new("thin-volume-density",m=>m.VolumeDensity=0,m=>m.VolumeDensity=2),
            new("volume-anisotropy",m=>m.VolumeAnisotropy=-.75f,m=>m.VolumeAnisotropy=.75f,Prepare:m=>m.VolumeDensity=1),
            new("volume-emission",m=>m.VolumeEmission=0,m=>m.VolumeEmission=3,Prepare:m=>m.VolumeDensity=1),
            new("color-texture",m=>m.Assets.ColorField="",m=>m.Assets.ColorField="color.png"),
            new("motion-mask",m=>m.Assets.MotionMask="",m=>m.Assets.MotionMask="mask.png"),
            new("surface-texture",m=>m.Assets.SurfaceMap="",m=>m.Assets.SurfaceMap="surface.png",Prepare:m=>m.Metallic=.8f),
            new("ribbon-curvature",m=>m.CrossCurvature=0,m=>m.CrossCurvature=3,Shape:2),
            new("ribbon-tilt",m=>m.AlongTilt=0,m=>m.AlongTilt=1.5f,Shape:2),
            new("detail-roughness",m=>m.DetailLayers[0].RoughnessDelta=0,m=>m.DetailLayers[0].RoughnessDelta=.8f,
                Prepare:m=>m.DetailLayers=[new(){Weight=1,Blend=1}]),
            new("detail-metallic",m=>m.DetailLayers[0].MetallicDelta=0,m=>m.DetailLayers[0].MetallicDelta=1,
                Prepare:m=>m.DetailLayers=[new(){Weight=1,Blend=1}]),
            new("detail-emission",m=>m.DetailLayers[0].EmissionStrength=0,m=>m.DetailLayers[0].EmissionStrength=3,
                Prepare:m=>m.DetailLayers=[new(){Weight=1,Tint=new(.1f,.4f,.8f)}]),
            new("detail-brush",m=>m.Assets.Brush="",m=>m.Assets.Brush="color.png",
                Prepare:m=>m.DetailLayers=[new(){Weight=.8f,Tint=new(.3f,.6f,.9f)}]),
            new("detail-noise",m=>m.Assets.Noise="",m=>m.Assets.Noise="color.png",
                Prepare:m=>m.DetailLayers=[new(){Weight=.8f,ScaleU=28,ScaleV=19,Tint=new(.3f,.6f,.9f)}])
        };
        foreach(var test in cases)
        {
            Color[][] pixels=new Color[2][];
            for(int side=0;side<2;side++)
            {
                var m=Preset("glass");m.BaseColor=new(.65f,.8f,.95f);m.Roughness=.28f;m.Transmission=.25f;m.LightStrength=2;
                if(test.Map!=null)m.Assets.GetType().GetProperty(test.Map).SetValue(m.Assets,resources.FileNames[Array.IndexOf(resources.MapNames,test.Map)]);
                test.Prepare?.Invoke(m);(side==0?test.A:test.B)(m);m.Validate();
                resources.Reload(m,1);pixels[side]=preview.Render(m,resources.Inputs,resources.Background,test.Shape,.25f,true);
                preview.Save(Path.Combine(output,test.Name+(side==0?"-a.png":"-b.png")));
            }
            long difference=0;int changed=0;
            for(int i=0;i<pixels[0].Length;i++)
            {
                int delta=Math.Abs(pixels[0][i].R-pixels[1][i].R)+Math.Abs(pixels[0][i].G-pixels[1][i].G)+Math.Abs(pixels[0][i].B-pixels[1][i].B);
                difference+=delta;if(delta>3)changed++;
            }
            bool passed=difference>10000&&changed>200;all&=passed;results.Add(new{test.Name,passed,difference,changed});
        }
        var roundTrip=Preset("film");roundTrip.Assets.NormalMap="normal.png";
        bool serialized=roundTrip.ToJson()==SurfaceMaterialDefinition.FromJson(roundTrip.ToJson()).ToJson();all&=serialized;
        bool invalidRejected=false;try{roundTrip.Set("Ior",float.NaN);}catch(ArgumentException){invalidRejected=true;}all&=invalidRejected;
        resources.Reload(roundTrip,1);string exported=resources.Export(root,roundTrip);
        var imported=SurfaceMaterialDefinition.FromJson(File.ReadAllText(Path.Combine(exported,"material.json")));
        bool exportRoundTrip=imported.ToJson()==roundTrip.ToJson();all&=exportRoundTrip;
        var pipeline=LabPipelineCheck.Run(output,resources,preview);all&=pipeline.Passed;
        var regions=LabRegionCheck.Run(output,resources,preview);all&=regions.Passed;
        var regionMaterial=Preset("regions");resources.Reload(regionMaterial,1);string regionExport=resources.Export(root,regionMaterial);
        File.WriteAllText(Path.Combine(output,"controls.json"),JsonSerializer.Serialize(SurfaceMaterialDefinition.Controls,SurfaceMaterialDefinition.JsonOptions));
        File.WriteAllText(Path.Combine(output,"sample.material.json"),Preset("glass").ToJson());
        File.WriteAllText(Path.Combine(output,"gpu.json"),JsonSerializer.Serialize(new{
            passed=all,actualGpu=true,productionRenderer=SurfaceMaterialRenderer.EffectPath,
            testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))).ToLowerInvariant(),
            labPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithMaterialLab.tmod")))).ToLowerInvariant(),
            cases=results,serialized,invalidRejected,exportRoundTrip,exported,regionExport,pipeline,regions,
            worldCombatTested=false,manualWorkbenchInputTested=false,
            limitations="次表面为包裹光近似，体积为解析薄层，薄膜为三波段近似；非完整BSDF／光线追踪。"
        },SurfaceMaterialDefinition.JsonOptions));
    }
}
