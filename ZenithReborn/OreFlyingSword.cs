using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public class OreFlyingSword : ModProjectile
{
    public const float LungeSpeed=10.5f*1.1f;
    public const int AppearanceStep=3, GrowFrames=18, ChargeFrames=8;
    public int GatherFrames=>readyAge;
    public bool WaitingForVolley {get;private set;}
    private int readyAge=GrowFrames+ChargeFrames;
    public void WaitForVolley(){WaitingForVolley=true;Projectile.netUpdate=true;}
    public void ReleaseVolley(int delay){WaitingForVolley=false;readyAge=age+delay;Projectile.netUpdate=true;}
    public int Age=>age;
    public bool Launched { get; private set; }
    public bool Mastered;
    public float DamageShare=OreFamily.BaseFlightDamage;
    public int WaveId;
    public int SetCount=1;
    public int AttackPass {get;private set;}
    private bool traitAttempted;
    public float FlightSpeed=>LungeSpeed*(SetCount>=10?1.5f:1);
    public bool TraitWindow=>!traitAttempted && SetCount>=8;
    public float FlightDistance=>Mastered?840f:560f;
    public float FormationAge=>age;
    // Collection ages differ across members. Geometry must share one phase,
    // including the closing gap, instead of rotating each blade by its own age.
    public static float FormationAngle(int index,int total)=>-MathHelper.PiOver2+MathHelper.TwoPi*index/Math.Max(1,total);
    public float FormationScale=>BloomScale(FormationAge);
    public float DisplayScale=>1.85f*Projectile.scale*(Launched?1:FormationScale);
    public static float BloomScale(float localAge) => localAge<=10?
        MathHelper.SmoothStep(.35f,1.4f,Math.Clamp(localAge/10,0,1)):
        MathHelper.SmoothStep(1.4f,1,Math.Clamp((localAge-10)/8,0,1));
    public int TargetIndex, Material, Total, RingIndex, VisualType;
    public string SourceSoulId = "";
    private int age;
    private int hitFlashAge=30,launchFlashAge=30;
    private Vector2 hitFlashAt;
    private long targetIdentity;
    private Vector2 launchedFrom,aimPoint,aimVelocity,previousCenter;
    private readonly SwordFlightTrail trail=new();
    public override string Texture => "Terraria/Images/Item_" + ItemID.Zenith;
    public void BindTarget(NPC npc)
    {
        TargetIndex=npc.whoAmI;aimPoint=npc.Center;aimVelocity=npc.velocity;
        targetIdentity=npc.GetGlobalNPC<HardOreTarget>().Id;
    }
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=20;
        Projectile.friendly=true; Projectile.DamageType=DamageClass.Melee;
        Projectile.tileCollide=false; Projectile.ignoreWater=true;
        Projectile.penetrate=-1; Projectile.timeLeft=240;
        Projectile.usesLocalNPCImmunity=true; Projectile.localNPCHitCooldown=6;
        // No custom DrawBehind queue: let the normal projectile pass invoke PreDraw.
        Projectile.hide=false;
        trail.Clear();
        age=0;Launched=false;targetIdentity=0;WaitingForVolley=false;readyAge=GrowFrames+ChargeFrames;
        SetCount=1;AttackPass=0;traitAttempted=false;
        hitFlashAge=launchFlashAge=30;
    }
    private NPC Target()
    {
        if(TargetIndex<0 || TargetIndex>=Main.maxNPCs) return null;
        NPC npc=Main.npc[TargetIndex];
        if(!HardOrePlayer.ValidTarget(npc)) return null;
        long identity=npc.GetGlobalNPC<HardOreTarget>().Id;
        if(targetIdentity==0) targetIdentity=identity; // Bind to this client's spawn identity.
        return targetIdentity==identity?npc:null;
    }
    private bool SourceValid()
    {
        if(Projectile.owner<0 || Projectile.owner>=Main.maxPlayers) return false;
        Player player=Main.player[Projectile.owner];
        var origin=Projectile.GetGlobalProjectile<SoulSource>();
        int source=origin.SourceWeapon;
        return player.active && !player.dead && player.HeldItem.ModItem is SoulItem soul && soul.Attached &&
            soul.SoulId==SourceSoulId && origin.PrimaryOrdinal>=0 && OreFamily.TriggerEligible(soul,source,origin.PrimaryOrdinal) &&
            OreFamily.Materials(soul).Contains(Material) && Vector2.Distance(player.Center,Projectile.Center)<=2000;
    }
    public override bool? CanDamage() => Launched && SourceValid()?null:false;
    public override bool? CanHitNPC(NPC target) => HardOrePlayer.ValidTarget(target)?null:false;
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(TargetIndex);writer.Write(Material);writer.Write(Total);writer.Write(RingIndex);
        writer.Write(SourceSoulId);writer.Write(VisualType);writer.Write(age);
        writer.Write(Launched);writer.Write(Mastered);writer.Write(WaveId);
        writer.Write(launchedFrom.X);writer.Write(launchedFrom.Y);
        writer.Write(WaitingForVolley);writer.Write(readyAge);writer.Write(DamageShare);
        writer.Write(aimPoint.X);writer.Write(aimPoint.Y);writer.Write(aimVelocity.X);writer.Write(aimVelocity.Y);
        writer.Write(SetCount);writer.Write(AttackPass);writer.Write(traitAttempted);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        TargetIndex=reader.ReadInt32();Material=Math.Clamp(reader.ReadInt32(),0,13);
        Total=Math.Clamp(reader.ReadInt32(),1,14);RingIndex=Math.Clamp(reader.ReadInt32(),0,13);
        SourceSoulId=reader.ReadString();VisualType=reader.ReadInt32();
        age=Math.Max(0,reader.ReadInt32());Launched=reader.ReadBoolean();Mastered=reader.ReadBoolean();WaveId=reader.ReadInt32();
        launchedFrom=new(reader.ReadSingle(),reader.ReadSingle());
        WaitingForVolley=reader.ReadBoolean();readyAge=Math.Max(0,reader.ReadInt32());DamageShare=reader.ReadSingle();
        aimPoint=new(reader.ReadSingle(),reader.ReadSingle());aimVelocity=new(reader.ReadSingle(),reader.ReadSingle());
        SetCount=Math.Clamp(reader.ReadInt32(),1,14);AttackPass=Math.Clamp(reader.ReadInt32(),0,1);traitAttempted=reader.ReadBoolean();
    }
    public override void AI()
    {
        if(!SourceValid()) { Projectile.Kill();return; }
        previousCenter=Projectile.Center;
        age++;
        hitFlashAge++;launchFlashAge++;
        if(!Launched)
        {
            NPC target=Target();
            if(target!=null){aimPoint=target.Center;aimVelocity=target.velocity;}
            else if(TargetIndex>=0 && TargetIndex<Main.maxNPCs && Main.npc[TargetIndex].active)
            { Projectile.Kill();return; } // Reused slot or invalid/friendly target.
            float angle=FormationAngle(RingIndex,Total);
            float radius=Math.Clamp(Math.Max(170,(target==null?0:Math.Max(target.width,target.height))*.5f+110),170,360);
            Projectile.Center=aimPoint+angle.ToRotationVector2()*radius;
            Projectile.velocity=Vector2.Zero;
            Projectile.rotation=(aimPoint-Projectile.Center).ToRotation();
            Projectile.hide=FormationAge<=0;
            if(!Main.dedServ && FormationAge>0)
            {
                Color tint=OreFamily.Tints[Math.Clamp(Material,0,13)];
                Lighting.AddLight(Projectile.Center,tint.ToVector3()*.22f);
                if((int)FormationAge%4==0) Spark(1,Main.rand.NextVector2Circular(.7f,.7f),.75f);
            }
            if(WaitingForVolley){Projectile.timeLeft=Math.Max(Projectile.timeLeft,120);return;}
            if(age<=GatherFrames)return;
            // Per-member ready ages correspond to one common release tick,
            // after the final collected blade has finished settling.
            Launched=true;Projectile.hide=false;
            launchFlashAge=0;
            trail.Clear();
            launchedFrom=Projectile.Center;
            float lead=Math.Min(24,Vector2.Distance(aimPoint,Projectile.Center)/FlightSpeed);
            Projectile.velocity=(aimPoint+aimVelocity*lead-Projectile.Center).SafeNormalize(Vector2.UnitY)*FlightSpeed;
            previousCenter=Projectile.Center;
            Projectile.timeLeft=(int)Math.Ceiling(FlightDistance/FlightSpeed)+3;
            Projectile.netUpdate=true;
            if(!Main.dedServ) Spark(3,-Projectile.velocity*.1f,.8f);
        }
        Projectile.rotation=Projectile.velocity.ToRotation();
        if(!Main.dedServ)
        {
            trail.Record(Projectile.Center,Projectile.rotation);
            if(age%3==0)
            {
                Color tint=OreFamily.Tints[Math.Clamp(Material,0,13)];
                Vector2 axis=Projectile.rotation.ToRotationVector2();
                SwordSpark.Emit(Projectile.Center+axis*22*Projectile.scale,
                    -Projectile.velocity*.08f+Main.rand.NextVector2Circular(.6f,.6f),tint,.65f);
                if(age%6==0)SwordSpark.Emit(Projectile.Center-axis*16,
                    -Projectile.velocity*.06f,tint,.4f);
            }
            Lighting.AddLight(Projectile.Center,OreFamily.Tints[Math.Clamp(Material,0,13)].ToVector3()*.22f);
        }
        if(Vector2.Distance(launchedFrom,Projectile.Center)>FlightDistance)
        {
            if(SetCount>=6 && AttackPass==0)
            {
                AttackPass=1;launchedFrom=Projectile.Center;previousCenter=Projectile.Center;
                launchFlashAge=0;
                Projectile.velocity=-Projectile.velocity;Projectile.rotation=Projectile.velocity.ToRotation();
                Projectile.timeLeft=(int)Math.Ceiling(FlightDistance/FlightSpeed)+3;trail.Clear();Projectile.netUpdate=true;
                if(!Main.dedServ)Spark(5,-Projectile.velocity*.08f,.85f);
            }
            else Projectile.Kill();
        }
    }
    public override bool? Colliding(Rectangle projHitbox,Rectangle targetHitbox)
    {
        float point=0;
        return projHitbox.Intersects(targetHitbox) || Collision.CheckAABBvLineCollision(
            targetHitbox.TopLeft(),targetHitbox.Size(),previousCenter,Projectile.Center,20*Projectile.scale*
                (SetCount>=8 && WeaponFamilies.Of(VisualType)==BladeFamily.Solid?1.15f:1),ref point);
    }
    public override void ModifyHitNPC(NPC target,ref NPC.HitModifiers modifiers)
    {
        // Small supplemental hits scale defense proportionally; strong flying
        // swords never amplify enemy armor beyond the normal 100% calculation.
        modifiers.DefenseEffectiveness*=Math.Clamp(DamageShare,0,1);
        Main.player[Projectile.owner].GetModPlayer<HardOrePlayer>().Modify(Projectile,target,ref modifiers);
        if(SetCount>=8 && VisualType==ItemID.CopperShortsword && Vector2.Distance(Main.player[Projectile.owner].Center,target.Center)<180)
            modifiers.FinalDamage*=1.2f;
    }
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(CanHitNPC(target)==false) return;
        if(!Main.dedServ && hitFlashAge>=12)
        {
            hitFlashAge=0;hitFlashAt=Vector2.Clamp(Projectile.Center,target.Hitbox.TopLeft(),target.Hitbox.BottomRight());
            OreSwordEffects.Impact(hitFlashAt,-Projectile.velocity,OreFamily.Tints[Math.Clamp(Material,0,13)],false);
        }
        if(TraitWindow && damageDone>0)
        {
            Main.player[Projectile.owner].GetModPlayer<HardOrePlayer>().Hit(Projectile,target,damageDone);
            traitAttempted=true;Projectile.netUpdate=true;
        }
        // Native local NPC immunity allows repeated hits every six frames.
        // Never stop, retarget or consume penetration on contact.
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if(Projectile.hide || !Launched && FormationAge<=0) return false;
        int type=OreFamily.MaterialOf(VisualType)==Material?VisualType:OreFamily.DefaultType[Material];
        Main.instance.LoadItem(type);
        Texture2D texture=TextureAssets.Item[type].Value;
        Color tint=OreFamily.Tints[Math.Clamp(Material,0,13)];
        float alpha=Launched?1:MathHelper.SmoothStep(0,1,Math.Clamp(FormationAge/6,0,1));
        float scale=DisplayScale;
        OreSwordEffects.Trail(trail,tint,12*Projectile.scale);
        for(int i=Math.Min(trail.Count-1,6);i>=2;i-=2)
            Main.spriteBatch.Draw(texture,trail.Point(i)-Main.screenPosition,null,tint*(.17f*SwordFlightTrail.Strength(i-1,trail.Count)),
                trail.Rotation(i)+MathHelper.PiOver4,texture.Size()*.5f,scale,SpriteEffects.None,0);
        float pulse=.8f+.2f*MathF.Sin(age*.12f+RingIndex);
        OreSwordEffects.Blade(texture,Projectile.Center,Projectile.rotation,scale,tint,alpha,pulse);
        if(!Launched)
        {
            float bloom=1-Math.Clamp(FormationAge/22,0,1);
            if(bloom>0)OreSwordEffects.Ring(Projectile.Center,tint,12+22*(1-bloom),bloom*.36f);
        }
        // A small moving highlight across the blade, separate from the trail.
        Vector2 gleam=Projectile.Center+
            Projectile.rotation.ToRotationVector2()*MathF.Sin(age*.18f)*texture.Size().Length()*.2f*scale;
        OreSwordEffects.Star(gleam,tint,.32f,.42f*alpha,Projectile.rotation);
        if(launchFlashAge<8)OreSwordEffects.Star(Projectile.Center,tint,.8f,1-launchFlashAge/8f,Projectile.rotation);
        if(hitFlashAge<12)
        {
            float fade=1-hitFlashAge/12f;
            OreSwordEffects.Star(hitFlashAt,tint,.6f*fade,.65f*fade,Projectile.rotation+.6f);
        }
        return false;
    }
    private void Spark(int count,Vector2 drift,float scale)
    {
        Color tint=OreFamily.Tints[Math.Clamp(Material,0,13)];
        for(int i=0;i<count;i++)
        {
            SwordSpark.Emit(Projectile.Center,drift+Main.rand.NextVector2Circular(1.5f,1.5f),tint,scale);
        }
    }
}
