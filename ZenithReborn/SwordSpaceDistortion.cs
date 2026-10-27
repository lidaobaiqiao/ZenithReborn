using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ZenithReborn;

// 刀光周围空间交互的参数入口；材质配色、形状和背景采集继续由各自模块负责。
public static class SwordSpaceDistortion
{
    public readonly record struct Frame(float Strength,float Radius,float Phase,Vector2 Origin,Vector4 PixelToWorld);

    public static Frame At(float clock,Vector2 camera,Matrix? view=null)
    {
        var inverse=Matrix.Invert(view??Matrix.Identity);
        var mapping=new Vector4(inverse.M11,inverse.M21,inverse.M12,inverse.M22);
        var origin=camera+new Vector2(inverse.M41,inverse.M42);
        if(!float.IsFinite(clock)||!float.IsFinite(camera.X)||!float.IsFinite(camera.Y))
            return new(0,GlassMaterialPreset.SpaceWarpRadius,0,Vector2.Zero,new(1,0,0,1));
        if(!float.IsFinite(mapping.X)||!float.IsFinite(mapping.Y)||!float.IsFinite(mapping.Z)||!float.IsFinite(mapping.W)||
            !float.IsFinite(origin.X)||!float.IsFinite(origin.Y))
            return new(0,GlassMaterialPreset.SpaceWarpRadius,0,Vector2.Zero,new(1,0,0,1));
        return new(GlassMaterialPreset.SpaceWarpStrength,GlassMaterialPreset.SpaceWarpRadius,
            clock*GlassMaterialPreset.SpaceWarpSpeed,origin,mapping);
    }

    public static void Apply(EffectParameterCollection parameters,float clock,Vector2 camera,Matrix? view=null)
    {
        var frame=At(clock,camera,view);
        parameters["uSpaceWarpStrength"]?.SetValue(frame.Strength);
        parameters["uSpaceWarpRadius"]?.SetValue(frame.Radius);
        parameters["uSpaceWarpPhase"]?.SetValue(frame.Phase);
        parameters["uSpaceWarpOrigin"]?.SetValue(frame.Origin);
        parameters["uSpaceWarpPixelToWorld"]?.SetValue(frame.PixelToWorld);
    }
}
