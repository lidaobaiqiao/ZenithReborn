using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// One state per wielder. Only real attack slots enter this sequence; support
// effects still ride on each blade through the existing SoulBlade logic.
public class SoulSwingPlayer : ModPlayer
{
    private SoulItem swinging;
    private Item[] hosts = [];
    private int[] types = [];
    private string cursorSoul;
    private int cursor, lastPhase;
    private bool pending;

    public void Begin(SoulItem soul)
    {
        swinging = soul;
        hosts = soul.Active.ToArray();
        types = hosts.Select(i => i.type).ToArray();
        if (cursorSoul != soul.SoulId) { cursorSoul = soul.SoulId; cursor = 0; }
        if (hosts.Length > 0) cursor %= hosts.Length;
        lastPhase = 0;
        pending = true;
    }

    // ItemCheck decrements the animation before PostItemCheck; the start frame
    // still has the full timer. Count that frame so the fifth node occurs on
    // the final visible frame (timer=1), before auto-reuse starts the next swing.
    public static int Phase(Player player) => player.itemAnimationMax <= 0 ? 0 :
        Math.Clamp((player.itemAnimationMax - player.itemAnimation + 1) * 5 / player.itemAnimationMax, 0, 5);

    public override void PostItemCheck()
    {
        if (swinging == null) return;
        if (Player.dead || Player.HeldItem.ModItem != swinging || !swinging.Attached ||
            Player.altFunctionUse == 2 || Player.itemAnimation <= 0 || SoulUI.IsEditing(swinging))
        { Cancel(); return; }
        var current = swinging.Active.ToArray();
        if (hosts.Length == 0 || current.Length != hosts.Length ||
            current.Where((item, i) => !ReferenceEquals(item, hosts[i]) || item.type != types[i]).Any())
        { Cancel(); return; }
        if (pending)
        {
            // Five separate release frames remain possible even at extreme
            // attack speed. Apply after vanilla calculates animation duration.
            if (Player.itemAnimationMax < 5)
            {
                Player.itemAnimation = Player.itemAnimationMax = 5;
                Player.itemTime = Math.Max(Player.itemTime, 5);
            }
            Player.GetModPlayer<SoulPlayer>().Cycle(hosts.Length);
            pending = false;
        }
        int phase = Phase(Player);
        if (phase <= lastPhase) return;
        lastPhase = phase;
        // Never catch up by spawning several swords on one frame. Normal
        // animation ticks visit every node; a skipped node stays empty.
        if (phase > Math.Min(5, hosts.Length)) return;
        swinging.EmitBlade(Player, hosts[cursor], cursor, hosts.Length);
        cursor = (cursor + 1) % hosts.Length;
    }

    private void Cancel() { swinging = null; hosts = []; types = []; pending = false; }
    public override void UpdateDead() { Cancel(); cursor = 0; cursorSoul = null; }
    public override void OnEnterWorld() { Cancel(); cursor = 0; cursorSoul = null; }
}
