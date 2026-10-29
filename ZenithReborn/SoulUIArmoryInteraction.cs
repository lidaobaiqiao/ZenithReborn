using Microsoft.Xna.Framework;
using Terraria;

namespace ZenithReborn;

// 配阵的临时选择与操作反馈。物品转移仍经既有背包交换接口，界面没有自己的物品仓库。
public partial class SoulUI
{
    private static int armoryPane,armoryScroll,armoryDetailScroll,candidateIndex=-1,previewSlot=-1;
    private static Item candidateReference;
    private static SoulArmoryPreview armoryPreview;
    private static double previewUpdated,armoryFocusAt,attunedAt=-10;
    private static int attunedSlot;
    private static Item attunedItem;
    private static Vector2 attunedFrom;
    // 剑位流入中央羁绊核心的一道流光；At为开始时间，支持延迟触发以等待剑到达剑位。
    private readonly record struct CoreFlowEvent(double At, int Slot);
    private static readonly List<CoreFlowEvent> coreFlows = new();
    private static void TriggerCoreFlow(int slot,double at) => coreFlows.Add(new CoreFlowEvent(at,slot));
    private static bool armorySortDamage;
    public static SoulArmoryLayout ArmoryLayout=>SoulArmoryLayout.Create(Main.screenWidth,Main.screenHeight);
    public static int ArmoryPane=>armoryPane;
    public static int CandidateInventorySlot=>CandidateItem==null?-1:candidateIndex;
    private static Item CandidateItem=>candidateIndex is >=0 and <50&&candidateReference!=null&&
        ReferenceEquals(Main.LocalPlayer.inventory[candidateIndex],candidateReference)&&SoulItem.CanHost(candidateReference)?candidateReference:null;
    public static void SelectArmoryPane(int value)=>armoryPane=Math.Clamp(value,0,2);
    private static void ClearArmoryCandidate()
    {candidateIndex=-1;candidateReference=null;armoryPreview=null;previewSlot=-1;}
    private static void ResetArmory()
    {
        armoryPane=armoryScroll=armoryDetailScroll=0;ClearArmoryCandidate();
        armoryFocusAt=UiTime;attunedAt=-10;attunedItem=null;coreFlows.Clear();libraryFilter=0;armorySortDamage=false;
    }
    public static bool SelectArmoryCandidate(int inventorySlot)
    {
        if(editing==null||inventorySlot is <0 or >=50||!SoulItem.CanHost(Main.LocalPlayer.inventory[inventorySlot]))return false;
        candidateIndex=inventorySlot;candidateReference=Main.LocalPlayer.inventory[inventorySlot];
        armoryPreview=null;armoryDetailScroll=0;armoryFocusAt=UiTime;
        if(ArmoryLayout.Compact)armoryPane=2;
        return true;
    }
    private static SoulArmoryPreview CurrentArmoryPreview()
    {
        Item item=CandidateItem;if(item==null){armoryPreview=null;return null;}
        if(armoryPreview==null||previewSlot!=selected||UiTime-previewUpdated>.3)
        {armoryPreview=SoulArmoryPreview.Evaluate(editing,selected,item);previewSlot=selected;previewUpdated=UiTime;}
        return armoryPreview;
    }
    public static bool ConfirmArmoryCandidate(out string error)
    {
        if(CandidateItem==null){error="候选武器已离开原背包位置，请重新选择。";return false;}
        Item incoming=CandidateItem;Vector2 from=ArmoryLayout.Library.Center.ToVector2();
        if(!EquipFromInventory(Main.LocalPlayer,candidateIndex,out error))return false;
        attunedItem=incoming;attunedSlot=selected;attunedFrom=from;attunedAt=UiTime;
        // 剑库确认的剑在0.55秒后到达剑位，随后流光从剑位流入中央。
        TriggerCoreFlow(selected,attunedAt+.55);
        ClearArmoryCandidate();armoryFocusAt=UiTime;
        if(ArmoryLayout.Compact)armoryPane=0;
        return true;
    }
    public static bool SelectNextEmptyArmorySlot()
    {
        if(editing==null)return false;
        int[] slots=editing.VisibleSlotIds.ToArray();int start=Array.IndexOf(slots,selected);
        for(int n=1;n<=slots.Length;n++)
        {
            int id=slots[(Math.Max(0,start)+n)%slots.Length];
            if(Available(editing,id)&&editing.ItemAt(id).IsAir)
            {SelectSlot(id);armoryPane=0;RevealArmorySlot(id);return true;}
        }
        return false;
    }
    private static void RevealArmorySlot(int id)
    {
        var layout=ArmoryLayout;var r=layout.Slot(id,editing.VisibleSupportCount,armoryScroll);
        if(r.Bottom>layout.Stage.Bottom-6)armoryScroll+=r.Bottom-layout.Stage.Bottom+6;
        if(r.Top<layout.Stage.Top+6)armoryScroll=Math.Max(0,armoryScroll-layout.Stage.Top+r.Top-6);
    }
    private static string ArmorySlotCode(int id)=>id==SoulItem.LegendarySlotId?"L":id<10?$"{id+1:00}":
        id is 15 or 16?$"R{id-14}":$"S{SoulItem.SupportOrdinal(id)+1}";
    private static Color ArmorySlotColor(int id)=>id<10?Accent:SoulItem.SupportOrdinal(id)>=0?new(184,163,233):Champagne;
}
