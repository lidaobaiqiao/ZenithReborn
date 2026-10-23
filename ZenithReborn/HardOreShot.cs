using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public class HardOreShot : ModProjectile
{
    public bool FromOreFlight;
    public float FlightDamageShare=1;
    public override void SendExtraAI(System.IO.BinaryWriter writer){writer.Write(FromOreFlight);writer.Write(FlightDamageShare);}
    public override void ReceiveExtraAI(System.IO.BinaryReader reader){FromOreFlight=reader.ReadBoolean();FlightDamageShare=reader.ReadSingle();}
    public override void ModifyHitNPC(NPC target,ref NPC.HitModifiers modifiers)
    {if(FromOreFlight)modifiers.DefenseEffectiveness*=Math.Clamp(FlightDamageShare,0,1);}
    private long targetGeneration;
    private readonly SwordFlightTrail visualTrail=new();
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalShard;
    public override void SetDefaults()
    {
        FromOreFlight=false;FlightDamageShare=1;
        visualTrail.Clear();
        Projectile.width = Projectile.height = 12;
        Projectile.friendly = true; Projectile.DamageType = DamageClass.Melee;
        Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.timeLeft = 45; Projectile.penetrate = 1;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
        Projectile.scale = .75f;
    }
    public override bool ShouldUpdatePosition() => (HardOreKind)(int)Projectile.ai[1] != HardOreKind.Titanium;
    public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
    {
        int target = (int)Projectile.ai[0];
        if (target >= 0 && target < Main.maxNPCs && Main.npc[target].active)
            targetGeneration = WoodCombat.Root(Main.npc[target]).GetGlobalNPC<HardOreTarget>().Id;
    }
    public override bool? CanDamage() => Projectile.damage <= 0 ? false : null;
    public override bool? CanHitNPC(NPC target)
    {
        int index = (int)Projectile.ai[0];
        return index >= 0 && index < Main.maxNPCs && Main.npc[index].active &&
            WoodCombat.Root(target).whoAmI == WoodCombat.Root(Main.npc[index]).whoAmI &&
            (targetGeneration == 0 || WoodCombat.Root(target).GetGlobalNPC<HardOreTarget>().Id == targetGeneration) ? null : false;
    }
    public override Color? GetAlpha(Color lightColor) => SoulVisuals.Tint(
        HardOreTypes.Types[Math.Clamp((int)Projectile.ai[1], 0, 5)]) * .8f;
    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        var player = Main.player[Projectile.owner];
        var source = Projectile.GetGlobalProjectile<SoulSource>();
        var soul = player.HeldItem.ModItem as SoulItem;
        var kind = (HardOreKind)(int)Projectile.ai[1];
        int index = (int)Projectile.ai[0];
        if (!player.active || player.dead || soul == null || !soul.Attached || soul.SoulId != source.SoulId ||
            !(FromOreFlight?OreFamily.CanFlightTrait(soul,source.SourceWeapon) && HardOreTypes.Index(source.SourceWeapon)==(int)kind:
                HardOreTypes.Enabled(soul, source.SourceWeapon, kind)) || index < 0 || index >= Main.maxNPCs ||
            !HardOrePlayer.ValidTarget(Main.npc[index]) || Vector2.DistanceSquared(Projectile.Center, player.Center) > 600 * 600)
        { Projectile.Kill(); return; }
        long generation = WoodCombat.Root(Main.npc[index]).GetGlobalNPC<HardOreTarget>().Id;
        if (targetGeneration == 0) targetGeneration = generation;
        if (generation != targetGeneration) { Projectile.Kill(); return; }
        float age = ++Projectile.localAI[0];
        if (kind == HardOreKind.Titanium)
        {
            var parent = Main.projectile.FirstOrDefault(p => p.active && p.owner == Projectile.owner &&
                p.identity == (int)Projectile.ai[2] && (p.ModProjectile is SoulBlade || FromOreFlight && p.ModProjectile is OreFlyingSword) &&
                p.GetGlobalProjectile<SoulSource>().SoulId == source.SoulId);
            if (parent == null) { Projectile.Kill(); return; }
            Projectile.Center = parent.Center + (age * .35f).ToRotationVector2() * 18;
            Projectile.rotation = age * .35f;
        }
        else
        {
            Vector2 desired = (Main.npc[index].Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 10;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, kind == HardOreKind.Cobalt ? .16f : .1f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }
        if(!Main.dedServ)visualTrail.Record(Projectile.Center,Projectile.rotation);
    }
    public void DrawOrePrimitives()
    {
        var kind=(HardOreKind)Math.Clamp((int)Projectile.ai[1],0,5);
        float rotation=kind==HardOreKind.Titanium?Projectile.rotation:Projectile.velocity.ToRotation();
        float clock=Main.GlobalTimeWrappedHourly;
        if(visualTrail.Count>=2)OreVisualRenderer.Flight(visualTrail,OreFamily.Tints[8+(int)kind],kind==HardOreKind.Titanium?6:8,clock,tipReach:12,head:Projectile.Center);
        else OreVisualRenderer.Shard(Projectile.Center,rotation,kind,Projectile.localAI[0],clock);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Color tint=OreFamily.Tints[8+Math.Clamp((int)Projectile.ai[1],0,5)];
        var texture=OreSwordEffects.SpectralTexture(TextureAssets.Projectile[ProjectileID.CrystalShard].Value);
        Main.EntitySpriteDraw(texture,Projectile.Center-Main.screenPosition,null,Color.Lerp(tint,Color.White,.55f)*.85f,
            Projectile.rotation,texture.Size()*.5f,Projectile.scale,SpriteEffects.None);
        return false;
    }
    public OreAirFront AirFront()
    {
        Vector2 axis=(int)Projectile.ai[1]==(int)HardOreKind.Titanium?Projectile.rotation.ToRotationVector2():Projectile.velocity.SafeNormalize(Vector2.UnitX);
        return new(Projectile.Center+axis*12,axis,OreFamily.Tints[8+Math.Clamp((int)Projectile.ai[1],0,5)],.55f);
    }
    public override void OnKill(int timeLeft)
    {
        if(Main.dedServ || Projectile.localAI[0]<=0)return;
        var front=AirFront();
        OrePierceFeedback.EndWeapon(new(Projectile.Center,Projectile.Center,front.Axis,front.Tint,-ProjectileID.CrystalShard,Projectile.scale,false));
    }
}
