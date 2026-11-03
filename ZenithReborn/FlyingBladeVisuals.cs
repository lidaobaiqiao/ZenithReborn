using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace ZenithReborn;

// 飞刃弹幕的纯绘制层：飞行拖尾、旋转环影、中心光点、命中光环、尾部火花与命中爆发。
// 只由 FlyingBladeShot 调用；不保存战斗状态、不产生弹幕、不改伤害与免疫。
// 所有队列在构造时一次性分配，Update/Draw 路径里不做任何分配，也不使用 LINQ。
// 三个渲染开关全部来自 VfxIsolation，发射与绘制两处都逐项判断，类内不写死。
public sealed class FlyingBladeVisuals
{
    // ---------- 数量与节奏常量 ----------
    public const int TrailMax=6;                     // 拖尾最大段数（收回期用满）
    public const int TrailFlightSegments=4;          // 直飞期只用前 4 段
    public const int TrailFlightInterval=2;          // 直飞期每 2 帧采样一次
    public const int TrailReturnInterval=1;          // 收回期每帧采样，速度高才有拉伸感
    // 旋转自转是 TwoPi/15（24°/帧），环影步长必须与它一致，否则一帧就过阈值、15 段能记出 576°。
    // 15 段 × 24° = 正好一整圈；寿命取 24 帧，最老一段在被覆盖前已淡到约 18%，接缝不明显。
    public const int EchoMax=15;                     // 旋转环影最大段数（15×24°=一整圈）
    public const float EchoStep=MathHelper.TwoPi/15f; // 采样间隔 = 每帧自转角 24°
    public const int EchoLife=24;                    // 环影寿命，短于旋转期（45帧），尾巴更快消失
    public const float EchoStepTolerance=.98f;       // 步长比较容差，避免浮点误差把采样退化成隔帧
    public const int HaloLife=12;                    // 命中光环 12 帧 = 0.2 秒
    public const float HaloStart=.55f;               // 光环起始缩放
    public const float HaloEnd=1.5f;                 // 光环终止缩放
    public const float HaloRadius=30f;               // 光环基准半径（像素）
    public const int HaloSegments=20;                // 细环分段数
    public const int SparkInterval=4;                // 尾部火花间隔帧
    public const int BurstCount=8;                   // 命中爆发 dust 数（规格 6~10）
    public const float BladeScale=.8f;               // 刀刃绘制缩放，沿用旧值
    public const float AxisTolerance=1.2f;           // 长宽比容差：超过即按长轴取正向
    private const float TrailAlphaFlight=.5f;        // 直飞拖尾亮度
    private const float TrailAlphaReturn=.8f;        // 收回拖尾更亮
    private const float EchoAlpha=.26f;              // 环影单段亮度
    private const float CoreRadius=26f;              // 中心光点半径
    private const float CoreAlpha=.5f;               // 中心光点亮度
    private const float HaloFillAlpha=.45f;          // 光环内填充亮度
    private const float HaloRingAlpha=.6f;           // 光环细边亮度
    private const float HaloRingWidth=1.4f;          // 光环细边宽度
    private const float LightPower=.42f;             // 刃身光照强度
    private const float CoolWhiteMix=.45f;           // 配色向冷白推进的比例
    private const float BodyBrighten=.3f;            // 刃身本体向白色提亮的比例
    private static readonly Color CoolWhite=new(235,242,255);

    // ---------- 每把飞刃自己的队列（构造时分配，运行期不再 new） ----------
    private readonly Vector2[] trailAt=new Vector2[TrailMax];
    private readonly float[] trailRot=new float[TrailMax];
    private int trailCount;
    private int trailTimer;

    private readonly float[] echoRot=new float[EchoMax];
    private readonly int[] echoAge=new int[EchoMax];
    private int echoCount;
    private int echoCursor;     // 环形缓冲写入位置，满了就覆盖最老的一段
    private float echoSpin;     // 自上次采样后累计的旋转角
    private float lastRotation;
    private int spinAge;        // 旋转期帧计数，只用于光点呼吸

    private int sparkTimer;
    private Vector2 haloAt;
    private int haloAge=HaloLife;   // 等于 HaloLife 表示当前没有光环

    private FlyingBladeShot.Stage lastStage=FlyingBladeShot.Stage.Fly;
    private int tintHost=-1;
    private Color tint=Color.White;

