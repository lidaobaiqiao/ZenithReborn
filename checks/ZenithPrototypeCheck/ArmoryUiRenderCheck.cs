using Microsoft.Xna.Framework;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 在独立临时剑魂上调用真实绘制和按钮；不接触正式人物、世界或背包。
internal static class ArmoryUiRenderCheck
{
    internal delegate void Frame(string name,int width=1440,int height=900,Point? click=null,float? age=null,float? opening=null);
    internal static object Run(SoulItem original,Player player,Frame render,out bool passed)
    {
        Item[] inventory=player.inventory;player.inventory=inventory.Select(i=>i.Clone()).ToArray();
        var soul=(SoulItem)player.inventory[0].ModItem;
        try
        {
            int[] extras=[ItemID.TitaniumSword,ItemID.FieryGreatsword,ItemID.BeamSword,ItemID.NightsEdge,ItemID.TerraBlade];
            for(int i=0;i<extras.Length;i++)player.inventory[i+3]=new Item(extras[i]);
            SoulUI.Open(soul);SoulUI.SelectSlot(2);
            byte[] Snapshot(){using var m=new MemoryStream();soul.NetSend(new BinaryWriter(m));return m.ToArray();}
            byte[] before=Snapshot();
            render("armory-populated");
            Item candidate=player.inventory[2],outgoing=soul.Hosts[2];
            render("armory-candidate-click",click:SoulUI.LibraryCardBounds(1).Center);
            bool previewReadOnly=SoulUI.CandidateInventorySlot==2&&before.SequenceEqual(Snapshot())&&ReferenceEquals(player.inventory[2],candidate);
            render("armory-preview");
            render("armory-confirm-click",click:SoulUI.ArmoryLayout.Confirm.Center);
            bool exactExchange=ReferenceEquals(soul.Hosts[2],candidate)&&ReferenceEquals(player.inventory[2],outgoing);
            Rectangle stable=SoulUI.SlotBounds(2);bool fixedTargets=true;
            const BindingFlags hidden=BindingFlags.Static|BindingFlags.NonPublic;
            Type coreFlowType=typeof(SoulUI).GetNestedType("CoreFlowEvent",BindingFlags.NonPublic);
            var coreFlowList=(System.Collections.IList)typeof(SoulUI).GetField("coreFlows",hidden).GetValue(null);
            // 归位和汇流共用同一事件年龄，实际顺序为先到达剑位，再进入核心。
            for(int i=0;i<13;i++)
            {
                float age=i*.105f;double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
                coreFlowList.Clear();coreFlowList.Add(Activator.CreateInstance(coreFlowType,now-age+.55,2));
                render($"attunement/frame-{i:00}",age:age);fixedTargets&=SoulUI.SlotBounds(2)==stable;
            }
            // 另保留核心流光的单独固定时间样本，便于核对弯曲拖尾和加速吸入。
            for(int i=0;i<10;i++)
            {
                coreFlowList.Clear();
                double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
                coreFlowList.Add(Activator.CreateInstance(coreFlowType,now-i*.08,2));
                render($"coreflow/frame-{i:00}");
            }
            SoulUI.SelectSlot(9);SoulUI.SelectArmoryCandidate(1);byte[] lockedBefore=Snapshot();
            render("armory-locked");render("armory-locked-click",click:SoulUI.ArmoryLayout.Confirm.Center);
            bool lockedRejected=lockedBefore.SequenceEqual(Snapshot())&&soul.Hosts[9].IsAir;
            SoulUI.SelectSlot(2);SoulUI.SelectArmoryPane(0);render("armory-narrow",640,720);
            SoulUI.SelectArmoryPane(1);render("armory-narrow-library",640,720);
            render("armory-narrow-select",640,720,SoulUI.LibraryCardBounds(0).Center);
            bool narrowDetail=SoulUI.ArmoryPane==2;
            render("armory-narrow-detail",640,720);
            // 开局与满支援是两份独立样本，避免靠绘制时伪造标签。
            var fresh=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;player.inventory[0]=fresh.Item;SoulUI.Open(fresh);
            render("armory-first-blessing");
            var full=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
            full.Attached=true;full.Kills=SoulProgress.All;full.Cores=63;full.Seals=SoulCurses.All;full.OreCollectionMask=OreFamily.AllMaterials;
            for(int i=0;i<5;i++){full.Hosts[i]=new Item(WoodTypes.Types[i]);full.Hosts[i+5]=new Item(OreFamily.DefaultType[i]);}
            int[] woodSupport=WoodTypes.Types.Skip(5).Take(3).ToArray();
            int[] oreSupport=OreFamily.DefaultType.Skip(5).Take(5).ToArray();
            int[] extraSupport={ItemID.EnchantedSword,ItemID.Starfury,ItemID.BeamSword};
            int[] supportTypes=woodSupport.Concat(oreSupport).Concat(extraSupport).ToArray();
            for(int i=0;i<supportTypes.Length;i++)
            {if(i<5)full.SupportHosts[i]=new Item(supportTypes[i]);else full.AdditionalSupports.Add(new Item(supportTypes[i]));}
            full.ForestHosts[0]=new Item(ItemID.TerraBlade);full.ForestHosts[1]=new Item(ItemID.Meowmere);full.RefreshStats();
            player.inventory[0]=full.Item;SoulUI.Open(full);SoulUI.SelectSlot(25);render("armory-full-supports");
            bool fullSupport=full.SupportCapacity==11;
            for(int i=0;i<5;i++)render($"opening/frame-{i:00}",opening:i*.17f);
            passed=previewReadOnly&&exactExchange&&fixedTargets&&lockedRejected&&narrowDetail&&fullSupport;
            return new{passed,previewReadOnly,exactExchange,fixedTargets,lockedRejected,narrowDetail,fullSupport,operationFrames=13,coreFlowFrames=10,openingFrames=5};
        }
        finally{player.inventory=inventory;SoulUI.Open(original);Main.mouseLeft=Main.mouseLeftRelease=false;}
    }
}
