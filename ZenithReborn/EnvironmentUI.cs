using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using E=ZenithReborn.SwordEnvironment;

namespace ZenithReborn;

// 环境选择和实际操作并排展示；选择图标只改变查看对象，不直接切换领域。
public partial class SoulUI
{
    private static int environmentGridScroll,environmentDetailScroll;
    private static bool environmentHelp;
    private static readonly int[] EnvironmentIcons=[0,ItemID.Acorn,ItemID.StoneBlock,ItemID.Amethyst,ItemID.Hellstone,
        ItemID.FallenStar,ItemID.SnowBlock,ItemID.IceBlock,ItemID.SandBlock,ItemID.Amber,ItemID.Seashell,
        ItemID.JungleSpores,ItemID.JungleGrassSeeds,ItemID.LihzahrdPowerCell,ItemID.GlowingMushroom,
        ItemID.EbonstoneBlock,ItemID.DemoniteOre,ItemID.CrimstoneBlock,ItemID.CrimtaneOre,ItemID.CrystalShard,ItemID.PearlstoneBlock,ItemID.Bone];
    private static int DrawEnvironments(SoulItem soul,Rectangle b,int unused)
    {
        soul.RegisterEnvironments();int count=EnvironmentRules.Count(soul.EnvironmentCollection),slots=EnvironmentRules.BlessingSlots(soul.EnvironmentCollection);
        Color accent=new(135,211,168);
        int y=InfoTitle(b,"环境之力",$"已收集 {count}/21 · 共同强度 {EnvironmentRules.Shared(soul.EnvironmentCollection):0.##}倍 · 常驻 {EnvironmentRules.Count(soul.EnvironmentBlessings)}/{slots}",SoulGlyph.World,accent);
        var panes=SoulInfoLayout.MasterDetail(new(b.X+14,y,b.Width-28,Math.Max(1,b.Bottom-y-12)));
        int columns=Math.Clamp((panes.List.Width-16)/112,1,4),rows=(21+columns-1)/columns;
        int atlasHeight=rows*108+12;environmentGridScroll=InfoScroll(panes.List,environmentGridScroll,atlasHeight);
        InfoSurface(panes.List,accent);
        InfoClip(panes.List,()=>
        {
            foreach(var d in EnvironmentAttributes.All)
            {
                int id=(int)d.Id;var r=SoulInfoLayout.Tile(new(panes.List.X+8,panes.List.Y+6-environmentGridScroll,panes.List.Width-16,96),id-1,columns,94);
                if(!r.Intersects(panes.List))continue;
                bool owned=EnvironmentRules.Has(soul.EnvironmentCollection,d.Id),resident=EnvironmentRules.Has(soul.EnvironmentBlessings,d.Id),domain=soul.DomainEnvironment==d.Id;
                bool focused=environmentFocus==id;Color tint=EnvironmentRules.Color(d.Id);
                InfoSurface(r,owned?tint:InfoQuiet,InfoHover("env/"+id,focused||InfoHovered(r)));
                InfoItem(EnvironmentIcons[id],new(r.Center.X,r.Y+25),27,owned?1:.28f);
                string name=d.Name.Replace("生物群系","").Replace("之地","");
                CenterText(Fit(name,r.Width-10,.63f),new(r.Center.X,r.Y+47),owned?InfoWhite:InfoQuiet,.63f);
                CenterText(domain?"领域展开":resident?"常驻机制":owned?"已收集":"待发现",new(r.Center.X,r.Y+73),owned?tint:InfoQuiet,.48f);
                if(focused)Box(new(r.X+12,r.Bottom-1,Math.Max(1,r.Width-24),2),tint);
                if(panes.List.Contains(Main.mouseX,Main.mouseY)&&InfoPressed(r))
                {environmentFocus=id;environmentDetailScroll=0;detailStarted=UiTime;}
            }
        });
        InfoScrollMark(panes.List,environmentGridScroll,atlasHeight,accent);
        var chosen=EnvironmentAttributes.Get((E)Math.Clamp(environmentFocus,1,21));
        bool collected=EnvironmentRules.Has(soul.EnvironmentCollection,chosen.Id),blessed=EnvironmentRules.Has(soul.EnvironmentBlessings,chosen.Id),active=soul.DomainEnvironment==chosen.Id;
        var ep=Main.LocalPlayer.GetModPlayer<EnvironmentPlayer>();Color color=EnvironmentRules.Color(chosen.Id);
        float strength=collected?EnvironmentRules.Shared(soul.EnvironmentCollection)*EnvironmentRules.Forest(soul.EnvironmentCollection)*
            (active?1.5f:1)*EnvironmentRules.Adaptation(chosen.Id,soul.DomainEnvironment==E.None?ep.NaturalMask:EnvironmentRules.Bit(soul.DomainEnvironment)):0;
        string state=!collected?"尚未收集 · 基础值预览":active?"主动领域 · 对应效果强化50%":blessed?"祝福常驻":
            EnvironmentRules.Has(ep.NaturalMask,chosen.Id)&&soul.DomainEnvironment==E.None?"自然机制运行中":"属性生效 · 机制休眠";
        InfoSurface(panes.Detail,color);
        bool paired=panes.Detail.Width>=420;int actionRows=paired?2:4;
        int actionHeight=Math.Min(34,Math.Max(22,(panes.Detail.Height-36)/Math.Max(3,actionRows+1)));
        int reserve=actionRows*(actionHeight+9)+14;
        var viewport=new Rectangle(panes.Detail.X+16,panes.Detail.Y+12,panes.Detail.Width-32,Math.Max(1,panes.Detail.Height-reserve-26));
        int contentHeight=0;
        void Detail(bool render)
        {
            int top=viewport.Y-environmentDetailScroll;
            if(render)
            {
                InfoItem(EnvironmentIcons[(int)chosen.Id],new(viewport.X+20,top+18),30);
                Text(chosen.Name,new(viewport.X+45,top+2),InfoWhite*DetailReveal,1.1f);
                InfoBadge(state,new(viewport.X+3,top+47),color);
            }
            int bottom=InfoLines(EnvironmentRules.StatsText(chosen.Id,collected?strength:1)+(collected?$" 〔{strength:0.##}倍〕":""),viewport.X+3,top+83,viewport.Width-6,Ink*DetailReveal,.8f,draw:render)+26;
            if(render)Text(EnvironmentRules.MechanismNames[(int)chosen.Id],new(viewport.X+3,bottom),color,.9f);
            bottom=InfoLines(EnvironmentRules.Descriptions[(int)chosen.Id],viewport.X+3,bottom+35,viewport.Width-6,InfoQuiet*DetailReveal,.78f,draw:render)+22;
            if(environmentHelp)bottom=InfoLines(EnvironmentGuide(soul),viewport.X+3,bottom+14,viewport.Width-6,InfoQuiet,.74f,draw:render)+20;
            contentHeight=bottom-top;
        }
        Detail(false);environmentDetailScroll=InfoScroll(viewport,environmentDetailScroll,contentHeight);
        InfoClip(viewport,()=>Detail(true));InfoScrollMark(viewport,environmentDetailScroll,contentHeight,color);
        int bx=panes.Detail.X+14,by=panes.Detail.Bottom-reserve+4,bw=panes.Detail.Width-28,half=(bw-10)/2;
        Rectangle ActionAt(int index)=>paired?new(bx+(index%2)*(half+10),by+(index/2)*(actionHeight+9),half,actionHeight):
            new(bx,by+index*(actionHeight+9),bw,actionHeight);
        if(InfoAction(ActionAt(0),active?"当前主动领域":"展开此环境领域",color,collected))
        {soul.SetEnvironmentDomain(chosen.Id);Sync(soul);}
        if(InfoAction(ActionAt(1),blessed?"取消常驻机制":"选择为常驻机制",color,collected,SoulGlyph.Spark))
        {
            if(soul.ToggleEnvironmentBlessing(chosen.Id))Sync(soul);
            else{feedback="常驻名额不足；收集更多环境，或取消一个已选机制。";feedbackUntil=Main.GameUpdateCount+180;}
        }
        if(InfoAction(ActionAt(2),"恢复自然环境",InfoQuiet,soul.DomainEnvironment!=E.None,SoulGlyph.Return))
        {soul.SetEnvironmentDomain(E.None);Sync(soul);}
        if(InfoAction(ActionAt(3),environmentHelp?"收起规则说明":"收集与常驻规则",InfoQuiet,true,SoulGlyph.Book))environmentHelp=!environmentHelp;
        return b.Bottom;
    }
    private static string EnvironmentGuide(SoulItem soul)=>
        "有效寄宿登记武器环境，或用剑魂在自然环境中击败敌人，收集当地力量；取下武器不会丢失收藏。\n手持已附着剑魂时生效。收集4/8/12/16/20种环境，各增加一个常驻名额。\n"+
        (EnvironmentRules.Has(soul.EnvironmentCollection,E.Forest)?"森林已收集：全环境+15%，没有虚弱环境。":"收集森林后，全环境+15%，没有虚弱环境。")+
        "\n领域半径60格，随玩家移动，没有使用冷却或限时。";
}
