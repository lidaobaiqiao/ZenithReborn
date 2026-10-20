using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
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
    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        SoulId = source switch {
            EntitySource_ItemUse item when item.Item.ModItem is SoulItem soul => soul.SoulId,
            EntitySource_Parent parent when parent.Entity is Projectile p => p.GetGlobalProjectile<SoulSource>().SoulId,
            _ => ""
        };
        SourceWeapon = projectile.ModProjectile is SoulBlade ? (int)projectile.ai[0] :
            source is EntitySource_Parent ancestry && ancestry.Entity is Projectile ancestor ? ancestor.GetGlobalProjectile<SoulSource>().SourceWeapon : 0;
    }
    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter writer)
    { writer.Write(SoulId); writer.Write(LeashRadius); writer.Write(SourceWeapon); }
    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader reader)
    { SoulId = reader.ReadString(); LeashRadius=reader.ReadSingle(); SourceWeapon=reader.ReadInt32(); }
    public override void AI(Projectile projectile)
    {
        if(LeashRadius>0 && projectile.owner>=0 && projectile.owner<Main.maxPlayers &&
            Vector2.Distance(projectile.Center,Main.player[projectile.owner].Center)>LeashRadius) projectile.Kill();
    }
}

public class SoulBlade : ModProjectile
{
    public Vector2 TargetOffset;
    public float SwordScale = 1;
    public int SpecialDamage;
    public bool Reborn { get; private set; }
    public void ConfigureReborn(bool reborn)
    {
        Reborn = reborn;
        // Vanilla 933: 15 updates at two updates/frame. This projectile keeps
        // its stable one-update trajectory, so use the equivalent eight frames.
        Projectile.localNPCHitCooldown = 8;
    }
    // Vanilla Zenith projectile 933 uses ai[0] = Random.Next(-100, 101) as
    // the ellipse's vertical radius / lean. One sword = one ellipse.
    public float Vertical = 60f;
    private int age;
    private bool released;
    private Vector2[] trailPoints;
    private Vector2[] trailCenters;
    private float[] trailRotations;
    private const int TrailLength = SoulRibbonMesh.HistoryLength;
    private int trailCount;
    public override string Texture => "Terraria/Images/Item_" + ItemID.Zenith;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 38;
        Projectile.friendly = true; Projectile.DamageType = DamageClass.Melee;
        Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.penetrate = -1; Projectile.timeLeft = 120;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 8;
        Reborn = false;
        Vertical = 60f;
        trailPoints=new Vector2[TrailLength];trailCenters=new Vector2[TrailLength];trailRotations=new float[TrailLength];
        trailCount=0;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => age <= Projectile.ai[2] ? false : null;
    public override void SendExtraAI(BinaryWriter writer)
    { writer.Write(TargetOffset.X); writer.Write(TargetOffset.Y); writer.Write(SwordScale); writer.Write(Vertical); writer.Write(SpecialDamage); writer.Write(Reborn); }
    public override void ReceiveExtraAI(BinaryReader reader)
    { TargetOffset = new(reader.ReadSingle(), reader.ReadSingle()); SwordScale = reader.ReadSingle(); Vertical = reader.ReadSingle(); SpecialDamage = reader.ReadInt32(); ConfigureReborn(reader.ReadBoolean()); }
    public override void AI()
    {
        Player player = Main.player[Projectile.owner];
        if (!player.active || player.dead) { Projectile.Kill(); return; }
        float elapsed = ++age - Projectile.ai[2];
        if (elapsed < 0) { Projectile.Center = player.Center; Projectile.hide = true; return; }
        Projectile.hide = false;
        float t = elapsed / Math.Max(1, Projectile.ai[1]);
        if (t >= 1) { Projectile.Kill(); return; }
        // Exact vanilla Zenith 933 / UpgradableZenith motion: one ellipse per
        // sword, ai[0] (Vertical) as its lean, orbit around the midpoint.
        Vector2 velocity = TargetOffset * .5f;
        float lerp = t;
        float num3 = Vertical;
        float aim = velocity.ToRotation();
        float dir = velocity.X > 0f ? 1f : -1f;
        float orbit = MathHelper.Pi + dir * lerp * MathHelper.TwoPi;
        float radius = Math.Max(60f, velocity.Length() +
            MathHelper.Lerp(0f, 40f, Utils.GetLerpValue(.5f, 1f, lerp, true)));
        Vector2 target = player.MountedCenter + velocity;
        Vector2 spinning = orbit.ToRotationVector2() * new Vector2(radius, num3 * MathHelper.Lerp(2f, 1f, lerp));
        Vector2 small = (1f - Utils.GetLerpValue(0f, .5f, lerp, true)) *
            new Vector2(dir * -radius * .1f, -num3 * .3f);
        Projectile.Center = target + spinning.RotatedBy(aim) + small;
        Projectile.rotation = orbit + aim + MathHelper.PiOver4;
        if (num3 < 0f)
            Projectile.rotation = MathHelper.Pi + dir * lerp * -MathHelper.TwoPi + aim + MathHelper.PiOver4;
        Projectile.direction = Projectile.spriteDirection = (int)dir;
        Projectile.scale = Math.Clamp(SwordScale, .6f, 2f);
        if(!Main.dedServ)
        {
            int historyLimit=Math.Clamp((int)(Projectile.ai[1]*.45f),8,TrailLength);
            if(trailCount>0 && Vector2.DistanceSquared(Projectile.Center,trailCenters[0])>90000) trailCount=0;
            for(int i=Math.Min(trailCount,historyLimit-1);i>0;i--)
            { trailPoints[i]=trailPoints[i-1];trailCenters[i]=trailCenters[i-1];trailRotations[i]=trailRotations[i-1]; }
            trailCenters[0]=Projectile.Center;trailRotations[0]=Projectile.rotation;
            trailPoints[0]=Projectile.Center+(Projectile.rotation-MathHelper.PiOver4).ToRotationVector2()*26*Projectile.scale;
            trailCount=Math.Min(historyLimit,trailCount+1);
            Color tint=SoulVisuals.Tint((int)Projectile.ai[0]);
            Lighting.AddLight(Projectile.Center,tint.ToVector3()*.08f);
            if(age%8==0 && t>.15f && t<.8f)
            {
                Dust dust=Dust.NewDustPerfect(trailPoints[0],DustID.Enchanted_Pink,
                    Vector2.Zero,190,tint,.4f);dust.noGravity=true;dust.noLight=true;
            }
        }
        if (!released && t >= .4f)
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
        bool solid = WeaponFamilies.Of((int)Projectile.ai[0]) == BladeFamily.Solid ||
            soul != null && soul.Supports.Any(i=>WeaponFamilies.Of(i.type)==BladeFamily.Solid);
        // The line helper can miss a short blade wholly inside a large target.
        // The blade segment passes through its center, so containment is a hit.
        return targetHitbox.Contains(Projectile.Center.ToPoint()) || Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - length, Projectile.Center + length, 20 * Projectile.scale *
            (solid?1.15f:1), ref point);
    }
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        var family=WeaponFamilies.Of((int)Projectile.ai[0]);
        if(family==BladeFamily.Frost) target.AddBuff(BuffID.Frostburn,120);
        else if(family==BladeFamily.Nature) target.AddBuff(BuffID.Poisoned,180);
        var soul = WoodCombat.FindSoul(Main.player[Projectile.owner], Projectile.GetGlobalProjectile<SoulSource>().SoulId);
        if (soul != null)
        {
            if (soul.Supports.Any(i => WeaponFamilies.Of(i.type) == BladeFamily.Frost)) target.AddBuff(BuffID.Frostburn, 120);
            if (soul.Supports.Any(i => WeaponFamilies.Of(i.type) == BladeFamily.Nature)) target.AddBuff(BuffID.Poisoned, 180);
        }
        WoodCombat.FinishPrimaryHit(Projectile, target, damageDone);
        Main.player[Projectile.owner].GetModPlayer<HardOrePlayer>().Hit(Projectile, target, damageDone);
        OreFamily.OnMainHit(Projectile, target, damageDone);
    }
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        WoodCombat.PreparePrimaryHit(Projectile, target);
        WoodCombat.ModifyPrimaryHit(Projectile, target, ref modifiers);
        Main.player[Projectile.owner].GetModPlayer<HardOrePlayer>().Modify(Projectile, target, ref modifiers);
        var soul = WoodCombat.FindSoul(Main.player[Projectile.owner], Projectile.GetGlobalProjectile<SoulSource>().SoulId);
        if (((int)Projectile.ai[0] == ItemID.CopperShortsword || soul != null && soul.Supports.Any(i => i.type == ItemID.CopperShortsword)) &&
            Vector2.Distance(Main.player[Projectile.owner].Center, target.Center) < 180)
            modifiers.FinalDamage *= 1.2f;
    }
    private void ReleaseSpecial(Player player)
    {
        ReleaseWeaponSpecial(player, (int)Projectile.ai[0]);
        var soul = WoodCombat.FindSoul(player, Projectile.GetGlobalProjectile<SoulSource>().SoulId);
        if (soul == null) return;
        var woodPlayer = player.GetModPlayer<WoodPlayer>();
        foreach (var support in soul.Supports)
            if (!WoodTypes.IsWood(support.type) && SoulProgress.ZenithWeapons.Contains(support.type) &&
                woodPlayer.Ready(soul.SoulId + ":special:" + support.type, 45))
                ReleaseWeaponSpecial(player, support.type);
    }
    private void ReleaseWeaponSpecial(Player player, int type)
    {
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
        float halfLength=frame.Size().Length()*.5f*Projectile.scale;
        float t=Math.Clamp((age-Projectile.ai[2])/Math.Max(1,Projectile.ai[1]),0,1);
        float visibility=Math.Min(Math.Min(t/.1f,(1-t)/.16f),1);
        return SoulRibbonMesh.Build(trailCenters,trailRotations,trailCount,halfLength,
            SoulVisuals.Tint(itemType),visibility,Main.screenPosition,vertices);
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
        float t=Math.Clamp((age-Projectile.ai[2])/Math.Max(1,Projectile.ai[1]),0,1);
        float visibility=Math.Min(Math.Min(t/.1f,(1-t)/.16f),1);
        // The mesh ribbon was drawn behind entities; retain two faint ghosts.
        for(int i=5;i>=2;i-=3)
        {
            if(i>=trailCount) continue;
            Main.EntitySpriteDraw(texture,trailCenters[i]-Main.screenPosition,frame,
                Color.Lerp(tint,Color.White,.3f)*(.12f*(1-i/(float)trailCount)*visibility),
                trailRotations[i],origin,Projectile.scale,SpriteEffects.None);
        }
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, WoodTypes.SwordTint(itemType)*visibility,
            Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);
        return false;
    }
}
