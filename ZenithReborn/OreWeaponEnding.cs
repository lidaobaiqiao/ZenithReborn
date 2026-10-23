using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace ZenithReborn;

public readonly record struct OreAirFront(Vector2 Tip,Vector2 Axis,Color Tint,float Size);
public readonly record struct OreWeaponEnd(Vector2 BladeAt,Vector2 Anchor,Vector2 Axis,Color Tint,
    int VisualType,float Scale,bool Giant,int Age=0)
{
    public int Lifetime=>Giant?36:20;
}
public sealed class OreWeaponEndHistory
{
    public const int Capacity=64;
    private readonly List<OreWeaponEnd> ends=new(Capacity);
    public IReadOnlyList<OreWeaponEnd> Ends=>ends;
    public bool Emit(OreWeaponEnd end)
    {
        if(!Finite(end.BladeAt)||!Finite(end.Anchor)||!Finite(end.Axis)||!float.IsFinite(end.Scale)||end.Scale<=0)return false;
        if(ends.Count==Capacity)ends.RemoveAt(0);
        ends.Add(end with {Axis=end.Axis.SafeNormalize(Vector2.UnitX),Age=0});return true;
    }
    private static bool Finite(Vector2 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y);
    public void Update()
    {
        for(int i=ends.Count-1;i>=0;i--)
        {
            var next=ends[i] with {Age=ends[i].Age+1};
            if(next.Age>=next.Lifetime)ends.RemoveAt(i);else ends[i]=next;
        }
    }
    public void Clear()=>ends.Clear();
}

public static class OreWeaponEnding
{
    public static IEnumerable<FlowSparkSpawn> Sparks(OreWeaponEnd end)
    {
        Vector2 normal=end.Axis.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
        int count=end.Giant?10:4;
        for(int i=0;i<count;i++)
        {
            float f=i/(float)(count-1),side=i%2==0?1:-1;bool mote=i%3==0;
            Vector2 at=end.Giant?end.Anchor:end.BladeAt+end.Axis*(f-.5f)*46;
            yield return new(at,normal*side*(2+f*5)+end.Axis*(f-.5f)*5,end.Tint,
                end.Giant?1.1f:.7f,mote?36:25,mote);
        }
    }
    public static void DrawEnd(OreWeaponEnd end,float clock,int? width=null,int? height=null,bool unscaled=false)
    {
        if(end.Age<0||end.Age>=end.Lifetime)return;
        float t=end.Age/(float)end.Lifetime,fade=MathF.Pow(1-t,1.2f);
        float length=end.Giant?280:100,halfWidth=end.Giant?65:22;
        MartialCrescentMaterial.OreFlame(end.Anchor-end.Axis*length*.75f,end.Anchor+end.Axis*length*.25f,
            halfWidth,end.Tint,fade*.78f,t,clock,width,height,unscaled);
    }
    public static void DrawEndGhost(OreWeaponEnd end)
    {
        if(end.Age<0||end.Age>=end.Lifetime)return;
        float fade=MathF.Pow(1-end.Age/(float)end.Lifetime,2);
        Texture2D texture;
        if(end.VisualType>0)
        {Main.instance.LoadItem(end.VisualType);texture=TextureAssets.Item[end.VisualType].Value;}
        else texture=TextureAssets.Projectile[-end.VisualType].Value;
        texture=OreSwordEffects.SpectralTexture(texture);
        Main.spriteBatch.Draw(texture,end.BladeAt-Main.screenPosition,null,end.Tint*(fade*.26f),
            end.Axis.ToRotation()+(end.VisualType>0?MathHelper.PiOver4:MathHelper.PiOver2),texture.Size()*.5f,
            end.Scale,SpriteEffects.None,0);
    }
}
