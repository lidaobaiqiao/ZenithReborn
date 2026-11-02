using Microsoft.Xna.Framework;
using Terraria;

namespace ZenithReborn;

// 选中剑位的状态、只读换入对比与固定操作区；长说明只在本区域内滚动。
public partial class SoulUI
{
    private static int armoryDetailHeight;
    private static void ArmoryMetric(string label,string value,Rectangle r,Color tint)
    {
        RoundedBox(r,new Color(20,33,46),8);Text(label,new(r.X+12,r.Y+8),InfoQuiet,.55f);
        Text(value,new(r.X+12,r.Y+29),tint,1.08f);
    }
    private static void DrawArmoryDetails(SoulItem soul,SoulArmoryLayout layout)
    {
        var panel=layout.Detail;var area=layout.DetailBody;Item current=soul.ItemAt(selected),candidate=CandidateItem;
        var preview=CurrentArmoryPreview();Color hue=candidate!=null?Champagne:ArmorySlotColor(selected);
        RoundedBox(panel,new Color(13,22,32),12);
        armoryDetailScroll=InfoScroll(area,armoryDetailScroll,armoryDetailHeight);
        int end=area.Y;
        InfoClip(area,()=>
        {
            int y=area.Y-armoryDetailScroll;int x=area.X,w=area.Width;
            InfoBadge($"{(candidate!=null?"换入预览":"当前剑位")}  /  {ArmorySlotCode(selected)}",new(x,y),hue);
            y+=30;
            Item shown=candidate??current;
            y=InfoLines(shown.IsAir?"等待一把剑":shown.Name,x,y,w,InfoWhite,1.06f,3)+9;
            string role=SoulItem.SupportOrdinal(selected)>=0?"共鸣支援 · 提供特性":selected==SoulItem.LegendarySlotId?"传奇主攻 · 独立出剑":selected is 15 or 16?"全收集奖励 · 独立出剑":"普通主攻 · 独立出剑";
            y=InfoLines(role,x,y,w,hue,.66f)+18;
            if(!shown.IsAir)
            {
                int half=(w-10)/2;
                ArmoryMetric("武器面板伤害",shown.damage.ToString(),new(x,y,half,65),InfoWhite);
                ArmoryMetric("武器面板暴击",shown.crit+"%",new(x+half+10,y,half,65),InfoWhite);y+=76;
                if(preview!=null)
                {
                    string delta=$"相对当前剑：伤害 {preview.DamageDelta:+0;-0;0}  /  暴击 {preview.CritDelta:+0;-0;0}%";
                    y=InfoLines(delta,x,y,w,Champagne,.61f)+8;
                }
                y=InfoLines("面板比较；实际伤害还受剑魂效能与战斗特性影响。",x,y,w,InfoQuiet,.56f)+14;
                // 族名放在环境之前：先回答"这是哪一族的剑"，再回答"它吃哪个环境"。
                string familyLine=WeaponFamilyTags.Line(shown.type,soul);
                if(familyLine.Length>0)y=InfoLines(familyLine,x,y,w,Champagne,.66f)+8;
                y=InfoLines(EnvironmentSwordTooltips.Label(shown),x,y,w,Accent,.66f)+18;
            }
            Line(new(x,y),new(x+w,y),hue*.18f);y+=16;
            if(preview!=null)
            {
                Glyph(preview.Allowed?SoulGlyph.Check:SoulGlyph.Lock,new(x+9,y+11),17,preview.Allowed?Accent:Champagne);
                Text(preview.Allowed?"可以归位":"暂时无法归位",new(x+28,y),preview.Allowed?Accent:Champagne,.78f);y+=32;
                string result=preview.Allowed?$"换入后主攻 {preview.MainCount}/{preview.MainCapacity} · 支援容量 {preview.SupportCapacity}\n"+
                    (current.IsAir?"候选武器将移入这个剑位。":"当前的“"+current.Name+"”将回到候选武器原来的背包格。"):
                    preview.Reason;
                y=InfoLines(result,x,y,w,InfoWhite,.68f)+16;
                if(InfoAction(new(x,y,w,30),"取消预览",InfoQuiet,glyph:SoulGlyph.Return))
                {ClearArmoryCandidate();armoryFocusAt=UiTime;}
                y+=46;
            }
            else
            {
                string state=!Available(soul,selected)?current.IsAir?"封印未解 · 查看下方条件":"已暂停 · 武器仍可取回":
                    !soul.Attached?"预览剑位 · 附着后启用":current.IsAir?"空位已开放":"已寄宿 · 当前有效";
                Text(state,new(x,y),Available(soul,selected)?Accent:Champagne,.72f);y+=34;
            }
            Text("剑位条件",new(x,y),InfoWhite,.74f);y+=27;
            y=InfoLines(SoulProgress.SlotHint(soul,selected),x,y,w,InfoQuiet,.66f)+21;
            Text("下一步成长",new(x,y),Champagne,.74f);y+=27;
            y=InfoLines(SoulProgress.Next(soul),x,y,w,InfoWhite,.68f)+12;
            if(InfoAction(new(x,y,w,32),"展开成长路线",Champagne,glyph:SoulGlyph.Memory))SelectPage(2);y+=44;
            if(soul.HasLegacyExpansion)
            {if(InfoAction(new(x,y,w,34),"取回旧版本遗留武器",Champagne,glyph:SoulGlyph.Return))SelectPage(RecoveryPage);y+=46;}
            end=y+armoryDetailScroll;
        });
        armoryDetailHeight=end-area.Y;InfoScrollMark(area,armoryDetailScroll,armoryDetailHeight,hue);
        Line(new(panel.X+16,layout.Confirm.Y-12),new(panel.Right-16,layout.Confirm.Y-12),hue*.18f);
        if(candidate!=null)
        {
            if(InfoAction(layout.Confirm,"寄宿到剑位 "+ArmorySlotCode(selected),Champagne,preview?.Allowed==true,SoulGlyph.Blade))
            {bool ok=ConfirmArmoryCandidate(out string error);DomainNote(ok,ok?"归位完成 · "+SlotName(selected):error);}
        }
        else if(InfoAction(layout.Confirm,current.IsAir?"从剑库选择武器":"选择替换武器",Accent,glyph:SoulGlyph.Blade))
        {if(layout.Compact)armoryPane=1;else {libraryScroll=0;feedback="点击左侧武器，查看换入预览。";feedbackUntil=Main.GameUpdateCount+180;}}
        if(InfoAction(layout.Return,"取回背包",InfoQuiet,!current.IsAir,SoulGlyph.Return))
        {bool ok=ReturnSelected(Main.LocalPlayer,out string error);armoryPreview=null;DomainNote(ok,ok?"武器已归还背包":error);}
    }
}
