using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// Vanilla ItemCheck owns the clock. This class only selects the next host.
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
        !Player.GetModPlayer<MartialPlayer>().SuppressNative(swinging) &&
        hosts.Length>0 && swinging.Active.SequenceEqual(hosts) && hosts.Select(i=>i.type).SequenceEqual(types);

    public void FireHost()
    {
        if(!Valid()){Cancel();return;}
        if(budget>3)return; // Evenly spaced extra nodes are handled below.
        EmitNext();
    }
    private void EmitNext()
    {
        if(lastAnimation==Player.itemAnimation)return;
        lastAnimation=Player.itemAnimation;
        // Sparse loadouts use each host once; spare native firing opportunities
        // remain empty. The next gesture resumes the same persistent cursor.
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
