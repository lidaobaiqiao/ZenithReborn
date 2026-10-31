using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 一个剑魂共享触发间隔，只有有效主剑命中才会生成落星。
public class HolyHeavenProc : ModSystem
{
    public const int StarDamage=1000;
    public const uint CooldownFrames=300;
    private static readonly Dictionary<string,uint> LastFall=[];
    public static void OnMainHit(Projectile projectile,NPC target,int damageDone)
    {
        if(Main.netMode==NetmodeID.Server || projectile.owner!=Main.myPlayer || projectile.owner<0 ||
            projectile.owner>=Main.maxPlayers || projectile.ModProjectile is not SoulBlade || damageDone<=0 ||
            !HardOrePlayer.ValidTarget(target,false))return;
        var source=projectile.GetGlobalProjectile<SoulSource>();
        var player=Main.player[projectile.owner];var soul=WoodCombat.FindSoul(player,source.SoulId);
        if(soul==null || player.dead || !ReferenceEquals(player.HeldItem.ModItem,soul) ||
            !soul.TraitEnabled(source.SourceWeapon) || !HolyHeavenFamily.Enabled(soul))return;
        uint now=(uint)Main.GameUpdateCount;string key=projectile.owner+":"+soul.SoulId;
        if(LastFall.TryGetValue(key,out uint last) && unchecked(now-last)<CooldownFrames)return;
        Vector2 aim=target.Center;
        Vector2 origin=new(aim.X,Math.Max(32,aim.Y-600));
        int index=Projectile.NewProjectile(projectile.GetSource_FromThis(),origin,new Vector2(0,24),
            ModContent.ProjectileType<HolyHeavenStar>(),StarDamage,10,projectile.owner);
        if(index<0 || index>=Main.maxProjectiles)return;
        Main.projectile[index].netUpdate=true;LastFall[key]=now;
    }
    public override void OnWorldUnload()=>LastFall.Clear();
    public override void Unload()=>LastFall.Clear();
}
