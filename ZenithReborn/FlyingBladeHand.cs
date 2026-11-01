using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 玩家侧飞刃蓄能：每0.3秒向手中叠加一把飞刃，弹容量与独行役使上限一致（SoulCurses.Blades）。
// 挥臂时一次性掷出手中全部飞刃，5°间隔扇形展开；释放后清空重新蓄能。全部为暂定初值。
public class FlyingBladeHand : ModPlayer
{
    public int Loaded;              // 当前手中飞刃数
    public uint NextStack;          // 下一把叠层的更新计数
    public const int StackInterval=18;   // 0.3秒 = 18帧
    public const float SpreadDegrees=5f; // 齐射扇面相邻间隔
    public Func<uint> Clock=null;     // 夹具注入的虚拟时钟；生产路径为 null，读全局更新计数
    public Vector2 AimOverride=Vector2.Zero; // 夹具注入的瞄准点；生产路径为零向量，读真实鼠标世界坐标

    public override void PostUpdate()
    {
        if(Player.HeldItem.ModItem is not SoulItem soul || !soul.Attached || !FlyingBladeFamily.Enabled(soul))
        {Loaded=0;NextStack=0;return;}
        int capacity=SoulCurses.Blades(soul);
        uint now=Clock!=null?Clock():(uint)Main.GameUpdateCount;
        if(Loaded<capacity && now>=NextStack){Loaded++;NextStack=now+StackInterval;}
    }

    // 挥臂开始时掷出手中全部飞刃；仅本地玩家生成弹幕，飞刃弹幕不携带SoulId，初版不与祝福/环境联动。
    public void Launch(SoulItem soul)
    {
        if(Player.whoAmI!=Main.myPlayer || Main.netMode==NetmodeID.Server || Loaded<=0 ||
            !FlyingBladeFamily.Enabled(soul))return;
        // 通刃已寄宿时，非本族主剑也能掷飞刃；否则只掷本族成员。
        Item[] members=FlyingBladeCores.Pass(soul)
            ?soul.Active.ToArray()
            :soul.Active.Where(i=>FlyingBladeFamily.IsMember(i.type)).ToArray();
        if(members.Length==0)return;
        Vector2 aim=(AimOverride==Vector2.Zero?Main.MouseWorld:AimOverride)-Player.Center;
        if(aim.LengthSquared()<2500)aim=Vector2.UnitX*Player.direction*50;
        float baseAngle=(float)Math.Atan2(aim.Y,aim.X);
        int count=Loaded;
        // 叠刃寄宿后，族内武器数量会放大飞刃伤害。
        float bonus=FlyingBladeCores.DamageBonus(soul);
        bool sigil=FlyingBladeCores.Sigil(soul);
        for(int i=0;i<count;i++)
        {
            var host=members[i%members.Length];
            // 扇形展开：以瞄准线为中心，相邻飞刃相隔5°。
            float angle=baseAngle+(i-(count-1)*.5f)*MathHelper.ToRadians(SpreadDegrees);
            int damage=(int)(soul.BladeDamage(Player,host)*bonus);
            int index=Projectile.NewProjectile(Player.GetSource_ItemUse(soul.Item),Player.Center,
                new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*FlyingBladeShot.LaunchSpeed,
                ModContent.ProjectileType<FlyingBladeShot>(),damage,
                Player.GetWeaponKnockback(host),Player.whoAmI,host.type,sigil?1f:0f);
            if(index<0 || index>=Main.maxProjectiles)break;
            var shot=(FlyingBladeShot)Main.projectile[index].ModProjectile;
            shot.HostType=host.type;
            Main.projectile[index].CritChance=Player.GetWeaponCrit(host);
            Main.projectile[index].netUpdate=true;
        }
        Loaded=0;NextStack=0;
    }
}
