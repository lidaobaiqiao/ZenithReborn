using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;

namespace ZenithReborn;

public partial class SoulUI
{
    public static bool DomainMode { get; set; }=true;
    private static int libraryScroll,libraryFilter;
    private static Rectangle DomainWorkBounds()
    {
        if(page==0)return ArmoryLayout.Workspace;
        if(page!=0&&page!=RecoveryPage)
        {
            int top=Main.screenWidth>=900?80:118;
            return new(24,top,Math.Max(1,Main.screenWidth-48),Math.Max(1,Main.screenHeight-top-50));
        }
        var left=LibraryBounds();return new(left.Right+20,118,Math.Max(1,Main.screenWidth-left.Right-44),Math.Max(1,Main.screenHeight-176));
    }
    private static bool SideInspector=>DomainMode && DomainWorkBounds().Width>=790;
    private static Rectangle RingViewport
    {
        get {var b=ContentBounds();return SideInspector?new(b.X,b.Y,b.Width-300,b.Height):b;}
    }
    public static Rectangle DifficultyBounds()=>DomainMode&&page==0?new(Main.screenWidth-(Main.screenWidth<700?146:250),20,Main.screenWidth<700?90:200,30):new(Math.Max(8,Main.screenWidth-298),18,222,36);
    public static int SelectedSlot=>selected;
    public static void SelectSlot(int slot)
    {
        if(slot<0 || (slot>=25 && editing?.VisibleSlotIds.Contains(slot)!=true))return;
        // 点击剑阵是查看当前剑位，即使再次点击同一个剑位，也应退出剑库候选预览。
        bool leavingPreview=DomainMode&&page==0&&candidateIndex>=0;
        if(DomainMode&&page==0)ClearArmoryCandidate();
        if(selected!=slot||leavingPreview){armoryFocusAt=UiTime;armoryDetailScroll=0;armoryPreview=null;}
        selected=slot;
    }
    public static IEnumerable<int> LibraryItems(Player player)=>Enumerable.Range(0,50)
        .Where(i=>SoulItem.CanHost(player.inventory[i]));
    // 直接交换复用原有剑位校验，卸下的剑进入腾出的背包格，不复制或增加容量。
    public static bool EquipFromInventory(Player player,int inventorySlot,out string error)
    {
        error="";
        if(editing==null || player!=Main.LocalPlayer || !player.inventory.Any(i=>ReferenceEquals(i.ModItem,editing)))
        {error="剑魂已不在背包中。";return false;}
        if(!Main.mouseItem.IsAir){error="请先放下鼠标上的物品。";return false;}
        if(inventorySlot<0 || inventorySlot>=50 || !SoulItem.CanHost(player.inventory[inventorySlot]))
        {error="这把剑已不在原背包位置。";return false;}
        if(!editing.TrySwapHost(selected,ref player.inventory[inventorySlot],out error))return false;
        SyncInventory(player,inventorySlot);Sync(editing);return true;
    }
    public static bool ReturnSelected(Player player,out string error)
    {
        error="";
        if(editing==null || player!=Main.LocalPlayer || !player.inventory.Any(i=>ReferenceEquals(i.ModItem,editing)))
        {error="剑魂已不在背包中。";return false;}
        if(!Main.mouseItem.IsAir){error="请先放下鼠标上的物品。";return false;}
        if(editing.ItemAt(selected).IsAir){error="这个剑位是空的。";return false;}
        int empty=Enumerable.Range(0,50).FirstOrDefault(i=>player.inventory[i].IsAir,-1);
        if(empty<0){error="背包已满；可点击另一把剑直接交换。";return false;}
        if(!editing.TrySwapHost(selected,ref player.inventory[empty],out error))return false;
        SyncInventory(player,empty);Sync(editing);return true;
    }
    private static void SyncInventory(Player player,int slot)
    {if(Main.netMode==NetmodeID.MultiplayerClient)NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,player.whoAmI,slot);}
    private static void DomainNote(bool success,string message)
    {feedback=message;feedbackUntil=Main.GameUpdateCount+240;if(success)SoundEngine.PlaySound(SoundID.Grab);}
    private static void DrawDomainBackdrop(Rectangle panel)
    {
        EnsureEffects();float t=(float)(UiTime%3600),open=Ease((float)(UiTime-openedAt)/.55f);
        Box(panel,new Color(5,7,13));
        // 稀疏背景以不同速度漂移；前景剑环保持固定，保证点击位置稳定。
        Vector2 mouse=new(Main.mouseX/(float)Math.Max(1,panel.Width)-.5f,Main.mouseY/(float)Math.Max(1,panel.Height)-.5f);
        Glow(new(panel.Width*.60f,panel.Height*.42f),Math.Max(panel.Width,panel.Height)*.68f,new Color(38,70,94),.32f*open);
        Glow(new(panel.Width*.83f,panel.Height*.78f),panel.Width*.30f,new Color(62,42,85),.20f*open);
        for(int i=0;i<116;i++)
        {
            float layer=.4f+i%3*.32f;
            float x=(i*173.37f+t*(1+i%3)*.75f)%panel.Width;
            float y=(i*89.73f+i*i*.31f)%panel.Height;
            var at=new Vector2(x,y)-mouse*(4+12*layer);
            float flicker=.3f+.25f*MathF.Sin(t*(.4f+i%4*.2f)+i);
            Glow(at,2+i%3,new Color(151,188,211),flicker*open);
        }
        float horizon=panel.Height*.72f;
        for(int i=0;i<8;i++)
        {
            float depth=(i+t*.045f)%8/8f;
            float y=horizon+depth*depth*(panel.Height-horizon);
            Line(new(0,y),new(panel.Width,y),Accent*(.015f+depth*.025f));
        }
        for(int i=-7;i<=7;i++)Line(new(panel.Width*.62f,horizon),new(panel.Width*.62f+i*panel.Width*.16f,panel.Height),Accent*.028f);
        int divider=page!=0&&page!=RecoveryPage?ContentBounds().Y-12:102;
        Line(new(22,divider),new(panel.Right-22,divider),Champagne*.22f*open);
    }
    private static void DrawDomainHeader(Point mouse)
    {
        if(page==0){DrawArmoryHeader(mouse);return;}
        if(page!=0&&page!=RecoveryPage)
        {
            Text("ZENITH  /  天 顶 魂 域",new(24,23),Ink,.84f);
            DrawNavigation(false);return;
        }
        Text("ZENITH  /  天 顶 魂 域",new(24,22),Ink,1f);
        Text("众剑寄宿之所",new(26,57),Champagne,.63f);
        DrawDomainDifficulty(mouse,false);
        DrawNavigation(false);
    }
    private static void DrawDomainDifficulty(Point mouse,bool compact)
    {
        var r=DifficultyBounds();bool hover=r.Contains(mouse);bool legendary=SoulDifficulty.Legendary;
        RoundedBox(r,legendary?new Color(39,67,40)*.85f:new Color(30,35,46)*.9f,9);
        Line(new(r.X+12,r.Bottom-1),new(r.Right-12,r.Bottom-1),legendary?Color.LimeGreen*.5f:Champagne*.3f);
        CenterText(compact?legendary?"传奇 · 开":"世界难度":legendary?"传奇难度 · 已开启":"切换原版传奇难度",new(r.Center.X,r.Y+9),legendary?Color.LightGreen:Champagne,.68f);
        if(hover && Main.mouseLeft && Main.mouseLeftRelease)
        {Main.mouseLeftRelease=false;bool ok=SoulDifficulty.Toggle(out var note);DomainNote(ok,note);}
        if(hover && Main.GameUpdateCount>=feedbackUntil)
        {
            string blocked=SoulDifficulty.BlockReason();
            feedback=blocked.Length>0?blocked:SoulDifficulty.Instance.Applied?"点击恢复开启前的难度。":"大师＋For the worthy，影响整个世界的战斗与种子规则；不会重新生成地图。";
            feedbackUntil=Main.GameUpdateCount+2;
        }
    }
    private static int DrawDomainReturn(Rectangle b,int y)
    {
        if(Button(new(b.X+10,y,b.Width-20,34),"取下选中剑 · 归还背包"))
        {bool ok=ReturnSelected(Main.LocalPlayer,out var error);DomainNote(ok,ok?"已归还背包":error);}
        return y+46;
    }
}
