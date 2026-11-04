using Terraria;

namespace ZenithReborn;

public readonly record struct SupportGrant(string Id,string Name,int[] Thresholds,Func<IEnumerable<Item>,int> Count);
// Every race contributes grants to one shared pool. Registering another race
// does not require changing the slot engine or adding a new race-owned array.
public static class SoulSupport
{
    public static readonly List<SupportGrant> Grants=[
        new("wood","木质族",[1,2,4,6,8],items=>System.Numerics.BitOperations.PopCount((uint)WoodTypes.Mask(items))),
        new("ore","矿石族",[1,2,4,6,8,10],items=>OreFamily.PopCount(OreFamily.Mask(items)))];
    public static int MaximumGrants=>Grants.Sum(g=>g.Thresholds.Length);
    public static bool CanSupport(Item item)=>SoulItem.CanHost(item);
    public static int DistinctCount(IEnumerable<Item> items)=>items.Where(CanSupport).Select(i=>i.type).Distinct().Count();
    public static int Capacity(SoulItem soul)
    {
        if(!soul.Attached)return 0;
        // Start from real normal slots, then admit only the currently opened
        // prefix. Locked and retired storage cannot bootstrap slots. Earned
        // reward primaries (ore reward and crown slots) may supply ore, but
        // never reverse-complete wood.
        // V2：木质全收集不再开启奖励槽，门槛只看矿石圆满。
        var seed=soul.Hosts.Take(soul.BaseCapacity).Where(CanSupport).ToArray();
        int capacity=0;
        for(int attempt=0;attempt<=MaximumGrants;attempt++)
        {
            var items=seed.Concat(soul.AllSupportSlots.Take(capacity)).ToArray();
            bool reward=soul.OreMastered;
            // 圣林槽位是真实剑位：激活时与全收集奖励槽同样并入矿石名册；木质族计数保持只用真实与支援槽，防止自举。
            var roster=items.Concat(soul.ForestHosts.Take(reward?2:0)).Concat(soul.CrownHosts.Take(soul.CrownCapacity));
            int next=Grants.Sum(g=>g.Thresholds.Count(n=>g.Count(g.Id=="wood"?items:roster)>=n));
            if(next==capacity)return capacity;
            capacity=next;
        }
        return capacity;
    }
    public static IEnumerable<Item> SetItems(SoulItem soul)=>soul.Hosts.Take(soul.BaseCapacity)
        .Concat(soul.AllSupportSlots.Take(Capacity(soul))).Where(i=>!i.IsAir);
    // 族判定用的有效寄宿集合：主攻槽 + 已生效支援槽 + 森林/圣林奖励槽 + 传奇位（等价于 Active∪Supports）。
    // 奖励位本身就是给玩家的回报，不设例外；支援解锁（UnlockCount）与种族授予（GrantedBy）仍只用 SetItems，
    // 因为那两处算的是“解锁进度”，放进奖励位的剑不该反向解锁更多格子。
    public static IEnumerable<Item> FamilyItems(SoulItem soul)=>soul.Active.Concat(soul.Supports);
    public static int UnlockCount(SoulItem soul)=>soul.Attached?DistinctCount(SetItems(soul)):0;
    public static int GrantedBy(SoulItem soul,string race)
    {
        if(!soul.Attached)return 0;var g=Grants.Single(g=>g.Id==race);var items=SetItems(soul).ToArray();
        bool reward=soul.OreMastered;
        // 与 Capacity 同语义：矿石名册并入奖励与圣林槽，木质族只用真实与支援槽。
        int count=g.Count(g.Id=="wood"?items:items.Concat(soul.ForestHosts.Take(reward?2:0)).Concat(soul.CrownHosts.Take(soul.CrownCapacity)));
        return g.Thresholds.Count(n=>count>=n);
    }
    public static string Breakdown(SoulItem soul)=>string.Join("＋",Grants.Select(g=>$"{g.Name}{GrantedBy(soul,g.Id)}格"));
}
