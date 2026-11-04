using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 锻根共生 · 锻芽（组合 A 机制，V2）。
// 派发入口：SoulBlade.OnHitNPC → WoodBondCore.ActiveForge 已确认激活后，
// 以铜阔剑（ItemID.CopperBroadsword）或铁阔剑（ItemID.IronBroadsword）来源主剑影调用本方法。
// 机制规格（V2）：
//  - 触发条件：主剑影命中带盐蚀标记（SaltCorrosion，棕榈木效果，WoodCombat 贡献通道 index 2）的目标时 +1 层锻芽；
//  - 数值：锻芽上限 3 层、每层持续 180 帧；满 3 层立即消耗全部层数并补斩 30%×D
//    （D 为 WoodBondHit.D 本次命中实际伤害快照），仅命中 1 个目标（当前命中对象）；
//  - 冷却：补斩后同目标 90 帧内不得再积层；
//  - 限制：补斩走 NPC.StrikeNPC 直伤路径，不发射 SoulBlade 或派生弹幕，
//    不会再触发任何按主命中计数的机制，也不重新施加状态；盐蚀状态读取经 WoodCombat.HasSalt。
// 多人执行模型：主剑影碰撞只在拥有者客户端发生，故客户端只做条件探测并把命中上报服务端
// （WoodBondRelay，action 12），由服务端用自身 SoulItem 状态重新校验后执行；客户端绝不本地结算。
internal static class WoodBondForge
{
    // 规格常量：上限3层、每层180帧、补斩30%×D、同目标90帧触发冷却。
    private const int MaxLayers = 3, LayerFrames = 180, CooldownFrames = 90;
    private const float StrikeRatio = 0.30f;

    // 锻芽层数：目标侧状态（参考 HardOrePlayer.marks 键形），键＝(玩家, 剑魂, 目标根NPC的HardOreTarget.Id)。
    // 每层独立记录到期帧（180帧内未续层即过期清零）；补斩后记 CooldownUntil，冷却期内命中不积层。
    // 目标重生时 HardOreTarget.Id 为新序列号，天然不串档；条目在每次执行时惰性清理（Prune）。
    // 冷却语义：层数与冷却**只记在执行侧**——单人＝本进程，多人＝服务端字典；客户端不写任何层数/冷却，
    // 因此不存在"两端各记一份把 90 帧冷却翻倍"的问题。
    // 不用 WoodPlayer.Ready 做冷却：Ready 每次成功都会重置窗口，会把积层节流成 90 帧/层，故与层数统一存目标侧。
    private sealed class SproutState
    {
        public int Npc;                       // 目标根NPC 的 Main.npc 索引：用于死亡/换实体时整条清理
        public readonly List<ulong> Expires = [];
        public ulong CooldownUntil;
    }
    private static readonly Dictionary<(int Player, string Soul, long Target), SproutState> sprouts = [];

    public static void OnPrimaryHit(WoodBondHit hit, int identity)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            // 多人：本函数只在弹幕拥有者客户端跑一次；仅探测条件并上报，打击交给服务端（避免双结算）。
            if (hit.Player.whoAmI != Main.myPlayer || !CanTrigger(hit)) return;
            WoodBondRelay.Send(WoodBondRelay.KindForge, hit, identity);
            return;
        }
        // 服务端：唯一执行入口是 WoodBondRelay.Receive（中继包），本地命中不结算，避免与客户端上报重复打击。
        if (Main.netMode == NetmodeID.Server) return;
        // 单人：直接本地执行。
        Execute(hit);
    }

    // 客户端条件探测：组合激活＋本剑魂盐蚀仍在（客户端贡献表由 SendExtraAI 同步，仅用于决定是否上报）。
    private static bool CanTrigger(WoodBondHit hit) =>
        WoodBondCore.ActiveForge(hit.Soul) &&
        WoodCombat.HasSalt(hit.Target, hit.Player.whoAmI, hit.Soul.SoulId);

    // 权威执行：单人本地与服务端中继共用同一入口，条件在此重新校验（服务端用自身 SoulItem/贡献表状态）。
    public static void Execute(WoodBondHit hit)
    {
        if (!WoodBondCore.ActiveForge(hit.Soul)) return;
        // 触发条件：命中目标带本剑魂盐蚀（贡献表 index 2 通道持续中）。
        if (!WoodCombat.HasSalt(hit.Target, hit.Player.whoAmI, hit.Soul.SoulId)) return;
        ulong now = Main.GameUpdateCount;
        Prune(now);
        var root = WoodCombat.Root(hit.Target);
        var key = (hit.Player.whoAmI, hit.Soul.SoulId, root.GetGlobalNPC<HardOreTarget>().Id);
        if (!sprouts.TryGetValue(key, out var state)) sprouts[key] = state = new();
        // 记录根NPC索引供 Prune 存活判定（key 里只有 Id，无法反查索引）。
        state.Npc = root.whoAmI;
        // 触发冷却：补斩后 90 帧内同目标不得再积层。
        if (now < state.CooldownUntil) return;
        // 过期层清零（每层独立 180 帧），随后本次命中 +1 层。
        state.Expires.RemoveAll(e => e <= now);
        state.Expires.Add(now + LayerFrames);
        if (state.Expires.Count < MaxLayers) return;
        // 满3层：立即消耗全部层数，对当前目标补斩 30%×D；
        // 伤害取整沿用项目直伤惯例（BlessingStrike 同式 Max(1,(int)(D×factor))）；
        // StrikeNPC 走 HitInfo 重载（无参构造已默认不暴击/零击退/零方向，与旧三参调用语义一致）。
        state.Expires.Clear();
        state.CooldownUntil = now + CooldownFrames;
        hit.Target.StrikeNPC(new NPC.HitInfo { Damage = Math.Max(1, (int)(hit.D * StrikeRatio)) });
        // StrikeNPC 为直伤：不进 SoulBlade 命中管道、不触发按主命中计数的机制，杜绝递归。
    }

    // 惰性清理：目标死亡/不存在/索引被新 NPC 复用（Id 不符）即整条移除；
    // 存活但层数已空且冷却结束的条目一并回收，避免长局内存泄漏。
    private static void Prune(ulong now)
    {
        foreach (var pair in sprouts.ToArray())
        {
            SproutState state = pair.Value;
            bool playerAlive = pair.Key.Player >= 0 && pair.Key.Player < Main.maxPlayers && Main.player[pair.Key.Player].active;
            // 目标存活判定按根NPC：蠕虫节段死亡但本体仍在时保留层数，本体灭失或索引被复用（Id 不符）才移除。
            bool targetAlive = false;
            if (state.Npc >= 0 && state.Npc < Main.maxNPCs)
            {
                NPC root = WoodCombat.Root(Main.npc[state.Npc]);
                targetAlive = root.active && root.GetGlobalNPC<HardOreTarget>().Id == pair.Key.Target;
            }
            if (!playerAlive || !targetAlive || (state.Expires.Count == 0 && now >= state.CooldownUntil))
                sprouts.Remove(pair.Key);
        }
    }
}

