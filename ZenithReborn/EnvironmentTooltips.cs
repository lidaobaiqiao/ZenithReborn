using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

public class EnvironmentSwordTooltips : GlobalItem
{
    public static string Label(Item item)
    {
        if(item==null || !SoulItem.CanHost(item))return "";
        var tags=item.GetEnvironmentAttributes();
        return "所属环境："+(tags.Count==0?"未归类":string.Join("、",tags.Select(t=>t.Name)));
    }
    public override void ModifyTooltips(Item item,List<TooltipLine> tooltips)
    {
        string label=Label(item);if(label.Length==0)return;
        var tags=item.GetEnvironmentAttributes();
        Color color=tags.Count==1?EnvironmentRules.Color(tags[0].Id):tags.Count>1?new Color(148,212,221):new Color(160,166,177);
        int index=tooltips.FindIndex(t=>t.Mod=="Terraria" && t.Name=="ItemName");
        index=index<0?tooltips.Count:index+1;
        // Keep multi-environment items readable without a screen-wide line.
        string line="所属环境：";int number=0;
        foreach(string name in tags.Count==0?new[]{"未归类"}:tags.Select(t=>t.Name))
        {
            string next=line+(line is "所属环境：" or "　　"?"":"、")+name;
            if(next.Length>28 && line is not ("所属环境：" or "　　"))
            {
                tooltips.Insert(index++,new(Mod,"SwordEnvironment"+number++,line){OverrideColor=color});
                line="　　"+name;
            }
            else line=next;
        }
        tooltips.Insert(index,new(Mod,"SwordEnvironment"+number,line){OverrideColor=color});
    }
}
