using System.Text.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class FollowupCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.Seals=SoulCurses.All;soul.OreCollectionMask=OreFamily.AllMaterials;
        // 旧口径：7 主槽 + 5 支援槽共 12 木；现名册 8 把 → 3 主槽 + 5 支援槽，
        // 保持 11 个支援槽全部占满（后面逐个点击取回需要每格都有物品）。
        for(int i=0;i<3;i++)soul.Hosts[i]=new Item(WoodTypes.Types[i]);
        for(int i=0;i<3;i++)soul.Hosts[i+7]=new Item(OreFamily.DefaultType[i]);
        for(int i=0;i<5;i++)soul.SupportHosts[i]=new Item(WoodTypes.Types[i+3]);
        soul.AdditionalSupports=OreFamily.DefaultType.Skip(3).Take(6).Select(t=>new Item(t)).ToList();
        soul.ForestHosts[0]=new Item(OreFamily.DefaultType[9]);soul.ForestHosts[1]=new Item(OreFamily.DefaultType[10]);
        Main.myPlayer=0;Main.GameMode=2;Main.getGoodWorld=true;
        Main.player[0]=new Player{whoAmI=0,active=true};Main.player[0].inventory[0]=soul.Item;
        SoulUI.DomainMode=true;SoulUI.Open(soul);
        var samples=new List<object>();
        foreach(var size in new[]{(1280,720),(1920,1080),(2560,1440)})foreach(float scale in new[]{1f,1.25f,2f})
        {
            Main.UIScale=scale;Main.screenWidth=(int)(size.Item1/scale);Main.screenHeight=(int)(size.Item2/scale);
            var nodes=soul.VisibleSlotIds.Select(id=>(id,rect:SoulUI.SlotBounds(id))).ToArray();
            var overlaps=nodes.SelectMany((a,i)=>nodes.Skip(i+1).Where(b=>a.rect.Intersects(b.rect)).Select(b=>new[]{a.id,b.id})).ToArray();
            samples.Add(new{width=size.Item1,height=size.Item2,scale,supports=soul.SupportCapacity,overlaps});
            check(overlaps.Length==0,"Eleven actual support nodes do not overlap any main/reward node: "+size+" / "+scale);
            check(nodes.All(n=>n.rect.Left>=SoulUI.ContentBounds().Left && n.rect.Right<=SoulUI.ContentBounds().Right),
                "All eleven support targets stay horizontally inside the scrolling workspace: "+size+" / "+scale);
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Environment.GetEnvironmentVariable("ZENITH_CHECK_REPORT")),"followup-layout.json"),JsonSerializer.Serialize(samples));
        check(soul.SupportCapacity==11,"Audit construct actually activates eleven shared supports");
        Main.getGoodWorld=false;
        Main.screenWidth=1280;Main.screenHeight=720;Main.UIScale=1;SoulUI.SelectPage(0);
        foreach(var host in soul.Hosts)host.TurnToAir();foreach(var host in soul.ForestHosts)host.TurnToAir();soul.OreCollectionMask=0;
        var paused=soul.VisibleSlotIds.Select(id=>(id,rect:SoulUI.SlotBounds(id))).ToArray();
        check(soul.SupportCapacity==0 && paused.All(a=>!paused.Any(b=>a.id!=b.id && a.rect.Intersects(b.rect))),
            "Dropping to zero grants preserves distinct paused support hit targets for recovery");
        foreach(int id in soul.VisibleSlotIds.Where(id=>SoulItem.SupportOrdinal(id)>=0))
        {
            var at=SoulUI.SlotBounds(id).Center;Main.mouseX=at.X;Main.mouseY=at.Y;Main.mouseItem=new Item();
            Main.mouseLeft=Main.mouseLeftRelease=true;SoulUI.DomainMode=false;
            // Use a large viewport so every saved paused target is in the
            // active body; this exercises the real click dispatcher.
            Main.screenWidth=1920;Main.screenHeight=1080;at=SoulUI.SlotBounds(id).Center;Main.mouseX=at.X;Main.mouseY=at.Y;
            check(SoulUI.TryClickHost(soul,out _) && !Main.mouseItem.IsAir,"Actual paused slot click retrieves its own item: "+id);
            Main.mouseItem=new Item();SoulUI.DomainMode=true;
        }
        var future=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;future.Attached=true;
        future.AdditionalSupports=Enumerable.Range(0,10).Select(i=>new Item(OreFamily.DefaultType[i])).ToList();
        future.LegendaryHost=new Item(ItemID.Meowmere);
        // 旧口径（0.3.129 之前）：无圣林槽，第 15 个支援是 35。现 35/36 归圣林之冠，支援后移两位到 37。
        check(SoulItem.SupportSlotId(14)==37 && SoulItem.SupportOrdinal(37)==14 && SoulItem.SupportOrdinal(34)==-1 &&
            SoulItem.SupportOrdinal(35)==-1 && SoulItem.SupportOrdinal(36)==-1 &&
            future.ItemAt(37).type==OreFamily.DefaultType[9] && future.ItemAt(34).type==ItemID.Meowmere &&
            future.VisibleSlotIds.Distinct().Count()==future.VisibleSlotIds.Count(),"Reserved legendary and crown IDs cannot alias a future fifteenth support or duplicate a visible slot");
        Item recovered=new();check(future.TrySwapHost(37,ref recovered,out _) && recovered.type==OreFamily.DefaultType[9] &&
            future.LegendaryHost.type==ItemID.Meowmere,"Future paused support beyond the legendary and crown IDs can be recovered without touching legendary storage");
        var detail=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;detail.Attached=true;detail.Kills=SoulProgress.All;detail.Cores=63;
        detail.LegacyReleased=0;detail.Seals=0;detail.Hosts[0]=new Item(ItemID.WoodenSword);
        detail.Hosts[1]=new Item(ItemID.CopperBroadsword);detail.Hosts[2]=new Item(ItemID.TinBroadsword);
        var text=SoulTraits.All(detail).Single(t=>t.Key=="ore-wave").Description;
        check(OreFamily.Count(detail)==2 && text.Contains("主攻2") && !text.Contains("10帧冷却"),
            "Codex primary material breakdown and cooldown text match the confirmed curse exception");
        SoulUI.Toggle(soul);
    }
}
