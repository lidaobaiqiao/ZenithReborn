using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public readonly record struct SlotGate(int Kills, int Cores, string Name, string Requirement);

public static partial class SoulProgress
{
    public const int Mechanical = Destroyer | Retinazer | Spazmatism | Prime;
    public static readonly SlotGate[] SlotGates = [
        new(Eye, 1, "初光", "克苏鲁之眼＋初光魂芯"),
        new(Eye|Evil, 1, "邪恶", "世界吞噬怪或克苏鲁之脑；沿用初光魂芯"),
        new(Eye|Evil|Skeleton, 1, "地牢", "骷髅王；沿用初光魂芯"),
        new(Eye|Evil|Skeleton|Flesh, 3, "血肉", "血肉墙＋血肉魂芯"),
        new(Eye|Evil|Skeleton|Flesh|Mechanical, 7, "机魂", "机械三王全部击败（双子两只都需铭刻）＋机魂魂芯"),
        new(Eye|Evil|Skeleton|Flesh|Mechanical|Flower, 15, "生息", "世纪之花＋生息魂芯"),
        new(Eye|Evil|Skeleton|Flesh|Mechanical|Flower|Temple, 31, "神庙", "石巨人＋神庙魂芯"),
        new(Eye|Evil|Skeleton|Flesh|Mechanical|Flower|Temple|Cultist, 31, "教徒", "拜月教邪教徒；沿用神庙魂芯"),
        new(All, 63, "天界", "月亮领主＋天界魂芯")
    ];
    public static readonly int[] CoreKills = [Eye, Flesh, Mechanical, Flower, Temple, Moon];
    public static readonly string[] CoreInstructions = ["初光：克苏鲁之眼；普通第2至4槽共用", "血肉：血肉墙；普通第5槽",
        "机魂：机械三王全部铭刻；普通第6槽", "生息：世纪之花；普通第7槽", "神庙：石巨人；普通第8至9槽共用", "天界：月亮领主；普通第10槽"];
    public static readonly (int Bit, string Name)[] BossNames = [(Eye,"克苏鲁之眼"), (Evil,"邪恶Boss"), (Skeleton,"骷髅王"),
        (Flesh,"血肉墙"), (Destroyer,"毁灭者"), (Retinazer,"激光眼"), (Spazmatism,"魔焰眼"), (Prime,"机械骷髅王"),
        (Flower,"世纪之花"), (Temple,"石巨人"), (Cultist,"拜月教邪教徒"), (Moon,"月亮领主")];
    public static bool CanAcceptCore(SoulItem soul, int index) => index >= 0 && index < 6 && soul.Attached &&
        (soul.Cores & (1 << index)) == 0 && Has(soul.Kills, CoreKills[index]);
    public static bool AcceptCore(SoulItem soul, int index)
    {
        if (!CanAcceptCore(soul, index)) return false;
        soul.Cores |= 1 << index;
        soul.RefreshStats();
        return true;
    }
    public static int RecoverableCore(SoulItem soul)
    {
        if (!soul.Attached || soul.Capacity >= 10) return -1;
        var gate = SlotGates[soul.Capacity - 1];
        if (!Has(soul.Kills, gate.Kills)) return -1;
        for (int index = 0; index < 6; index++)
            if ((gate.Cores & (1 << index)) != 0 && CanAcceptCore(soul, index)) return index;
        return -1;
    }
    public static string Missing(SoulItem soul, int slot)
    {
        if (slot == 0) return soul.Attached ? "祝福·附着已完成" : "与友善史莱姆王对话，接受祝福·附着";
        if (slot < 1 || slot > 9) return "未知普通槽";
        var gate = SlotGates[slot-1];
        string[] missingBosses = BossNames.Where(b => (gate.Kills & b.Bit) != 0 && !Has(soul.Kills, b.Bit)).Select(b => b.Name).ToArray();
        if (missingBosses.Length > 0) return "用此剑魂参与击败：" + string.Join("、", missingBosses);
        string[] missingCores = Enumerable.Range(0,6).Where(i => (gate.Cores & (1 << i)) != 0 && (soul.Cores & (1 << i)) == 0)
            .Select(i => CoreNames[i] + "魂芯").ToArray();
        if (missingCores.Length > 0) return "融入" + string.Join("、", missingCores) + "（使用物品或路线页补办）";
        return soul.Attached ? "条件已满足" : "还需获得祝福·附着";
    }
    public static string SlotHint(SoulItem soul, int slot)
    {
        if (slot < 10)
        {
            string requirement = slot == 0 ? "友善史莱姆王授予附着" : SlotGates[slot-1].Requirement;
            string status = slot < soul.Capacity ? "已解锁" : "尚未解锁";
            return $"普通第{slot+1}槽 · {status}\n解锁：{requirement}\n{(soul.Capacity > slot ? "武器正常提供属性与主剑影" : Missing(soul,slot))}";
        }
        if (slot == SoulItem.LegendarySlotId)
            return $"传奇剑位 · {(soul.LegendaryOn?"已激活":"未激活（仅实际传奇难度，大师＋FTW）")}\n"+
                "只在实际传奇难度生效；可放任意可寄宿近战武器，提供独立主攻。\n关闭传奇后物品暂停保留，空手可取回；不占普通10槽、不伪造难度标记。";
        if (SoulItem.SupportOrdinal(slot)>=0)
        {
            int ordinal=SoulItem.SupportOrdinal(slot);
            return $"共用支援第{ordinal+1}槽 · {(ordinal<soul.SupportCapacity?"已激活":"未激活／暂停")}\n当前奖励：{SoulSupport.Breakdown(soul)}＝{soul.SupportCapacity}格；各族提供的槽位累加，不取较高值\n木质1/2/4/6/8种各给1格；矿石1/2/4/6/8/10材质各给1格\n支援不出主剑、计入所属族羁绊和已实装特性；失去条件时暂停，物品仍可取回";
        }
        if (slot < 17)
            return $"矿石全收集第{slot-14}槽 · {(soul.BonusCapacity>0?"已解锁":"尚未解锁")}\n累计有效登记14种矿石（{soul.OreCollected}/14）即可解锁\n2格真实主攻剑位；矿石登记取下不丢（V2起木质全收集不再开启此奖励）";
        if (SoulItem.IsCrownSlot(slot))
            return $"圣林之冠第{slot-SoulItem.CrownSlotBase+1}槽 · {(soul.CrownCapacity>0?"已激活":"未激活／暂停")}\n圣林归冠：8种木剑同时有效寄宿且泰拉刃寄宿于剑魂\n2格真实主攻剑位，仅归冠激活时生效；失效时暂停并保留物品，空手可取回\n圣林槽位不参与归冠自身的解锁判定（防自举）";
        return "旧扩展已停用：物品不参与伤害、特性与羁绊；只可空手取回，不可放入。";
    }
    public static void RequestCoreRecovery(SoulItem soul)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        { var packet = ModContent.GetInstance<ZenithReborn>().GetPacket(); packet.Write((byte)5); packet.Write(soul.SoulId); packet.Send(); }
        else RecoverCore(Main.LocalPlayer, soul.SoulId);
    }
    public static bool RecoverCore(Player player, string id)
    {
        SoulItem soul = AttachmentStory.FindSoul(player, id);
        if (soul == null || !player.active || player.dead) return false;
        int index = RecoverableCore(soul);
        if (index < 0) return false;
        int type = SoulCore.TypeFor(index);
        Item heldCore = player.inventory.FirstOrDefault(i => i.type == type && i.stack > 0);
        if (heldCore != null)
        {
            heldCore.stack--;
            if (heldCore.stack == 0) heldCore.TurnToAir();
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, Array.IndexOf(player.inventory, heldCore));
        }
        AcceptCore(soul, index);
        for (int slot = 0; slot < 58; slot++)
            if (ReferenceEquals(player.inventory[slot].ModItem, soul) && Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot);
        AttachmentStory.Notice(player, $"{CoreNames[index]}魂芯已归位。普通槽 {soul.Capacity}/10；" + Next(soul));
        return true;
    }
}
