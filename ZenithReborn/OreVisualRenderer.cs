using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// One masked material connects wake, blade and leading tip.
[Autoload(Side=ModSide.Client)]
public class OreVisualRenderer : ModSystem
{
    public static readonly Color ForgeGold=new(255,213,125);
    public override void Unload()=>OreSwordEffects.Unload();
    public static void Flight(SwordFlightTrail trail,Color tint,float halfWidth,float clock,
        int? width=null,int? height=null,bool unscaled=false,float tailOffset=0,float tipReach=0,float heat=0,Vector2? head=null)
    {
        if(trail.Count<2)return;
        Vector2 axis=(trail.Point(0)-trail.Point(1)).SafeNormalize(Vector2.UnitX);
        Vector2 from=trail.Point(trail.Count-1)-axis*tailOffset*.35f;
        Vector2 to=(head??trail.Point(0))+axis*(tipReach+halfWidth*.45f);
        MartialCrescentMaterial.OreFlame(from,to,halfWidth*1.5f,tint,.78f+heat*.12f,0,clock,width,height,unscaled);
    }
    public static void Sheath(Vector2 at,float angle,float reach,float scale,Color tint,float opacity,float clock,
        int? width=null,int? height=null,bool unscaled=false)
    {
        Vector2 axis=angle.ToRotationVector2();
        MartialCrescentMaterial.OreFlame(at-axis*reach*.85f,at+axis*(reach+scale*3),9*scale,tint,
            opacity*.72f,0,clock,width,height,unscaled);
    }
    public static void Shard(Vector2 at,float rotation,HardOreKind kind,float age,float clock,
        int? width=null,int? height=null,bool unscaled=false)
    {
        Color tint=OreFamily.Tints[8+Math.Clamp((int)kind,0,5)];Vector2 axis=rotation.ToRotationVector2();
        MartialCrescentMaterial.OreFlame(at-axis*25,at+axis*16,10,tint,.72f,0,clock,width,height,unscaled);
    }
}
