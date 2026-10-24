using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// 剑体已经有独立材质，不再自动叠加药剂、岩浆石等附魔粒子。
// 只关闭外观开关，原版命中附加减益仍由 StatusNPC 正常处理。
public class SwordEnchantmentVisuals : GlobalProjectile
{
    public override void SetDefaults(Projectile projectile)
    {
        if(projectile.ModProjectile is SoulBlade or OreFlyingSword or OreSkyfall or MartialBlade or MartialGiant)
            projectile.noEnchantmentVisuals=true;
    }
}
