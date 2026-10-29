using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 项目自有带面着色器的接入。默认只用程序色场／噪声，没有第三方美术资源。
// 美术可逐剑绑定颜色场与动态蒙版图集，轨迹和着色器源码不需要跟着修改。
public class SwordFlowMaterial : ModSystem
{
    public sealed record Artwork(string ColorField=null,string MotionMask=null,int Frames=1,int Columns=1,float LoopSeconds=1,string SurfaceMaterial=null);
    public readonly record struct GlassOptics(float Ior,float Roughness,float Transmission,float Metallic,float BodyOpacity)
    {
        // 旧接口继续使用当前主体密度；覆盖度由绘制调用的生命周期单独控制。
        public GlassOptics(float ior,float roughness,float transmission):this(ior,roughness,transmission,0,GlassMaterialPreset.Opacity){}
        public GlassOptics(float ior,float roughness,float transmission,float metallic):this(ior,roughness,transmission,metallic,GlassMaterialPreset.Opacity){}
        public void Deconstruct(out float ior,out float roughness,out float transmission)
        {ior=Ior;roughness=Roughness;transmission=Transmission;}
        public void Deconstruct(out float ior,out float roughness,out float transmission,out float metallic)
        {ior=Ior;roughness=Roughness;transmission=Transmission;metallic=Metallic;}
    }
    public static GlassOptics DefaultOptics=>new(GlassMaterialPreset.Ior,GlassMaterialPreset.Roughness,GlassMaterialPreset.Transmission,GlassMaterialPreset.Metallic,GlassMaterialPreset.Opacity);
    private static readonly Dictionary<string,Artwork> artwork=new();
    private static readonly Dictionary<string,Texture2D> textures=new();
    private static readonly Dictionary<string,SurfaceMaterialDefinition> surfaceMaterials=new();
    private static readonly Dictionary<int,(SurfaceMaterialDefinition Material,SurfaceMaterialRenderer.Inputs Inputs)> runtimeMaterials=new();
    private static readonly Dictionary<(SurfaceMaterialDefinition,MaterialQuality.Level),SurfaceMaterialDefinition> degraded=new();
    public static bool HasSurfaceMaterials=>!VfxIsolation.PureCodeOnly&&(surfaceMaterials.Count>0||runtimeMaterials.Count>0);
    // 供制作模组或其他接入者临时预览；正式玩家界面不调用，卸载制作模组时撤销。
    public static void SetMaterialBinding(int itemType,SurfaceMaterialDefinition material,SurfaceMaterialRenderer.Inputs inputs=null)
    {
        if(material==null||VfxIsolation.PureCodeOnly){runtimeMaterials.Remove(itemType);return;}
        material.Validate();runtimeMaterials[itemType]=(material,inputs??SurfaceMaterialRenderer.LoadInputs(material));
    }
    private static Effect effect;
    private static Texture2D white;
    // 模组实例：Load 阶段 ModContent.GetInstance 可能还没有这个类型，必须先缓存下来
    private static Mod owner;
    private static Texture2D defaultBrush;   // 默认拉丝笔刷（美术出图后放这里）
    private static Texture2D defaultNoise;   // 无缝噪声（程序生成）
    // 美术出的 PBR 贴图：法线、色场、粗糙度、高度、自发光、环境
    private static readonly Dictionary<string,Texture2D> maps=new();
    // 自己创建、需要自己释放的贴图（原始数据上传的那几张）
    private static readonly List<Texture2D> ownedData=new();
    public static string EffectPath=>"ZenithReborn/Assets/Effects/ZRSwordFlow";
    public static string[] LoadedArtworkPaths=>textures.Keys.ToArray();

