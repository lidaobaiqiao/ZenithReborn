using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using ReLogic.Graphics;

namespace ZenithReborn;

public partial class SoulUI : ModSystem
{
    private static SoulItem editing;
    private static int page, selected, scroll, contentHeight;
    public const int TraitsPage=SoulUIPages.Traits, RecoveryPage=SoulUIPages.Recovery;
    private static int catalogFilter;
    private static Vector2? windowPosition;
    private static Vector2 dragOffset;
    public static bool WindowDragging { get; private set; }
    public static IReadOnlyList<int> NavigationPages=>SoulUIPages.Navigation;
    private static int TabCount=>NavigationPages.Count;
    public const int EnvironmentPage=SoulUIPages.Environments;
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
        page=SoulUIPages.Resolve(value,editing?.HasLegacyExpansion==true);scroll=0;contentHeight=0;
        pageStarted=UiTime;
        if (page == RecoveryPage && selected is not (>=17 and <25)) selected = 17;
        if (page != RecoveryPage && selected is >=17 and <25) selected = 0;
        HoveredSlotRequirement = "";
    }
    public static void Toggle(SoulItem soul)
    {
        HoveredSlotRequirement = "";
        WindowDragging=false;
        if (ReferenceEquals(editing, soul)) { Sync(editing); editing = null; ResetArmory(); return; }
        if (editing != null) Sync(editing);
        editing = soul; selected = 0; catalogFilter = 0; feedback = ""; SelectPage(0);
        openedAt=UiTime;libraryScroll=0;ResetInformation();ResetArmory();growthFocus=sealFocus=-1; Array.Fill(lastSlotType,-1); Array.Fill(slotLit,0f); Main.playerInventory = true;
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
        editing = null; HoveredSlotRequirement = ""; SoulInput.Reset(); ResetWindowPosition();ResetInformation();ResetArmory();
        clipState?.Dispose(); clipState = null;
        glowTexture?.Dispose(); glowTexture=null; shardTexture?.Dispose(); shardTexture=null;
        veilTexture?.Dispose(); veilTexture=null;
    }
    public override void OnWorldUnload()
    {
        editing = null; HoveredSlotRequirement = ""; SoulInput.Reset(); ResetWindowPosition(); SelectPage(0); HeroPrefixCompat.Clear();ResetInformation();ResetArmory();
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
        // 魂域独立拥有整个场景，隐藏的原版生命栏和背包不再往前景排入提示或成就。
        if(DomainMode && editing!=null && !Main.hideUI && !Main.mapFullscreen)
            layers.RemoveAll(l=>l.Name.StartsWith("Vanilla:") && l.Name is not
                ("Vanilla: Mouse Text" or "Vanilla: Cursor" or "Vanilla: Mouse Item / NPC Head"));
        // 原版背包在魂域之前绘制；输入需要对所有背景层屏蔽，而非只屏蔽末尾提示层。
        layers.Insert(0, new LegacyGameInterfaceLayer("ZenithReborn: Input Shield",
            () =>
            {
                // 物品可能在本输入钩子之后才打开面板。
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
    private static void Text(string text, Vector2 at, Color color, float scale = .78f)
    {
        Color ink=color*(.35f+.65f*PageFade);
        // 魂域已有稳定暗底，直接绘字保留细节；旧浮窗保留描边。
        if(DomainMode||page!=0&&page!=RecoveryPage)
            Main.spriteBatch.DrawString(FontAssets.MouseText.Value,text,at,ink,0,Vector2.Zero,scale,SpriteEffects.None,0);
        else Utils.DrawBorderString(Main.spriteBatch,text,at,ink,scale);
    }
    private static void Border(Rectangle r, Color color)
    {
        Box(new(r.X,r.Y,r.Width,1),color); Box(new(r.X,r.Bottom-1,r.Width,1),color);
        Box(new(r.X,r.Y,1,r.Height),color); Box(new(r.Right-1,r.Y,1,r.Height),color);
    }
    // 界面层已经把屏幕和鼠标转换为界面单位，布局和命中不能再次应用缩放。
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
    // 在输入更新中、屏蔽按钮之前处理拖动；离开旧面板仍保留拖动，绘制不处理物理移动。
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
    // 输入更新仍使用物理屏幕单位；绘制使用界面单位。
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
        int ordinal=SoulUIPages.Index(tab);if(ordinal<0)return Rectangle.Empty;
        if(DomainMode)
        {
            if(page==0){int homeStep=(Main.screenWidth-48)/TabCount;return new(24+ordinal*homeStep,66,Math.Max(1,homeStep-8),32);}
            if(page!=0&&page!=RecoveryPage)
            {
                bool wide=Main.screenWidth>=900;int left=wide?300:24;
                int width=Math.Max(1,Main.screenWidth-left-(wide?64:48)),infoStep=width/TabCount;
                return new(left+ordinal*infoStep,wide?20:62,Math.Max(1,infoStep-8),36);
            }
            var work=DomainWorkBounds();int domainStep=work.Width/TabCount;
            return new(work.X+ordinal*domainStep,62,domainStep-5,36);
        }
        if(page==0)
        {
            float[] angles=[-90,-24,142,180];
            Vector2 c=OrbitPoint(angles[ordinal],RingRadius+62);
            return new((int)c.X-24,(int)c.Y-24,48,48);
        }
        var p=PanelBounds(); int step=(p.Width-28)/TabCount;
        return new(p.X+14+ordinal*step,p.Y+64,step-4,42);
    }
    public static Rectangle CloseBounds()
    {
        var p=PanelBounds(); return new(p.Right-46,p.Y+10,32,30);
    }
    private static int GridWidth => Math.Min(330,ContentBounds().Width);
    private static int SlotStep => (GridWidth-8)/5;
    // 动画装饰围绕固定命中框播放；打开、悬停和放入动画都不移动可点击剑位。
    public static Rectangle SlotBounds(int index)
    {
        if(DomainMode&&page==0)return ArmoryLayout.Slot(index,editing?.VisibleSupportCount??5,armoryScroll);
        if(index==SoulItem.LegendarySlotId)
        {
            Vector2 legendaryCenter=OrbitPoint(-45,RingRadius+86);
            return new((int)legendaryCenter.X-29,(int)legendaryCenter.Y-29,58,58);
        }
        // 圣林之冠双槽位于森林奖励位与传奇位之间的空档弧段（约-104°与-78°），
        // 避开下方支援弧线随数量增长可能占据的左下区域。
        if(SoulItem.IsCrownSlot(index))
        {
            Vector2 crownCenter=OrbitPoint(-104+(index-SoulItem.CrownSlotBase)*26,RingRadius+62);
            int crownSize=RingViewport.Width<420?38:44;
            return new((int)crownCenter.X-crownSize/2,(int)crownCenter.Y-crownSize/2,crownSize,crownSize);
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
            // 间距保留已保存的暂停槽，容量缩小时不挤压命中框；大组沿下弧折行。
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
        int top=76+row*(size+10)+28;
        return new(b.X+4+col*step,b.Y+top-scroll,size,size);
    }
    public static bool TryClickHost(SoulItem soul, out string error)
    {
        error="";
        if(DomainMode&&page==0&&ArmoryLayout.Compact&&armoryPane!=0)return false;
        if((page!=0 && page!=RecoveryPage) || !Main.mouseLeft || !Main.mouseLeftRelease) return false;
        Point mouse=new(Main.mouseX,Main.mouseY);
        if(!ContentBounds().Contains(mouse)) return false;
        if(page==0)
        {
            foreach(int i in soul.VisibleSlotIds)
            {
                if(!SlotBounds(i).Contains(mouse)) continue;
                SelectSlot(i); Main.mouseLeftRelease=false;
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
        foreach(int i in NavigationPages) if(TabBounds(i).Contains(mouse) && (DomainMode || page!=0 || ContentBounds().Contains(mouse)))
        { Main.mouseLeftRelease=false; SelectPage(i); return true; }
        return false;
    }
    private static bool Button(Rectangle r,string label,bool enabled=true)
    {
        Point mouse=new(Main.mouseX,Main.mouseY);
        bool hover=r.Contains(mouse) && ContentBounds().Contains(mouse);
        RoundedBox(r, enabled ? hover?new Color(62,69,72)*.9f:new Color(36,41,47)*.9f : new Color(26,28,33)*.8f,8);
        Line(new(r.X+10,r.Bottom-1),new(r.Right-10,r.Bottom-1),enabled?Accent*(hover?.55f:.22f):Color.White*.07f);
        // 按钮维持单行，按实际字宽缩放文字，避免溢出。
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
        SoulItem.IsCrownSlot(i) ? i-SoulItem.CrownSlotBase<soul.CrownCapacity :
        i<10 ? i<Math.Max(1,soul.Capacity) :
        i<15 ? i-10<soul.SupportCapacity : i<17 ? i-15<soul.BonusCapacity :
        i>=25 && SoulItem.SupportOrdinal(i)<soul.SupportCapacity;
    private static string SlotName(int i) => i==SoulItem.LegendarySlotId?"传奇剑位":SoulItem.IsCrownSlot(i)?$"圣林之冠剑位 C{i-SoulItem.CrownSlotBase+1:00}":
        i<10?$"普通剑位 {i+1:00}":i<15?$"支援剑位 S{i-9:00}":
        i<17?$"矿石奖励剑位 R{i-14:00}":i<19?$"旧普通扩展 {i-16:00} · 仅可取回":i<25?$"旧支援扩展 {i-18:00} · 仅可取回":$"支援剑位 S{SoulItem.SupportOrdinal(i)+1:00}";
    private static void Draw()
    {
        var soul=editing;if(SoulUIPages.Resolve(page,soul.HasLegacyExpansion)!=page)SelectPage(0);
        var panel=PanelBounds(); var body=ContentBounds();
        Point mouse=new(Main.mouseX,Main.mouseY);
        bool inside=panel.Contains(mouse);
        if(inside)
        {
            Main.LocalPlayer.mouseInterface=true; PlayerInput.LockVanillaMouseScroll("ZenithReborn: Soul");
            Main.HoverItem=new Item(); Main.hoverItemName="";
        }
        if(DomainMode){DrawDomainBackdrop(panel);DrawDomainHeader(mouse);if(page==RecoveryPage)DrawLibrary(soul);}
        else
        {
            DrawChrome(panel,mouse);
            Text(soul.Awakened?"天顶复生 · 众剑归位":"天顶残魂",new(panel.X+18,panel.Y+14),Ink,panel.Width<430?.86f:1f);
            if(panel.Width>=430) Text("拖动标题栏",new(panel.Right-145,panel.Y+18),WindowDragging?Accent:Muted,.6f);
            Text(soul.Attached?$"主攻 {soul.Active.Count()}/{soul.Capacity+soul.BonusCapacity+soul.CrownCapacity+(soul.LegendaryOn?1:0)}   支援 {soul.Supports.Count()}/{soul.SupportCapacity}   效能 {soul.Potency:P0}":
                "尚未附着 · 第一剑位仅供预览",new(panel.X+18,panel.Y+43),Accent,panel.Width<430?.61f:.73f);
        }
        var close=CloseBounds();
        if(close.Contains(mouse)) RoundedBox(close,new Color(112,62,65)*.62f,8);
        Text("×",new(close.X+10,close.Y+4),Ink,.88f);
        HandleNavigation(); if(editing==null) return;
        body=ContentBounds();
        if(page!=0 && !DomainMode) DrawNavigation(false);
        if(!(DomainMode&&page==0)&&page!=TraitsPage&&page!=2&&page!=EnvironmentPage&&body.Contains(mouse) && PlayerInput.ScrollWheelDelta!=0)
            scroll=Math.Clamp(scroll-PlayerInput.ScrollWheelDelta/120*48,0,Math.Max(0,contentHeight-body.Height));
        // 只裁剪可滚动主体；标题、页脚与原版提示继续使用普通批次，结束后恢复裁剪状态。
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
            int bottom=page==0?DrawHosts(soul,body,top):page==TraitsPage?DrawTraits(soul,body,top):page==2?DrawProgress(soul,body,top):page==EnvironmentPage?DrawEnvironments(soul,body,top):DrawExtended(soul,body,top);
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
            DomainMode?page==0?"选择剑位 → 剑库预览 → 确认寄宿   ·   滚轮浏览当前区域   ·   P / Esc 关闭":page==RecoveryPage?"选择旧物 → 取回背包 · P / Esc 关闭":"选择条目查看详情 · 滚轮浏览当前区域 · P / Esc 关闭":"左键交换 · 拖动标题栏移动窗口 · P 关闭";
        Paragraph(message,panel.X+16,fy+4,panel.Width-32,Main.GameUpdateCount<feedbackUntil?Color.LightSalmon:Muted,panel.Width<430?.60f:.68f);
        if(contentHeight>body.Height) Text("滚轮浏览",new(panel.Right-84,panel.Bottom-20),Muted,.60f);
    }
    private static int DrawHosts(SoulItem soul,Rectangle b,int y)
    {
        if(DomainMode)return DrawArmory(soul,b);
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
        if(!chosen.IsAir)
        {
            // 族名先于环境：先说明这是哪一族的剑，再说它吃哪个环境。
            string familyLine=WeaponFamilyTags.Line(chosen.type,soul);
            if(familyLine.Length>0)bottom=Paragraph(familyLine,b.X+18,bottom,b.Width-30,Champagne,.66f)+4;
            bottom=Paragraph(EnvironmentSwordTooltips.Label(chosen),b.X+18,bottom,b.Width-30,Accent,.66f)+12;
        }
        bottom=Section("解锁条件",SoulProgress.SlotHint(soul,selected),b.X+8,bottom,b.Width-16,Accent);
        bottom=Section("下一步",SoulProgress.Next(soul),b.X+8,bottom,b.Width-16,Accent);
        if(DomainMode)bottom=DrawDomainReturn(b,bottom);
        if(soul.HasLegacyExpansion)
        {
            if(Button(new(b.X+18,bottom,b.Width-36,34),"取回旧版本遗留武器 →"))SelectPage(RecoveryPage);
            bottom+=46;
        }
        if(Button(new(b.X+18,bottom,b.Width-36,34),"查看成长路线 / 史莱姆王记忆 →")) SelectPage(2);
        return Math.Max(bottom+48,SideInspector?ringBottom:0);
    }
    private static int DrawExtended(SoulItem soul,Rectangle b,int y)
    {
        if(Button(new(b.X+4,y,b.Width-8,30),"← 返回武器寄宿"))SelectPage(0);
        y+=38;
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

}

public class SoulSlotTooltips : GlobalItem
{
    public override void ModifyTooltips(Item item,List<TooltipLine> tooltips)
    {
        if(SoulUI.EditingSoul!=null && SoulUI.HoveredSlotRequirement.Length>0)
            tooltips.Add(new(Mod,"SlotRequirement",SoulUI.HoveredSlotRequirement));
    }
}
