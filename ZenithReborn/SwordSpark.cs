using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// An emissive sprite particle, distinct from the blade's mesh ribbon. The
// alpha-zero tint uses the game's normal premultiplied blend for gentle glow.
public class SwordSpark : ModDust
{
    public static void Emit(Vector2 at,Vector2 velocity,Color tint,float scale)
    {
        if(Main.dedServ) return;
        Dust.NewDustPerfect(at,ModContent.DustType<SwordSpark>(),velocity,0,tint,scale);
    }
    public override void OnSpawn(Dust dust)
    {
        dust.frame=new Rectangle(0,0,16,16);
        dust.noGravity=true;dust.noLight=false;dust.alpha=0;
    }
    public override Color? GetAlpha(Dust dust,Color lightColor)
    {
        Color color=Color.Lerp(dust.color,Color.White,.2f)*Math.Clamp(dust.scale/.85f,0,1);
        color.A=0;return color;
    }
    public override bool Update(Dust dust)
    {
        dust.position+=dust.velocity;dust.velocity*=.94f;
        dust.rotation+=.025f;dust.scale*=.92f;
        Lighting.AddLight(dust.position,dust.color.ToVector3()*(.18f*dust.scale));
        if(dust.scale<.14f) dust.active=false;
        return false;
    }
}
