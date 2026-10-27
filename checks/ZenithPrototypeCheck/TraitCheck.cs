using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;
internal static class TraitCheck
{
    private static SoulItem Soul(bool attached=true)
    {
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        soul.Attached=attached;return soul;
    }
    private static HashSet<string> Keys(SoulItem soul)=>SoulTraits.Active(soul).Select(t=>t.Key).ToHashSet();
    private static byte[] Snapshot(SoulItem soul)
    {using var bytes=new MemoryStream();soul.NetSend(new BinaryWriter(bytes));return bytes.ToArray();}
    internal static void Run(Action<bool,string> check)
    {
        Main.netMode=NetmodeID.SinglePlayer;Main.myPlayer=0;Main.player[0]=new Player {active=true,whoAmI=0};
        var soul=Soul(false);soul.Hosts[0]=new Item(ItemID.WoodenSword);
        check(!SoulTraits.Active(soul).Any(),"Unattached preview never displays active traits");
        var all=SoulTraits.All(soul);
        check(Enumerable.Range(0,12).All(i=>all.Any(t=>t.Key=="wood-"+i)) && all.Count(t=>t.Key.StartsWith("ore-trait-"))==6,"Codex keeps all twelve wood and six implemented hard-ore effects without equipment");
        check(all.All(t=>t.Requirement.Length>0 && t.Description.Length>0) && all.Select(t=>t.Key).Distinct().Count()==all.Count,"Every codex entry has a unique key, current description and activation requirement");
        soul.Attached=true;soul.SupportHosts[0]=new Item(ItemID.CobaltSword);
        var active=Keys(soul);
        check(active.Contains("wood-set") && active.Contains("wood-0") && active.Contains("ore-trait-0"),"Valid wood main and Cobalt support activate exact corresponding effects");
        check(active.Contains("ore-wave") && active.Contains("shared-support") && !active.Contains("forest") && !active.Contains("wood-1"),"One active ore support unlocks flight through the shared mechanism without inventing full forest or missing wood");
        check(active.Contains("attachment") && Enum.GetValues<SoulCurse>().All(c=>active.Contains("curse-"+c)) && !active.Contains("mech-curse"),"Six current curse states replace the obsolete mechanical curse entry");
        soul.Hosts[1]=new Item(ItemID.CobaltSword);
        check(Keys(soul).Contains("ore-wave") && OreFamily.Count(soul)==1,"Locked duplicate ore primary does not change the one material supplied by legal support");
        soul.Kills=SoulProgress.All;soul.Cores=63;
        active=Keys(soul);
        check(active.Contains("ore-wave") && soul.Curse(SoulCurse.Solitude)==CurseStage.Released && !active.Contains("mech-blessing"),"Unlocked ore main activates wave; compatibility restores normal ability without inventing a personal blessing");
        soul.Hosts[1].TurnToAir();soul.SupportHosts[0].TurnToAir();
        soul.Hosts[10]=new Item(ItemID.CobaltSword);soul.SupportHosts[5]=new Item(ItemID.IceBlade);
        soul.ForestHosts[0]=new Item(ItemID.TitaniumSword);
        active=Keys(soul);
        check(!active.Contains("ore-wave") && !active.Contains("ore-trait-0") && !active.Contains("ore-trait-5") && !active.Contains("family-Frost"),"Recovery storage and paused forest slot do not leak effects into summary");
        soul.SupportHosts[0]=new Item(ItemID.IceBlade);
        check(Keys(soul).Contains("family-Frost"),"Valid support family is represented in active summary");
        soul.Hosts[0].TurnToAir();
        check(!Keys(soul).Contains("family-Frost"),"Losing wood unlock pauses Ice support immediately");
        var full=Soul();full.Kills=SoulProgress.All;full.Cores=63;
        for(int i=0;i<7;i++)full.Hosts[i]=new Item(WoodTypes.Types[i]);
        for(int i=0;i<5;i++)full.SupportHosts[i]=new Item(WoodTypes.Types[i+7]);
        check(Keys(full).Contains("forest") && Enumerable.Range(0,12).All(i=>Keys(full).Contains("wood-"+i)),"Full valid wood set displays all twelve effects and Forest Power");
        var tips=new List<TooltipLine>();full.ModifyTooltips(tips);
        check(tips.Count==3 && tips.All(t=>!t.Name.StartsWith("Trait-")) && tips.Any(t=>t.Name=="TraitCodex"),
            "Full forest loadout keeps only three compact tooltip lines, detailed traits remain in UI");
        check(tips.All(t=>t.Text.Length<=28 && !t.Text.Contains('\n')),"Every custom tooltip line stays short and single-line");
        full.Hosts[0].SetNameOverride(new string('剑',300));tips.Clear();full.ModifyTooltips(tips);
        check(tips.Count==3 && tips.All(t=>t.Text.Length<=28),"Long hosted names never inflate the soul tooltip");
        full.Hosts[6].TurnToAir();tips.Clear();full.ModifyTooltips(tips);
        check(!tips.Any(t=>t.Name=="Trait-forest" || t.Name=="Trait-wood-6") && tips.Any(t=>t.Name=="TraitCodex"),"Removing a wood hides its effect and Forest Power in the real tooltip while retaining codex link");
        Main.player[0].inventory[0]=full.Item;SoulUI.Open(full);
        byte[] before=Snapshot(full);SoulUI.SelectTraitScope(false);
        for(int filter=0;filter<5;filter++)
        {
            SoulUI.SelectTraitCategory(filter);
            var entries=SoulUI.TraitEntries(full).ToArray();
            check(entries.Length>0 && (filter==0 || entries.All(t=>(int)t.Category==filter-1)),"Codex category filter returns correct implemented entries: "+filter);
        }
        SoulUI.SelectTraitCategory(0);SoulUI.SelectTraitScope(true);
        check(SoulUI.TraitEntries(full).All(t=>t.Active)&&SoulUI.TraitEntries(full).Count()==SoulTraits.Active(full).Count(),"生效筛选只显示当前实际生效的条目");
        SoulUI.SelectTraitScope(false);
        check(SoulUI.TraitEntries(full).Count()==SoulTraits.All(full).Count(),"同一工作区可恢复全部特性及激活条件");
        check(before.SequenceEqual(Snapshot(full)),"Browsing and filtering codex never changes stored items or progression");
        Main.screenWidth=1280;Main.screenHeight=900;
        SoulUI.SelectPage(SoulUI.TraitsPage);Point point=SoulUI.SlotBounds(0).Center;
        Main.mouseX=point.X;Main.mouseY=point.Y;Main.mouseItem=new Item(ItemID.CopperShortsword);Main.mouseLeft=Main.mouseLeftRelease=true;
        check(!SoulUI.TryClickHost(full,out _) && Main.mouseLeftRelease && Main.mouseItem.type==ItemID.CopperShortsword,"Catalog cannot consume slot clicks or deposit cursor item");
        full.Hosts[10]=new Item(ItemID.TerraBlade);
        check(SoulUI.TabBounds(SoulUI.RecoveryPage)==Rectangle.Empty,"有旧物时也不增加主导航，回收由配剑区提示进入");
        SoulUI.SelectPage(SoulUI.RecoveryPage);point=SoulUI.ExtendedSlotBounds(0).Center;
        Main.mouseX=point.X;Main.mouseY=point.Y;Main.mouseItem=new Item();Main.mouseLeftRelease=true;
        check(SoulUI.TryClickHost(full,out _) && Main.mouseItem.type==ItemID.TerraBlade && !full.HasLegacyExpansion,"配剑区回收视图保留旧物原引用，取空后不再有旧物");
        full.Attached=false;full.AwakeningMemory=true;
        check(!SoulTraits.Active(full).Any(),"Detached stored progress never advertises usable powers");
        tips.Clear();full.ModifyTooltips(tips);
        check(tips.Count==4 && tips.All(t=>t.Text.Length<=28 && !t.Text.Contains('\n')) && tips.Any(t=>t.Name=="Unstable"),
            "Unattached tooltip adds only one short next-action hint");
        SoulUI.Toggle(full);SoulUI.SelectTraitCategory(0);SoulUI.SelectPage(0);
        Main.mouseItem=new Item();Main.mouseLeft=false;Main.mouseLeftRelease=false;
    }
}
