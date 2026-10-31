using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ZenithReborn;

// 飞刀和悠悠球的原版弹幕会在后续更新中读取持有武器的玩家。
public class LivingChannelProjectile : GlobalProjectile
{
    public override bool InstancePerEntity=>true;
    internal LivingWeaponProxy Proxy;
    public override void OnSpawn(Projectile projectile,IEntitySource source)
    {
        if(LivingWeaponProxy.Current!=null && LivingWeaponProxy.Current.Player.HeldItem.channel && projectile.aiStyle is 9 or 15 or 99)
            Proxy=LivingWeaponProxy.Current;
    }
}

// 仅在这些代理弹幕的原版AI调用期间提供代理玩家，结束后立即恢复真实玩家和鼠标。
public class LivingChannelSystem : ModSystem
{
    public override void Load()=>On_Projectile.AI+=RunWithProxy;
    public override void Unload()=>On_Projectile.AI-=RunWithProxy;
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
}
