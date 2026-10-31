using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 在独立服务端夹具中检查寄宿、触发、范围和伤害，不改玩家存档。
internal static class HolyHeavenCheck
{
    private static Projectile[] Stars()=>Main.projectile.Where(p=>p.active&&p.ModProjectile is HolyHeavenStar).ToArray();
    private static Projectile[] Impacts()=>Main.projectile.Where(p=>p.active&&p.ModProjectile is HolyHeavenImpact).ToArray();
    internal static void Run(Action<bool,string> check)
    {
        foreach(var p in Main.projectile)p.active=false;
        foreach(var n in Main.npc)n.active=false;
        ModContent.GetInstance<HolyHeavenProc>().OnWorldUnload();
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.dayTime=true;
        var player=new Player {active=true,whoAmI=0,position=new Vector2(1000,1000)};Main.player[0]=player;player.ResetEffects();
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.Hosts[0]=new Item(ItemID.Starfury);
        player.inventory[0]=soul.Item;player.selectedItem=0;soul.RefreshStats();
        var target=new NPC();target.SetDefaults(NPCID.Zombie);target.whoAmI=20;target.active=true;
        target.life=target.lifeMax=100000;target.HideStrikeDamage=true;target.defense=0;
        target.Center=player.Center+new Vector2(100,0);Main.npc[20]=target;
        var other=new NPC();other.SetDefaults(NPCID.Zombie);other.whoAmI=21;other.active=true;
        other.life=other.lifeMax=100000;other.HideStrikeDamage=true;other.defense=0;
        other.Center=target.Center+new Vector2(60,0);Main.npc[21]=other;
        var far=new NPC();far.SetDefaults(NPCID.Zombie);far.whoAmI=22;far.active=true;
        far.life=far.lifeMax=100000;far.HideStrikeDamage=true;far.defense=0;
        far.Center=target.Center+new Vector2(300,0);Main.npc[22]=far;
        check(HolyHeavenFamily.IsMember(ItemID.StarWrath)&&HolyHeavenFamily.IsMember(ItemID.PearlwoodSword)&&
            HolyHeavenFamily.IsMember(ItemID.TrueExcalibur)&&!HolyHeavenFamily.IsMember(ItemID.CopperBroadsword),"神圣与星辰名册边界");
        check(HolyHeavenFamily.Count(soul)==1&&HolyHeavenFamily.Enabled(soul),"有效主槽激活族特性");
        check(SoulTraits.All(soul).Any(t=>t.Key=="heaven-star"&&t.Active&&t.Category==TraitCategory.Heaven),"界面显示有效族特性");
        SoulUI.SelectTraitScope(false);SoulUI.SelectTraitCategory(5);
        check(SoulUI.TraitEntries(soul).Any(t=>t.Key=="heaven-star")&&SoulUI.TraitEntries(soul).All(t=>t.Category==TraitCategory.Heaven),"独立种族筛选可以找到落星特性");
        soul.Hosts[0]=new Item(ItemID.WoodenSword);soul.SupportHosts[0]=new Item(ItemID.StarWrath);
        check(soul.SupportCapacity>0&&HolyHeavenFamily.Enabled(soul),"有效星辰支援可激活族特性");
        soul.SupportHosts[0].TurnToAir();check(!HolyHeavenFamily.Enabled(soul),"取下唯一族成员后立即失效");
        soul.Hosts[0]=new Item(ItemID.Starfury);soul.RefreshStats();
        int index=soul.EmitBlade(player,soul.Hosts[0],0,1);var blade=Main.projectile[index];blade.damage=100;blade.CritChance=0;blade.ModProjectile.AI();
        HolyHeavenProc.OnMainHit(blade,target,100);check(Stars().Length==1,"有效命中生成一颗落星");
        var star=Stars()[0];check(star.damage==1000&&star.velocity.Y>0&&star.Center.Y<target.Center.Y,"基础1000伤害且从上方实际坠落");
        HolyHeavenProc.OnMainHit(blade,target,100);check(Stars().Length==1,"同一剑魂的冷却阻止连续生成");
        star.Center=target.Center;target.immune[0]=0;int before=target.life;star.Damage();
        check(target.life<before&&Impacts().Length==1&&!star.active,"真实弹幕直击产生伤害并转入冲击");
        var impact=Impacts()[0];int direct=target.life,otherBefore=other.life,farBefore=far.life;
        target.immune[0]=other.immune[0]=far.immune[0]=0;impact.Damage();
        check(target.life==direct&&other.life<otherBefore&&far.life==farBefore,"冲击不重复伤害直击目标，命中范围内其他敌人");
        int after=other.life;impact.Damage();check(other.life==after,"同一冲击不会重复伤害目标");
        check(HolyHeavenImpact.Radius==120&&HolyHeavenProc.CooldownFrames==300,"范围和初始冷却一致");
        ModContent.GetInstance<HolyHeavenProc>().OnWorldUnload();
        HolyHeavenProc.OnMainHit(blade,target,100);check(Stars().Length==1,"世界清理后可再次触发");
        ((HolyHeavenStar)Stars()[0].ModProjectile).OnTileCollide(Vector2.UnitY);
        check(Impacts().Length==2,"方块撞击产生冲击");
    }
}
