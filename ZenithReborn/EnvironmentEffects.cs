using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using E=ZenithReborn.SwordEnvironment;

namespace ZenithReborn;

// Children carry the soul's normal damage identity, but never run primary-hit
// environment logic. Local/root immunity prevents segmented enemies multiplying bursts.
public class EnvironmentEffect : ModProjectile
{
    public override string Texture=>"Terraria/Images/Extra_98";
    private int age;
    private readonly Dictionary<int,int> hitRoots=[];
    public E Environment=>(E)(int)Projectile.ai[0];
    public int Shape=>(int)Projectile.ai[1];
    public static int Spawn(Player owner,SoulItem soul,E e,Vector2 position,Vector2 velocity,int damage,int shape,Projectile parent=null)
    {
        if(owner.whoAmI!=Main.myPlayer || Main.netMode==NetmodeID.Server)return Main.maxProjectiles;
        IEntitySource source=parent!=null?parent.GetSource_FromThis():owner.GetSource_ItemUse(soul.Item);
        int index=Projectile.NewProjectile(source,position,velocity,ModContent.ProjectileType<EnvironmentEffect>(),damage,1,owner.whoAmI,(int)e,shape);
        if(index<Main.maxProjectiles){Main.projectile[index].GetGlobalProjectile<SoulSource>().SoulId=soul.SoulId;Main.projectile[index].netUpdate=true;}
        return index;
    }
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=24;Projectile.friendly=true;Projectile.DamageType=DamageClass.Melee;
        Projectile.penetrate=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.timeLeft=24;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=-1;
    }
    public override void OnSpawn(IEntitySource source)
    {
        Projectile.timeLeft=Shape switch{4=>180,5=>90,7=>100,2=>42,3=>42,6=>45,_=>24};
        if(Shape==4)Projectile.localNPCHitCooldown=30;
    }
    public override bool? CanDamage()=>Projectile.damage<=0 || Shape==3 && age<10?false:null;
    public override bool? CanHitNPC(NPC target)
    {
        int root=WoodCombat.Root(target).whoAmI;
        return hitRoots.TryGetValue(root,out int tick) && (Shape!=4 || age-tick<30)?false:null;
    }
    public override bool? Colliding(Rectangle projHitbox,Rectangle targetHitbox)
    {
        float radius=Shape==4?110:Shape==0?24+age*5:Shape==5?40:Shape==6?20:25;
        Vector2 point=Vector2.Clamp(Projectile.Center,targetHitbox.TopLeft(),targetHitbox.BottomRight());
        return Vector2.DistanceSquared(point,Projectile.Center)<=radius*radius;
    }
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        hitRoots[WoodCombat.Root(target).whoAmI]=age;
        if(Environment==E.GlowingMushroom && Projectile.owner>=0 && Projectile.owner<Main.maxPlayers)
        {
            var ep=Main.player[Projectile.owner].GetModPlayer<EnvironmentPlayer>();var s=ep.Held;
            if(s?.SoulId!=Projectile.GetGlobalProjectile<SoulSource>().SoulId)return;
            var mark=target.GetGlobalNPC<EnvironmentTarget>().Get(Projectile.owner,s.SoulId);
            mark.Spore=.08f*ep.Strength(E.GlowingMushroom);mark.Expires=Main.GameUpdateCount+180;
        }
    }
    public override void AI()
    {
        age++;
        if(Shape==0 || Shape==4)Projectile.velocity=Vector2.Zero;
        if(Shape==3 && age<10)Projectile.position-=Projectile.velocity;
        if(Shape==5 && age==45)Projectile.velocity=-Projectile.velocity;
        if(Shape==7)
        {
            var target=Main.npc.Where(n=>n.CanBeChasedBy(Projectile)).OrderBy(n=>Vector2.DistanceSquared(n.Center,Projectile.Center)).FirstOrDefault();
            if(target!=null)Projectile.velocity=Vector2.Lerp(Projectile.velocity,(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*11,.12f);
        }
        Projectile.rotation=Projectile.velocity.LengthSquared()>0?Projectile.velocity.ToRotation():age*.08f;
        if(Main.dedServ)return;
        var color=EnvironmentRules.Color(Environment);Lighting.AddLight(Projectile.Center,color.ToVector3()*.35f);
        if(age%3==0)
        {
            var dust=Dust.NewDustPerfect(Projectile.Center+Main.rand.NextVector2Circular(14,14),DustID.TintableDustLighted,
                -Projectile.velocity*.08f,120,color,.7f);dust.noGravity=true;
        }
    }
    internal static void Line(SpriteBatch batch,Vector2 a,Vector2 b,Color color,float width)
    {
        var d=b-a;
        // Match SoulUI's line renderer: MagicPixel is a larger texture in this
        // runtime. Scale a single texel, not the entire texture, into a thin line.
        batch.Draw(TextureAssets.MagicPixel.Value,a,new Rectangle(0,0,1,1),color,
            d.ToRotation(),new(0,.5f),new Vector2(d.Length(),width),SpriteEffects.None,0);
    }
    internal static void Ring(SpriteBatch batch,Vector2 center,float radius,Color color,int steps=48,float rotation=0)
    {
        Vector2 previous=center+new Vector2(radius,0).RotatedBy(rotation);
        for(int i=1;i<=steps;i++){var next=center+new Vector2(radius,0).RotatedBy(rotation+MathHelper.TwoPi*i/steps);Line(batch,previous,next,color,1.4f);previous=next;}
    }
    public override bool PreDraw(ref Color lightColor)
    {
        var at=Projectile.Center-Main.screenPosition;var color=EnvironmentRules.Color(Environment);
        float opacity=MathHelper.Clamp(Projectile.timeLeft/12f,0,1)*Math.Min(1,age/6f);
        if(Shape is 2 or 3 or 7)
        {
            int type=Projectile.GetGlobalProjectile<SoulSource>().SourceWeapon;
            if(type>0)
            {
                Main.instance.LoadItem(type);var texture=TextureAssets.Item[type].Value;
                for(int i=4;i>0;i--)Main.EntitySpriteDraw(texture,at-Projectile.velocity*i*.8f,null,color*(opacity*.08f),Projectile.rotation+MathHelper.PiOver4,texture.Size()/2,1.25f,SpriteEffects.None);
                Main.EntitySpriteDraw(texture,at,null,Color.Lerp(color,Color.White,.35f)*opacity,Projectile.rotation+MathHelper.PiOver4,texture.Size()/2,1.25f,SpriteEffects.None);
            }
            else Ring(Main.spriteBatch,at,14,color*opacity,8,Projectile.rotation);
        }
        else if(Shape==6)
        {
            var axis=Projectile.velocity.SafeNormalize(Vector2.UnitX);Line(Main.spriteBatch,at-axis*75,at+axis*75,color*(opacity*.4f),12);
            Line(Main.spriteBatch,at-axis*75,at+axis*75,Color.White*(opacity*.8f),3);
        }
        else
        {
            float radius=Shape==4?105:Shape==5?38:20+age*4;
            Ring(Main.spriteBatch,at,radius,color*(opacity*.65f),Shape==4?40:32,Projectile.rotation*.15f);
            Ring(Main.spriteBatch,at,radius*.72f,color*(opacity*.25f),24,-Projectile.rotation*.15f);
        }
        return false;
    }
}

