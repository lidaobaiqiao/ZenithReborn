using Microsoft.Xna.Framework;

namespace ZenithReborn;

// 成长页保留六封印选择卡；展开部分复用特性页的模型与绘制，不重复维护效果数值。
public partial class SoulUI
{
    private static int sealFocus=-1;
    private static int DrawSealUnlockList(SoulItem soul,int x,int y,int w)
    {
        y=InfoHeading("六封印","只读取此剑魂的个人铭刻",x,y,w,SoulGlyph.Core,new(184,162,224));
        int sealColumns=SoulInfoLayout.Columns(w);
        for(int start=0;start<6;start+=sealColumns)
        {
            int opened=-1;
            for(int col=0;col<sealColumns&&start+col<6;col++)
            {
                int i=start+col;var curse=(SoulCurse)i;var stage=soul.Curse(curse);
                Color tint=stage==CurseStage.Blessed?Champagne:stage>=CurseStage.Released?new(130,208,171):new(209,142,164);
                var r=SoulInfoLayout.Tile(new(x,y,w,116),col,sealColumns,116);
                if(InfoVisible(r))
                {
                    InfoSurface(r,tint,InfoHover("seal/"+i,sealFocus==i||InfoHovered(r)));
                    Glyph(CurseGlyph(curse),new(r.X+28,r.Y+31),25,tint);
                    Text(SoulCurses.Names[i],new(r.X+53,r.Y+14),InfoWhite,.85f);
                    InfoBadge(SoulCurses.StageName(stage),new(r.X+54,r.Y+42),tint);
                    int total=SoulCurses.Groups[i].Length;
                    for(int boss=0;boss<total;boss++)
                    {
                        bool recorded=(soul.Seals&SoulCurses.Groups[i][boss])==SoulCurses.Groups[i][boss];
                        var at=new Vector2(r.X+24+boss*24,r.Y+88);
                        Arc(at,4,0,MathHelper.TwoPi,tint*(recorded?.9f:.2f));if(recorded)Box(new((int)at.X-2,(int)at.Y-2,4,4),tint);
                    }
                    Text($"{SoulCurses.Count(soul,curse)}/{total} 铭刻 · 详情",new(r.Right-112,r.Y+79),InfoQuiet,.52f);
                    if(InfoPressed(r)){sealFocus=sealFocus==i?-1:i;detailStarted=UiTime;}
                }
                if(sealFocus==i)opened=i;
            }
            y+=116+SoulInfoLayout.Gap;
            if(opened>=0)y=DrawBossUnlocks(SoulBossUnlocks.Read(soul,(SoulCurse)opened),x,y,w,true);
        }
        if((soul.Seals&(SoulCurses.Duke|SoulCurses.Deer))!=0)
            y=InfoBlock("外域留痕",$"猪龙鱼公爵 · {((soul.Seals&SoulCurses.Duke)!=0?"已铭刻":"未铭刻")}    鹿角怪 · {((soul.Seals&SoulCurses.Deer)!=0?"已铭刻":"未铭刻")}\n二者不施咒；当前只记录胜利，特殊祝福效果尚未设计。",x,y,w,InfoQuiet,SoulGlyph.Memory);
        return y;
    }
}
