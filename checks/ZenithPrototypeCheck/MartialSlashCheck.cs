using Microsoft.Xna.Framework;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class MartialSlashCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var trail=new MartialSlashTrail();var pivot=new Vector2(800,600);
        for(int i=0;i<24;i++)trail.Update(pivot,i*.08f,true,false);
        check(trail.Count==MartialSlashTrail.MaximumSamples,"Slash history has a fixed client visual budget");
        trail.Update(pivot+new Vector2(400,0),2,true,false);
        check(trail.Count==1,"Teleport breaks the slash trail instead of drawing a screen-spanning ribbon");
        trail.Update(pivot+new Vector2(400,0),float.NaN,true,false);
        check(trail.Count==0,"Non-finite slash samples are discarded");
        trail.Update(pivot,3.13f,true,false);trail.Update(pivot,-3.13f,true,false);
        check(trail.Count==2,"Crossing the angle wrap keeps a continuous slash history");
        for(int i=0;i<MartialSlashTrail.Lifetime;i++)trail.Update(pivot,0,false,true);
        check(trail.Count==0,"Slash residue expires completely after its fade window");
        for(int i=0;i<8;i++)trail.Update(pivot,i*.1f,true,false);
        trail.Update(pivot,1,false,false);
        check(trail.Count==0,"Cancelling or returning to a non-slash phase removes the old ribbon");
        for(int i=0;i<8;i++)trail.Update(pivot,i*.1f,true,false);
        trail.Update(pivot,2,true,false);
        check(trail.Count==1,"A discontinuous rotation starts a fresh ribbon");
    }
}
