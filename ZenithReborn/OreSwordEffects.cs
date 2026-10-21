using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// Client-only drawing helpers. No extra projectiles, combat state or blend-state changes.
public static class OreSwordEffects
{
    private static Color Glow(Color tint,float strength)
    { var color=tint*Math.Clamp(strength,0,1);color.A=0;return color; }

    public static void Line(Vector2 from,Vector2 to,Color tint,float width,float strength)
    {
        Vector2 delta=to-from;float length=delta.Length();
        if(!float.IsFinite(length) || length<.1f || length>900)return;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,from-Main.screenPosition,new Rectangle(0,0,1,1),
            Glow(tint,strength),delta.ToRotation(),new Vector2(0,.5f),new Vector2(length,width),SpriteEffects.None,0);
    }

    public static void Halo(Vector2 at,Color tint,float radius,float strength)
    {
        var texture=ModContent.Request<Texture2D>("ZenithReborn/SwordSpark").Value;
        Main.spriteBatch.Draw(texture,at-Main.screenPosition,null,Glow(tint,strength),0,
            texture.Size()*.5f,radius/8,SpriteEffects.None,0);
    }

    public static void Star(Vector2 at,Color tint,float size,float strength,float rotation=0)
    {
        var texture=TextureAssets.Extra[ExtrasID.SharpTears].Value;
        var center=texture.Size()*.5f;var pos=at-Main.screenPosition;
        Main.spriteBatch.Draw(texture,pos,null,Glow(tint,strength),rotation,center,new Vector2(.13f,.75f)*size,SpriteEffects.None,0);
        Main.spriteBatch.Draw(texture,pos,null,Glow(tint,strength*.65f),rotation+MathHelper.PiOver2,center,new Vector2(.13f,.48f)*size,SpriteEffects.None,0);
        Main.spriteBatch.Draw(texture,pos,null,Glow(Color.White,strength*.65f),rotation,center,new Vector2(.055f,.42f)*size,SpriteEffects.None,0);
    }

    public static void Ring(Vector2 at,Color tint,float radius,float strength,float flatten=1)
    {
        const int segments=40;
        for(int i=0;i<segments;i++)
        {
            Vector2 a=(MathHelper.TwoPi*i/segments).ToRotationVector2()*radius;
            Vector2 b=(MathHelper.TwoPi*(i+1)/segments).ToRotationVector2()*radius;
            a.Y*=flatten;b.Y*=flatten;
            Line(at+a,at+b,tint,1.5f,strength);
        }
    }

    public static void Trail(SwordFlightTrail trail,Color tint,float width)
    {
        for(int i=trail.Count-1;i>=1;i--)
        {
            Vector2 a=trail.Point(i),b=trail.Point(i-1);
            if(Vector2.DistanceSquared(a,b)>96*96)continue;
            float strength=SwordFlightTrail.Strength(i-1,trail.Count);
            Line(a,b,tint,width*strength,.12f*strength);
            Line(a,b,tint,width*.38f*strength,.3f*strength);
            Line(a,b,Color.Lerp(tint,Color.White,.7f),Math.Max(.65f,width*.08f*strength),.48f*strength);
        }
    }

    public static void Blade(Texture2D texture,Vector2 at,float rotation,float scale,Color tint,float alpha,float pulse)
    {
        Vector2 normal=(rotation+MathHelper.PiOver2).ToRotationVector2();
        for(int side=-1;side<=1;side+=2)
            Main.spriteBatch.Draw(texture,at-Main.screenPosition+normal*side*1.4f,null,Glow(tint,.15f*alpha),
                rotation+MathHelper.PiOver4,texture.Size()*.5f,scale,SpriteEffects.None,0);
        Halo(at,tint,16*scale,.09f*alpha);
        Main.spriteBatch.Draw(texture,at-Main.screenPosition,null,Color.Lerp(tint,Color.White,.72f)*alpha,
            rotation+MathHelper.PiOver4,texture.Size()*.5f,scale,SpriteEffects.None,0);
        Vector2 tip=at+rotation.ToRotationVector2()*texture.Size().Length()*.32f*scale;
        Star(tip,tint,.42f*scale,.4f*alpha*pulse,rotation);
    }

    public static void Impact(Vector2 at,Vector2 direction,Color tint,bool giant)
    {
        if(Main.dedServ)return;
        Vector2 axis=direction.SafeNormalize(-Vector2.UnitY);
        int count=giant?18:5;
        for(int i=0;i<count;i++)
        {
            Vector2 velocity=axis.RotatedBy(Main.rand.NextFloat(-1.15f,1.15f))*Main.rand.NextFloat(1.5f,giant?7:3.5f);
            SwordSpark.Emit(at+Main.rand.NextVector2Circular(5,5),velocity,tint,giant?1.1f:.65f);
        }
    }
}
