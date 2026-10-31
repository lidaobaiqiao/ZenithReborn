using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace ZenithReborn;

// 落星视觉效果集中在这里，粒子种类和蓝金配色来自原版自然落星。
internal static class HolyHeavenStarVisuals
{
    private const int StarDust=58,StarSpark=278;
    public static void Flight(Projectile star)
    {
        if(Main.dedServ)return;
        if(Main.rand.NextBool(20) || Main.tenthAnniversaryWorld && Main.rand.NextBool(15))
            Dust.NewDust(star.position,star.width,star.height,StarDust,
                star.velocity.X*.5f,star.velocity.Y*.5f,150,default,1.2f);
    }
    public static void Impact(Projectile star,Vector2 velocity)
    {
        if(Main.dedServ)return;
        SoundEngine.PlaySound(SoundID.Item10,star.position);
        Color blue=Main.tenthAnniversaryWorld?Color.HotPink:Color.CornflowerBlue;
        if(Main.tenthAnniversaryWorld)blue.A/=2;
        for(int i=0;i<7;i++)
            Dust.NewDust(star.position,star.width,star.height,StarDust,velocity.X*.1f,velocity.Y*.1f,150,default,.8f);
        for(int i=0;i<8;i++)
        {
            float angle=i*MathHelper.TwoPi/8+Main.rand.NextFloat()*.5f;
            Vector2 speed=Vector2.UnitY.RotatedBy(angle)*(4+Main.rand.NextFloat()*4);
            Dust.NewDustPerfect(star.Center,StarSpark,speed,150,blue).noGravity=true;
        }
        for(int i=0;i<4;i++)
        {
            float angle=i*MathHelper.TwoPi/4+Main.rand.NextFloat()*.5f;
            Vector2 speed=Vector2.UnitY.RotatedBy(angle)*(2+Main.rand.NextFloat()*3);
            Dust.NewDustPerfect(star.Center,StarSpark,speed,150,Color.Gold).noGravity=true;
        }
    }
}
