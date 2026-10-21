using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class WindowDragCheck
{
    private static Rectangle Bounds()
    {
        int w=Main.screenWidth,h=Main.screenHeight;
        float scale=Main.UIScale;
        Main.screenWidth=(int)(w/scale);Main.screenHeight=(int)(h/scale);
        try { return SoulUI.PanelBounds(); }
        finally { Main.screenWidth=w;Main.screenHeight=h; }
    }
    private static void Input(Point p,bool down,bool edge)
    {
        Main.mouseX=(int)(p.X*Main.UIScale);Main.mouseY=(int)(p.Y*Main.UIScale);
        Main.mouseLeft=down;Main.mouseLeftRelease=edge;Main.mouseRight=false;
        PlayerInput.Triggers.Current.MouseLeft=down;
        ModContent.GetInstance<SoulUI>().PostUpdateInput();
    }
    internal static void Run(Action<bool,string> check)
    {
        int oldW=Main.screenWidth,oldH=Main.screenHeight;float oldScale=Main.UIScale;
        bool oldMenu=Main.gameMenu,oldHidden=Main.hideUI,oldMap=Main.mapFullscreen,oldServer=Main.dedServ;
        Item cursor=Main.mouseItem;
        try
        {
            Main.dedServ=Main.gameMenu=Main.hideUI=Main.mapFullscreen=false;
            Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;
            Main.player[0]=new Player {active=true,whoAmI=0};
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            Main.player[0].inventory[0]=soul.Item;SoulUI.Open(soul);
            foreach(float scale in new[]{1f,1.25f,1.6296296f,2f})
            {
                SoulUI.ResetWindowPosition();SoulInput.Reset();Main.mouseItem=new Item();
                Main.screenWidth=2560;Main.screenHeight=1440;Main.UIScale=scale;
                float actualScale=Main.UIScale;
                Rectangle first=Bounds();Point header=new(first.X+40,first.Y+20);
                Input(header,true,true);
                check(SoulUI.WindowDragging && SoulInput.Blocking && !Main.mouseLeft && !PlayerInput.Triggers.Current.MouseLeft,
                    "Actual input hook starts header drag before world input at scale "+scale);
                SoulInput.RestoreAfterInterface();
                Point moved=header+new Point(70,20);Input(moved,true,false);
                Rectangle next=Bounds();
                check(Math.Abs(next.X-first.X-70)<=1 && Math.Abs(next.Y-Math.Min(first.Y+20,(int)(1440/actualScale)-first.Height))<=1,
                    "Dragged UI displacement matches physical pixels divided once by scale "+scale+" actual "+actualScale+" "+first+" -> "+next);
                check(SoulInput.Blocking && !Main.mouseLeft,"Moving the panel retains background shielding");
                SoulInput.RestoreAfterInterface();Input(new Point(-1000,-1000),true,false);
                check(Bounds().X==0 && Bounds().Y==0 && SoulUI.WindowDragging && SoulInput.Blocking,
                    "Dragging outside clamps window and keeps click ownership");
                SoulInput.RestoreAfterInterface();Input(new Point(-1000,-1000),false,false);
                check(!SoulUI.WindowDragging,"Releasing outside ends drag");
                SoulInput.RestoreAfterInterface();Input(new Point(-1000,-1000),false,true);
                check(!SoulInput.Blocking,"Input outside returns after drag release");
                SoulUI.Toggle(soul);SoulUI.Open(soul);
                check(Bounds().Location==Point.Zero,"Reopening remembers the session window position");
                Main.screenWidth=800;Main.screenHeight=600;
                Rectangle small=Bounds();
                check(new Rectangle(0,0,(int)(800/Main.UIScale),(int)(600/Main.UIScale)).Contains(small),
                    "Resized viewport keeps whole window reachable: "+scale+" actual "+Main.UIScale+" "+small);
                Main.mouseItem=new Item(ItemID.CopperShortsword);SoulInput.Reset();
                Input(new Point(small.X+20,small.Y+20),true,true);
                check(!SoulUI.WindowDragging && Main.mouseItem.type==ItemID.CopperShortsword,"Cursor weapon cannot accidentally start a window drag");
                SoulInput.RestoreAfterInterface();Input(new Point(-100,-100),false,true);SoulInput.RestoreAfterInterface();
                Main.mouseItem=new Item();SoulInput.Reset();
                Point close=new(small.Right-30,small.Y+25);Input(close,true,true);
                check(!SoulUI.WindowDragging,"Close button is excluded from drag handle");
                SoulInput.RestoreAfterInterface();Input(new Point(-100,-100),false,true);SoulInput.RestoreAfterInterface();
            }
        }
        finally
        {
            if(SoulUI.EditingSoul!=null)SoulUI.Toggle(SoulUI.EditingSoul);
            SoulUI.ResetWindowPosition();SoulInput.Reset();
            Main.screenWidth=oldW;Main.screenHeight=oldH;Main.UIScale=oldScale;
            Main.dedServ=oldServer;Main.gameMenu=oldMenu;Main.hideUI=oldHidden;Main.mapFullscreen=oldMap;
            Main.mouseItem=cursor;Main.mouseLeft=Main.mouseRight=false;Main.mouseLeftRelease=Main.mouseRightRelease=true;
        }
    }
}
