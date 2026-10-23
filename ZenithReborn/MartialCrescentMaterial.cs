using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// Adapter for HoCha113's ready-made MIT crescent material. The unchanged
// shader, compiled effect, brush/noise and license live in Assets/ThirdParty.
// This submits one standard textured quad; no new mesh/rendering framework.
internal static class MartialCrescentMaterial
{
    private const string Root="ZenithReborn/Assets/ThirdParty/CalamityOverhaul/";
    private static Effect effect;
    private static Texture2D brush,noise;
    private static readonly VertexPositionColorTexture[] quad=new VertexPositionColorTexture[4];

    internal static void Draw(Vector2 pivot,float startAngle,float signedSpan,float newestAge,int tier,
        float clock,int? width,int? height,bool unscaled)
    {
        float span=MathF.Abs(signedSpan);
        float remaining=Math.Clamp(1-newestAge/MartialSlashTrail.Lifetime,0,1);
        if(span<.06f || remaining<=0)return;
        EnsureLoaded();
        var p=effect.Parameters;
        p["transformMatrix"].SetValue((unscaled?Matrix.Identity:Main.GameViewMatrix.TransformationMatrix)*
            Matrix.CreateOrthographicOffCenter(0,width??Main.screenWidth,height??Main.screenHeight,0,-1,1));
        p["uTime"].SetValue(clock);
        p["uBrushTex"].SetValue(brush);p["uNoiseTex"].SetValue(noise);
        p["uMode"].SetValue(0f);p["uSweep"].SetValue(1f);
        p["uArcSpan"].SetValue(span);p["uFlip"].SetValue(MathF.Sign(signedSpan));
        p["uThick"].SetValue(tier==3?.34f:.30f);
        p["uOpacity"].SetValue(MathF.Pow(remaining,.65f)*.98f);
        p["uErode"].SetValue(MathF.Pow(1-remaining,1.3f)*.95f);
        p["uTailErode"].SetValue((1-remaining)*.5f);
        p["uFlash"].SetValue(.08f*remaining);
        p["uFlowPhase"].SetValue(clock*.9f);
        p["uColorShift"].SetValue(0f);p["uSeed"].SetValue(.237f);
        p["uFrontGlow"].SetValue(.8f*remaining);p["uRazorTailWiden"].SetValue(.75f);
        p["uFarSel"].SetValue(0f);p["uFarDim"].SetValue(1f);p["uFarDirLocal"].SetValue(Vector2.Zero);
        p["uInk"].SetValue(0f);p["uFeiBai"].SetValue(.12f);p["uBleed"].SetValue(0f);p["uSplitTail"].SetValue(.35f);
        p["uColHot"].SetValue(new Vector3(1.65f,1.8f,1.95f));
        p["uColBright"].SetValue(SoulFlowStyle.Head(tier).ToVector3()*1.3f);
        p["uColDeep"].SetValue(SoulFlowStyle.Tail(tier).ToVector3()*.7f);
        p["uColDark"].SetValue(SoulFlowStyle.Tail(tier).ToVector3()*.08f);

        Vector2 x=(startAngle+signedSpan*.5f).ToRotationVector2()*(310f/.9f);
        Submit(pivot-Main.screenPosition,x,x.RotatedBy(MathHelper.PiOver2));
    }
    private static void EnsureLoaded()
    {
        if(effect==null)
        {
            // Created on first draw, on the graphics thread. Never mutate the
            // shared loaded Effect, nor the other mod's runtime shader instance.
            effect=ModContent.Request<Effect>(Root+"OniCrimsonSlash",AssetRequestMode.ImmediateLoad).Value.Clone();
            brush=ModContent.Request<Texture2D>(Root+"SlashBrush01",AssetRequestMode.ImmediateLoad).Value;
            noise=ModContent.Request<Texture2D>(Root+"NoiseSoft01",AssetRequestMode.ImmediateLoad).Value;
        }
    }
    private static void SetupOre(Color tint,float opacity,float erosion,float clock,int? width,int? height,bool unscaled)
    {
        EnsureLoaded();var p=effect.Parameters;
        p["transformMatrix"].SetValue((unscaled?Matrix.Identity:Main.GameViewMatrix.TransformationMatrix)*
            Matrix.CreateOrthographicOffCenter(0,width??Main.screenWidth,height??Main.screenHeight,0,-1,1));
        p["uTime"].SetValue(clock);
        p["uBrushTex"].SetValue(brush);p["uNoiseTex"].SetValue(noise);
        p["uMode"].SetValue(0f);p["uSweep"].SetValue(1f);
        p["uArcSpan"].SetValue(1f);p["uFlip"].SetValue(1f);
        p["uOpacity"].SetValue(Math.Clamp(opacity,0,1));
        p["uErode"].SetValue(Math.Clamp(erosion,0,1));
        p["uTailErode"].SetValue(erosion*.4f);
        p["uFlash"].SetValue(0f);
        p["uFlowPhase"].SetValue(clock*.9f);
        p["uColorShift"].SetValue(0f);p["uSeed"].SetValue(.237f);
        p["uFrontGlow"].SetValue(.25f);p["uRazorTailWiden"].SetValue(.35f);
        p["uFarSel"].SetValue(0f);p["uFarDim"].SetValue(1f);p["uFarDirLocal"].SetValue(Vector2.Zero);
        p["uInk"].SetValue(0f);p["uFeiBai"].SetValue(.12f);p["uBleed"].SetValue(0f);p["uSplitTail"].SetValue(.35f);
        p["uColHot"].SetValue(Color.Lerp(tint,Color.White,.8f).ToVector3()*1.55f);
        p["uColBright"].SetValue(tint.ToVector3()*1.25f);
        p["uColDeep"].SetValue(tint.ToVector3()*.55f);
        p["uColDark"].SetValue(tint.ToVector3()*.06f);
    }
    // One existing compiled pass: low base color plus rolling brush/noise and
    // a moving erosion threshold. No extra white core, arc or V layer.
    internal static void OreFlame(Vector2 from,Vector2 to,float halfWidth,Color tint,float opacity,float dissolve,
        float clock,int? width=null,int? height=null,bool unscaled=false)
    {
        Vector2 delta=to-from;float length=delta.Length();
        if(!float.IsFinite(length)||length<2||length>900||halfWidth<=0||opacity<=0)return;
        SetupOre(tint,opacity,0,clock*3.4f,width,height,unscaled);
        var p=effect.Parameters;float seed=.17f+tint.R*.0017f+tint.B*.0009f;
        p["uMode"].SetValue(1f);p["uThick"].SetValue(.64f);
        p["uSeed"].SetValue(seed);p["uFlowPhase"].SetValue(clock*2.3f);
        p["uErode"].SetValue(Math.Clamp(.43f+.10f*MathF.Sin(clock*9+seed*4)+dissolve*.85f,0,1));
        p["uTailErode"].SetValue(.56f+dissolve*.4f);
        p["uInk"].SetValue(.42f);p["uFeiBai"].SetValue(.5f);
        p["uBleed"].SetValue(.16f);p["uSplitTail"].SetValue(.85f);
        p["uFrontGlow"].SetValue(0f);p["uRazorTailWiden"].SetValue(0f);
        p["uColHot"].SetValue(tint.ToVector3()*.65f+new Vector3(.08f));
        p["uColBright"].SetValue(tint.ToVector3()*.60f);
        p["uColDeep"].SetValue(tint.ToVector3()*.24f);
        p["uColDark"].SetValue(tint.ToVector3()*.06f);
        Vector2 x=delta*.5f,y=delta/length*halfWidth*1.65f;
        Submit((from+to)*.5f-Main.screenPosition,x,y.RotatedBy(MathHelper.PiOver2));
    }
    private static void Submit(Vector2 center,Vector2 x,Vector2 y)
    {
        quad[0]=new(new Vector3(center-x-y,0),Color.White,new(0,0));
        quad[1]=new(new Vector3(center+x-y,0),Color.White,new(1,0));
        quad[2]=new(new Vector3(center-x+y,0),Color.White,new(0,1));
        quad[3]=new(new Vector3(center+x+y,0),Color.White,new(1,1));
        var device=Main.graphics.GraphicsDevice;
        device.BlendState=BlendState.AlphaBlend;device.RasterizerState=RasterizerState.CullNone;
        foreach(var pass in effect.CurrentTechnique.Passes)
        {pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleStrip,quad,0,2);}
        device.BlendState=BlendState.Additive;
    }
    internal static void Unload()
    {var old=effect;effect=null;brush=null;noise=null;if(old!=null)Main.QueueMainThreadAction(old.Dispose);}
}
