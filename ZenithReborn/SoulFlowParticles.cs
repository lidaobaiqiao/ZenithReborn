using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// Shared palette for the approved flowing material and its detached sparks.
public static class SoulFlowStyle
{
    public static Color Head(int tier)=>tier==3?new(113,237,255):new(97,255,225);
    public static Color Tail(int tier)=>tier==3?new(213,45,255):new(35,85,255);
}

public readonly record struct FlowSparkSpawn(Vector2 Position,Vector2 Velocity,Color Tint,float Size,int Lifetime,bool Mote);

// A small emitter, not a particle framework: vanilla Dust owns allocation,
// update, drawing and eviction. Only visual poses are sampled here.
public sealed class SlashSparkEmitter
{
    public const int MaximumPerSlash=96;
    private readonly Random random;
    private Vector2 previousPivot;
    private float previousAngle;
    private bool sampling,finished;
    public int Emitted {get;private set;}
    public SlashSparkEmitter(int? seed=null)=>random=seed.HasValue?new(seed.Value):new();
    private float Range(float a,float b)=>a+(b-a)*random.NextSingle();
    private static bool Finite(Vector2 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y);

    public void Update(Vector2 pivot,float angle,int tier,int slashAge,bool slashing,Action<FlowSparkSpawn> emit)
    {
        if(!slashing || !Finite(pivot) || !float.IsFinite(angle))
        {sampling=false;finished=false;Emitted=0;return;}
        float turn=MathHelper.WrapAngle(angle-previousAngle);
        if(!sampling || Vector2.DistanceSquared(pivot,previousPivot)>96*96 || MathF.Abs(turn)>.8f)
        {previousPivot=pivot;previousAngle=angle;sampling=true;return;}
        float travel=MathF.Abs(turn);
        int count=travel<.003f?0:Math.Clamp((int)MathF.Ceiling(travel/.055f),1,5);
        for(int i=0;i<count;i++)
        {
            float t=(i+Range(.15f,.85f))/count;
            float at=previousAngle+turn*t;
            Vector2 axis=at.ToRotationVector2();
            Vector2 tangent=axis.RotatedBy(MathHelper.PiOver2)*MathF.Sign(turn);
            bool mote=(Emitted%3)==0;
            Add(Vector2.Lerp(previousPivot,pivot,t)+axis*Range(257,290),
                tangent*Range(mote?1.4f:5,mote?3.5f:10)+axis*Range(.4f,2.8f),mote);
        }
        // One small terminal accent, synchronized to the existing visual sweep.
        if(!finished && slashAge>=17 && count>0)
        {
            finished=true;
            Vector2 axis=angle.ToRotationVector2(),tangent=axis.RotatedBy(MathHelper.PiOver2)*MathF.Sign(turn);
            for(int i=0;i<8;i++)Add(pivot+axis*Range(267,287),
                tangent.RotatedBy(Range(-.65f,.65f))*Range(5,13)+axis*Range(1,3),i%3==0);
        }
        previousPivot=pivot;previousAngle=angle;

        void Add(Vector2 position,Vector2 velocity,bool mote)
        {
            if(Emitted>=MaximumPerSlash)return;
            Emitted++;
            emit(new(position,velocity,Color.Lerp(SoulFlowStyle.Tail(tier),SoulFlowStyle.Head(tier),Range(0,1)),
                Range(mote?.45f:.6f,mote?.8f:1.05f),mote?random.Next(32,47):random.Next(19,33),mote));
        }
    }
}

// Reuses the game's Dust pool and vanilla SharpTears sprite. No extra mod,
// texture, shader, projectile, network packet or custom render pass is needed.
public class SoulFlowSpark : ModDust
{
    public override string Texture=>"ZenithReborn/SwordSpark";
    public static Dust Spawn(FlowSparkSpawn spawn)
    {
        if(Main.dedServ)return null;
        Dust dust=Dust.NewDustPerfect(spawn.Position,ModContent.DustType<SoulFlowSpark>(),spawn.Velocity,0,spawn.Tint,spawn.Size);
        if(dust.active)Configure(dust,spawn);
        return dust;
    }
    public override void OnSpawn(Dust dust)
    {dust.noGravity=true;dust.noLight=true;dust.fadeIn=0;dust.frame=new(0,0,16,16);}
    public static void Configure(Dust dust,FlowSparkSpawn spawn)
    {
        dust.position=spawn.Position;dust.velocity=spawn.Velocity;dust.color=spawn.Tint;dust.scale=spawn.Size;
        dust.customData=spawn;dust.fadeIn=0;dust.noGravity=true;dust.noLight=true;
    }
    public static void Step(Dust dust)
    {
        if(dust.customData is not FlowSparkSpawn state){dust.active=false;return;}
        dust.fadeIn++;
        if(dust.fadeIn>=state.Lifetime){dust.active=false;return;}
        float turn=state.Mote?MathF.Sin(dust.fadeIn*.17f)*.035f:0;
        dust.velocity=dust.velocity.RotatedBy(turn)*(state.Mote?.955f:.935f);
        dust.velocity.Y+=state.Mote?.025f:.045f;
        dust.position+=dust.velocity;
        dust.rotation=dust.velocity.ToRotation()+MathHelper.PiOver2;
    }
    public override bool Update(Dust dust){Step(dust);return false;}
    public static void DrawSpark(SpriteBatch batch,Dust dust)
    {
        if(!dust.active || dust.customData is not FlowSparkSpawn state)return;
        float t=Math.Clamp(dust.fadeIn/state.Lifetime,0,1);
        float fade=MathF.Pow(1-t,1.6f)*Math.Min(1,(dust.fadeIn+1)/3);
        var texture=TextureAssets.Extra[ExtrasID.SharpTears].Value;
        Vector2 center=texture.Size()*.5f,at=dust.position-Main.screenPosition;
        float length=state.Mote?17:Math.Clamp(dust.velocity.Length()*5.5f,18,64);
        Vector2 size=new((state.Mote?10f:7.5f)*state.Size/texture.Width,length*state.Size/texture.Height);
        Color color=state.Tint*(fade*(1-t*.2f));color.A=0;
        batch.Draw(texture,at,null,color,dust.rotation,center,size,SpriteEffects.None,0);
        Color core=Color.Lerp(state.Tint,Color.White,.75f)*(fade*.8f);core.A=0;
        batch.Draw(texture,at,null,core,dust.rotation,center,size*new Vector2(.42f,.85f),SpriteEffects.None,0);
        if(state.Mote)
            batch.Draw(texture,at,null,color*.6f,dust.rotation+MathHelper.PiOver2,center,size*.7f,SpriteEffects.None,0);
    }
    public override bool PreDraw(Dust dust){DrawSpark(Main.spriteBatch,dust);return false;}
}