// A/B/C 三个复合羁绊共用的中继协议（ModPacket action 12）——新增独立帧，不改动 WoodCombat 的 action 3 布局。
// 帧格式：kind(byte) | 目标 whoAmI(short) | 目标 netID(int) | 来源武器 type(int) | HostSlot(short) | identity(int) | SoulId(string)
// 方向：拥有者客户端 → 服务端（单机不走本协议）。
// 帧不再携带客户端自报伤害 D，改带本次命中主剑影弹幕的 identity；服务端按 identity 在 Main.projectile
// 定位真实 SoulBlade，以 projectile.damage 覆盖结算值，找不到/不匹配即丢弃（防 D 伪造）。收发两端字段必须对称。
// 服务端校验：发包者在位且存活、目标索引/实体/类型一致、距离合理、剑魂仍寄宿、来源武器与 kind 匹配、
// 按 (owner=who, identity, active, SoulBlade) 定位弹幕；随后各 Execute 再用服务端自己的状态重判条件。
internal static class WoodBondRelay
{
    public const byte KindForge = 0, KindRot = 1, KindEmber = 2;
    // 距离上限：主剑影由玩家发射并持有，正常命中不会离玩家太远（牵引半径见 SoulSource.LeashRadius）；
    // 4000 像素（250 格）留足飞行余量，同时挡掉伪造的远处目标。
    private const float MaxRelayDistance = 4000f;

    public static void Send(byte kind, WoodBondHit hit, int identity)
    {
        var packet = ModContent.GetInstance<ZenithReborn>().GetPacket();
        packet.Write((byte)12);
        packet.Write(kind);
        packet.Write((short)hit.Target.whoAmI);
        packet.Write(hit.Target.netID);
        packet.Write(hit.SourceWeapon);
        packet.Write((short)hit.HostSlot);
        packet.Write(identity);
        packet.Write(hit.Soul.SoulId);
        packet.Send();
    }

    public static void Receive(BinaryReader reader, int who)
    {
        byte kind = reader.ReadByte();
        short index = reader.ReadInt16();
        int netId = reader.ReadInt32();
        int weapon = reader.ReadInt32();
        short hostSlot = reader.ReadInt16();
        int identity = reader.ReadInt32();
        string soulId = reader.ReadString();
        if (Main.netMode != NetmodeID.Server || who < 0 || who >= Main.maxPlayers) return;
        if (index < 0 || index >= Main.maxNPCs || soulId.Length > 64) return;
        Player player = Main.player[who];
        NPC npc = Main.npc[index];
        if (!player.active || player.dead || !npc.active || npc.netID != netId || !WoodCombat.Eligible(npc)) return;
        if (Vector2.Distance(player.Center, npc.Center) > MaxRelayDistance) return;
        if (!WeaponMatches(kind, weapon)) return;
        SoulItem soul = WoodCombat.FindSoul(player, soulId);
        if (soul == null) return;
        // 按 identity + owner + active 定位真实主剑影（照 WoodCombat.ReceiveHit 的 action 3 写法）。
        // 伪造包没有对应弹幕，或 identity/owner/类型不匹配，FirstOrDefault 返回 null 即 early return 丢弃。
        Projectile blade = Main.projectile.FirstOrDefault(p => p.active && p.owner == who && p.identity == identity && p.ModProjectile is SoulBlade);
        if (blade == null) return;
        // 结算值一律用服务端真实弹幕伤害覆盖；客户端上报帧里不再有 D，无法自报数值。
        // PrimaryOrdinal 填 0：中继只承载"真主命中"，衍生伤害不经本协议回流。
        var hit = new WoodBondHit(player, soul, weapon, hostSlot, 0, npc, blade.damage, true);
        switch (kind)
        {
            case KindForge: WoodBondForge.Execute(hit); break;
            case KindRot: WoodBondRot.Execute(hit); break;
            case KindEmber: WoodBondEmber.Execute(hit); break;
        }
    }

    // 来源武器→机制映射与 SoulBlade.OnHitNPC 的派发条件一致，防止客户端把别的武器命中塞进某个机制。
    private static bool WeaponMatches(byte kind, int weapon) => kind switch
    {
        KindForge => weapon is ItemID.CopperBroadsword or ItemID.IronBroadsword,
        KindRot => weapon is ItemID.Muramasa or ItemID.NightsEdge,
        KindEmber => weapon == ItemID.FieryGreatsword,
        _ => false
    };
}
