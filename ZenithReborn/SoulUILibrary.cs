using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace ZenithReborn;

// 真实背包的只读索引与候选选择；寄宿动作由配阵操作模块提交。
public partial class SoulUI
{
    public static Rectangle LibraryBounds()=>DomainMode&&page==0?ArmoryLayout.Library:
        new(20,118,Math.Clamp(Main.screenWidth/5,208,310),Math.Max(1,Main.screenHeight-176));
    public static Rectangle LibraryCardBounds(int ordinal)=>ArmoryLayout.LibraryRow(ordinal,libraryScroll);
    public static int[] ArmoryLibraryItems()
    {
        var items=LibraryItems(Main.LocalPlayer).Where(i=>libraryFilter==0||
            !editing.Stored.Any(s=>!s.IsAir&&s.type==Main.LocalPlayer.inventory[i].type));
        if(armorySortDamage)items=items.OrderByDescending(i=>Main.LocalPlayer.inventory[i].damage).ThenBy(i=>i);
        return items.ToArray();
    }
    private static void DrawLibrary(SoulItem soul)
    {
        var b=LibraryBounds();
        RoundedBox(b,new Color(11,17,26),14);
        Text("旧物归还",new(b.X+16,b.Y+18),Champagne,.9f);
        InfoLines("这里保留旧版本遗留的武器。选中后取回背包，即可重新参与配阵。",b.X+16,b.Y+60,b.Width-32,InfoQuiet,.7f);
    }
    private static void DrawArmoryLibrary(SoulItem soul,SoulArmoryLayout layout)
    {
        Rectangle b=layout.Library,list=layout.LibraryList;var mouse=new Point(Main.mouseX,Main.mouseY);
        RoundedBox(b,new Color(13,22,32),12);
        Text("随 身 剑 库",new(b.X+18,b.Y+15),InfoWhite,.94f);
        if(b.Height>=240)Text("先选剑位，再选武器预览",new(b.X+18,b.Y+46),InfoQuiet,.60f);
        for(int f=0;f<2&&b.Height>=240;f++)
        {
            var filter=new Rectangle(b.X+14+f*85,b.Y+74,78,24);bool active=libraryFilter==f;
            if(active)RoundedBox(filter,new Color(38,60,69),6);
            Text(f==0?"全部":"未寄宿",new(filter.X+12,filter.Y+3),active?Accent:InfoQuiet,.59f);
            if(InfoPressed(filter)){libraryFilter=f;libraryScroll=0;}
        }
        var sort=new Rectangle(b.Right-77,b.Y+17,64,26);
        Text(armorySortDamage?"伤害 ↓":"背包序",new(sort.X+4,sort.Y+4),Champagne,.55f);
        if(InfoPressed(sort)){armorySortDamage=!armorySortDamage;libraryScroll=0;}
        int[] items=ArmoryLibraryItems();int height=items.Length*72;
        libraryScroll=InfoScroll(list,libraryScroll,height);
        InfoClip(list,()=>
        {
            for(int i=0;i<items.Length;i++)
            {
                var r=layout.LibraryRow(i,libraryScroll);if(!r.Intersects(list))continue;
                Item item=Main.LocalPlayer.inventory[items[i]];bool hover=list.Contains(mouse)&&r.Contains(mouse);
                bool candidate=CandidateInventorySlot==items[i];float lit=InfoHover("bag/"+items[i],hover||candidate);
                bool stored=soul.Stored.Any(s=>!s.IsAir&&s.type==item.type);
                Color tint=SoulVisuals.Tint(item.type);
                RoundedBox(r,Color.Lerp(new Color(19,30,43),new Color(31,48,60),lit),8);
                if(candidate)Box(new(r.X,r.Y+9,3,r.Height-18),Champagne);
                Glow(new(r.X+29,r.Center.Y),32,tint,.19f+lit*.12f);
                ArmoryItem(item,new(r.X+29,r.Center.Y),34,stored?new Color(159,169,181):Color.White);
                Text(Fit(item.Name,r.Width-72,.7f),new(r.X+57,r.Y+8),candidate?Champagne:InfoWhite,.7f);
                Text($"{item.damage} 伤害  ·  {item.crit}% 暴击",new(r.X+57,r.Y+31),InfoQuiet,.53f);
                // 副标题族名在前：列表里 30 把光剑只看名字和伤害分不出族。
                string family=WeaponFamilyTags.Of(item.type),environment=EnvironmentSwordTooltips.Label(item).Replace("所属环境：","");
                string subtitle=stored?"已有同种寄宿":family.Length==0?environment:family+" · "+environment;
                Text(Fit(subtitle,r.Width-66,.50f),new(r.X+57,r.Y+48),stored?new Color(183,146,122):tint,.50f);
                if(hover)
                {
                    HoveredSlotRequirement=item.Name+" · 点击查看换入对比";
                    if(Main.mouseLeft&&Main.mouseLeftRelease)
                    {Main.mouseLeftRelease=false;SelectArmoryCandidate(items[i]);break;}
                }
            }
            if(items.Length==0)
            {
                Glyph(SoulGlyph.Blade,new(list.Center.X,list.Y+54),36,Accent*.45f);
                CenterText("静候下一把剑",new(list.Center.X,list.Y+88),InfoWhite,.8f);
                InfoLines(libraryFilter==0?"把近战武器放入随身背包，就会出现在这里。":"当前筛选下没有武器，切换“全部”查看。",list.X+14,list.Y+125,list.Width-28,InfoQuiet,.67f);
            }
        });
        InfoScrollMark(list,libraryScroll,height,Accent);
        if(b.Height>=240)Text($"{items.Length} 把武器  /  来自随身背包",new(b.X+17,b.Bottom-27),InfoQuiet,.55f);
    }
}
