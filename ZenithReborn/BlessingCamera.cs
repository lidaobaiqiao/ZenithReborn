using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// Native InitTargets reserves only 192 pixels per side. A 0.5 zoom exposes
// the edge of those targets unless both the allocation and its native cap grow.
public class BlessingCamera : ModSystem
{
    public static readonly MethodInfo InitTargets=typeof(Main).GetMethod("InitTargets",
        BindingFlags.Instance|BindingFlags.NonPublic,null,[typeof(int),typeof(int)],null);
    private ILHook targetHook;
    private static int requestedPadding,requestedLimit;
    private static readonly FieldInfo removeMinimum=typeof(Terraria.ModLoader.ModLoader).GetField("removeForcedMinimumZoom",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
    public static int PaddingFor(int width,int height,float zoom)=>
        Math.Max(192,(int)Math.Ceiling(Math.Max(width,height)*Math.Max(0,1/Math.Max(.1f,zoom)-1)*.5f)+64);
    public static void PatchTargets(ILContext il)
    {
        var cursor=new ILCursor(il);
        if(!cursor.TryGotoNext(MoveType.After,i=>i.MatchLdcI4(192)))
            throw new InvalidOperationException("Native terrain padding pattern changed");
        cursor.EmitDelegate<Func<int,int>>(original=>Math.Max(original,requestedPadding));
        cursor.Index=0;int caps=0;
        while(cursor.TryGotoNext(MoveType.After,i=>i.MatchLdsfld<Main>("_renderTargetMaxSize")))
        {cursor.EmitDelegate<Func<int,int>>(original=>Math.Max(original,requestedLimit));caps++;}
        if(caps!=2)throw new InvalidOperationException("Native terrain target cap pattern changed");
    }
    public override void Load()
    {
        if(Main.dedServ)return;
        targetHook=new ILHook(InitTargets,PatchTargets);Main.OnPreDraw+=PrepareTargets;
    }
    private void PrepareTargets(GameTime time)
    {
        if(Main.dedServ || Main.instance?.GraphicsDevice==null)return;
        var parameters=Main.instance.GraphicsDevice.PresentationParameters;
        int width=parameters.BackBufferWidth,height=parameters.BackBufferHeight;
        int desired=0;
        if(!Main.gameMenu)
        {
            var state=Main.LocalPlayer.GetModPlayer<BlessingPlayer>();
            if(state.BurstLeft>0 || state.ZoomBlend>0)
            {
                bool unrestricted=removeMinimum?.GetValue(null) is true;
                float minZoom=Math.Max(1,Math.Max(width/(unrestricted?8192f:Main.MinimumZoomComparerX),
                    height/(unrestricted?8192f:Main.MinimumZoomComparerY)));
                desired=PaddingFor(width,height,minZoom*MathHelper.Clamp(Main.GameZoomTarget,1,2)*.5f);
                if(desired<=192)desired=0;
            }
        }
        int limit=desired>0?Math.Max(width,height)+2*desired:0;
        if(desired==requestedPadding && limit==requestedLimit)return;
        requestedPadding=desired;requestedLimit=limit;
        if(Main.drawToScreen)return;
        // Use the engine's complete allocator. Changing offScreenRange alone
        // would still leave small targets and incorrect scene offsets.
        InitTargets.Invoke(Main.instance,[width,height]);
        Main.renderNow=true;
    }
    public override void Unload()
    {
        Main.OnPreDraw-=PrepareTargets;
        bool restore=requestedPadding>0;requestedPadding=requestedLimit=0;
        targetHook?.Dispose();targetHook=null;
        if(restore && !Main.dedServ)Main.QueueMainThreadAction(()=>
        {
            if(Main.instance?.GraphicsDevice==null)return;
            var p=Main.instance.GraphicsDevice.PresentationParameters;
            InitTargets.Invoke(Main.instance,[p.BackBufferWidth,p.BackBufferHeight]);Main.renderNow=true;
        });
    }
}
