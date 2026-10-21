using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ZenithPrototypeCheck;

internal static class LegendaryCheck
{
    internal static void Run(Action<bool, string> check)
    {
        int oldMode = Main.GameMode;
        bool oldWorthy = Main.getGoodWorld;
        try
        {
            Main.GameMode = 2;
            Main.getGoodWorld = true;
            var item = new Item(ModContent.ItemType<ZenithReborn.SoulItem>());
            var soul = (ZenithReborn.SoulItem)item.ModItem;
            soul.Attached = true;
            soul.Hosts[0] = new Item(ItemID.CopperShortsword);
            check(soul.LegendaryOn && soul.VisibleSlotIds.Contains(ZenithReborn.SoulItem.LegendarySlotId),
                "Legendary slot is visible only in actual legendary mode");
            check(ZenithReborn.SoulItem.SupportOrdinal(ZenithReborn.SoulItem.LegendarySlotId) < 0 &&
                  ZenithReborn.SoulProgress.SlotHint(soul, ZenithReborn.SoulItem.LegendarySlotId).Contains("传奇"),
                "Legendary slot is not addressed as a support slot");
            var sword = new Item(ItemID.IronBroadsword);
            check(soul.TrySwapHost(ZenithReborn.SoulItem.LegendarySlotId, ref sword, out _) && sword.IsAir,
                "Legendary slot accepts a main weapon");
            check(soul.Active.Any(i => i.type == ItemID.IronBroadsword), "Legendary slot contributes an active main weapon");

            var tag = new TagCompound();
            soul.SaveData(tag);
            var loaded = (ZenithReborn.SoulItem)new Item(item.type).ModItem;
            loaded.LoadData(tag);
            check(loaded.LegendaryHost.type == ItemID.IronBroadsword, "Legendary slot survives save/load");

            using var stream = new MemoryStream();
            soul.NetSend(new BinaryWriter(stream));
            stream.Position = 0;
            loaded.NetReceive(new BinaryReader(stream));
            check(loaded.LegendaryHost.type == ItemID.IronBroadsword, "Legendary slot survives network round-trip");

            Main.getGoodWorld = false;
            check(!loaded.LegendaryOn && loaded.VisibleSlotIds.Contains(ZenithReborn.SoulItem.LegendarySlotId),
                "Leaving legendary keeps the paused slot visible for retrieval");
            var incoming = new Item(ItemID.GoldBroadsword);
            check(!loaded.TrySwapHost(ZenithReborn.SoulItem.LegendarySlotId, ref incoming, out _) && !incoming.IsAir,
                "Paused legendary slot rejects insertion");
            var empty = new Item();
            check(loaded.TrySwapHost(ZenithReborn.SoulItem.LegendarySlotId, ref empty, out _) &&
                  empty.type == ItemID.IronBroadsword && loaded.LegendaryHost.IsAir,
                "Paused legendary item can be retrieved with an empty hand");
        }
        finally
        {
            Main.GameMode = oldMode;
            Main.getGoodWorld = oldWorthy;
        }
    }
}
