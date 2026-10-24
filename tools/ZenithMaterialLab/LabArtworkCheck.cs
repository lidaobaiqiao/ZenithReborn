using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using ZenithReborn;

namespace ZenithMaterialLab;

// 固定剑迹下对照用户素材，生成实际GPU动画；不冒充世界战斗或美术验收。
internal static class LabArtworkCheck
{
    internal static void Run(string output,string root,LabResources resources,LabPreview preview,SurfaceMaterialDefinition material)
    {
        Directory.CreateDirectory(output);var tint=SoulVisuals.Tint(ItemID.TerraBlade);
        var baseline=LabChecks.Preset("glass");baseline.UseWeaponColor=true;
        resources.Reload(baseline,1);var before=preview.Render(baseline,resources.Inputs,resources.Background,2,.25f,true,weaponColor:tint);
        preview.Save(Path.Combine(output,"before.png"));
        resources.Reload(material,1);var after=preview.Render(material,resources.Inputs,resources.Background,2,.25f,true,weaponColor:tint);
        preview.Save(Path.Combine(output,"after.png"));
        long difference=0;int changed=0;
        for(int i=0;i<after.Length;i++)
        {int d=Math.Abs(after[i].R-before[i].R)+Math.Abs(after[i].G-before[i].G)+Math.Abs(after[i].B-before[i].B);difference+=d;if(d>3)changed++;}
        var normal0=preview.Render(material,resources.Inputs,resources.Background,2,0,true,1);
        // 用不变形的平面检查表面纹理本身的运动，排除轮廓呼吸造成的假阳性。
        var color0=preview.Render(material,resources.Inputs,resources.Background,0,0,true,weaponColor:tint);
        var color1=preview.Render(material,resources.Inputs,resources.Background,0,2,true,weaponColor:tint);
        bool animated=!color0.SequenceEqual(color1);
        // 固定时间，仅切换纹理输入，检查表面图没有制造第二套轮廓。
        resources.Reload(baseline,1);var baseNormal=preview.Render(baseline,resources.Inputs,resources.Background,2,0,true,1);
        bool sameCoverage=normal0.Zip(baseNormal).All(pair=>pair.First.A==pair.Second.A);
        resources.Reload(material,1);
        var ablations=new List<object>();bool inputsEffective=true;
        foreach(string slot in new[]{"NormalMap","SurfaceMap","Brush","Noise","LayerMask"})
        {
            var disabled=material.Copy();disabled.Assets.GetType().GetProperty(slot).SetValue(disabled.Assets,"");
            resources.Reload(disabled,1);var pixels=preview.Render(disabled,resources.Inputs,resources.Background,2,.25f,true,weaponColor:tint);
            long delta=0;int count=0;
            for(int i=0;i<pixels.Length;i++)
            {int d=Math.Abs(pixels[i].R-after[i].R)+Math.Abs(pixels[i].G-after[i].G)+Math.Abs(pixels[i].B-after[i].B);delta+=d;if(d>3)count++;}
            inputsEffective&=count>10;ablations.Add(new{slot,changed=count,difference=delta});
            preview.Save(Path.Combine(output,"without-"+slot+".png"));
        }
        resources.Reload(material,1);
        preview.Render(material,resources.Inputs,resources.Background,0,.25f,weaponColor:tint);preview.Save(Path.Combine(output,"plane.png"));
        preview.Render(material,resources.Inputs,resources.Background,1,.25f,weaponColor:tint);preview.Save(Path.Combine(output,"sphere.png"));
        var paths=new List<string>();
        for(int frame=0;frame<48;frame++)
        {
            preview.Render(material,resources.Inputs,resources.Background,2,frame/24f,weaponColor:tint);
            string path=Path.Combine(output,"frames",$"frame-{frame:000}.png");preview.Save(path);paths.Add(path);
        }
        foreach(int weapon in new[]{ItemID.TerraBlade,ItemID.EnchantedSword,ItemID.Starfury})
        {preview.Render(material,resources.Inputs,resources.Background,2,.25f,weaponColor:SoulVisuals.Tint(weapon));preview.Save(Path.Combine(output,$"palette-{weapon}.png"));}
        string export=resources.Export(root,material);
        File.WriteAllText(Path.Combine(output,"artwork.json"),JsonSerializer.Serialize(new{
            passed=changed>1000&&animated&&sameCoverage&&inputsEffective,actualGpu=true,syntheticFixedTrajectory=true,
            testedPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithReborn.tmod")))) .ToLowerInvariant(),
            testedLabPackageSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Main.SavePath,"Mods/ZenithMaterialLab.tmod")))) .ToLowerInvariant(),
            changed,difference,animated,animationControl="fixed-plane",sameCoverage,inputsEffective,ablations,frames=paths.Count,export,
            worldCombatTested=false,artApproved=false,productionBindingsChanged=false
        },SurfaceMaterialDefinition.JsonOptions));
    }
}
