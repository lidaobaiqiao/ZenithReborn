using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

[Autoload(Side=ModSide.Client)]
public class OrePierceFeedback : ModSystem
{
    public static OreWeaponEndHistory Endings {get;}=new();
    private static ulong? launchSound,hitSound;
    public static void Launch(Vector2 at,Vector2 direction,Color tint)
    {
        if(Main.dedServ)return;
        if(!launchSound.HasValue || Main.GameUpdateCount-launchSound.Value>=6)
        {launchSound=Main.GameUpdateCount;SoundEngine.PlaySound(SoundID.Item1 with {Volume=.55f,Pitch=.3f,MaxInstances=1},at);}
    }
    public static void Strike(Vector2 at,Vector2 direction,Color tint)
    {
        if(Main.dedServ)return;
        Vector2 axis=direction.SafeNormalize(Vector2.UnitX);
        for(int i=0;i<4;i++)
            OreSwordEffects.Ember(at,axis.RotatedBy((i-1.5f)*.45f)*(4+i),tint,.75f,i==0);
        if(!hitSound.HasValue || Main.GameUpdateCount-hitSound.Value>=5)
        {hitSound=Main.GameUpdateCount;SoundEngine.PlaySound(SoundID.Item10 with {Volume=.4f,Pitch=-.15f,MaxInstances=1},at);}
    }
    public static void EndWeapon(OreWeaponEnd end)
    {
        if(Main.dedServ || !Endings.Emit(end))return;
        foreach(var spark in OreWeaponEnding.Sparks(end))SoulFlowSpark.Spawn(spark);
    }
    public override void Load()=>On_Main.DrawDust+=DrawOverEntities;
    public override void Unload(){On_Main.DrawDust-=DrawOverEntities;Reset();OreSwordEffects.Unload();}
    public override void OnWorldUnload()=>Reset();
    private static void Reset(){Endings.Clear();launchSound=hitSound=null;}
    public override void PostUpdateDusts(){if(!Main.gamePaused)Endings.Update();}
    private static void DrawOverEntities(On_Main.orig_DrawDust orig,Main self)
    {
        orig(self);if(Main.gameMenu)return;
        if(Endings.Ends.Count==0)return;
        Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,Main.DefaultSamplerState,
            DepthStencilState.None,Main.Rasterizer,null,Main.GameViewMatrix.TransformationMatrix);
        foreach(var end in Endings.Ends)OreWeaponEnding.DrawEndGhost(end);
        Main.spriteBatch.End();
    }
}
