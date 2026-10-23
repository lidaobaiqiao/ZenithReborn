using Microsoft.Xna.Framework;
using Terraria;
using E=ZenithReborn.SwordEnvironment;

namespace ZenithReborn;

public partial class SoulUI
{
    private static int DrawEnvironments(SoulItem soul,Rectangle b,int y)
    {
        soul.RegisterEnvironments();int x=b.X+14,w=b.Width-28;
        int count=EnvironmentRules.Count(soul.EnvironmentCollection),slots=EnvironmentRules.BlessingSlots(soul.EnvironmentCollection);
        y=Section($"环境之力 · {count}/21",$"共同强度 {EnvironmentRules.Shared(soul.EnvironmentCollection):0.##}倍 · 每种+20%\n"+
            $"万境之根 {(EnvironmentRules.Has(soul.EnvironmentCollection,E.Forest)?"+15% · 无虚弱环境":"未收集森林")}\n"+
            $"常驻机制 {EnvironmentRules.Count(soul.EnvironmentBlessings)}/{slots} · 收集4/8/12/16/20种各增加一个名额",x,y,w,new Color(125,225,155));
        y=Paragraph("有效寄宿登记武器对应环境，或在自然环境中用剑魂击败敌人收集当地力量；登记后取下武器不丢进度。属性在手持已附着剑魂时生效。",x+10,y,w-20,Muted,.64f)+10;
        string chosen=soul.DomainEnvironment==E.None?"自然环境":EnvironmentAttributes.Get(soul.DomainEnvironment).Name;
        y=Paragraph($"当前领域：{chosen} · 半径60格 · 主动环境对应效果+50%\n机制由当前环境启用，选为常驻后可跨环境使用。领域随玩家移动，无使用冷却或限时。",x+10,y,w-20,Accent,.64f)+8;
        bool Click(Rectangle r,string label,bool enabled=true)=>Button(r,label,enabled);
        if(Click(new(x+8,y,Math.Max(100,w-16),30),"关闭主动领域 · 恢复自然环境"))
        {soul.SetEnvironmentDomain(E.None);Sync(soul);}
        y+=44;
        var ep=Main.LocalPlayer.GetModPlayer<EnvironmentPlayer>();
        foreach(var d in EnvironmentAttributes.All)
        {
            bool collected=EnvironmentRules.Has(soul.EnvironmentCollection,d.Id),blessed=EnvironmentRules.Has(soul.EnvironmentBlessings,d.Id);
            bool domain=soul.DomainEnvironment==d.Id;
            float strength=collected?EnvironmentRules.Shared(soul.EnvironmentCollection)*EnvironmentRules.Forest(soul.EnvironmentCollection)*
                (domain?1.5f:1)*EnvironmentRules.Adaptation(d.Id,soul.DomainEnvironment==E.None?ep.NaturalMask:EnvironmentRules.Bit(soul.DomainEnvironment)):0;
            string state=!collected?"未收集":domain?"主动领域 · 强化50%":blessed?"祝福常驻":EnvironmentRules.Has(ep.NaturalMask,d.Id)&&soul.DomainEnvironment==E.None?"机制运行":"属性生效 · 机制休眠";
            var color=EnvironmentRules.Color(d.Id);
            Text($"{(int)d.Id:00}  {d.Name} · {state}",new(x+10,y),collected?color:Muted,.74f);y+=28;
            y=Paragraph(EnvironmentRules.StatsText(d.Id,collected?strength:1)+(collected?$" 〔{strength:0.##}倍〕":" 〔基础值〕"),x+10,y,w-20,collected?Ink:Muted,.62f)+6;
            y=Paragraph(EnvironmentRules.MechanismNames[(int)d.Id]+"："+EnvironmentRules.Descriptions[(int)d.Id],x+10,y,w-20,Muted,.64f)+9;
            int half=(w-24)/2;
            if(Click(new(x+8,y,half,30),domain?"当前主动环境":"展开对应环境",collected))
            {soul.SetEnvironmentDomain(d.Id);Sync(soul);}
            if(Click(new(x+16+half,y,half,30),blessed?"取消常驻":"选择常驻机制",collected))
            {
                if(soul.ToggleEnvironmentBlessing(d.Id))Sync(soul);
                else{feedback="常驻名额不足；收集更多环境，或取消一个已选机制。";feedbackUntil=Main.GameUpdateCount+180;}
            }
            y+=46;Line(new(x+8,y),new(x+w-8,y),color*.18f);y+=18;
        }
        return y+12;
    }
}
