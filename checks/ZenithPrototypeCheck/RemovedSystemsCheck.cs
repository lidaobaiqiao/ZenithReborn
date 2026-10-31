using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;

// 检查移除武技与灵能分类后，旧字段和已寄宿武器仍能安全往返。
internal static class RemovedSystemsCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var assembly=typeof(SoulItem).Assembly;
        foreach(string name in new[]{"MartialPlayer","MartialBlade","MartialGiant","MartialWave","MartialHUD","MartialConfig","MartialFreezeNPC","MartialFreezeProjectile","SpiritFamily"})
            check(assembly.GetType("ZenithReborn."+name)==null,"已移除的系统不再注册："+name);
        var source=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;
        source.Attached=true;source.Hosts[0]=new Item(ItemID.WoodenSword);source.Hosts[0].Prefix(PrefixID.Legendary);
        source.Hosts[1]=new Item(ItemID.CopperBroadsword);source.SupportHosts[0]=new Item(ItemID.EnchantedSword);
        source.LegacyMartialCharge=234;source.LegacyMartialTier=3;
        var tag=new TagCompound();source.SaveData(tag);
        var restored=(SoulItem)new Item(source.Type).ModItem;restored.LoadData(tag);
        check(restored.Hosts[0].type==ItemID.WoodenSword&&restored.Hosts[0].prefix==source.Hosts[0].prefix&&restored.Hosts[1].type==ItemID.CopperBroadsword,
            "木剑、矿石剑与前缀通过旧档读写保留");
        check(restored.SupportHosts[0].type==ItemID.EnchantedSword,"原灵能名册中的武器仍可保存在支援槽");
        check(restored.LegacyMartialCharge==234&&restored.LegacyMartialTier==3,"旧武技字段可回退但不再参与战斗");
        using(var bytes=new MemoryStream())
        {
            source.NetSend(new BinaryWriter(bytes));byte[] payload=bytes.ToArray();
            check(payload.Length>=3&&payload[^3]==234&&payload[^2]==0&&payload[^1]==3,"旧武技字段保留网络末尾三字节格式");
            bytes.Position=0;restored.NetReceive(new BinaryReader(bytes));
            check(bytes.Position==bytes.Length&&restored.LegacyMartialCharge==234&&restored.LegacyMartialTier==3,"网络数据完整读取且保留旧字段");
            check(restored.Hosts[0].type==ItemID.WoodenSword&&restored.Hosts[1].type==ItemID.CopperBroadsword,"网络同步保留木质与矿石寄宿物品");
        }
        source.PreReforge();source.LegacyMartialCharge=0;source.LegacyMartialTier=1;source.PostReforge();
        check(source.LegacyMartialCharge==234&&source.LegacyMartialTier==3&&source.Hosts[1].type==ItemID.CopperBroadsword,"重铸保留旧字段与寄宿物品");
    }
}
