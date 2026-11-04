using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 每个槽位持有独立玩家对象；攻击使用引擎的物品检查流程。
internal sealed class LivingWeaponProxy
{
    [ThreadStatic] internal static LivingWeaponProxy Current;
    private readonly Player player;
    private readonly Player owner;
    private readonly Projectile summon;
    private readonly int summonIdentity;
    public bool Active=>summon.active && summon.type==ModContent.ProjectileType<LivingArmament>() && summon.identity==summonIdentity;
    public Vector2 Target {get;private set;}
    private readonly int type,prefix;
    private readonly Item source;
    public Player Player=>player;
    internal Player Owner=>owner;
    public LivingWeaponProxy(Player owner,Item weapon,Projectile summon)
    {
        this.owner=owner;this.summon=summon;summonIdentity=summon.identity;source=weapon;type=weapon.type;prefix=weapon.prefix;
        player=new Player {whoAmI=owner.whoAmI,active=true,direction=1};
        player.ResetEffects();player.inventory[0]=weapon.Clone();player.selectedItem=0;
        player.lastVisualizedSelectedItem=player.inventory[0].Clone();
    }
    public bool Matches(Item item)=>ReferenceEquals(source,item) && item.type==type && item.prefix==prefix;
    public void Tick(Vector2 position,Vector2 target)
    {
        LivingProxyOwnerState.Copy(owner,player);
        Target=target;player.Center=position;player.direction=target.X>=position.X?1:-1;
        player.controlUseItem=true;player.releaseUseItem=true;
        int oldX=Main.mouseX,oldY=Main.mouseY;var previous=Current;
        try
        {
            Main.mouseX=(int)(target.X-Main.screenPosition.X);
            Main.mouseY=(int)(target.Y-Main.screenPosition.Y);
            Current=this;player.ItemCheck();
        }
        finally {Current=previous;Main.mouseX=oldX;Main.mouseY=oldY;}
    }
}
