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
    public static Rectangle LibraryBounds()=>new(20,118,Math.Clamp(Main.screenWidth/5,208,310),Math.Max(1,Main.screenHeight-176));
    private static Rectangle DomainWorkBounds()
    {var left=LibraryBounds();return new(left.Right+20,118,Math.Max(1,Main.screenWidth-left.Right-44),Math.Max(1,Main.screenHeight-176));}
    private static bool SideInspector=>DomainMode && DomainWorkBounds().Width>=790;
    private static Rectangle RingViewport
    {
        get {var b=ContentBounds();return SideInspector?new(b.X,b.Y,b.Width-300,b.Height):b;}
    }
    public static Rectangle DifficultyBounds()=>new(Math.Max(8,Main.screenWidth-298),18,222,36);
    public static int SelectedSlot=>selected;
    public static void SelectSlot(int slot)
    {if(slot>=0 && (slot<25 || editing?.VisibleSlotIds.Contains(slot)==true))selected=slot;}
    public static IEnumerable<int> LibraryItems(Player player)=>Enumerable.Range(0,50)
        .Where(i=>SoulItem.CanHost(player.inventory[i]));
    // Direct exchange reuses the exact slot validation. The outgoing sword goes
    // into the vacated inventory slot; no clone, hidden stash or extra capacity.
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
        // Sparse layers move at different speeds; the foreground sword orbit
        // stays stationary and clickable while the surrounding space drifts.
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
        Line(new(22,102),new(panel.Right-22,102),Champagne*.22f*open);
    }
    private static void DrawDomainHeader(Point mouse)
    {
        Text("ZENITH  /  天 顶 魂 域",new(24,22),Ink,1f);
        Text("众剑寄宿之所",new(26,57),Champagne,.63f);
        var r=DifficultyBounds();bool hover=r.Contains(mouse);bool legendary=SoulDifficulty.Legendary;
        RoundedBox(r,legendary?new Color(39,67,40)*.85f:new Color(30,35,46)*.9f,9);
        Line(new(r.X+12,r.Bottom-1),new(r.Right-12,r.Bottom-1),legendary?Color.LimeGreen*.5f:Champagne*.3f);
        CenterText(legendary?"传奇难度 · 已开启":"切换原版传奇难度",new(r.Center.X,r.Y+9),legendary?Color.LightGreen:Champagne,.68f);
        if(hover && Main.mouseLeft && Main.mouseLeftRelease)
        {Main.mouseLeftRelease=false;bool ok=SoulDifficulty.Toggle(out var note);DomainNote(ok,note);}
        if(hover && Main.GameUpdateCount>=feedbackUntil)
        {
            string blocked=SoulDifficulty.BlockReason();
            feedback=blocked.Length>0?blocked:SoulDifficulty.Instance.Applied?"点击恢复开启前的难度。":"大师＋For the worthy，影响整个世界的战斗与种子规则；不会重新生成地图。";
            feedbackUntil=Main.GameUpdateCount+2;
        }
        DrawNavigation(false);
    }
    private static Rectangle LibraryGridBounds()
    {var b=LibraryBounds();return new(b.X+8,b.Y+83,b.Width-16,Math.Max(1,b.Height-115));}
    public static Rectangle LibraryCardBounds(int ordinal)
    {
        var b=LibraryGridBounds();int cols=Math.Max(1,b.Width/66),step=b.Width/cols;
        return new(b.X+ordinal%cols*step,b.Y+ordinal/cols*76-libraryScroll,step-6,68);
    }
    private static void DrawLibrary(SoulItem soul)
    {
        var b=LibraryBounds();var grid=LibraryGridBounds();var p=Main.LocalPlayer;var mouse=new Point(Main.mouseX,Main.mouseY);
        RoundedBox(b,new Color(11,17,26)*.92f,14);
        Text("随身剑库",new(b.X+14,b.Y+13),Champagne,.89f);
        Text("选择剑位，再点剑直接寄宿",new(b.X+14,b.Y+42),Muted,.62f);
        var filter=new Rectangle(b.X+12,b.Y+62,b.Width-24,20);
        Text(libraryFilter==0?"全部可寄宿剑  ▾":"未寄宿的剑  ▾",filter.Location.ToVector2(),Accent,.58f);
        if(filter.Contains(mouse) && Main.mouseLeft && Main.mouseLeftRelease)
        {Main.mouseLeftRelease=false;libraryFilter=1-libraryFilter;libraryScroll=0;}
        int[] items=LibraryItems(p).Where(i=>libraryFilter==0 || !soul.Stored.Any(s=>!s.IsAir && s.type==p.inventory[i].type)).ToArray();
        int cols=Math.Max(1,grid.Width/66),height=((items.Length+cols-1)/cols)*76;
        if(b.Contains(mouse) && PlayerInput.ScrollWheelDelta!=0)libraryScroll-=PlayerInput.ScrollWheelDelta/120*76;
        libraryScroll=Math.Clamp(libraryScroll,0,Math.Max(0,height-grid.Height));
        for(int i=0;i<items.Length;i++)
        {
            var r=LibraryCardBounds(i);if(!grid.Contains(r))continue;
            var item=p.inventory[items[i]];bool hover=r.Contains(mouse),stored=soul.Stored.Any(s=>!s.IsAir && s.type==item.type);
            var tint=SoulVisuals.Tint(item.type);
            RoundedBox(r,new Color(22,29,40)*(hover?.96f:.65f),8);
            Line(new(r.X+7,r.Bottom-2),new(r.Right-7,r.Bottom-2),tint*(stored?.18f:hover?.9f:.35f));
            Main.instance.LoadItem(item.type);var texture=TextureAssets.Item[item.type].Value;
            var frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Frame();
            float scale=Math.Min(1.1f,34f/Math.Max(frame.Width,frame.Height));
            if(hover)Glow(r.Center.ToVector2(),32,tint,.23f);
            Main.spriteBatch.Draw(texture,new Vector2(r.Center.X,r.Y+23),frame,stored?Color.Gray:Color.White,0,frame.Size()*.5f,scale,SpriteEffects.None,0);
            CenterText(Fit(item.Name,r.Width-6,.46f),new(r.Center.X,r.Bottom-22),stored?Muted:Ink,.46f);
            if(hover)
            {
                HoveredSlotRequirement=item.Name+" · 伤害 "+item.damage+(stored?" · 已寄宿同种":" · 点击装入选中剑位");
                if(Main.mouseLeft && Main.mouseLeftRelease)
                {
                    Main.mouseLeftRelease=false;
                    bool ok=EquipFromInventory(p,items[i],out var error);
                    DomainNote(ok,ok?"已寄宿 · "+SlotName(selected):error);break;
                }
            }
        }
        if(items.Length==0)Paragraph("背包中暂无可寄宿剑。\n将剑放进随身背包，再打开魂域。",grid.X+8,grid.Y+24,grid.Width-16,Muted,.68f);
        Text($"{items.Length} 把 · 滚轮浏览 · 不额外存储物品",new(b.X+12,b.Bottom-24),Muted,.51f);
    }
    private static int DrawDomainReturn(Rectangle b,int y)
    {
        if(Button(new(b.X+10,y,b.Width-20,34),"取下选中剑 · 归还背包"))
        {bool ok=ReturnSelected(Main.LocalPlayer,out var error);DomainNote(ok,ok?"已归还背包":error);}
        return y+46;
    }
}
