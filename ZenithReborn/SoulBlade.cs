using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

public class SoulSource : GlobalProjectile
{
    public override bool InstancePerEntity => true;
    public string SoulId = "";
    public float LeashRadius;
    public int SourceWeapon;
    public int PrimaryOrdinal=-1;
    public int HostSlot=-1;
    private bool environmentFirstHit;
    public bool EnvironmentServerHit;
    public override void OnHitNPC(Projectile p,NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(string.IsNullOrEmpty(SoulId) || damageDone<=0 || p.owner<0 || p.owner>=Main.maxPlayers)return;
        bool first=p.ModProjectile is SoulBlade && !environmentFirstHit;
        if(first)environmentFirstHit=true;
        Main.player[p.owner].GetModPlayer<EnvironmentPlayer>().Hit(p,target,hit,damageDone,first);
    }
    public override void ModifyHitNPC(Projectile projectile,NPC target,ref NPC.HitModifiers modifiers)
    {
        if(string.IsNullOrEmpty(SoulId) || projectile.owner<0 || projectile.owner>=Main.maxPlayers)return;
        var state=Main.player[projectile.owner].GetModPlayer<BlessingPlayer>();
        if(state.Held?.SoulId==SoulId)modifiers.FinalDamage*=state.DistanceMultiplier(state.Held,target);
        Main.player[projectile.owner].GetModPlayer<EnvironmentPlayer>().ModifyHit(projectile,target,ref modifiers);
    }
    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        SoulId = source switch {
            EntitySource_ItemUse item when item.Item.ModItem is SoulItem soul => soul.SoulId,
            EntitySource_Parent parent when parent.Entity is Projectile p => p.GetGlobalProjectile<SoulSource>().SoulId,
            _ => ""
        };
        SourceWeapon = projectile.ModProjectile is SoulBlade ? (int)projectile.ai[0] :
            source is EntitySource_Parent ancestry && ancestry.Entity is Projectile ancestor ? ancestor.GetGlobalProjectile<SoulSource>().SourceWeapon : 0;
        PrimaryOrdinal=source is EntitySource_Parent parentSource && parentSource.Entity is Projectile parentProjectile?
            parentProjectile.GetGlobalProjectile<SoulSource>().PrimaryOrdinal:-1;
        HostSlot=source is EntitySource_Parent hostSource && hostSource.Entity is Projectile hostParent?
            hostParent.GetGlobalProjectile<SoulSource>().HostSlot:-1;
    }
    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter writer)
    { writer.Write(SoulId); writer.Write(LeashRadius); writer.Write(SourceWeapon); writer.Write(PrimaryOrdinal); writer.Write(HostSlot); }
    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader reader)
    { SoulId = reader.ReadString(); LeashRadius=reader.ReadSingle(); SourceWeapon=reader.ReadInt32(); PrimaryOrdinal=reader.ReadInt32(); HostSlot=reader.ReadInt32(); }
    public override void AI(Projectile projectile)
    {
        if(LeashRadius>0 && projectile.owner>=0 && projectile.owner<Main.maxPlayers &&
            Vector2.Distance(projectile.Center,Main.player[projectile.owner].Center)>LeashRadius) projectile.Kill();
    }
}

