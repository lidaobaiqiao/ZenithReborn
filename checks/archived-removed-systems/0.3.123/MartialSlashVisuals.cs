using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;

namespace ZenithReborn;

// 这里只保存近期可见剑姿，不参与弹幕生成、伤害、碰撞、存档、网络或攻击计时。
public sealed class MartialSlashTrail
{
    public const int Lifetime=16, MaximumSamples=14;
    private readonly record struct Pose(Vector2 Pivot,float Angle,int Age);
    private readonly List<Pose> poses=new(MaximumSamples);
    private readonly List<Vector2> points=new(MaximumSamples);
    private readonly VertexStrip flowingStrip=new();
    private readonly VertexStrip ribbonStrip=new();
    private readonly Vector2[] flowPoints=new Vector2[64];
    private readonly float[] flowAngles=new float[64];
    public int Count=>poses.Count;

    public void Update(Vector2 pivot,float angle,bool slashing,bool fading)
    {
        if(!float.IsFinite(pivot.X) || !float.IsFinite(pivot.Y) || !float.IsFinite(angle))
        {poses.Clear();return;}
        if(!slashing && !fading){poses.Clear();return;}
        for(int i=poses.Count-1;i>=0;i--)
        {
            var pose=poses[i] with {Age=poses[i].Age+1};
            if(pose.Age>=Lifetime)poses.RemoveAt(i);else poses[i]=pose;
        }
        if(!slashing)return;
        if(poses.Count>0)
        {
            var last=poses[^1];
            float turn=MathHelper.WrapAngle(angle-last.Angle);
            // 瞬移、阶段重置或弹幕复用时不能跨越断点连接轨迹。
            if(Vector2.DistanceSquared(last.Pivot,pivot)>96*96 || MathF.Abs(turn)>.8f)poses.Clear();
            else if(MathF.Abs(turn)<.001f && Vector2.DistanceSquared(last.Pivot,pivot)<.25f)return;
        }
        if(poses.Count==MaximumSamples)poses.RemoveAt(0);
        poses.Add(new(pivot,angle,0));
    }

    private float Envelope(float progress)
    {
        float sample=progress*(poses.Count-1);
        int index=Math.Clamp((int)sample,0,poses.Count-1);
        float age=MathHelper.Lerp(poses[index].Age,poses[Math.Min(index+1,poses.Count-1)].Age,sample-index);
        return MathF.Pow(Math.Clamp(1-age/Lifetime,0,1),.8f)*MathF.Pow(progress,.4f);
    }
    private static float Taper(float progress)=>MathF.Pow(Math.Max(0,MathF.Sin(progress*MathHelper.Pi)),.55f);

