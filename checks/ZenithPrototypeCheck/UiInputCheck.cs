using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class UiInputCheck
{
    internal static void Run(Action<bool,string> check)
    {
        int oldW=Main.screenWidth,oldH=Main.screenHeight;float oldScale=Main.UIScale;
        Item oldCursor=Main.mouseItem;
        try
        {
            Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;
            var player=new Player { active=true,whoAmI=0 }; Main.player[0]=player;
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            player.inventory[0]=soul.Item;
            Main.screenWidth=1280;Main.screenHeight=720;Main.UIScale=1;SoulUI.Open(soul);
            var layers=new List<GameInterfaceLayer> {
                new LegacyGameInterfaceLayer("Vanilla: Inventory",()=>true),
                new LegacyGameInterfaceLayer("Vanilla: Mouse Text",()=>true) };
            ModContent.GetInstance<SoulUI>().ModifyInterfaceLayers(layers);
            check(layers[0].Name=="ZenithReborn: Input Shield" && layers.FindIndex(l=>l.Name=="ZenithReborn: Soul")>layers.FindIndex(l=>l.Name=="Vanilla: Inventory"),"Shield precedes vanilla inventory while foreground panel retains draw order");
            foreach(float scale in new[]{1f,1.25f,1.6296296f,2f})
            {
                Main.UIScale=scale;Main.screenWidth=(int)(1920/scale);Main.screenHeight=(int)(1080/scale);
                Rectangle p=SoulUI.PanelBounds();Point center=p.Center;
                check(SoulUI.ContainsScreenPoint((int)(center.X*scale),(int)(center.Y*scale),1920,1080,scale),"Update-phase pixel hit test matches UI panel at scale "+scale);
                check(!SoulUI.ContainsScreenPoint(0,0,1920,1080,scale),"Viewport outside panel remains interactive at scale "+scale);
            }
            Main.UIScale=1;Main.screenWidth=1280;Main.screenHeight=720;SoulUI.SelectPage(0);
            var background=new[]{new Item(ItemID.GoldBroadsword)};
            Main.mouseItem=new Item();Main.mouseLeft=Main.mouseRight=Main.mouseLeftRelease=Main.mouseRightRelease=true;
            PlayerInput.Triggers.Current.MouseLeft=PlayerInput.Triggers.Current.MouseRight=true;
            SoulInput.Reset();SoulInput.Capture(true);
            check(!Main.mouseLeft && !Main.mouseRight && !Main.mouseLeftRelease && !Main.mouseRightRelease,"Both buttons and release edges hidden before background UI updates");
            check(!PlayerInput.Triggers.Current.MouseLeft && !PlayerInput.Triggers.Current.MouseRight,"World action triggers masked before player control copy");
            ItemSlot.LeftClick(background,0,0);ItemSlot.RightClick(background,0,0);
            check(background[0].type==ItemID.GoldBroadsword && Main.mouseItem.IsAir,"Actual underlying vanilla slot cannot take/equip/split on captured click");
            player.controlUseItem=player.controlUseTile=true;player.controlLeft=true;
            player.GetModPlayer<SoulUIControls>().SetControls();
            check(!player.controlUseItem && !player.controlUseTile && player.controlLeft && player.mouseInterface,"World use/tile actions blocked while movement preserved");
            Point slot=SoulUI.SlotBounds(0).Center;Main.mouseX=slot.X;Main.mouseY=slot.Y;
            Main.mouseItem=new Item(ItemID.CopperShortsword);
            SoulInput.BeginSoulLayer();
            check(SoulUI.TryClickHost(soul,out _) && soul.Hosts[0].type==ItemID.CopperShortsword && Main.mouseItem.IsAir,"Foreground slot still receives captured click exactly once");
            SoulInput.EndSoulLayer();ItemSlot.LeftClick(background,0,0);
            check(background[0].type==ItemID.GoldBroadsword && Main.mouseItem.IsAir,"Layers after foreground panel also cannot receive click");
            SoulInput.RestoreAfterInterface();
            check(Main.mouseLeft && Main.mouseRight && !Main.mouseLeftRelease && !Main.mouseRightRelease,"Physical state restored with click consumed for next frame");
            SoulInput.Capture(true);SoulInput.BeginSoulLayer();
            check(!Main.mouseLeftRelease && !SoulUI.TryClickHost(soul,out _) && !soul.Hosts[0].IsAir,"Holding button does not repeat swaps");
            SoulInput.EndSoulLayer();SoulInput.RestoreAfterInterface();
            SoulInput.Capture(false);
            check(SoulInput.Blocking && !Main.mouseLeft && !Main.mouseRight,"Dragging held buttons outside retains ownership");
            SoulInput.RestoreAfterInterface();Main.mouseLeft=Main.mouseRight=false;
            SoulInput.Capture(false);check(!SoulInput.Blocking,"Releasing buttons outside releases capture");
            // Outside the panel the actual vanilla handler must still work.
            Main.mouseLeft=Main.mouseLeftRelease=true;Main.mouseRight=false;Main.mouseItem=new Item();
            SoulInput.Capture(false);
            // The headless fixture has no client crafting-availability cache.
            // Skip only post-transfer recipe refresh; exercise the real slot handler.
            using(var recipeRefresh=new MonoMod.RuntimeDetour.Hook(typeof(Recipe).GetMethod("FindRecipes",new[]{typeof(bool)}),
                (Action<Action<bool>,bool>)((original,canDelay)=>{})))
                ItemSlot.LeftClick(background,0,0);
            check(background[0].IsAir && Main.mouseItem.type==ItemID.GoldBroadsword,"Uncovered vanilla inventory remains functional");
            // A blank-panel press followed by close must not become a world click.
            Main.mouseLeft=Main.mouseRight=false;SoulInput.Capture(true);SoulInput.RestoreAfterInterface();
            Main.mouseLeft=Main.mouseLeftRelease=true;SoulInput.Capture(true);SoulInput.BeginSoulLayer();
            Point close=SoulUI.CloseBounds().Center;Main.mouseX=close.X;Main.mouseY=close.Y;
            check(SoulUI.HandleNavigation() && SoulUI.EditingSoul==null,"Close button works through capture");
            SoulInput.EndSoulLayer();SoulInput.RestoreAfterInterface();SoulInput.Capture(false);
            check(SoulInput.Blocking && !Main.mouseLeft,"Closing does not leak the still-held click to the world");
            SoulInput.RestoreAfterInterface();Main.mouseLeft=Main.mouseRight=false;SoulInput.Capture(false);
            check(!SoulInput.Blocking,"Input resumes after closing and releasing");
        }
        finally
        {
            if(SoulUI.EditingSoul!=null) SoulUI.Toggle(SoulUI.EditingSoul);
            SoulInput.Reset();SoulUI.SelectPage(0);Main.screenWidth=oldW;Main.screenHeight=oldH;Main.UIScale=oldScale;
            Main.mouseItem=oldCursor;Main.mouseLeft=Main.mouseRight=false;Main.mouseLeftRelease=Main.mouseRightRelease=true;
        }
    }
}
