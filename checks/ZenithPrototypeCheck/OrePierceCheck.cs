using Microsoft.Xna.Framework;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class OrePierceCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var ends=new OreWeaponEndHistory();
        var small=new OreWeaponEnd(new(100,90),new(100,90),Vector2.UnitX,Color.Cyan,1,1,false);
        var giant=small with {BladeAt=new(100,700),Anchor=new(100,400),Giant=true};
        check(!ends.Emit(small with {Anchor=new(float.NaN,0)}) && !ends.Emit(small with {Scale=0}),"Invalid endings cannot enter graphics history");
        ends.Emit(small);ends.Emit(giant);
        for(int i=0;i<20;i++)ends.Update();
        check(ends.Ends.Count==1 && ends.Ends[0].Giant && ends.Ends[0].Anchor.Y==400 && ends.Ends[0].BladeAt.Y==700,
            "Giant aftermath survives small-blade lifetime and retains contact separate from final blade pose");
        for(int i=0;i<16;i++)ends.Update();
        check(ends.Ends.Count==0,"Independent aftermath expires without any live weapon");
        for(int i=0;i<80;i++)ends.Emit(small with {BladeAt=new(i,0)});
        check(ends.Ends.Count==OreWeaponEndHistory.Capacity && ends.Ends[0].BladeAt.X==16,"Dense ending events have a fixed memory budget");
        ends.Clear();check(ends.Ends.Count==0,"World cleanup also clears weapon endings");
    }
}