    // 移动材质仍是原版彩虹魔杖 / MagicMissile 的现成 Effect 与贴图。
    // 光带全部改用原版 VertexStrip 绘制，不再依赖 Luminance 的轨迹渲染和泛光。
    public void DrawPrimitives(int tier,int? projectionWidth=null,int? projectionHeight=null,bool unscaled=false,float? visualTime=null)
    {
        if(poses.Count<3)return;   // 原版Shader在进入世界前已就绪，不再需要额外门控
        float clock=visualTime??Main.GlobalTimeWrappedHourly;
        Color head=SoulFlowStyle.Head(tier);
        Color tail=SoulFlowStyle.Tail(tier);
        // 第三方厚月牙主体已删除，目前仅保留原版流光与细刃。
        Ribbon(268,73,.085f,0);
        Flow(265,38,.3f,0);
        Flow(291,14,.38f,1);
        Ribbon(309,2.1f,1.15f,.95f);

        void Flow(float radius,float width,float intensity,int strand)
        {
            for(int i=0;i<flowPoints.Length;i++)
            {
                float t=i/(float)(flowPoints.Length-1);
                float sample=t*(poses.Count-1);int index=(int)sample;
                var a=poses[index];var b=poses[Math.Min(index+1,poses.Count-1)];float f=sample-index;
                float angle=a.Angle+MathHelper.WrapAngle(b.Angle-a.Angle)*f;
                // 细微的不规则边缘区分流动细丝；扫掠始终跟随武器的真实姿态。
                float ripple=MathF.Sin(t*21-clock*12+strand*2)*MathF.Sin(t*MathHelper.Pi)*(strand==0?3:5);
                flowPoints[i]=Vector2.Lerp(a.Pivot,b.Pivot,f)+angle.ToRotationVector2()*(radius+ripple);
            }
            for(int i=0;i<flowPoints.Length;i++)
            {
                Vector2 delta=i==flowPoints.Length-1?flowPoints[i]-flowPoints[i-1]:flowPoints[i+1]-flowPoints[i];
                flowAngles[i]=delta.ToRotation();
            }
            var shader=MartialSlashRenderer.FlowShader;
            shader.UseSaturation(-1.6f).UseOpacity(2.2f).Apply();
            shader.Shader.Parameters["uTime"]?.SetValue(clock+strand*.13f);
            shader.Shader.Parameters["uWorldViewProjection"]?.SetValue(
                (unscaled?Matrix.Identity:Main.GameViewMatrix.TransformationMatrix)*
                Matrix.CreateOrthographicOffCenter(0,projectionWidth??Main.screenWidth,projectionHeight??Main.screenHeight,0,-1,1));
            shader.Shader.CurrentTechnique.Passes["MagicMissile"].Apply();
            Main.graphics.GraphicsDevice.BlendState=BlendState.AlphaBlend;
            flowingStrip.PrepareStrip(flowPoints,flowAngles,t=>{
                Color color=Color.Lerp(tail,head,t)*(intensity*Envelope(t));color.A=0;return color;
            },t=>width*Taper(t),-Main.screenPosition,flowPoints.Length,true);
            flowingStrip.DrawTrail();
            Main.graphics.GraphicsDevice.BlendState=BlendState.Additive;
        }

        void Ribbon(float radius,float width,float intensity,float white)
        {
            points.Clear();
            foreach(var pose in poses)points.Add(pose.Pivot+pose.Angle.ToRotationVector2()*radius);
            // 逐点朝向：由相邻点差分得到，交给原版 VertexStrip 画成一条平滑轨迹
            for(int i=0;i<points.Count;i++)
            {
                Vector2 delta=i==points.Count-1?points[i]-points[i-1]:points[i+1]-points[i];
                flowAngles[i]=delta.ToRotation();
            }
            Vector2 offset=unscaled?Vector2.Zero:-Main.screenPosition;
            ribbonStrip.PrepareStrip(points.ToArray(),flowAngles,
                t=>{
                    Color color=Color.Lerp(Color.Lerp(tail,head,t),Color.White,white)*(intensity*Envelope(t));
                    color.A=255;return color;
                },t=>width*Taper(t),offset,points.Count,true);
            ribbonStrip.DrawTrail();
        }
    }

    public void DrawSwordGhosts(int tier)
    {
        if(poses.Count<3)return;
        Main.instance.LoadItem(ItemID.Zenith);
        var texture=TextureAssets.Item[ItemID.Zenith].Value;
        Color tint=tier==3?MartialVisual.Violet:MartialVisual.Cyan;
        for(int i=poses.Count-3;i>=0;i-=3)
        {
            var pose=poses[i];
            float fade=MathF.Pow(Math.Clamp(1-pose.Age/(float)Lifetime,0,1),2)*.25f;
            Vector2 axis=pose.Angle.ToRotationVector2();
            Vector2 at=pose.Pivot+axis*145-Main.screenPosition;
            for(int side=-1;side<=1;side+=2)
            {
                Color split=(side<0?new Color(233,77,255):new Color(65,224,255))*fade;split.A=0;
                Main.EntitySpriteDraw(texture,at+axis.RotatedBy(MathHelper.PiOver2)*side*3,null,split,
                    pose.Angle+MathHelper.PiOver4,texture.Size()/2,300f/Math.Max(texture.Width,texture.Height),SpriteEffects.None);
            }
            Vector2 spark=pose.Pivot+axis*(278+MathF.Sin(i*2.7f)*20);
            OreSwordEffects.Star(spark,tint,.25f+fade,.55f*fade,pose.Angle);
            Vector2 drift=axis.RotatedBy(MathHelper.PiOver2)*(5+pose.Age*1.6f);
            OreSwordEffects.Line(spark-drift,spark-drift-axis*(8+pose.Age*.7f),tint,1.4f,fade*.9f);
        }
        var head=poses[^1];
        float remaining=Math.Clamp(1-head.Age/(float)Lifetime,0,1);
        OreSwordEffects.Star(head.Pivot+head.Angle.ToRotationVector2()*269,tint,.85f,.85f*remaining,head.Angle);
    }
}

