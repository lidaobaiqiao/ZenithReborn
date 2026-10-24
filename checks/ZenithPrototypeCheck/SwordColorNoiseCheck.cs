using Microsoft.Xna.Framework;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class SwordColorNoiseCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var a=SwordBodyColorNoise.CreatePixels(64);
        var b=SwordBodyColorNoise.CreatePixels(64);
        check(a.SequenceEqual(b),"自有底色噪声在相同参数下可重复生成");
        check(a.All(color=>color.A==255&&color.R==color.G&&color.G==color.B),
            "底色只提供不透明灰度，不覆盖逐剑颜色和形状透明度");
        check(a.Max(color=>color.R)-a.Min(color=>color.R)>60,"底色具有可见的明暗变化");
        float edgeError=0;
        for(int i=0;i<33;i++)
        {
            float t=i/32f;
            edgeError=Math.Max(edgeError,Math.Abs(SwordBodyColorNoise.Sample(0,t)-SwordBodyColorNoise.Sample(1,t)));
            edgeError=Math.Max(edgeError,Math.Abs(SwordBodyColorNoise.Sample(t,0)-SwordBodyColorNoise.Sample(t,1)));
        }
        check(edgeError<.00001f,"慢底色两轴循环接缝连续");
    }
}