public class SoulBlade : ModProjectile
{
    private SoulTrailEnding.Handle[] trailEndings=new SoulTrailEnding.Handle[SoulTrailUnits.Count];
    public Vector2 TargetOffset;
    public float SwordScale = 1;
    // 剑体、剑影、拖尾与粒子统一放大；碰撞和来源尺寸仍读取原始 Projectile.scale。
    public float DisplayScale => Projectile.scale * 1.6f;
    // 与原版933保持相同阶段增量，每游戏帧更新两次。
    public float MotionPhase => Projectile.localAI[0];
    public static float SpriteVisibility(float phase) => Utils.GetLerpValue(0,20,phase,true)*Utils.GetLerpValue(68,60,phase,true);
    public float BladeOpacity => SpriteVisibility(MotionPhase)*Projectile.Opacity*(retired?1-retireAge/8f:1);
    // 原版旋转为轴角加半周的一半，物品贴图再按朝向偏转；
    // 碰撞与拖尾仍以轴角加四分之一半周保存。
    public float DrawRotation => Projectile.rotation+MathHelper.PiOver4*(1-Projectile.spriteDirection);
    public SpriteEffects DrawEffects => Projectile.spriteDirection==1?SpriteEffects.None:SpriteEffects.FlipHorizontally;
    public int SpecialDamage;
    public BladeBlessings Blessings=new();
    public bool Reborn { get; private set; }
    public void ConfigureReborn(bool reborn)
    {
        Reborn = reborn;
        // 每帧两次更新，16次更新对应八游戏帧命中免疫。
        Projectile.localNPCHitCooldown = 16;
    }
    // 原版天顶933以随机值控制椭圆纵向半径与倾斜，每把剑各有一条椭圆轨迹。
    public float Vertical = 60f;
    private int age;
    private bool released;
    private int oreHitUnits;
    public int HitUnit {get;private set;}
    private bool retired,terrainArmed;
    private int retireAge;
    private float terrainDepth;
    private Vector2 previousCenter;
    private Dictionary<long,int> targetHits=[];
    private SoulItem SourceSoul=>Projectile.owner>=0 && Projectile.owner<Main.maxPlayers?
        WoodCombat.FindSoul(Main.player[Projectile.owner],Projectile.GetGlobalProjectile<SoulSource>().SoulId):null;
    public bool Retired=>retired;
    public void Retire(){if(retired)return;retired=true;retireAge=0;Projectile.netUpdate=true;}
    // 本体和三个实体剑影各有一次资格；是否允许剑影参与由矿石特性统一判定。
    public bool TryConsumeOreHit(int unit=0)
    {
        if(unit<0 || unit>=SoulTrailUnits.Count || (oreHitUnits&(1<<unit))!=0)return false;
        oreHitUnits|=1<<unit;
        return true;
    }
    private Vector2[] trailPoints;
    private Vector2[] trailCenters;
    private float[] trailRotations;
    private float[] trailPhases;
    // 原版会为每个历史点保存 oldSpriteDirection；剑飞行途中会翻转朝向，
    // 旧剑影必须用它自己那一帧的朝向，否则镜像和旋转偏移都会错。
    private int[] trailDirections;
    private const int TrailLength = SoulRibbonMesh.HistoryLength;
    private const int HistoryLength = 96;
    private int trailCount;
    private Vector2 lastTrailAnchor;
    private bool hasTrailAnchor;
    public override string Texture => "Terraria/Images/Item_" + ItemID.Zenith;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 38;
        Projectile.friendly = true; Projectile.DamageType = DamageClass.Melee;
        Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.penetrate = -1; Projectile.timeLeft = 120;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 16;
        Projectile.extraUpdates=1;
        Reborn = false;age=0;released=false;retired=terrainArmed=false;retireAge=0;terrainDepth=0;targetHits=[];
        oreHitUnits=0;HitUnit=0;
        Blessings=new();
        Vertical = 60f;
        trailPoints=new Vector2[HistoryLength];trailCenters=new Vector2[HistoryLength];trailRotations=new float[HistoryLength];trailPhases=new float[HistoryLength];trailDirections=new int[HistoryLength];
        trailCount=0;hasTrailAnchor=false;
        trailEndings=new SoulTrailEnding.Handle[SoulTrailUnits.Count];
    }
    public override bool ShouldUpdatePosition() => false;
    // 本体仍在原阶段六十停止伤害；可见剑影按自己的时间窗继续，不给消散材质附加碰撞。
    public bool BodyCanDamage=>!retired&&MotionPhase<60&&age>Projectile.ai[2];
    public override bool? CanDamage()
    {
        if(retired||age<=Projectile.ai[2])return false;
        if(BodyCanDamage)return null;
        for(int echo=1;echo<SoulTrailUnits.Count;echo++)
            if(TryGetEchoPose(echo,out _,out _,out _))return null;
        return false;
    }
    // 绘制和伤害读取完全相同的十五更新采样及可见阈值；不将短尾网格算成实体剑。
    public bool TryGetEchoPose(int echo,out Vector2 center,out float rotation,out float opacity)
    {
        center=Vector2.Zero;rotation=opacity=0;
        if(echo<=0||echo>=SoulTrailUnits.Count)return false;
        int index=echo*SoulTrailUnits.Spacing;
        if(index>=trailCount)return false;
        opacity=SoulTrailUnits.At(echo,trailCount,MotionPhase).Opacity*Projectile.Opacity*(retired?1-retireAge/8f:1);
        if(opacity<=.02f)return false;
        center=trailCenters[index];rotation=trailRotations[index];return true;
    }
    public override bool? CanHitNPC(NPC target)
    {
        var soul=SourceSoul;
        if(soul==null)return null;
        long key=WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id;
        return targetHits.GetValueOrDefault(key)>=SoulCurses.HitLimit(soul)?false:null;
    }
    public override void SendExtraAI(BinaryWriter writer)
    { writer.Write(TargetOffset.X); writer.Write(TargetOffset.Y); writer.Write(SwordScale); writer.Write(Vertical); writer.Write(SpecialDamage); writer.Write(Reborn);writer.Write(retired);writer.Write(retireAge); }
    public override void ReceiveExtraAI(BinaryReader reader)
    { TargetOffset = new(reader.ReadSingle(), reader.ReadSingle()); SwordScale = reader.ReadSingle(); Vertical = reader.ReadSingle(); SpecialDamage = reader.ReadInt32(); ConfigureReborn(reader.ReadBoolean());retired=reader.ReadBoolean();retireAge=Math.Clamp(reader.ReadInt32(),0,8); }
    public override void AI()
    {
        Player player = Main.player[Projectile.owner];
        if (!player.active || player.dead) { Projectile.Kill(); return; }
        // 相同主人位移作用于历史剑影，避免人物移动把原本的轨迹拉成斜带。
        // 服务器与客户端使用同一历史，剑影显示和碰撞不会分离。
        Vector2 anchor=player.MountedCenter;
        if(hasTrailAnchor && trailCount>0)
        {
            Vector2 shift=anchor-lastTrailAnchor;
            if(SoulTrailMotion.IsOrdinaryShift(shift))SoulTrailMotion.Translate(trailCenters,trailPoints,trailCount,shift);
            else trailCount=0;
        }
        lastTrailAnchor=anchor;hasTrailAnchor=true;
        float elapsed = ++age - Projectile.ai[2];
        if (elapsed < 0) { Projectile.Center = player.Center; Projectile.hide = true; return; }
        Projectile.hide = false;
        previousCenter=Projectile.Center;
        if(retired)retireAge=Math.Min(8,retireAge+1);
        Vector2 velocity = TargetOffset * .5f;
        float distanceFactor=Utils.GetLerpValue(900,0,velocity.Length()*2,true);
        float step=MathHelper.Lerp(.7f,2f,distanceFactor);
        Projectile.localAI[0]+=step;
        float phase=MotionPhase;
        if(phase>=60 && !retired)Blessings.CompleteReturn(Projectile);
        if(phase>=120){Projectile.Kill();return;}
        // 慢速宿主也保留原版尾迹寿命，沿用原版返回动作与剑体尺寸。
        Projectile.timeLeft=Math.Max(Projectile.timeLeft,2+(int)Math.Ceiling((120-phase)/step));
        float lerp = Utils.GetLerpValue(0,1,phase/60f,true);
        float num3 = Vertical;
        float aim = velocity.ToRotation();
        float dir = velocity.X > 0f ? 1f : -1f;
        float orbit = MathHelper.Pi + dir * lerp * MathHelper.TwoPi;
        float radius = Math.Max(60f, velocity.Length() +
            MathHelper.Lerp(0f, 40f, Utils.GetLerpValue(.5f, 1f, lerp, true)));
        Vector2 target = player.MountedCenter + velocity;
        Vector2 spinning = orbit.ToRotationVector2() * new Vector2(radius, num3 * MathHelper.Lerp(2f, 1f, distanceFactor));
        Vector2 small = (1f - Utils.GetLerpValue(0f, .5f, lerp, true)) *
            new Vector2(dir * -radius * .1f, -num3 * .3f);
        Projectile.Center = target + spinning.RotatedBy(aim) + small;
        var sourceSoul=SourceSoul;
        if(sourceSoul!=null && !retired && phase<60)
        {
            if(terrainArmed)Blessings.CrossedSolid(Projectile,previousCenter,Projectile.Center);
            if(terrainArmed && SoulTerrain.Sweep(previousCenter,Projectile.Center,SoulCurses.TerrainAllowance(sourceSoul),ref terrainDepth))Retire();
            if(!SoulTerrain.SolidAt(Projectile.Center) && phase>=6)terrainArmed=true;
        }
        Projectile.rotation = orbit + aim + MathHelper.PiOver4;
        Projectile.direction = Projectile.spriteDirection = (int)dir;
        if (num3 < 0f)
        {
            Projectile.rotation = MathHelper.Pi + dir * lerp * -MathHelper.TwoPi + aim + MathHelper.PiOver4;
            Projectile.direction = Projectile.spriteDirection = -(int)dir;
        }
        Projectile.Opacity=Utils.GetLerpValue(0,5,phase,true)*Utils.GetLerpValue(120,115,phase,true);
        Projectile.scale = Math.Clamp(SwordScale, .6f, 2f);
        if(!Main.dedServ)
        {
            if(Projectile.localAI[1]==0){Projectile.localAI[1]=1;SoundEngine.PlaySound(SoundID.Item169,Projectile.Center);}
            // 沿旧刃缘少量脱落碎光，使用共用粒子入口；每四次更新最多一粒。
            // 普通返回由带面本身侵蚀收尾；提前收回才补少量碎光，保留原版飞行粒子。
            if(retired && retireAge<8 && age%4==0 && trailCount>=4)
            {
                int itemType=(int)Projectile.ai[0];
                Color tint=SoulVisuals.Tint(itemType);
                int idx=Main.rand.Next(2,Math.Min(trailCount,SoulMaterialTrail.HistoryLength));
                Vector2 axis=(trailRotations[idx]-MathHelper.PiOver4).ToRotationVector2();
                Vector2 at=trailCenters[idx]+axis*26*DisplayScale;
                Vector2 drift=Vector2.Clamp(trailCenters[idx-1]-trailCenters[idx],new Vector2(-8),new Vector2(8))*.16f;
                if(VfxIsolation.FlowSpark)
                    SoulFlowSpark.Spawn(new FlowSparkSpawn(at,drift+new Vector2(0,-.6f),tint,.55f,18,true));
            }
        }
        // 服务器同样保留实际姿态历史，剑影碰撞不能依赖客户端绘制或显卡资源。
        int historyLimit=HistoryLength;
        if(trailCount>0 && Vector2.DistanceSquared(Projectile.Center,trailCenters[0])>90000) trailCount=0;
        for(int i=Math.Min(trailCount,historyLimit-1);i>0;i--)
        { trailPoints[i]=trailPoints[i-1];trailCenters[i]=trailCenters[i-1];trailRotations[i]=trailRotations[i-1];trailPhases[i]=trailPhases[i-1];trailDirections[i]=trailDirections[i-1]; }
        trailCenters[0]=Projectile.Center;trailRotations[0]=Projectile.rotation;
        trailPhases[0]=phase;
        trailDirections[0]=Projectile.spriteDirection;
        trailPoints[0]=Projectile.Center+(Projectile.rotation-MathHelper.PiOver4).ToRotationVector2()*26*DisplayScale;
        trailCount=Math.Min(historyLimit,trailCount+1);
        if(!Main.dedServ)
        {
            CaptureTrailEndings();
            if(!retired&&VfxIsolation.FlyingParticles)SoulZenithEffects.EmitFlying(this,player);
        }
        if (!released && !retired && phase >= 24)
        {
            released = true;
            if (Projectile.owner == Main.myPlayer) ReleaseSpecial(player);
        }
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        =>ResolveHitUnit(targetHitbox)>=0;
    // 重叠时优先本体，其次最近的剑影；一次原生命中只归属一个单元。
    public int ResolveHitUnit(Rectangle targetHitbox)
    {
        if(CanDamage()==false)return -1;
        bool solid=SolidCollision();
        if(BodyCanDamage&&PoseCollides(Projectile.Center,Projectile.rotation,targetHitbox,solid))return 0;
        for(int echo=1;echo<SoulTrailUnits.Count;echo++)
            if(TryGetEchoPose(echo,out var center,out float rotation,out _)&&PoseCollides(center,rotation,targetHitbox,solid))return echo;
        return -1;
    }
    public bool BodyCollides(Rectangle targetHitbox)
        =>BodyCanDamage&&PoseCollides(Projectile.Center,Projectile.rotation,targetHitbox,SolidCollision());
    private bool SolidCollision()
    {
        var soul = WoodCombat.FindSoul(Main.player[Projectile.owner], Projectile.GetGlobalProjectile<SoulSource>().SoulId);
        return soul!=null && (soul.TraitEnabled((int)Projectile.ai[0]) && WeaponFamilies.Of((int)Projectile.ai[0]) == BladeFamily.Solid ||
            soul.TraitSupports.Any(i=>WeaponFamilies.Of(i.type)==BladeFamily.Solid));
    }
    private bool PoseCollides(Vector2 center,float rotation,Rectangle targetHitbox,bool solid)
    {
        float point=0;
        Vector2 length=new Vector2((Reborn?40:26)*Projectile.scale,0).RotatedBy(rotation-MathHelper.PiOver4);
        // 短刃完全位于大目标内部时线段助手可能漏判；包含剑体中心也视为命中。
        return targetHitbox.Contains(center.ToPoint()) || Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            center - length, center + length, 20 * Projectile.scale *
            (solid?1.15f:1), ref point);
    }
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        var soul=SourceSoul;
        long key=WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id;
        if(damageDone>0)targetHits[key]=targetHits.GetValueOrDefault(key)+1;
        var family=soul==null || soul.TraitEnabled((int)Projectile.ai[0])?WeaponFamilies.Of((int)Projectile.ai[0]):BladeFamily.Plain;
        if(family==BladeFamily.Frost) target.AddBuff(BuffID.Frostburn,(int)(120*Blessings.TraitPower));
        else if(family==BladeFamily.Nature) target.AddBuff(BuffID.Poisoned,(int)(180*Blessings.TraitPower));
        if (soul != null)
        {
            if (soul.TraitSupports.Any(i => WeaponFamilies.Of(i.type) == BladeFamily.Frost)) target.AddBuff(BuffID.Frostburn,(int)(120*Blessings.TraitPower));
            if (soul.TraitSupports.Any(i => WeaponFamilies.Of(i.type) == BladeFamily.Nature)) target.AddBuff(BuffID.Poisoned,(int)(180*Blessings.TraitPower));
        }
        WoodCombat.FinishPrimaryHit(Projectile, target, damageDone);
        Main.player[Projectile.owner].GetModPlayer<HardOrePlayer>().Hit(Projectile, target, damageDone);
        OreFamily.OnMainHit(Projectile, target, damageDone);
        HolyHeavenProc.OnMainHit(Projectile, target, damageDone);
        Blessings.Hit(Projectile,target,damageDone);
    }
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        // 在击退移动目标前保存实际碰撞单元，伤害与命中后的矿石积攒读取同一来源。
        HitUnit=Math.Max(0,ResolveHitUnit(target.Hitbox));
        modifiers.FinalDamage*=SoulEchoDamage.Multiplier(SourceSoul,HitUnit);
        Blessings.Prepare(Projectile,target);
        WoodCombat.PreparePrimaryHit(Projectile, target);
        WoodCombat.ModifyPrimaryHit(Projectile, target, ref modifiers);
        Main.player[Projectile.owner].GetModPlayer<HardOrePlayer>().Modify(Projectile, target, ref modifiers);
        var soul = WoodCombat.FindSoul(Main.player[Projectile.owner], Projectile.GetGlobalProjectile<SoulSource>().SoulId);
        if (soul!=null && (soul.TraitEnabled((int)Projectile.ai[0]) && (int)Projectile.ai[0] == ItemID.CopperShortsword || soul.TraitSupports.Any(i => i.type == ItemID.CopperShortsword)) &&
            Vector2.Distance(Main.player[Projectile.owner].Center, target.Center) < 180)
            modifiers.FinalDamage *= 1.2f;
    }
    private void ReleaseSpecial(Player player)
    {
        ReleaseWeaponSpecial(player, (int)Projectile.ai[0]);
        var soul = WoodCombat.FindSoul(player, Projectile.GetGlobalProjectile<SoulSource>().SoulId);
        if (soul == null) return;
        var woodPlayer = player.GetModPlayer<WoodPlayer>();
        foreach (var support in soul.TraitSupports)
            if (!WoodTypes.IsWood(support.type) && SoulProgress.ZenithWeapons.Contains(support.type) &&
                woodPlayer.Ready(soul.SoulId + ":special:" + support.type, 45))
                ReleaseWeaponSpecial(player, support.type);
    }
    private void ReleaseWeaponSpecial(Player player, int type)
    {
        var soul=SourceSoul;
        if(soul!=null && !soul.TraitEnabled(type))return;
        bool arcane=WeaponFamilies.Of(type)==BladeFamily.Arcane;
        if(arcane)
        {
            string soulId = Projectile.GetGlobalProjectile<SoulSource>().SoulId;
            if(!player.GetModPlayer<SoulPlayer>().TryArcane(soulId,type)) return;
        }
        if (!SoulProgress.ZenithWeapons.Contains(type) || type == ItemID.CopperShortsword) return;
        Item template = new(type);
        int shot = template.shoot;
        if (shot <= 0) return;
        Vector2 toward = TargetOffset.SafeNormalize(Vector2.UnitX * player.direction);
        Vector2 origin = Projectile.Center;
        Vector2 velocity = toward * Math.Max(8, template.shootSpeed);
        int count = 1;
        float power = .5f;
        if (type is ItemID.Starfury or ItemID.StarWrath)
        {
            origin = player.Center + TargetOffset + new Vector2(Main.rand.Next(-100,101), -400);
            velocity = (player.Center + TargetOffset - origin).SafeNormalize(Vector2.UnitY) * 16;
            count = type == ItemID.StarWrath ? 3 : 1;
            power = .4f;
        }
        if (type == ItemID.BeeKeeper) { shot = ProjectileID.Bee; count = 3; power = .2f; }
        if (type == ItemID.Seedler) { shot = ProjectileID.SeedlerNut; velocity = toward * 10; }
        if (type == ItemID.TheHorsemansBlade) { shot = ProjectileID.FlamingJack; velocity = toward * 10; }
        if(arcane)
        {
            power=.3f;
            if(type==ItemID.Starfury) origin=player.Center+TargetOffset+new Vector2(0,-220);
        }
        for (int i = 0; i < count; i++)
        {
            int index = Projectile.NewProjectile(Projectile.GetSource_FromThis(), origin,
                velocity.RotatedBy((i - (count - 1) * .5f) * .12f), shot,
                Math.Max(1, (int)((SpecialDamage > 0 ? SpecialDamage : Projectile.damage) * power)), Projectile.knockBack * .5f, Projectile.owner);
            if (index >= Main.maxProjectiles) continue;
            Main.projectile[index].DamageType = DamageClass.Melee;
            Main.projectile[index].CritChance = Projectile.CritChance;
            if(arcane)
            {
                Main.projectile[index].timeLeft=Math.Min(Main.projectile[index].timeLeft,30);
                Main.projectile[index].GetGlobalProjectile<SoulSource>().LeashRadius=400;
            }
            Main.projectile[index].netUpdate = true;
        }
    }
    public int BuildRibbon(VertexPositionColor[] vertices)
    {
        if(Projectile.hide || trailCount<2) return 0;
        int itemType=(int)Projectile.ai[0];
        if(itemType<=0 || itemType>=ItemLoader.ItemCount) return 0;
        Main.instance.LoadItem(itemType);
        Texture2D texture=TextureAssets.Item[itemType].Value;
        Rectangle frame=Main.itemAnimations[itemType]?.GetFrame(texture)??texture.Frame();
        float halfLength=frame.Size().Length()*.5f*DisplayScale;
        return SoulRibbonMesh.Build(trailCenters,trailRotations,Math.Min(trailCount,TrailLength),halfLength,
            SoulVisuals.Tint(itemType),Projectile.Opacity*(retired?1-retireAge/8f:1),Main.screenPosition,vertices);
    }
    // 按实际记录的剑身位置和朝向铺材质；不再由玩家中心重新猜一条圆弧。
    private void CaptureTrailEndings(bool force=false)
    {
        if(Projectile.hide||trailCount<2||Main.dedServ||Main.gameMenu||Main.graphics?.GraphicsDevice==null)return;
        int itemType=(int)Projectile.ai[0];
        if(itemType<=0||itemType>=ItemLoader.ItemCount)return;
        Main.instance.LoadItem(itemType);var texture=TextureAssets.Item[itemType].Value;
        var frame=Main.itemAnimations[itemType]?.GetFrame(texture)??texture.Frame();
        float halfLength=frame.Size().Length()*.5f*DisplayScale;
        for(int i=0;i<SoulTrailUnits.Count;i++)
        {
            var unit=SoulTrailUnits.At(i,trailCount,MotionPhase);
            float localPhase=MotionPhase-unit.Start;
            if(trailEndings[i]!=null)
            {
                if(!retired&&localPhase<68)trailEndings[i].Follow(trailCenters,trailRotations,unit.Start);
                continue;
            }
            if(!force&&!retired&&localPhase<60)continue;
            float opacity=Projectile.Opacity*unit.Opacity*(retired?1-retireAge/8f:1)*.86f;
            trailEndings[i]=SoulTrailEnding.CaptureUnit(trailCenters,trailRotations,trailCount,unit,halfLength,
                itemType,SoulVisuals.Tint(itemType),opacity,retired);
        }
    }
    public override void OnKill(int timeLeft)=>CaptureTrailEndings(true);

    private int FindTrailSample(float samplePhase)
    {
        if(samplePhase<0 || trailCount<=0)return -1;
        int i=0;
        while(i+1<trailCount && Math.Abs(trailPhases[i+1]-samplePhase)<Math.Abs(trailPhases[i]-samplePhase))i++;
        return i;
    }
    public void DrawRibbonShader(VertexPositionColorTexture[] vertices)
    {
        if(Projectile.hide || trailCount<2 || Main.dedServ)return;
        int itemType=(int)Projectile.ai[0];
        if(itemType<=0 || itemType>=ItemLoader.ItemCount)return;
        float visibility=Projectile.Opacity*(retired?1-retireAge/8f:1);
        if(visibility<=0)return;
        Main.instance.LoadItem(itemType);
        Texture2D texture=TextureAssets.Item[itemType].Value;
        Rectangle frame=Main.itemAnimations[itemType]?.GetFrame(texture)??texture.Frame();
        float halfLength=frame.Size().Length()*.5f*DisplayScale;
        float clock=Main.GlobalTimeWrappedHourly;
        // 剑影仍取十五更新间隔的原位置；每条短尾只使用自己的窗口，绝不补画窗口之间的三角形。
        Span<SoulMaterialTrail.TrailLayer> layers=stackalloc SoulMaterialTrail.TrailLayer[SoulTrailUnits.Count];
        int used=0;
        for(int i=SoulTrailUnits.Count-1;i>=0;i--)
        {
            var unit=SoulTrailUnits.At(i,trailCount,MotionPhase);
            if(trailEndings[i]==null&&unit.Samples>=2&&unit.Opacity>0)layers[used++]=unit.Layer;
        }
        if(used==0)return;
        int count=SoulMaterialTrail.BuildLayered(trailCenters,trailRotations,trailCount,halfLength,
            Main.screenPosition,vertices,layers[..used],clock);
        if(count<3)return;
        SoulMaterialTrail.Draw(vertices,count,SoulVisuals.Tint(itemType),visibility*.86f,clock,
            itemType:itemType);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (Projectile.hide) return false;
        int itemType = (int)Projectile.ai[0];
        if (itemType <= 0 || itemType >= ItemLoader.ItemCount) return false;
        Main.instance.LoadItem(itemType);
        Texture2D texture = TextureAssets.Item[itemType].Value;
        Rectangle frame = Main.itemAnimations[itemType]?.GetFrame(texture) ?? texture.Frame();
        Vector2 origin = frame.Size() * .5f;
        Color tint=SoulVisuals.Tint(itemType);
        float visibility=BladeOpacity;
        // 原版 DrawProjDirect 的天顶分支就是：for(j = 60; j > 0; j -= 15)
        // → 只画 oldPos[45] / [30] / [15] 三个剑影（j=60 越界被 continue 掉）。
        // 15 这个间隔在 FinalFractalHelper.FramesPerImportantTrail 里写死。
        // 亮度 = (60 - j) / 60，即近 0.75、中 0.50、远 0.25，越靠后越暗。
        // 可见性按 phase - j 判定，所以三个剑影是依次点亮、再依次熄灭，不会同时全亮。
        if(VfxIsolation.EchoBlades)
        for(int j=(SoulTrailUnits.Count-1)*SoulTrailUnits.Spacing;j>0;j-=SoulTrailUnits.Spacing)
        {
            if(!TryGetEchoPose(j/SoulTrailUnits.Spacing,out var center,out float rotation,out float opacity))continue;
            // 三个位置各自独立开关，可只留近位/中位/远位其中一个
            if(!VfxIsolation.EchoSlotEnabled(j/SoulTrailUnits.Spacing))continue;
            // 亮度阶梯可按需摘除，统一满亮度便于单独观察某个位置的形状
            if(!VfxIsolation.EchoBladeAlpha)opacity=1f;
            int direction=trailDirections[j]==0?Projectile.spriteDirection:trailDirections[j];
            Color echoColor=VfxIsolation.EchoTint?Color.Lerp(tint,Color.White,.22f):tint;
            Main.EntitySpriteDraw(texture,center-Main.screenPosition,frame,
                echoColor*opacity,
                rotation+MathHelper.PiOver4*(1-direction),origin,DisplayScale,
                direction==1?SpriteEffects.None:SpriteEffects.FlipHorizontally);
        }
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, WoodTypes.SwordTint(itemType)*visibility,
            DrawRotation, origin, DisplayScale, DrawEffects);
        if(VfxIsolation.TipGlow)SoulZenithEffects.DrawTip(this);
        return false;
    }
}