// 在实体之前绘制一次，让真正的剑身永远清晰地压在剑弧上面。
// 这里会切换自定义 Shader 与混合状态，画完必须恢复，避免污染原版和其他模组。
[Autoload(Side=ModSide.Client)]
public class MartialSlashRenderer : ModSystem
{
    private static Effect flowEffect;
    private static MiscShaderData flowShader;
    public static MiscShaderData FlowShader
    {
        get
        {
            if(flowShader!=null)return flowShader;
            // 克隆原版现成的 Effect：这样我们的时间/矩阵/透明度参数
            // 永远不会改到别的武器共用的 RainbowRod Shader 设置。
            flowEffect=GameShaders.Misc["RainbowRod"].Shader.Clone();
            // 这个仅 1.4.4 可用的构造函数允许我们持有私有 Effect 副本；
            // 若直接引用共享 Asset，参数会互相串味。
#pragma warning disable CS0618
            flowShader=new MiscShaderData(new Ref<Effect>(flowEffect),"MagicMissile")
                .UseProjectionMatrix(true).UseImage0(TextureAssets.Extra[ExtrasID.RainbowRodTrailColorGradient])
                .UseImage1(TextureAssets.Extra[ExtrasID.RainbowRodTrailShape])
                .UseImage2(TextureAssets.Extra[ExtrasID.RainbowRodTrailErosion]);
#pragma warning restore CS0618
            return flowShader;
        }
    }
    public static void DrawWithRestoredState(Action draw)
    {
        var device=Main.graphics.GraphicsDevice;
        var blend=device.BlendState;var depth=device.DepthStencilState;var raster=device.RasterizerState;
        var scissor=device.ScissorRectangle;
        var samplers=Enumerable.Range(0,3).Select(i=>device.SamplerStates[i]).ToArray();
        var textures=Enumerable.Range(0,3).Select(i=>device.Textures[i]).ToArray();
        var vertices=device.GetVertexBuffers();var indices=device.Indices;
        try
        {
            device.BlendState=BlendState.Additive;
            device.DepthStencilState=DepthStencilState.None;
            draw();
        }
        finally
        {
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
            device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=raster;
            device.ScissorRectangle=scissor;
            for(int i=0;i<3;i++){device.SamplerStates[i]=samplers[i];device.Textures[i]=textures[i];}
            device.SetVertexBuffers(vertices);device.Indices=indices;
        }
    }
    public override void PostDrawTiles()
    {
        if(Main.gameMenu)return;
        bool visible=false;
        foreach(var projectile in Main.ActiveProjectiles)
            if(projectile.ModProjectile is MartialGiant g && g.SlashTrail.Count>=3){visible=true;break;}
        if(!visible)return;
        DrawWithRestoredState(()=>{
            foreach(var projectile in Main.ActiveProjectiles)
                if(projectile.ModProjectile is MartialGiant giant)giant.SlashTrail.DrawPrimitives(giant.Tier);
        });
    }
    public override void Unload()
    {
        var effect=flowEffect;flowEffect=null;flowShader=null;
        if(effect!=null)Main.QueueMainThreadAction(effect.Dispose);
    }
}
