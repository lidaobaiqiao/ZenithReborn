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
            return;
        }
        Projectile.velocity=new Vector2(0,FallSpeed);Projectile.rotation=MathHelper.PiOver2;
        if(!Main.dedServ)
        {
            trail.Record(Projectile.Center,Projectile.rotation);
            Lighting.AddLight(Projectile.Center,new Vector3(.65f,.5f,.25f));
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
        int type=OreFamily.IsOre(VisualType)?VisualType:ItemID.TitaniumSword;Main.instance.LoadItem(type);
        OreSwordEffects.Blade(TextureAssets.Item[type].Value,Projectile.Center,MathHelper.PiOver2,5.6f,
            OreVisualRenderer.ForgeGold,Falling?1:Math.Clamp(age/18f,0,1)*.55f,1);
        return false;
    }
    public void DrawOrePrimitives()
    {
        float clock=Main.GlobalTimeWrappedHourly;
        float reach=Vector2.Distance(Projectile.Center,AirFront().Tip);
        if(Falling && trail.Count>=2)
            OreVisualRenderer.Flight(trail,OreVisualRenderer.ForgeGold,43,clock,tailOffset:140,tipReach:reach,head:Projectile.Center);
        else OreVisualRenderer.Sheath(Projectile.Center,MathHelper.PiOver2,reach,5.6f,
            OreVisualRenderer.ForgeGold,Math.Clamp(age/18f,0,1)*.55f,clock);
    }
    public OreAirFront AirFront()
    {
        int type=OreFamily.IsOre(VisualType)?VisualType:ItemID.TitaniumSword;Main.instance.LoadItem(type);
        return new(Projectile.Center+Vector2.UnitY*OreSwordEffects.BladeReach(TextureAssets.Item[type].Value,5.6f),
            Vector2.UnitY,OreVisualRenderer.ForgeGold,2.5f);
    }
    public override void OnKill(int timeLeft)
    {
        if(Main.dedServ || age<=0 && struck.Count==0)return;
        int type=OreFamily.IsOre(VisualType)?VisualType:ItemID.TitaniumSword;
        OrePierceFeedback.EndWeapon(new(Projectile.Center,struck.Count>0?impactAt:Projectile.Center,Vector2.UnitY,
            OreVisualRenderer.ForgeGold,type,5.6f,Falling||struck.Count>0));
    }
}
