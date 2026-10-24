using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace ZenithReborn;

// 正式渲染与独立制作模组共用的数据合同；这里没有玩家配置或界面。
public sealed class SurfaceMaterialDefinition
{
    public int SchemaVersion {get;set;}=1;
    public string Name {get;set;}="自有玻璃";
    public bool UseWeaponColor {get;set;}=true;
    public Vector3 BaseColor {get;set;}=Vector3.One;
    public float Metallic {get;set;}=0;
    public float Roughness {get;set;}=.08f;
    public float Ior {get;set;}=1.5f;
    public float Opacity {get;set;}=1;
    public float Transmission {get;set;}=.55f;
    public float Thickness {get;set;}=72;
    public Vector3 AbsorptionColor {get;set;}=new(.92f,.98f,1);
    public float AbsorptionDensity {get;set;}=.12f;
    public float NormalStrength {get;set;}=1;
    public float BumpStrength {get;set;}=0;
    public float Displacement {get;set;}=0;
    public float Specular {get;set;}=1;
    public float Anisotropy {get;set;}=0;
    public float AnisotropyRotation {get;set;}=0;
    public float EnvironmentStrength {get;set;}=.65f;
    public float LightStrength {get;set;}=1;
    public Vector3 LightColor {get;set;}=Vector3.One;
    public float LightAzimuth {get;set;}=-35;
    public float LightElevation {get;set;}=45;
    public Vector3 AmbientColor {get;set;}=new(.10f,.12f,.16f);
    public float ClearCoat {get;set;}=0;
    public float CoatRoughness {get;set;}=.10f;
    public float CoatIor {get;set;}=1.5f;
    public float Sheen {get;set;}=0;
    public float SheenRoughness {get;set;}=.35f;
    public Vector3 SheenColor {get;set;}=new(.8f,.9f,1);
    public float FilmWeight {get;set;}=0;
    public float FilmThickness {get;set;}=400;
    public float FilmIor {get;set;}=1.4f;
    public Vector3 EmissionColor {get;set;}=new(.25f,.55f,1);
    public float EmissionStrength {get;set;}=0;
    public float WorldLightStrength {get;set;}=0;
    public float Subsurface {get;set;}=0;
    public Vector3 SubsurfaceColor {get;set;}=new(1,.35f,.18f);
    public float SubsurfaceRadius {get;set;}=.5f;
    public float VolumeDensity {get;set;}=0;
    public Vector3 VolumeColor {get;set;}=new(.45f,.65f,.85f);
    public float VolumeAnisotropy {get;set;}=0;
    public float VolumeEmission {get;set;}=0;
    public float CrossCurvature {get;set;}=1.7f;
    public float AlongTilt {get;set;}=.8f;
    // 色散：不同波长用不同折射率，玻璃边缘才会出彩边。0 = 关闭
    public float Dispersion {get;set;}=0;
    // 屏幕空间反射强度：0 = 只用环境图，>0 = 反射近处场景（树、敌人、其他剑）
    public float SsrStrength {get;set;}=0;
    public MaterialArtwork Assets {get;set;}=new();

    // 质感细节层：整片刀身上往上叠的材质，用来增强表面质感。
    // 每层是程序化噪声（不需要贴图），可调频率、滚动、染色、混合方式。
    // 与"按 h 分区"无关——层覆盖整个带面，可以叠多层。
    public sealed class DetailLayer
    {
        public float Weight {get;set;}=0;          // 0 = 该层关闭
        public float Blend {get;set;}=0;           // 0 = 相加(发光/提亮)，1 = 相乘(压暗/磨砂)，中间值连续过渡
        public float ScaleU {get;set;}=3f;         // 沿剑迹方向的噪声频率
        public float ScaleV {get;set;}=2f;         // 沿宽度方向的噪声频率
        public float ScrollU {get;set;}=0f;        // 沿剑迹滚动速度（拉丝感）
        public float ScrollV {get;set;}=0f;        // 沿宽度滚动速度
        public Vector3 Tint {get;set;}=Vector3.One; // 该层的颜色
        public float Threshold {get;set;}=0f;      // >0 时只在亮刃附近生效（做刃口质感）
        public float RoughnessDelta {get;set;}=0; // 局部粗糙度变化，不改变整片形状
        public float MetallicDelta {get;set;}=0;  // 局部金属度变化
        public float EmissionStrength {get;set;}=0; // 局部自发光，不额外叠画轮廓
    }
    public List<DetailLayer> DetailLayers {get;set;}=new();

