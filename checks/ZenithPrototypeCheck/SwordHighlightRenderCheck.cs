using Microsoft.Xna.Framework;
using Terraria.ID;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 关掉真实环境、内部色流和贴图自发光，单独检查面高光与亮刃是否保留逐剑色彩。
internal static class SwordHighlightRenderCheck
{
    internal sealed record Sample(int Item,int VisiblePixels,int ColoredPixels,float MeanSaturation);
    internal sealed record Result(bool Passed,Sample[] Samples);
    internal static Result Run(Func<string,int,int,Color[]> render)
    {
        var saved=VfxIsolation.PartValues.ToArray();var samples=new List<Sample>();
        try
        {
            VfxIsolation.PartValues[22]=0;VfxIsolation.PartValues[23]=0;VfxIsolation.PartValues[24]=0;
            foreach(int item in new[]{ItemID.TerraBlade,ItemID.EnchantedSword,ItemID.Starfury})
            {
                render("highlight-display-"+item,item,0);
                var pixels=render("highlight-surface-data-"+item,item,2);
                int visible=0,colored=0;float sum=0;
                foreach(var c in pixels)
                {
                    float high=Math.Max(c.R,Math.Max(c.G,c.B)),low=Math.Min(c.R,Math.Min(c.G,c.B));
                    if(high<12)continue;
                    float saturation=(high-low)/high;visible++;sum+=saturation;if(saturation>.25f)colored++;
                }
                samples.Add(new(item,visible,colored,visible>0?sum/visible:0));
            }
            return new(samples.All(s=>s.VisiblePixels>100&&s.ColoredPixels>s.VisiblePixels*.8f&&s.MeanSaturation>.35f),samples.ToArray());
        }
        finally{Array.Copy(saved,VfxIsolation.PartValues,saved.Length);}
    }
}