public class EnvironmentDomain : ModProjectile
{
    public override string Texture=>"Terraria/Images/Extra_98";
    public override void SetDefaults()
    {Projectile.width=Projectile.height=2;Projectile.tileCollide=false;Projectile.timeLeft=2;Projectile.penetrate=-1;}
    public override void AI()
    {
        if(Projectile.owner<0 || Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        var player=Main.player[Projectile.owner];var ep=player.GetModPlayer<EnvironmentPlayer>();var s=ep.Held;
        if(!player.active || s==null || s.DomainEnvironment==E.None){Projectile.Kill();return;}
        Projectile.Center=player.Center;Projectile.timeLeft=2;Projectile.ai[0]=(int)s.DomainEnvironment;
        if(!Main.dedServ && Main.GameUpdateCount%8==0)
        {
            var color=EnvironmentRules.Color(s.DomainEnvironment);
            var d=Dust.NewDustPerfect(player.Center+Main.rand.NextVector2CircularEdge(52,42),DustID.TintableDustLighted,Vector2.Zero,140,color,.6f);d.noGravity=true;
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        var color=EnvironmentRules.Color((E)(int)Projectile.ai[0]);var at=Projectile.Center-Main.screenPosition;
        float turn=(float)(Main.GameUpdateCount%3600)/180f;
        EnvironmentEffect.Ring(Main.spriteBatch,at,EnvironmentRules.DomainRadius,color*.15f,128,turn*.04f);
        EnvironmentEffect.Ring(Main.spriteBatch,at,44+3*MathF.Sin(turn),color*.26f,40,turn);
        return false;
    }
}
