using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;
internal static class SparkCheck
{
    internal static void Run(Action<bool,string> check)
    {
        Lighting.Initialize();
        // ModDust is client-only and not registered on the headless server.
        var spark=new SwordSpark();
        Dust dust=new() {active=true,position=Vector2.Zero,velocity=Vector2.UnitX,color=Color.Cyan,scale=.85f};
        spark.OnSpawn(dust);
        check(dust.noGravity && !dust.noLight && dust.frame.Width==16 && dust.frame.Height==16,"Custom sword particle has visible sixteen-pixel sprite and emits light");
        Color bright=spark.GetAlpha(dust,Color.Black).Value;
        check(bright.G>150 && bright.A==0,"Particle remains emissive in darkness rather than inheriting scene darkness");
        spark.Update(dust);
        check(dust.position.X>0 && dust.velocity.X<1 && dust.scale<.85,"Glow particle drifts and fades independently of mesh ribbon");
        for(int i=0;i<40;i++)spark.Update(dust);
        check(!dust.active && spark.GetAlpha(dust,Color.White).Value.G<bright.G,"Particle expires with bounded lifetime and fading color");
    }
}
