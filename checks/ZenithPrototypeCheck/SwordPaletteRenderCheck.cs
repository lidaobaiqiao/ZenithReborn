using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 同一网格、时间、光向分别换实际剑体身份，读取生产输出色，避免只比较有无像素差异。
internal static class SwordPaletteRenderCheck
{
    internal static bool Run(string output)
    {
        var device=Main.graphics.GraphicsDevice;var saved=device.GetRenderTargets();var viewport=device.Viewport;
        string folder=Path.Combine(output,"sword-palette");Directory.CreateDirectory(folder);
        int[] items=[ItemID.TerraBlade,ItemID.EnchantedSword,ItemID.Starfury,ItemID.FieryGreatsword,ItemID.IceBlade,ItemID.Meowmere,ItemID.SilverBroadsword];
        var results=new List<object>();var hues=new List<float>();bool passed=true;
        using var target=new RenderTarget2D(device,640,300,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        VertexPositionColorTexture[] mesh=[
            new(new(50,45,0),Color.White,new(0,0)),new(new(590,45,0),Color.White,new(1,0)),new(new(50,255,0),Color.White,new(0,1)),
            new(new(590,45,0),Color.White,new(1,0)),new(new(590,255,0),Color.White,new(1,1)),new(new(50,255,0),Color.White,new(0,1))];
        try
        {
            SoulSceneRefraction.EndCapture();
            foreach(int item in items)
            {
                Color source=SwordSpritePalette.Resolve(item,SoulVisuals.Tint(item));
                device.SetRenderTarget(target);device.Clear(Color.Transparent);
                MartialSlashRenderer.DrawWithRestoredState(()=>SwordFlowMaterial.Draw(mesh,6,SoulVisuals.Tint(item),1,.3f,640,300,true,
                    itemType:item,lighting:Vector3.Normalize(new(.6f,-.4f,.65f))));
                var pixels=new Color[640*300];target.GetData(pixels);
                var body=pixels.Where(p=>p.A>160).ToArray();
                Vector3 mean=new((float)body.Average(p=>p.R),(float)body.Average(p=>p.G),(float)body.Average(p=>p.B));
                Color shown=new((int)mean.X,(int)mean.Y,(int)mean.Z);
                Vector3 sourceHsl=Main.rgbToHsl(source),shownHsl=Main.rgbToHsl(shown);
                float distance=Math.Abs(sourceHsl.X-shownHsl.X);distance=Math.Min(distance,1-distance);
                bool match=sourceHsl.Y<.15f||distance<.09f;
                passed&=match&&shownHsl.Z>.22f;
                if(hues.Count<3)hues.Add(shownHsl.X);
                using(var file=File.Create(Path.Combine(folder,$"material-{item}.png")))target.SaveAsPng(file,640,300);
                var texture=TextureAssets.Item[item].Value;
                using(var file=File.Create(Path.Combine(folder,$"sprite-{item}.png")))texture.SaveAsPng(file,texture.Width,texture.Height);
                results.Add(new{item,name=ItemID.Search.GetName(item),sourceColor=new[]{(int)source.R,(int)source.G,(int)source.B},
                    outputMean=new[]{mean.X,mean.Y,mean.Z},sourceHue=sourceHsl.X,outputHue=shownHsl.X,hueError=distance,matched=match});
            }
            for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)
            {float d=Math.Abs(hues[i]-hues[j]);passed&=Math.Min(d,1-d)>.10f;}
            File.WriteAllText(Path.Combine(output,"sword-palette.json"),JsonSerializer.Serialize(new{passed,actualGpu=true,
                actualItemTextureInputs=true,items=results},new JsonSerializerOptions{WriteIndented=true}));
            return passed;
        }
        finally{device.SetRenderTargets(saved);device.Viewport=viewport;}
    }
}
