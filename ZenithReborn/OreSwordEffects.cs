using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 仅客户端使用的绘制辅助函数。不产生额外弹幕、不改战斗状态、不改混合状态。
public static class OreSwordEffects
{
    public static readonly Color ForgeGold=new(255,213,125);
    private static readonly Dictionary<Texture2D,Texture2D> spectralTextures=new();
    private static readonly Dictionary<Texture2D,float> bladeReach=new();
    public static float BladeReach(Texture2D texture,float scale)
    {SpectralTexture(texture);return bladeReach[texture]*scale;}
    // 保留原版轮廓、透明度和明暗层次；先把原贴图自带的杂色去掉，
    // 再用这把剑自己的配色重新照亮。
    public static Texture2D SpectralTexture(Texture2D source)
    {
        if(spectralTextures.TryGetValue(source,out var cached))return cached;
        var pixels=new Color[source.Width*source.Height];source.GetData(pixels);
        float reach=1;
        for(int i=0;i<pixels.Length;i++)
        {
            Color c=pixels[i];
            if(c.A>32)
                reach=Math.Max(reach,((i%source.Width+1-source.Width*.5f)-(i/source.Width-source.Height*.5f))*.70710678f);
            float light=c.A==0?0:Math.Clamp((c.R*.2126f+c.G*.7152f+c.B*.0722f)/c.A,0,1);
            byte value=(byte)Math.Clamp((.08f+.92f*MathF.Sqrt(light))*c.A,0,255);
            pixels[i]=new(value,value,value,c.A);
        }
        var copy=new Texture2D(Main.graphics.GraphicsDevice,source.Width,source.Height);copy.SetData(pixels);
        spectralTextures[source]=copy;spectralTextures[copy]=copy;
        bladeReach[source]=bladeReach[copy]=reach;return copy;
    }
    public static void Unload()
    {
        var owned=spectralTextures.Values.Distinct().ToArray();spectralTextures.Clear();bladeReach.Clear();
        Main.QueueMainThreadAction(()=>{foreach(var texture in owned)texture.Dispose();});
    }
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
        // 自有光斑图片也受纯代码排查限制；不拿别的纹理替代，以免掩盖来源。
        if(VfxIsolation.PureCodeOnly)return;
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
            // 只画两层而不是三层：一条主体加一条细亮芯。
            // 之前那层宽光晕正是把拖尾糊成一片的原因。
            Line(a,b,tint,width*.34f*strength,.22f*strength);
            Line(a,b,Color.Lerp(tint,Color.White,.55f),Math.Max(.5f,width*.06f*strength),.3f*strength);
        }
    }

    public static void Blade(Texture2D texture,Vector2 at,float rotation,float scale,Color tint,float alpha,float pulse)
    {
        // 剑身保留自己的原版像素、完全不透明。剑才是主角，特效只在它背后；
        // 不重新染色，也不再盖一层60%透明度的灰。
        Main.spriteBatch.Draw(texture,at-Main.screenPosition,null,Color.White*alpha,
            rotation+MathHelper.PiOver4,texture.Size()*.5f,scale,SpriteEffects.None,0);
    }

    public static void Impact(Vector2 at,Vector2 direction,Color tint,bool giant)
    {
        if(Main.dedServ)return;
        Vector2 axis=direction.SafeNormalize(-Vector2.UnitY);
        int count=giant?9:3;
        for(int i=0;i<count;i++)
        {
            Vector2 velocity=axis.RotatedBy(Main.rand.NextFloat(-.95f,.95f))*Main.rand.NextFloat(1.2f,giant?5.2f:2.6f);
            Ember(at+Main.rand.NextVector2Circular(4,4),velocity*(giant?1.6f:1.3f),tint,giant?1.1f:.7f,i%3==0);
        }
    }
    public static void FlyingBlade(Texture2D texture,Vector2 at,float rotation,float scale,Color tint,
        float alpha,float pulse,int launchAge,float preparation=0)
    {
        Vector2 axis=rotation.ToRotationVector2();
        float snap=launchAge<4?1-launchAge/4f:0;
        // 蓄势阶段只是视觉表现（此时 CanDamage 为 false）；
        // 出剑后的剑身始终对准真实的弹幕/碰撞位置。
        at-=axis*(preparation*9);
        Blade(texture,at,rotation,scale*(1+snap*.12f-preparation*.06f),tint,alpha,pulse);
    }
    public static void Ember(Vector2 at,Vector2 velocity,Color tint,float size,bool mote=false)
    {
        if(Main.dedServ)return;
        SoulFlowSpark.Spawn(new(at,velocity,tint,size,mote?36:25,mote));
    }
    public static void TraitAccent(Vector2 at,Color tint,bool inward=false)
    {
        if(Main.dedServ)return;
        for(int i=0;i<8;i++)
        {
            Vector2 axis=(i*MathHelper.TwoPi/8).ToRotationVector2();
            Ember(at+axis*(inward?35:5),axis*(inward?-1.9f:2.5f)+axis.RotatedBy(MathHelper.PiOver2)*1.4f,tint,.7f,true);
        }
    }
}
