using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 召唤体负责寻敌和显示武器；玩家身体保持不可见。
public class LivingArmament : ModProjectile
{
    private LivingWeaponProxy proxy;
    public override string Texture=>"Terraria/Images/Item_"+ItemID.EnchantedSword;
    public override void SetDefaults()
    {Projectile.width=24;Projectile.height=24;Projectile.friendly=false;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.timeLeft=2;}
    public override bool? CanDamage()=>false;
    public override void AI()
    {
        if(Projectile.owner<0 || Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        Player owner=Main.player[Projectile.owner];string id=Projectile.GetGlobalProjectile<SoulSource>().SoulId;
        SoulItem soul=WoodCombat.FindSoul(owner,id);int slot=(int)Projectile.ai[0];
        if(!owner.active || owner.dead || soul==null || !ReferenceEquals(owner.HeldItem.ModItem,soul) || !LivingArmamentFamily.Enabled(soul) ||
            !soul.VisibleSlotIds.Contains(slot)){Projectile.Kill();return;}
        Item item=soul.ItemAt(slot);
        if(item.IsAir || !soul.TraitMains.Any(i=>ReferenceEquals(i,item))){Projectile.Kill();return;}
        Projectile.timeLeft=2;
        NPC target=null;float best=900*900;
        foreach(NPC n in Main.ActiveNPCs)
            if(HardOrePlayer.ValidTarget(n,false))
            {float d=Vector2.DistanceSquared(owner.Center,n.Center);if(d<best){best=d;target=n;}}
        Vector2 goal=target==null?owner.Center+new Vector2(32+(slot%4)*20,-55-(slot/4)*20):
            target.Center+new Vector2(owner.Center.X<target.Center.X?-48:48,0);
        Projectile.Center=Vector2.Lerp(Projectile.Center,goal,.12f);
        Projectile.rotation=target==null?-.5f:(target.Center-Projectile.Center).ToRotation()+MathHelper.PiOver4;
        if(Projectile.owner!=Main.myPlayer || target==null)return;
        if(proxy==null || !proxy.Matches(item))proxy=new LivingWeaponProxy(owner,item,Projectile);
        proxy.Tick(Projectile.Center,target.Center);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Player owner=Main.player[Projectile.owner];var soul=WoodCombat.FindSoul(owner,Projectile.GetGlobalProjectile<SoulSource>().SoulId);
        if(soul==null)return false;Item item=soul.ItemAt((int)Projectile.ai[0]);if(item.IsAir)return false;
        Main.instance.LoadItem(item.type);var texture=TextureAssets.Item[item.type].Value;
        Rectangle frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Frame();
        Player ghost=proxy?.Player;
        float rotation=ghost!=null && ghost.itemAnimation>0?ghost.itemRotation+(ghost.direction<0?MathHelper.PiOver2:0):Projectile.rotation;
        SpriteEffects effects=ghost!=null && ghost.direction<0?SpriteEffects.FlipHorizontally:SpriteEffects.None;
        Main.EntitySpriteDraw(texture,Projectile.Center-Main.screenPosition,frame,lightColor,rotation,
            frame.Size()*.5f,item.scale,effects,0);return false;
    }
}
