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

public partial class SoulUI : ModSystem
{
    private static SoulItem editing;
    private static int page, selected, scroll, contentHeight;
    public const int CatalogPage = 3, RecoveryPage = 4;
    private static int catalogFilter;
    private static Vector2? windowPosition;
    private static Vector2 dragOffset;
    public static bool WindowDragging { get; private set; }
    private static int TabCount => editing?.HasLegacyExpansion == true ? 5 : 4;
    private static string feedback = "";
    private static ulong feedbackUntil;
    private static RasterizerState clipState;
    private static readonly Color Ink = new(230, 232, 235), Muted = new(158, 165, 176), Accent = new(148, 202, 208);
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
        pageStarted=UiTime;
        if (page == RecoveryPage && selected is not (>=17 and <25)) selected = 17;
        if (page != RecoveryPage && selected is >=17 and <25) selected = 0;
        HoveredSlotRequirement = "";
    }
    public static void Toggle(SoulItem soul)
    {
        HoveredSlotRequirement = "";
        WindowDragging=false;
        if (ReferenceEquals(editing, soul)) { Sync(editing); editing = null; return; }
        if (editing != null) Sync(editing);
        editing = soul; selected = 0; catalogFilter = 0; feedback = ""; SelectPage(0);
        openedAt=UiTime;libraryScroll=0; Array.Fill(lastSlotType,-1); Array.Fill(slotLit,0f); Main.playerInventory = true;
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
        editing = null; HoveredSlotRequirement = ""; SoulInput.Reset(); ResetWindowPosition();
        clipState?.Dispose(); clipState = null;
        glowTexture?.Dispose(); glowTexture=null; shardTexture?.Dispose(); shardTexture=null;
        veilTexture?.Dispose(); veilTexture=null;
    }
    public override void OnWorldUnload()
    {
        editing = null; HoveredSlotRequirement = ""; SoulInput.Reset(); ResetWindowPosition(); SelectPage(0); HeroPrefixCompat.Clear();
    }
    public override void PostUpdateInput()
    {
        HoveredSlotRequirement = "";
        if (Main.gameMenu || Main.dedServ) return;
        if (ZenithReborn.OpenSoul?.JustPressed == true && !Main.drawingPlayerChat)
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
        { Sync(editing); editing = null; WindowDragging=false; }
        bool visible=editing != null && !Main.hideUI && !Main.mapFullscreen;
        bool inside=visible && ContainsScreenPoint(Main.mouseX,Main.mouseY,Main.screenWidth,Main.screenHeight,Main.UIScale);
        bool dragCapture=false;
        if(visible) dragCapture=HandleWindowDrag(Main.mouseX,Main.mouseY,Main.screenWidth,Main.screenHeight,
            Main.UIScale,Main.mouseLeft,Main.mouseLeftRelease);
        else WindowDragging=false;
        SoulInput.Capture(inside || dragCapture || visible &&
            ContainsScreenPoint(Main.mouseX,Main.mouseY,Main.screenWidth,Main.screenHeight,Main.UIScale));
    }
    public override void PostDrawInterface(SpriteBatch spriteBatch) => SoulInput.RestoreAfterInterface();
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        // The domain owns a separate scene. Do not let a hidden vanilla health
        // meter/inventory enqueue hover text or achievements over the scene.
        if(DomainMode && editing!=null && !Main.hideUI && !Main.mapFullscreen)
            layers.RemoveAll(l=>l.Name.StartsWith("Vanilla:") && l.Name is not
                ("Vanilla: Mouse Text" or "Vanilla: Cursor" or "Vanilla: Mouse Item / NPC Head"));
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
                try { if (editing != null && !Main.hideUI && !Main.mapFullscreen) Draw(); }
                finally { SoulInput.EndSoulLayer(); }
                return true;
            }, InterfaceScaleType.UI));
    }
    private static void Box(Rectangle r, Color color) => Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, r, color);
    private static void Text(string text, Vector2 at, Color color, float scale = .78f) =>
        Utils.DrawBorderString(Main.spriteBatch, text, at, color * (.35f+.65f*PageFade), scale);
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
        if(DomainMode)return new(0,0,width,height);
        int w = Math.Min(760, Math.Max(1,width-24));
        int h = Math.Min(650, Math.Max(1,height-24));
        Vector2 position=windowPosition??new Vector2((width-w)/2,Math.Min(110,(height-h)/2));
        return new((int)Math.Clamp(position.X,0,Math.Max(0,width-w)),
            (int)Math.Clamp(position.Y,0,Math.Max(0,height-h)),w,h);
    }
    public static void ResetWindowPosition() { windowPosition=null; WindowDragging=false; }
    private static Rectangle HeaderDragBounds(Rectangle panel) => DomainMode?Rectangle.Empty:new(panel.X+8,panel.Y+5,Math.Max(1,panel.Width-64),52);
    public static Rectangle HeaderDragBounds() => HeaderDragBounds(PanelBounds());
    // Called in PostUpdateInput, before masking world/UI buttons. Drag ownership
    // survives leaving the old panel; drawing never handles physical movement.
    public static bool HandleWindowDrag(int x,int y,int width,int height,float scale,bool down,bool pressEdge)
    {
        if(editing==null || DomainMode) { WindowDragging=false; return false; }
        int uiWidth=(int)(width/scale),uiHeight=(int)(height/scale);
        Vector2 mouse=new(x/scale,y/scale);
        Rectangle panel=PanelBounds(uiWidth,uiHeight);
        bool owned=WindowDragging;
        if(!down) { WindowDragging=false; return owned; }
        if(!WindowDragging && pressEdge && Main.mouseItem.IsAir && HeaderDragBounds(panel).Contains(mouse.ToPoint()))
        { WindowDragging=true; dragOffset=mouse-panel.Location.ToVector2(); }
        if(WindowDragging)
            windowPosition=new Vector2(Math.Clamp(mouse.X-dragOffset.X,0,Math.Max(0,uiWidth-panel.Width)),
                Math.Clamp(mouse.Y-dragOffset.Y,0,Math.Max(0,uiHeight-panel.Height)));
        return owned || WindowDragging;
    }
    // PostUpdateInput is still in physical screen units; drawing uses UI units.
    public static bool ContainsScreenPoint(int x,int y,int width,int height,float scale) =>
        PanelBounds((int)(width/scale),(int)(height/scale)).Contains((int)(x/scale),(int)(y/scale));
    public static Rectangle ContentBounds()
    {
        if(DomainMode)return DomainWorkBounds();
        var p=PanelBounds(); int top=page==0?72:112;
        return new(p.X+14,p.Y+top,p.Width-36,Math.Max(1,p.Height-top-48));
    }
    public static Rectangle TabBounds(int tab)
    {
        if(DomainMode)
        {
            var work=DomainWorkBounds();int domainStep=work.Width/TabCount;
            return new(work.X+tab*domainStep,62,domainStep-5,36);
        }
        if(page==0)
        {
            float[] angles=[-90,-24,142,180,-164];
            Vector2 c=OrbitPoint(angles[tab],RingRadius+62);
            return new((int)c.X-24,(int)c.Y-24,48,48);
        }
        var p=PanelBounds(); int step=(p.Width-28)/TabCount;
        return new(p.X+14+tab*step,p.Y+64,step-4,42);
    }
    public static Rectangle CloseBounds()
    {
        var p=PanelBounds(); return new(p.Right-46,p.Y+10,32,30);
    }
    private static int GridWidth => Math.Min(330,ContentBounds().Width);
    private static int SlotStep => (GridWidth-8)/5;
    // All animated decoration is anchored to these fixed hit targets. Opening,
    // hover and insertion effects never move a clickable slot away from its item.
    public static Rectangle SlotBounds(int index)
    {
        if(index==SoulItem.LegendarySlotId)
        {
            Vector2 legendaryCenter=OrbitPoint(-45,RingRadius+86);
            return new((int)legendaryCenter.X-29,(int)legendaryCenter.Y-29,58,58);
        }
        int supportOrdinal=SoulItem.SupportOrdinal(index);
        if(supportOrdinal>=0)
        {
            if(supportOrdinal>=11)
            {
                var b=DomainMode?RingViewport:ContentBounds();int extra=supportOrdinal-11;
                int columns=Math.Max(1,Math.Min(5,(b.Width-12)/58));
                return new(b.Right-54-extra%columns*58,ContentBounds().Y+BaseRingCanvasHeight+14+extra/columns*58-scroll,44,44);
            }
            // Include saved paused slots in spacing. Never collapse their hit
            // targets when capacity shrinks. Large sets wrap around the lower arc.
            int count=editing?.VisibleSupportCount??5;
            float start=count<=6?8:-6;
            float angle=start+supportOrdinal*20;
            Vector2 supportCenter=OrbitPoint(angle,RingRadius+62);
            int supportSize=RingViewport.Width<420?35:RingRadius<116?38:44;
            return new((int)supportCenter.X-supportSize/2,(int)supportCenter.Y-supportSize/2,supportSize,supportSize);
        }
        if(index>=25)
        {
            var body=DomainMode?RingViewport:ContentBounds();int ordinal=index-25;
            int columns=Math.Max(1,Math.Min(5,(body.Width-12)/58)),col=ordinal%columns,row=ordinal/columns;
            float bend=MathF.Sin((col+.5f)/columns*MathHelper.Pi)*9;
            return new(body.Right-54-col*58,ContentBounds().Y+BaseRingCanvasHeight+14+row*58+(int)bend-scroll,44,44);
        }
        Vector2 c=index<10?OrbitPoint(-90+index*36,RingRadius):
            index<15?OrbitPoint(8+(index-10)*20,RingRadius+62):
            OrbitPoint(-142+(index-15)*22,RingRadius+62);
        int size=RingViewport.Width<420?index<10?40:35:index<10?48:44;
        return new((int)c.X-size/2,(int)c.Y-size/2,size,size);
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
        if((page!=0 && page!=RecoveryPage) || !Main.mouseLeft || !Main.mouseLeftRelease) return false;
        Point mouse=new(Main.mouseX,Main.mouseY);
        if(!ContentBounds().Contains(mouse)) return false;
        if(page==0)
        {
            foreach(int i in soul.VisibleSlotIds)
            {
                if(!SlotBounds(i).Contains(mouse)) continue;
                selected=i; Main.mouseLeftRelease=false;
                if(DomainMode && Main.mouseItem.IsAir)return false;
                return soul.TrySwapHost(i,ref Main.mouseItem,out error);
            }
        }
        else
        {
            for(int i=0;i<8;i++)
            {
                if(!ExtendedSlotBounds(i).Contains(mouse)) continue;
                selected=17+i; Main.mouseLeftRelease=false;
                if(DomainMode && Main.mouseItem.IsAir)return false;
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
        for(int i=0;i<TabCount;i++) if(TabBounds(i).Contains(mouse) && (DomainMode || page!=0 || ContentBounds().Contains(mouse)))
        { Main.mouseLeftRelease=false; SelectPage(i); return true; }
        return false;
    }
    private static bool Button(Rectangle r,string label,bool enabled=true)
    {
        Point mouse=new(Main.mouseX,Main.mouseY);
        bool hover=r.Contains(mouse) && ContentBounds().Contains(mouse);
        RoundedBox(r, enabled ? hover?new Color(62,69,72)*.9f:new Color(36,41,47)*.9f : new Color(26,28,33)*.8f,8);
        Line(new(r.X+10,r.Bottom-1),new(r.Right-10,r.Bottom-1),enabled?Accent*(hover?.55f:.22f):Color.White*.07f);
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
        Box(new(x,y+3,3,16),color); Text(Fit(title,width-14,.82f),new(x+10,y),color,.82f);
        return Paragraph(body,x+10,y+28,width-14,Ink)+18;
    }
    private static bool Available(SoulItem soul,int i) => i==SoulItem.LegendarySlotId ? soul.LegendaryOn :
        i<10 ? i<Math.Max(1,soul.Capacity) :
        i<15 ? i-10<soul.SupportCapacity : i<17 ? i-15<soul.BonusCapacity :
        i>=25 && SoulItem.SupportOrdinal(i)<soul.SupportCapacity;
    private static string SlotName(int i) => i==SoulItem.LegendarySlotId?"传奇剑位":i<10?$"普通剑位 {i+1:00}":i<15?$"支援剑位 S{i-9:00}":
        i<17?$"奖励剑位 R{i-14:00}":i<19?$"旧普通扩展 {i-16:00} · 仅可取回":i<25?$"旧支援扩展 {i-18:00} · 仅可取回":$"支援剑位 S{SoulItem.SupportOrdinal(i)+1:00}";
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
        if(DomainMode){DrawDomainBackdrop(panel);DrawDomainHeader(mouse);DrawLibrary(soul);}
        else
        {
            DrawChrome(panel,mouse);
            Text(soul.Awakened?"天顶复生 · 众剑归位":"天顶残魂",new(panel.X+18,panel.Y+14),Ink,panel.Width<430?.86f:1f);
            if(panel.Width>=430) Text("拖动标题栏",new(panel.Right-145,panel.Y+18),WindowDragging?Accent:Muted,.6f);
            Text(soul.Attached?$"主攻 {soul.Active.Count()}/{soul.Capacity+soul.BonusCapacity+(soul.LegendaryOn?1:0)}   支援 {soul.Supports.Count()}/{soul.SupportCapacity}   效能 {soul.Potency:P0}":
                "尚未附着 · 第一剑位仅供预览",new(panel.X+18,panel.Y+43),Accent,panel.Width<430?.61f:.73f);
        }
        var close=CloseBounds();
        if(close.Contains(mouse)) RoundedBox(close,new Color(112,62,65)*.62f,8);
        Text("×",new(close.X+10,close.Y+4),Ink,.88f);
        HandleNavigation(); if(editing==null) return;
        body=ContentBounds();
        if(page!=0 && !DomainMode) DrawNavigation(false);
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
            DrawPageSurface(body);
            int top=body.Y-scroll;
            int bottom=page==0?DrawHosts(soul,body,top):page==1?DrawTraits(soul,body,top):page==2?DrawProgress(soul,body,top):page==CatalogPage?DrawCatalog(soul,body,top):DrawExtended(soul,body,top);
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
            var track=new Rectangle(panel.Right-12,body.Y+8,2,Math.Max(1,body.Height-16)); Box(track,Color.White*.055f);
            int h=Math.Max(20,track.Height*body.Height/contentHeight);
            Box(new(track.X,track.Y+(track.Height-h)*scroll/Math.Max(1,contentHeight-body.Height),2,h),Accent*.5f);
        }
        int fy=panel.Bottom-47;

        string message=Main.GameUpdateCount<feedbackUntil?feedback:DomainMode && HoveredSlotRequirement.Length>0?HoveredSlotRequirement.Split('\n')[0]:
            DomainMode?"选择剑位 → 点击左侧剑库寄宿 · P / Esc 关闭":"左键交换 · 拖动标题栏移动窗口 · P 关闭";
        Paragraph(message,panel.X+16,fy+4,panel.Width-32,Main.GameUpdateCount<feedbackUntil?Color.LightSalmon:Muted,panel.Width<430?.60f:.68f);
        if(contentHeight>body.Height) Text("滚轮浏览",new(panel.Right-84,panel.Bottom-20),Muted,.60f);
    }
    private static int DrawHosts(SoulItem soul,Rectangle b,int y)
    {
        DrawSoulRing(soul,DomainMode?RingViewport:b);
        if(TryClickHost(soul,out string error))
        {
            Sync(soul); SoundEngine.PlaySound(SoundID.Grab);
            feedback="已交换 · "+SlotName(selected); feedbackUntil=Main.GameUpdateCount+180;
        }
        else if(error.Length>0) { feedback=error;feedbackUntil=Main.GameUpdateCount+240; }
        int bottom=y+RingCanvasHeight+12;
        int ringBottom=bottom;
        if(SideInspector){b=new(b.Right-284,b.Y,280,b.Height);bottom=y+14;}
        var chosen=soul.ItemAt(selected);
        string state=Available(soul,selected)?!soul.Attached?"预览 · 附着后启用":SoulItem.SupportOrdinal(selected)>=0?"支援 · 不出剑":"主攻 · 独立出剑":"未解锁 / 已暂停 · 可空手取回";
        bottom=Section(SlotName(selected)+(chosen.IsAir?"":" · "+chosen.Name),state,b.X+8,bottom,b.Width-16,Color.Gold);
        bottom=Section("解锁条件",SoulProgress.SlotHint(soul,selected),b.X+8,bottom,b.Width-16,Accent);
        bottom=Section("下一步",SoulProgress.Next(soul),b.X+8,bottom,b.Width-16,Accent);
        if(DomainMode)bottom=DrawDomainReturn(b,bottom);
        if(Button(new(b.X+18,bottom,b.Width-36,34),"查看成长路线 / 史莱姆王记忆 →")) SelectPage(2);
        return Math.Max(bottom+48,SideInspector?ringBottom:0);
    }
    private static int DrawExtended(SoulItem soul,Rectangle b,int y)
    {
        Text("旧扩展物品 · 仅可空手取回",new(b.X+4,y+3),Color.Gold,.84f);
        Text("支援按当前激活奖励扩展；此页旧物仍不生效，只可取回",new(b.X+4,y+24),Muted,.63f);
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
            if(!DomainMode)selected=i;
            HoveredSlotRequirement=SoulProgress.SlotHint(soul,i);
            if(!item.IsAir){Main.HoverItem=item.Clone();Main.hoverItemName=item.Name;}
            else Main.hoverItemName=HoveredSlotRequirement;
        }
        if(TryClickHost(soul,out string error))
        { Sync(soul); SoundEngine.PlaySound(SoundID.Grab); feedback="已交换 · "+SlotName(selected); feedbackUntil=Main.GameUpdateCount+180; }
        else if(error.Length>0){feedback=error;feedbackUntil=Main.GameUpdateCount+240;}
        int dx=b.X+4,dy=y+2*(size+10)+40;
        dy=Section(SlotName(selected),Available(soul,selected)?"可用":"未解锁 / 暂停 · 可空手取回",dx,dy,b.Width-8,Color.Gold);
        dy=Section("解锁条件",SoulProgress.SlotHint(soul,selected),dx,dy,b.Width-8,Accent);
        if(DomainMode)dy=DrawDomainReturn(b,dy);
        int oreCount=OreFamily.Count(soul);
        dy=Section("矿石族",$"材质 {oreCount}/14 · 飞剑 {Math.Min(14,oreCount)} 把 · 共用支援 {soul.SupportCapacity} 格\n"+
            "旧扩展内容不参与战斗或羁绊；新增支援独立保存，此页仅用于取回旧物。",dx,dy,b.Width-8,Color.LightBlue);
        return dy+20;
    }
    private static Color TraitColor(TraitCategory category) => category switch {
        TraitCategory.Wood=>Color.LightGreen,TraitCategory.Ore=>Accent,
        TraitCategory.Growth=>Color.Gold,_=>new Color(197,183,242) };
    private static int DrawTraits(SoulItem soul,Rectangle b,int y)
    {
        int x=b.X+4,w=b.Width-8;
        var traits=SoulTraits.Active(soul).ToArray();
        y=Section("当前生效特性",$"共 {traits.Length} 项；仅统计有效寄宿和已获得的祝福。命中触发效果会注明触发条件，并不代表当前正在施加。",x,y,w,Accent);
        if(Button(new(x+10,y,w-20,34),"打开特性图鉴 · 查看全部效果与激活条件 →")) SelectPage(CatalogPage);
        y+=48;
        if(traits.Length==0) return Section("暂无生效特性","获得附着并寄宿武器后，这里会自动列出当前生效的效果。",x,y,w,Muted);
        foreach(var category in Enum.GetValues<TraitCategory>())
        {
            var group=traits.Where(t=>t.Category==category).ToArray();
            if(group.Length==0)continue;
            y=Section(SoulTraits.CategoryName(category),$"{group.Length} 项生效",x,y,w,TraitColor(category));
            foreach(var trait in group)y=Section(trait.Title,trait.Description,x+8,y,w-8,Ink);
        }
        return y;
    }
    public static int CatalogFilter => catalogFilter;
    public static void SelectCatalogFilter(int value)
    { catalogFilter=Math.Clamp(value,0,4);scroll=0;contentHeight=0; }
    public static IEnumerable<SoulTrait> CatalogEntries(SoulItem soul) => SoulTraits.All(soul)
        .Where(t=>catalogFilter==0 || (int)t.Category==catalogFilter-1);
    private static int DrawCatalog(SoulItem soul,Rectangle b,int y)
    {
        int x=b.X+4,w=b.Width-8;
        y=Section("特性图鉴","查看已实装的特性与激活条件；未激活项只在这里展示。浏览图鉴不会解锁特性。S为木族倍率，P或D为实际来源主剑伤害。",x,y,w,Accent);
        string[] filters=["全部","木质族","矿石族","剑术","成长"];
        int cols=Math.Clamp(w/110,2,5),step=w/cols;
        for(int i=0;i<filters.Length;i++)
        {
            var r=new Rectangle(x+i%cols*step,y+i/cols*38,step-6,32);
            if(Button(r,(catalogFilter==i?"● ":"")+filters[i])) SelectCatalogFilter(i);
        }
        y+=((filters.Length+cols-1)/cols)*38+12;
        foreach(var trait in CatalogEntries(soul))
        {
            Color color=trait.Active?TraitColor(trait.Category):Muted;
            y=Section((trait.Active?"已激活 · ":"未激活 · ")+trait.Title,
                trait.Description+"\n激活条件："+trait.Requirement,x,y,w,color);
        }
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
        y=Section("六封印 · 此剑魂独立铭刻","两Boss组：解除→祝福；三Boss组：削弱→解除→祝福。解除即恢复正常机制，祝福额外强化另计。",x,y,w,Accent);
        foreach(SoulCurse curse in Enum.GetValues<SoulCurse>())
            y=Section(SoulCurses.Names[(int)curse]+" · "+SoulCurses.StageName(soul.Curse(curse))+" · "+SoulCurses.Count(soul,curse)+"/"+SoulCurses.Groups[(int)curse].Length,
                SoulCurses.Description(soul,curse)+"\n"+SoulCurses.Requirement(soul,curse),x,y,w,soul.Curse(curse)>=CurseStage.Released?Color.LightGreen:Color.LightPink);
        y=Section("外域 · 独立祝福记录",$"猪龙鱼公爵：{((soul.Seals&SoulCurses.Duke)!=0?"已铭刻":"未铭刻")}；鹿角怪：{((soul.Seals&SoulCurses.Deer)!=0?"已铭刻":"未铭刻")}。二者不施咒；特殊祝福效果尚未设计，本版只记录胜利。",x,y,w,Muted);
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