    // 刀刃朝向：速度方向对齐贴图长轴。服务器不加载贴图，这里直接跳过。
    public void Orient(Projectile projectile,int hostType)
    {
        if(Main.dedServ||!ValidHost(hostType))return;
        if(projectile.velocity.LengthSquared()<.01f)return;
        var blade=TextureAssets.Item[hostType].Value;
        if(blade is null||blade.Width<=0)return;
        projectile.rotation=projectile.velocity.ToRotation()-Forward(blade);
    }

    // ai[0] 是随网络同步的裸 int，异常值（0、负数、越界）不能直接拿去索引贴图数组。
    public static bool ValidHost(int hostType)
        => hostType>0 && hostType<TextureAssets.Item.Length;

    // 原版剑类贴图把剑尖画在右上 45°，所以方形贴图的"剑尖正向"是右上对角线；
    // 这条约定与项目内 OreSwordEffects.Blade 的 +PiOver4 一致——直接删掉 45° 会让剑侧着飞。
    // 只有长宽比明显偏离正方形的贴图才改按长轴取正向：竖版剑尖朝上，横版剑尖朝右。
    public static float Forward(Texture2D blade)
    {
        if(blade.Height>blade.Width*AxisTolerance)return -MathHelper.PiOver2;
        if(blade.Width>blade.Height*AxisTolerance)return 0f;
        return -MathHelper.PiOver4;
    }

    // 加法混合下 alpha 不能再参与乘法：BlendState.Additive 是 SourceAlpha/One，
    // 颜色自带的 alpha 会被 blend 再乘一次，等效亮度变成 alpha²（0.5→0.25、0.26→0.07），
    // 亮背景下几乎看不见。所以这里统一把 alpha 顶回 255，只让 RGB 承担亮度——
    // 这样下面那些亮度常量的数值就是真实亮度，后续调参不会再踩同一个坑。
    private static Color Glow(Color color)
    {
        color.A=255;
        return color;
    }

    // 每帧由 FlyingBladeShot.AI 调用：记录姿态、推进光环寿命、放尾部火花。
    public void Update(Projectile projectile,int hostType,FlyingBladeShot.Stage stage)
    {
        // 服务端只跑行为逻辑，不产生任何客户端视觉数据。
        if(Main.dedServ)return;
        Color now=ColorOf(hostType);
        if(stage!=lastStage)
        {
            trailCount=0;trailTimer=0;   // 换阶段就换一条新拖尾，避免跨阶段连线
            if(stage==FlyingBladeShot.Stage.Rotate)
            {
                echoCount=0;echoCursor=0;echoSpin=0;spinAge=0;lastRotation=projectile.rotation;
            }
            lastStage=stage;
        }
        switch(stage)
        {
            case FlyingBladeShot.Stage.Fly:
                RecordTrail(projectile,TrailFlightSegments,TrailFlightInterval);
                break;
            case FlyingBladeShot.Stage.Rotate:
                RecordEcho(projectile);
                spinAge++;
                break;
            default:
                RecordTrail(projectile,TrailMax,TrailReturnInterval);
                break;
        }
        if(haloAge<HaloLife)haloAge++;
        if(stage!=FlyingBladeShot.Stage.Rotate&&++sparkTimer>=SparkInterval)
        {sparkTimer=0;EmitSpark(projectile,now);}
        if(VfxIsolation.FlyingBladeGlow)
            Lighting.AddLight(projectile.Center,now.ToVector3()*LightPower);
    }

    // 命中反馈：直击与收回期都给一次爆发；旋转期是持续伤害，只在转入旋转的那一次爆。
    // 已知取舍：命中结算只在弹幕 owner 端跑 OnHitNPC，所以光环与命中爆发只有发起端看得到；
    // 尾部火花写在 AI 里，所有客户端都能看见。要让旁观者也看到命中特效必须改同步，不属本轮。
    public void Hit(Projectile projectile,int hostType,FlyingBladeShot.Stage stage)
    {
        if(Main.dedServ||stage==FlyingBladeShot.Stage.Rotate)return;
        if(VfxIsolation.FlyingBladeGlow){haloAt=projectile.Center;haloAge=0;}
        if(VfxIsolation.FlyingBladeImpact)EmitBurst(projectile,ColorOf(hostType));
    }