    public override void Load()
    {
        if(Main.dedServ)return;
        owner=Mod;
        // 在任何素材配置、纹理请求及原始通道上传之前退出，不能只关绘制开关。
        if(VfxIsolation.PureCodeOnly)return;
        using var document=JsonDocument.Parse(Mod.GetFileBytes("Assets/Vfx/Materials/SwordFlow/bindings.json"));
        foreach(var entry in document.RootElement.GetProperty("weapons").EnumerateObject())
        {
            var value=entry.Value.Deserialize<Artwork>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
            if(value==null || value.Frames<1 || value.Columns<1 || !float.IsFinite(value.LoopSeconds) || value.LoopSeconds<=0)
                throw new Exception($"剑流素材配置无效：{entry.Name}");
            artwork[entry.Name]=value;
            if(!string.IsNullOrEmpty(value.SurfaceMaterial))
            {
                // 支持 "base" 继承：每把剑只写差异字段，改基础材质即全局生效。
                // 单份材质出问题只记警告并跳过，不让整个模组加载失败。
                var owner=Mod;
                try
                {
                    surfaceMaterials[entry.Name]=MaterialInheritance.Resolve(
                        System.Text.Encoding.UTF8.GetString(owner.GetFileBytes(value.SurfaceMaterial)),
                        path=>System.Text.Encoding.UTF8.GetString(owner.GetFileBytes(path)));
                }
                catch(Exception error)
                {
                    owner.Logger.Warn($"[材质] {entry.Name} 加载失败（可能是 inherit 断链或 JSON 错误）：{error.Message}");
                }
            }
        }
        // 打包后 png 会被转成 rawimg，资源名不带扩展名，由加载器自行解析；
        // 写成 ".png" 会导致 HasAsset 失败而静默返回 null，所有贴图都不生效。
        defaultBrush=TryLoad("ZenithReborn/Assets/Vfx/Materials/SwordFlow/BrushDefault");
        defaultNoise=TryLoad("ZenithReborn/Assets/Vfx/Materials/SwordFlow/NoiseSeamless");
        // 四区遮罩必须走原始数据上传：这张图的 A 通道本身就是磨损区数据，
        // 常规贴图加载会清空 Alpha=0 处的 RGB，刃口/脊线/腹部三区会整片读成黑色。
        var loadingOwner=owner;
        Main.QueueMainThreadAction(()=>{
            // 原始数据上传直接创建显卡纹理，必须在绘制线程执行；卸载后的旧任务不能重新挂回资源。
            if(!ReferenceEquals(owner,loadingOwner))return;
            var zone=TryLoadRaw("Assets/Vfx/Materials/SwordFlow/ZoneMask.zrdata");
            if(zone!=null)maps["ZoneMask"]=zone;
            ValidateLoaded(loadingOwner);
        });
        foreach(string name in new[]{"NormalMap","ColorField","SurfaceMap","HeightMap","EmissionMap","EnvironmentMap"})
        {
            var texture=TryLoad("ZenithReborn/Assets/Vfx/Materials/SwordFlow/"+name);
            if(texture!=null)maps[name]=texture;
        }
    }

    // 把一张可选贴图绑到对应的开关与 sampler
    private static void BindMap(EffectParameterCollection parameters,string flagKey,string samplerKey,string mapName)
    {
        Texture2D texture=null;
        // 玻璃平面基线不接任何外来贴图：先做出干净的平整玻璃，再谈条带。
        if(!VfxIsolation.PureCodeOnly&&!VfxIsolation.GlassPlaneOnly)maps.TryGetValue(mapName,out texture);
        parameters[flagKey]?.SetValue(texture!=null?1f:0f);
        parameters[samplerKey]?.SetValue(texture??white);
    }

    // 安全加载可选贴图：不存在就返回 null，走程序噪声
    private static Texture2D TryLoad(string path)
    {
        try
        {
            // 此处传入包含模组名的完整资源键，必须走全局查询，不能交给只认模组内相对键的HasAsset。
            if(VfxIsolation.PureCodeOnly||owner is null||!ModContent.HasAsset(path))return null;
            return ModContent.Request<Texture2D>(path,AssetRequestMode.ImmediateLoad).Value;
        }
        catch(Exception error){owner?.Logger.Warn($"[材质] 可选贴图加载失败：{path}，{error.Message}");return null;}
    }