    public sealed class MaterialArtwork
    {
        public string ColorField {get;set;}="";
        public string MotionMask {get;set;}="";
        public string NormalMap {get;set;}="";
        public string SurfaceMap {get;set;}="";
        public string HeightMap {get;set;}="";
        public string EmissionMap {get;set;}="";
        public string EnvironmentMap {get;set;}="";
        public string Brush {get;set;}="";       // 拉丝/笔触（替换程序噪声，带手工痕迹）
        public string Noise {get;set;}="";       // 高频噪声，用于域扭曲
        public string LayerMask {get;set;}="";   // RGBA分别控制四个细节层；生产使用无损标量数据载荷
        public int Frames {get;set;}=1;
        public int Columns {get;set;}=1;
        public float LoopSeconds {get;set;}=1;
    }
    public sealed record Control(string Key,string Label,string Group,float Min,float Max);
    public static readonly JsonSerializerOptions JsonOptions=new(){IncludeFields=true,WriteIndented=true,
        UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow};
    public static readonly Control[] Controls=BuildControls();
    private static Control[] BuildControls()
    {
        var list=new List<Control>();
        void F(string key,string label,string group,float min=0,float max=1)=>list.Add(new(key,label,group,min,max));
        void C(string key,string label,string group,float max=1)
        {foreach(string axis in new[]{"X","Y","Z"})F(key+"."+axis,label+" "+(axis=="X"?"红":axis=="Y"?"绿":"蓝"),group,0,max);}
        C(nameof(BaseColor),"基础色","表面");F(nameof(Metallic),"金属度","表面");F(nameof(Roughness),"粗糙度","表面",.02f,1);
        F(nameof(Ior),"折射率","表面",1,2.5f);F(nameof(Opacity),"覆盖度","表面");F(nameof(Specular),"反射强度","表面",0,2);
        F(nameof(Anisotropy),"各向异性","表面",-.9f,.9f);F(nameof(AnisotropyRotation),"纹理方向","表面",0,360);
        F(nameof(Transmission),"透射","透射");F(nameof(Thickness),"虚拟厚度","透射",0,128);C(nameof(AbsorptionColor),"透射颜色","透射");F(nameof(AbsorptionDensity),"吸收密度","透射",0,4);
        F(nameof(SsrStrength),"屏幕反射","表面",0,2);F(nameof(Dispersion),"色散","表面",0,1);
        F(nameof(NormalStrength),"法线强度","形状",0,3);F(nameof(BumpStrength),"凹凸强度","形状",0,3);F(nameof(Displacement),"几何置换像素","形状",-32,32);
        F(nameof(CrossCurvature),"横向曲率","形状",0,4);F(nameof(AlongTilt),"沿迹倾角","形状",-2,2);
        F(nameof(EnvironmentStrength),"环境反射","照明",0,3);F(nameof(LightStrength),"主光强度","照明",0,8);
        F(nameof(LightAzimuth),"主光方位","照明",-180,180);F(nameof(LightElevation),"主光高度","照明",5,90);C(nameof(LightColor),"主光色","照明",4);C(nameof(AmbientColor),"环境色","照明");
        F(nameof(ClearCoat),"涂层权重","涂层");F(nameof(CoatRoughness),"涂层粗糙度","涂层",.02f,1);F(nameof(CoatIor),"涂层折射率","涂层",1,2.5f);
        F(nameof(Sheen),"边缘光泽","光泽");F(nameof(SheenRoughness),"光泽粗糙度","光泽",.02f,1);C(nameof(SheenColor),"光泽色","光泽");
        F(nameof(FilmWeight),"薄膜权重","薄膜");F(nameof(FilmThickness),"薄膜厚度纳米","薄膜",0,1200);F(nameof(FilmIor),"薄膜折射率","薄膜",1,2.5f);
        C(nameof(EmissionColor),"自发光色","自发光",8);F(nameof(EmissionStrength),"自发光强度","自发光",0,8);F(nameof(WorldLightStrength),"世界照明强度","自发光",0,2);
        F(nameof(Subsurface),"次表面近似","散射");C(nameof(SubsurfaceColor),"散射色","散射");F(nameof(SubsurfaceRadius),"散射范围","散射",.05f,2);
        F(nameof(VolumeDensity),"薄层体积密度","体积",0,4);C(nameof(VolumeColor),"体积散射色","体积");F(nameof(VolumeAnisotropy),"体积散射方向","体积",-.8f,.8f);F(nameof(VolumeEmission),"体积自发光","体积",0,4);
        return list.ToArray();
    }
    public float Get(string key)
    {
        var parts=key.Split('.');var property=GetType().GetProperty(parts[0])!;
        return parts.Length==1?(float)property.GetValue(this)!:(float)typeof(Vector3).GetField(parts[1])!.GetValue(property.GetValue(this))!;
    }
    public void Set(string key,float value)
    {
        var c=Controls.First(x=>x.Key==key);
        if(!float.IsFinite(value)||value<c.Min||value>c.Max)throw new ArgumentException("材质参数越界："+key);
        var parts=key.Split('.');var property=GetType().GetProperty(parts[0])!;
        if(parts.Length==1)property.SetValue(this,value);
        else{object color=property.GetValue(this)!;typeof(Vector3).GetField(parts[1])!.SetValue(color,value);property.SetValue(this,color);}
    }
    public void Validate()
    {
        if(SchemaVersion!=1||string.IsNullOrWhiteSpace(Name)||Assets==null)throw new ArgumentException("材质格式或名称无效");
        foreach(var c in Controls){float v=Get(c.Key);if(!float.IsFinite(v)||v<c.Min||v>c.Max)throw new ArgumentException("材质参数无效："+c.Key);}
        if(Assets.Frames<1||Assets.Frames>256||Assets.Columns<1||Assets.Columns>Assets.Frames||!float.IsFinite(Assets.LoopSeconds)||Assets.LoopSeconds<=0)throw new ArgumentException("材质动画图集无效");
        foreach(var p in typeof(MaterialArtwork).GetProperties().Where(p=>p.PropertyType==typeof(string)))
        {string path=(string)p.GetValue(Assets)!;if(path==null||path.Contains("..")||path.Contains(':')||path.StartsWith('/')||path.StartsWith('\\'))throw new ArgumentException("材质贴图路径无效："+p.Name);}
        if(DetailLayers==null||DetailLayers.Count>4)throw new ArgumentException("细节层最多四层");
        foreach(var layer in DetailLayers)
        {
            if(layer==null)throw new ArgumentException("细节层不能为空");
            void Range(float value,float min,float max)
            {if(!float.IsFinite(value)||value<min||value>max)throw new ArgumentException("细节层参数越界");}
            Range(layer.Weight,0,1);Range(layer.Blend,0,1);Range(layer.Threshold,0,1);
            Range(layer.ScaleU,.01f,128);Range(layer.ScaleV,.01f,128);Range(layer.ScrollU,-8,8);Range(layer.ScrollV,-8,8);
            Range(layer.Tint.X,0,4);Range(layer.Tint.Y,0,4);Range(layer.Tint.Z,0,4);
            Range(layer.RoughnessDelta,-1,1);Range(layer.MetallicDelta,-1,1);Range(layer.EmissionStrength,0,8);
        }
    }
    public string ToJson(){Validate();return JsonSerializer.Serialize(this,JsonOptions);}
    public SurfaceMaterialDefinition Copy()=>FromJson(ToJson());
    public static SurfaceMaterialDefinition FromJson(string json)
    {var value=JsonSerializer.Deserialize<SurfaceMaterialDefinition>(json,JsonOptions)??throw new ArgumentException("空材质");value.Validate();return value;}
}
