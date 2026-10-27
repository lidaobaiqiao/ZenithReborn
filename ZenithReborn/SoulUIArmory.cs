using Microsoft.Xna.Framework;
using Terraria;

namespace ZenithReborn;

// 配阵主页只负责协调剑库、剑阵和详情。其他三个信息工作区沿用原入口。
public partial class SoulUI
{
    private static int DrawArmory(SoulItem soul,Rectangle body)
    {
        var layout=ArmoryLayout;EnsureEffects();
        if(layout.Compact)
        {
            string[] names=["剑阵","随身剑库","剑位详情"];
            for(int i=0;i<3;i++)
            {
                var r=layout.Mode(i);if(i==armoryPane)RoundedBox(r,new Color(33,52,62),7);
                CenterText(names[i],new(r.Center.X,r.Y+6),i==armoryPane?Accent:InfoQuiet,.69f);
                if(InfoPressed(r))armoryPane=i;
            }
        }
        // 先绘制列表，让当前帧的候选选择立刻反映到剑阵和详情里。
        if(!layout.Compact||armoryPane==1)DrawArmoryLibrary(soul,layout);
        if(!layout.Compact||armoryPane==0)DrawArmoryScene(soul,layout);
        if(!layout.Compact||armoryPane==2)DrawArmoryDetails(soul,layout);
        if(!layout.Compact||armoryPane==0)DrawArmoryAttunement(layout);
        if(!layout.Compact||armoryPane==0)DrawCoreFlow(soul,layout);
        return body.Bottom;
    }
    private static void DrawArmoryHeader(Point mouse)
    {
        bool small=Main.screenWidth<700;
        Glyph(SoulGlyph.Blade,new(37,35),23,Accent);
        Text(small?"天顶剑魂":"天 顶 剑 魂  /  众剑归位",new(58,21),InfoWhite,small?.87f:1.02f);
        var empty=new Rectangle(Math.Max(24,Main.screenWidth-(small?236:380)),20,small?80:118,30);
        RoundedBox(empty,new Color(22,36,46),7);
        CenterText(small?"找空位":"定位空剑位",new(empty.Center.X,empty.Y+6),Accent,.62f);
        if(empty.Contains(mouse)&&Main.mouseLeft&&Main.mouseLeftRelease)
        {Main.mouseLeftRelease=false;bool found=SelectNextEmptyArmorySlot();DomainNote(false,found?"已定位开放空位 · "+SlotName(selected):"当前已开放的剑位都已寄宿。");}
        DrawDomainDifficulty(mouse,small);
        DrawNavigation(false);
    }
}
