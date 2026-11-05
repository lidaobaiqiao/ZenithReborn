using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 玩家代理使用独立对象，复制的是当前有效数值，不共享玩家的可变数组。
internal static class LivingProxyOwnerState
{
    public static void Copy(Player owner,Player proxy)
    {
        for(int i=0;i<DamageClassLoader.DamageClassCount;i++)
        {
            DamageClass kind=DamageClassLoader.GetDamageClass(i);
            proxy.GetDamage(kind)=owner.GetDamage(kind);
            proxy.GetCritChance(kind)=owner.GetCritChance(kind);
            proxy.GetAttackSpeed(kind)=owner.GetAttackSpeed(kind);
            proxy.GetArmorPenetration(kind)=owner.GetArmorPenetration(kind);
            proxy.GetKnockback(kind)=owner.GetKnockback(kind);
        }
        proxy.arrowDamage=owner.arrowDamage;
        proxy.bulletDamage=owner.bulletDamage;
        proxy.specialistDamage=owner.specialistDamage;
        proxy.meleeScaleGlove=owner.meleeScaleGlove;
        proxy.autoReuseGlove=owner.autoReuseGlove;
        proxy.kbGlove=owner.kbGlove;
        proxy.strongBees=owner.strongBees;
        proxy.meleeEnchant=owner.meleeEnchant;
        proxy.frostBurn=owner.frostBurn;
        proxy.magmaStone=owner.magmaStone;
        proxy.yoyoString=owner.yoyoString;
        proxy.yoyoGlove=owner.yoyoGlove;
        proxy.counterWeight=owner.counterWeight;
        proxy.gravDir=owner.gravDir;
        proxy.parryDamageBuff=owner.parryDamageBuff;

        proxy.statLife=owner.statLife;
        proxy.statLifeMax2=owner.statLifeMax2;
        CopyValues(owner.buffType,proxy.buffType);
        CopyValues(owner.buffTime,proxy.buffTime);
        CopyValues(owner.buffImmune,proxy.buffImmune);
        CopyValues(owner.ownedProjectileCounts,proxy.ownedProjectileCounts);
    }
    private static void CopyValues<T>(T[] source,T[] destination)
    {
        Array.Copy(source,destination,Math.Min(source.Length,destination.Length));
        if(destination.Length>source.Length)Array.Clear(destination,source.Length,destination.Length-source.Length);
    }
}

// 蝙蝠棍原版直接治疗使用武器的玩家；只将这一条明确路径交给真实玩家执行。
public class LivingProxyHealing : ModSystem
{
    public override void Load()=>On_Player.Heal+=HealRealOwner;
    public override void Unload()=>On_Player.Heal-=HealRealOwner;
    private static void HealRealOwner(On_Player.orig_Heal original,Player player,int amount)
    {
        var proxy=LivingWeaponProxy.Current;
        if(proxy==null || !ReferenceEquals(proxy.Player,player) || player.HeldItem.type!=ItemID.BatBat)
        {original(player,amount);return;}
        Player owner=proxy.Owner;
        original(owner,amount);
        player.statLife=owner.statLife;
    }
}
