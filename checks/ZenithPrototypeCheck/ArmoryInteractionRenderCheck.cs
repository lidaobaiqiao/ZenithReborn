using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 通过生产界面绘制接收模拟点击；所有交换只发生在临时人物与剑魂上。
internal static class ArmoryInteractionRenderCheck
{
    internal delegate void Frame(string name,int width,int height,Point? click);
    private const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    private static int Field(string name)=>(int)typeof(SoulUI).GetField(name,Hidden).GetValue(null);
    private static void Field(string name,int value)=>typeof(SoulUI).GetField(name,Hidden).SetValue(null,value);

    internal static object Run(SoulItem original,Player player,Frame render,out bool passed)
    {
        Item[] inventory=player.inventory;float scale=Main.UIScale;
        player.inventory=inventory.Select(i=>i.Clone()).ToArray();
        var soul=(SoulItem)player.inventory[0].ModItem;
        try
        {
            Main.UIScale=1;SoulUI.Open(soul);SoulUI.SelectPage(0);SoulUI.SelectSlot(2);
            Item candidate=player.inventory[2],outgoing=soul.Hosts[2];
            byte[] Snapshot(){using var m=new MemoryStream();soul.NetSend(new BinaryWriter(m));return m.ToArray();}
            byte[] before=Snapshot();
            render("01-initial",1440,1100,null);
            render("02-candidate",1440,1100,SoulUI.LibraryCardBounds(1).Center);
            bool candidateSelected=SoulUI.CandidateInventorySlot==2&&before.SequenceEqual(Snapshot());
            render("03-same-slot",1440,1100,SoulUI.SlotBounds(2).Center);
            bool sameSlotShowsCurrent=SoulUI.SelectedSlot==2&&SoulUI.CandidateInventorySlot==-1&&before.SequenceEqual(Snapshot());
            render("04-candidate-again",1440,1100,SoulUI.LibraryCardBounds(1).Center);
            render("05-other-slot",1440,1100,SoulUI.SlotBounds(0).Center);
            bool otherSlotShowsCurrent=SoulUI.SelectedSlot==0&&SoulUI.CandidateInventorySlot==-1&&before.SequenceEqual(Snapshot());

            // 测量真实详情内容的末尾，再调整窗口高度，让被裁剪的成长按钮恰好处在固定操作按钮后面。
            int OverlapHeight(bool confirm)
            {
                render(confirm?"06-measure-confirm":"09-measure-return",1440,1100,null);
                var layout=SoulUI.ArmoryLayout;
                int growthCenter=layout.DetailBody.Y+Field("armoryDetailHeight")-44+16;
                int fixedCenter=confirm?layout.Confirm.Center.Y:layout.Return.Center.Y;
                return growthCenter+Main.screenHeight-fixedCenter;
            }
            bool OverlapsFixedButton(bool confirm)
            {
                var layout=SoulUI.ArmoryLayout;
                int growthTop=layout.DetailBody.Y+Field("armoryDetailHeight")-44-Field("armoryDetailScroll");
                var growth=new Rectangle(layout.DetailBody.X,growthTop,layout.DetailBody.Width,32);
                var fixedButton=confirm?layout.Confirm:layout.Return;
                return growth.Contains(fixedButton.Center)&&!layout.DetailBody.Contains(fixedButton.Center);
            }

            SoulUI.SelectSlot(2);SoulUI.SelectArmoryCandidate(2);
            int confirmHeight=OverlapHeight(true);
            render("07-confirm-overlap",1440,confirmHeight,null);
            bool confirmOverlap=OverlapsFixedButton(true);
            render("08-confirm-click",1440,confirmHeight,SoulUI.ArmoryLayout.Confirm.Center);
            bool confirmWorks=Field("page")==0&&ReferenceEquals(soul.Hosts[2],candidate)&&ReferenceEquals(player.inventory[2],outgoing);

            soul.Hosts[2]=outgoing;player.inventory[2]=candidate;SoulUI.SelectPage(0);SoulUI.SelectSlot(2);SoulUI.SelectArmoryCandidate(2);
            render("08a-scroll-measure",1440,1100,null);
            int scrollGrowthCenter=SoulUI.ArmoryLayout.DetailBody.Y+Field("armoryDetailHeight")-44+16;
            int confirmFromBottom=Main.screenHeight-SoulUI.ArmoryLayout.Confirm.Center.Y;
            int scrollOffset=scrollGrowthCenter-(900-confirmFromBottom);
            Field("armoryDetailScroll",scrollOffset);
            render("08b-scrolled-overlap",1440,900,null);
            bool scrolledOverlap=scrollOffset>0&&Field("armoryDetailScroll")==scrollOffset&&OverlapsFixedButton(true);
            render("08c-scrolled-confirm",1440,900,SoulUI.ArmoryLayout.Confirm.Center);
            bool scrolledConfirmWorks=Field("page")==0&&ReferenceEquals(soul.Hosts[2],candidate)&&ReferenceEquals(player.inventory[2],outgoing);

            soul.Hosts[2]=outgoing;player.inventory[2]=candidate;SoulUI.SelectPage(0);SoulUI.SelectSlot(2);SoulUI.SelectArmoryCandidate(2);
            int returnHeight=OverlapHeight(false);
            render("10-return-overlap",1440,returnHeight,null);
            bool returnOverlap=OverlapsFixedButton(false);
            render("11-return-click",1440,returnHeight,SoulUI.ArmoryLayout.Return.Center);
            bool returnWorks=Field("page")==0&&soul.Hosts[2].IsAir&&player.inventory.Any(i=>ReferenceEquals(i,outgoing))&&ReferenceEquals(player.inventory[2],candidate);

            for(int i=1;i<50;i++)if(ReferenceEquals(player.inventory[i],outgoing))player.inventory[i]=new Item();
            soul.Hosts[2]=outgoing;player.inventory[2]=candidate;SoulUI.SelectPage(0);SoulUI.SelectSlot(9);SoulUI.SelectArmoryCandidate(2);
            bool locked=!SoulArmoryPreview.Evaluate(soul,9,candidate).Allowed;
            int lockedHeight=OverlapHeight(true);
            render("12-locked-overlap",1440,lockedHeight,null);
            bool lockedOverlap=OverlapsFixedButton(true);byte[] lockedBefore=Snapshot();
            render("13-locked-click",1440,lockedHeight,SoulUI.ArmoryLayout.Confirm.Center);
            bool disabledNoClickThrough=locked&&Field("page")==0&&lockedBefore.SequenceEqual(Snapshot())&&SoulUI.CandidateInventorySlot==2;

            SoulUI.SelectPage(0);SoulUI.SelectSlot(2);SoulUI.SelectArmoryCandidate(2);
            render("14-visible-growth",1440,1100,null);
            var visible=SoulUI.ArmoryLayout.DetailBody;
            int visibleY=visible.Y+Field("armoryDetailHeight")-44-Field("armoryDetailScroll")+16;
            var visibleGrowth=new Point(visible.Center.X,visibleY);
            bool growthInBody=visible.Contains(visibleGrowth);
            render("15-growth-click",1440,1100,visibleGrowth);
            bool visibleGrowthWorks=growthInBody&&Field("page")==2;

            SoulUI.SelectPage(0);SoulUI.SelectSlot(0);SoulUI.SelectArmoryPane(1);
            render("16-compact-library",640,720,null);
            render("17-compact-candidate",640,720,SoulUI.LibraryCardBounds(1).Center);
            bool compactPreview=SoulUI.ArmoryPane==2&&SoulUI.CandidateInventorySlot==2;
            render("18-compact-stage",640,720,SoulUI.ArmoryLayout.Mode(0).Center);
            render("19-compact-slot",640,720,SoulUI.SlotBounds(0).Center);
            bool compactSlotShowsCurrent=SoulUI.SelectedSlot==0&&SoulUI.CandidateInventorySlot==-1;
            render("20-compact-detail",640,720,SoulUI.ArmoryLayout.Mode(2).Center);

            Main.UIScale=1.25f;SoulUI.SelectPage(0);SoulUI.SelectSlot(2);
            render("21-scaled",1800,1125,null);
            render("22-scaled-candidate",1800,1125,SoulUI.LibraryCardBounds(1).Center);
            render("23-scaled-slot",1800,1125,SoulUI.SlotBounds(2).Center);
            bool scaledSlotShowsCurrent=SoulUI.SelectedSlot==2&&SoulUI.CandidateInventorySlot==-1;
            SoulUI.SelectArmoryCandidate(2);render("23a-scaled-measure",1800,1375,null);
            int scaledGrowthCenter=SoulUI.ArmoryLayout.DetailBody.Y+Field("armoryDetailHeight")-44+16;
            int scaledHeight=scaledGrowthCenter+Main.screenHeight-SoulUI.ArmoryLayout.Confirm.Center.Y;
            int scaledPixels=(int)Math.Ceiling(scaledHeight*Main.UIScale);
            render("23b-scaled-overlap",1800,scaledPixels,null);
            bool scaledOverlap=OverlapsFixedButton(true);
            render("23c-scaled-confirm",1800,scaledPixels,SoulUI.ArmoryLayout.Confirm.Center);
            bool scaledConfirmWorks=Field("page")==0&&ReferenceEquals(soul.Hosts[2],candidate)&&ReferenceEquals(player.inventory[2],outgoing);

            passed=candidateSelected&&sameSlotShowsCurrent&&otherSlotShowsCurrent&&confirmOverlap&&confirmWorks&&scrolledOverlap&&scrolledConfirmWorks&&
                returnOverlap&&returnWorks&&lockedOverlap&&disabledNoClickThrough&&visibleGrowthWorks&&compactPreview&&compactSlotShowsCurrent&&scaledSlotShowsCurrent;
            passed=passed&&scaledOverlap&&scaledConfirmWorks;
            return new{passed,candidateSelected,sameSlotShowsCurrent,otherSlotShowsCurrent,confirmOverlap,confirmWorks,
                scrolledOverlap,scrolledConfirmWorks,returnOverlap,returnWorks,lockedOverlap,disabledNoClickThrough,visibleGrowthWorks,
                compactPreview,compactSlotShowsCurrent,scaledSlotShowsCurrent,scaledOverlap,scaledConfirmWorks,
                confirmHeight,returnHeight,lockedHeight,scrollOffset,scaledHeight};
        }
        finally{Main.UIScale=scale;player.inventory=inventory;SoulUI.Open(original);Main.mouseLeft=Main.mouseLeftRelease=false;}
    }
}
