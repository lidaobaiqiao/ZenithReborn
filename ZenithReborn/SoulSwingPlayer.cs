using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// 原版物品检查控制发射时序，本类只选择下一把寄宿武器。
public class SoulSwingPlayer : ModPlayer
{
    private SoulItem swinging;
    private Item[] hosts=[];
    private int[] types=[];
    private string cursorSoul;
    private int cursor, shots, lastAnimation;
    private int budget;

    public void Begin(SoulItem soul)
    {
        swinging=soul;hosts=soul.Active.ToArray();types=hosts.Select(i=>i.type).ToArray();
        if(cursorSoul!=soul.SoulId){cursorSoul=soul.SoulId;cursor=0;}
        if(hosts.Length>0)cursor%=hosts.Length;
        shots=0;lastAnimation=-1;
        budget=Math.Min(hosts.Length,SoulCurses.Blades(soul));
        if(soul.Curse(SoulCurse.Sheathing)<CurseStage.Released)
            foreach(var p in Main.ActiveProjectiles)
                if(p.owner==Player.whoAmI && p.ModProjectile is SoulBlade blade &&
                    p.GetGlobalProjectile<SoulSource>().SoulId==soul.SoulId)blade.Retire();
        Player.GetModPlayer<SoulPlayer>().Cycle(hosts.Length);
    }

    private bool Valid()=>swinging!=null && !Player.dead && Player.HeldItem.ModItem==swinging &&
        swinging.Attached && Player.altFunctionUse!=2 && Player.itemAnimation>0 && !SoulUI.IsEditing(swinging) &&
        hosts.Length>0 && swinging.Active.SequenceEqual(hosts) && hosts.Select(i=>i.type).SequenceEqual(types);

    public void FireHost()
    {
        if(!Valid()){Cancel();return;}
        if(budget>3)return; // 均匀分布的额外发射时机由下方处理。
        EmitNext();
    }
    private void EmitNext()
    {
        if(lastAnimation==Player.itemAnimation)return;
        lastAnimation=Player.itemAnimation;
        // 寄宿数量较少时，每把剑只发射一次，多余时机留空；
        // 下一轮从保留的游标位置继续。
        if(shots>=budget)return;
        shots++;
        swinging.EmitBlade(Player,hosts[cursor],cursor,hosts.Length);
        cursor=(cursor+1)%hosts.Length;
    }

    public override void PostItemCheck()
    {
        if(swinging==null)return;
        if(!Valid()){Cancel();return;}
        if(budget<=3)return;
        int nodes=Math.Min(budget,Player.itemAnimationMax);
        int elapsed=Player.itemAnimationMax-Player.itemAnimation;
        int due=Math.Min(nodes,1+elapsed*nodes/Math.Max(1,Player.itemAnimationMax));
        if(shots<due)EmitNext();
    }
    private void Cancel(){swinging=null;hosts=[];types=[];}
    public override void UpdateDead(){Cancel();cursor=0;cursorSoul=null;}
    public override void OnEnterWorld(){Cancel();cursor=0;cursorSoul=null;}
}
