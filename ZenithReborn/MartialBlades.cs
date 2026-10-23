using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public static class MartialVisual
{
    public static readonly Color Cyan=new(102,223,246),Violet=new(192,139,249);
    public static void Line(Vector2 a,Vector2 b,Color color,float width)=>EnvironmentEffect.Line(Main.spriteBatch,a-Main.screenPosition,b-Main.screenPosition,color,width);
    public static void Spark(Vector2 at,Vector2 velocity,Color color,float scale=.8f)
    {
        if(Main.dedServ)return;
        var d=Dust.NewDustPerfect(at,DustID.TintableDustLighted,velocity,110,color,scale);d.noGravity=true;
    }
    public static void Sword(int type,Vector2 center,float rotation,float size,Color tint,float opacity)
    {
        if(Main.dedServ || type<=0 || type>=TextureAssets.Item.Length)return;
        Main.instance.LoadItem(type);var texture=TextureAssets.Item[type].Value;
        float scale=size/Math.Max(texture.Width,texture.Height);
        var origin=texture.Size()/2;var at=center-Main.screenPosition;
        Main.EntitySpriteDraw(texture,at,null,tint*(opacity*.25f),rotation,origin,scale*1.13f,SpriteEffects.None);
        Main.EntitySpriteDraw(texture,at,null,Color.White*opacity,rotation,origin,scale,SpriteEffects.None);
        Vector2 axis=(rotation-MathHelper.PiOver4).ToRotationVector2();
        Line(center-axis*size*.2f,center+axis*size*.45f,tint*(opacity*.35f),2);
    }
    public static void Bind(Projectile q,SoulItem soul,Item weapon,int slot,Player owner)
    {
        var source=q.GetGlobalProjectile<SoulSource>();source.SoulId=soul.SoulId;source.SourceWeapon=weapon.type;source.HostSlot=slot;
        source.PrimaryOrdinal=Array.FindIndex(soul.Active.ToArray(),w=>ReferenceEquals(w,weapon));
        q.CritChance=owner.GetWeaponCrit(weapon);q.netUpdate=true;
    }
}

