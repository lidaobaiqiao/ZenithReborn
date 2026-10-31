using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 持有剑魂时维护出战槽对应的召唤体；不改变天顶剑本身的发射。
public class LivingArmamentPlayer : ModPlayer
{
    public override void PostUpdate()
    {
        if(Main.netMode==NetmodeID.Server || Player.whoAmI!=Main.myPlayer || Player.dead ||
            Player.HeldItem.ModItem is not SoulItem soul || !LivingArmamentFamily.Enabled(soul))return;
        foreach(int slot in soul.VisibleSlotIds)
        {
            Item item=soul.ItemAt(slot);
            if(item.IsAir || !soul.TraitMains.Any(i=>ReferenceEquals(i,item)))continue;
            bool exists=false;
            foreach(Projectile p in Main.ActiveProjectiles)
                if(p.owner==Player.whoAmI && p.ModProjectile is LivingArmament && (int)p.ai[0]==slot &&
                    p.GetGlobalProjectile<SoulSource>().SoulId==soul.SoulId){exists=true;break;}
            if(exists)continue;
            int index=Projectile.NewProjectile(Player.GetSource_ItemUse(soul.Item),Player.Center,Vector2.Zero,
                ModContent.ProjectileType<LivingArmament>(),0,0,Player.whoAmI,slot);
            if(index<Main.maxProjectiles)Main.projectile[index].netUpdate=true;
        }
    }
}
