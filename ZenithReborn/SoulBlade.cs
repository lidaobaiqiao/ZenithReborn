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
    public bool MartialCharged;
    public override void OnHitNPC(Projectile p,NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(string.IsNullOrEmpty(SoulId) || damageDone<=0 || p.owner<0 || p.owner>=Main.maxPlayers)return;
        Main.player[p.owner].GetModPlayer<MartialPlayer>().PrimaryHit(p,target,damageDone);
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
    private bool trailEndingCaptured;
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
    private bool oreHitConsumed;
    private bool retired,terrainArmed;
    private int retireAge;
    private float terrainDepth;
    private Vector2 previousCenter;
    private Dictionary<long,int> targetHits=[];
    private SoulItem SourceSoul=>Projectile.owner>=0 && Projectile.owner<Main.maxPlayers?
        WoodCombat.FindSoul(Main.player[Projectile.owner],Projectile.GetGlobalProjectile<SoulSource>().SoulId):null;
    public bool Retired=>retired;
    public void Retire(){if(retired)return;retired=true;retireAge=0;Projectile.netUpdate=true;}
    // 每把主飞剑只消耗一次首击资格；先消耗再查冷却，受阻首击不能随后重试。
    public bool TryConsumeOreHit()
    {
        if (oreHitConsumed) return false;
        oreHitConsumed = true;
        return true;
    }
    private Vector2[] trailPoints;
    private Vector2[] trailCenters;
    private float[] trailRotations;
    private float[] trailPhases;
    private const int TrailLength = SoulRibbonMesh.HistoryLength;
    private const int HistoryLength = 96;
    private int trailCount;
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
        oreHitConsumed = false;
        Blessings=new();
        Vertical = 60f;
        trailPoints=new Vector2[HistoryLength];trailCenters=new Vector2[HistoryLength];trailRotations=new float[HistoryLength];trailPhases=new float[HistoryLength];
        trailCount=0;
        trailEndingCaptured=false;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => retired || MotionPhase>=60 || age <= Projectile.ai[2] ? false : null;
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
            if(retired||phase>=60)CaptureTrailEnding();
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
                SoulFlowSpark.Spawn(new FlowSparkSpawn(at,drift+new Vector2(0,-.6f),tint,.55f,18,true));
            }
            int historyLimit=HistoryLength;
            if(trailCount>0 && Vector2.DistanceSquared(Projectile.Center,trailCenters[0])>90000) trailCount=0;
            for(int i=Math.Min(trailCount,historyLimit-1);i>0;i--)
            { trailPoints[i]=trailPoints[i-1];trailCenters[i]=trailCenters[i-1];trailRotations[i]=trailRotations[i-1];trailPhases[i]=trailPhases[i-1]; }
            trailCenters[0]=Projectile.Center;trailRotations[0]=Projectile.rotation;
            trailPhases[0]=phase;
            trailPoints[0]=Projectile.Center+(Projectile.rotation-MathHelper.PiOver4).ToRotationVector2()*26*DisplayScale;
            trailCount=Math.Min(historyLimit,trailCount+1);
            if(!retired)SoulZenithEffects.EmitFlying(this,player);
        }
        if (!released && !retired && phase >= 24)
        {
            released = true;
            if (Projectile.owner == Main.myPlayer) ReleaseSpecial(player);
        }
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float point = 0;
        Vector2 length = new Vector2((Reborn ? 40 : 26) * Projectile.scale, 0).RotatedBy(Projectile.rotation - MathHelper.PiOver4);
        var soul = WoodCombat.FindSoul(Main.player[Projectile.owner], Projectile.GetGlobalProjectile<SoulSource>().SoulId);
        bool solid = soul!=null && (soul.TraitEnabled((int)Projectile.ai[0]) && WeaponFamilies.Of((int)Projectile.ai[0]) == BladeFamily.Solid ||
            soul.TraitSupports.Any(i=>WeaponFamilies.Of(i.type)==BladeFamily.Solid));
        // 短刃完全位于大目标内部时线段助手可能漏判；包含剑体中心也视为命中。
        return targetHitbox.Contains(Projectile.Center.ToPoint()) || Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - length, Projectile.Center + length, 20 * Projectile.scale *
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
        Blessings.Hit(Projectile,target,damageDone);
    }
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
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
    private void CaptureTrailEnding()
    {
        if(trailEndingCaptured||Projectile.hide||trailCount<2||Main.dedServ||Main.gameMenu||Main.graphics?.GraphicsDevice==null)return;
        int itemType=(int)Projectile.ai[0];
        if(itemType<=0||itemType>=ItemLoader.ItemCount)return;
        Main.instance.LoadItem(itemType);var texture=TextureAssets.Item[itemType].Value;
        var frame=Main.itemAnimations[itemType]?.GetFrame(texture)??texture.Frame();
        float formation=MathHelper.SmoothStep(0,1,Math.Clamp(MotionPhase/12f,0,1));
        trailEndingCaptured=SoulTrailEnding.Capture(trailCenters,trailRotations,trailCount,frame.Size().Length()*.5f*DisplayScale,
            itemType,SoulVisuals.Tint(itemType),Projectile.Opacity*formation*.86f,retired);
    }
    public override void OnKill(int timeLeft)=>CaptureTrailEnding();

    public void DrawRibbonShader(VertexPositionColorTexture[] vertices)
    {
        if(Projectile.hide || trailCount<2 || Main.dedServ||trailEndingCaptured)return;
        int itemType=(int)Projectile.ai[0];
        if(itemType<=0 || itemType>=ItemLoader.ItemCount)return;
        float visibility=Projectile.Opacity*SoulMaterialTrail.Visibility(MotionPhase,retired,retireAge);
        if(visibility<=0)return;
        Main.instance.LoadItem(itemType);
        Texture2D texture=TextureAssets.Item[itemType].Value;
        Rectangle frame=Main.itemAnimations[itemType]?.GetFrame(texture)??texture.Frame();
        float halfLength=frame.Size().Length()*.5f*DisplayScale;
        float clock=Main.GlobalTimeWrappedHourly;
        // 单条带面，尾部亮度和浓度交给着色器按 s 坐标衰减，
        // 分段拼接会在每段起点重新"渐入"，拼出来是锯齿。
        int count=SoulMaterialTrail.Build(trailCenters,trailRotations,trailCount,halfLength,
            Main.screenPosition,vertices,clock);
        if(count<3)return;
        SoulMaterialTrail.Draw(vertices,count,SoulVisuals.Tint(itemType),visibility*.86f,clock,
            dissolve:SoulMaterialTrail.Dissolve(MotionPhase,retired,retireAge),itemType:itemType);
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
        // 原版933每隔十五阶段单位绘制一份剑影，各自随年龄消散，形成原版返回尾迹。
        for(int behind=60;behind>=15;behind-=15)
        {
            float samplePhase=MotionPhase-behind;
            if(samplePhase<0)continue;
            int i=0;
            while(i+1<trailCount && Math.Abs(trailPhases[i+1]-samplePhase)<Math.Abs(trailPhases[i]-samplePhase))i++;
            if(i>=trailCount)continue;
            float opacity=SpriteVisibility(trailPhases[i])*Projectile.Opacity*(1-behind/75f)*(retired?1-retireAge/8f:1);
            Main.EntitySpriteDraw(texture,trailCenters[i]-Main.screenPosition,frame,
                Color.Lerp(tint,Color.White,.22f)*(opacity*.5f),
                trailRotations[i]+MathHelper.PiOver4*(1-Projectile.spriteDirection),origin,DisplayScale,DrawEffects);
        }
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, WoodTypes.SwordTint(itemType)*visibility,
            DrawRotation, origin, DisplayScale, DrawEffects);
        SoulZenithEffects.DrawTip(this);
        return false;
    }
}
