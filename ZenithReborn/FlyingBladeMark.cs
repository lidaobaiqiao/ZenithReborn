using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace ZenithReborn;

// 飞刃印记：直击与旋转tick各叠1层，持续3秒；满5层在目标周围爆发（该剑直击的200%，96像素范围）后清零。
// 初版族内消化，跨族消费（落星/剑影读取印记）为候选方向，暂不实现。数值均为暂定初值。
public static class FlyingBladeMark
{
    public const int BurstLayers=5;
    public const int BurstRange=96;
    public const int MarkDuration=180;    // 3秒
    public const float BurstDamage=2f;
    private sealed record Mark(uint Expire,int Layers,int LastDamage);
    private static readonly Dictionary<long,Mark> Marks=[]; // key: owner*10000 + npcWhoAmI
    public static void Stack(int owner,int npcWho,int damageDone)
    {
        if(Main.netMode==NetmodeID.Server || damageDone<=0)return;
        var npc=Main.npc[npcWho];
        if(npc==null || !npc.active || npc.friendly || npc.life<=0 || !HardOrePlayer.ValidTarget(npc,false))return;
        long key=(long)owner*10000+npcWho;
        uint now=(uint)Main.GameUpdateCount;
        var mark=Marks.TryGetValue(key,out var found) && found.Expire>now
            ?found with {Layers=found.Layers+1,LastDamage=damageDone}
            :new Mark(now+MarkDuration,1,damageDone);
        if(mark.Layers>=BurstLayers)
        {
            Marks.Remove(key);
            Burst(owner,npc,mark.LastDamage);
            return;
        }
        Marks[key]=mark;
    }
    private static void Burst(int owner,NPC source,int lastDamage)
    {
        int burst=Math.Max(1,(int)(lastDamage*BurstDamage));
        var center=source.Center;
        foreach(var target in Main.ActiveNPCs)
        {
            if(!HardOrePlayer.ValidTarget(target,false) || Vector2.Distance(target.Center,center)>BurstRange)continue;
            target.SimpleStrikeNPC(burst,target.position.X<center.X?-1:1);
        }
    }
    public static int Layers(int owner,int npcWho)
    {
        var key=(long)owner*10000+npcWho;
        return Marks.TryGetValue(key,out var mark) && mark.Expire>(uint)Main.GameUpdateCount?mark.Layers:0;
    }
    // 每帧清理过期印记与已死亡目标的记录。
    public static void Prune()
    {
        uint now=(uint)Main.GameUpdateCount;
        long[] expired=[.. Marks.Where(pair=>pair.Value.Expire<=now || !Main.npc[pair.Key%10000].active).Select(pair=>pair.Key)];
        foreach(long key in expired)Marks.Remove(key);
    }
}
