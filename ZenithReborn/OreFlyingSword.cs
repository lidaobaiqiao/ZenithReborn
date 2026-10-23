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
    public float FlightSpeed=>LungeSpeed*(SetCount>=10?1.5f:1)*(Projectile.owner>=0 && Projectile.owner<Main.maxPlayers?
        1+Main.player[Projectile.owner].GetModPlayer<EnvironmentPlayer>().Stat("FlightSpeed"):1);
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
    private int passAge;
    public int PassAge=>passAge;
    private int hitFlashAge=30,launchFlashAge=30;
    private Vector2 hitFlashAt;
    private int visualPass=-1;
    private readonly HashSet<long> visualStruck=[];
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
        passAge=0;
        SetCount=1;AttackPass=0;traitAttempted=false;
        hitFlashAge=launchFlashAge=30;
        visualPass=-1;visualStruck.Clear();
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
        writer.Write(passAge);
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
        passAge=Math.Max(0,reader.ReadInt32());
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
            if(!WaitingForVolley)radius+=OreFlightMotion.Pullback(age,GatherFrames);
            Projectile.Center=aimPoint+angle.ToRotationVector2()*radius;
            Projectile.velocity=Vector2.Zero;
            Projectile.rotation=(aimPoint-Projectile.Center).ToRotation();
            Projectile.hide=FormationAge<=0;
            if(!Main.dedServ && FormationAge>0)
            {
                Color tint=OreFamily.Tints[Math.Clamp(Material,0,13)];
                Lighting.AddLight(Projectile.Center,tint.ToVector3()*.22f);
            }
            if(WaitingForVolley){Projectile.timeLeft=Math.Max(Projectile.timeLeft,120);return;}
            if(age<=GatherFrames)return;
            // Per-member ready ages correspond to one common release tick,
            // after the final collected blade has finished settling.
            Launched=true;Projectile.hide=false;
            passAge=0;
            launchFlashAge=0;
            trail.Clear();
            launchedFrom=Projectile.Center;
            float lead=Math.Min(24,OreFlightMotion.TravelFrames(Vector2.Distance(aimPoint,Projectile.Center),FlightSpeed));
            Projectile.velocity=(aimPoint+aimVelocity*lead-Projectile.Center).SafeNormalize(Vector2.UnitY)*FlightSpeed;
            previousCenter=Projectile.Center;
            Projectile.timeLeft=OreFlightMotion.TravelFrames(FlightDistance,FlightSpeed)+4;
            Projectile.netUpdate=true;
        }
        Projectile.velocity=Projectile.velocity.SafeNormalize(Vector2.UnitY)*FlightSpeed*OreFlightMotion.SpeedMultiplier(passAge);
        passAge++;
        Projectile.rotation=Projectile.velocity.ToRotation();
        if(!Main.dedServ)
        {
            if(visualPass!=AttackPass)
            {
                visualPass=AttackPass;launchFlashAge=0;
                OrePierceFeedback.Launch(Projectile.Center,Projectile.velocity,OreFamily.Tints[Math.Clamp(Material,0,13)]);
            }
            trail.Record(Projectile.Center,Projectile.rotation);
            Lighting.AddLight(Projectile.Center,OreFamily.Tints[Math.Clamp(Material,0,13)].ToVector3()*.22f);
        }
        if(Vector2.Distance(launchedFrom,Projectile.Center)>FlightDistance)
        {
            if(SetCount>=6 && AttackPass==0)
            {
                AttackPass=1;launchedFrom=Projectile.Center;previousCenter=Projectile.Center;
                passAge=0;
                launchFlashAge=0;
                Projectile.velocity=-Projectile.velocity.SafeNormalize(Vector2.UnitY)*FlightSpeed*OreFlightMotion.SpeedMultiplier(0);Projectile.rotation=Projectile.velocity.ToRotation();
                Projectile.timeLeft=OreFlightMotion.TravelFrames(FlightDistance,FlightSpeed)+4;trail.Clear();Projectile.netUpdate=true;
                visualStruck.Clear();
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
        if(!Main.dedServ && damageDone>0 && visualStruck.Add(WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id))
        {
            hitFlashAge=0;hitFlashAt=Vector2.Clamp(Projectile.Center,target.Hitbox.TopLeft(),target.Hitbox.BottomRight());
            OrePierceFeedback.Strike(hitFlashAt,Projectile.velocity,OreFamily.Tints[Math.Clamp(Material,0,13)]);
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
        if(Projectile.hide || !Launched && FormationAge<=0)return false;
        int type=OreFamily.MaterialOf(VisualType)==Material?VisualType:OreFamily.DefaultType[Material];
        Main.instance.LoadItem(type);
        float alpha=Launched?1:MathHelper.SmoothStep(0,1,Math.Clamp(FormationAge/6,0,1));
        OreSwordEffects.FlyingBlade(TextureAssets.Item[type].Value,Projectile.Center,Projectile.rotation,DisplayScale,
            OreFamily.Tints[Math.Clamp(Material,0,13)],alpha,1,Launched?launchFlashAge:30);
        return false;
    }
    public void DrawOrePrimitives()
    {
        if(Projectile.hide || !Launched && FormationAge<=0)return;
        var front=AirFront();float clock=Main.GlobalTimeWrappedHourly;
        float reach=Vector2.Distance(Projectile.Center,front.Tip);
        if(Launched && trail.Count>=2)
            OreVisualRenderer.Flight(trail,front.Tint,14*Projectile.scale,clock,tailOffset:32*Projectile.scale,
                tipReach:reach,heat:Math.Clamp(1-hitFlashAge/12f,0,1),head:Projectile.Center);
        else OreVisualRenderer.Sheath(Projectile.Center,Projectile.rotation,reach,DisplayScale,front.Tint,
            Math.Clamp(FormationAge/6,0,1),clock);
    }
    public OreAirFront AirFront()
    {
        int type=OreFamily.MaterialOf(VisualType)==Material?VisualType:OreFamily.DefaultType[Math.Clamp(Material,0,13)];
        Main.instance.LoadItem(type);
        float snap=launchFlashAge<4?1-launchFlashAge/4f:0;
        Vector2 axis=Projectile.rotation.ToRotationVector2();
        float tip=OreSwordEffects.BladeReach(TextureAssets.Item[type].Value,DisplayScale*(1+snap*.12f));
        return new(Projectile.Center+axis*tip,axis,OreFamily.Tints[Math.Clamp(Material,0,13)],Projectile.scale);
    }
    public override void OnKill(int timeLeft)
    {
        if(Main.dedServ || Projectile.hide || !Launched && FormationAge<=0)return;
        int type=OreFamily.MaterialOf(VisualType)==Material?VisualType:OreFamily.DefaultType[Math.Clamp(Material,0,13)];
        OrePierceFeedback.EndWeapon(new(Projectile.Center,Projectile.Center,Projectile.velocity.SafeNormalize(Vector2.UnitX),
            OreFamily.Tints[Math.Clamp(Material,0,13)],type,DisplayScale,false));
    }
}
