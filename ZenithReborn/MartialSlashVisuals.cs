using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;

namespace ZenithReborn;

// Only the recent visible sword poses live here. No projectile spawning,
// damage, collision, saved data, networking or attack clock is involved.
public sealed class MartialSlashTrail
{
    public const int Lifetime=16, MaximumSamples=14;
    private readonly record struct Pose(Vector2 Pivot,float Angle,int Age);
    private readonly List<Pose> poses=new(MaximumSamples);
    private readonly List<Vector2> points=new(MaximumSamples);
    private readonly VertexStrip flowingStrip=new();
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
            // Never bridge a teleport, a phase reset or a reused projectile.
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

    // The moving material is the existing vanilla Rainbow Rod / MagicMissile
    // effect and textures. Luminance still handles the quiet bloom and edge.
    public void DrawPrimitives(int tier,int? projectionWidth=null,int? projectionHeight=null,bool unscaled=false,float? visualTime=null)
    {
        if(poses.Count<3 || !ShaderManager.HasFinishedLoading)return;
        float clock=visualTime??Main.GlobalTimeWrappedHourly;
        Color head=SoulFlowStyle.Head(tier);
        Color tail=SoulFlowStyle.Tail(tier);
        float span=0;
        for(int i=1;i<poses.Count;i++)span+=MathHelper.WrapAngle(poses[i].Angle-poses[i-1].Angle);
        Ribbon(268,73,.085f,0);
        MartialCrescentMaterial.Draw(poses[^1].Pivot,poses[0].Angle,span,poses[^1].Age,tier,clock,
            projectionWidth,projectionHeight,unscaled);
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
                // A small irregular edge separates the travelling filaments;
                // the weapon's real pose remains the source of the sweep.
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
            PrimitiveRenderer.RenderTrail(points,new(
                t=>width*Taper(t),
                t=>{
                    Color color=Color.Lerp(Color.Lerp(tail,head,t),Color.White,white)*(intensity*Envelope(t));
                    color.A=255;return color;
                },Smoothen:poses.Count>4,ProjectionAreaWidth:projectionWidth,ProjectionAreaHeight:projectionHeight,
                UseUnscaledMatrix:unscaled),48);
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

// Draw once before entities, so the real blade remains crisp above its arc.
// Luminance changes the GPU pipeline; restore it for vanilla and other mods.
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
            // Clone the ready-made native effect so our time/matrix/opacity
            // never alter another weapon's shared RainbowRod shader settings.
            flowEffect=GameShaders.Misc["RainbowRod"].Shader.Clone();
            // This 1.4.4-only constructor permits ownership of a private Effect
            // clone. A shared Asset reference would leak our parameters.
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
        if(Main.gameMenu || !ShaderManager.HasFinishedLoading)return;
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
        MartialCrescentMaterial.Unload();
        var effect=flowEffect;flowEffect=null;flowShader=null;
        if(effect!=null)Main.QueueMainThreadAction(effect.Dispose);
    }
}
