using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class DomainCheck
{
    internal static void Run(Action<bool,string> check)
    {
        int width=Main.screenWidth,height=Main.screenHeight;float ui=Main.UIScale;
        var oldData=Main.ActiveWorldFileData;int oldMode=Main.GameMode;bool oldWorthy=Main.getGoodWorld,oldMenu=Main.gameMenu;
        Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.mouseItem=new Item();
        var p=new Player{active=true,whoAmI=0};Main.player[0]=p;
        var s=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;s.Attached=true;
        p.inventory[0]=s.Item;p.inventory[1]=new Item(ItemID.IronBroadsword);p.inventory[1].Prefix(PrefixID.Legendary);
        p.inventory[2]=new Item(ItemID.WoodenBow);p.inventory[3]=new Item(ItemID.CopperPickaxe);
        try
        {
            SoulUI.DomainMode=true;SoulUI.Open(s);
            check(SoulUI.LibraryItems(p).SequenceEqual(new[]{1}),"Library reads only actual eligible bag weapons, excluding bows, tools and recursive souls");
            foreach(var size in new[]{(800,600),(1280,720),(1920,1080),(2560,1440)})foreach(float scale in new[]{1f,1.25f,1.6296296f,2f})
            {
                Main.UIScale=scale;Main.screenWidth=(int)(size.Item1/scale);Main.screenHeight=(int)(size.Item2/scale);
                var panel=SoulUI.PanelBounds();var library=SoulUI.LibraryBounds();var work=SoulUI.ContentBounds();
                check(panel==new Rectangle(0,0,Main.screenWidth,Main.screenHeight),"Independent scene covers the complete UI viewport");
                check(panel.Contains(library) && panel.Contains(work) && !library.Intersects(work),"Sword library and main workspace stay separate and on-screen");
                check(panel.Contains(SoulUI.CloseBounds()) && panel.Contains(SoulUI.DifficultyBounds()),"World difficulty and exit controls remain reachable");
                check(Enumerable.Range(0,4).All(i=>!SoulUI.TabBounds(i).Intersects(library) && !SoulUI.TabBounds(i).Intersects(SoulUI.DifficultyBounds())),"Fragment navigation does not collide with inventory or difficulty controls");
            }
            Main.screenWidth=1280;Main.screenHeight=720;Main.UIScale=1;SoulUI.SelectPage(0);SoulUI.SelectSlot(0);
            Main.hideUI=Main.mapFullscreen=false;
            var layers=new List<Terraria.UI.GameInterfaceLayer>{new Terraria.UI.LegacyGameInterfaceLayer("Vanilla: Inventory",()=>true),
                new Terraria.UI.LegacyGameInterfaceLayer("Vanilla: Resource Bars",()=>true),new Terraria.UI.LegacyGameInterfaceLayer("Vanilla: Mouse Text",()=>true),
                new Terraria.UI.LegacyGameInterfaceLayer("Vanilla: Cursor",()=>true,Terraria.UI.InterfaceScaleType.None),
                new Terraria.UI.LegacyGameInterfaceLayer("Vanilla: Mouse Item / NPC Head",()=>true)};
            var cursorLayer=layers.Single(l=>l.Name=="Vanilla: Cursor");
            var carriedLayer=layers.Single(l=>l.Name=="Vanilla: Mouse Item / NPC Head");
            ModContent.GetInstance<SoulUI>().ModifyInterfaceLayers(layers);
            check(!layers.Any(l=>l.Name=="Vanilla: Inventory" || l.Name=="Vanilla: Resource Bars") && layers.Any(l=>l.Name=="Vanilla: Mouse Text"),
                "Fullscreen scene removes hidden background hover producers while retaining native mouse text");
            check(layers.Contains(cursorLayer) && layers.IndexOf(cursorLayer)>layers.FindIndex(l=>l.Name=="ZenithReborn: Soul") &&
                cursorLayer.ScaleType==Terraria.UI.InterfaceScaleType.None,"Actual native cursor layer survives above the fullscreen scene with its original pixel scale");
            check(layers.Contains(carriedLayer) && layers.IndexOf(carriedLayer)>layers.IndexOf(cursorLayer),"Carried mouse item remains visible above the native cursor without restoring hidden inventory");
            Item original=p.inventory[1];
            check(SoulUI.EquipFromInventory(p,1,out _) && ReferenceEquals(s.Hosts[0],original) && s.Hosts[0].prefix==PrefixID.Legendary && p.inventory[1].IsAir,"Direct deposit transfers the exact prefixed item without cloning");
            p.inventory[1]=new Item(ItemID.IceBlade);Item incoming=p.inventory[1];
            check(SoulUI.EquipFromInventory(p,1,out _) && ReferenceEquals(s.Hosts[0],incoming) && ReferenceEquals(p.inventory[1],original),"Replacement returns outgoing sword to the vacated bag position atomically");
            SoulUI.SelectSlot(9);check(!SoulUI.EquipFromInventory(p,1,out _) && ReferenceEquals(p.inventory[1],original),"Locked main slots reject sword-library deposits without touching source");
            SoulUI.SelectSlot(17);check(!SoulUI.EquipFromInventory(p,1,out _),"Library cannot reopen retired expansion slots");
            SoulUI.SelectSlot(0);p.inventory[4]=new Item(ItemID.IceBlade);s.Hosts[9]=new Item(ItemID.IceBlade);
            check(!SoulUI.EquipFromInventory(p,4,out _) && p.inventory[4].type==ItemID.IceBlade,"Duplicate-host validation also applies to quick equip");
            s.Hosts[9].TurnToAir();
            Main.mouseItem=new Item(ItemID.TerraBlade);check(!SoulUI.EquipFromInventory(p,1,out _) && !SoulUI.ReturnSelected(p,out _),"Held cursor item cannot be overwritten by library operations");Main.mouseItem=new Item();
            var hit=SoulUI.SlotBounds(0).Center;Main.mouseX=hit.X;Main.mouseY=hit.Y;Main.mouseLeft=Main.mouseLeftRelease=true;
            check(!SoulUI.TryClickHost(s,out _) && Main.mouseItem.IsAir && ReferenceEquals(s.Hosts[0],incoming),"Empty-hand constellation click selects without extracting into hidden inventory");
            check(SoulUI.ReturnSelected(p,out _) && s.Hosts[0].IsAir && p.inventory.Take(50).Any(i=>ReferenceEquals(i,incoming)),"Take-off returns the exact sword to real inventory");
            s.Hosts[0]=incoming;for(int i=1;i<50;i++)p.inventory[i]=new Item(ItemID.StoneBlock);
            check(!SoulUI.ReturnSelected(p,out _) && ReferenceEquals(s.Hosts[0],incoming),"Full bag refuses take-off without dropping or deleting the sword");
            s.Hosts[10]=new Item(ItemID.GoldBroadsword);p.inventory[1]=new Item();SoulUI.SelectSlot(17);
            check(SoulUI.ReturnSelected(p,out _) && s.Hosts[10].IsAir && p.inventory[1].type==ItemID.GoldBroadsword,"Legacy recovery still returns items through the new backpack route");
            var extended=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            extended.Attached=true;extended.Kills=SoulProgress.All;extended.Cores=63;extended.Seals=SoulCurses.All;
            for(int i=0;i<10;i++)extended.Hosts[i]=new Item(OreFamily.DefaultType[i]);
            p.inventory[0]=extended.Item;p.inventory[1]=new Item(ItemID.Meowmere);p.inventory[1].Prefix(PrefixID.Legendary);
            SoulUI.Open(extended);SoulUI.SelectSlot(25);
            check(SoulUI.SelectedSlot==25 && SoulUI.EquipFromInventory(p,1,out _) && extended.AdditionalSupports.Single().type==ItemID.Meowmere,
                "Sword library selects and deposits into the actual sixth earned shared support");
            SoulUI.SelectPage(1);check(SoulUI.SelectedSlot==25,"Changing tabs preserves selection of a live new support instead of confusing it with retired storage");
            SoulUI.SelectPage(0);foreach(var host in extended.Hosts)host.TurnToAir();
            check(extended.SupportCapacity==0 && SoulUI.ReturnSelected(p,out _) && p.inventory[1].type==ItemID.Meowmere && p.inventory[1].prefix==PrefixID.Legendary,
                "Full domain can return a paused new support to the real backpack with its prefix intact");
            Difficulty(check);
        }
        finally
        {
            if(SoulUI.EditingSoul!=null)SoulUI.Toggle(SoulUI.EditingSoul);
            SoulUI.DomainMode=false;SoulInput.Reset();Main.screenWidth=width;Main.screenHeight=height;Main.UIScale=ui;
            Main.ActiveWorldFileData=oldData;Main.GameMode=oldMode;Main.getGoodWorld=oldWorthy;Main.gameMenu=oldMenu;
            Main.mouseItem=new Item();Main.mouseLeft=Main.mouseLeftRelease=false;
        }
    }
    private static void Difficulty(Action<bool,string> check)
    {
        foreach(var n in Main.npc)n.active=false;NPC.MoonLordCountdown=0;Main.gameMenu=false;
        var state=SoulDifficulty.Instance;
        foreach(int mode in new[]{0,1,2})foreach(bool worthy in new[]{false,true})
        {
            Main.ActiveWorldFileData=new WorldFileData("isolated-difficulty.wld",false){ForTheWorthy=worthy};
            Main.GameMode=mode;Main.getGoodWorld=worthy;state.OnWorldLoad();
            if(mode==2 && worthy)
            {check(!SoulDifficulty.Toggle(out _) && Main.GameMode==2 && Main.getGoodWorld,"Pre-existing native legendary world is not accidentally downgraded");continue;}
            check(SoulDifficulty.Toggle(out _) && Main.GameMode==2 && Main.getGoodWorld && Main.masterMode && Main.expertMode && Main.ActiveWorldFileData.ForTheWorthy,"Difficulty button changes real native master and FTW world flags");
            var tag=new TagCompound();state.SaveWorldData(tag);state.OnWorldLoad();state.LoadWorldData(tag);
            check(state.Applied,"Saved world retains restore provenance after loading");
            check(SoulDifficulty.Toggle(out _) && Main.GameMode==mode && Main.getGoodWorld==worthy && Main.ActiveWorldFileData.ForTheWorthy==worthy,"Second click restores exact original difficulty and FTW status");
        }
        Main.GameMode=3;Main.getGoodWorld=false;check(!SoulDifficulty.Toggle(out _) && Main.GameMode==3,"Journey worlds keep their native world type");
        Main.GameMode=0;Main.netMode=NetmodeID.MultiplayerClient;check(!SoulDifficulty.Toggle(out _) && Main.GameMode==0,"Client cannot silently alter a multiplayer world");
        Main.netMode=NetmodeID.SinglePlayer;Main.npc[20].active=true;Main.npc[20].boss=true;
        check(!SoulDifficulty.Toggle(out _) && Main.GameMode==0,"Existing boss fight cannot change its stat baseline mid-combat");Main.npc[20].active=false;
        NPC.MoonLordCountdown=100;check(!SoulDifficulty.Toggle(out _),"Pending Moon Lord event also blocks switching");NPC.MoonLordCountdown=0;
        state.OnWorldUnload();
    }
}
