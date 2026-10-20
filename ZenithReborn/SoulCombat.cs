using Terraria;

namespace ZenithReborn;

public partial class SoulItem
{
    // Damage belongs to this host. Neither other hosts, a shared item panel,
    // nor the number of occupied slots raises or dilutes this blade's damage.
    public int BladeDamage(Player player, Item host) =>
        Math.Max(1, (int)(player.GetWeaponDamage(host) * Potency));
}
