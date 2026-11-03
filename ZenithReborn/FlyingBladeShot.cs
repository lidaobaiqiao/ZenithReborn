using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 飞刃弹幕三阶段：直线飞出 → 刺中（或飞满射程）原地旋转三圈（持续伤害）→ 收回。
// 旋转期伤害为直击的50%，节奏沿用8帧共享免疫；收回期默认无伤害，族内达到第4阶（6把）后保留50%。
// 印记为核心设计留白，未实现；第4阶目前只接管"收回期伤害"这一项特殊效果。
// 本类只负责行为与姿态记录，残影、光环、粒子等绘制细节全部在 FlyingBladeVisuals。
public class FlyingBladeShot : ModProjectile
{
    // 绘制完全由 PreDraw 接管（寄宿武器原版贴图逐剑取色）；此占位仅满足资产加载，兜底显示原版飞刀贴图。
    public override string Texture => "Terraria/Images/Item_" + ItemID.FlyingKnife;
    // 外观与伤害来源的寄宿武器类型。存在 ai[0] 而不是自定义字段，才能随弹幕同步给其他玩家。
    public int HostType => (int)Projectile.ai[0];

    // 三阶段枚举对外公开，只为让视觉层按阶段分支，不参与伤害与命中判断。
    public enum Stage{Fly,Rotate,Return}

    private Stage stage;
    private int rotateLeft;   // 旋转剩余帧
    private float travel;     // 直飞行程
    private int easeFrame;    // 起飞缓入已用帧数
    // 视觉状态（拖尾、环影、光环）每把飞刃各一份，构造时一次分配。
    private readonly FlyingBladeVisuals visuals=new();

    // 第4阶开关：由 Launch 写入 ai[1]（>0 表示收回期保留伤害），随弹幕自动同步。
    private bool ReturnHits=>Projectile.ai[1]>0f;
    public const float LaunchSpeed=12f;  // 掷出速度（暂定初值，接近原版飞刀）
    public const float MaxRange=600f;    // 未命中时的射程
    public const int RotationFrames=45;  // 三圈，每圈15帧
    public const int RotationTick=8;     // 旋转持续伤害节奏
    public const float RotationDamage=.5f;
    // 起飞缓入：出生后 6 帧内把速度从 60% 拉到全速，掷出才有"甩"出去的加速感。
    // 只改每帧速度大小，不改方向；射程仍按 travel 记录的真实位移累计，600 像素不变。
    public const int LaunchEaseFrames=6;
    public const float LaunchEaseStart=.6f;

    public override void SetDefaults()
    {
        Projectile.width=16;Projectile.height=16;
        Projectile.friendly=true;Projectile.penetrate=-1;
        Projectile.tileCollide=false;Projectile.DamageType=DamageClass.Melee;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=RotationTick;
        Projectile.timeLeft=600;
    }
    public override void AI()
    {
        switch(stage)
        {
            case Stage.Fly:
                EaseLaunch();
                travel+=Projectile.velocity.Length();
                // 未命中飞满射程同样转入旋转，行为统一。
                if(travel>=MaxRange)EnterRotation();
                break;
            case Stage.Rotate:
                // 原地自转：速度清零保留命中判定，绘制持续旋转。
                Projectile.velocity=Vector2.Zero;
                Projectile.rotation+=MathHelper.TwoPi/15f;
                rotateLeft--;
                // 印刃未寄宿时收回期无伤害；寄宿后保留 50%（见 ModifyHitNPC）。
                if(rotateLeft<=0){stage=Stage.Return;if(!ReturnHits)Projectile.damage=0;}
                break;
            case Stage.Return:
                var back=Main.player[Projectile.owner].Center-Projectile.Center;
                if(back.Length()<24f || Projectile.timeLeft<5)
                {KillReason=$"kill: back={back.Length():F0} timeLeft={Projectile.timeLeft}";Projectile.Kill();return;}
                Projectile.velocity=back.SafeNormalize(Vector2.UnitX)*12f;
                break;
        }
        // 旋转外的两段：刀刃长轴对齐速度方向（旋转期由自转接管姿态）。
        if(stage!=Stage.Rotate && HostType!=0)visuals.Orient(Projectile,HostType);
        visuals.Update(Projectile,HostType,stage);
    }
    // 直飞前 6 帧线性加速；速度一律先归一到掷出速度再缩放，避免缓动叠加到别的速度上。
    private void EaseLaunch()
    {
        if(easeFrame>=LaunchEaseFrames)return;
        easeFrame++;
        float k=LaunchEaseStart+(1f-LaunchEaseStart)*easeFrame/LaunchEaseFrames;
        Projectile.velocity=Projectile.velocity.SafeNormalize(Vector2.UnitX)*LaunchSpeed*k;
    }
    private void EnterRotation()
    {
        stage=Stage.Rotate;rotateLeft=RotationFrames;
        Projectile.velocity=Vector2.Zero;
    }
    public override bool? CanHitNPC(NPC target)=>stage==Stage.Return && !ReturnHits?false:null;
    public string KillReason=""; // 夹具诊断：记录收回期 Kill 的触发条件
    public string DebugState=>$"phase={stage} rotateLeft={rotateLeft} travel={travel:F0} vel={Projectile.velocity} timeLeft={Projectile.timeLeft} active={Projectile.active} {KillReason}";
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        // 视觉反馈先按命中时的阶段结算（旋转期的持续伤害不重复爆），再决定是否转入旋转。
        visuals.Hit(Projectile,HostType,stage);
        if(stage==Stage.Fly)EnterRotation(); // 刺中后转入旋转
    }
    public override void ModifyHitNPC(NPC target,ref NPC.HitModifiers modifiers)
    {
        // 旋转期与印刃收回期都只结算一半伤害。
        if(stage==Stage.Rotate || stage==Stage.Return)modifiers.FinalDamage*=RotationDamage;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        // ai[0] 来自网络同步，异常值不能直接索引贴图数组；寄宿类型非法时走原版兜底贴图。
        if(!FlyingBladeVisuals.ValidHost(HostType))return true;
        var blade=TextureAssets.Item[HostType].Value;
        // 贴图取不到时返回 true 让原版画占位图，不能返回 false 把弹幕画成彻底隐形。
        if(blade is null)return true;
        visuals.Draw(Projectile,blade,HostType,stage,lightColor);
        return false;
    }
}