// ai: weapon, state (orbit/thrust/embedded/flung), age. Embedded blades stay
// visible and feed a real owned DoT instead of vanishing on contact.
public class MartialBlade : ModProjectile
{
    public override string Texture=>"Terraria/Images/Item_"+ItemID.Zenith;
    public int State=>(int)Projectile.ai[1];
    public int Age=>(int)Projectile.ai[2];
    public int VisualType=>(int)Projectile.ai[0];
    private int order,count=3,target=-1,anchor=-1;
    private long anchorId;
    private Vector2 aim,offset;
    private readonly Dictionary<long,int> hits=[];
    private readonly List<Vector2> trail=[];
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=30;Projectile.friendly=true;Projectile.DamageType=DamageClass.Melee;
        Projectile.penetrate=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.timeLeft=660;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=12;
    }
    public override bool ShouldUpdatePosition()=>false;
    public static int Spawn(Player p,SoulItem soul,Item weapon,int slot,int order,int count,NPC target,Vector2 aim)
    {
        int index=Projectile.NewProjectile(p.GetSource_ItemUse(soul.Item),p.Center,Vector2.Zero,
            ModContent.ProjectileType<MartialBlade>(),soul.BladeDamage(p,weapon),p.GetWeaponKnockback(weapon),p.whoAmI,weapon.type);
        if(index>=Main.maxProjectiles)return index;
        var q=Main.projectile[index];var blade=(MartialBlade)q.ModProjectile;
        blade.order=order;blade.count=count;blade.target=target?.whoAmI??-1;blade.aim=aim;
        MartialVisual.Bind(q,soul,weapon,slot,p);return index;
    }
    public override void SendExtraAI(BinaryWriter w)
    {w.Write(order);w.Write(count);w.Write(target);w.Write(anchor);w.Write(aim.X);w.Write(aim.Y);w.Write(offset.X);w.Write(offset.Y);}
    public override void ReceiveExtraAI(BinaryReader r)
    {
        order=Math.Clamp(r.ReadInt32(),0,5);count=Math.Clamp(r.ReadInt32(),1,6);target=r.ReadInt32();anchor=r.ReadInt32();
        aim=new(r.ReadSingle(),r.ReadSingle());offset=new(r.ReadSingle(),r.ReadSingle());
        if(anchor>=0 && anchor<Main.maxNPCs && Main.npc[anchor].active)anchorId=WoodCombat.Root(Main.npc[anchor]).GetGlobalNPC<HardOreTarget>().Id;
    }
    private NPC Target=>target>=0 && target<Main.maxNPCs && MartialTuning.Target(Main.npc[target])?Main.npc[target]:null;
    public bool PinnedTo(NPC root)=>State==2 && anchor>=0 && anchor<Main.maxNPCs && Main.npc[anchor].active &&
        WoodCombat.Root(Main.npc[anchor])==root && root.GetGlobalNPC<HardOreTarget>().Id==anchorId;
    public void Fling(Vector2 direction)
    {
        if(State!=2)return;
        Projectile.ai[1]=3;Projectile.ai[2]=0;Projectile.velocity=direction.SafeNormalize(Vector2.UnitX)*22;
        Projectile.damage=Math.Max(1,(int)(Projectile.damage*1.5f));Projectile.timeLeft=65;hits.Clear();Projectile.netUpdate=true;
    }
    public override bool? CanDamage()=>State is 1 or 3?null:false;
    public override bool? CanHitNPC(NPC n)
    {
        if(!MartialTuning.Target(n))return false;
        long root=WoodCombat.Root(n).GetGlobalNPC<HardOreTarget>().Id;
        return hits.TryGetValue(root,out int tick) && Age-tick<12?false:null;
    }
    public override bool? Colliding(Rectangle own,Rectangle box)
    {
        float collision=0;Vector2 axis=(Projectile.rotation-MathHelper.PiOver4).ToRotationVector2()*34;
        return box.Contains(Projectile.Center.ToPoint()) || Collision.CheckAABBvLineCollision(box.TopLeft(),box.Size(),Projectile.Center-axis,Projectile.Center+axis,17,ref collision);
    }
    public override void OnHitNPC(NPC npc,NPC.HitInfo hit,int damageDone)
    {
        if(damageDone<=0)return;
        var root=WoodCombat.Root(npc);long id=root.GetGlobalNPC<HardOreTarget>().Id;hits[id]=Age;
        if(State!=1)return;
        anchor=npc.whoAmI;anchorId=id;offset=Projectile.Center-npc.Center;
        if(offset.Length()>70)offset=offset.SafeNormalize(Vector2.UnitX)*70;
        Projectile.ai[1]=2;Projectile.ai[2]=0;Projectile.velocity=Vector2.Zero;Projectile.timeLeft=360;
        root.AddBuff(ModContent.BuffType<MartialImpaled>(),360);Projectile.netUpdate=true;
        var source=Projectile.GetGlobalProjectile<SoulSource>();
        var pins=Main.projectile.Where(q=>q.active && q.owner==Projectile.owner && q.ModProjectile is MartialBlade b && b.State==2 &&
            q.GetGlobalProjectile<SoulSource>().SoulId==source.SoulId).OrderByDescending(q=>q.ai[2]).ToArray();
        for(int i=0;i<pins.Length-MartialTuning.MaxPins;i++)pins[i].Kill();
        for(int i=0;i<6;i++)MartialVisual.Spark(Projectile.Center,Main.rand.NextVector2Circular(2,2),MartialVisual.Cyan);
    }
    public override void AI()
    {
        if(Projectile.owner<0 || Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        var p=Main.player[Projectile.owner];var source=Projectile.GetGlobalProjectile<SoulSource>();
        if(!p.active || p.dead || p.HeldItem.ModItem is not SoulItem s || s.SoulId!=source.SoulId || !MartialTuning.Enabled)
        {Projectile.Kill();return;}
        Projectile.ai[2]++;int age=Age;
        if(State==0)
        {
            float t=Math.Clamp(age/(float)(MartialTuning.OrbitFrames+order*3),0,1);
            float turn=MathHelper.TwoPi*(1-t)+MathHelper.TwoPi*order/count;
            Vector2 center=p.Center+new Vector2(-p.direction*82,-28);
            Projectile.Center=center+new Vector2(MathF.Cos(turn)*30,MathF.Sin(turn)*52);
            Vector2 direction=((Target?.Center??aim)-Projectile.Center).SafeNormalize(Vector2.UnitX*p.direction);
            Projectile.rotation=direction.ToRotation()+MathHelper.PiOver4+(1-t)*MathHelper.TwoPi;
            Projectile.scale=.6f+MathF.Sin(t*MathHelper.Pi)*.6f+t*.6f;
            if(t>=1)
            {Projectile.ai[1]=1;Projectile.ai[2]=0;Projectile.velocity=direction*18;Projectile.netUpdate=true;
                if(!Main.dedServ)SoundEngine.PlaySound(SoundID.Item1 with {Volume=.35f},Projectile.Center);}
        }
        else if(State==1 || State==3)
        {
            if(State==1 && Target!=null && age<25)
                Projectile.velocity=Vector2.Lerp(Projectile.velocity,(Target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*18,.045f);
            Projectile.Center+=Projectile.velocity;Projectile.rotation=Projectile.velocity.ToRotation()+MathHelper.PiOver4;Projectile.scale=1.2f;
            if(age>(State==1?75:50))Projectile.Kill();
        }
        else if(State==2)
        {
            if(anchor<0 || anchor>=Main.maxNPCs || !MartialTuning.Target(Main.npc[anchor]) || !PinnedTo(WoodCombat.Root(Main.npc[anchor])))
            {Projectile.Kill();return;}
            Projectile.Center=Main.npc[anchor].Center+offset;Projectile.scale=1.15f;
            if(age>=330){Projectile.Kill();return;}
        }
        if(Main.dedServ)return;
        if(State!=2){trail.Insert(0,Projectile.Center);if(trail.Count>10)trail.RemoveAt(trail.Count-1);}
        else trail.Clear();
        if(age%3==0)MartialVisual.Spark(Projectile.Center,State==2?new(0,-.4f):-Projectile.velocity*.08f,MartialVisual.Cyan,.65f);
        Lighting.AddLight(Projectile.Center,MartialVisual.Cyan.ToVector3()*.35f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Color tint=State==2?MartialVisual.Violet:MartialVisual.Cyan;
        for(int i=trail.Count-1;i>0;i--)MartialVisual.Line(trail[i],trail[i-1],tint*((1-i/(float)trail.Count)*.35f),Math.Max(1,7-i*.5f));
        float opacity=State==2?Math.Min(1,(330-Age)/20f):1;
        MartialVisual.Sword(VisualType,Projectile.Center,Projectile.rotation,66*Projectile.scale,tint,opacity);
        if(State==2)EnvironmentEffect.Ring(Main.spriteBatch,Projectile.Center-Main.screenPosition,8+MathF.Sin(Age*.12f)*2,tint*.3f,16);
        return false;
    }
}

// ai: tier, phase (ready/windup/dash/hold/slash/fade), age in phase.
public class MartialGiant : ModProjectile
{
    public override string Texture=>"Terraria/Images/Item_"+ItemID.Zenith;
    public int Tier=>(int)Projectile.ai[0];
    public int Phase=>(int)Projectile.ai[1];
    public int Age=>(int)Projectile.ai[2];
    private bool FormationPending(int maxState)=>Main.projectile.Any(q=>q.active && q.owner==Projectile.owner &&
        q.ModProjectile is MartialBlade b && b.State<=maxState && q.GetGlobalProjectile<SoulSource>().SoulId==Projectile.GetGlobalProjectile<SoulSource>().SoulId);
    public bool ReadyForCut=>Tier==3 && Phase==0 && Age>=MartialTuning.GiantReadyFrames && !FormationPending(1);
    public bool TimeStopped=>Tier==3 && Phase is >=1 and <=3;
    private Vector2 aim=Vector2.UnitX,dashStart,dashEnd;
    private int target=-1,clock;
    private bool flung,wave,readyNotified;
    private readonly Dictionary<long,int> hits=[];
    public MartialSlashTrail SlashTrail {get;}=new();
    private readonly SlashSparkEmitter slashSparks=new();
    private Player Owner=>Main.player[Projectile.owner];
    private bool Local=>Projectile.owner==Main.myPlayer && Main.netMode!=NetmodeID.Server;
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=40;Projectile.friendly=true;Projectile.DamageType=DamageClass.Melee;
        Projectile.penetrate=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.timeLeft=1800;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=12;
    }
    public override bool ShouldUpdatePosition()=>false;
    public static int Spawn(Player p,SoulItem soul,Item weapon,int slot,int tier,NPC target,Vector2 at)
    {
        int index=Projectile.NewProjectile(p.GetSource_ItemUse(soul.Item),p.Center,Vector2.Zero,ModContent.ProjectileType<MartialGiant>(),
            soul.BladeDamage(p,weapon),p.GetWeaponKnockback(weapon),p.whoAmI,tier);
        if(index>=Main.maxProjectiles)return -1;
        var q=Main.projectile[index];var g=(MartialGiant)q.ModProjectile;g.target=target?.whoAmI??-1;
        g.aim=((target?.Center??at)-p.Center).SafeNormalize(Vector2.UnitX*p.direction);
        MartialVisual.Bind(q,soul,weapon,slot,p);return q.identity;
    }
    public override void SendExtraAI(BinaryWriter w)
    {w.Write(target);w.Write(aim.X);w.Write(aim.Y);w.Write(dashStart.X);w.Write(dashStart.Y);w.Write(dashEnd.X);w.Write(dashEnd.Y);}
    public override void ReceiveExtraAI(BinaryReader r)
    {target=r.ReadInt32();aim=new(r.ReadSingle(),r.ReadSingle());dashStart=new(r.ReadSingle(),r.ReadSingle());dashEnd=new(r.ReadSingle(),r.ReadSingle());}
    private void Transition(int phase){Projectile.ai[1]=phase;Projectile.ai[2]=0;hits.Clear();Projectile.netUpdate=true;}
    public bool StartTimeCut(Vector2 at)
    {
        if(!ReadyForCut || !Local)return false;
        var n=MartialTuning.AimTarget(Owner,at);
        if(n==null){Owner.GetModPlayer<MartialPlayer>().Tell("瞄准附近敌人，再攻击发动凝时斩");return false;}
        target=n.whoAmI;aim=(n.Center-Owner.Center).SafeNormalize(Vector2.UnitX*Owner.direction);
        dashStart=Owner.Center;dashEnd=n.Center-aim*145;
        if(Vector2.DistanceSquared(dashStart,dashEnd)>MartialTuning.FreezeRadius*MartialTuning.FreezeRadius)return false;
        Transition(1);return true;
    }
    public override bool? CanDamage()=>Phase is 2 or 3 or 4?null:false;
    public override bool? CanHitNPC(NPC n)
    {
        if(!MartialTuning.Target(n))return false;
        long id=WoodCombat.Root(n).GetGlobalNPC<HardOreTarget>().Id;
        return hits.TryGetValue(id,out int last) && (Phase==4 || clock-last<12)?false:null;
    }
    public override bool? Colliding(Rectangle own,Rectangle box)
    {
        var axis=Projectile.rotation.ToRotationVector2();float point=0;
        return box.Contains(Owner.Center.ToPoint()) || Collision.CheckAABBvLineCollision(box.TopLeft(),box.Size(),Owner.Center,
            Owner.Center+axis*MartialTuning.GiantLength,Phase==4?65:38,ref point);
    }
    public override void ModifyHitNPC(NPC n,ref NPC.HitModifiers modifiers)
    {modifiers.FinalDamage*=Phase==4?(Tier==3?12:8):Phase==3?.8f:2;}
    public override void OnHitNPC(NPC n,NPC.HitInfo hit,int damageDone)
    {
        if(damageDone<=0)return;hits[WoodCombat.Root(n).GetGlobalNPC<HardOreTarget>().Id]=clock;
        if(Tier==3 && Phase==4){var root=WoodCombat.Root(n);root.GetGlobalNPC<MartialTarget>().Scar(root,Projectile);}
        for(int i=0;i<8;i++)MartialVisual.Spark(n.Center,Main.rand.NextVector2Circular(4,4),Tier==3?MartialVisual.Violet:MartialVisual.Cyan,1);
    }
    private void FlingPins()
    {
        if(flung || !Local)return;flung=true;
        var id=Projectile.GetGlobalProjectile<SoulSource>().SoulId;
        var pins=Main.projectile.Where(q=>q.active && q.owner==Projectile.owner && q.ModProjectile is MartialBlade b && b.State==2 &&
            q.GetGlobalProjectile<SoulSource>().SoulId==id).ToArray();
        for(int i=0;i<pins.Length;i++)((MartialBlade)pins[i].ModProjectile).Fling(aim.RotatedBy((i-(pins.Length-1)*.5f)*.045f));
    }
    public override void AI()
    {
        if(Projectile.owner<0 || Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        var s=Owner.HeldItem.ModItem as SoulItem;
        if(!Owner.active || Owner.dead || s?.SoulId!=Projectile.GetGlobalProjectile<SoulSource>().SoulId || !MartialTuning.Enabled)
        {Projectile.Kill();return;}
        clock++;Projectile.ai[2]++;int age=Age;float direction=aim.ToRotation();
        if(Phase==0)
        {
            // Summoning is a sequence, not a race between two independent AIs:
            // first release the orbiting weapons, then form the giant and wait
            // for the incoming swords to embed (or finish their missed flight).
            if(FormationPending(0)){Projectile.ai[2]=0;age=0;}
            Projectile.rotation=direction-.8f;Projectile.scale=Math.Min(1,age/30f);
            if(Tier==2 && age>=MartialTuning.GiantReadyFrames && !FormationPending(1))Transition(4);
            if(ReadyForCut && !readyNotified && Local){readyNotified=true;Owner.GetModPlayer<MartialPlayer>().Tell("巨剑就绪 · 瞄准敌人，攻击发动时停突进");}
        }
        else if(Phase==1)
        {
            Projectile.rotation=direction;Projectile.scale=1;
            if(Local){Owner.Center=dashStart;Owner.velocity=Vector2.Zero;}
            if(age>=10)Transition(2);
        }
        else if(Phase==2)
        {
            Projectile.rotation=direction;
            if(Local)
            {
                Vector2 desired=Vector2.Lerp(dashStart,dashEnd,MathHelper.SmoothStep(0,1,Math.Min(1,age/(float)MartialTuning.DashFrames)));
                Vector2 step=desired-Owner.Center;
                Owner.position+=Collision.TileCollision(Owner.position,step,Owner.width,Owner.height,true,true);
                Owner.velocity=Vector2.Zero;Owner.fallStart=(int)(Owner.position.Y/16);Owner.immune=true;Owner.immuneTime=Math.Max(Owner.immuneTime,4);
            }
            if(age>=MartialTuning.DashFrames){if(Local)dashEnd=Owner.Center;Transition(3);}
        }
        else if(Phase==3)
        {
            Projectile.rotation=direction;
            if(Local)
            {
                Owner.Center=dashEnd;Owner.velocity=Vector2.Zero;Owner.fallStart=(int)(Owner.position.Y/16);
                Owner.immune=true;Owner.immuneTime=Math.Max(Owner.immuneTime,4);
            }
            if(age>=MartialTuning.HoldFrames)Transition(4);
        }
        else if(Phase==4)
        {
            Projectile.rotation=direction+MathHelper.Lerp(-1.35f,1.45f,MathHelper.SmoothStep(0,1,Math.Min(1,age/18f)));
            if(age>=6)
            {
                FlingPins();
                if(Tier==3 && !wave && Local){wave=true;MartialWave.Spawn(Projectile,Owner.Center+aim*160,aim);}
            }
            if(age>=MartialTuning.SlashFrames)Transition(5);
        }
        else if(Phase==5 && age>=16){Projectile.Kill();return;}
        Projectile.Center=Owner.Center;Owner.direction=aim.X>=0?1:-1;
        if(TimeStopped && Local){Owner.controlJump=false;Owner.controlDown=false;Owner.controlUp=false;Owner.controlLeft=false;Owner.controlRight=false;}
        if(Main.dedServ)return;
        SlashTrail.Update(Owner.Center,Projectile.rotation,Phase==4 && Age>0,Phase==5);
        slashSparks.Update(Owner.Center,Projectile.rotation,Tier,Age,Phase==4 && Age>0,spawn=>SoulFlowSpark.Spawn(spawn));
        if(clock%3==0)MartialVisual.Spark(Owner.Center+Projectile.rotation.ToRotationVector2()*Main.rand.NextFloat(100,330),Main.rand.NextVector2Circular(1,1),Tier==3?MartialVisual.Violet:MartialVisual.Cyan);
        if(Phase==4 && age==1)SoundEngine.PlaySound(SoundID.Item71 with {Volume=.65f},Owner.Center);
        Lighting.AddLight(Owner.Center,MartialVisual.Cyan.ToVector3()*.7f);
    }
    public override void OnKill(int timeLeft)
    {if(Local && Owner.GetModPlayer<MartialPlayer>().FormSoul==Projectile.GetGlobalProjectile<SoulSource>().SoulId)Owner.GetModPlayer<MartialPlayer>().Complete();}
    public override bool PreDraw(ref Color lightColor)
    {
        Color tint=Tier==3?MartialVisual.Violet:MartialVisual.Cyan;
        float opacity=Phase==5?Math.Max(0,1-Age/16f):1;
        SlashTrail.DrawSwordGhosts(Tier);
        var direction=Projectile.rotation.ToRotationVector2();
        MartialVisual.Sword(ItemID.Zenith,Owner.Center+direction*145,Projectile.rotation+MathHelper.PiOver4,300*Projectile.scale,tint,opacity);
        if(TimeStopped)
        {
            Vector2 tip=Owner.Center+direction*280;
            for(int i=0;i<4;i++)
            {var ray=new Vector2(1,0).RotatedBy(i*MathHelper.PiOver2+clock*.025f);MartialVisual.Line(tip-ray*18,tip+ray*18,tint*.65f,2);}
            EnvironmentEffect.Ring(Main.spriteBatch,Owner.Center-Main.screenPosition,60+MathF.Sin(clock*.05f)*3,tint*.2f,48,clock*.005f);
        }
        return false;
    }
}

public class MartialWave : ModProjectile
{
    public override string Texture=>"Terraria/Images/Projectile_"+ProjectileID.SwordBeam;
    private int age;
    private readonly Dictionary<long,int> hits=[];
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=140;Projectile.friendly=true;Projectile.DamageType=DamageClass.Melee;
        Projectile.penetrate=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.timeLeft=50;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=12;
    }
    public static int Spawn(Projectile giant,Vector2 at,Vector2 direction)
    {
        int index=Projectile.NewProjectile(giant.GetSource_FromThis(),at,direction*23,ModContent.ProjectileType<MartialWave>(),giant.damage*14,0,giant.owner);
        if(index<Main.maxProjectiles){Main.projectile[index].CritChance=giant.CritChance;Main.projectile[index].netUpdate=true;}
        return index;
    }
    public override bool? CanHitNPC(NPC npc)
    {
        if(!MartialTuning.Target(npc))return false;
        long id=WoodCombat.Root(npc).GetGlobalNPC<HardOreTarget>().Id;
        return hits.TryGetValue(id,out int last) && age-last<12?false:null;
    }
    public override void OnHitNPC(NPC n,NPC.HitInfo hit,int damageDone)
    {
        if(damageDone<=0)return;var root=WoodCombat.Root(n);hits[root.GetGlobalNPC<HardOreTarget>().Id]=age;
        root.GetGlobalNPC<MartialTarget>().Scar(root,Projectile);
    }
    public override void AI()
    {
        if(Projectile.owner<0 || Projectile.owner>=Main.maxPlayers || !Main.player[Projectile.owner].active || Main.player[Projectile.owner].dead ||
            Main.player[Projectile.owner].HeldItem.ModItem is not SoulItem soul || soul.SoulId!=Projectile.GetGlobalProjectile<SoulSource>().SoulId)
        {Projectile.Kill();return;}
        age++;Projectile.rotation=Projectile.velocity.ToRotation();
        if(age%2==0)for(int i=0;i<2;i++)MartialVisual.Spark(Projectile.Center+new Vector2(0,Main.rand.NextFloat(-65,65)).RotatedBy(Projectile.rotation),-Projectile.velocity*.05f,MartialVisual.Violet);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float fade=Math.Min(1,Projectile.timeLeft/12f);Vector2 axis=Projectile.rotation.ToRotationVector2(),side=axis.RotatedBy(MathHelper.PiOver2);
        Vector2 previous=Projectile.Center+side*100-axis*28;
        for(int i=1;i<=24;i++)
        {
            float t=i/24f,curve=MathF.Sin(t*MathHelper.Pi)*58;
            var next=Projectile.Center+side*MathHelper.Lerp(100,-100,t)+axis*(curve-28);
            MartialVisual.Line(previous,next,MartialVisual.Violet*(fade*.25f),16);
            MartialVisual.Line(previous,next,Color.White*(fade*.85f),3);previous=next;
        }
        return false;
    }
}
