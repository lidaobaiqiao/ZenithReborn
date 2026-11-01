using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 分类顺序同时是图鉴筛选按钮的顺序，新增分类只能追加在末尾，不能插到中间打乱旧编号。
public enum TraitCategory { Wood, Ore, Sword, Growth, Heaven, Living, Flying }
public readonly record struct SoulTrait(string Key,string Title,string Description,string Requirement,TraitCategory Category,bool Active);

// 展示层与战斗读取同一有效槽规则；查阅图鉴不会改变解锁、战斗状态或寄宿物品。
public static class SoulTraits
{
    public static string CategoryName(TraitCategory category) => category switch {
        TraitCategory.Wood=>"木质族",TraitCategory.Ore=>"矿石族",TraitCategory.Sword=>"剑术",TraitCategory.Heaven=>"神圣天堂",TraitCategory.Living=>"剑灵（暂名）",TraitCategory.Flying=>"飞刃族",_=>"成长" };
    public static IEnumerable<SoulTrait> Active(SoulItem soul) => All(soul).Where(t=>t.Active);
    public static IReadOnlyList<SoulTrait> All(SoulItem soul)
    {
        var result=new List<SoulTrait>();
        var main=soul.Attached?soul.TraitMains.ToArray():[];
        var support=soul.Attached?soul.TraitSupports.ToArray():[];
        var equipped=main.Concat(support).ToArray();
        bool hasMain=main.Length>0;
        var wood=soul.CombatWood;
        void Add(string key,string title,string description,string requirement,TraitCategory category,bool active) =>
            result.Add(new(key,title,description,requirement,category,active));
        Add("wood-set","自然之力",wood.Count>0?
            $"木质族 {wood.Count}/12 · 环境效果 {wood.Strength:0.##}倍 · 最大生命 +{wood.Life}":
            "1/2/4/6/8种有效木剑对应1.10/1.20/1.35/1.50/1.75倍环境效果、5/10/15/20/30生命。支援槽已改为独立通用机制。",
            "获得附着，将木剑放入有效普通槽；有效支援中的木剑也计入种类。",TraitCategory.Wood,wood.Count>0);
        Add("forest","森林之力","12种木剑同时有效寄宿：环境效果达到基础3倍，最大生命+100，解锁2个共享全收集奖励剑位；有效战斗60秒到12层。满层主攻伤害+36%、速度+24%、防御+36、回复+6/秒、自有持续伤害+60%、减伤+12个百分点。",
            "12种木剑同时放在基础普通及有效支援槽中，支持共用支援池的不同排布。取下失效，奖励槽不能反向凑木质全套。",TraitCategory.Wood,wood.Full);
        for(int i=0;i<WoodTypes.Types.Length;i++)
            Add("wood-"+i,WoodTypes.Names[i]+" · "+WoodTypes.Effects[i],WoodTypes.Description(WoodTypes.Types[i]),
                "该木剑处于有效普通或支援槽。S为当前木族倍率，P为实际主剑影伤害；命中后触发，持续时间与目标免疫仍适用。",TraitCategory.Wood,(wood.Mask&(1<<i))!=0);
        int oreCount=OreFamily.Count(soul);
        int mainOre=OreFamily.PopCount(OreFamily.Mask(soul.OreFlightMains)),supportOre=OreFamily.PopCount(OreFamily.Mask(support));
        Add("shared-support","共用支援",$"当前激活 {soul.SupportCapacity} 格：{SoulSupport.Breakdown(soul)}。每条已激活的支援奖励各提供1格，累加而非取最大值。所有可寄宿近战武器均可放入；不出主剑、不提供武器面板属性，但计入所属族羁绊及已实装特性。",
            "木质1/2/4/6/8种各给1格；矿石1/2/4/6/8/10材质各给1格。基础普通与已开放支援参与；已解锁奖励主槽可贡献矿材，但不能反向凑木质全套。停用取回槽不计入。后续种族可提供新奖励，不固定五格。",TraitCategory.Growth,soul.SupportCapacity>0);
        Add("collection-slots","全收集奖励","解锁左上R1、R2两个共享真实主攻剑位。木质全套或矿石圆满任一满足即可；两族同时完成仍共用2格。普通主槽仍10格，支援数量另由当前激活的各族奖励累加。",
            "已附着，12种木剑同时有效寄宿，或累计有效登记14种矿石材质。矿石登记取下不丢。",TraitCategory.Growth,soul.BonusCapacity>0);
        Add("ore-wave","百炼飞剑",$"有效矿材 {oreCount}/14（主攻{mainOre}、支援{supportOre}，重复材质合并）。每把允许参与飞剑的主剑本体首次有效命中积攒一把；铜质剑特性允许三个剑影各自再积攒一次，同一单元后续帧伤不补触发。不同主剑可同时积攒。按有效材质成阵，集齐后蓄力齐射；中断3秒或目标死亡释放现有飞剑。距目标至少170像素，每趟贯穿560像素，沿途每6帧可再次命中。基础伤害D×150%×[1＋5%×(材质数－1)]，10层再×1.5，全收集再×1.3。防御效果最多按正常100%结算，不放大敌人护甲。累计登记 {soul.OreCollected}/14种。",
            "至少1种有效矿材解锁；切换未解除时仅前4把有效主剑计入矿材并触发飞剑，解除后恢复全部。普通特性和支援仍受原有切换限制。",TraitCategory.Ore,soul.Active.Any(i=>OreFamily.TriggerEligible(soul,i.type)));
        Add("ore-tier-2","锻金 · 淬刃","每把矿石主剑自身伤害＋5%，不按材质数量叠乘，不强化其他武器。",
            "当前有效不同矿材达到2种。",TraitCategory.Ore,oreCount>=2 && oreCount<4);
        Add("ore-tier-4","锻金 · 百锻","2层加成提升100%：每把矿石主剑自身伤害＋10%，替代原＋5%。",
            "当前有效不同矿材达到4种。",TraitCategory.Ore,oreCount>=4);
        Add("ore-tier-6","锻金 · 回锋","飞剑完成一次贯穿后沿原路径折返，再贯穿攻击一次；每趟保留6帧命中间隔。",
            "当前有效不同矿材达到6种。",TraitCategory.Ore,oreCount>=6);
        Add("ore-tier-8","锻金 · 铸魂","每把飞剑首次有效命中可直接触发其矿剑已有特效；肉后六矿保留原触发冷却和治疗预算，飞剑效果与主剑分别计数。帧伤和折返不重复触发、不递归积攒飞剑。铜、锡的成阵联动独立生效，不要求8材质；其他肉前矿材的新独有机制尚未实装。",
            "当前有效不同矿材达到8种。",TraitCategory.Ore,oreCount>=8);
        Add("ore-copper-echo","铜质剑 · 叠影入阵","本体及三个实体剑影各有一次百炼积攒资格；伤害与共享命中间隔仍按断锋诅咒及原有规则结算，不把外观拖尾当成额外攻击。短剑和阔剑共享此材质特性。",
            "有效普通主槽或支援槽中存在铜质剑，需要主攻剑；遵守切换诅咒的普通特性限制。",TraitCategory.Ore,OreSwordTraits.EchoStacking(soul));
        Add("ore-tin-growth","锡质剑 · 百炼增辉",$"同一目标同一阵每多积攒一把，阵中每把飞剑伤害+{OreStackGrowth.DamagePerExtraStack:P0}；第一把不额外加成，最多{OreStackGrowth.MaxStacks}层（+{OreStackGrowth.DamagePerExtraStack*(OreStackGrowth.MaxStacks-1):P0}）。各剑保留自身伤害快照，亮度随层数平滑提高；齐射或超时释放后保留该阵最终倍率，新阵从1层开始。短剑和阔剑共享此材质特性。",
            "有效普通主槽或支援槽中存在锡质剑，需要主攻剑；遵守切换诅咒的普通特性限制。",TraitCategory.Ore,OreSwordTraits.StackDamage(soul));
        Add("ore-tier-10","锻金 · 共鸣","飞剑伤害与飞行速度各提高50%。每把允许参与的主剑首次命中独立积攒一把，不设共用冷却；切换未解除时仅前4把主剑参与；矿石支援已接入通用机制，1种即计数和解锁飞剑，支援仍受切换诅咒限制。",
            "当前有效不同矿材达到10种，主攻与通用支援合并去重；暂停槽不计入。",TraitCategory.Ore,oreCount>=10);
        string registered=string.Join("、",Enumerable.Range(0,14).Where(i=>(soul.OreCollectionMask&(1<<i))!=0).Select(i=>OreFamily.Names[i]));
        string missing=string.Join("、",Enumerable.Range(0,14).Where(i=>(soul.OreCollectionMask&(1<<i))==0).Select(i=>OreFamily.Names[i]));
        Add("ore-mastery","百炼圆满",soul.OreMastered?"累计登记14种材质：保留飞剑伤害＋30%、距离＋50%至840像素，以及2个共享奖励剑位。完整齐射额外召来天铸巨剑：30帧预兆后从天而降，基础取完成齐射的来源主剑12倍伤害，并继承圆满30%和10层50%增伤；每目标主体一次重击，同剑魂5秒冷却。中断释放的部分波次不召巨剑。":
            $"累计登记 {soul.OreCollected}/14种。已登记：{(registered.Length>0?registered:"无")}。尚缺：{missing}。全收集后飞剑伤害+30%、距离+50%，解锁2个共享奖励剑位，并在完整齐射时召来天铸巨剑（来源主剑基础12倍伤害，同魂5秒冷却）。",
            "同一枚已附着剑魂，累计将14种矿石材质放入有效主攻或支援槽；短剑/阔剑同材质算一种。暂停槽、旧扩展取回区、未附着预览不登记。",TraitCategory.Ore,
            soul.OreMastered&&main.Any(i=>OreFamily.TriggerEligible(soul,i.type)));
        foreach(int type in HardOreTypes.Types)
        {
            int kind=HardOreTypes.Index(type);
            Add("ore-trait-"+kind,HardOreTypes.Names[kind],HardOreTypes.Description(type),
                "放入有效主槽，或作为有效支援附加到主攻剑；需要至少一把有效主攻剑，按各来源独立计数和取伤害。",TraitCategory.Ore,
                hasMain&&main.Any(i=>HardOreTypes.Enabled(soul,i.type,(HardOreKind)kind)));
        }
        int heavenCount=HolyHeavenFamily.Count(soul);
        Add("heaven-star","神圣天堂 · 落星",$"有效寄宿 {heavenCount} 种神圣或星辰武器。主剑命中后召唤落星，基础伤害1000；撞击时对120像素内其他敌人造成同等基础伤害，同一剑魂间隔5秒。落星受方块阻挡。",
            "成员：珍珠木剑、星怒、神圣剑、真神圣剑、星光、狂星之怒、日耀喷发剑、破晓之光。至少一把处于有效普通或支援槽，且有有效主攻剑；珍珠木剑同时保留木质族身份。",TraitCategory.Heaven,HolyHeavenFamily.Enabled(soul));
        Add("living-armament","剑灵 · 唤兵（暂名）",$"有效族成员 {LivingArmamentFamily.Count(soul)} 种。持有剑魂时召唤有效出战武器，自动追击敌人并使用对应武器攻击；保留现有飞剑及其他种族效果。",
            "暂定成员：附魔剑、飞刀、波涌之刃、无头骑士剑。至少一把在有效主槽或支援槽，且有有效出战主剑。",TraitCategory.Living,LivingArmamentFamily.Enabled(soul));
        // 飞刃族阶梯：名册前四把各带一层特殊效果，越靠前越先拿到。
        Add("flying-seed","蓝相位剑 · 飞刃启程","激活飞刃族的掷刃攻击；不寄宿它，族内其他剑只按普通挥砍结算。",
            "寄宿蓝相位剑（名册第1把）于有效普通或支援槽。",TraitCategory.Flying,FlyingBladeFamily.Activated(soul));
        Add("flying-tally","红相位剑 · 飞刃叠势",$"族内每多一把有效武器，飞刃伤害 +{FlyingBladeFamily.StepPercent}%，最多计 {FlyingBladeFamily.StepCap} 把。",
            "寄宿红相位剑（名册第2把）于有效普通或支援槽。",TraitCategory.Flying,FlyingBladeFamily.Tally(soul));
        Add("flying-pass","绿相位剑 · 飞刃通行","允许非本族主剑也使用飞刃攻击方式；飞刃仍沿用该主剑自己的贴图与伤害，用于混族配装。",
            "寄宿绿相位剑（名册第3把）于有效普通或支援槽。",TraitCategory.Flying,FlyingBladeFamily.Pass(soul));
        Add("flying-sigil","紫相位剑 · 飞刃印记","族特殊效果生效：当前为收回期的飞刃保留一半伤害。",
            "寄宿紫相位剑（名册第4把）于有效普通或支援槽。",TraitCategory.Flying,FlyingBladeFamily.Sigil(soul));
        var families=new[]{(BladeFamily.Swift,ItemID.CopperShortsword),(BladeFamily.Solid,ItemID.BoneSword),
            (BladeFamily.Frost,ItemID.IceBlade),(BladeFamily.Nature,ItemID.BladeofGrass),(BladeFamily.Arcane,ItemID.EnchantedSword)};
        foreach(var(family,type) in families)
            Add("family-"+family,WeaponFamilies.Name(type),WeaponFamilies.Description(type),
                "对应类别的剑处于有效主槽，或提供已登记的有效支援；暂停槽与未附着预览均不生效。",TraitCategory.Sword,
                hasMain&&equipped.Any(i=>WeaponFamilies.Of(i.type)==family));
        Add("copper-close","铜短剑 · 初心","该来源主剑或铜短剑支援，在距持有者180像素内命中时最终伤害提高20%。",
            "铜短剑处于有效主槽或有效支援槽，且有主攻剑。",TraitCategory.Sword,hasMain&&equipped.Any(i=>i.type==ItemID.CopperShortsword));
        var specials=new[]{
            (ItemID.BeeKeeper,"蜂群","飞行途中尝试释放3只蜜蜂，每只取来源主剑D的20%。"),
            (ItemID.Seedler,"种子弹","飞行途中释放种子弹，取来源主剑D的50%。"),
            (ItemID.TheHorsemansBlade,"南瓜追击","飞行途中释放追踪南瓜，取来源主剑D的50%。"),
            (ItemID.InfluxWaver,"波涌剑气","飞行途中释放来源武器剑气，取来源主剑D的50%。"),
            (ItemID.TerraBlade,"泰拉剑气","飞行途中释放泰拉剑气，取来源主剑D的50%。"),
            (ItemID.StarWrath,"狂星坠落","飞行途中释放3颗落星，每颗取来源主剑D的40%。"),
            (ItemID.Meowmere,"彩虹猫","飞行途中释放彩虹猫弹幕，取来源主剑D的50%。")};
        foreach(var(type,name,description) in specials)
        {
            // 与原版发射模板的检查一致：没有发射弹幕时，不把原版命中行为宣传为剑魂特效。
            if(Terraria.ID.ContentSamples.ItemsByType[type].shoot<=ProjectileID.None)continue;
            Add("special-"+type,name,description,
                "对应天顶材料剑处于有效主槽或支援槽；支援触发独立45帧冷却，使用实际主攻来源伤害。",TraitCategory.Sword,
                hasMain&&equipped.Any(i=>i.type==type));
        }
        Add("attachment","祝福 · 附着","残魂可以稳定依附武器；第一普通剑位永久可用。",
            "白天地表使用凝胶信物，与友善史莱姆王对话接受祝福。",TraitCategory.Growth,soul.Attached);
        foreach(SoulCurse curse in Enum.GetValues<SoulCurse>())
        {
            var stage=soul.Curse(curse);
            Add("curse-"+curse,SoulCurses.Names[(int)curse]+" · "+SoulCurses.StageName(stage),SoulCurses.Description(soul,curse),
                SoulCurses.Requirement(soul,curse),TraitCategory.Growth,soul.Attached);
        }
        Add("reborn","天顶复生","复生永久保留，效能110%，可自由更换寄宿武器。",
            "完成全部主线铭刻、六枚魂芯归位，基础普通槽同时寄宿十把天顶材料武器。",TraitCategory.Growth,soul.Attached&&soul.Awakened);
        return result;
    }
}