    // 绘制入口：特效走加法混合并压在刀刃下面，刀刃本体最后画。
    public void Draw(Projectile projectile,Texture2D blade,int hostType,
        FlyingBladeShot.Stage stage,Color lightColor)
    {
        if(blade is null||blade.Width<=0)return;
        Color now=ColorOf(hostType);
        Vector2 origin=blade.Size()*.5f;
        float scale=projectile.scale*BladeScale;
        Vector2 pos=projectile.Center-Main.screenPosition;
        bool trailOn=VfxIsolation.FlyingBladeTrail&&trailCount>0;
        // 旋转期不采样拖尾（trailCount 被清零），环影必须自己一支条件，
        // 否则它会跟着 trailOn 一起恒假，整个旋转期一段都画不出来。
        bool echoOn=VfxIsolation.FlyingBladeTrail&&stage==FlyingBladeShot.Stage.Rotate&&echoCount>0;
        bool glowOn=VfxIsolation.FlyingBladeGlow&&
            (stage==FlyingBladeShot.Stage.Rotate||haloAge<HaloLife);
        if(trailOn||echoOn||glowOn)
        {
            // 加法混合：残影是"武器留下的光"，AlphaBlend 低透明度只会画成一团灰。
            // 已知取舍：在 PreDraw 内 End/Begin 会打断原版批次，理论上可能与 CurrentDrawnEntityShader
            // 状态机错序。彻底解法是整体挪到 On_Main.DrawDust 走一次加法通道，属较大重构，本轮不做。
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.Additive,Main.DefaultSamplerState,
                DepthStencilState.None,Main.Rasterizer,null,Main.GameViewMatrix.TransformationMatrix);
            if(trailOn)DrawTrail(blade,origin,scale,stage,now);
            if(echoOn)DrawEchoes(blade,pos,origin,scale,now);
            if(glowOn)
            {
                DrawCore(pos,stage,now);
                DrawHalo(now);
            }
            Main.spriteBatch.End();
            // 还原成原版弹幕批次参数（Main.DrawProjectiles 使用的同一组）。
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,Main.DefaultSamplerState,
                DepthStencilState.None,Main.Rasterizer,null,Main.GameViewMatrix.TransformationMatrix);
        }
        Main.spriteBatch.Draw(blade,pos,null,Color.Lerp(lightColor,Color.White,BodyBrighten),
            projectile.rotation,origin,scale,SpriteEffects.None,0);
    }

    private void RecordTrail(Projectile projectile,int segments,int interval)
    {
        if(++trailTimer<interval)return;
        trailTimer=0;
        int last=Math.Min(trailCount,segments-1);
        for(int i=last;i>0;i--){trailAt[i]=trailAt[i-1];trailRot[i]=trailRot[i-1];}
        trailAt[0]=projectile.Center;
        trailRot[0]=projectile.rotation;
        if(trailCount<segments)trailCount++;
    }

    private void RecordEcho(Projectile projectile)
    {
        // 每帧推进一次寿命，采样与否都算，环影的淡出才不会卡住。
        for(int i=0;i<echoCount;i++)if(echoAge[i]>0)echoAge[i]--;
        float delta=projectile.rotation-lastRotation;
        lastRotation=projectile.rotation;
        echoSpin+=delta;
        // 一圈正好是 15 帧、每帧 24°，所以这里的比较必须留 2% 容差：
        // 浮点累加出来的 delta 可能比常量小 1 个 ulp，卡在阈值上会退化成隔帧采样（只剩 7~8 段）。
        if(echoSpin<EchoStep*EchoStepTolerance)return;
        echoSpin=0;
        echoRot[echoCursor]=projectile.rotation;
        echoAge[echoCursor]=EchoLife;
        echoCursor=echoCursor+1>=EchoMax?0:echoCursor+1;
        if(echoCount<EchoMax)echoCount++;
    }

    private void DrawTrail(Texture2D blade,Vector2 origin,float scale,FlyingBladeShot.Stage stage,Color now)
    {
        int segments=stage==FlyingBladeShot.Stage.Return?TrailMax:TrailFlightSegments;
        int count=Math.Min(trailCount,segments);
        float baseAlpha=stage==FlyingBladeShot.Stage.Return?TrailAlphaReturn:TrailAlphaFlight;
        for(int i=count-1;i>=0;i--)
        {
            // 越靠后越淡，长度按整条拖尾归一，收尾不会突然截断。
            float falloff=1f-i/(float)TrailMax;
            Color color=Glow(now*(baseAlpha*falloff*falloff));
            Main.spriteBatch.Draw(blade,trailAt[i]-Main.screenPosition,null,color,
                trailRot[i],origin,scale*(1f-.05f*i),SpriteEffects.None,0);
        }
    }

    // 调用前由 Draw 判过 stage==Rotate 与 echoCount>0，这里只负责按寿命画。
    private void DrawEchoes(Texture2D blade,Vector2 pos,Vector2 origin,float scale,Color now)
    {
        for(int i=0;i<echoCount;i++)
        {
            int age=echoAge[i];
            if(age<=0)continue;
            float life=age/(float)EchoLife;
            // 每段环影按各自寿命淡出，最老的先消失，转出来的是一圈渐隐的扇形。
            Color color=Glow(now*(EchoAlpha*life*life));
            Main.spriteBatch.Draw(blade,pos,null,color,echoRot[i],origin,scale*.97f,SpriteEffects.None,0);
        }
    }

    private void DrawCore(Vector2 pos,FlyingBladeShot.Stage stage,Color now)
    {
        if(stage!=FlyingBladeShot.Stage.Rotate)return;
        var glow=GlowTexture();
        if(glow is null)return;
        // 呼吸：旋转期亮度与尺寸各自缓慢起伏，避免中心是个死点。
        float pulse=.8f+.2f*MathF.Sin(spinAge*.28f);
        float scale=CoreRadius*2f/glow.Width*pulse;
        Main.spriteBatch.Draw(glow,pos,null,Glow(now*(CoreAlpha*pulse)),0,glow.Size()*.5f,scale,
            SpriteEffects.None,0);
    }

    private void DrawHalo(Color now)
    {
        if(haloAge>=HaloLife)return;
        var glow=GlowTexture();
        if(glow is null)return;
        float progress=haloAge/(float)HaloLife;
        float scale=HaloStart+(HaloEnd-HaloStart)*progress;
        float fade=(1f-progress)*(1f-progress);
        float radius=HaloRadius*scale;
        Vector2 at=haloAt-Main.screenPosition;
        Main.spriteBatch.Draw(glow,at,null,Glow(now*(HaloFillAlpha*fade)),0,glow.Size()*.5f,
            radius*2f/glow.Width,SpriteEffects.None,0);
        Color edge=Glow(Color.Lerp(now,Color.White,.75f)*(HaloRingAlpha*fade));
        for(int i=0;i<HaloSegments;i++)
        {
            float a0=MathHelper.TwoPi*i/HaloSegments;
            float a1=MathHelper.TwoPi*(i+1)/HaloSegments;
            DrawLine(at+a0.ToRotationVector2()*radius,at+a1.ToRotationVector2()*radius,edge,HaloRingWidth);
        }
    }

    private static void DrawLine(Vector2 from,Vector2 to,Color color,float width)
    {
        Vector2 delta=to-from;
        float length=delta.Length();
        if(length<.01f)return;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,from,new Rectangle(0,0,1,1),color,
            delta.ToRotation(),new Vector2(0f,.5f),new Vector2(length,width),SpriteEffects.None,0);
    }

    // 原版星光柱的软圆光斑（72×72），只读引用，不新增本模组贴图。
    private static Texture2D GlowTexture()
    {
        var asset=TextureAssets.Projectile[ProjectileID.StardustTowerMark];
        return asset?.Value;
    }

    private Color ColorOf(int hostType)
    {
        if(hostType<=0)return CoolWhite;   // 未寄宿（兜底贴图）时不取色
        if(tintHost==hostType)return tint;
        tintHost=hostType;
        // 寄宿武器的既有配色再推向冷白：飞刃是武器留下的光，不是原色贴图。
        tint=Color.Lerp(SoulVisuals.Tint(hostType),CoolWhite,CoolWhiteMix);
        return tint;
    }

    private void EmitSpark(Projectile projectile,Color now)
    {
        if(!VfxIsolation.FlyingBladeTrail)return;
        Vector2 back=-projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 at=projectile.Center+back*(projectile.width*.5f+2f)+Main.rand.NextVector2Circular(3f,3f);
        int type=Main.rand.NextBool(4)?DustID.Platinum:DustID.Silver;
        var dust=Dust.NewDustPerfect(at,type,
            back*Main.rand.NextFloat(.5f,1.5f)+Main.rand.NextVector2Circular(1.1f,1.1f),
            0,now,Main.rand.NextFloat(.6f,.95f));
        dust.noGravity=true;
    }

    private static void EmitBurst(Projectile projectile,Color now)
    {
        for(int i=0;i<BurstCount;i++)
        {
            float angle=MathHelper.TwoPi*i/BurstCount+Main.rand.NextFloat(-.18f,.18f);
            Vector2 dir=angle.ToRotationVector2();
            var dust=Dust.NewDustPerfect(projectile.Center+dir*5f,
                i%3==0?DustID.Platinum:DustID.Silver,
                dir*Main.rand.NextFloat(2.2f,4.6f),0,now,Main.rand.NextFloat(.7f,1.1f));
            dust.noGravity=true;
        }
    }
}
