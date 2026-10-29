using Microsoft.Xna.Framework;
using Terraria;

namespace ZenithReborn;

// Boss 目标和累计奖励各自表达进度；两列没有一一绑定，保留任意顺序解锁的规则。
public partial class SoulUI
{
    private static double bossAnimationStarted;
    private static readonly Color BossLit=new(127,223,190),BossGold=new(225,192,123);
    private static int DrawBossUnlocks(SoulBossUnlockView view,int x,int y,int width,bool draw)
    {
        if(draw)
        {
            Text("击败 Boss · 唤醒剑魂",new(x,y),BossGold*DetailReveal,.84f);
            string progress=$"{view.Count} / {view.Total} 已铭刻";
            Text(progress,new(x+Math.Max(0,width-132),y+3),BossLit*DetailReveal,.63f);
        }
        y=InfoLines("组内任选顺序，每个目标贡献一道铭刻；右侧效果按累计数量解锁。",x,y+30,width,InfoQuiet*DetailReveal,.64f,draw:draw)+18;
        bool columns=width>=540;int left=columns?Math.Clamp(width/3,188,252):width;
        int rightX=columns?x+left+20:x,rightWidth=columns?width-left-20:width;
        if(draw)
        {
            Text("挑战目标",new(x,y),InfoQuiet,.64f);
            if(columns)Text("累计铭刻 → 解锁效果",new(rightX,y),InfoQuiet,.64f);
        }
        y+=29;
        if(columns)
        {
            for(int i=0;i<view.Total;i++)
            {
                int h=Math.Max(132,BossRewardHeight(view.Rewards[i],rightWidth));
                if(draw)
                {
                    DrawBossTarget(view.Targets[i],new(x,y,left,h),i);
                    DrawBossReward(view.Rewards[i],view.Curse,new(rightX,y,rightWidth,h));
                }
                y+=h+14;
            }
        }
        else
        {
            // 小屏先排列全部挑战对象，再列累计奖励，避免名字和属性被挤成窄条。
            foreach(var target in view.Targets)
            {if(draw)DrawBossTarget(target,new(x,y,width,94),0);y+=104;}
            if(draw)Text("累计铭刻 → 解锁效果",new(x,y+8),InfoQuiet,.64f);
            y+=40;
            foreach(var reward in view.Rewards)
            {
                int h=BossRewardHeight(reward,width);
                if(draw)DrawBossReward(reward,view.Curse,new(x,y,width,h));y+=h+14;
            }
        }
        return y+12;
    }
    private static int BossRewardHeight(SoulBossReward reward,int width)=>
        Math.Max(132,InfoLines(reward.Description,0,70,Math.Max(12,width-42),Color.White,.71f,draw:false)+20);
    private static void DrawBossTarget(SoulBossTarget target,Rectangle r,int ordinal)
    {
        if(!InfoVisible(r))return;
        float alpha=DetailReveal;bool done=target.Recorded;
        Color accent=done?BossLit:InfoQuiet;
        RoundedBox(r,(done?new Color(24,43,43):new Color(20,27,38))*alpha,9);
        if(done)Box(new(r.X,r.Y+15,2,r.Height-30),accent*.8f*alpha);
        Vector2 at=new(r.X+46,r.Center.Y-7);
        if(done)Glow(at,53,BossLit,.25f*alpha);
        Arc(at,38,-MathHelper.PiOver2,MathHelper.TwoPi,accent*(done?.35f:.12f)*alpha);
        double animation=UiTime-bossAnimationStarted;
        SoulBossPortraits.Draw(target.Seal,at,73,Color.White*(done?1:.37f)*alpha,animation);
        int textX=r.X+94,textWidth=r.Width-103;
        int textY=InfoLines(target.Name,textX,r.Center.Y-29,textWidth,(done?InfoWhite:InfoQuiet)*alpha,.69f);
        InfoLines(target.Status,textX,textY+6,textWidth,accent*(done?1:.75f)*alpha,.55f);
        if(target.TotalParts>1)
        {
            string eyes=target.Recorded?"两只均已完成":"激光眼与魔焰眼均需击败";
            if(r.Height>110)InfoLines(eyes,r.X+10,r.Bottom-27,r.Width-20,accent*.8f*alpha,.48f);
        }
    }
    private static void DrawBossReward(SoulBossReward reward,SoulCurse curse,Rectangle r)
    {
        if(!InfoVisible(r))return;
        float alpha=DetailReveal;bool lit=reward.Active;Color accent=reward.Stage==CurseStage.Blessed?BossGold:BossLit;
        RoundedBox(r,(lit?new Color(26,38,43):new Color(20,26,36))*alpha,9);
        if(lit)Line(new(r.X+15,r.Y),new(r.Right-15,r.Y),accent*.6f*alpha,1.5f);
        string stage=reward.Stage switch{CurseStage.Weakened=>"削弱",CurseStage.Released=>"解除",_=>"祝福"};
        string title=$"累计 {reward.Count} 位 · {stage}{SoulCurses.Names[(int)curse]}";
        Text(Fit(title,r.Width-38,.78f),new(r.X+18,r.Y+12),(lit?accent:InfoQuiet)*alpha,.78f);
        InfoBadge(reward.Status,new(r.X+19,r.Y+42),(lit?accent:InfoQuiet*.75f)*alpha);
        InfoLines(reward.Description,r.X+19,r.Y+70,r.Width-42,(lit?InfoWhite:new Color(119,132,151))*alpha,.71f);
        if(reward.Current&&lit)Glyph(SoulGlyph.Core,new(r.Right-22,r.Y+46),15,accent*.85f*alpha);
    }
}
