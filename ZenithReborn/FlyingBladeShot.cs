using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 飞刃弹幕三阶段：直线飞出 → 刺中（或飞满射程）原地旋转三圈（持续伤害）→ 收回。
// 旋转期伤害为直击的50%，节奏沿用8帧共享免疫；收回期默认无伤害，族内达到第4阶（6把）后保留50%。
// 印记为核心设计留白，未实现；第4阶目前只接管"收回期伤害"这一项特殊效果。
public class FlyingBladeShot : ModProjectile
{
    // 绘制完全由 PreDraw 接管（寄宿武器原版贴图逐剑取色）；此占位仅满足资产加载，兜底显示原版飞刀贴图。
    public override string Texture => "Terraria/Images/Item_" + ItemID.FlyingKnife;
    // 外观与伤害来源的寄宿武器类型。存在 ai[0] 而不是自定义字段，才能随弹幕同步给其他玩家。
    public int HostType => (int)Projectile.ai[0];
    private enum Phase{Fly,Rotate,Return}
    private Phase phase;
    private int rotateLeft;   // 旋转剩余帧
    private float travel;     // 直飞行程
    // 第4阶开关：由 Launch 写入 ai[1]（>0 表示收回期保留伤害），随弹幕自动同步。
    private bool ReturnHits=>Projectile.ai[1]>0f;
    public const float LaunchSpeed=12f;  // 掷出速度（暂定初值，接近原版飞刀）
    public const float MaxRange=600f;    // 未命中时的射程
    public const int RotationFrames=45;  // 三圈，每圈15帧
    public const int RotationTick=8;     // 旋转持续伤害节奏
    public const float RotationDamage=.5f;
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
        if(HostType!=0)Projectile.rotation=(float)Math.Atan2(Projectile.velocity.Y,Projectile.velocity.X)+MathHelper.PiOver4;
        switch(phase)
        {
            case Phase.Fly:
                travel+=Projectile.velocity.Length();
                // 未命中飞满射程同样转入旋转，行为统一。
                if(travel>=MaxRange)EnterRotation();
                break;
            case Phase.Rotate:
                // 原地自转：速度清零保留命中判定，绘制持续旋转。
                Projectile.velocity=Vector2.Zero;
                Projectile.rotation+=MathHelper.TwoPi/15f;
                rotateLeft--;
                // 印刃未寄宿时收回期无伤害；寄宿后保留 50%（见 ModifyHitNPC）。
                if(rotateLeft<=0){phase=Phase.Return;if(!ReturnHits)Projectile.damage=0;}
                break;
            case Phase.Return:
                var back=Main.player[Projectile.owner].Center-Projectile.Center;
                if(back.Length()<24f || Projectile.timeLeft<5)
                {KillReason=$"kill: back={back.Length():F0} timeLeft={Projectile.timeLeft}";Projectile.Kill();return;}
                Projectile.velocity=back.SafeNormalize(Vector2.UnitX)*12f;
                Projectile.rotation=(float)Math.Atan2(Projectile.velocity.Y,Projectile.velocity.X)+MathHelper.PiOver4;
                break;
        }
    }
    private void EnterRotation()
    {
        phase=Phase.Rotate;rotateLeft=RotationFrames;
        Projectile.velocity=Vector2.Zero;
    }
    public override bool? CanHitNPC(NPC target)=>phase==Phase.Return && !ReturnHits?false:null;
    public string KillReason=""; // 夹具诊断：记录收回期 Kill 的触发条件
    public string DebugState=>$"phase={phase} rotateLeft={rotateLeft} travel={travel:F0} vel={Projectile.velocity} timeLeft={Projectile.timeLeft} active={Projectile.active} {KillReason}";
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(phase==Phase.Fly)EnterRotation(); // 刺中后转入旋转
    }
    public override void ModifyHitNPC(NPC target,ref NPC.HitModifiers modifiers)
    {
        // 旋转期与印刃收回期都只结算一半伤害。
        if(phase==Phase.Rotate || phase==Phase.Return)modifiers.FinalDamage*=RotationDamage;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if(HostType==0)return true;
        var texture=TextureAssets.Item[HostType].Value;
        var origin=new Vector2(texture.Width*.5f,texture.Height*.5f);
        Main.EntitySpriteDraw(texture,Projectile.Center-Main.screenPosition,null,lightColor,
            Projectile.rotation,origin,Projectile.scale*.8f,Microsoft.Xna.Framework.Graphics.SpriteEffects.None);
        return false;
    }
}
