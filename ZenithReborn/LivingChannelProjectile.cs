using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ZenithReborn;

// 为依附武器持有者的弹幕保存独立代理；其他伤害和治疗弹幕继续读取真实玩家。
public class LivingChannelProjectile : GlobalProjectile
{
    public override bool InstancePerEntity=>true;
    internal LivingWeaponProxy Proxy;
    // 来源记录只用于视线和继续传播，不表示弹幕需要代理参与 AI。
    internal LivingWeaponProxy Origin;

    // 这些原版攻击会在弹幕AI中读取使用者的位置、动画或持按状态。
    private static bool UsesWeaponOwner(Projectile projectile)=>projectile.aiStyle is
        3 or 9 or 13 or 15 or 19 or 69 or 75 or 99 or 140 or 142 or 161 or 190 or 191;

    public override void OnSpawn(Projectile projectile,IEntitySource source)
    {
        LivingWeaponProxy current=LivingWeaponProxy.Current;
        if(current!=null && source is EntitySource_ItemUse itemSource &&
            ReferenceEquals(itemSource.Player,current.Player) &&
            itemSource.Item.type==current.Player.HeldItem.type &&
            projectile.owner==current.Player.whoAmI)
        {
            Origin=current;
            if(current.Player.HeldItem.channel || UsesWeaponOwner(projectile))
                Proxy=current;
            return;
        }
        if(source is not EntitySource_Parent parent)
            return;
        if(parent.Entity is Projectile previous && previous.owner==projectile.owner)
        {
            var previousBinding=previous.GetGlobalProjectile<LivingChannelProjectile>();
            Origin=previousBinding.Origin;
            // 仅原有依附型子弹幕继承 AI 代理。
            if(UsesWeaponOwner(projectile))
                Proxy=previousBinding.Proxy;
        }
        else if(parent.Entity is Player player)
        {
            // 某些命中效果以玩家而非父弹幕作为来源，仅认本次伤害窗口内的对应玩家。
            var origin=LivingChannelSystem.DamageOrigin;
            if(origin!=null && origin.Active && projectile.owner==player.whoAmI &&
                projectile.owner==origin.Player.whoAmI &&
                (ReferenceEquals(player,origin.Owner) || ReferenceEquals(player,origin.Player)))
                Origin=origin;
        }
    }
}

// 只在依附型弹幕的原版AI调用期间提供代理，结束后立即恢复真实玩家和鼠标。
public class LivingChannelSystem : ModSystem
{
    [System.ThreadStatic] internal static LivingWeaponProxy DamageOrigin;

    public override void Load()
    {
        On_Projectile.AI+=RunWithProxy;
        On_Projectile.Damage+=TrackDamageOrigin;
        On_Projectile.CanHitWithMeleeWeapon+=CanHitWithProxy;
    }
    public override void Unload()
    {
        On_Projectile.AI-=RunWithProxy;
        On_Projectile.Damage-=TrackDamageOrigin;
        On_Projectile.CanHitWithMeleeWeapon-=CanHitWithProxy;
    }

    // 嵌套伤害各自覆盖来源，结束或异常时恢复；整个命中过程仍使用原玩家。
    private static void TrackDamageOrigin(On_Projectile.orig_Damage original,Projectile projectile)
    {
        var previous=DamageOrigin;
        var origin=projectile.GetGlobalProjectile<LivingChannelProjectile>().Origin;
        int owner=projectile.owner;
        DamageOrigin=origin!=null && origin.Active && owner==Main.myPlayer &&
            owner>=0 && owner<Main.maxPlayers && owner==origin.Player.whoAmI ? origin : null;
        try {original(projectile);}
        finally {DamageOrigin=previous;}
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
        var binding=projectile.GetGlobalProjectile<LivingChannelProjectile>();
        var proxy=binding.Proxy ?? (projectile.ownerHitCheck ? binding.Origin : null);
        int owner=projectile.owner;
        if(proxy==null || !proxy.Active || owner!=Main.myPlayer || owner<0 || owner>=Main.maxPlayers)
            return original(projectile,target);
        Player real=Main.player[owner];
        try {Main.player[owner]=proxy.Player;return original(projectile,target);}
        finally {Main.player[owner]=real;}
    }
}
