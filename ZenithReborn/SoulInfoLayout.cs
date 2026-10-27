using Microsoft.Xna.Framework;

namespace ZenithReborn;

// 信息页共用的自适应排版；只接收界面单位，不重复换算游戏的界面缩放。
public static class SoulInfoLayout
{
    public const int Gap=14, TileHeight=130;
    public static int Columns(int width)=>width>=1120?3:width>=620?2:1;
    public static Rectangle Tile(Rectangle area,int ordinal,int columns,int height=TileHeight)
    {
        columns=Math.Max(1,columns);int width=Math.Max(1,(area.Width-Gap*(columns-1))/columns);
        return new(area.X+ordinal%columns*(width+Gap),area.Y+ordinal/columns*(height+Gap),width,height);
    }
    public static float Ease(float value){value=Math.Clamp(value,0,1);return value*value*(3-2*value);}
    public static float Approach(float current,float target,float seconds)=>
        current+(target-current)*(1-MathF.Exp(-Math.Clamp(seconds,0,.08f)*13));
    // 查找对象与阅读详情分别拥有固定区域，切换选择不再挤动整页内容。
    public static (Rectangle List,Rectangle Detail) MasterDetail(Rectangle area)
    {
        if(area.Width>=850)
        {
            int left=Math.Clamp((int)(area.Width*.38f),290,470);
            return(new(area.X,area.Y,left,area.Height),new(area.X+left+Gap,area.Y,Math.Max(1,area.Width-left-Gap),area.Height));
        }
        int top=Math.Clamp((int)(area.Height*.36f),80,218);
        top=Math.Min(top,Math.Max(1,area.Height/2));
        return(new(area.X,area.Y,area.Width,top),new(area.X,area.Y+top+Gap,area.Width,Math.Max(1,area.Height-top-Gap)));
    }
}
