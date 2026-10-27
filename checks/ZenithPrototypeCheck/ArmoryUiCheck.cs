using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 对应配阵改版：布局可达、候选只读、物品身份和失效候选拦截。
internal static class ArmoryUiCheck
{
    internal static void Run(Action<bool,string> check)
    {
        int oldWidth=Main.screenWidth,oldHeight=Main.screenHeight;bool oldDomain=SoulUI.DomainMode;
        var oldPlayer=Main.player[0];Item oldMouse=Main.mouseItem;
        try
        {
            Main.myPlayer=0;Main.netMode=NetmodeID.SinglePlayer;Main.mouseItem=new Item();
            var player=new Player{active=true,whoAmI=0};Main.player[0]=player;
            var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            soul.Attached=true;soul.Kills=SoulProgress.All;soul.Cores=63;soul.Seals=SoulCurses.All;
            soul.Hosts[0]=new Item(ItemID.WoodenSword);soul.RefreshStats();
            player.inventory[0]=soul.Item;player.inventory[1]=new Item(ItemID.IceBlade);player.inventory[1].Prefix(PrefixID.Legendary);
            SoulUI.DomainMode=true;SoulUI.Open(soul);
            foreach(var size in new[]{(400,300),(640,720),(960,720),(1280,720),(1440,900),(2560,1440)})
            foreach(int supports in new[]{5,11,20})
            {
                Main.screenWidth=size.Item1;Main.screenHeight=size.Item2;var layout=SoulUI.ArmoryLayout;
                var canvas=new Rectangle(layout.Stage.X,layout.Stage.Y,layout.Stage.Width,layout.SceneHeight(supports));
                int[] ids=Enumerable.Range(0,10).Concat(Enumerable.Range(0,supports).Select(SoulItem.SupportSlotId)).Concat(new[]{15,16,34}).ToArray();
                var slots=ids.Select(id=>layout.Slot(id,supports,0)).ToArray();
                check(slots.All(r=>canvas.Contains(r)&&r.Width>=40),"剑位保持可用尺寸，暂停和扩展支援也在可滚动画布内");
                check(!slots.Where((r,i)=>slots.Skip(i+1).Any(r.Intersects)).Any(),"主攻、奖励、传奇与所有支援命中框均无重叠");
                check(layout.Workspace.Contains(layout.Library)&&layout.Workspace.Contains(layout.Detail)&&layout.Workspace.Contains(layout.Stage),"三区或窄屏视图均在当前工作区内");
                check(layout.Compact||!layout.Library.Intersects(layout.Detail),"宽屏的剑库与详情不重叠");
                check(layout.Detail.Contains(layout.Confirm)&&layout.Detail.Contains(layout.Return)&&!layout.Confirm.Intersects(layout.Return),"短视口改为并排操作，确认与取回始终可达");
                check(layout.Detail.Contains(layout.DetailBody)&&layout.Library.Contains(layout.LibraryList),"详情和剑库滚动区域始终位于各自面板内");
            }
            Main.screenWidth=1440;Main.screenHeight=900;SoulUI.SelectSlot(0);
            byte[] Snapshot(){using var m=new MemoryStream();soul.NetSend(new BinaryWriter(m));return m.ToArray();}
            byte[] before=Snapshot();Item original=soul.Hosts[0],candidate=player.inventory[1];
            var bonds=BondSnapshot.Evaluate(soul);var leading=BondSnapshot.Leading(bonds);
            check(leading.Kind==BondKind.Wood&&leading.Count==1&&leading.Tier==1&&!leading.IsMax&&before.SequenceEqual(Snapshot()),"中央羁绊核心读取真实木质阶段，不改写剑魂");
            var preview=SoulArmoryPreview.Evaluate(soul,0,candidate);
            check(preview.Allowed&&before.SequenceEqual(Snapshot())&&ReferenceEquals(soul.Hosts[0],original)&&ReferenceEquals(player.inventory[1],candidate),"换入计算不写真实剑魂快照、收藏或背包物品");
            check(SoulUI.SelectArmoryCandidate(1)&&before.SequenceEqual(Snapshot()),"选择候选本身只改变界面状态");
            check(SoulUI.ConfirmArmoryCandidate(out _)&&ReferenceEquals(soul.Hosts[0],candidate)&&ReferenceEquals(player.inventory[1],original)&&candidate.prefix==PrefixID.Legendary,"确认交换真实物品并保持前缀，原武器回原背包格");
            check(SoulUI.CandidateInventorySlot==-1,"完成寄宿后清除候选，不能重复提交旧对象");
            SoulUI.SelectArmoryCandidate(1);player.inventory[1]=new Item(ItemID.Starfury);
            check(!SoulUI.ConfirmArmoryCandidate(out _)&&ReferenceEquals(soul.Hosts[0],candidate)&&player.inventory[1].type==ItemID.Starfury,"背包格被其他操作替换后，旧候选不能误寄宿新物品");
            soul.Hosts[1]=new Item(ItemID.Starfury);before=Snapshot();
            check(!SoulArmoryPreview.Evaluate(soul,0,player.inventory[1]).Allowed&&before.SequenceEqual(Snapshot()),"同种限制使用原规则，拒绝预览不改变收藏");
            Main.screenWidth=640;SoulUI.SelectArmoryPane(1);var click=SoulUI.SlotBounds(0).Center;
            Main.mouseX=click.X;Main.mouseY=click.Y;Main.mouseLeft=Main.mouseLeftRelease=true;
            Main.mouseItem=new Item(ItemID.IronBroadsword);
            check(!SoulUI.TryClickHost(soul,out _)&&Main.mouseLeftRelease&&Main.mouseItem.type==ItemID.IronBroadsword,"窄屏隐藏的剑阵不能消费剑库上的点击");
            Main.mouseItem=new Item();
            check(SoulUI.SelectNextEmptyArmorySlot()&&SoulUI.SelectedSlot==2&&SoulUI.ArmoryPane==0,"定位空位只选择已开放空位，并返回剑阵");
            var fresh=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            check(!SoulArmoryPreview.Evaluate(fresh,9,player.inventory[1]).Allowed&&fresh.Hosts.All(i=>i.IsAir),"未解锁剑位的候选预览被真实规则拒绝");
        }
        finally
        {
            if(SoulUI.EditingSoul!=null)SoulUI.Toggle(SoulUI.EditingSoul);
            Main.screenWidth=oldWidth;Main.screenHeight=oldHeight;Main.player[0]=oldPlayer;Main.mouseItem=oldMouse;
            Main.mouseLeft=Main.mouseLeftRelease=false;SoulUI.DomainMode=oldDomain;SoulInput.Reset();
        }
    }
}
