using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace ZenithReborn;

// Capture before vanilla UI/world updates, expose only to our foreground layer,
// then restore physical buttons after the interface has finished. A press that
// starts here stays owned until release, even after closing or dragging outside.
public static class SoulInput
{
    private static bool left, right, previousLeft, previousRight, capturedLeft, capturedRight;
    private static bool pendingLeft, pendingRight, overPanel;
    public static bool Blocking { get; private set; }
    public static void Reset()
    {
        left=right=previousLeft=previousRight=capturedLeft=capturedRight=false;
        pendingLeft=pendingRight=overPanel=Blocking=false;
    }
    public static void Capture(bool inside)
    {
        left=Main.mouseLeft; right=Main.mouseRight; overPanel=inside;
        if(!left) capturedLeft=false;
        if(!right) capturedRight=false;
        if(inside)
        {
            if(left) capturedLeft=true;
            if(right) capturedRight=true;
            pendingLeft |= left && !previousLeft && Main.mouseLeftRelease;
            pendingRight |= right && !previousRight && Main.mouseRightRelease;
        }
        previousLeft=left; previousRight=right;
        Blocking=inside || capturedLeft || capturedRight;
        if(!Blocking) { pendingLeft=pendingRight=false;return; }
        // Triggers feed Player.ResetControls; masking Main alone cannot stop
        // attacks or tile/NPC interaction during the update portion of a frame.
        PlayerInput.Triggers.Current.MouseLeft=false;
        PlayerInput.Triggers.Current.MouseRight=false;
        MaskBackground();
    }
    public static void MaskBackground()
    {
        if(!Blocking) return;
        Main.mouseLeft=Main.mouseRight=Main.mouseLeftRelease=Main.mouseRightRelease=false;
        Main.LocalPlayer.mouseInterface=true;
        PlayerInput.LockVanillaMouseScroll("ZenithReborn: Soul");
    }
    public static void BeginSoulLayer()
    {
        if(!Blocking) return;
        Main.mouseLeft=overPanel && left; Main.mouseRight=overPanel && right;
        Main.mouseLeftRelease=pendingLeft; Main.mouseRightRelease=pendingRight;
    }
    public static void EndSoulLayer()
    {
        if(!Blocking) return;
        // Empty space, labels and disabled buttons also own their clicks.
        pendingLeft=pendingRight=false;
        MaskBackground();
    }
    public static void RestoreAfterInterface()
    {
        if(!Blocking) return;
        Main.mouseLeft=left; Main.mouseRight=right;
        Main.mouseLeftRelease=!left; Main.mouseRightRelease=!right;
    }
}

public class SoulUIControls : ModPlayer
{
    public override void SetControls()
    {
        if(!SoulInput.Blocking) return;
        Player.mouseInterface=true;
        Player.controlUseItem=false; Player.controlUseTile=false;
    }
}
