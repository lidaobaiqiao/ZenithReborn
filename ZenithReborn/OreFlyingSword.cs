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
    // 各成员的收集年龄不同，但几何必须共用同一个相位——闭合缺口也一样，
    // 不能让每把剑按自己的年龄各转各的。
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
        // 不另开 DrawBehind 队列，交给普通弹幕绘制流程调用 PreDraw。
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
        if(targetIdentity==0) targetIdentity=identity; // 绑定本客户端的生成身份。
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
    // 光照强度必须和画面亮度匹配：之前 0.22 只有视觉亮度的四分之一，
    // 结果是"看着很亮却不照亮环境"。火把在泰拉里约等于 1.0。
    private const float LightFlight=0.95f;   // 飞行中（拖尾最亮）
    private const float LightIdle=0.55f;     // 成型悬停（无拖尾）

    // 泰拉没有真正的阴影，单点光看起来像灯泡。用主光 + 三个偏心辅助光，
    // 让照亮范围扩散开，读起来才像"光有体积"。
    private static void AddVolumeLight(Vector2 center,Vector3 tint)
    {
        Lighting.AddLight(center,tint);
        Lighting.AddLight(center+new Vector2(0,26),tint*.45f);
        Lighting.AddLight(center+new Vector2(-22,10),tint*.30f);
        Lighting.AddLight(center+new Vector2(22,10),tint*.30f);
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
            { Projectile.Kill();return; } // 槽位已复用，或目标失效／变成友方。
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
                Lighting.AddLight(Projectile.Center,tint.ToVector3()*LightIdle);
            }
            if(WaitingForVolley){Projectile.timeLeft=Math.Max(Projectile.timeLeft,120);return;}
            if(age<=GatherFrames)return;
            // 每把剑的就绪年龄对应同一个齐射时刻：等最后一把收集到的剑稳定下来再放。
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
            AddVolumeLight(Projectile.Center,OreFamily.Tints[Math.Clamp(Material,0,13)].ToVector3()*LightFlight);
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
        // 小额补充伤害按比例折算防御；强力飞剑不会把敌人护甲放大到正常100%计算之外。
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
        // 原版本地NPC免伤计时允许每6帧重复命中；接触时绝不停止、不换目标、不消耗穿透。
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if(Projectile.hide || !Launched && FormationAge<=0)return false;
        int type=OreFamily.MaterialOf(VisualType)==Material?VisualType:OreFamily.DefaultType[Material];
        Main.instance.LoadItem(type);
        float alpha=Launched?1:MathHelper.SmoothStep(0,1,Math.Clamp(FormationAge/6,0,1));
        // 绘制顺序：先画气流（在下面），再画剑本体（在上面），剑永远压住特效。
        // 物理前提：静止的剑没有相对气流，所以只有真正飞起来（Launched）才画。
        // 浓度读取真实速度；折返重新加速时也会重新建立气流。
        if(Launched && VfxSwordMask.Has(type))
        {
            float wake=MathHelper.Clamp(Projectile.velocity.Length()/(FlightSpeed*OreFlightMotion.PeakSpeed),0f,1f);
            float snap=launchFlashAge<4?1-launchFlashAge/4f:0;
            VfxSwordMask.Draw(type,Projectile.Center,Projectile.rotation,DisplayScale*(1+snap*.12f),
                Main.GlobalTimeWrappedHourly,alpha*wake);
        }
        OreSwordEffects.FlyingBlade(TextureAssets.Item[type].Value,Projectile.Center,Projectile.rotation,DisplayScale,
            OreFamily.Tints[Math.Clamp(Material,0,13)],alpha,1,Launched?launchFlashAge:30);
        return false;
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
