using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ZenithReborn;

// 为依附武器持有者的弹幕保存独立代理；其他伤害和治疗弹幕继续读取真实玩家。
public class LivingChannelProjectile : GlobalProjectile
{
    public override bool InstancePerEntity=>true;
    internal LivingWeaponProxy Proxy;

    // 这些原版攻击会在弹幕AI中读取使用者的位置、动画或持按状态。
    private static bool UsesWeaponOwner(Projectile projectile)=>projectile.aiStyle is
        3 or 9 or 13 or 15 or 19 or 69 or 75 or 99 or 140 or 142 or 161 or 190 or 191;

    public override void OnSpawn(Projectile projectile,IEntitySource source)
    {
        LivingWeaponProxy current=LivingWeaponProxy.Current;
        if(current!=null && source is EntitySource_ItemUse itemSource &&
            ReferenceEquals(itemSource.Player,current.Player) &&
            itemSource.Item.type==current.Player.HeldItem.type &&
            projectile.owner==current.Player.whoAmI &&
            (current.Player.HeldItem.channel || UsesWeaponOwner(projectile)))
        {
            Proxy=current;
            return;
        }
        // 只向同样依附使用者的子弹幕传递代理；独立子弹幕及回血效果保留真实归属。
        if(UsesWeaponOwner(projectile) && source is EntitySource_Parent parent &&
            parent.Entity is Projectile previous && previous.owner==projectile.owner)
            Proxy=previous.GetGlobalProjectile<LivingChannelProjectile>().Proxy;
    }
}

// 只在依附型弹幕的原版AI调用期间提供代理，结束后立即恢复真实玩家和鼠标。
public class LivingChannelSystem : ModSystem
{
    public override void Load()
    {
        On_Projectile.AI+=RunWithProxy;
        On_Projectile.CanHitWithMeleeWeapon+=CanHitWithProxy;
    }
    public override void Unload()
    {
        On_Projectile.AI-=RunWithProxy;
        On_Projectile.CanHitWithMeleeWeapon-=CanHitWithProxy;
    }
    private static void RunWithProxy(On_Projectile.orig_AI original,Projectile projectile)
    {
        var proxy=projectile.GetGlobalProjectile<LivingChannelProjectile>().Proxy;
        int owner=projectile.owner;
        if(proxy==null || !proxy.Active || owner!=Main.myPlayer || owner<0 || owner>=Main.maxPlayers)
        {original(projectile);return;}
        Player real=Main.player[owner];int oldX=Main.mouseX,oldY=Main.mouseY;
        try
        {
            Main.player[owner]=proxy.Player;
            Main.mouseX=(int)(proxy.Target.X-Main.screenPosition.X);
            Main.mouseY=(int)(proxy.Target.Y-Main.screenPosition.Y);
            original(projectile);
        }
        finally {Main.player[owner]=real;Main.mouseX=oldX;Main.mouseY=oldY;}
    }

    // 近战弹幕在 AI 之外检查方块视线，应从同一把武器的代理位置检查。
    private static bool CanHitWithProxy(On_Projectile.orig_CanHitWithMeleeWeapon original,Projectile projectile,Entity target)
    {
        var proxy=projectile.GetGlobalProjectile<LivingChannelProjectile>().Proxy;
        int owner=projectile.owner;
        if(proxy==null || !proxy.Active || owner!=Main.myPlayer || owner<0 || owner>=Main.maxPlayers)
            return original(projectile,target);
        Player real=Main.player[owner];
        try {Main.player[owner]=proxy.Player;return original(projectile,target);}
        finally {Main.player[owner]=real;}
    }
}
