using Microsoft.Xna.Framework;

namespace ZenithReborn;

// 剑流表面的明暗调色只在这里生成，绘制入口负责传参，着色器负责按受光位置连续插值。
public static class SwordFlowPalette
{
    public readonly record struct Colors(Vector3 Shadow,Vector3 Mid,Vector3 Highlight);

    public static Colors From(Color tint)
    {
        Vector3 rgb=tint.ToVector3();
        float high=Math.Max(rgb.X,Math.Max(rgb.Y,rgb.Z)),low=Math.Min(rgb.X,Math.Min(rgb.Y,rgb.Z));
        float chroma=high-low,saturation=high>0?chroma/high:0;
        // 无彩剑使用冷钢色阶，避免接近灰色时被不稳定的色相染成鲜艳颜色。
        if(saturation<.12f)
            return new(new(.10f,.13f,.18f),new(.42f,.48f,.56f),new(.80f,.88f,.96f));
        float hue=high==rgb.X?(rgb.Y-rgb.Z)/chroma:high==rgb.Y?2+(rgb.Z-rgb.X)/chroma:4+(rgb.X-rgb.Y)/chroma;
        hue/=6;hue-=MathF.Floor(hue);
        Vector3 shadow=Hsv(hue+.025f,Math.Clamp(saturation+.12f,.65f,.97f),.16f+high*.08f);
        Vector3 mid=Hsv(hue,Math.Clamp(saturation,.40f,.95f),Math.Clamp(high*.90f,.35f,.82f));
        Vector3 highlight=Hsv(hue-.025f,Math.Clamp(saturation*.72f,.35f,.82f),.96f);
        // 相邻色相的感知亮度不同；确保暗部、中间调、亮部的亮度顺序一致。
        mid*=Math.Min(1,.78f*Luminance(highlight)/Math.Max(.0001f,Luminance(mid)));
        shadow*=Math.Min(1,.42f*Luminance(mid)/Math.Max(.0001f,Luminance(shadow)));
        return new(shadow,mid,highlight);
    }

    private static float Luminance(Vector3 color)=>Vector3.Dot(color,new(.2126f,.7152f,.0722f));
    private static Vector3 Hsv(float hue,float saturation,float value)
    {
        float sector=(hue-MathF.Floor(hue))*6;
        int index=(int)sector;float fraction=sector-index;
        float low=value*(1-saturation),fall=value*(1-saturation*fraction),rise=value*(1-saturation*(1-fraction));
        return index switch
        {
            0=>new(value,rise,low),1=>new(fall,value,low),2=>new(low,value,rise),
            3=>new(low,fall,value),4=>new(rise,low,value),_=>new(value,low,fall)
        };
    }
}
