using Terraria;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithPrototypeCheck;

internal static class BossUnlockUiCheck
{
    internal static void Run(Action<bool,string> check)
    {
        var soul=(SoulItem)new Item(ModContent.ItemType<SoulItem>()).ModItem;soul.Attached=true;soul.LegacyReleased=0;
        foreach(SoulCurse curse in Enum.GetValues<SoulCurse>())
        {
            var group=SoulCurses.Groups[(int)curse];
            // 检查各组的所有击败组合，包含先打后列 Boss，防止把展示顺序写成解锁顺序。
            for(int mask=0;mask<(1<<group.Length);mask++)
            {
                soul.Seals=group.Where((_,i)=>(mask&(1<<i))!=0).Aggregate(0,(a,b)=>a|b);
                using var before=new MemoryStream();soul.NetSend(new BinaryWriter(before));
                var view=SoulBossUnlocks.Read(soul,curse);
                int count=System.Numerics.BitOperations.PopCount((uint)mask);
                check(view.Count==count&&view.Targets.Count(t=>t.Recorded)==count,"Boss 亮暗只取个人铭刻，任意击败顺序正确");
                check(view.Rewards.Count(r=>r.Unlocked)==count&&view.Rewards.All(r=>r.Active==r.Unlocked),"阶段收益按累计数量点亮");
                check(view.Rewards.All(r=>r.Description==SoulCurses.StageDescription(soul,curse,r.Stage)),"未来奖励与当前说明共用同一来源");
                using var after=new MemoryStream();soul.NetSend(new BinaryWriter(after));
                check(before.ToArray().SequenceEqual(after.ToArray()),"预览未来奖励不修改剑魂数据");
            }
            soul.Seals=0;soul.LegacyReleased=1<<(int)curse;
            var legacy=SoulBossUnlocks.Read(soul,curse);
            check(legacy.Targets.All(t=>!t.Recorded)&&legacy.Rewards.Last().Unlocked==false&&legacy.Rewards.Any(r=>r.Legacy),"旧档解咒不伪造击败头像，不提前点亮祝福");
            soul.LegacyReleased=0;
        }
        soul.Seals=SoulCurses.Retinazer;
        var twins=SoulBossUnlocks.Read(soul,SoulCurse.Solitude);
        check(twins.Count==0&&twins.Targets[1].RecordedParts==1&&!twins.Targets[1].Recorded,"双子只击败一只时显示部分完成，不增加完整铭刻");
        soul.Seals=SoulCurses.All;soul.Attached=false;
        var detached=SoulBossUnlocks.Read(soul,SoulCurse.Switching);
        check(detached.Targets.All(t=>t.Recorded)&&detached.Rewards.All(r=>r.Unlocked&&!r.Active),"已获得铭刻但尚未附着时不宣称属性生效");
        check(!SoulBossUnlocks.TryCurse("ore-wave",out _)&&!SoulBossUnlocks.TryCurse("curse-99",out _),"普通特性不会误用 Boss 解锁列表");
        foreach(var animation in SoulBossPortraits.Catalog.Values)
        {
            check(animation.DurationsMs.Length>1&&animation.TotalDurationMs>0&&animation.FrameAt(0)==0,"每个 Boss 都有真实的多帧 Wiki 动画");
            check(animation.FrameAt(animation.DurationsMs[0]+.001)==1&&animation.FrameAt(animation.TotalDurationMs+.001)==0,"动画尊重 GIF 原始时长并循环");
        }
        check(SoulCurses.Groups.SelectMany(g=>g).All(s=>SoulBossPortraits.Catalog.ContainsKey(SoulBossPortraits.Key(s))),"六组十五个挑战目标都有头像资源");
    }
}
