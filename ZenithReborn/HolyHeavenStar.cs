using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 使用原版落星的贴图、尺寸和坠落式样；命中或撞上方块时产生单独的范围冲击。
public class HolyHeavenStar : ModProjectile
{
    public override string Texture=>"Terraria/Images/Projectile_"+ProjectileID.FallingStar;
    private bool impacted;
    public override void SetDefaults()
    {
        Projectile.CloneDefaults(ProjectileID.FallingStar);
        Projectile.aiStyle=ProjAIStyleID.FallingStar;Projectile.hostile=false;Projectile.friendly=true;
        Projectile.DamageType=DamageClass.Generic;Projectile.timeLeft=180;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=-1;
    }
    public override void AI()=>HolyHeavenStarVisuals.Flight(Projectile);
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)=>Impact(WoodCombat.Root(target).whoAmI+1,Projectile.velocity);
    public override bool OnTileCollide(Vector2 oldVelocity){Impact(0,oldVelocity);return false;}
    private void Impact(int excludedRoot,Vector2 velocity)
    {
        if(impacted)return;impacted=true;
        if(Projectile.owner==Main.myPlayer)
            Projectile.NewProjectile(Projectile.GetSource_FromThis(),Projectile.Center,Vector2.Zero,
                ModContent.ProjectileType<HolyHeavenImpact>(),HolyHeavenProc.StarDamage,10,Projectile.owner,excludedRoot);
        HolyHeavenStarVisuals.Impact(Projectile,velocity);
        Projectile.Kill();
    }
}

// 直击目标已经受到落星伤害，冲击波只伤害范围内其他目标主体。
public class HolyHeavenImpact : ModProjectile
{
    public const int Radius=120;
    private readonly HashSet<int> struck=[];
    public override string Texture=>"Terraria/Images/Projectile_"+ProjectileID.FallingStar;
    public override void SetDefaults()
    {
        Projectile.width=Radius*2;Projectile.height=Radius*2;Projectile.friendly=true;
        Projectile.DamageType=DamageClass.Generic;Projectile.tileCollide=false;Projectile.ignoreWater=true;
        Projectile.penetrate=-1;Projectile.timeLeft=2;Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=-1;
    }
    public override bool? CanHitNPC(NPC target)=>!HardOrePlayer.ValidTarget(target,false) ||
        WoodCombat.Root(target).whoAmI+1==(int)Projectile.ai[0] || struck.Contains(WoodCombat.Root(target).whoAmI)?false:null;
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {if(damageDone>0)struck.Add(WoodCombat.Root(target).whoAmI);}
    public override bool? Colliding(Rectangle projectileHitbox,Rectangle targetHitbox)
    {
        Vector2 c=Projectile.Center;
        float x=MathHelper.Clamp(c.X,targetHitbox.Left,targetHitbox.Right),y=MathHelper.Clamp(c.Y,targetHitbox.Top,targetHitbox.Bottom);
        return Vector2.DistanceSquared(c,new Vector2(x,y))<=Radius*Radius;
    }
    public override bool PreDraw(ref Color lightColor)=>false;
}
