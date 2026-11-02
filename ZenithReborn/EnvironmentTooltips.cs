using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

public class EnvironmentSwordTooltips : GlobalItem
{
    // 族名行用青色，与环境行的群系颜色区分开，但不压过物品名。
    private static readonly Color FamilyColor=new(148,212,221);
    public static string Label(Item item)
    {
        if(item==null || !SoulItem.CanHost(item))return "";
        var tags=item.GetEnvironmentAttributes();
        return "所属环境："+(tags.Count==0?"未归类":string.Join("、",tags.Select(t=>t.Name)));
    }
    // 种族行与环境行共用同一套折行：每行不超过28字，续行以两个全角空格起头。
    // 名称本身超长时不强拆，避免把族名/群系名切成两半。
    private int InsertWrapped(List<TooltipLine> tooltips,int index,string key,string head,IEnumerable<string> names,Color color)
    {
        string line=head;int number=0;
        foreach(string name in names)
        {
            bool fresh=line==head||line=="　　";
            string next=line+(fresh?"":"、")+name;
            if(next.Length>28 && !fresh)
            {
                tooltips.Insert(index++,new TooltipLine(Mod,key+number++,line){OverrideColor=color});
                line="　　"+name;
            }
            else line=next;
        }
        tooltips.Insert(index++,new TooltipLine(Mod,key+number,line){OverrideColor=color});
        return index;
    }
    public override void ModifyTooltips(Item item,List<TooltipLine> tooltips)
    {
        string label=Label(item);if(label.Length==0)return;
        var tags=item.GetEnvironmentAttributes();
        Color color=tags.Count==1?EnvironmentRules.Color(tags[0].Id):tags.Count>1?new Color(148,212,221):new Color(160,166,177);
        int index=tooltips.FindIndex(t=>t.Mod=="Terraria" && t.Name=="ItemName");
        index=index<0?tooltips.Count:index+1;
        // 先认族再看环境：族名行插入在环境行之前，无族武器不显示这一行。
        // 一并带上当前阶位，否则玩家只看到族名、不知道还差几把进下一阶。
        // 阶位只有飞刃族才产出，非飞刃族直接传 null，避免每帧悬停都去遍历背包找剑魂。
        // 这一行是单行、且阶位括号不可拆，所以允许超过 InsertWrapped 的 28 字折行阈值；
        // 现实最长（多族 + 满阶）约 26–37 字，原版 tooltip 会自适应撑宽而不会截断。
        string familyLine=WeaponFamilyTags.Line(item.type,FlyingBladeFamily.IsMember(item.type)?ActiveSoul():null);
        if(familyLine.Length>0)
        {
            tooltips.Insert(index,new(Mod,"SwordFamily0",familyLine){OverrideColor=FamilyColor});
            index++;
        }
        // Keep multi-environment items readable without a screen-wide line.
        index=InsertWrapped(tooltips,index,"SwordEnvironment","所属环境：",tags.Count==0?new[]{"未归类"}:tags.Select(t=>t.Name),color);
    }

    // tooltip 本身不属于某个剑魂，但族阶位依赖剑魂。
    // 三级回退：正在编辑的 → 手持的 → 背包里第一枚已附着的；都没有就只显示族名，不显示阶位。
    private static SoulItem ActiveSoul()
    {
        var player=Main.LocalPlayer;
        if(player==null)return null;
        // 优先正在编辑的剑魂：魂界面打开时左侧面板也用它，避免同屏出现两个不同的阶位。
        if(SoulUI.EditingSoul is SoulItem editing && editing.Attached &&
           player.inventory.Any(i=>ReferenceEquals(i.ModItem,editing)))return editing;
        // 其次手持：这是全模组运行时实际生效的那枚。
        if(player.HeldItem.ModItem is SoulItem held && held.Attached)return held;
        // 最后退回背包里第一枚已附着的。
        foreach(var slot in player.inventory)
            if(slot.ModItem is SoulItem soul && soul.Attached)return soul;
        return null;
    }
}
