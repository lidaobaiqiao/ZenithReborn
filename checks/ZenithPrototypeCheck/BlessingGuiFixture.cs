using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// Read-only telemetry after isolated UiFixture prepares the sword. No automatic
// key press or activation: the actual user's keybind input path must fire it.
public class BlessingGuiFixture : ModSystem
{
    private readonly Dictionary<string,object> samples=[];
    private bool activated;
    private int rendered;
    private bool Enabled=>!Main.dedServ && !Main.gameMenu && Environment.GetEnvironmentVariable("ZENITH_BLESS_GUI")=="1" &&
        Main.SavePath.Contains("stage-one-playtest",StringComparison.OrdinalIgnoreCase);
    public override void OnWorldLoad(){samples.Clear();activated=false;rendered=0;}
    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if(!Enabled || Main.LocalPlayer.HeldItem.ModItem is not SoulItem soul)return;
        var state=Main.LocalPlayer.GetModPlayer<BlessingPlayer>();rendered++;
        if(state.BurstLeft>0)activated=true;
        string phase=!activated?"ready":state.BurstLeft>0?(state.ZoomBlend>=.999f?"wide":"activated"):
            state.ZoomBlend>0?"restoring":"expired";
        if(!samples.ContainsKey(phase))samples[phase]=new{charge=soul.YujianCharge,remaining=state.BurstLeft,
            blend=state.ZoomBlend,zoomX=Main.GameViewMatrix.Zoom.X,zoomY=Main.GameViewMatrix.Zoom.Y,
            reach=soul.Reach(Main.LocalPlayer),editing=SoulUI.EditingSoul!=null,rendered};
        System.IO.File.WriteAllText(System.IO.Path.Combine(Main.SavePath,"blessing-gui.json"),JsonSerializer.Serialize(new{activated,rendered,samples}));
    }
}
