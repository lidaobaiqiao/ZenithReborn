using Microsoft.Xna.Framework;
using Terraria;

namespace ZenithReborn;

// 成长操作按剑位、魂芯、封印分组；下一步目标固定展示，当前分组单独滚动。
public partial class SoulUI
{
    private static int growthFocus=-1,memoryTab,growthScroll,growthContentHeight;
    private static int DrawProgress(SoulItem soul,Rectangle b,int unused)
    {
        int x=b.X+14,w=b.Width-28,unlocked=soul.Attached?soul.Capacity:0;
        int y=InfoTitle(b,"成长记忆",$"普通剑位 {unlocked}/10 · 魂芯 {System.Numerics.BitOperations.PopCount((uint)(soul.Cores&63))}/6",SoulGlyph.Memory,Champagne);
        y=InfoLines("下一步 · "+SoulProgress.Next(soul),x+6,y,w-12,Champagne,.78f,3)+16;
        string[] groups=["剑位路线","魂芯收藏","封印铭刻"];
        int step=Math.Min(196,w/3);
        for(int i=0;i<3;i++)
        {
            var r=new Rectangle(x+i*step,y,step-10,34);
            if(InfoAction(r,groups[i],memoryTab==i?Champagne:InfoQuiet,true,i==0?SoulGlyph.Blade:i==1?SoulGlyph.Core:SoulGlyph.Lock))
            {memoryTab=i;growthScroll=0;growthContentHeight=0;}
            if(memoryTab==i)Box(new(r.X+12,r.Bottom-1,Math.Max(1,r.Width-24),2),Champagne*.8f);
        }
        y+=48;var viewport=new Rectangle(x,y,w,Math.Max(1,b.Bottom-y-12));
        growthScroll=InfoScroll(viewport,growthScroll,growthContentHeight);
        int top=viewport.Y-growthScroll,bottom=top;
        InfoClip(viewport,()=>bottom=memoryTab==0?DrawSwordMemories(soul,x,top,w):memoryTab==1?DrawCoreMemories(soul,x,top,w):DrawSealUnlockList(soul,x,top,w));
        growthContentHeight=bottom-top;growthScroll=Math.Clamp(growthScroll,0,Math.Max(0,growthContentHeight-viewport.Height));
        InfoScrollMark(viewport,growthScroll,growthContentHeight,Champagne);return b.Bottom;
    }
    private static int DrawSwordMemories(SoulItem soul,int x,int y,int w)
    {
        int unlocked=soul.Attached?soul.Capacity:0;
        var king=AttachmentStory.FindKing(Main.LocalPlayer,soul.SoulId);
        if(king!=null)
        {
            y=InfoBlock("史莱姆王的回应",king.ai[2]>0?"祝福「附着」已经归位。让这把剑，陪你重新站起来。":AttachmentStory.Lines[Math.Clamp((int)king.ai[1],0,3)],x,y,w,Champagne,SoulGlyph.Core);
            if(king.ai[2]==0)
            {
                if(InfoAction(new(x,y,w,38),king.ai[1]<3?"继续聆听":"接受祝福 · 附着",Champagne))AttachmentStory.RequestAdvance(king);
                y+=54;
            }
        }
        else if(!soul.KingMemory)
        {
            y=InfoBlock("最初的记忆 · 友善史莱姆王",AttachmentStory.Requirement(Main.LocalPlayer,soul)+"\n白天地表也可使用凝胶信物呼唤，无需附近史莱姆。",x,y,w,Champagne,SoulGlyph.Core);
            bool ready=AttachmentStory.CanStart(Main.LocalPlayer,soul);
            if(InfoAction(new(x,y,w,38),ready?"回应呼唤 · 与史莱姆王相遇":"附近普通史莱姆 · 白天 · 地表",Champagne,ready))AttachmentStory.RequestStart(soul);
            y+=54;
        }

        y=InfoHeading("十剑解封","选择节点查看完整条件",x,y,w,SoulGlyph.Blade,Accent);
        int columns=Math.Clamp(w/86,2,10),rows=(10+columns-1)/columns;
        int focused=growthFocus<0?Math.Min(9,unlocked):Math.Clamp(growthFocus,0,9);
        for(int i=0;i<10;i++)
        {
            var r=SoulInfoLayout.Tile(new(x,y,w,96),i,columns,96);bool done=i<unlocked,next=i==unlocked;
            Color tint=done?new(130,208,171):next?Champagne:InfoQuiet;
            if(!InfoVisible(r))continue;
            float hover=InfoHover("gate/"+i,focused==i||InfoHovered(r));InfoSurface(r,tint,hover);
            Vector2 center=new(r.Center.X,r.Y+35);
            if(i%columns>0)Line(new(r.X-SoulInfoLayout.Gap,center.Y),new(r.X,center.Y),tint*.25f);
            InfoItem(SoulProgress.ZenithWeapons[i],center,30,done?1:next?.8f:.32f);
            CenterText((i+1).ToString("00"),new(r.Center.X,r.Y+57),tint,.64f);
            CenterText(done?"已解封":next?"下一剑位":"待解封",new(r.Center.X,r.Y+78),tint,.45f);
            if(focused==i)Box(new(r.X+12,r.Bottom-1,Math.Max(1,r.Width-24),2),tint);
            if(InfoPressed(r))growthFocus=focused=i;
        }
        y+=rows*(96+SoulInfoLayout.Gap);
        string condition=focused==0?"与友善史莱姆王对话，接受祝福「附着」。":SoulProgress.SlotGates[focused-1].Requirement;
        y=InfoBlock($"第 {focused+1:00} 剑位 · {(focused<unlocked?"已解封":focused==unlocked?"下一目标":"等待前序解封")}",condition,x,y,w,focused<unlocked?new(130,208,171):Champagne,SoulGlyph.Blade);
        if(!soul.KingMemory)y=InfoBlock("凝胶信物","新角色自带信物。旧角色可用凝胶10＋坠落之星1，在工作台制作。",x,y,w,InfoQuiet,SoulGlyph.Core);
        return y;
    }
    private static int DrawCoreMemories(SoulItem soul,int x,int y,int w)
    {
        int count=System.Numerics.BitOperations.PopCount((uint)(soul.Cores&63));
        y=InfoHeading("魂芯收藏",$"{count} / 6 · 铭刻之后的证明",x,y,w,SoulGlyph.Core,Champagne);
        int cols=Math.Clamp(w/110,2,6);
        for(int i=0;i<6;i++)
        {
            var r=SoulInfoLayout.Tile(new(x,y,w,88),i,cols,88);if(!InfoVisible(r))continue;
            bool owned=(soul.Cores&(1<<i))!=0;InfoSurface(r,Champagne);
            InfoItem(SoulCore.TypeFor(i),new(r.Center.X,r.Y+27),28,owned?1:.24f);
            CenterText(SoulProgress.CoreNames[i],new(r.Center.X,r.Y+50),owned?Champagne:InfoQuiet,.63f);
            CenterText(owned?"已融入":"未归位",new(r.Center.X,r.Y+71),InfoQuiet,.43f);
        }
        y+=((6+cols-1)/cols)*(88+SoulInfoLayout.Gap);
        int core=SoulProgress.RecoverableCore(soul);
        if(core>=0)
        {
            bool has=Main.LocalPlayer.inventory.Any(i=>i.type==SoulCore.TypeFor(core)&&i.stack>0);
            if(InfoAction(new(x,y,w,38),(has?"融入":"凭胜利补办并融入")+SoulProgress.CoreNames[core]+"魂芯",Champagne))SoulProgress.RequestCoreRecovery(soul);
            y+=52;
        }
        else y=InfoLines("遗失的魂芯可凭对应胜利补办；不会跳过主线条件。",x+6,y,w-12,InfoQuiet,.61f)+22;
        return y;
    }
}
