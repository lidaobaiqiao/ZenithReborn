using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace ZenithPrototypeCheck;

// Selected R2 vector-mask authoring only. The frozen atlas is the visual baseline.
// SpriteBatch supplies the native GPU renderer; this recipe only defines Alpha.
// Any sword may reuse the R2 recipe family: geometry (tip, widths) is either given
// explicitly or auto-fitted from the vanilla sprite, so one baseline serves all
// ore swords without re-drawing the frozen TitaniumSword R2 look.
internal sealed class SwordBodyVectorMask : IDisposable
{
    // Frozen R2 baseline canvas geometry (TitaniumSword R2 recipe):
    // 448x256 canvas, pivot at [305,128], tip at [408,128]; 103 canvas px equal the
    // 1.85x in-game blade length of TitaniumSword (reach 78.49 world px at 1.85x).
    private const float BaselinePivotX=305f,BaselinePivotY=128f;
    private const float BaselineBladeCanvasPx=103f;
    private const float BaselineBladeWorldPx=103f/1.3122883f;
    private const float AtlasPerWorldPx=BaselineBladeCanvasPx/BaselineBladeWorldPx;

    private readonly Texture2D profile;
    private readonly float bodyWidth,tailWidth,loop,waveStart,waveEnd,waveHeight,frontWidth,backWidth,topPhase,bottomPhase,leadingExtension;
    private readonly Vector2 tip,pivot;
    public Vector2 Tip=>tip;
    public Vector2 Pivot=>pivot;
    public float BladeCanvasPx=>tip.X-pivot.X;
    public SwordBodyVectorMask(GraphicsDevice device,string path,Texture2D blade=null)
    {
        using var json=JsonDocument.Parse(File.ReadAllText(path));var root=json.RootElement;
        if(!root.TryGetProperty("family",out var family)||family.GetString()!="R2")throw new InvalidOperationException("Only the user-preferred R2 recipe family is active. Later hook studies are withdrawn.");
        bool autoFit=root.TryGetProperty("autoFit",out var fit)&&fit.GetBoolean();
        if(autoFit&&blade==null)throw new InvalidOperationException("autoFit recipe requires the vanilla blade texture.");
        loop=root.GetProperty("loopSeconds").GetSingle();leadingExtension=root.GetProperty("leadingExtensionPixels").GetSingle();
        var wave=root.GetProperty("wave");
        Vector2? givenTip=root.TryGetProperty("tip",out var t)?new Vector2(t[0].GetSingle(),t[1].GetSingle()):null;
        Vector2? givenPivot=root.TryGetProperty("pivot",out var pv)?new Vector2(pv[0].GetSingle(),pv[1].GetSingle()):null;
        float? givenBody=root.TryGetProperty("bodyHalfWidth",out var bw)?bw.GetSingle():(float?)null;
        float? givenTail=root.TryGetProperty("tailHalfWidth",out var tw)?tw.GetSingle():(float?)null;
        float? givenWaveStart=wave.TryGetProperty("startX",out var ws)?ws.GetSingle():(float?)null;
        float? givenWaveEnd=wave.TryGetProperty("endX",out var we)?we.GetSingle():(float?)null;
        waveHeight=wave.GetProperty("height").GetSingle();frontWidth=wave.GetProperty("leadingWidth").GetSingle();backWidth=wave.GetProperty("trailingWidth").GetSingle();
        topPhase=wave.GetProperty("topPhase").GetSingle();bottomPhase=wave.GetProperty("bottomPhase").GetSingle();
        pivot=givenPivot??new Vector2(BaselinePivotX,BaselinePivotY);
        tip=givenTip??new Vector2(BaselinePivotX+BaselineBladeCanvasPx,BaselinePivotY);
        bodyWidth=givenBody??20f;tailWidth=givenTail??8f;
        waveStart=givenWaveStart??tip.X-18f;waveEnd=givenWaveEnd??48f;
        if(autoFit)
        {
            // Measure the vanilla silhouette along the same diagonal axis OreSwordEffects.BladeReach uses,
            // then map it onto the frozen R2 canvas: blade length and mid-body half width scale with the
            // sprite; the tapered wake keeps the R2 body-to-tail ratio (20:8).
            var pixels=new Color[blade.Width*blade.Height];blade.GetData(pixels);
            float cx=(blade.Width-1)*.5f,cy=(blade.Height-1)*.5f,reach=1,mid=0;
            for(int i=0;i<pixels.Length;i++)
            {
                if(pixels[i].A<=32)continue;
                float dx=i%blade.Width-cx,dy=i/blade.Width-cy;
                reach=Math.Max(reach,(dx-dy)*.70710678f);
            }
            for(int i=0;i<pixels.Length;i++)
            {
                if(pixels[i].A<=32)continue;
                float dx=i%blade.Width-cx,dy=i/blade.Width-cy;
                float along=(dx-dy)*.70710678f,across=(dx+dy)*.70710678f;
                if(along>=reach*.55f&&along<=reach*.82f)mid=Math.Max(mid,Math.Abs(across));
            }
            float bladeCanvas=reach*1.85f*AtlasPerWorldPx;
            pivot=new(Math.Min(BaselinePivotX,442f-leadingExtension-bladeCanvas),BaselinePivotY);
            tip=new(pivot.X+bladeCanvas,pivot.Y);
            bodyWidth=MathF.Round(mid*1.85f*AtlasPerWorldPx,1);
            tailWidth=MathF.Round(bodyWidth*.4f,1);
            waveStart=tip.X-18f;waveEnd=pivot.X-257f;
        }
        float center=root.GetProperty("centerAlpha").GetSingle(),edge=root.GetProperty("edgeAlpha").GetSingle();
        const int samples=256;var colors=new Color[samples];
        for(int i=0;i<samples;i++)
        {
            float v=i/(float)(samples-1),shoulder=MathF.Exp(-MathF.Pow((v-.74f)/.19f,2));
            colors[i]=Color.White*((center+(edge-center)*shoulder)*(1-Smooth(.87f,1,v)));
        }
        profile=new Texture2D(device,1,samples);profile.SetData(colors);
    }
    public float Crest(float time,float phase)
    {float q=time/loop+phase;q-=MathF.Floor(q);return MathHelper.Lerp(waveStart,waveEnd,q);}
    public void Draw(float time)
    {
        float top=Crest(time,topPhase),bottom=Crest(time,bottomPhase);
        float px=pivot.X;
        for(int x=1;x<(int)(tip.X+leadingExtension);x++)
        {
            float blade=Smooth(px-55,px+5,x),end=1-Smooth(tip.X-16,tip.X+leadingExtension,x),tail=Smooth(px-271,px-105,x);
            float half=MathHelper.Lerp(tailWidth*tail,bodyWidth,blade)*end;
            float support=Smooth(px-267,px-195,x)*(1-Smooth(px+45,px+100,x));
            float ripple=1.4f*MathF.Sin(x*.11f+time/loop*MathHelper.TwoPi*5)*support;
            float upper=half+waveHeight*Pulse(x,top)*support+ripple;
            float lower=half+waveHeight*.8f*Pulse(x,bottom)*support-ripple;
            float center=tip.Y+(1-blade)*1.7f*MathF.Sin((time/loop-x/180f)*MathHelper.TwoPi)*tail;
            float opacity=Smooth(px-271,px-135,x)*Smooth(0,9,half);
            if(opacity<=.001f || upper+lower<.2f)continue;
            Main.spriteBatch.Draw(profile,new Vector2(x,center),null,Color.White*opacity,MathHelper.Pi,
                new Vector2(1,0),new Vector2(1,upper/256f),SpriteEffects.None,0);
            Main.spriteBatch.Draw(profile,new Vector2(x,center),null,Color.White*opacity,0,
                Vector2.Zero,new Vector2(1,lower/256f),SpriteEffects.None,0);
        }
    }
    private float Pulse(float x,float at)
    {float distance=x-at,width=distance>0?frontWidth:backWidth;return MathF.Exp(-distance*distance/(width*width));}
    private static float Smooth(float from,float to,float value)=>MathHelper.SmoothStep(0,1,Math.Clamp((value-from)/(to-from),0,1));
    public void Dispose()=>profile.Dispose();
}
