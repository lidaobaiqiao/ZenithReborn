using MonoMod.RuntimeDetour;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class BlessingCameraCheck
{
    internal static void Run(Action<bool,string> check)
    {
        // Admit the hook against the actual shipped method, not a source-shaped
        // stub. This checks IL compatibility without running a GPU allocator.
        using(var hook=new ILHook(BlessingCamera.InitTargets,BlessingCamera.PatchTargets))
            check(true,"Native InitTargets admits both the 192-pixel padding patch and both target-cap accesses");
        foreach(var size in new[]{(855,535),(1280,800),(1920,1080),(2560,1440),(3440,1440),(3840,2160)})
            foreach(float original in new[]{1f,1.25f,2f})
        {
            float minimum=Math.Max(1,Math.Max(size.Item1/1920f,size.Item2/1200f));float zoom=minimum*original*.5f;
            int padding=BlessingCamera.PaddingFor(size.Item1,size.Item2,zoom);
            check((size.Item1+2*padding)*zoom>=size.Item1 && (size.Item2+2*padding)*zoom>=size.Item2,
                "Zoomed terrain target covers the complete physical viewport: "+size+" / "+original);
        }
        check(BlessingCamera.PaddingFor(1280,800,.5f)>192,"The screenshot's 1280-wide half zoom receives more than the original 192-pixel border");
        check(BlessingCamera.PaddingFor(1920,1080,1f)==192,"Normal zoom keeps the original terrain buffer padding");
    }
}
