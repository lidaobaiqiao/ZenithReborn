using System.Numerics;

namespace ZenithReborn;

public readonly record struct SoulBossTarget(int Seal,string Name,int RecordedParts,int TotalParts)
{
    public bool Recorded=>RecordedParts==TotalParts;
    public string Status=>Recorded?"已击败 · 已铭刻":RecordedParts>0?$"已击败 {RecordedParts}/{TotalParts} · 未完成":"未击败";
}
public readonly record struct SoulBossReward(int Count,CurseStage Stage,string Description,bool Unlocked,bool Current,bool Active,bool Legacy)
{
    public string Status=>Legacy?"旧档解咒保留":!Unlocked?"未解锁":!Active?"已解锁 · 待附着":Current?"当前生效":"已达成";
}
public sealed record SoulBossUnlockView(SoulCurse Curse,int Count,int Total,bool Attached,SoulBossTarget[] Targets,SoulBossReward[] Rewards);

// 只读的展示模型：读取这把剑魂的个人铭刻，不读取世界 Boss 进度，也不补写存档。
public static class SoulBossUnlocks
{
    public static bool TryCurse(string key,out SoulCurse curse)
    {
        curse=default;
        return key.StartsWith("curse-",StringComparison.Ordinal)&&Enum.TryParse(key[6..],out curse)&&Enum.IsDefined(curse);
    }
    public static SoulBossUnlockView Read(SoulItem soul,SoulCurse curse)
    {
        int index=(int)curse;var group=SoulCurses.Groups[index];int count=SoulCurses.Count(soul,curse);
        var current=soul.Curse(curse);
        var targets=group.Select((bit,i)=>new SoulBossTarget(bit,SoulCurses.BossNames[index][i],
            BitOperations.PopCount((uint)(soul.Seals&bit)),BitOperations.PopCount((uint)bit))).ToArray();
        var rewards=Enumerable.Range(1,group.Length).Select(n=>
        {
            var stage=SoulCurses.StageForCount(n,group.Length);
            bool legacy=count<n&&stage<=CurseStage.Released&&(soul.LegacyReleased&(1<<index))!=0;
            bool unlocked=count>=n||legacy;
            return new SoulBossReward(n,stage,SoulCurses.StageDescription(soul,curse,stage),unlocked,
                stage==current,unlocked&&soul.Attached,legacy);
        }).ToArray();
        return new(curse,count,group.Length,soul.Attached,targets,rewards);
    }
}
