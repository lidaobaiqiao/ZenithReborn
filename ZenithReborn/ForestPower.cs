using Terraria;

namespace ZenithReborn;

// 森林之力 V2：8 木全套（基础普通槽＋已生效支援槽＋森林/圣林奖励槽＋传奇位）激活基础成长，上限 6 层；
// 圣林之冠（WoodCrown 判定，由并行代理实现真实组合）生效时上限提升到 12 层，
// 已积累层数保留并继续成长，全部层数统一按当前生效的成长表计算（两套表切换，不叠加）。
// 成长状态存放于 WoodPlayer 的 ModPlayer 字段（沿用既有同步链路）；
// 本类承载对外静态查询契约、成长引擎与 buff 文案。类名沿用原 buff 类型名
// （checks 与本地化键按 ForestPower 引用），静态契约以 ForestPower.Layers(player)
// 等形式调用，与独立静态类的调用形式完全一致。
public class ForestPower : WoodPlayerBuff
{
    public const int BaseCap = 6;            // 基础上限（仅 8 木全套）
    public const int CrownCap = 12;          // 圣林之冠上限
    private const int FramesPerLayer = 300;  // 每层所需有效战斗帧：基础 30 秒满层，归冠 60 秒满层
    private const int CombatGrace = 300;     // 最近一次有效命中后的成长窗口（帧）
    private const int DecayInterval = 120;   // 脱战后每 120 帧 -1 层

    // —— 对外查询契约（并行代理依赖，勿改名）——
    public static int Layers(Player player) => player.GetModPlayer<WoodPlayer>().EffectiveLayers;
    // 透传圣林之冠判定（真实组合判定由 WoodCrown 并行实现）。
    public static bool CrownActive(Player player) => Snapshot(player).Crown;

    // 每层成长表——基础：伤害+2% 速度+1% 防御+2 回复+0.3/秒 DoT+3% 减伤+0.5个百分点；
    // 归冠：伤害+3% 速度+2% 防御+3 回复+0.5/秒 DoT+5% 减伤+1个百分点。
    // 收益均为独立加值，不乘环境倍率 S。
    public static float DamageBonus(Player player) { var (n, c) = Snapshot(player); return n * (c ? .03f : .02f); }
    public static float SpeedBonus(Player player) { var (n, c) = Snapshot(player); return n * (c ? .02f : .01f); }
    public static int DefenseBonus(Player player) { var (n, c) = Snapshot(player); return n * (c ? 3 : 2); }
    public static float RegenBonus(Player player) { var (n, c) = Snapshot(player); return n * (c ? .5f : .3f); }
    public static float DotBonusPercent(Player player) { var (n, c) = Snapshot(player); return n * (c ? .05f : .03f); }
    public static float DamageReduction(Player player) { var (n, c) = Snapshot(player); return n * (c ? .01f : .005f); }

    private static (int Layers, bool Crown) Snapshot(Player player)
    {
        var wp = player.GetModPlayer<WoodPlayer>();
        return (wp.EffectiveLayers, wp.HeldSoul is { } soul && WoodCrown.Active(soul));
    }

    // 8 木全套判定口径：未过滤的「有效寄宿」＝主攻槽＋已生效支援槽＋森林/圣林奖励槽＋传奇位（FamilyItems），
    // 与 WoodCrown / WoodTypes.Evaluate 同源；奖励位同样计入，不设例外。
    // 诅咒 TraitEnabled 只影响特性施加与 DoT 标记，不参与此处判定。
    internal static bool FullSet(SoulItem soul) => WoodTypes.Mask(SoulSupport.FamilyItems(soul)) == 0xFF;

    // —— 成长引擎：由 WoodPlayer.AdvanceGrowth/RecordHit/ResetGrowth 转调 ——
    internal static void Advance(WoodPlayer wp)
    {
        SoulItem soul = wp.HeldSoul;
        // 8 木全套不满足（含未持魂/死亡）：立即清零。
        if (soul == null || !FullSet(soul)) { Reset(wp); return; }
        // 换魂：清零后重新积累；死亡清零由 WoodPlayer.UpdateDead → ResetGrowth 触发。
        if (wp.CurrentSoul != soul.SoulId) { Reset(wp); wp.CurrentSoul = soul.SoulId; }
        int cap = WoodCrown.Active(soul) ? CrownCap : BaseCap;
        // 降级：失去归冠但仍 8 木——超出新上限的层直接削掉，其余层数保留。
        if (wp.Layers > cap) { wp.Layers = cap; wp.CombatFrames = 0; }
        if (wp.RecentCombat > 0)
        {
            // 有效战斗窗口内：每累计 300 帧 +1 层（基础与归冠同速）。
            wp.RecentCombat--; wp.Decay = 0;
            if (wp.Layers < cap && ++wp.CombatFrames >= FramesPerLayer) { wp.Layers++; wp.CombatFrames = 0; }
        }
        else if (++wp.Decay >= DecayInterval)
        {
            // 脱战：每 120 帧 -1 层。
            wp.Decay = 0;
            wp.Layers = Math.Max(0, wp.Layers - 1);
            if (wp.Layers == 0) wp.CombatFrames = 0;
        }
    }

    internal static void RecordHit(WoodPlayer wp, SoulItem soul)
    {
        // 有效命中：手持同魂且 8 木全套；挥空、假人、DoT 跳伤不会进入本入口（由 WoodCombat 调用方保证）。
        if (wp.HeldSoul?.SoulId != soul.SoulId || !FullSet(soul)) return;
        if (wp.CurrentSoul != soul.SoulId) { Reset(wp); wp.CurrentSoul = soul.SoulId; }
        wp.RecentCombat = CombatGrace;
    }

    internal static void Reset(WoodPlayer wp)
    {
        wp.Layers = wp.CombatFrames = wp.RecentCombat = wp.Decay = 0;
        wp.CurrentSoul = "";
    }

    public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
    {
        Player player = Main.LocalPlayer;
        var (n, crown) = Snapshot(player);
        // 生命与真实剑位是全套/归冠的固定收益，与层数无关（数值对应 WoodLoadout.Life 与全收集奖励槽）。
        int life = crown ? 100 : 40;
        string head = crown
            ? $"森林之力 · 圣林之冠 {n}/{CrownCap}层 · 生命＋{life} · 真实剑位＋2"
            : $"森林之力 {n}/{BaseCap}层 · 生命＋{life} · 真实剑位＋2";
        string tail = crown
            ? "有效战斗60秒满层，脱战每2秒-1层；失去归冠回落至6层，失去全套立即清零"
            : "有效战斗30秒满层，脱战每2秒-1层；失去全套立即清零";
        tip = $"{head}\n" +
              $"主攻伤害＋{DamageBonus(player) * 100:0.#}% · 出剑速度＋{SpeedBonus(player) * 100:0.#}% · 防御＋{DefenseBonus(player)}\n" +
              $"自然回复＋{RegenBonus(player):0.#}/秒 · 自有持续伤害＋{DotBonusPercent(player) * 100:0.#}% · 减伤＋{DamageReduction(player) * 100:0.#}个百分点\n" +
              tail;
    }
}
