using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace ZenithReborn;

// 材质继承：让每把剑只写"和基础材质不同的那几个字段"，而不是复制 40 个参数。
//
// 用法：在材质 json 里加一行 "base": "Assets/Vfx/Materials/Base.json"，
// 其余字段只写要覆盖的。解析时先递归加载 base，再按字段覆盖。
// 没有 base 字段时行为与原来完全一致。
//
// 为什么不用 FromJson：定义的 JSON 选项里 UnmappedMemberHandling=Disallow，
// 多出的 "base" 字段会直接抛异常。所以这里自己做解析，并且对未知字段宽松忽略，
// 这样豆包那边加新参数时不会因为这边没同步而整份材质加载失败。
public static class MaterialInheritance
{
    private const int MaxDepth=8;

    // text：当前材质的 json 文本；readFile：按相对路径读取另一个材质 json
    public static SurfaceMaterialDefinition Resolve(string text,Func<string,string> readFile,int depth=0)
    {
        if(depth>MaxDepth)throw new InvalidOperationException("材质继承层数过深，可能存在循环引用。");
        using var document=JsonDocument.Parse(text);
        var root=document.RootElement;

        // 有 base 就先拿基础材质的完整定义，再逐字段覆盖
        SurfaceMaterialDefinition result=new();
        if(root.TryGetProperty("base",out var baseNode)&&baseNode.ValueKind==JsonValueKind.String)
        {
            string basePath=baseNode.GetString();
            if(!string.IsNullOrWhiteSpace(basePath))
                result=Resolve(readFile(basePath),readFile,depth+1);
        }

        foreach(var property in root.EnumerateObject())
        {
            if(property.Name.Equals("base",StringComparison.OrdinalIgnoreCase))continue;
            Apply(result,property.Name,property.Value);
        }
        if(depth==0)result.Validate();
        return result;
    }

    private static void Apply(SurfaceMaterialDefinition target,string name,JsonElement value)
    {
        var property=typeof(SurfaceMaterialDefinition).GetProperty(name,
            BindingFlags.Public|BindingFlags.Instance|BindingFlags.IgnoreCase);
        if(property is null||!property.CanWrite)return;   // 未知字段忽略，保持向前兼容
        Type type=property.PropertyType;

        if(type==typeof(float))property.SetValue(target,value.GetSingle());
        else if(type==typeof(int))property.SetValue(target,value.GetInt32());
        else if(type==typeof(bool))property.SetValue(target,value.GetBoolean());
        else if(type==typeof(string))property.SetValue(target,value.GetString());
        else if(type==typeof(Vector3))property.SetValue(target,ReadVector3(value));
        else if(type==typeof(List<SurfaceMaterialDefinition.DetailLayer>))
            property.SetValue(target,JsonSerializer.Deserialize<List<SurfaceMaterialDefinition.DetailLayer>>(value.GetRawText(),SurfaceMaterialDefinition.JsonOptions));
        else if(type==typeof(SurfaceMaterialDefinition.MaterialArtwork))
            ApplyArtwork((SurfaceMaterialDefinition.MaterialArtwork)property.GetValue(target)!,value);
    }

    private static void ApplyArtwork(SurfaceMaterialDefinition.MaterialArtwork target,JsonElement value)
    {
        if(value.ValueKind!=JsonValueKind.Object)return;
        foreach(var field in value.EnumerateObject())
        {
            var property=typeof(SurfaceMaterialDefinition.MaterialArtwork).GetProperty(field.Name,
                BindingFlags.Public|BindingFlags.Instance|BindingFlags.IgnoreCase);
            if(property is null||!property.CanWrite)continue;
            if(property.PropertyType==typeof(string))property.SetValue(target,field.Value.GetString());
            else if(property.PropertyType==typeof(int))property.SetValue(target,field.Value.GetInt32());
            else if(property.PropertyType==typeof(float))property.SetValue(target,field.Value.GetSingle());
        }
    }

    // 颜色同时接受 {"X":..,"Y":..,"Z":..} 和 [x,y,z] 两种写法
    private static Vector3 ReadVector3(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Array)
        {
            float x=0,y=0,z=0;int index=0;
            foreach(var item in value.EnumerateArray())
            {
                float v=item.GetSingle();
                if(index==0)x=v;else if(index==1)y=v;else if(index==2)z=v;
                index++;
            }
            return new Vector3(x,y,z);
        }
        float rx=0,ry=0,rz=0;
        if(value.TryGetProperty("X",out var px))rx=px.GetSingle();
        if(value.TryGetProperty("Y",out var py))ry=py.GetSingle();
        if(value.TryGetProperty("Z",out var pz))rz=pz.GetSingle();
        return new Vector3(rx,ry,rz);
    }
}
