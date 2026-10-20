using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ZenithReborn;

public class SoulUI : ModSystem
{
    private static SoulItem editing;
    private static int page, selected, scroll, contentHeight;
    private static int TabCount => editing?.HasLegacyExpansion == true ? 4 : 3;
    private static string feedback = "";
    private static ulong feedbackUntil;
    private static RasterizerState clipState;
    private static readonly Color Ink = new(218, 227, 239), Muted = new(148, 166, 190), Accent = new(121, 204, 234);
    public static SoulItem EditingSoul => editing;
    public static string HoveredSlotRequirement { get; private set; } = "";
    public static bool IsEditing(SoulItem soul) => ReferenceEquals(editing, soul);
    public static void Open(SoulItem soul, bool progress = false)
    {
        if (!IsEditing(soul)) Toggle(soul);
        SelectPage(progress ? 2 : 0);
    }
    public static void SelectPage(int value)
    {
        page = Math.Clamp(value, 0, TabCount-1); scroll = 0; contentHeight = 0;
        if (page == 3 && selected < 17) selected = 17;
        if (page != 3 && selected >= 17) selected = 0;
        HoveredSlotRequirement = "";
    }
    public static void Toggle(SoulItem soul)
    {
        HoveredSlotRequirement = "";
        if (ReferenceEquals(editing, soul)) { Sync(editing); editing = null; return; }
        if (editing != null) Sync(editing);
        editing = soul; selected = 0; feedback = ""; SelectPage(0); Main.playerInventory = true;
        SoundEngine.PlaySound(SoundID.MenuOpen);
    }
    public static void Sync(SoulItem soul)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient) return;
        Player player = Main.LocalPlayer;
        for (int i = 0; i < 58; i++)
            if (ReferenceEquals(player.inventory[i].ModItem, soul))
                NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, i);
    }
    public override void Unload()
    {
        editing = null; HoveredSlotRequirement = ""; SoulInput.Reset();
        clipState?.Dispose(); clipState = null;
    }
    public override void OnWorldUnload()
    {
        editing = null; HoveredSlotRequirement = ""; SoulInput.Reset(); SelectPage(0); HeroPrefixCompat.Clear();
    }
    public override void PostUpdateInput()
    {
        HoveredSlotRequirement = "";
        if (Main.gameMenu || Main.dedServ) return;
        if (ZenithReborn.OpenSoul.JustPressed && !Main.drawingPlayerChat)
        {
            if (editing != null) Toggle(editing);
            else
            {
            var soul = Main.LocalPlayer.HeldItem.ModItem as SoulItem ??
                Main.LocalPlayer.inventory.Select(i => i.ModItem).OfType<SoulItem>().FirstOrDefault();
            if (soul != null) Toggle(soul);
            }
        }
        if (editing != null && (!Main.playerInventory || Main.LocalPlayer.dead ||
            !Main.LocalPlayer.inventory.Any(i => ReferenceEquals(i.ModItem, editing))))
        { Sync(editing); editing = null; }
        SoulInput.Capture(editing != null && !Main.hideUI && !Main.mapFullscreen &&
            ContainsScreenPoint(Main.mouseX, Main.mouseY, Main.screenWidth, Main.screenHeight, Main.UIScale));
    }
    public override void PostDrawInterface(SpriteBatch spriteBatch) => SoulInput.RestoreAfterInterface();
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        // Vanilla draws inventory slots before our panel. Keep input hidden from
        // every background layer, not just the mouse-text layer at the end.
        layers.Insert(0, new LegacyGameInterfaceLayer("ZenithReborn: Input Shield",
            () =>
            {
                // An item can open the panel later in the update than our hook.
                if (!SoulInput.Blocking && editing != null && PanelBounds().Contains(Main.mouseX,Main.mouseY))
                    SoulInput.Capture(true);
                SoulInput.MaskBackground(); return true;
            }, InterfaceScaleType.UI));
        int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
        layers.Insert(index < 0 ? layers.Count : index, new LegacyGameInterfaceLayer("ZenithReborn: Soul",
            () =>
            {
                SoulInput.BeginSoulLayer();
                try { if (editing != null) Draw(); }
                finally { SoulInput.EndSoulLayer(); }
                return true;
            }, InterfaceScaleType.UI));
    }
    private static void Box(Rectangle r, Color color) => Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, r, color);
    private static void Text(string text, Vector2 at, Color color, float scale = .78f) =>
        Utils.DrawBorderString(Main.spriteBatch, text, at, color, scale);
    private static void Border(Rectangle r, Color color)
    {
        Box(new(r.X,r.Y,r.Width,1),color); Box(new(r.X,r.Bottom-1,r.Width,1),color);
        Box(new(r.X,r.Y,1,r.Height),color); Box(new(r.Right-1,r.Y,1,r.Height),color);
    }
    // The interface layer already transforms screen and mouse into UI units.
    // Do not apply UIScale a second time to either layout or hit testing.
    public static Rectangle PanelBounds() => PanelBounds(Main.screenWidth, Main.screenHeight);
    private static Rectangle PanelBounds(int width, int height)
    {
        int w = Math.Min(760, Math.Max(1,width-24));
        int h = Math.Min(650, Math.Max(1,height-24));
        return new((width-w)/2, Math.Min(110, (height-h)/2), w, h);
    }
    // PostUpdateInput is still in physical screen units; drawing uses UI units.
    public static bool ContainsScreenPoint(int x,int y,int width,int height,float scale) =>
        PanelBounds((int)(width/scale),(int)(height/scale)).Contains((int)(x/scale),(int)(y/scale));
    public static Rectangle ContentBounds()
    {
        var p = PanelBounds(); return new(p.X+14,p.Y+106,p.Width-36,Math.Max(1,p.Height-170));
    }
    public static Rectangle TabBounds(int tab)
    {
        var p = PanelBounds(); int w = (p.Width-28)/TabCount;
        return new(p.X+14+tab*w,p.Y+66,w-4,30);
    }
    public static Rectangle CloseBounds()
    {
        var p=PanelBounds(); return new(p.Right-46,p.Y+10,32,30);
    }
    private static int GridWidth => Math.Min(330,ContentBounds().Width);
    private static int SlotStep => (GridWidth-8)/5;
    public static Rectangle SlotBounds(int index)
    {
        var b=ContentBounds(); int step=SlotStep, size=Math.Min(54,step-6);
        int row=index<10?index/5:index<15?2:3;
        int col=index<10?index%5:index<15?index-10:index-15;
        int top=row<2?38+row*(size+10):row==2?38+2*(size+10)+46:38+3*(size+10)+92;
        return new(b.X+4+col*step,b.Y+top-scroll,size,size);
    }
    public static Rectangle ExtendedSlotBounds(int local)
    {
        var b=ContentBounds(); int step=SlotStep, size=Math.Min(54,step-6);
        int row=local/4, col=local%4;
        int top=38+row*(size+10)+28;
        return new(b.X+4+col*step,b.Y+top-scroll,size,size);
    }
    public static bool TryClickHost(SoulItem soul, out string error)
    {
        error="";
        if((page!=0 && page!=3) || !Main.mouseLeft || !Main.mouseLeftRelease) return false;
        Point mouse=new(Main.mouseX,Main.mouseY);
        if(!ContentBounds().Contains(mouse)) return false;
        if(page==0)
        {
            for(int i=0;i<17;i++)
            {
                if(!SlotBounds(i).Contains(mouse)) continue;
                selected=i; Main.mouseLeftRelease=false;
                return soul.TrySwapHost(i,ref Main.mouseItem,out error);
            }
        }
        else
        {
            for(int i=0;i<8;i++)
            {
                if(!ExtendedSlotBounds(i).Contains(mouse)) continue;
                selected=17+i; Main.mouseLeftRelease=false;
                return soul.TrySwapHost(selected,ref Main.mouseItem,out error);
            }
        }
        return false;
    }
    public static bool HandleNavigation()
    {
        if(editing==null || !Main.mouseLeft || !Main.mouseLeftRelease) return false;
        Point mouse=new(Main.mouseX,Main.mouseY);
        if(CloseBounds().Contains(mouse))
        { Main.mouseLeftRelease=false; Toggle(editing); return true; }
        for(int i=0;i<TabCount;i++) if(TabBounds(i).Contains(mouse))
        { Main.mouseLeftRelease=false; SelectPage(i); return true; }
        return false;
    }
    private static bool Button(Rectangle r,string label,bool enabled=true)
    {
        Point mouse=new(Main.mouseX,Main.mouseY);
        bool hover=r.Contains(mouse) && ContentBounds().Contains(mouse);
        Box(r, enabled ? hover?new(54,100,128):new(37,66,90) : new(36,42,55));
        Border(r,enabled?new(74,126,153):new(57,65,80));
        // Buttons remain one line; fit measured text instead of overflowing.
        float scale=Math.Min(.76f,(r.Width-16)/Math.Max(1,FontAssets.MouseText.Value.MeasureString(label).X));
        Text(label,new(r.X+8,r.Y+(r.Height-20*scale)/2-2),enabled?Ink:Muted,scale);
        if(!enabled || !hover || !Main.mouseLeft || !Main.mouseLeftRelease) return false;
        Main.mouseLeftRelease=false; return true;
    }
    private static int Paragraph(string value,int x,int y,int width,Color color,float scale=.75f)
    {
        string line=""; int lineHeight=(int)Math.Ceiling(FontAssets.MouseText.Value.LineSpacing*scale)+2;
        foreach(char c in value)
        {
            if(c=='\r') continue;
            if(c=='\n' || (line.Length>0 && FontAssets.MouseText.Value.MeasureString(line+c).X*scale>width))
            { Text(line,new(x,y),color,scale); y+=lineHeight; line=""; }
            if(c!='\n') line+=c;
        }
        if(line.Length>0) { Text(line,new(x,y),color,scale); y+=lineHeight; }
        return y;
    }
    private static int Section(string title,string body,int x,int y,int width,Color color)
    {
        Box(new(x,y+3,3,16),color); Text(title,new(x+10,y),color,.82f);
        return Paragraph(body,x+10,y+28,width-14,Ink)+18;
    }
    private static bool Available(SoulItem soul,int i) => i<10 ? i<Math.Max(1,soul.Capacity) :
        i<15 ? i-10<soul.SupportCapacity : i<17 ? i-15<soul.BonusCapacity :
        false;
    private static string SlotName(int i) => i<10?$"普通剑位 {i+1:00}":i<15?$"支援剑位 S{i-9:00}":
        i<17?$"森林剑位 F{i-14:00}":i<19?$"旧普通扩展 {i-16:00} · 仅可取回":$"旧支援扩展 {i-18:00} · 仅可取回";
    private static void Draw()
    {
        var soul=editing; if(page>=TabCount) SelectPage(0);
        var panel=PanelBounds(); var body=ContentBounds();
        Point mouse=new(Main.mouseX,Main.mouseY);
        bool inside=panel.Contains(mouse);
        if(inside)
        {
            Main.LocalPlayer.mouseInterface=true; PlayerInput.LockVanillaMouseScroll("ZenithReborn: Soul");
            Main.HoverItem=new Item(); Main.hoverItemName="";
        }
        Box(new(panel.X+4,panel.Y+5,panel.Width,panel.Height),Color.Black*.35f);
        Box(panel,new(15,23,38,248)); Border(panel,new(66,92,118));
        Box(new(panel.X+1,panel.Y+1,panel.Width-2,3),Accent*.7f);
        Text(soul.Awakened?"天顶复生 · 众剑归位":"天顶残魂",new(panel.X+18,panel.Y+14),Ink,panel.Width<430?.86f:1f);
        Text(soul.Attached?$"主攻 {soul.Active.Count()}/{soul.Capacity+soul.BonusCapacity}   支援 {soul.Supports.Count()}/{soul.SupportCapacity}   效能 {soul.Potency:P0}":
            "尚未附着 · 第一剑位仅供预览",new(panel.X+18,panel.Y+43),Accent,panel.Width<430?.61f:.73f);
        var close=CloseBounds(); Box(close,close.Contains(mouse)?new(108,57,68):new(44,50,66));
        Text("×",new(close.X+10,close.Y+4),Ink,.88f);
        string[] tabs=["武器寄宿","羁绊特性","成长与记忆","旧物取回"];
        for(int i=0;i<TabCount;i++)
        {
            var tab=TabBounds(i); bool active=page==i;
            Box(tab,active?new(38,69,92):tab.Contains(mouse)?new(37,47,65):new(27,36,52));
            if(active) Box(new(tab.X,tab.Bottom-2,tab.Width,2),Accent);
            Text(tabs[i],new(tab.X+10,tab.Y+6),active?Ink:Muted,panel.Width<430?.66f:.78f);
        }
        HandleNavigation(); if(editing==null) return;
        if(body.Contains(mouse) && PlayerInput.ScrollWheelDelta!=0)
            scroll=Math.Clamp(scroll-PlayerInput.ScrollWheelDelta/120*48,0,Math.Max(0,contentHeight-body.Height));
        // Clip only the scrolling body. Headers/footer and vanilla item tooltips
        // use the regular UI batch; never leave the scissor enabled for other layers.
        GraphicsDevice device=Main.graphics.GraphicsDevice;
        Main.spriteBatch.End();
        Rectangle oldScissor=device.ScissorRectangle; RasterizerState oldRasterizer=device.RasterizerState;
        clipState ??= new RasterizerState { CullMode=CullMode.None,ScissorTestEnable=true };
        Vector2 tl=Vector2.Transform(body.Location.ToVector2(),Main.UIScaleMatrix);
        Vector2 br=Vector2.Transform(new Vector2(body.Right,body.Bottom),Main.UIScaleMatrix);
        device.ScissorRectangle=Rectangle.Intersect(device.Viewport.Bounds,new Rectangle((int)tl.X,(int)tl.Y,(int)Math.Ceiling(br.X-tl.X),(int)Math.Ceiling(br.Y-tl.Y)));
        Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,Main.DefaultSamplerState,DepthStencilState.None,clipState,null,Main.UIScaleMatrix);
        try
        {
            int top=body.Y-scroll;
            int bottom=page==0?DrawHosts(soul,body,top):page==1?DrawTraits(soul,body,top):page==2?DrawProgress(soul,body,top):DrawExtended(soul,body,top);
            contentHeight=bottom-top;
        }
        finally
        {
            Main.spriteBatch.End(); device.ScissorRectangle=oldScissor;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,Main.DefaultSamplerState,DepthStencilState.None,oldRasterizer,null,Main.UIScaleMatrix);
        }
        scroll=Math.Clamp(scroll,0,Math.Max(0,contentHeight-body.Height));
        if(contentHeight>body.Height)
        {
            var track=new Rectangle(panel.Right-14,body.Y,4,body.Height); Box(track,new(32,46,64));
            int h=Math.Max(20,body.Height*body.Height/contentHeight);
            Box(new(track.X,track.Y+(body.Height-h)*scroll/Math.Max(1,contentHeight-body.Height),4,h),Accent*.7f);
        }
        int fy=panel.Bottom-57;
        Box(new(panel.X+14,fy-3,panel.Width-28,1),new(48,65,85));
        string message=Main.GameUpdateCount<feedbackUntil?feedback:"左键交换武器 · 悬停查看详情 · P 关闭";
        Paragraph(message,panel.X+16,fy+4,panel.Width-32,Main.GameUpdateCount<feedbackUntil?Color.LightSalmon:Muted,panel.Width<430?.60f:.68f);
        if(contentHeight>body.Height) Text("滚轮浏览",new(panel.Right-84,panel.Bottom-20),Muted,.60f);
    }
    private static int DrawHosts(SoulItem soul,Rectangle b,int y)
    {
        int size=SlotBounds(0).Width;
        Text("主攻剑位",new(b.X+4,y+3),Accent,.84f);
        Text("参与出剑 · 各自计算伤害",new(b.X+4,y+24),Muted,.63f);
        int supportY=y+38+2*(size+10)+18, forestY=y+38+3*(size+10)+64;
        Text("支援剑位 · 只提供特性与羁绊",new(b.X+4,supportY),new(185,171,234),.70f);
        Text("森林剑位 · 完整属性与出剑",new(b.X+4,forestY),Color.LightGreen,.70f);
        var stored=soul.SlotItems(); Point mouse=new(Main.mouseX,Main.mouseY);
        for(int i=0;i<17;i++)
        {
            Rectangle r=SlotBounds(i); Item item=stored[i]; bool unlocked=Available(soul,i);
            Color tint=unlocked?(i<10?Accent:i<15?new(185,171,234):Color.LightGreen):new(75,85,102);
            bool hovered=b.Contains(mouse) && r.Contains(mouse);
            Box(r,hovered?new(53,70,95):new(29,41,60));
            Border(r,selected==i?Color.Gold:hovered?tint:tint*.55f);
            Text(i<10?$"{i+1}":i<15?$"S{i-9}":$"F{i-14}",new(r.X+3,r.Y+1),tint,.48f);
            if(!item.IsAir)
            {
                Main.instance.LoadItem(item.type);
                var texture=TextureAssets.Item[item.type].Value;
                var frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Frame();
                float scale=Math.Min(1,(size-14f)/Math.Max(frame.Width,frame.Height));
                Main.spriteBatch.Draw(texture,r.Center.ToVector2()+new Vector2(0,3),frame,unlocked?(item.color==default?Color.White:item.color):Color.Gray,
                    0,frame.Size()*.5f,scale,SpriteEffects.None,0);
                Box(new(r.X+4,r.Bottom-4,r.Width-8,2),unlocked?SoulVisuals.Tint(item.type):Color.Gray);
                if(SoulProgress.ZenithWeapons.Contains(item.type)) Text("*",new(r.Right-11,r.Y),Color.Gold,.65f);
            }
            else Text(unlocked?"+":"锁",new(r.Center.X-8,r.Center.Y-8),tint,.72f);
            if(!hovered) continue;
            selected=i; HoveredSlotRequirement=SoulProgress.SlotHint(soul,i);
            if(!item.IsAir) { Main.HoverItem=item.Clone();Main.hoverItemName=item.Name; }
            else Main.hoverItemName=HoveredSlotRequirement;
        }
        if(TryClickHost(soul,out string error))
        {
            Sync(soul); SoundEngine.PlaySound(SoundID.Grab);
            feedback="已交换 · "+SlotName(selected); feedbackUntil=Main.GameUpdateCount+180;
        }
        else if(error.Length>0) { feedback=error;feedbackUntil=Main.GameUpdateCount+240; }
        int gridBottom=SlotBounds(16).Bottom+20;
        bool side=b.Width>=650; int dx=side?b.X+GridWidth+20:b.X+4,dy=side?y+4:gridBottom;
        int width=side?b.Width-GridWidth-24:b.Width-8;
        var chosen=soul.ItemAt(selected);
        string state=Available(soul,selected)?!soul.Attached?"预览 · 附着后启用":selected<10||selected>=15?"主攻 · 独立出剑":"支援 · 不出剑":"未解锁 / 已暂停 · 可空手取回";
        dy=Section(SlotName(selected),state,dx,dy,width,Color.Gold);
        if(!chosen.IsAir)
        {
            dy=Section(chosen.Name,$"{WeaponFamilies.Name(chosen.type)}\n面板伤害 {chosen.damage} · 使用间隔 {chosen.useAnimation}帧\n"+
                WoodTypes.Description(chosen.type),dx,dy,width,Ink);
            if(WoodTypes.IsWood(chosen.type)) dy=Paragraph($"当前环境倍率 S = {soul.Wood.Strength:0.##}；P 为实际主剑影伤害。",dx+10,dy,width-14,Muted,.68f)+12;
        }
        else dy=Section("等待寄宿","拿起一把近战武器，左键点击剑位放入。再次空手点击即可取回。金色星号 * 表示天顶材料剑。",dx,dy,width,Muted);
        dy=Section("解锁条件",SoulProgress.SlotHint(soul,selected),dx,dy,width,Accent);
        int bottom=Math.Max(gridBottom,dy)+8;
        bottom=Section("下一步",SoulProgress.Next(soul),b.X+4,bottom,b.Width-8,Accent);
        if(Button(new(b.X+14,bottom,b.Width-28,34),"查看成长路线 / 史莱姆王记忆 →")) SelectPage(2);
        return bottom+48;
    }
    private static int DrawExtended(SoulItem soul,Rectangle b,int y)
    {
        Text("旧扩展物品 · 仅可空手取回",new(b.X+4,y+3),Color.Gold,.84f);
        Text("当前只有17个有效寄宿槽；旧版扩展物品不生效，也不能再放入",new(b.X+4,y+24),Muted,.63f);
        var stored=soul.SlotItems(); Point mouse=new(Main.mouseX,Main.mouseY);
        int size=SlotBounds(0).Width;
        for(int local=0;local<8;local++)
        {
            int i=17+local; Rectangle r=ExtendedSlotBounds(local); Item item=stored[i]; bool unlocked=Available(soul,i);
            Color tint=unlocked?(i<19?Accent:new(185,171,234)):new(75,85,102);
            bool hovered=b.Contains(mouse)&&r.Contains(mouse);
            Box(r,hovered?new(53,70,95):new(29,41,60));
            Border(r,selected==i?Color.Gold:hovered?tint:tint*.55f);
            Text(i<19?$"N{i-6}":$"S{i-13}",new(r.X+3,r.Y+1),tint,.48f);
            if(!item.IsAir)
            {
                Main.instance.LoadItem(item.type);
                var texture=TextureAssets.Item[item.type].Value;
                var frame=Main.itemAnimations[item.type]?.GetFrame(texture)??texture.Frame();
                float scale=Math.Min(1,(size-14f)/Math.Max(frame.Width,frame.Height));
                Main.spriteBatch.Draw(texture,r.Center.ToVector2()+new Vector2(0,3),frame,unlocked?(item.color==default?Color.White:item.color):Color.Gray,
                    0,frame.Size()*.5f,scale,SpriteEffects.None,0);
                Box(new(r.X+4,r.Bottom-4,r.Width-8,2),unlocked?SoulVisuals.Tint(item.type):Color.Gray);
            }
            else Text(unlocked?"+":"锁",new(r.Center.X-8,r.Center.Y-8),tint,.72f);
            if(!hovered) continue;
            selected=i; HoveredSlotRequirement=SoulProgress.SlotHint(soul,i);
            if(!item.IsAir){Main.HoverItem=item.Clone();Main.hoverItemName=item.Name;}
            else Main.hoverItemName=HoveredSlotRequirement;
        }
        if(TryClickHost(soul,out string error))
        { Sync(soul); SoundEngine.PlaySound(SoundID.Grab); feedback="已交换 · "+SlotName(selected); feedbackUntil=Main.GameUpdateCount+180; }
        else if(error.Length>0){feedback=error;feedbackUntil=Main.GameUpdateCount+240;}
        int dx=b.X+4,dy=y+2*(size+10)+40;
        dy=Section(SlotName(selected),Available(soul,selected)?"可用":"未解锁 / 暂停 · 可空手取回",dx,dy,b.Width-8,Color.Gold);
        dy=Section("解锁条件",SoulProgress.SlotHint(soul,selected),dx,dy,b.Width-8,Accent);
        int oreCount=OreFamily.Count(soul);
        dy=Section("矿石族",$"材质 {oreCount}/14 · 飞剑 {Math.Min(14,oreCount)} 把 · 支援上限 {soul.SupportCapacity}/5\n"+
            "维持原17槽：旧扩展内容不参与战斗或羁绊；此页仅用于取回旧物。",dx,dy,b.Width-8,Color.LightBlue);
        return dy+20;
    }
    private static int DrawTraits(SoulItem soul,Rectangle b,int y)
    {
        var wood=soul.Wood; int x=b.X+4,w=b.Width-8;
        y=Section("木质族 · 自然之力",$"已寄宿 {wood.Count}/12 种 · 特殊效果 {wood.Strength:0.##}倍 · 生命 +{wood.Life}\n"+
            "1 / 2 / 4 / 6 / 8 种分别开放第 1～5 支援槽；支援剑不贡献武器属性。",x,y,w,Color.LightGreen);
        var bar=new Rectangle(x+10,y,w-20,7);Box(bar,new(36,53,59));Box(new(bar.X,bar.Y,bar.Width*wood.Count/12,bar.Height),new(102,192,133));y+=22;
        y=Section(wood.Full?"森林之力 · 已激活":"森林之力 · 等待全套",wood.Full?
            $"12种同时寄宿：环境效果达到基础3倍，生命+100，解锁2个真实剑位。战斗成长 {Main.LocalPlayer.GetModPlayer<WoodPlayer>().LayersFor(soul)}/12层。":
            "需12种木剑同时寄宿在普通与有效支援槽，至少7普通＋5支援。全套获得基础3倍环境效果、100生命、2个真实剑位和战斗成长；取下则暂停。",x,y,w,Color.LightGreen);
        for(int i=0;i<12;i++)
        {
            bool has=(wood.Mask&(1<<i))!=0;
            y=Section((has?"已激活 · ":"未寄宿 · ")+WoodTypes.Names[i]+" / "+WoodTypes.Effects[i],WoodTypes.Description(WoodTypes.Types[i]),x,y,w,has?Color.LightGreen:Muted);
        }
        y=Section("肉后三矿 · 当前有效特性","主攻剑携带自身特性；有效支援槽为主攻剑附加特性。计数、冷却和伤害按实际来源武器独立结算。",x,y,w,Accent);
        var ore=soul.Active.Concat(soul.Supports).Select(i=>i.type).Where(HardOreTypes.IsOre).Distinct().ToArray();
        if(ore.Length==0) y=Paragraph("尚未寄宿肉后三矿剑。",x+10,y,w-14,Muted)+16;
        foreach(int type in ore) y=Section(HardOreTypes.Names[HardOreTypes.Index(type)],HardOreTypes.Description(type),x,y,w,SoulVisuals.Tint(type));
        return y;
    }
    private static int DrawProgress(SoulItem soul,Rectangle b,int y)
    {
        int x=b.X+4,w=b.Width-8;
        y=Section("下一步",SoulProgress.Next(soul),x,y,w,Accent);
        var king=AttachmentStory.FindKing(Main.LocalPlayer,soul.SoulId);
        if(king!=null)
        {
            y=Section("史莱姆王的回应",king.ai[2]>0?"祝福「附着」已经归位。让这把剑，陪你重新站起来。":AttachmentStory.Lines[Math.Clamp((int)king.ai[1],0,3)],x,y,w,Color.Gold);
            if(king.ai[2]==0)
            {
                if(Button(new(x+10,y,w-20,34),king.ai[1]<3?"继续聆听 →":"接受祝福：附着")) AttachmentStory.RequestAdvance(king);
                y+=48;
            }
        }
        else if(!soul.KingMemory)
        {
            y=Section("友善史莱姆王 · 最初的记忆",AttachmentStory.Requirement(Main.LocalPlayer,soul)+"\n也可白天地表使用凝胶信物呼唤，无需附近史莱姆。",x,y,w,Color.Gold);
            bool ready=AttachmentStory.CanStart(Main.LocalPlayer,soul);
            if(Button(new(x+10,y,w-20,34),ready?"回应呼唤 · 与史莱姆王相遇":"附近普通史莱姆 · 白天 · 地表",ready)) AttachmentStory.RequestStart(soul);
            y+=48;
        }
        y=Section("普通剑位 · 主线解封","胜利与魂芯记录在这枚剑魂上。先前的条件全部满足后，下一个剑位才会开放。",x,y,w,Accent);
        for(int slot=0;slot<10;slot++)
        {
            bool done=slot<soul.Capacity; string condition=slot==0?"与友善史莱姆王对话，接受「附着」":SoulProgress.SlotGates[slot-1].Requirement;
            y=Section($"{(done?"已解锁":slot==soul.Capacity?"下一槽":"待解封")} · 第 {slot+1:00} 剑位",condition,x,y,w,done?Color.LightGreen:slot==soul.Capacity?Color.Gold:Muted);
        }
        y=Section("魂芯收藏",string.Join("   ",Enumerable.Range(0,6).Select(i=>((soul.Cores&(1<<i))!=0?"● ":"○ ")+SoulProgress.CoreNames[i])),x,y,w,Color.Gold);
        int core=SoulProgress.RecoverableCore(soul);
        if(core>=0)
        {
            bool has=Main.LocalPlayer.inventory.Any(i=>i.type==SoulCore.TypeFor(core)&&i.stack>0);
            if(Button(new(x+10,y,w-20,34),(has?"融入":"凭胜利铭刻补办并融入")+SoulProgress.CoreNames[core]+"魂芯")) SoulProgress.RequestCoreRecovery(soul);
            y+=48;
        }
        else y=Paragraph("记录了对应胜利后，遗失的魂芯可在此补办；不会跳过主线条件。",x+10,y,w-14,Muted)+18;
        int mechs=SoulProgress.Mechs(soul.Kills);
        y=Section(mechs==3?"祝福 · 机魂协律":$"诅咒 · 迟滞械缓 {mechs}/3",mechs==3?"连续轮换时，攻速与距离最多提升20%。":"每击败一位机械Boss，诅咒便减弱一分；单剑不累积迟滞。",x,y,w,Color.LightPink);
        return Section("凝胶信物","新角色自带信物。旧角色：凝胶10＋坠落之星1，在工作台制作。",x,y,w,Muted);
    }
}

public class SoulSlotTooltips : GlobalItem
{
    public override void ModifyTooltips(Item item,List<TooltipLine> tooltips)
    {
        if(SoulUI.EditingSoul!=null && SoulUI.HoveredSlotRequirement.Length>0)
            tooltips.Add(new(Mod,"SlotRequirement",SoulUI.HoveredSlotRequirement));
    }
}
