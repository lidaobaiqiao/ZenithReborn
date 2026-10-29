using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 一个特性工作区同时查看当前力量与全部资料，列表和详情分别滚动。
public partial class SoulUI
{
    private static bool activeTraitsOnly=true;
    private static string traitSelection="";
    private static int traitListScroll,traitDetailScroll;
    public static bool ActiveTraitsOnly=>activeTraitsOnly;
    public static int TraitCategoryFilter=>catalogFilter;
    public static string SelectedTraitKey=>traitSelection;
    public static void SelectTraitScope(bool activeOnly)
    {activeTraitsOnly=activeOnly;traitListScroll=traitDetailScroll=0;traitSelection="";}
    public static void SelectTraitCategory(int value)
    {catalogFilter=Math.Clamp(value,0,4);traitListScroll=traitDetailScroll=0;traitSelection="";}
    public static IEnumerable<SoulTrait> TraitEntries(SoulItem soul)=>SoulTraits.All(soul)
        .Where(t=>(!activeTraitsOnly||t.Active)&&(catalogFilter==0||(int)t.Category==catalogFilter-1));
    private static Color TraitColor(TraitCategory category)=>category switch {
        TraitCategory.Wood=>new(135,211,168),TraitCategory.Ore=>new(126,194,218),
        TraitCategory.Growth=>new(224,193,130),_=>new(184,162,224)};
    private static int TraitItem(SoulTrait trait)
    {
        if(trait.Key.StartsWith("wood-")&&int.TryParse(trait.Key[5..],out int wood)&&wood>=0&&wood<WoodTypes.Types.Length)return WoodTypes.Types[wood];
        if(trait.Key.StartsWith("ore-trait-")&&int.TryParse(trait.Key[10..],out int ore)&&ore>=0&&ore<HardOreTypes.Types.Length)return HardOreTypes.Types[ore];
        if(trait.Key.StartsWith("special-")&&int.TryParse(trait.Key[8..],out int special))return special;
        return trait.Key switch {
            "ore-copper-echo" or "copper-close"=>ItemID.CopperShortsword,"ore-tin-growth"=>ItemID.TinBroadsword,
            "wood-set" or "forest"=>ItemID.WoodenSword,"ore-wave" or "ore-mastery"=>ItemID.TitaniumSword,
            "family-Swift"=>ItemID.CopperShortsword,"family-Solid"=>ItemID.BoneSword,
            "family-Frost"=>ItemID.IceBlade,"family-Nature"=>ItemID.BladeofGrass,"family-Arcane"=>ItemID.EnchantedSword,
            _=>0};
    }
    private static int DrawTraits(SoulItem soul,Rectangle b,int unused)
    {
        Color accent=SoulUIPages.Color(TraitsPage);var all=SoulTraits.All(soul);
        int y=InfoTitle(b,"剑魂特性",$"当前生效 {all.Count(t=>t.Active)} / 全部 {all.Count}",SoulGlyph.Spark,accent);
        int modeWidth=Math.Min(154,(b.Width-46)/2);bool inline=b.Width>=850;
        int modeX=inline?b.Right-modeWidth*2-24:b.X+14,modeY=inline?b.Y+8:y;
        var activeMode=new Rectangle(modeX,modeY,modeWidth,34);var allMode=new Rectangle(modeX+10+modeWidth,modeY,modeWidth,34);
        if(InfoAction(activeMode,"当前生效",activeTraitsOnly?accent:InfoQuiet,true,SoulGlyph.Check))SelectTraitScope(true);
        if(InfoAction(allMode,"全部特性",!activeTraitsOnly?accent:InfoQuiet,true,SoulGlyph.Book))SelectTraitScope(false);
        var chosenMode=activeTraitsOnly?activeMode:allMode;
        Box(new(chosenMode.X+12,chosenMode.Bottom-1,chosenMode.Width-24,2),accent*.8f);
        if(!inline)y+=46;
        int filterWidth=Math.Min(840,Math.Max(20,b.Width-28)),columns=Math.Clamp(filterWidth/132,2,5),step=filterWidth/columns;
        for(int i=0;i<5;i++)
        {
            int filter=i;var source=all.Where(t=>!activeTraitsOnly||t.Active);
            int count=source.Count(t=>filter==0||(int)t.Category==filter-1);
            string label=(i==0?"全部":SoulTraits.CategoryName((TraitCategory)(i-1)))+" "+count;
            var r=new Rectangle(b.X+14+i%columns*step,y+i/columns*38,step-8,30);
            if(InfoAction(r,label,catalogFilter==i?accent:InfoQuiet,true,i==0?SoulGlyph.Book:CategoryGlyph((TraitCategory)(i-1))))SelectTraitCategory(i);
            if(catalogFilter==i)Box(new(r.X+12,r.Bottom-1,Math.Max(1,r.Width-24),2),accent*.8f);
        }
        y+=((5+columns-1)/columns)*38+10;
        var panes=SoulInfoLayout.MasterDetail(new(b.X+14,y,b.Width-28,Math.Max(1,b.Bottom-y-12)));
        var entries=TraitEntries(soul).ToArray();
        if(entries.Length==0)
        {
            InfoBlock("暂无匹配特性","切换分类或查看全部特性，可以查阅对应激活条件。",panes.List.X,panes.List.Y,panes.List.Width,accent,SoulGlyph.Book);
            return b.Bottom;
        }
        if(!entries.Any(t=>t.Key==traitSelection))traitSelection=entries[0].Key;
        traitListScroll=InfoScroll(panes.List,traitListScroll,entries.Length*66);
        InfoSurface(panes.List,accent);
        InfoClip(panes.List,()=>
        {
            for(int i=0;i<entries.Length;i++)
            {
                var trait=entries[i];var r=new Rectangle(panes.List.X+6,panes.List.Y+i*66-traitListScroll,panes.List.Width-14,60);
                if(!r.Intersects(panes.List))continue;
                bool chosen=traitSelection==trait.Key,hover=InfoHovered(r)&&panes.List.Contains(Main.mouseX,Main.mouseY);
                float motion=InfoHover("trait-row/"+trait.Key,chosen||hover);Color tint=TraitColor(trait.Category);
                if(chosen||hover)RoundedBox(r,new Color(38,48,64)*(.38f+motion*.52f),7);
                if(chosen)Box(new(r.X,r.Y+11,2,38),tint);
                if(TraitItem(trait)>0)InfoItem(TraitItem(trait),new(r.X+29,r.Center.Y),29,trait.Active?1:.5f);
                else Glyph(CategoryGlyph(trait.Category),new(r.X+28,r.Center.Y),22,tint);
                Text(Fit(trait.Title,r.Width-68,.8f),new(r.X+54,r.Y+9),trait.Active?InfoWhite:Muted,.8f);
                string status=SoulBossUnlocks.TryCurse(trait.Key,out var rowCurse)?
                    (soul.Attached?SoulCurses.StageName(soul.Curse(rowCurse)):"待附着"):(trait.Active?"已生效":"待激活");
                InfoBadge(status+" · "+SoulTraits.CategoryName(trait.Category),new(r.X+55,r.Y+35),trait.Active?tint:InfoQuiet);
                if(hover&&InfoPressed(r)){traitSelection=trait.Key;traitDetailScroll=0;detailStarted=UiTime;}
            }
        });
        InfoScrollMark(panes.List,traitListScroll,entries.Length*66,accent);
        var selectedTrait=entries.First(t=>t.Key==traitSelection);Color color=TraitColor(selectedTrait.Category);
        var unlocks=SoulBossUnlocks.TryCurse(selectedTrait.Key,out var selectedCurse)?SoulBossUnlocks.Read(soul,selectedCurse):null;
        InfoSurface(panes.Detail,color);var viewport=new Rectangle(panes.Detail.X+16,panes.Detail.Y+12,panes.Detail.Width-32,Math.Max(1,panes.Detail.Height-24));
        int content=100;
        void Detail(bool render)
        {
            int top=viewport.Y-traitDetailScroll;
            if(render)
            {
                Glyph(CategoryGlyph(selectedTrait.Category),new(viewport.X+15,top+17),23,color);
                Text(Fit(selectedTrait.Title,viewport.Width-48,1f),new(viewport.X+38,top+1),InfoWhite*DetailReveal,1f);
                string status=unlocks!=null?$"{(soul.Attached?"当前 · "+SoulCurses.StageName(soul.Curse(selectedCurse)):"待附着")}   |   个人铭刻 {unlocks.Count}/{unlocks.Total}":
                    selectedTrait.Active?"当前寄宿条件已满足":"尚未满足激活条件";
                InfoBadge(status,new(viewport.X+4,top+40),color);
                Line(new(viewport.X,top+69),new(viewport.Right,top+69),color*.18f);
            }
            int bottom=InfoLines(selectedTrait.Description,viewport.X+3,top+88,viewport.Width-6,Ink*DetailReveal,.78f,draw:render)+24;
            if(unlocks!=null)bottom=DrawBossUnlocks(unlocks,viewport.X+3,bottom,viewport.Width-6,render);
            else
            {
                if(render)Text("激活条件",new(viewport.X+3,bottom),color,.82f);
                bottom=InfoLines(selectedTrait.Requirement,viewport.X+3,bottom+34,viewport.Width-6,InfoQuiet*DetailReveal,.75f,draw:render)+22;
            }
            content=bottom-top;
        }
        Detail(false);traitDetailScroll=InfoScroll(viewport,traitDetailScroll,content);
        InfoClip(viewport,()=>Detail(true));InfoScrollMark(panes.Detail,traitDetailScroll,content,color);
        return b.Bottom;
    }
}
