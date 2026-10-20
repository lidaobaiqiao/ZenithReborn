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
    public int TargetIndex, Material, Total, RingIndex, VisualType;
    public string SourceSoulId = "";
    private int age, hits;
    private long targetIdentity;
    private Vector2 launchedFrom;
    public override string Texture => "Terraria/Images/Item_" + ItemID.Zenith;
    public void BindTarget(NPC npc)
    {
        TargetIndex=npc.whoAmI;
        targetIdentity=npc.GetGlobalNPC<HardOreTarget>().Id;
    }
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=20;
        Projectile.friendly=true; Projectile.DamageType=DamageClass.Melee;
        Projectile.tileCollide=false; Projectile.ignoreWater=true;
        Projectile.penetrate=1; Projectile.timeLeft=70;
        Projectile.usesLocalNPCImmunity=true; Projectile.localNPCHitCooldown=6;
        // No custom DrawBehind queue: let the normal projectile pass invoke PreDraw.
        Projectile.hide=false;
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
        int source=Projectile.GetGlobalProjectile<SoulSource>().SourceWeapon;
        return player.active && !player.dead && player.HeldItem.ModItem is SoulItem soul && soul.Attached &&
            soul.SoulId==SourceSoulId && soul.Active.Any(i=>i.type==source) &&
            OreFamily.Materials(soul).Contains(Material) && Vector2.Distance(player.Center,Projectile.Center)<=600;
    }
    public override bool? CanDamage() => age>=21 && Target()!=null && SourceValid()?null:false;
    public override bool? CanHitNPC(NPC target) => Target() is NPC root &&
        WoodCombat.Root(target).whoAmI==root.whoAmI && HardOrePlayer.ValidTarget(target)?null:false;
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(TargetIndex);writer.Write(Material);writer.Write(Total);writer.Write(RingIndex);
        writer.Write(SourceSoulId);writer.Write(VisualType);writer.Write(age);writer.Write(hits);
        writer.Write(launchedFrom.X);writer.Write(launchedFrom.Y);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        TargetIndex=reader.ReadInt32();Material=Math.Clamp(reader.ReadInt32(),0,13);
        Total=Math.Clamp(reader.ReadInt32(),1,14);RingIndex=Math.Clamp(reader.ReadInt32(),0,13);
        SourceSoulId=reader.ReadString();VisualType=reader.ReadInt32();
        age=reader.ReadInt32();hits=reader.ReadInt32();
        launchedFrom=new(reader.ReadSingle(),reader.ReadSingle());
    }
    public override void AI()
    {
        NPC target=Target();
        if(target==null || !SourceValid()) { Projectile.Kill();return; }
        if(age==0) Projectile.penetrate=Total>=OreFamily.MaterialCount?3:1;
        age++;
        if(hits>0)
        {
            Projectile.velocity=Vector2.Zero;Projectile.Center=target.Center;return;
        }
        if(age<=20)
        {
            float angle=-MathHelper.PiOver2+MathHelper.TwoPi*RingIndex/Math.Max(1,Total)+age*.018f;
            Projectile.Center=target.Center+angle.ToRotationVector2()*92f;
            Projectile.velocity=Vector2.Zero;
            Projectile.rotation=(target.Center-Projectile.Center).ToRotation();
            if(!Main.dedServ && (age==1 || age==12))
            {
                Dust dust=Dust.NewDustPerfect(Projectile.Center,DustID.Enchanted_Pink,Vector2.Zero,180,
                    OreFamily.Tints[Math.Clamp(Material,0,13)],.35f);
                dust.noGravity=true;
            }
            return;
        }
        if(age==21)
        {
            launchedFrom=Projectile.Center;
            Projectile.velocity=(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitY)*24f;
            Projectile.netUpdate=true;
        }
        Projectile.rotation=Projectile.velocity.ToRotation();
        if(Vector2.Distance(launchedFrom,Projectile.Center)>180f) Projectile.Kill();
    }
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(CanHitNPC(target)==false) return;
        hits++;
        if(Total<OreFamily.MaterialCount || hits>=3) { Projectile.Kill();return; }
        Projectile.velocity=Vector2.Zero;Projectile.Center=target.Center;Projectile.timeLeft=20;
        Projectile.netUpdate=true;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        int type=OreFamily.MaterialOf(VisualType)==Material?VisualType:OreFamily.DefaultType[Material];
        Main.instance.LoadItem(type);
        Texture2D texture=TextureAssets.Item[type].Value;
        float alpha=age<20?MathHelper.Clamp(age/20f,.15f,1f):1f;
        Color color=Color.Lerp(OreFamily.Tints[Material],Color.White,.45f)*alpha;
        Main.spriteBatch.Draw(texture,Projectile.Center-Main.screenPosition,null,color,
            Projectile.rotation+MathHelper.PiOver4,texture.Size()*.5f,.72f*Projectile.scale,SpriteEffects.None,0f);
        return false;
    }
}
