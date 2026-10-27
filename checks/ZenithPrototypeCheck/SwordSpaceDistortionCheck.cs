using Microsoft.Xna.Framework;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class SwordSpaceDistortionCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var frame=SwordSpaceDistortion.At(10,new Vector2(100,200));
        check(frame.Strength>0&&frame.Strength<=24&&frame.Radius>=frame.Strength,"空间交互位移有界且小于影响半径");
        check(SwordSpaceDistortion.At(float.NaN,Vector2.Zero).Strength==0&&
            SwordSpaceDistortion.At(1,new(float.PositiveInfinity,0)).Strength==0,"无效时钟或相机输入不产生空间偏移");
        check(SwordSpaceDistortion.At(10,Vector2.Zero).Phase==frame.Phase&&frame.Origin==new Vector2(100,200),"相机原点与时间相位显式传递且不改强度");
        var scaled=SwordSpaceDistortion.At(10,Vector2.Zero,Matrix.CreateScale(2,2,1));
        check(scaled.PixelToWorld==new Vector4(.5f,0,0,.5f),"屏幕缩放通过逆变换还原世界相位坐标");
        check(SwordSpaceDistortion.At(10,Vector2.Zero,new Matrix()).Strength==0,"奇异观察矩阵安全关闭空间偏移");
        check(VfxIsolation.GlassParts.Count(p=>p.Uniform=="uGlassPartSpaceWarp")==1&&
            VfxIsolation.GlassParts.Single(p=>p.Uniform=="uGlassPartSpaceWarp").UsesExternalInput,"空间交互独立开关且受纯代码屏幕输入限制");
    }
}