    // 直接上传原始 RGBA，绕开图像解码对透明像素颜色的处理。
    // 文件格式：魔数 ZRDT、版本 1、宽、高、宽*高*4 字节 RGBA。
    // 用于承载"把四个通道都当数据用"的图，这类图不能走常规贴图加载。
    private static Texture2D TryLoadRaw(string path)
    {
        if(VfxIsolation.PureCodeOnly)return null;
        if(owner is null)
        {
            Console.WriteLine("[材质] 四区遮罩：模组实例尚未就绪。");
            return null;
        }
        try
        {
            // 不先查 FileExists：模组文件表对未知扩展名的判定不可靠，
            // 直接读字节，文件不存在就让异常兜住并报出来。
            using var stream=new MemoryStream(owner.GetFileBytes(path));
            using var reader=new BinaryReader(stream);
            uint magic=reader.ReadUInt32(),version=reader.ReadUInt32();
            if(magic!=0x5444525A||version!=1)
            {
                owner.Logger.Warn($"[材质] 四区遮罩数据格式不符：magic=0x{magic:X8} version={version}");
                return null;
            }
            int width=reader.ReadInt32(),height=reader.ReadInt32();
            long expect=(long)width*height*4;
            if(width<1||height<1||width>8192||height>8192||stream.Length-stream.Position!=expect)
            {
                owner.Logger.Warn($"[材质] 四区遮罩尺寸无效：{width}x{height}，剩余 {stream.Length-stream.Position} 字节，应为 {expect}");
                return null;
            }
            var texture=new Texture2D(Main.graphics.GraphicsDevice,width,height);
            texture.SetData(reader.ReadBytes((int)expect));
            ownedData.Add(texture);
            owner.Logger.Info($"[材质] 四区遮罩已加载：{width}x{height}，原始 RGBA 直接上传。");
            return texture;
        }
        catch(Exception error)
        {
            owner.Logger.Warn($"[材质] 四区遮罩数据加载失败：{error.GetType().Name} {error.Message}");
            return null;
        }
    }

    // 启动校验：参数越界、贴图缺失、继承断链。问题写进日志，不等画面出错才发现。
    private static void ValidateLoaded(Mod owner)
    {
        // 遮罩缺失不会报错，只会让四区静默失效，所以单独查一条
        if(!maps.ContainsKey("ZoneMask"))
            owner.Logger.Warn("[材质] 四区遮罩未加载（ZoneMask.zrdata 缺失或格式错误），刃口/脊线/腹部/磨损不会生效。");
        if(surfaceMaterials.Count==0)return;
        int issues=0;
        foreach(var (name,material) in surfaceMaterials)
        {
            try{material.Validate();}
            catch(Exception error){owner.Logger.Warn($"[材质] {name} 参数越界：{error.Message}");issues++;continue;}
            var assets=material.Assets;
            string[] maps={assets.ColorField,assets.MotionMask,assets.NormalMap,assets.SurfaceMap,
                assets.HeightMap,assets.EmissionMap,assets.EnvironmentMap,assets.Brush,assets.Noise,assets.LayerMask};
            foreach(string path in maps)
            {
                if(string.IsNullOrWhiteSpace(path))continue;
                bool exists=path.EndsWith(".zrdata",StringComparison.OrdinalIgnoreCase)
                    ?owner.FileExists(path.StartsWith(owner.Name+"/")?path[(owner.Name.Length+1)..]:path):ModContent.HasAsset(path);
                if(!exists){owner.Logger.Warn($"[材质] {name} 贴图不存在：{path}");issues++;}
            }
        }
        if(issues>0)owner.Logger.Warn($"[材质] 校验完成：{issues} 个问题（共 {surfaceMaterials.Count} 份材质）。");
        else owner.Logger.Info($"[材质] 校验通过：{surfaceMaterials.Count} 份材质，无越界、无缺失贴图。");
    }

