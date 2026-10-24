using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ZenithReborn;

namespace ZenithMaterialLab;

// 全部界面只存在于独立制作模组；使用原版字体、绘图和输入，不增加正式玩家界面。
[Autoload(Side=ModSide.Client)]
public sealed class MaterialLabSystem : ModSystem
{
    private SurfaceMaterialDefinition material;
    private LabResources resources;
    private LabPreview preview;
    private SpriteBatch batch;
    private MouseState lastMouse;
    private KeyboardState lastKeys;
    private string root,status="就绪",group="表面",dragKey;
    private int shape=1,background=1,scroll,ticks,outputMode,palette;
    private readonly int[] weapons=[ItemID.TerraBlade,ItemID.EnchantedSword,ItemID.Starfury,ItemID.Meowmere];
    private readonly string[] weaponNames=["泰拉之刃","附魔剑","星怒","彩虹猫"];
    private bool paused,orbit,autoRan,exitAfterFrame;
    private float clock;
    private readonly Stack<string> undo=new(),redo=new();
    private readonly List<(Rectangle Bounds,Action Action)> buttons=new();
    private readonly List<(Rectangle Bounds,SurfaceMaterialDefinition.Control Control)> sliders=new();
    private float scale=1;
    private bool Enabled=>Main.SavePath.Contains("material-lab-profile",StringComparison.OrdinalIgnoreCase);
    public override void Load(){Main.OnPreDraw+=BeforeDraw;Main.OnPostDraw+=AfterDraw;}
    public override void Unload()
    {
        Main.OnPreDraw-=BeforeDraw;Main.OnPostDraw-=AfterDraw;
        var r=resources;var p=preview;var b=batch;resources=null;preview=null;batch=null;
        Main.QueueMainThreadAction(()=>{r?.Dispose();p?.Dispose();b?.Dispose();});
    }
    private void Initialize()
    {
        Main.instance.Window.Title="天顶材质实验室 · 独立制作客户端";
        root=Path.Combine(Main.SavePath,"MaterialLab");Directory.CreateDirectory(root);
        material=new(){UseWeaponColor=true};material.Assets.EnvironmentMap="environment.png";
        resources=new(root);preview=new();batch=new(Main.graphics.GraphicsDevice);
        if(File.Exists(Path.Combine(root,"active.material.json")))material=SurfaceMaterialDefinition.FromJson(File.ReadAllText(Path.Combine(root,"active.material.json")));
        resources.Reload(material,background);
        Save();
    }
    private void BeforeDraw(GameTime time)
    {
        if(!Enabled||!Main.gameMenu||Main.menuMode!=0)return;
        try
        {
            if(material==null)Initialize();
            ticks++;scale=Math.Min(Main.screenWidth/1440f,Main.screenHeight/900f);
            var mouse=Mouse.GetState();var keys=Keyboard.GetState();var position=new Point((int)(mouse.X/scale),(int)(mouse.Y/scale));
            if(mouse.LeftButton==ButtonState.Pressed&&lastMouse.LeftButton==ButtonState.Released)
            {
                var slider=sliders.FirstOrDefault(x=>x.Bounds.Contains(position));
                if(slider.Control!=null){Remember();dragKey=slider.Control.Key;}
                else foreach(var button in buttons.ToArray())if(button.Bounds.Contains(position)){button.Action();break;}
            }
            if(mouse.LeftButton==ButtonState.Released)dragKey=null;
            if(dragKey!=null)
            {
                var slider=sliders.First(x=>x.Control.Key==dragKey);var c=slider.Control;
                material.Set(c.Key,MathHelper.Lerp(c.Min,c.Max,Math.Clamp((position.X-slider.Bounds.X)/(float)slider.Bounds.Width,0,1)));
            }
            if(position.X<390&&mouse.ScrollWheelValue!=lastMouse.ScrollWheelValue)scroll=Math.Max(0,scroll-(mouse.ScrollWheelValue-lastMouse.ScrollWheelValue)/120*3);
            if(keys.IsKeyDown(Keys.LeftControl)&&keys.IsKeyDown(Keys.Z)&&lastKeys.IsKeyUp(Keys.Z))Undo();
            if(keys.IsKeyDown(Keys.LeftControl)&&keys.IsKeyDown(Keys.Y)&&lastKeys.IsKeyUp(Keys.Y))Redo();
            lastMouse=mouse;lastKeys=keys;
            if(!paused)clock+=(float)time.ElapsedGameTime.TotalSeconds;
            var shown=material.Copy();if(orbit)shown.LightAzimuth=MathF.Sin(clock*.4f)*180;
            preview.Render(shown,resources.Inputs,resources.Background,shape,clock,mode:outputMode,weaponColor:SoulVisuals.Tint(weapons[palette]));
            // 先消费菜单输入，防止操作实验室按钮时点到下面的单人／多人菜单。
            Main.mouseLeft=false;Main.mouseLeftRelease=false;Main.mouseRight=false;
            string output=Environment.GetEnvironmentVariable("ZENITH_MATERIAL_LAB_CHECK");
            string artwork=Environment.GetEnvironmentVariable("ZENITH_MATERIAL_LAB_ARTCHECK");
            if(!autoRan&&!string.IsNullOrEmpty(artwork)&&ticks>=12)
            {
                autoRan=true;LabArtworkCheck.Run(artwork,root,resources,preview,material.Copy());
                resources.Reload(material,background);status="用户素材GPU对照完成";exitAfterFrame=true;
            }
            if(!autoRan&&!string.IsNullOrEmpty(output)&&ticks>=12)
            {
                autoRan=true;LabChecks.Run(output,root,resources,preview);resources.Reload(material,background);status="逐项GPU核对完成，结果已导出";
                preview.Render(material,resources.Inputs,resources.Background,shape,clock,weaponColor:SoulVisuals.Tint(weapons[palette]));exitAfterFrame=true;
            }
        }
        catch(Exception ex)
        {
            status=ex.Message;
            string output=Environment.GetEnvironmentVariable("ZENITH_MATERIAL_LAB_CHECK");
            if(string.IsNullOrEmpty(output))output=Environment.GetEnvironmentVariable("ZENITH_MATERIAL_LAB_ARTCHECK");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());exitAfterFrame=true;}
        }
    }
    private void AfterDraw(GameTime time)
    {
        if(!Enabled||!Main.gameMenu||Main.menuMode!=0||batch==null)return;
        buttons.Clear();sliders.Clear();
        var pixel=TextureAssets.MagicPixel.Value;
        batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,null,Matrix.CreateScale(scale));
        void Box(Rectangle bounds,Color color)=>batch.Draw(pixel,bounds,new Rectangle(0,0,1,1),color);
        void Text(string text,float x,float y,Color? color=null,float size=.8f)=>Utils.DrawBorderString(batch,text,new Vector2(x,y),color??Color.White,size);
        void Button(string title,Rectangle bounds,Action action)
        {
            Box(bounds,new Color(49,59,76));Text(title,bounds.X+8,bounds.Y+7,size:.75f);buttons.Add((bounds,action));
        }
        Box(new Rectangle(0,0,1440,900),new Color(18,22,29));Box(new Rectangle(10,78,380,762),new Color(27,33,43));
        Text("天顶材质实验室",22,15,size:1.12f);Text("独立制作模组 · 与正式飞剑共用材质核心",270,23,new Color(155,185,210),.75f);
        Button("加载",new(690,15,76,36),()=>Safe(()=>{Remember();material=SurfaceMaterialDefinition.FromJson(File.ReadAllText(Path.Combine(root,"active.material.json")));resources.Reload(material,background);status="已加载活动材质";}));
        Button("保存",new(774,15,76,36),()=>Safe(Save));
        Button("导出",new(858,15,76,36),()=>Safe(()=>status="已导出："+resources.Export(root,material)));
        Button("刷新素材",new(942,15,112,36),()=>Safe(()=>{resources.Reload(material,background);status="Input素材已刷新";}));
        Button("截图",new(1062,15,76,36),()=>Safe(()=>{string path=Path.Combine(root,"Snapshots",DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png");preview.Save(path);status="预览已保存："+path;}));
        Button("撤销",new(1146,15,76,36),Undo);Button("重做",new(1230,15,76,36),Redo);Button("退出",new(1314,15,100,36),()=>Main.instance.Exit());
        string[] groups=SurfaceMaterialDefinition.Controls.Select(c=>c.Group).Distinct().ToArray();
        for(int i=0;i<groups.Length;i++)
        {
            string selected=groups[i];var rect=new Rectangle(20+i%5*73,92+i/5*34,68,29);
            Button(selected,rect,()=>{group=selected;scroll=0;dragKey=null;});if(group==selected)Box(new(rect.X,rect.Bottom-3,rect.Width,3),Color.Cyan);
        }
        var controls=SurfaceMaterialDefinition.Controls.Where(c=>c.Group==group).ToArray();scroll=Math.Min(scroll,Math.Max(0,controls.Length-10));
        for(int i=scroll;i<Math.Min(controls.Length,scroll+10);i++)
        {
            var c=controls[i];int y=185+(i-scroll)*60;float value=material.Get(c.Key);
            Text(c.Label,24,y,size:.75f);Text(value.ToString("0.###"),290,y,new Color(140,220,239),.75f);
            var rect=new Rectangle(25,y+26,340,15);Box(rect,new Color(52,60,75));
            Box(new(rect.X,rect.Y,(int)(rect.Width*(value-c.Min)/(c.Max-c.Min)),rect.Height),new Color(60,143,185));sliders.Add((rect,c));
        }
        Button("玻璃",new(410,79,90,35),()=>Preset("glass"));Button("金属",new(510,79,90,35),()=>Preset("metal"));Button("涂层",new(610,79,90,35),()=>Preset("coat"));
        Button("薄膜",new(710,79,90,35),()=>Preset("film"));Button("发光",new(810,79,90,35),()=>Preset("emission"));Button("散射",new(910,79,90,35),()=>Preset("scatter"));
        Button("球体",new(1030,79,85,35),()=>shape=1);Button("平面",new(1125,79,85,35),()=>shape=0);Button("剑迹网格",new(1220,79,110,35),()=>shape=2);
        if(preview.Target!=null)batch.Draw(preview.Target,new Rectangle(410,135,1008,567),Color.White);
        Button("棋盘背景",new(410,716,118,35),()=>ChangeBackground(0));Button("校准树干",new(538,716,118,35),()=>ChangeBackground(1));Button("导入背景",new(666,716,118,35),()=>ChangeBackground(2));
        Button(paused?"继续动画":"暂停动画",new(820,716,120,35),()=>paused=!paused);Button(orbit?"停止转光":"旋转主光",new(950,716,120,35),()=>orbit=!orbit);
        Button("逐项核对",new(1080,716,120,35),()=>Safe(()=>{LabChecks.Run(Path.Combine(root,"Checks",DateTime.Now.ToString("yyyyMMdd-HHmmss")),root,resources,preview);resources.Reload(material,background);status="逐项核对已完成";}));
        Button("分区示例",new(1210,716,120,35),()=>Preset("regions"));
        for(int i=0;i<resources.MapNames.Length;i++)
        {
            int index=i;var p=material.Assets.GetType().GetProperty(resources.MapNames[i]);bool active=!string.IsNullOrEmpty((string)p.GetValue(material.Assets));
            Button((active?"✓ ":"□ ")+resources.MapLabels[i],new(410+i*125,770,120,35),()=>Safe(()=>
            {
                Remember();p.SetValue(material.Assets,active?"":resources.FileNames[index]);
                resources.Reload(material,background);
            }));
        }
        Button("图集帧数 "+material.Assets.Frames,new(410,815,148,29),()=>Safe(()=>{Remember();int[] frames=[1,4,8,16,32];material.Assets.Frames=frames[(Array.IndexOf(frames,material.Assets.Frames)+1)%frames.Length];material.Assets.Columns=Math.Min(material.Assets.Columns,material.Assets.Frames);}));
        Button("图集列数 "+material.Assets.Columns,new(568,815,148,29),()=>Safe(()=>{Remember();material.Assets.Columns=material.Assets.Columns>=Math.Min(8,material.Assets.Frames)?1:Math.Min(material.Assets.Columns*2,material.Assets.Frames);}));
        Button("放慢播放",new(726,815,115,29),()=>{material.Assets.LoopSeconds=Math.Min(30,material.Assets.LoopSeconds*1.5f);});
        Button("加快播放",new(851,815,115,29),()=>{material.Assets.LoopSeconds=Math.Max(.05f,material.Assets.LoopSeconds/1.5f);});
        string[] outputs=["最终画面","法线数据","反光数据","材质数据","透射数据"];
        Button(outputs[outputMode],new(976,815,145,29),()=>outputMode=(outputMode+1)%outputs.Length);
        Button((material.UseWeaponColor?"✓ ":"□ ")+"随剑配色",new(1131,815,136,29),()=>{Remember();material.UseWeaponColor=!material.UseWeaponColor;});
        Button(weaponNames[palette],new(1277,815,136,29),()=>palette=(palette+1)%weapons.Length);
        Text("Input存放贴图；active.material.json保存材质。散射、体积、薄膜为二维近似。",412,849,new Color(165,175,191),.70f);
        Text(status.Length>112?status[..112]+"…":status,20,873,new Color(185,215,235),.68f);
        batch.End();
        if(exitAfterFrame)
        {
            string output=Environment.GetEnvironmentVariable("ZENITH_MATERIAL_LAB_CHECK");
            if(!string.IsNullOrEmpty(output))
            {
                var device=Main.graphics.GraphicsDevice;var pp=device.PresentationParameters;var pixels=new Color[pp.BackBufferWidth*pp.BackBufferHeight];device.GetBackBufferData(pixels);
                using var screenshot=new Texture2D(device,pp.BackBufferWidth,pp.BackBufferHeight);screenshot.SetData(pixels);
                using var file=File.Create(Path.Combine(output,"workbench.png"));screenshot.SaveAsPng(file,pp.BackBufferWidth,pp.BackBufferHeight);
            }
            Main.instance.Exit();
        }
    }
    private void ChangeBackground(int value)=>Safe(()=>{background=value;resources.Reload(material,background);});
    private void Safe(Action action){try{action();}catch(Exception ex){status="操作失败："+ex.Message;}}
    private void Remember(){undo.Push(material.ToJson());redo.Clear();}
    private void Undo()=>Safe(()=>{if(undo.Count==0)return;redo.Push(material.ToJson());material=SurfaceMaterialDefinition.FromJson(undo.Pop());resources.Reload(material,background);});
    private void Redo()=>Safe(()=>{if(redo.Count==0)return;undo.Push(material.ToJson());material=SurfaceMaterialDefinition.FromJson(redo.Pop());resources.Reload(material,background);});
    private void Save()
    {
        string path=Path.Combine(root,"active.material.json"),pending=path+".pending";File.WriteAllText(pending,material.ToJson());File.Move(pending,path,true);status="活动材质已保存";
    }
    private void Preset(string name)=>Safe(()=>{Remember();material=LabChecks.Preset(name);resources.Reload(material,background);scroll=0;status="已切换预设："+name;});
}
