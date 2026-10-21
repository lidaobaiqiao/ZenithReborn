using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public class OreSkyfall : ModProjectile
{
    public const int WarningFrames=30;
    public const float BaseDamageMultiplier=12;
    public int VisualType=ItemID.TitaniumSword;
    public string SoulId="";
    public float FallSpeed=32;
    private Vector2 aim,previousCenter;
    private int age;
    private int impactAge=30;
    private Vector2 impactAt;
    private long targetGeneration;
    private HashSet<long> struck=[];
    private SwordFlightTrail trail=new();
    public bool Falling=>age>WarningFrames;
    public static float DamageMultiplier(SoulItem soul)=>BaseDamageMultiplier*1.3f*(OreFamily.Count(soul)>=10?1.5f:1);
    public static bool Spawn(SoulItem soul,NPC target,Projectile parent)
    {
        Vector2 aim=target.Center;
        int index=Projectile.NewProjectile(parent.GetSource_FromThis(),aim-new Vector2(0,600),Vector2.Zero,
            ModContent.ProjectileType<OreSkyfall>(),Math.Max(1,(int)(parent.damage*DamageMultiplier(soul))),8,parent.owner,target.whoAmI);
        if(index>=Main.maxProjectiles)return false;
        var p=Main.projectile[index];var giant=(OreSkyfall)p.ModProjectile;
        giant.aim=aim;giant.SoulId=soul.SoulId;giant.targetGeneration=WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id;
        giant.VisualType=OreFamily.Representative(soul,OreFamily.Materials(soul).Last()).type;
        giant.FallSpeed=32*(OreFamily.Count(soul)>=10?1.5f:1);
        p.CritChance=parent.CritChance;p.netUpdate=true;return true;
    }
    public override string Texture=>"Terraria/Images/Item_"+ItemID.TitaniumSword;
    public override void SetDefaults()
    {
        Projectile.width=72;Projectile.height=160;Projectile.friendly=true;Projectile.DamageType=DamageClass.Melee;
        Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.penetrate=-1;Projectile.timeLeft=140;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=-1;
        age=0;impactAge=30;struck=[];trail=new();
    }
    public override bool ShouldUpdatePosition()=>Falling;
    private bool SourceValid()
    {
        if(Projectile.owner<0 || Projectile.owner>=Main.maxPlayers)return false;
        var player=Main.player[Projectile.owner];int source=Projectile.GetGlobalProjectile<SoulSource>().SourceWeapon;
        return player.active && !player.dead && player.HeldItem.ModItem is SoulItem s && s.Attached && s.SoulId==SoulId &&
            s.OreMastered && OreFamily.TriggerEligible(s,source) && Vector2.Distance(player.Center,Projectile.Center)<2400;
    }
    public override bool? CanDamage()=>Falling && SourceValid()?null:false;
    public override bool? CanHitNPC(NPC target)=>HardOrePlayer.ValidTarget(target) &&
        !struck.Contains(WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id)?null:false;
    public override void AI()
    {
        if(!SourceValid()){Projectile.Kill();return;}
        previousCenter=Projectile.Center;age++;impactAge++;
        if(!Falling)
        {
            int index=(int)Projectile.ai[0];
            if(index>=0 && index<Main.maxNPCs && HardOrePlayer.ValidTarget(Main.npc[index]))
            {
                long generation=WoodCombat.Root(Main.npc[index]).GetGlobalNPC<HardOreTarget>().Id;
                if(targetGeneration==0)targetGeneration=generation;
                if(targetGeneration==generation)aim=Main.npc[index].Center+new Vector2(Main.npc[index].velocity.X*12,0);
            }
            Projectile.Center=new Vector2(aim.X,Math.Max(48,aim.Y-600));
            Projectile.rotation=MathHelper.PiOver2;Projectile.velocity=Vector2.Zero;
            if(!Main.dedServ && age%3==0)
            {
                float angle=age*2.4f;
                Vector2 offset=angle.ToRotationVector2()*MathHelper.Lerp(62,24,age/(float)WarningFrames);
                offset.Y*=.35f;
                SwordSpark.Emit(aim+offset,-offset*.025f-new Vector2(0,.6f),new Color(255,213,125),.65f);
            }
            return;
        }
        Projectile.velocity=new Vector2(0,FallSpeed);Projectile.rotation=MathHelper.PiOver2;
        if(!Main.dedServ)
        {
            trail.Record(Projectile.Center,Projectile.rotation);
            Lighting.AddLight(Projectile.Center,new Vector3(.65f,.5f,.25f));
            if(age%2==0)
            {
                SwordSpark.Emit(Projectile.Center+new Vector2(Main.rand.NextFloat(-24,24),65),new Vector2(0,-2),new Color(255,221,145),1);
                SwordSpark.Emit(Projectile.Center+new Vector2(Main.rand.NextBool()?-28:28,-40),new Vector2(0,-3),new Color(255,213,125),.65f);
            }
        }
        if(Projectile.Center.Y>aim.Y+420)Projectile.Kill();
    }
    public override bool? Colliding(Rectangle projHitbox,Rectangle targetHitbox)
    {
        float point=0;
        return projHitbox.Intersects(targetHitbox) || Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(),targetHitbox.Size(),
            previousCenter-new Vector2(0,70),Projectile.Center+new Vector2(0,70),72,ref point);
    }
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(damageDone<=0)return;
        struck.Add(WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id);
        if(!Main.dedServ && impactAge>=8)
        {
            impactAge=0;impactAt=target.Center;
            OreSwordEffects.Impact(impactAt,-Vector2.UnitY,new Color(255,224,153),true);
        }
    }
    public override void SendExtraAI(BinaryWriter writer)
    {writer.Write(SoulId);writer.Write(VisualType);writer.Write(aim.X);writer.Write(aim.Y);writer.Write(age);writer.Write(FallSpeed);}
    public override void ReceiveExtraAI(BinaryReader reader)
    {SoulId=reader.ReadString();VisualType=reader.ReadInt32();aim=new(reader.ReadSingle(),reader.ReadSingle());age=reader.ReadInt32();FallSpeed=reader.ReadSingle();}
    public override bool PreDraw(ref Color lightColor)
    {
        Color gold=new(255,213,125);float reveal=Math.Clamp(age/18f,0,1);
        if(!Falling)
        {
            float charge=age/(float)WarningFrames;
            OreSwordEffects.Ring(aim,gold,MathHelper.Lerp(70,28,charge),reveal*.42f,.32f);
            OreSwordEffects.Halo(aim,gold,50,reveal*.12f);
            OreSwordEffects.Star(aim,gold,.8f,reveal*.6f);
            OreSwordEffects.Line(aim-new Vector2(0,580),aim,gold,2,.08f*reveal);
            // Narrow converging side marks instead of a screen-filling column.
            for(int side=-1;side<=1;side+=2)
                OreSwordEffects.Line(aim+new Vector2(side*48,-18),aim+new Vector2(side*18,-18),gold,1.5f,reveal*.35f);
        }
        OreSwordEffects.Trail(trail,gold,46);
        int type=OreFamily.IsOre(VisualType)?VisualType:ItemID.TitaniumSword;Main.instance.LoadItem(type);
        var texture=TextureAssets.Item[type].Value;float scale=5.6f;
        for(int i=Math.Min(trail.Count-1,6);i>=2;i-=2)
            Main.spriteBatch.Draw(texture,trail.Point(i)-Main.screenPosition,null,gold*(.1f*SwordFlightTrail.Strength(i-1,trail.Count)),
                MathHelper.Pi*.75f,texture.Size()*.5f,scale,SpriteEffects.None,0);
        OreSwordEffects.Blade(texture,Projectile.Center,MathHelper.PiOver2,scale,gold,Falling?1:reveal*.55f,1);
        if(impactAge<18)
        {
            float t=impactAge/18f,fade=1-t;
            OreSwordEffects.Ring(impactAt,gold,18+t*100,.65f*fade,.36f);
            OreSwordEffects.Ring(impactAt,Color.White,10+t*65,.3f*fade,.36f);
            OreSwordEffects.Halo(impactAt,gold,65,fade*.3f);
            OreSwordEffects.Star(impactAt,gold,1.5f*fade,.8f*fade);
        }
        return false;
    }
}