    public override void Unload()
    {
        var oldEffect=effect;var oldWhite=white;var oldBrush=defaultBrush;var oldNoise=defaultNoise;
        var rawData=ownedData.ToArray();ownedData.Clear();
        effect=null;white=null;owner=null;defaultBrush=null;defaultNoise=null;
        artwork.Clear();textures.Clear();surfaceMaterials.Clear();runtimeMaterials.Clear();degraded.Clear();
        maps.Clear();
        SwordFlowMeshBasis.Clear();
        Main.QueueMainThreadAction(()=>{oldEffect?.Dispose();oldWhite?.Dispose();foreach(var texture in rawData)texture.Dispose();});
    }
    private static Texture2D Texture(string path)
    {
        if(VfxIsolation.PureCodeOnly||string.IsNullOrWhiteSpace(path))return white;
        if(!textures.TryGetValue(path,out var value))textures[path]=value=ModContent.Request<Texture2D>(path,AssetRequestMode.ImmediateLoad).Value;
        return value;
    }
    // 降级副本：同一份材质 + 同一档位只构造一次，缓存复用，不做每帧分配
    private static SurfaceMaterialDefinition DegradedFor(SurfaceMaterialDefinition source,MaterialQuality.Level level)
    {
        var key=(source,level);
        if(degraded.TryGetValue(key,out var cached))return cached;
        var copy=SurfaceMaterialDefinition.FromJson(source.ToJson());
        float detail=MaterialQuality.DetailScale;
        // 只降"形状细节"，不碰任何直接改亮度的参数。
        // SsrStrength / Dispersion 会往表面加光，一降档亮度就变，
        // 会让同一个特效在不同存档/不同人物下亮度不一致。
        copy.NormalStrength*=detail;
        copy.BumpStrength*=detail;
        copy.Thickness*=MaterialQuality.ThicknessScale;
        if(!MaterialQuality.UseEnvironmentMaps)copy.EnvironmentStrength*=.25f;
        degraded[key]=copy;
        return copy;
    }

