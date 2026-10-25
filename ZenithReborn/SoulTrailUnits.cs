using Terraria;

namespace ZenithReborn;

// 本体和三个原版剑影共用单元定义；采样位置不变，只限制各自拖尾的历史窗口。
public static class SoulTrailUnits
{
    public const int Count=4;
    public const int Spacing=15;
    public readonly record struct Unit(int Index,int Start,int Samples,float Opacity)
    {
        public SoulMaterialTrail.TrailLayer Layer=>new(Start,Samples,Opacity,true);
    }
    public static Unit At(int index,int available,float phase)
    {
        if(index<0||index>=Count)throw new ArgumentOutOfRangeException(nameof(index));
        int start=index*Spacing;
        // 分别占十二、十、八、六个更新间隔；其余间隔留白，旧剑影的尾巴逐个缩短。
        int samples=Math.Max(0,Math.Min(13-index*2,available-start));
        float opacity=(Count-index)/(float)Count*SoulBlade.SpriteVisibility(phase-start);
        return new(index,start,samples,opacity);
    }
}
