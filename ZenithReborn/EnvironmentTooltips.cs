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
        string family=WeaponFamilyTags.Of(item.type);
        if(family.Length>0)
            index=InsertWrapped(tooltips,index,"SwordFamily","所属种族：",family.Split('、'),FamilyColor);
        // Keep multi-environment items readable without a screen-wide line.
        index=InsertWrapped(tooltips,index,"SwordEnvironment","所属环境：",tags.Count==0?new[]{"未归类"}:tags.Select(t=>t.Name),color);
    }
}
