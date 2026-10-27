using Microsoft.Xna.Framework;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class SwordFlowPaletteCheck
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(var tint in new[]{new Color(45,220,120),new Color(70,110,240),new Color(225,65,150),Color.Gray})
        {
            var palette=SwordFlowPalette.From(tint);
            var colors=new[]{palette.Shadow,palette.Mid,palette.Highlight};
            check(colors.All(c=>float.IsFinite(c.X)&&float.IsFinite(c.Y)&&float.IsFinite(c.Z)&&
                c.X>=0&&c.Y>=0&&c.Z>=0&&c.X<=1&&c.Y<=1&&c.Z<=1),"剑色明暗锚点有限且在有效色域内");
            float Lum(Vector3 c)=>Vector3.Dot(c,new(.2126f,.7152f,.0722f));
            check(Lum(colors[0])<Lum(colors[1])&&Lum(colors[1])<Lum(colors[2]),"暗部、中间调和高光的感知亮度依次递增");
            if(tint!=Color.Gray)
                check(colors.All(c=>Math.Max(c.X,Math.Max(c.Y,c.Z))-Math.Min(c.X,Math.Min(c.Y,c.Z))>.1f),"有色剑的三个锚点均保留色彩而非叠白");
        }
        check(SwordFlowPalette.From(new Color(45,220,120))!=SwordFlowPalette.From(new Color(70,110,240)),"不同剑色生成各自的明暗色阶");
    }
}
