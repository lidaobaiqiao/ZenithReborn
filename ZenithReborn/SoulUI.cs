using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ZenithReborn;

public class SoulUI : ModSystem
{
    private static SoulItem editing;
    private static bool showProgress;
    public static SoulItem EditingSoul => editing;
    public static string HoveredSlotRequirement { get; private set; } = "";
    public static bool IsEditing(SoulItem soul) => ReferenceEquals(editing, soul);
    public static void Open(SoulItem soul, bool progress = false)
    {
        if (!IsEditing(soul)) Toggle(soul);
        showProgress = progress;
    }
    public static void Toggle(SoulItem soul)
    {
        if (ReferenceEquals(editing, soul)) { Sync(editing); editing = null; return; }
        if (editing != null) Sync(editing);
        editing = soul; showProgress = false; Main.playerInventory = true;
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
    public override void Unload() { editing = null; HoveredSlotRequirement = ""; }
    public override void OnWorldUnload()
    {
        editing = null;
        HoveredSlotRequirement = "";
        HeroPrefixCompat.Clear();
    }
    public override void PostUpdateInput()
    {
        HoveredSlotRequirement = "";
        if (Main.gameMenu || Main.dedServ) return;
        if (ZenithReborn.OpenSoul.JustPressed && !Main.drawingPlayerChat)
        {
            if (editing != null) { Toggle(editing); return; }
            var soul = Main.LocalPlayer.HeldItem.ModItem as SoulItem ??
                Main.LocalPlayer.inventory.Select(i => i.ModItem).OfType<SoulItem>().FirstOrDefault();
            if (soul != null) Toggle(soul);
        }
        if (editing != null && (!Main.playerInventory || Main.LocalPlayer.dead ||
            !Main.LocalPlayer.inventory.Any(i => ReferenceEquals(i.ModItem, editing))))
        { Sync(editing); editing = null; }
    }
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
        layers.Insert(index < 0 ? layers.Count : index, new LegacyGameInterfaceLayer("ZenithReborn: Soul",
            () => { if (editing != null) Draw(); return true; }, InterfaceScaleType.UI));
    }
    private static void Box(Rectangle rectangle, Color color) => Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, rectangle, color);
    private static void Text(string text, Vector2 at, Color color, float scale = .85f) =>
        Utils.DrawBorderString(Main.spriteBatch, text, at, color, scale);
    // GameInterfaceLayer.Draw already calls PlayerInput.SetZoom_UI: screen and
    // mouse coordinates are both in UI units here. Do not divide by UIScale again.
    public static Rectangle PanelBounds() => new(Math.Max(8, (Main.screenWidth - 650) / 2),
        Math.Max(8, Math.Min(180, Main.screenHeight - 620)), 650, 600);
    public static Rectangle SlotBounds(int index)
    {
        var panel = PanelBounds();
        if (index < 10) return new Rectangle(panel.X + 18 + index * 61, panel.Y + 84, 54, 54);
        if (index < 15) return new Rectangle(panel.X + 268 + (index - 10) * 61, panel.Y + 184, 54, 54);
        return new Rectangle(panel.X + 18 + (index - 15) * 61, panel.Y + 184, 54, 54);
    }
    public static bool TryClickHost(SoulItem soul, out string error)
    {
        error = "";
        if (!Main.mouseLeft || !Main.mouseLeftRelease) return false;
        Point mouse = new(Main.mouseX, Main.mouseY);
        for (int i = 0; i < 17; i++)
        {
            if (!SlotBounds(i).Contains(mouse)) continue;
            Main.mouseLeftRelease = false;
            return soul.TrySwapHost(i, ref Main.mouseItem, out error);
        }
        return false;
    }
    private static void Draw()
    {
        var soul = editing;
        var bounds = PanelBounds();
        int x = bounds.X, y = bounds.Y;
        var mouse = new Point(Main.mouseX, Main.mouseY);
        if (bounds.Contains(mouse)) Main.LocalPlayer.mouseInterface = true;
        Box(bounds, new Color(17, 24, 45, 245));
        Text(soul.Awakened ? "天顶复生 · 众剑归位" : "天顶残魂 · 寄宿与解封", new(x + 18, y + 14), new Color(170,220,255), 1f);
        var close = new Rectangle(x + 598, y + 10, 36, 30);
        Text("关闭", new(close.X, close.Y + 3), Color.LightGray);
        if (close.Contains(mouse) && Main.mouseLeft && Main.mouseLeftRelease)
        { Main.mouseLeftRelease = false; Toggle(soul); return; }
        if (showProgress) { DrawProgress(soul, bounds, mouse); return; }
        Text(soul.Attached?$"祝福·附着   普通 {soul.Capacity}/10＋{soul.BonusCapacity}/2   效能 {soul.Potency:P0}":
            "残魂未稳 · 等待「附着」祝福 · 第一槽可预览武器", new(x+18,y+48),Color.White);
        Text("森林奖励·真实剑位", new(x+18,y+154), Color.LightGreen, .75f);
        Text($"支援槽 {soul.SupportCapacity}/5 · 不出主剑、不分摊伤害", new(x+268,y+154), Color.LightBlue, .72f);
        Item[] stored = soul.Stored.ToArray();
        for (int i = 0; i < 17; i++)
        {
            var slot = SlotBounds(i);
            bool unlocked = i < 10 ? i < Math.Max(1,soul.Capacity) : i < 15 ? i-10 < soul.SupportCapacity : i-15 < soul.BonusCapacity;
            Box(slot, unlocked ? i >= 15 ? new Color(40, 95, 65) : i >= 10 ? new Color(65, 65, 110) : new Color(53, 76, 114) : new Color(39, 39, 51));
            Item host = stored[i];
            if (!host.IsAir)
            {
                Main.instance.LoadItem(host.type);
                var texture = TextureAssets.Item[host.type].Value;
                var frame = Main.itemAnimations[host.type]?.GetFrame(texture) ?? texture.Frame();
                float scale = Math.Min(1, 40f / Math.Max(frame.Width, frame.Height));
                Main.spriteBatch.Draw(texture, slot.Center.ToVector2(), frame, unlocked ? (host.color == default ? Color.White : host.color) : Color.Gray,
                    0, frame.Size()*.5f, scale, SpriteEffects.None, 0);
                if (SoulProgress.ZenithWeapons.Contains(host.type))
                    Text("*",new(slot.Right-13,slot.Top+1),Color.Gold);
            }
            else Text(unlocked ? "+" : "锁", new(slot.X+18,slot.Y+16),unlocked ? Color.LightBlue : Color.Gray);
            if (!slot.Contains(mouse)) continue;
            HoveredSlotRequirement = SoulProgress.SlotHint(soul,i);
            if (!host.IsAir) { Main.HoverItem = host.Clone(); Main.hoverItemName = host.Name; }
            else Main.hoverItemName = HoveredSlotRequirement;
        }
        if (TryClickHost(soul, out string error)) { Sync(soul); SoundEngine.PlaySound(SoundID.Grab); }
        else if (error.Length > 0) Main.NewText(error,230,170,170);
        var wood = soul.Wood;
        Text($"木质族 {wood.Count}/12 · 环境效果 {wood.Strength:0.##}倍 · 生命＋{wood.Life}" +
            (wood.Full ? $" · 森林之力 {Main.LocalPlayer.GetModPlayer<WoodPlayer>().LayersFor(soul)}/12层" : ""), new(x+18,y+254), Color.LightGreen,.8f);
        string[] missing = Enumerable.Range(0,12).Where(i=>(wood.Mask&(1<<i))==0).Select(i=>WoodTypes.Names[i]).ToArray();
        Text(wood.Full ? "全套生效：两个绿色槽提供完整武器属性与主剑影。" : "缺少："+string.Join("、",missing.Take(6)),new(x+18,y+282),Color.LightGray,.74f);
        Text(wood.Full ? "奖励槽不反向凑全套；取下木剑后暂停，武器保留可取回。" : string.Join("、",missing.Skip(6)),new(x+18,y+306),Color.Gray,.72f);
        var king=AttachmentStory.FindKing(Main.LocalPlayer,soul.SoulId);
        var action=new Rectangle(x+18,y+468,614,35);
        if(king!=null)
        {
            string line=king.ai[2]>0?"祝福「附着」已经归位。让这把剑，陪你重新站起来。":
                AttachmentStory.Lines[Math.Clamp((int)king.ai[1],0,3)];
            Text("史莱姆王的回应",new(x+18,y+359),Color.Gold,.95f);
            Text(line,new(x+18,y+401),Color.White,.86f);
            if(king.ai[2]==0)
            {
                Box(action,new Color(54,87,128));
                Text(king.ai[1]<3?"继续聆听 →":"接受祝福：「附着」",new(action.X+15,action.Y+7),Color.White);
                if(action.Contains(mouse) && Main.mouseLeft && Main.mouseLeftRelease)
                { Main.mouseLeftRelease=false;AttachmentStory.RequestAdvance(king); }
            }
        }
        else if(!soul.KingMemory)
        {
            Text(soul.Attached?"旧剑魂已保留能力；可以补看史莱姆王的记忆。":"古老的凝胶正在回应史莱姆。",new(x+18,y+360),Color.LightBlue,.8f);
            Text(AttachmentStory.Requirement(Main.LocalPlayer,soul),new(x+18,y+400),Color.LightGray,.8f);
            bool ready=AttachmentStory.CanStart(Main.LocalPlayer,soul);
            Box(action,ready?new Color(54,87,128):new Color(48,49,58));
            Text(ready?"回应呼唤 · 与史莱姆王相遇":"等待回应：白天 · 地表 · 附近普通史莱姆",new(action.X+15,action.Y+7),ready?Color.White:Color.Gray,.8f);
            if(ready && action.Contains(mouse) && Main.mouseLeft && Main.mouseLeftRelease)
            { Main.mouseLeftRelease=false;AttachmentStory.RequestStart(soul); }
        }
        else
        {
            Text("「附着」永久生效。"+SoulProgress.Next(soul),new(x+18,y+360),Color.LightGreen,.8f);
            Text("魂芯："+string.Join("  ",Enumerable.Range(0,6).Select(i=>((soul.Cores&(1<<i))!=0?"●":"○")+SoulProgress.CoreNames[i])),new(x+18,y+400),Color.LightGray,.8f);
            int mechs=SoulProgress.Mechs(soul.Kills);
            Text(mechs==3?"机魂协律：连续轮换攻速与距离最多+20%。":
                $"沉睡的封印：迟滞械缓 {mechs}/3 · 单剑不累积迟滞",new(x+18,y+469),Color.LightPink,.8f);
        }
        Text("下一步："+SoulProgress.Next(soul),new(x+18,y+517),Color.LightBlue,.67f);
        var roadmap = new Rectangle(x+455,y+551,175,31);
        Box(roadmap,new Color(50,84,100));Text("成长路线 / 补办魂芯",new(roadmap.X+9,roadmap.Y+8),Color.LightGreen,.69f);
        if (roadmap.Contains(mouse) && Main.mouseLeft && Main.mouseLeftRelease)
        { Main.mouseLeftRelease=false;showProgress=true; }
        Text("左键交换；灰色暂停槽可取回。",new(x+18,y+559),Color.Gray,.72f);
    }
    private static void DrawProgress(SoulItem soul, Rectangle panel, Point mouse)
    {
        int x=panel.X,y=panel.Y;
        Text("普通槽按顺序解锁；Boss胜利必须记录在这枚剑魂上。",new(x+18,y+49),Color.LightBlue,.75f);
        for (int slot=0;slot<10;slot++)
        {
            bool done=slot<soul.Capacity;
            string condition=slot==0 ? "友善史莱姆王对话 → 接受附着" : SoulProgress.SlotGates[slot-1].Requirement;
            Text($"{(done ? "●" : "○")} 第{slot+1}槽  {condition}",new(x+18,y+83+slot*28),done?Color.LightGreen:Color.LightGray,.70f);
        }
        Text("支援1～5槽：分别达到1 / 2 / 4 / 6 / 8种有效木剑。",new(x+18,y+376),Color.LightBlue,.73f);
        Text("森林奖励2槽：全12种木剑同时寄宿，至少7普通＋5支援。",new(x+18,y+404),Color.LightGreen,.73f);
        Text("下一步："+SoulProgress.Next(soul),new(x+18,y+439),Color.White,.67f);
        int core=SoulProgress.RecoverableCore(soul);
        if (core>=0)
        {
            bool has=Main.LocalPlayer.inventory.Any(i=>i.type==SoulCore.TypeFor(core) && i.stack>0);
            var recover=new Rectangle(x+18,y+474,430,34);
            Box(recover,new Color(48,88,112));
            Text((has ? "融入" : "凭胜利铭刻补办并融入")+SoulProgress.CoreNames[core]+"魂芯",new(recover.X+10,recover.Y+8),Color.White,.77f);
            if(recover.Contains(mouse) && Main.mouseLeft && Main.mouseLeftRelease)
            { Main.mouseLeftRelease=false;SoulProgress.RequestCoreRecovery(soul); }
        }
        else Text("魂芯遗失可在此补办；没有对应胜利铭刻不能补办。",new(x+18,y+482),Color.Gray,.72f);
        Text("新角色附带凝胶信物；旧角色：凝胶10＋坠落之星1，工作台制作。",new(x+18,y+523),Color.Gray,.66f);
        var back=new Rectangle(x+456,y+551,174,31);
        Box(back,new Color(50,84,100));Text("返回寄宿界面",new(back.X+27,back.Y+7),Color.LightBlue,.75f);
        if(back.Contains(mouse) && Main.mouseLeft && Main.mouseLeftRelease) { Main.mouseLeftRelease=false;showProgress=false; }
    }
}

public class SoulSlotTooltips : GlobalItem
{
    public override void ModifyTooltips(Item item,List<TooltipLine> tooltips)
    {
        if (SoulUI.EditingSoul != null && SoulUI.HoveredSlotRequirement.Length>0)
            tooltips.Add(new(Mod,"SlotRequirement",SoulUI.HoveredSlotRequirement));
    }
}
