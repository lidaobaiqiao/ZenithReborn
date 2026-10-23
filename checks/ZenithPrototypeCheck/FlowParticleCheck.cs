using Microsoft.Xna.Framework;
using Terraria;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class FlowParticleCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var pivot=new Vector2(400,300);var samples=new List<FlowSparkSpawn>();
        var emitter=new SlashSparkEmitter(42);
        emitter.Update(pivot,0,3,1,true,samples.Add);
        emitter.Update(pivot,.2f,3,2,true,samples.Add);
        check(samples.Count>0 && samples.All(p=>Vector2.Dot(p.Velocity,Vector2.UnitY)>0),"Slash sparks follow the positive sweep tangent");
        var reverse=new SlashSparkEmitter(42);var backward=new List<FlowSparkSpawn>();
        reverse.Update(pivot,0,3,1,true,backward.Add);reverse.Update(pivot,-.2f,3,2,true,backward.Add);
        check(backward.Count==samples.Count && backward.All(p=>Vector2.Dot(p.Velocity,Vector2.UnitY)<0),"Mirrored sparks follow the opposite sweep tangent");
        int count=samples.Count;emitter.Update(pivot+new Vector2(500,0),.3f,3,3,true,samples.Add);
        check(samples.Count==count,"Teleport emits no bridging particles");
        emitter.Update(pivot,float.NaN,3,4,true,samples.Add);
        check(samples.Count==count,"Invalid poses emit no particles");
        for(int i=0;i<500;i++)emitter.Update(pivot,i*.1f,3,i,true,samples.Add);
        check(emitter.Emitted==SlashSparkEmitter.MaximumPerSlash,"Persistent slash visuals respect a per-slash emission budget");
        emitter.Update(pivot,0,3,0,false,samples.Add);
        check(emitter.Emitted==0,"Leaving the slash resets the emitter for a new attack");
        var d=new Dust{active=true};SoulFlowSpark.Configure(d,samples[0]);
        Vector2 start=d.position;float speed=d.velocity.Length();SoulFlowSpark.Step(d);
        check(d.position!=start && d.velocity.Length()<speed && d.noGravity && d.noLight,"Detached dust moves and slows without any projectile or parent position");
        for(int i=0;i<60;i++)SoulFlowSpark.Step(d);
        check(!d.active,"Detached sparks expire even after their parent slash has ended");
    }
}
