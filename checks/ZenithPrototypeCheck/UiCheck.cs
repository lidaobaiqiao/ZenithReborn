using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class UiCheck
{
    internal static void Run(Action<bool,string> check)
    {
        int oldW=Main.screenWidth,oldH=Main.screenHeight; float oldScale=Main.UIScale;
        Item oldCursor=Main.mouseItem;
        try
        {
            Main.netMode=NetmodeID.SinglePlayer; Main.myPlayer=0;
            Main.player[0]=new Player { active=true,whoAmI=0 };
            foreach(var resolution in new[]{(1920,1080),(1280,720),(2560,1440)})
            foreach(float scale in new[]{1f,1.25f,1.6296296f,2f})
            {
                Main.UIScale=scale;
                // Simulate the UI layer's already-scaled viewport, not raw pixels.
                Main.screenWidth=(int)(resolution.Item1/scale);Main.screenHeight=(int)(resolution.Item2/scale);
                SoulUI.SelectPage(0);
                var panel=SoulUI.PanelBounds(); var body=SoulUI.ContentBounds();
                check(new Rectangle(0,0,Main.screenWidth,Main.screenHeight).Contains(panel),"UI panel fits viewport: "+resolution+" / "+scale);
                check(panel.Contains(body) && !body.Intersects(SoulUI.TabBounds(0)),"Scrolling body stays below fixed navigation");
                check(panel.Contains(SoulUI.CloseBounds()),"Close button stays on screen");
                var slots=Enumerable.Range(0,17).Select(SoulUI.SlotBounds).ToArray();
                for(int i=0;i<slots.Length;i++)
                {
                    check(slots[i].Width>=35 && slots[i].Left>=body.Left && slots[i].Right<=body.Right,"Slot horizontal bounds and usable target "+i);
                    check(!slots.Where((r,j)=>j!=i).Any(r=>r.Intersects(slots[i])),"Slots never overlap "+i);
                }
                check(slots[5].Y>slots[0].Bottom && slots[4].Y==slots[0].Y,"Ten main slots use two rows of five");
                var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
                Main.player[0].inventory[0]=soul.Item;
                Point pos=slots[0].Center; Main.mouseX=pos.X;Main.mouseY=pos.Y;
                Main.mouseItem=new Item(ItemID.CopperShortsword);Main.mouseItem.Prefix(PrefixID.Legendary);
                Main.mouseLeft=Main.mouseLeftRelease=true;
                check(SoulUI.TryClickHost(soul,out _) && Main.mouseItem.IsAir && soul.Hosts[0].prefix==PrefixID.Legendary,"Real visible handler inserts and preserves prefix at scale "+scale);
                Main.mouseLeftRelease=true;
                check(SoulUI.TryClickHost(soul,out _) && soul.Hosts[0].IsAir && Main.mouseItem.type==ItemID.CopperShortsword,"Real handler returns preview weapon");
                Main.mouseItem=new Item(ItemID.WoodenBow);Main.mouseLeftRelease=true;
                check(!SoulUI.TryClickHost(soul,out var error) && error.Length>0 && Main.mouseItem.type==ItemID.WoodenBow,"Rejected item stays on cursor");
                foreach(int page in new[]{1,2})
                {
                    SoulUI.SelectPage(page); Main.mouseLeftRelease=true;
                    check(!SoulUI.TryClickHost(soul,out _) && Main.mouseItem.type==ItemID.WoodenBow && Main.mouseLeftRelease,"Hidden slots cannot consume clicks on other pages");
                }
                SoulUI.SelectPage(0); Main.mouseX=body.Left-1;Main.mouseY=slots[0].Center.Y;Main.mouseLeftRelease=true;
                check(!SoulUI.TryClickHost(soul,out _) && Main.mouseLeftRelease,"Outside body cannot swap weapons");
            }
            Main.screenWidth=1280; Main.screenHeight=900; Main.UIScale=1;
            for(int slot=0;slot<17;slot++)
            {
                var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
                // Every stored slot, including now-locked support/forest slots,
                // must retain an escape route. Do not silently destroy paused items.
                if(slot<10) soul.Hosts[slot]=new Item(ItemID.CopperShortsword);
                else if(slot<15) soul.SupportHosts[slot-10]=new Item(ItemID.CopperShortsword);
                else soul.ForestHosts[slot-15]=new Item(ItemID.CopperShortsword);
                SoulUI.SelectPage(0); var pos=SoulUI.SlotBounds(slot).Center;
                Main.mouseX=pos.X;Main.mouseY=pos.Y;Main.mouseItem=new Item();Main.mouseLeft=Main.mouseLeftRelease=true;
                check(SoulUI.TryClickHost(soul,out _) && Main.mouseItem.type==ItemID.CopperShortsword && soul.Stored.All(i=>i.IsAir),"All 17 slot indices retrieve paused item correctly: "+slot);
            }
            var navSoul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            SoulUI.Open(navSoul); Main.mouseItem=new Item(ItemID.CobaltSword);
            var tab=SoulUI.TabBounds(2).Center;Main.mouseX=tab.X;Main.mouseY=tab.Y;Main.mouseLeft=Main.mouseLeftRelease=true;
            check(SoulUI.HandleNavigation() && !Main.mouseLeftRelease && SoulUI.IsEditing(navSoul),"Tab navigation consumes click without closing editor");
            check(!SoulUI.TryClickHost(navSoul,out _) && Main.mouseItem.type==ItemID.CobaltSword,"Navigation never deposits cursor item");
            var close=SoulUI.CloseBounds().Center;Main.mouseX=close.X;Main.mouseY=close.Y;Main.mouseLeftRelease=true;
            check(SoulUI.HandleNavigation() && SoulUI.EditingSoul==null && Main.mouseItem.type==ItemID.CobaltSword,"Close button leaves cursor item intact");
        }
        finally
        {
            SoulUI.SelectPage(0);Main.screenWidth=oldW;Main.screenHeight=oldH;Main.UIScale=oldScale;
            Main.mouseItem=oldCursor;Main.mouseLeft=false;Main.mouseLeftRelease=false;
        }
    }
}