    public static void Draw(VertexPositionColorTexture[] vertices,int count,Color tint,float opacity,float clock,
        int? width=null,int? height=null,bool unscaled=false,float dissolve=0,int itemType=0,GlassOptics? optics=null)
    {
        if(Main.dedServ || count<3 || !float.IsFinite(opacity) || !float.IsFinite(clock) || !float.IsFinite(dissolve) || opacity<=0)return;
        var device=Main.graphics.GraphicsDevice;
        if(effect==null)
        {
            effect=ModContent.Request<Effect>(EffectPath,AssetRequestMode.ImmediateLoad).Value.Clone();
            white=new Texture2D(device,1,1);white.SetData(new[]{Color.White});
        }
        string identity=ItemLoader.GetItem(itemType)?.FullName??ItemID.Search.GetName(itemType)??"";
        artwork.TryGetValue(identity,out var art);
        if(VfxIsolation.PureCodeOnly)art=null;
        SurfaceMaterialDefinition material=null;SurfaceMaterialRenderer.Inputs materialInputs=null;
        if(!VfxIsolation.PureCodeOnly&&runtimeMaterials.TryGetValue(itemType,out var bound)){material=bound.Material;materialInputs=bound.Inputs;}
        else if(!VfxIsolation.PureCodeOnly&&surfaceMaterials.TryGetValue(identity,out var stored)){material=stored;materialInputs=SurfaceMaterialRenderer.LoadInputs(stored);}
        if(material!=null)
        {
            var quality=MaterialQuality.Current;
            if(quality!=MaterialQuality.Level.Full)material=DegradedFor(material,quality);
            SurfaceMaterialRenderer.Draw(vertices,count,material,materialInputs,SoulSceneRefraction.Scene,tint,opacity,clock,
                width??Main.screenWidth,height??Main.screenHeight,unscaled,dissolve,SoulSceneRefraction.MaterialOutputMode);
            return;
        }
        var parameters=effect.Parameters;
        var view=unscaled?Matrix.Identity:Main.GameViewMatrix.TransformationMatrix;
        parameters["transformMatrix"].SetValue(view*
            Matrix.CreateOrthographicOffCenter(0,width??Main.screenWidth,height??Main.screenHeight,0,-1,1));
        parameters["uTime"].SetValue(clock);
        // 沿用后续版本的屏幕主光方向计算，日月位置改变曲面高光；不读取固定环境反射图。
        float daylight=Math.Clamp((float)Main.time/(Main.dayTime?54000f:32400f),0,1);
        float lightAngle=MathHelper.Pi*(.18f+.64f*daylight);
        parameters["uLightDirection"]?.SetValue(Vector3.Normalize(new Vector3(-MathF.Cos(lightAngle)*.75f,-MathF.Sin(lightAngle)*.72f,.65f)));
        // 该值只表示形成、羽化与消散；玻璃主体密度不能拿来抹掉整个光学表面。
        parameters["uOpacity"].SetValue(Math.Clamp(opacity,0,1));
        parameters["uThickness"].SetValue(GlassMaterialPreset.Thickness*MaterialQuality.ThicknessScale);
        parameters["uDissolve"].SetValue(Math.Clamp(dissolve,0,1));
        var glass=optics??DefaultOptics;
        parameters["uBodyOpacity"]?.SetValue(Math.Clamp(glass.BodyOpacity,0,1));
        parameters["uIor"].SetValue(Math.Clamp(glass.Ior,1,2.5f));
        parameters["uRoughness"].SetValue(Math.Clamp(glass.Roughness,0,1));
        parameters["uMetallic"]?.SetValue(Math.Clamp(glass.Metallic,0,1));
        parameters["uTransmission"].SetValue(Math.Clamp(glass.Transmission,0,1));
        // 色散与屏幕反射不随质量档位变化：它们直接影响画面亮度
        parameters["uDispersion"]?.SetValue(Math.Clamp(GlassMaterialPreset.Dispersion,0,1));
        // 【逐项测试】这三项强度改由对应 part 开关（P21/P23/P24）在着色器里门控，
        // 这里回填生产值，保证单独打开那一项时能真正看到效果。
        parameters["uSsrStrength"]?.SetValue(Math.Clamp(GlassMaterialPreset.SsrStrength,0,2));
        parameters["uEmissionStrength"]?.SetValue(Math.Clamp(GlassMaterialPreset.EmissionStrength,0,2));
        parameters["uStripeStrength"]?.SetValue(Math.Clamp(GlassMaterialPreset.StripeStrength,0,2));
        // 贴图宽长比修正倍率：1 = 着色器按实际几何自动算，方形贴图不再被压成横条
        parameters["uMapAspectScale"]?.SetValue(1f);
        // 资源策略在加载与绑定两端生效；纯代码时只绑定运行时创建的1×1白色中性值。
        bool mapsOn=!VfxIsolation.PureCodeOnly&&!VfxIsolation.GlassPlaneOnly;
        parameters["uUseBrush"]?.SetValue(mapsOn&&defaultBrush!=null?1f:0f);
        parameters["uBrushTex"]?.SetValue(mapsOn?defaultBrush??white:white);
        parameters["uUseNoise"]?.SetValue(mapsOn&&defaultNoise!=null?1f:0f);
        parameters["uNoiseTex"]?.SetValue(mapsOn?defaultNoise??white:white);
        BindMap(parameters,"uUseNormal","uNormalTex","NormalMap");
        BindMap(parameters,"uUseSurface","uSurfaceTex","SurfaceMap");
        BindMap(parameters,"uUseHeight","uHeightTex","HeightMap");
        BindMap(parameters,"uUseEmission","uEmissionTex","EmissionMap");
        BindMap(parameters,"uUseEnvironment","uEnvironmentTex","EnvironmentMap");
        BindMap(parameters,"uUseZoneMask","uZoneMaskTex","ZoneMask");
        var scene=VfxIsolation.PureCodeOnly?null:SoulSceneRefraction.Scene;
        parameters["uScene"].SetValue(scene??white);
        // 【逐项测试】背景采样恢复生产取值，是否进入颜色由 P22 在着色器里门控。
        parameters["uHasScene"].SetValue(scene==null?0f:1f);
        // 带面构成逐项开关：集中表驱动下发，加新项只改 VfxIsolation.Parts 一处。
        for(int i=0;i<VfxIsolation.Parts.Length&&i<VfxIsolation.PartValues.Length;i++)
            parameters[VfxIsolation.Parts[i].Uniform]?.SetValue(VfxIsolation.Parts[i].Allowed?VfxIsolation.PartValues[i]:0f);
        // 液态玻璃条带起伏的强度：1 ≈ 峰值坡度 45°，折射偏移随它线性变化。
        parameters["uRidgeStrength"]?.SetValue(1f);
        parameters["uOutputRefractionMask"].SetValue(SoulSceneRefraction.BuildingMask?1f:0f);
        parameters["uOutputSurface"].SetValue(SoulSceneRefraction.BuildingSurface?1f:0f);
        parameters["uMaterialOutput"].SetValue((float)SoulSceneRefraction.MaterialOutputMode);
        parameters["uSceneTexel"].SetValue(scene==null?Vector2.One:new Vector2(1f/scene.Width,1f/scene.Height));
        parameters["uSceneUvTransform"].SetValue(SoulSceneRefraction.UvTransform);
        parameters["uSeed"].SetValue(tint.R*.007f+tint.B*.003f);
        var palette=SwordFlowPalette.From(tint);
        parameters["uBodyColor"].SetValue(tint.ToVector3()*1.25f);
        parameters["uEdgeColor"].SetValue(palette.Highlight);
        parameters["uShadeColor"].SetValue(palette.Shadow);
        parameters["uMidColor"].SetValue(palette.Mid);
        parameters["uHighlightColor"].SetValue(palette.Highlight);
        parameters["uColorField"].SetValue(Texture(art?.ColorField));
        var mask=Texture(art?.MotionMask);
        parameters["uMotionMask"].SetValue(mask);
        parameters["uUseColorField"].SetValue(string.IsNullOrWhiteSpace(art?.ColorField)?0f:1f);
        parameters["uUseMotionMask"].SetValue(string.IsNullOrWhiteSpace(art?.MotionMask)?0f:1f);
        int frames=art?.Frames??1,columns=art?.Columns??1,rows=(frames+columns-1)/columns;
        int frame=(int)(clock/(art?.LoopSeconds??1)*frames)%frames;
        // 半个纹素的内缩防止线性采样串到图集的相邻帧。
        float frameWidth=mask.Width/(float)columns,frameHeight=mask.Height/(float)rows;
        parameters["uMaskFrame"].SetValue(new Vector4((frame%columns*frameWidth+.5f)/mask.Width,
            (frame/columns*frameHeight+.5f)/mask.Height,Math.Max(0,frameWidth-1)/mask.Width,Math.Max(0,frameHeight-1)/mask.Height));
        device.BlendState=BlendState.AlphaBlend;device.DepthStencilState=DepthStencilState.None;
        device.RasterizerState=RasterizerState.CullNone;
        var smoothVertices=SwordFlowMeshBasis.Prepare(vertices,count,view);
        foreach(var pass in effect.CurrentTechnique.Passes)
        {pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,smoothVertices,0,count/3);}
    }
    public override void PostUpdateEverything()
    {
        if(Main.dedServ||Main.gameMenu||!HasSurfaceMaterials)return;
        foreach(var p in Main.ActiveProjectiles)
        {
            if(p.ModProjectile is not SoulBlade)continue;
            int type=(int)p.ai[0];SurfaceMaterialDefinition material=null;
            if(runtimeMaterials.TryGetValue(type,out var bound))material=bound.Material;
            else
            {
                string identity=ItemLoader.GetItem(type)?.FullName??ItemID.Search.GetName(type)??"";
                surfaceMaterials.TryGetValue(identity,out material);
            }
            if(material!=null&&material.WorldLightStrength>0&&material.EmissionStrength>0)
                Lighting.AddLight(p.Center,material.EmissionColor*Math.Min(2,material.EmissionStrength)*material.WorldLightStrength*p.Opacity);
        }
    }
}
